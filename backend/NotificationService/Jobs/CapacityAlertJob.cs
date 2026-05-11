using NotificationService.HttpClients;
using NotificationService.Messaging;

namespace NotificationService.Jobs;

public class CapacityAlertJob(
    IHotelServiceClient hotelClient,
    INotificationFactory factory,
    ILogger<CapacityAlertJob> logger)
{
    /// <precondition>days >= 1; HotelService is reachable at configured BaseUrl</precondition>
    /// <postcondition>Admin alert dispatched for every InventoryBlock where AvailableCount/TotalCount &lt; 0.20 within the next {days} days</postcondition>
    public async Task RunAsync(int days = 30)
    {
        logger.LogInformation("Nightly capacity alert job started — checking next {Days} days", days);

        var lowCapacityHotels = await hotelClient.GetLowCapacityHotelsAsync(days);

        if (lowCapacityHotels.Count == 0)
        {
            logger.LogInformation("Capacity alert job: no hotels below 20% threshold found.");
            return;
        }

        var notification = factory.Create(NotificationType.LowCapacity);
        foreach (var hotel in lowCapacityHotels)
            notification.Send(hotel);

        logger.LogInformation("Capacity alert job completed. {Count} alert(s) dispatched.", lowCapacityHotels.Count);
    }
}
