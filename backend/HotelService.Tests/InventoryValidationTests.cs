using HotelService.DTOs;
using HotelService.Services;
using SharedKernel.Exceptions;

namespace HotelService.Tests;

public class InventoryValidationTests
{
    // Guard clauses execute before any DB access — so null dependencies are safe here.
    private static readonly InventoryService Service = new(null!, null!, null!);

    [Fact]
    public async Task UpsertInventory_StartDateEqualToEndDate_ThrowsAppException()
    {
        var request = new UpsertInventoryRequest
        {
            HotelId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 1),
            AvailableCount = 10,
            IsAvailable = true
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => Service.UpsertInventoryAsync(request));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("StartDate", ex.Message);
    }

    [Fact]
    public async Task UpsertInventory_NegativeAvailableCount_ThrowsAppException()
    {
        var request = new UpsertInventoryRequest
        {
            HotelId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 30),
            AvailableCount = -1,
            IsAvailable = true
        };

        var ex = await Assert.ThrowsAsync<AppException>(() => Service.UpsertInventoryAsync(request));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("AvailableCount", ex.Message);
    }
}
