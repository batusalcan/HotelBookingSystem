using System.ComponentModel.DataAnnotations;

namespace HotelService.DTOs;

public class UpdateHotelRequest
{
    [MaxLength(100)] public string? Name { get; set; }
    [MaxLength(100)] public string? Destination { get; set; }
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }
    public string? ImageUrl { get; set; }
    public bool? IsActive { get; set; }
}
