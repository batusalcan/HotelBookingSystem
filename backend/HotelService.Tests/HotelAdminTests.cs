using HotelService.Cache;
using HotelService.Data;
using HotelService.Entities;
using HotelService.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using SharedKernel.Exceptions;

namespace HotelService.Tests;

public class HotelAdminTests
{
    private sealed class NullCache : ICacheService
    {
        public Task<string?> GetAsync(string key) => Task.FromResult<string?>(null);
        public Task SetAsync(string key, string value, TimeSpan ttl) => Task.CompletedTask;
        public Task RemoveAsync(string key) => Task.CompletedTask;
    }

    private static DbContextOptions<CatalogDbContext> InMemoryOptions(string name)
        => new DbContextOptionsBuilder<CatalogDbContext>()
            .UseInMemoryDatabase(name)
            .Options;

    [Fact]
    public async Task DeleteHotel_ExistingHotel_SetsIsActiveFalse()
    {
        var opts = InMemoryOptions("delete_hotel_" + Guid.NewGuid());
        var hotelId = Guid.NewGuid();

        await using (var seed = new CatalogDbContext(opts))
        {
            seed.Hotels.Add(new Hotel
            {
                HotelId = hotelId,
                Name = "Test Hotel",
                Destination = "Istanbul",
                Latitude = 41.0m,
                Longitude = 29.0m,
                IsActive = true
            });
            await seed.SaveChangesAsync();
        }

        await using var db = new CatalogDbContext(opts);
        var service = new InventoryService(db, new NullCache(), NullLogger<InventoryService>.Instance);

        await service.DeleteHotelAsync(hotelId);

        var hotel = await db.Hotels.FindAsync(hotelId);
        Assert.NotNull(hotel);
        Assert.False(hotel.IsActive);
    }

    [Fact]
    public async Task DeleteHotel_NonExistentHotel_ThrowsNotFoundException()
    {
        var opts = InMemoryOptions("delete_missing_" + Guid.NewGuid());
        await using var db = new CatalogDbContext(opts);
        var service = new InventoryService(db, new NullCache(), NullLogger<InventoryService>.Instance);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            service.DeleteHotelAsync(Guid.NewGuid()));
    }
}
