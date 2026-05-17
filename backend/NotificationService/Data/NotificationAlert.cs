namespace NotificationService.Data;

public class NotificationAlert
{
    public Guid NotificationId { get; set; } = Guid.NewGuid();
    public string HotelName { get; set; } = string.Empty;
    public Guid HotelId { get; set; }
    public string RoomTypeName { get; set; } = string.Empty;
    public int AvailableCount { get; set; }
    public int TotalCount { get; set; }
    public double CapacityRatio { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; } = false;
}
