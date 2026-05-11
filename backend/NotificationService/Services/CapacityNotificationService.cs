using NotificationService.Jobs;

namespace NotificationService.Services;

public class CapacityNotificationService(CapacityAlertJob job) : INotificationService
{
    /// <precondition>days >= 1</precondition>
    /// <postcondition>All low-capacity hotels for next {days} days have been alerted via the Factory</postcondition>
    public Task RunCapacityAlertsAsync(int days = 30) => job.RunAsync(days);
}
