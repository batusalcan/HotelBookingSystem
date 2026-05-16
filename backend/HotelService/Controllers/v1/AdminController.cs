using System.Text.Json;
using HotelService.DTOs;
using HotelService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Models;

namespace HotelService.Controllers.v1;

[ApiController]
[Route("api/v1/admin")]
[Authorize]
public class AdminController(IInventoryService inventoryService) : ControllerBase
{
    // Supabase puts roles in app_metadata.roles — decode the raw JWT to verify admin.
    private bool IsAdmin()
    {
        var auth = Request.Headers.Authorization.ToString();
        if (!auth.StartsWith("Bearer ")) return false;
        try
        {
            var seg = auth["Bearer ".Length..].Trim().Split('.')[1]
                .Replace('-', '+').Replace('_', '/');
            seg += new string('=', (4 - seg.Length % 4) % 4);
            var doc = JsonDocument.Parse(System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(seg)));
            if (!doc.RootElement.TryGetProperty("app_metadata", out var meta)) return false;
            if (!meta.TryGetProperty("roles", out var roles)) return false;
            if (roles.ValueKind == JsonValueKind.Array)
                return roles.EnumerateArray().Any(r => r.GetString() == "admin");
            if (roles.ValueKind == JsonValueKind.String)
                return roles.GetString() == "admin";
        }
        catch { }
        return false;
    }

    [HttpPost("hotels")]
    public async Task<IActionResult> CreateHotel([FromBody] CreateHotelRequest request)
    {
        if (!IsAdmin()) return Forbid();
        var hotel = await inventoryService.CreateHotelAsync(request);
        return CreatedAtAction(nameof(GetHotels), new { }, ApiResponse<object>.Ok(new { hotel.HotelId, hotel.Name }));
    }

    [HttpPut("hotels/{hotelId:guid}")]
    public async Task<IActionResult> UpdateHotel(Guid hotelId, [FromBody] UpdateHotelRequest request)
    {
        if (!IsAdmin()) return Forbid();
        var hotel = await inventoryService.UpdateHotelAsync(hotelId, request);
        return Ok(ApiResponse<object>.Ok(new { hotel.HotelId, hotel.Name }));
    }

    [HttpGet("hotels")]
    public async Task<IActionResult> GetHotels()
    {
        if (!IsAdmin()) return Forbid();
        var hotels = await inventoryService.GetAllHotelsAsync();
        return Ok(ApiResponse<object>.Ok(hotels.Select(h => new { h.HotelId, h.Name, h.Destination, h.IsActive })));
    }

    [HttpDelete("hotels/{hotelId:guid}")]
    public async Task<IActionResult> DeleteHotel(Guid hotelId)
    {
        if (!IsAdmin()) return Forbid();
        await inventoryService.DeleteHotelAsync(hotelId);
        return Ok(ApiResponse<string>.Ok("Hotel deleted successfully"));
    }

    [HttpPost("hotels/{hotelId:guid}/roomtypes")]
    public async Task<IActionResult> CreateRoomType(Guid hotelId, [FromBody] CreateRoomTypeRequest request)
    {
        if (!IsAdmin()) return Forbid();
        var roomType = await inventoryService.CreateRoomTypeAsync(hotelId, request);
        return CreatedAtAction(nameof(GetRoomTypes), new { hotelId }, ApiResponse<object>.Ok(new { roomType.RoomTypeId, roomType.TypeName }));
    }

    [HttpGet("hotels/{hotelId:guid}/roomtypes")]
    public async Task<IActionResult> GetRoomTypes(Guid hotelId)
    {
        if (!IsAdmin()) return Forbid();
        var types = await inventoryService.GetRoomTypesAsync(hotelId);
        return Ok(ApiResponse<object>.Ok(types.Select(r => new { r.RoomTypeId, r.TypeName, r.MaxGuests, r.BasePricePerNight })));
    }

    [HttpPost("inventory")]
    public async Task<IActionResult> UpsertInventory([FromBody] UpsertInventoryRequest request)
    {
        if (!IsAdmin()) return Forbid();
        await inventoryService.UpsertInventoryAsync(request);
        return Ok(ApiResponse<string>.Ok("Inventory updated successfully"));
    }

    [HttpGet("debug-auth")]
    public IActionResult DebugAuth()
    {
        var auth = Request.Headers.Authorization.ToString();
        string? error = null;
        string? appMetaRaw = null;
        bool adminResult = false;
        try
        {
            var seg = auth["Bearer ".Length..].Trim().Split('.')[1]
                .Replace('-', '+').Replace('_', '/');
            seg += new string('=', (4 - seg.Length % 4) % 4);
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(seg));
            var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("app_metadata", out var meta))
            {
                appMetaRaw = meta.GetRawText();
                if (meta.TryGetProperty("roles", out var roles) && roles.ValueKind == JsonValueKind.Array)
                    adminResult = roles.EnumerateArray().Any(r => r.GetString() == "admin");
            }
        }
        catch (Exception ex) { error = ex.Message; }

        return Ok(new
        {
            authHeaderLength = auth.Length,
            startsWithBearer = auth.StartsWith("Bearer "),
            appMetaRaw,
            adminResult,
            error,
            claims = User.Claims.Select(c => new { c.Type, c.Value }).Take(10).ToList()
        });
    }

    /// <summary>Internal endpoint for NotificationService nightly cron — not exposed via API Gateway.</summary>
    [AllowAnonymous]
    [HttpGet("hotels/capacity-report")]
    public async Task<IActionResult> GetCapacityReport([FromQuery] int days = 30)
    {
        var report = await inventoryService.GetLowCapacityReportAsync(days);
        return Ok(ApiResponse<object>.Ok(report));
    }
}
