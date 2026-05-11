using NotificationService.HttpClients;

namespace NotificationService.Messaging;

public class LowCapacityAlertNotification(ILogger<LowCapacityAlertNotification> logger) : INotification
{
    /// <precondition>payload is a non-null LowCapacityHotelDto</precondition>
    /// <postcondition>Low-capacity admin alert logged to console</postcondition>
    public void Send(object payload)
    {
        if (payload is not LowCapacityHotelDto hotel) return;

        logger.LogWarning(
            "[LOW CAPACITY ALERT] Hotel={HotelName} (Id={HotelId}) | RoomType={RoomType} | " +
            "Capacity={Ratio:P0} ({Available}/{Total}) | Period={Start} → {End}",
            hotel.HotelName, hotel.HotelId, hotel.RoomTypeName,
            hotel.CapacityRatio, hotel.AvailableCount, hotel.TotalCount,
            hotel.StartDate, hotel.EndDate);
    }
}
