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

    /// <precondition>userId is a valid non-empty user identifier from JWT</precondition>
    /// <postcondition>Returns all bookings for the user enriched with hotel and room type names from CatalogDb</postcondition>
    public async Task<List<BookingDto>> GetUserBookingsAsync(string userId)
    {
        var bookings = await bookingDb.Bookings
            .Where(b => b.UserId == userId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        if (bookings.Count == 0) return [];

        var hotelIds = bookings.Select(b => b.HotelId).Distinct().ToList();
        var roomTypeIds = bookings.Select(b => b.RoomTypeId).Distinct().ToList();

        var hotels = await catalogDb.Hotels
            .Where(h => hotelIds.Contains(h.HotelId))
            .Select(h => new { h.HotelId, h.Name })
            .ToListAsync();

        var roomTypes = await catalogDb.RoomTypes
            .Where(r => roomTypeIds.Contains(r.RoomTypeId))
            .Select(r => new { r.RoomTypeId, r.TypeName })
            .ToListAsync();

        var hotelMap = hotels.ToDictionary(h => h.HotelId, h => h.Name);
        var roomMap = roomTypes.ToDictionary(r => r.RoomTypeId, r => r.TypeName);

        return bookings.Select(b => new BookingDto
        {
            BookingId = b.BookingId,
            HotelId = b.HotelId,
            HotelName = hotelMap.GetValueOrDefault(b.HotelId, "Unknown Hotel"),
            RoomTypeName = roomMap.GetValueOrDefault(b.RoomTypeId, "Unknown Room"),
            CheckInDate = b.CheckInDate,
            CheckOutDate = b.CheckOutDate,
            GuestCount = b.GuestCount,
            TotalAmount = b.TotalAmount,
            Status = b.Status,
            CreatedAt = b.CreatedAt
        }).ToList();
    }

    /// <precondition>bookingId belongs to userId; booking Status == "Confirmed"</precondition>
    /// <postcondition>
    /// Booking Status set to "Cancelled" in BookingDb.
    /// Matching InventoryBlock.AvailableCount incremented by 1 in CatalogDb (IsAvailable restored if was 0).
    /// </postcondition>
    public async Task CancelBookingAsync(Guid bookingId, string userId)
    {
        var booking = await bookingDb.Bookings
            .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.UserId == userId)
            ?? throw new NotFoundException($"Booking {bookingId} not found.");

        if (booking.Status == "Cancelled")
            throw new AppException("Booking is already cancelled.", 400);

        booking.Status = "Cancelled";
        await bookingDb.SaveChangesAsync();

        // Restore inventory — find block covering the booking's date range
        var block = await catalogDb.InventoryBlocks
            .FirstOrDefaultAsync(i => i.RoomTypeId == booking.RoomTypeId
                && i.StartDate <= booking.CheckInDate
                && i.EndDate >= booking.CheckOutDate);

        if (block is not null)
        {
            block.AvailableCount++;
            block.IsAvailable = true;
            await catalogDb.SaveChangesAsync();
        }

        logger.LogInformation("Booking {BookingId} cancelled by user {UserId}", bookingId, userId);
    }
}
