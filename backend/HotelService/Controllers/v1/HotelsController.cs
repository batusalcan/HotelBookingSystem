using HotelService.Services;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Models;

namespace HotelService.Controllers.v1;

[ApiController]
[Route("api/v1/hotels")]
public class HotelsController(IBookingService bookingService) : ControllerBase
{
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
