using System.Text.Json;
using HotelService.Cache;
using HotelService.Data;
using HotelService.DTOs;
using HotelService.Pricing;
using Microsoft.EntityFrameworkCore;
using SharedKernel.Models;

namespace HotelService.Services;

public class HotelSearchService(
    CatalogDbContext db,
    ICacheService cache,
    IConfiguration config,
    ILogger<HotelSearchService> logger) : IHotelSearchService
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    /// <precondition>request.Destination non-empty; request.StartDate < request.EndDate; request.GuestCount >= 1</precondition>
    /// <postcondition>
    /// Returns paginated hotel results filtered by IsAvailable=true AND AvailableCount>0 AND MaxGuests>=guestCount AND dates overlap.
    /// Prices are discounted 15% if isAuthenticated=true (AuthenticatedPricingStrategy), unchanged otherwise (GuestPricingStrategy).
    /// Results sourced from Redis cache if available (cache-aside), falling back to SQL and repopulating cache.
    /// </postcondition>
    public async Task<PaginatedResult<HotelSearchResult>> SearchAsync(HotelSearchRequest request, bool isAuthenticated)
    {
        IPricingStrategy pricing = isAuthenticated
            ? new AuthenticatedPricingStrategy()
            : new GuestPricingStrategy();

        // v2: prefix busts any pre-deduplication cache entries
        var cacheKey = $"v2:search:{request.Destination.ToLower()}:{request.StartDate:yyyy-MM-dd}:{request.EndDate:yyyy-MM-dd}:{request.GuestCount}";
        var cached = await cache.GetAsync(cacheKey);

        List<HotelSearchResult> allResults;

        if (cached is not null)
        {
            logger.LogInformation("Cache HIT for key {Key}", cacheKey);
            allResults = JsonSerializer.Deserialize<List<HotelSearchResult>>(cached, JsonOpts) ?? [];
        }
        else
        {
            logger.LogInformation("Cache MISS for key {Key} — querying SQL", cacheKey);
            allResults = await QuerySqlAsync(request);

            var ttlMinutes = int.Parse(config["Cache:SearchTtlMinutes"] ?? "15");
            await cache.SetAsync(cacheKey, JsonSerializer.Serialize(allResults, JsonOpts), TimeSpan.FromMinutes(ttlMinutes));
        }

        // Dedup here (not inside QuerySqlAsync) so cached data is also deduplicated
        allResults = allResults
            .GroupBy(r => r.HotelId)
            .Select(g => g.OrderBy(r => r.PricePerNight).First())
            .ToList();

        // Apply pricing strategy at response time — NEVER cache discounted prices
        var priced = allResults.Select(r => r with { PricePerNight = pricing.Apply(r.PricePerNight) }).ToList();

        var totalRecords = priced.Count;
        var paged = priced.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize);

        return PaginatedResult<HotelSearchResult>.Create(paged, totalRecords, request.Page, request.PageSize);
    }

    private async Task<List<HotelSearchResult>> QuerySqlAsync(HotelSearchRequest request)
    {
        var rows = await db.InventoryBlocks
            .Where(i => i.IsAvailable
                     && i.AvailableCount > 0
                     && i.RoomType.MaxGuests >= request.GuestCount
                     && i.StartDate <= request.EndDate
                     && i.EndDate >= request.StartDate
                     && i.RoomType.Hotel.IsActive
                     && EF.Functions.Like(i.RoomType.Hotel.Destination, $"%{request.Destination}%"))
            .Select(i => new HotelSearchResult
            {
                HotelId = i.RoomType.Hotel.HotelId,
                Name = i.RoomType.Hotel.Name,
                Location = i.RoomType.Hotel.Destination,
                Coordinates = new CoordinatesDto { Lat = i.RoomType.Hotel.Latitude, Lng = i.RoomType.Hotel.Longitude },
                ImageUrl = i.RoomType.Hotel.ImageUrl,
                Description = i.RoomType.Hotel.Description,
                RoomTypeId = i.RoomTypeId,
                RoomTypeName = i.RoomType.TypeName,
                PricePerNight = i.RoomType.BasePricePerNight,
                AvailableRooms = i.AvailableCount,
                Rating = i.RoomType.Hotel.BaseRating,
                TotalReviews = i.RoomType.Hotel.TotalReviews
            })
            .AsNoTracking()
            .ToListAsync();

        return rows;
    }
}
