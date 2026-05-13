namespace HotelService.DTOs;

public class RoomDetailDto
{
    public Guid RoomTypeId { get; set; }
    public Guid HotelId { get; set; }
    public string RoomType { get; set; } = string.Empty;
    public decimal PricePerNight { get; set; }
    public int AvailableCount { get; set; }
    public Guid InventoryId { get; set; }
    public uint RowVersion { get; set; }
}
