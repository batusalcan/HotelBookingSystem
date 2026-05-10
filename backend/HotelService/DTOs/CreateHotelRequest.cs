using System.ComponentModel.DataAnnotations;

namespace HotelService.DTOs;

public class CreateHotelRequest
{
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string Destination { get; set; } = string.Empty;
    [Required] public decimal Latitude { get; set; }
    [Required] public decimal Longitude { get; set; }
    public string? ImageUrl { get; set; }
}
