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
    [HttpPost]
    public async Task<IActionResult> CreateBooking([FromBody] CreateBookingRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? throw new UnauthorizedAccessException("User identity not found in token");

        var confirmation = await bookingService.CreateBookingAsync(request, userId, isAuthenticated: true);
        return Ok(ApiResponse<BookingConfirmationDto>.Ok(confirmation));
    }
}
