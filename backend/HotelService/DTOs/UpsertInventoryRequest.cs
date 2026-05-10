using System.ComponentModel.DataAnnotations;

namespace HotelService.DTOs;

public class UpsertInventoryRequest
{
    [Required] public Guid HotelId { get; set; }
    [Required] public Guid RoomTypeId { get; set; }
    [Required] public DateOnly StartDate { get; set; }
    [Required] public DateOnly EndDate { get; set; }
    [Range(0, int.MaxValue)] public int AvailableCount { get; set; }
    public bool IsAvailable { get; set; } = true;
}
