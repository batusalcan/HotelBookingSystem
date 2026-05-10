using HotelService.DTOs;
using HotelService.Services;
using SharedKernel.Exceptions;

namespace HotelService.Tests;

public class BookingValidationTests
{
    // Guard clauses execute before any DB access — so null dependencies are safe here.
    private static readonly BookingService Service = new(null!, null!, null!, null!);

    [Fact]
    public async Task CreateBooking_StartDateEqualToEndDate_ThrowsAppException()
    {
        var request = new CreateBookingRequest
        {
            HotelId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            InventoryId = Guid.NewGuid(),
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 1),
            GuestCount = 1,
            RowVersion = Convert.ToBase64String(new byte[8])
        };

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            Service.CreateBookingAsync(request, "user-1", isAuthenticated: true));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("StartDate", ex.Message);
    }

    [Fact]
    public async Task CreateBooking_StartDateAfterEndDate_ThrowsAppException()
    {
        var request = new CreateBookingRequest
        {
            HotelId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            InventoryId = Guid.NewGuid(),
            StartDate = new DateOnly(2026, 6, 5),
            EndDate = new DateOnly(2026, 6, 1),
            GuestCount = 1,
            RowVersion = Convert.ToBase64String(new byte[8])
        };

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            Service.CreateBookingAsync(request, "user-1", isAuthenticated: true));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task CreateBooking_GuestCountZero_ThrowsAppException()
    {
        var request = new CreateBookingRequest
        {
            HotelId = Guid.NewGuid(),
            RoomTypeId = Guid.NewGuid(),
            InventoryId = Guid.NewGuid(),
            StartDate = new DateOnly(2026, 6, 1),
            EndDate = new DateOnly(2026, 6, 5),
            GuestCount = 0,
            RowVersion = Convert.ToBase64String(new byte[8])
        };

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            Service.CreateBookingAsync(request, "user-1", isAuthenticated: true));

        Assert.Equal(400, ex.StatusCode);
        Assert.Contains("GuestCount", ex.Message);
    }
}
