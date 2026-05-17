using NotificationService.Data;
using NotificationService.HttpClients;
using NotificationService.Messaging;

namespace NotificationService.Jobs;

public class CapacityAlertJob(
    IHotelServiceClient hotelClient,
    INotificationFactory factory,
    NotificationsDbContext db,
    ILogger<CapacityAlertJob> logger)
{
    /// <precondition>days >= 1; HotelService is reachable at configured BaseUrl</precondition>
    /// <postcondition>Alert records persisted to DB for every InventoryBlock where AvailableCount/TotalCount &lt; 0.20 within the next {days} days</postcondition>
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
        var alerts = new List<NotificationAlert>();

        foreach (var hotel in lowCapacityHotels)
        {
            notification.Send(hotel);
            alerts.Add(new NotificationAlert
            {
                HotelId = hotel.HotelId,
                HotelName = hotel.HotelName,
                RoomTypeName = hotel.RoomTypeName,
                AvailableCount = hotel.AvailableCount,
                TotalCount = hotel.TotalCount,
                CapacityRatio = hotel.CapacityRatio,
                StartDate = hotel.StartDate,
                EndDate = hotel.EndDate,
            });
        }

        db.NotificationAlerts.AddRange(alerts);
        await db.SaveChangesAsync();

        logger.LogInformation("Capacity alert job completed. {Count} alert(s) saved.", alerts.Count);
    }
}
