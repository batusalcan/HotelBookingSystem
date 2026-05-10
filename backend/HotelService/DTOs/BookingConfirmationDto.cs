namespace HotelService.DTOs;

public class BookingConfirmationDto
{
    public Guid BookingId { get; set; }
    public string Status { get; set; } = "Confirmed";
}
