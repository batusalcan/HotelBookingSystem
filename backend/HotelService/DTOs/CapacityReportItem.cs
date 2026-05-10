namespace HotelService.DTOs;

public class CapacityReportItem
{
    public Guid HotelId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int TotalCount { get; set; }
    public int AvailableCount { get; set; }
    public double CapacityRatio { get; set; }
}
