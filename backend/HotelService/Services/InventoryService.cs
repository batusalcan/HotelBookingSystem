using HotelService.Cache;
using HotelService.Data;
using HotelService.DTOs;
using HotelService.Entities;
using Microsoft.EntityFrameworkCore;
using Polly;
using Polly.CircuitBreaker;
using SharedKernel.Exceptions;

namespace HotelService.Services;

public class InventoryService(
    CatalogDbContext db,
    ICacheService cache,
    ILogger<InventoryService> logger,
    [FromKeyedServices("sql")] ResiliencePipeline? sqlPipeline = null) : IInventoryService
{
    private ResiliencePipeline Pipeline => sqlPipeline ?? ResiliencePipeline.Empty;

    private async Task SaveAsync(CancellationToken ct = default)
    {
        try
        {
            await Pipeline.ExecuteAsync(async token => await db.SaveChangesAsync(token), ct);
        }
        catch (BrokenCircuitException)
        {
            logger.LogError("SQL circuit breaker open — write aborted");
            throw new AppException("Database is temporarily unavailable. Please try again later.", 503);
        }
    }

    /// <precondition>request.Name and request.Destination are non-empty strings</precondition>
    /// <postcondition>Hotel record persisted in CatalogDbContext.Hotels</postcondition>
    public async Task<Hotel> CreateHotelAsync(CreateHotelRequest request)
    {
        var hotel = new Hotel
        {
            Name = request.Name,
            Destination = request.Destination,
            Latitude = request.Latitude,
            Longitude = request.Longitude,
            ImageUrl = request.ImageUrl
        };
        db.Hotels.Add(hotel);
        await SaveAsync();
        logger.LogInformation("Created hotel {HotelId} {Name}", hotel.HotelId, hotel.Name);
        return hotel;
    }

    /// <precondition>Hotel with hotelId exists in database</precondition>
    /// <postcondition>Hotel record updated; hotel:detail cache key evicted</postcondition>
    public async Task<Hotel> UpdateHotelAsync(Guid hotelId, UpdateHotelRequest request)
    {
        var hotel = await db.Hotels.FindAsync(hotelId)
            ?? throw new NotFoundException($"Hotel {hotelId} not found");

        if (request.Name is not null) hotel.Name = request.Name;
        if (request.Destination is not null) hotel.Destination = request.Destination;
        if (request.Latitude.HasValue) hotel.Latitude = request.Latitude.Value;
        if (request.Longitude.HasValue) hotel.Longitude = request.Longitude.Value;
        if (request.ImageUrl is not null) hotel.ImageUrl = request.ImageUrl;
        if (request.IsActive.HasValue) hotel.IsActive = request.IsActive.Value;

        await SaveAsync();
        await cache.RemoveAsync($"hotel:detail:{hotelId}");
        return hotel;
    }

    public async Task<IEnumerable<Hotel>> GetAllHotelsAsync()
        => await db.Hotels.Where(h => h.IsActive).OrderBy(h => h.Name).ToListAsync();

    /// <precondition>Hotel with hotelId exists; request.TypeName non-empty; request.MaxGuests >= 1; request.BasePricePerNight > 0</precondition>
    /// <postcondition>RoomType record persisted under the given hotel</postcondition>
    public async Task<RoomType> CreateRoomTypeAsync(Guid hotelId, CreateRoomTypeRequest request)
    {
        if (!await db.Hotels.AnyAsync(h => h.HotelId == hotelId))
            throw new NotFoundException($"Hotel {hotelId} not found");

        var roomType = new RoomType
        {
            HotelId = hotelId,
            TypeName = request.TypeName,
            MaxGuests = request.MaxGuests,
            BasePricePerNight = request.BasePricePerNight
        };
        db.RoomTypes.Add(roomType);
        await SaveAsync();
        return roomType;
    }

    public async Task<IEnumerable<RoomType>> GetRoomTypesAsync(Guid hotelId)
        => await db.RoomTypes.Where(r => r.HotelId == hotelId).ToListAsync();

    /// <precondition>request.StartDate < request.EndDate AND request.AvailableCount >= 0</precondition>
    /// <postcondition>
    /// InventoryBlock upserted in SQL.
    /// On CREATE: TotalCount = AvailableCount (set once from admin "Oda Adedi" input, immutable after).
    /// On UPDATE: only AvailableCount and IsAvailable change; TotalCount preserved.
    /// hotel:detail:{hotelId} cache key evicted from Redis.
    /// </postcondition>
    public async Task UpsertInventoryAsync(UpsertInventoryRequest request)
    {
        if (request.StartDate >= request.EndDate)
            throw new AppException("StartDate must be before EndDate", 400);
        if (request.AvailableCount < 0)
            throw new AppException("AvailableCount cannot be negative", 400);

        var roomType = await db.RoomTypes
            .FirstOrDefaultAsync(r => r.RoomTypeId == request.RoomTypeId && r.HotelId == request.HotelId)
            ?? throw new NotFoundException($"RoomType {request.RoomTypeId} not found for hotel {request.HotelId}");

        var existing = await db.InventoryBlocks
            .FirstOrDefaultAsync(i => i.RoomTypeId == request.RoomTypeId
                                   && i.StartDate == request.StartDate
                                   && i.EndDate == request.EndDate);

        if (existing is null)
        {
            db.InventoryBlocks.Add(new InventoryBlock
            {
                RoomTypeId = request.RoomTypeId,
                StartDate = request.StartDate,
                EndDate = request.EndDate,
                TotalCount = request.AvailableCount,   // set once on creation
                AvailableCount = request.AvailableCount,
                IsAvailable = request.IsAvailable
            });
        }
        else
        {
            existing.AvailableCount = request.AvailableCount;
            existing.IsAvailable = request.IsAvailable;
        }

        await SaveAsync();
        await cache.RemoveAsync($"hotel:detail:{request.HotelId}");
        logger.LogInformation("Upserted inventory for RoomType {RoomTypeId}", request.RoomTypeId);
    }

    /// <precondition>days >= 1</precondition>
    /// <postcondition>Returns hotels where AvailableCount/TotalCount < 0.20 within next {days} days</postcondition>
    public async Task<IEnumerable<CapacityReportItem>> GetLowCapacityReportAsync(int days)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(days));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        return await db.InventoryBlocks
            .Where(i => i.StartDate >= today && i.StartDate <= cutoff
                     && i.TotalCount > 0
                     && (double)i.AvailableCount / i.TotalCount < 0.20)
            .Select(i => new CapacityReportItem
            {
                HotelId = i.RoomType.Hotel.HotelId,
                HotelName = i.RoomType.Hotel.Name,
                RoomTypeName = i.RoomType.TypeName,
                StartDate = i.StartDate,
                EndDate = i.EndDate,
                TotalCount = i.TotalCount,
                AvailableCount = i.AvailableCount,
                CapacityRatio = (double)i.AvailableCount / i.TotalCount
            })
            .ToListAsync();
    }
}
