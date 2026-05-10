namespace HotelService.DTOs;

public record HotelSearchResult
{
    public Guid HotelId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public CoordinatesDto Coordinates { get; set; } = new();
    public Guid RoomTypeId { get; set; }
    public string RoomTypeName { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public int AvailableRooms { get; set; }
    public decimal? Rating { get; set; }
    public int TotalReviews { get; set; }
}

public record CoordinatesDto
{
    public decimal Lat { get; set; }
    public decimal Lng { get; set; }
}
