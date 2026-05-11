namespace NotificationService.Services;

public interface INotificationService
{
    Task RunCapacityAlertsAsync(int days = 30);
}
