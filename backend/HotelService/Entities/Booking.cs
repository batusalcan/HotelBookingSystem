namespace HotelService.Entities;

public class Booking
{
    public Guid BookingId { get; set; } = Guid.NewGuid();
    public string UserId { get; set; } = string.Empty;
    public Guid HotelId { get; set; }
    public Guid RoomTypeId { get; set; }
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int GuestCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = "Confirmed";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
