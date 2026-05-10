namespace HotelService.Entities;

public class RoomType
{
    public Guid RoomTypeId { get; set; } = Guid.NewGuid();
    public Guid HotelId { get; set; }
    public string TypeName { get; set; } = string.Empty;
    public int MaxGuests { get; set; }
    public decimal BasePricePerNight { get; set; }

    public Hotel Hotel { get; set; } = null!;
    public ICollection<InventoryBlock> InventoryBlocks { get; set; } = [];
}
