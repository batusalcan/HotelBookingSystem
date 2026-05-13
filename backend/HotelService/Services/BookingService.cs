using HotelService.Cache;
using HotelService.Data;
using HotelService.DTOs;
using HotelService.Entities;
using HotelService.Messaging;
using HotelService.Pricing;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.CircuitBreaker;
using SharedKernel.Events;
using SharedKernel.Exceptions;

namespace HotelService.Services;

public class BookingService(
    CatalogDbContext catalogDb,
    BookingDbContext bookingDb,
    IRabbitMqPublisher publisher,
    ILogger<BookingService> logger,
    [FromKeyedServices("sql")] ResiliencePipeline? sqlPipeline = null) : IBookingService
{
    /// <precondition>Hotel with hotelId exists; RoomType with roomTypeId belongs to that hotel</precondition>
    /// <postcondition>Returns RoomDetailDto with current RowVersion token for optimistic concurrency.
    /// If startDate and endDate are provided, the returned block must fully cover the requested stay.</postcondition>
    public async Task<RoomDetailDto> GetRoomDetailAsync(Guid hotelId, Guid roomTypeId, DateOnly? startDate = null, DateOnly? endDate = null)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var query = catalogDb.InventoryBlocks
            .Where(i => i.RoomTypeId == roomTypeId
                     && i.RoomType.HotelId == hotelId
                     && i.IsAvailable
                     && i.AvailableCount > 0
                     && i.EndDate >= today);

        // When dates are provided, the block must fully cover the requested stay
        if (startDate.HasValue && endDate.HasValue)
            query = query.Where(i => i.StartDate <= startDate.Value && i.EndDate >= endDate.Value);

        var block = await query
            .OrderBy(i => i.StartDate)
            .Include(i => i.RoomType)
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException($"No available inventory found for room type {roomTypeId} in hotel {hotelId}");

        var xmin = catalogDb.Entry(block).Property<uint>("xmin").CurrentValue;

        return new RoomDetailDto
        {
            RoomTypeId = roomTypeId,
            HotelId = hotelId,
            RoomType = block.RoomType.TypeName,
            PricePerNight = block.RoomType.BasePricePerNight,
            AvailableCount = block.AvailableCount,
            InventoryId = block.InventoryId,
            RowVersion = xmin
        };
    }

    /// <precondition>
    /// Valid JWT AND rowVersion provided AND StartDate &lt; EndDate AND GuestCount >= 1
    /// AND InventoryBlock with matching rowVersion exists and has AvailableCount > 0
    /// </precondition>
    /// <postcondition>
    /// AvailableCount -= 1 in InventoryBlocks (SQL) AND Booking record created in BookingDbContext
    /// AND ReservationCreatedEvent published to RabbitMQ.
    /// HTTP 409 Conflict returned if rowVersion mismatch (overbooking prevented).
    /// </postcondition>
    public async Task<BookingConfirmationDto> CreateBookingAsync(
        CreateBookingRequest request,
        string userId,
        bool isAuthenticated)
    {
        if (request.StartDate >= request.EndDate)
            throw new AppException("StartDate must be before EndDate", 400);
        if (request.GuestCount < 1)
            throw new AppException("GuestCount must be at least 1", 400);

        var block = await catalogDb.InventoryBlocks
            .Include(i => i.RoomType).ThenInclude(r => r.Hotel)
            .FirstOrDefaultAsync(i => i.InventoryId == request.InventoryId)
            ?? throw new NotFoundException($"InventoryBlock {request.InventoryId} not found");

        // Verify that the requested stay falls entirely within the inventory block's date range
        if (request.StartDate < block.StartDate || request.EndDate > block.EndDate)
            throw new AppException(
                $"Requested dates ({request.StartDate} – {request.EndDate}) are not covered by inventory block " +
                $"(valid {block.StartDate} – {block.EndDate})", 400);

        // Set the client-provided xmin as original value — EF Core/Npgsql will include it in WHERE clause
        catalogDb.Entry(block).Property<uint>("xmin").OriginalValue = request.RowVersion;

        block.AvailableCount -= 1;
        if (block.AvailableCount == 0) block.IsAvailable = false;

        var pipeline = sqlPipeline ?? ResiliencePipeline.Empty;
        try
        {
            await pipeline.ExecuteAsync(async ct => await catalogDb.SaveChangesAsync(ct));
        }
        catch (DbUpdateConcurrencyException)
        {
            logger.LogWarning("Optimistic concurrency conflict for InventoryId={InventoryId}", request.InventoryId);
            throw new ConflictException("Room capacity changed since you last viewed it. Please refresh and try again.");
        }
        catch (BrokenCircuitException)
        {
            logger.LogError("SQL circuit breaker open — booking aborted for InventoryId={InventoryId}", request.InventoryId);
            throw new AppException("Database is temporarily unavailable. Please try again later.", 503);
        }

        IPricingStrategy pricing = isAuthenticated ? new AuthenticatedPricingStrategy() : new GuestPricingStrategy();
        var nights = request.EndDate.DayNumber - request.StartDate.DayNumber;
        var totalAmount = pricing.Apply(block.RoomType.BasePricePerNight) * nights;

        var booking = new Booking
        {
            UserId = userId,
            HotelId = request.HotelId,
            RoomTypeId = request.RoomTypeId,
            CheckInDate = request.StartDate,
            CheckOutDate = request.EndDate,
            GuestCount = request.GuestCount,
            TotalAmount = totalAmount
        };

        bookingDb.Bookings.Add(booking);
        try
        {
            await pipeline.ExecuteAsync(async ct => await bookingDb.SaveChangesAsync(ct));
        }
        catch (BrokenCircuitException)
        {
            logger.LogError("SQL circuit breaker open — booking record not saved for BookingId={BookingId}", booking.BookingId);
            throw new AppException("Database is temporarily unavailable. Please try again later.", 503);
        }

        var evt = new ReservationCreatedEvent
        {
            BookingId = booking.BookingId,
            UserId = userId,
            HotelId = request.HotelId,
            HotelName = block.RoomType.Hotel.Name,
            RoomTypeId = request.RoomTypeId,
            CheckInDate = request.StartDate,
            CheckOutDate = request.EndDate,
            GuestCount = request.GuestCount,
            TotalAmount = totalAmount
        };

        await publisher.PublishReservationCreatedAsync(evt);
        logger.LogInformation("Booking {BookingId} confirmed for user {UserId}", booking.BookingId, userId);

        return new BookingConfirmationDto { BookingId = booking.BookingId, Status = booking.Status };
    }
}
