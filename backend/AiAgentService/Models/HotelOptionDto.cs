namespace AiAgentService.Models;

public class HotelOptionDto
{
    public Guid HotelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public Guid RoomTypeId { get; set; }
    public string RoomTypeName { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public int AvailableRooms { get; set; }
    public decimal Rating { get; set; }
    public int TotalReviews { get; set; }
}
