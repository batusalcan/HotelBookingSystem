namespace NotificationService.Messaging;

public class NotificationFactory(
    BookingConfirmationNotification bookingConfirmation,
    LowCapacityAlertNotification lowCapacity) : INotificationFactory
{
    /// <precondition>type is a valid NotificationType enum value</precondition>
    /// <postcondition>Returns the concrete INotification implementation for the requested type</postcondition>
    public INotification Create(NotificationType type) => type switch
    {
        NotificationType.BookingConfirmation => bookingConfirmation,
        NotificationType.LowCapacity => lowCapacity,
        _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unknown notification type: {type}")
    };
}
