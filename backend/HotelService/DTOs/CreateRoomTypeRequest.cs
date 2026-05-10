using System.ComponentModel.DataAnnotations;

namespace HotelService.DTOs;

public class CreateRoomTypeRequest
{
    [Required, MaxLength(50)] public string TypeName { get; set; } = string.Empty;
    [Range(1, 20)] public int MaxGuests { get; set; }
    [Range(0.01, double.MaxValue)] public decimal BasePricePerNight { get; set; }
}
