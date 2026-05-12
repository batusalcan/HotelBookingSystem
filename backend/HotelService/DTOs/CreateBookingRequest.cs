using System.ComponentModel.DataAnnotations;

namespace HotelService.DTOs;

public class CreateBookingRequest
{
    [Required] public Guid HotelId { get; set; }
    [Required] public Guid RoomTypeId { get; set; }
    [Required] public Guid InventoryId { get; set; }
    [Required] public DateOnly StartDate { get; set; }
    [Required] public DateOnly EndDate { get; set; }
    [Range(1, 20)] public int GuestCount { get; set; }
    [Required(AllowEmptyStrings = false), MinLength(1)] public string RowVersion { get; set; } = string.Empty;
}
