using Microsoft.AspNetCore.Mvc;
using NotificationService.Services;
using SharedKernel.Models;

namespace NotificationService.Controllers.v1;

[ApiController]
[Route("api/v1/notifications")]
public class NotificationController(INotificationService notificationService) : ControllerBase
{
    /// <summary>
    /// POST /api/v1/notifications/capacity-check
    /// Triggered nightly by Azure App Logic / Google Cloud Scheduler (BP-06).
    /// Calls HotelService capacity-report and dispatches admin alerts for hotels below 20% capacity.
    /// </summary>
    [HttpPost("capacity-check")]
    public async Task<IActionResult> TriggerCapacityCheck([FromQuery] int days = 30)
    {
        await notificationService.RunCapacityAlertsAsync(days);
        return Ok(ApiResponse<object>.Ok(new { message = $"Capacity check completed for next {days} days." }));
    }
}
