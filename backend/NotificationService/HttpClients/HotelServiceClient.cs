using System.Net.Http.Json;

namespace NotificationService.HttpClients;

public class HotelServiceClient(HttpClient http, ILogger<HotelServiceClient> logger) : IHotelServiceClient
{
    /// <precondition>days >= 1</precondition>
    /// <postcondition>Returns hotels where AvailableCount/TotalCount &lt; 0.20 for the next {days} days; returns empty list on network failure</postcondition>
    public async Task<List<LowCapacityHotelDto>> GetLowCapacityHotelsAsync(int days = 30)
    {
        try
        {
            var result = await http.GetFromJsonAsync<List<LowCapacityHotelDto>>(
                $"/api/v1/admin/hotels/capacity-report?days={days}");
            return result ?? [];
        }
        catch (Exception ex)
        {
            logger.LogWarning("Failed to fetch capacity report from HotelService: {Message}", ex.Message);
            return [];
        }
    }
}
