namespace NotificationService.Messaging;

public interface INotificationFactory
{
    /// <summary>
    /// Factory Method: creates the correct INotification implementation for the given type.
    /// </summary>
    INotification Create(NotificationType type);
}
