namespace HotelService.Entities;

public class Hotel
{
    public Guid HotelId { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public decimal Latitude { get; set; }
    public decimal Longitude { get; set; }
    public decimal? BaseRating { get; set; }
    public int TotalReviews { get; set; } = 0;
    public string? ImageUrl { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<RoomType> RoomTypes { get; set; } = [];
}
