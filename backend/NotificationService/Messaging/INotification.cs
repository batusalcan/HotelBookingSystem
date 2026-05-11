namespace NotificationService.Messaging;

public interface INotification
{
    void Send(object payload);
}
