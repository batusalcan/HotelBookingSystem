using HotelService.DTOs;
using HotelService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Models;

namespace HotelService.Controllers.v1;

[ApiController]
[Route("api/v1/admin")]
[Authorize(Roles = "admin")]
public class AdminController(IInventoryService inventoryService) : ControllerBase
{
    [HttpPost("hotels")]
    public async Task<IActionResult> CreateHotel([FromBody] CreateHotelRequest request)
    {
        var hotel = await inventoryService.CreateHotelAsync(request);
        return CreatedAtAction(nameof(GetHotels), new { }, ApiResponse<object>.Ok(new { hotel.HotelId, hotel.Name }));
    }

    [HttpPut("hotels/{hotelId:guid}")]
    public async Task<IActionResult> UpdateHotel(Guid hotelId, [FromBody] UpdateHotelRequest request)
    {
        var hotel = await inventoryService.UpdateHotelAsync(hotelId, request);
        return Ok(ApiResponse<object>.Ok(new { hotel.HotelId, hotel.Name }));
    }

    [HttpGet("hotels")]
    public async Task<IActionResult> GetHotels()
    {
        var hotels = await inventoryService.GetAllHotelsAsync();
        return Ok(ApiResponse<object>.Ok(hotels.Select(h => new { h.HotelId, h.Name, h.Destination, h.IsActive })));
    }

    [HttpPost("hotels/{hotelId:guid}/roomtypes")]
    public async Task<IActionResult> CreateRoomType(Guid hotelId, [FromBody] CreateRoomTypeRequest request)
    {
        var roomType = await inventoryService.CreateRoomTypeAsync(hotelId, request);
        return CreatedAtAction(nameof(GetRoomTypes), new { hotelId }, ApiResponse<object>.Ok(new { roomType.RoomTypeId, roomType.TypeName }));
    }

    [HttpGet("hotels/{hotelId:guid}/roomtypes")]
    public async Task<IActionResult> GetRoomTypes(Guid hotelId)
    {
        var types = await inventoryService.GetRoomTypesAsync(hotelId);
        return Ok(ApiResponse<object>.Ok(types.Select(r => new { r.RoomTypeId, r.TypeName, r.MaxGuests, r.BasePricePerNight })));
    }

    [HttpPost("inventory")]
    public async Task<IActionResult> UpsertInventory([FromBody] UpsertInventoryRequest request)
    {
        await inventoryService.UpsertInventoryAsync(request);
        return Ok(ApiResponse<string>.Ok("Inventory updated successfully"));
    }

    /// <summary>Internal endpoint for NotificationService nightly cron — not exposed via API Gateway.</summary>
    [HttpGet("hotels/capacity-report")]
    public async Task<IActionResult> GetCapacityReport([FromQuery] int days = 30)
    {
        var report = await inventoryService.GetLowCapacityReportAsync(days);
        return Ok(ApiResponse<object>.Ok(report));
    }
}
