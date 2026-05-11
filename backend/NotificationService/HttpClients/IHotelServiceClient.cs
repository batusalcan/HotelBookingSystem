namespace NotificationService.HttpClients;

public interface IHotelServiceClient
{
    Task<List<LowCapacityHotelDto>> GetLowCapacityHotelsAsync(int days = 30);
}
