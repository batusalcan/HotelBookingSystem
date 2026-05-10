namespace HotelService.Entities;

public class InventoryBlock
{
    public Guid InventoryId { get; set; } = Guid.NewGuid();
    public Guid RoomTypeId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int TotalCount { get; set; }
    public int AvailableCount { get; set; }
    public bool IsAvailable { get; set; } = true;

    /// <summary>EF Core optimistic concurrency token — auto-managed by SQL Server ROWVERSION.</summary>
    public byte[] RowVersion { get; set; } = [];

    public RoomType RoomType { get; set; } = null!;
}
