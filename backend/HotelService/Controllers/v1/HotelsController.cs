using HotelService.Services;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Models;

namespace HotelService.Controllers.v1;

[ApiController]
[Route("api/v1/hotels")]
public class HotelsController(IBookingService bookingService, IInventoryService inventoryService) : ControllerBase
{
    [HttpGet("{hotelId:guid}/roomtypes")]
    public async Task<IActionResult> GetRoomTypes(Guid hotelId)
    {
        var types = await inventoryService.GetRoomTypesAsync(hotelId);
        return Ok(ApiResponse<object>.Ok(types.Select(r => new { r.RoomTypeId, r.TypeName, r.MaxGuests, r.BasePricePerNight })));
    }

    /// <summary>
    /// Fetches room details and current RowVersion token required for optimistic concurrency.
    /// Optionally pass startDate and endDate to ensure the returned block covers the intended stay.
    /// Client must call this before POST /api/v1/bookings.
    /// </summary>
    [HttpGet("{hotelId:guid}/rooms/{roomTypeId:guid}")]
    public async Task<IActionResult> GetRoomDetail(
        Guid hotelId,
        Guid roomTypeId,
        [FromQuery] DateOnly? startDate = null,
        [FromQuery] DateOnly? endDate = null)
    {
        var detail = await bookingService.GetRoomDetailAsync(hotelId, roomTypeId, startDate, endDate);
        return Ok(ApiResponse<object>.Ok(detail));
    }
}
