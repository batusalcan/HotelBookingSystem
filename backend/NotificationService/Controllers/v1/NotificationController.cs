using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Services;
using SharedKernel.Models;

namespace NotificationService.Controllers.v1;

[ApiController]
[Route("api/v1/notifications")]
public class NotificationController(INotificationService notificationService, NotificationsDbContext db) : ControllerBase
{
    /// <summary>POST /api/v1/notifications/capacity-check — triggered nightly by Azure scheduler.</summary>
    [HttpPost("capacity-check")]
    public async Task<IActionResult> TriggerCapacityCheck([FromQuery] int days = 30)
    {
        await notificationService.RunCapacityAlertsAsync(days);
        return Ok(ApiResponse<object>.Ok(new { message = $"Capacity check completed for next {days} days." }));
    }

    /// <summary>GET /api/v1/notifications — returns stored capacity alerts for the admin panel.</summary>
    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
    {
        var alerts = await db.NotificationAlerts
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.NotificationId,
                a.HotelId,
                a.HotelName,
                a.RoomTypeName,
                a.AvailableCount,
                a.TotalCount,
                a.CapacityRatio,
                StartDate = a.StartDate.ToString("yyyy-MM-dd"),
                EndDate = a.EndDate.ToString("yyyy-MM-dd"),
                a.CreatedAt,
                a.IsRead,
            })
            .ToListAsync();

        return Ok(ApiResponse<object>.Ok(alerts));
    }

    /// <summary>PATCH /api/v1/notifications/{id}/read — marks a notification as read.</summary>
    [HttpPatch("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id)
    {
        var alert = await db.NotificationAlerts.FindAsync(id);
        if (alert is null) return NotFound(ApiResponse<string>.Fail("Notification not found"));
        alert.IsRead = true;
        await db.SaveChangesAsync();
        return Ok(ApiResponse<string>.Ok("Marked as read"));
    }
}
