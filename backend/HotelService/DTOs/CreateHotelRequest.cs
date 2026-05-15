using System.ComponentModel.DataAnnotations;

namespace HotelService.DTOs;

public class CreateHotelRequest
{
    [Required(AllowEmptyStrings = false), MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required(AllowEmptyStrings = false), MaxLength(100)] public string Destination { get; set; } = string.Empty;
    [Required, Range(-90.0, 90.0)] public decimal Latitude { get; set; }
    [Required, Range(-180.0, 180.0)] public decimal Longitude { get; set; }
    [MaxLength(500)] public string? ImageUrl { get; set; }
    [Range(0.0, 10.0)] public decimal? BaseRating { get; set; }
}
