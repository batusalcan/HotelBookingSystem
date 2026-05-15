using System.Security.Claims;
using HotelService.DTOs;
using HotelService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Models;

namespace HotelService.Controllers.v1;

[ApiController]
[Route("api/v1/bookings")]
[Authorize]
public class BookingsController(IBookingService bookingService) : ControllerBase
{
    private string GetUserId() =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? User.FindFirstValue("sub")
        ?? throw new UnauthorizedAccessException("User identity not found in token");

    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)
    {
        var confirmation = await bookingService.CreateBookingAsync(request, GetUserId(), isAuthenticated: true);
        return Ok(ApiResponse<BookingConfirmationDto>.Ok(confirmation));
    }

    [HttpGet]
    public async Task<IActionResult> GetUserBookings()
    {
        var bookings = await bookingService.GetUserBookingsAsync(GetUserId());
        return Ok(ApiResponse<object>.Ok(bookings));
    }

    [HttpDelete("{bookingId:guid}")]
    public async Task<IActionResult> CancelBooking(Guid bookingId)
    {
        await bookingService.CancelBookingAsync(bookingId, GetUserId());
        return Ok(ApiResponse<object>.Ok(new { message = "Booking cancelled successfully." }));
    }
}
