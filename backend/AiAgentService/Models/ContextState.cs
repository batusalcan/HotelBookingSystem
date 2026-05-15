using System.Text.Json.Serialization;

namespace AiAgentService.Models;

public class ContextState
{
    [JsonPropertyName("pendingAction")]
    public string PendingAction { get; set; } = "NONE"; // NONE | CLARIFY | BOOK

    [JsonPropertyName("destination")]
    public string? Destination { get; set; }

    [JsonPropertyName("startDate")]
    public string? StartDate { get; set; }

    [JsonPropertyName("endDate")]
    public string? EndDate { get; set; }

    [JsonPropertyName("guestCount")]
    public int? GuestCount { get; set; }

    [JsonPropertyName("targetHotelId")]
    public Guid? TargetHotelId { get; set; }

    [JsonPropertyName("targetRoomTypeId")]
    public Guid? TargetRoomTypeId { get; set; }

    [JsonPropertyName("targetInventoryId")]
    public Guid? TargetInventoryId { get; set; }

    [JsonPropertyName("rowVersion")]
    public uint? RowVersion { get; set; }

    [JsonPropertyName("hotelName")]
    public string? HotelName { get; set; }

    // Serialized List<HotelOptionDto> stored while user is choosing (PendingAction=="SELECT")
    [JsonPropertyName("hotelOptionsJson")]
    public string? HotelOptionsJson { get; set; }
}
