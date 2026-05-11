using AiAgentService.Models;

namespace AiAgentService.Facade;

public interface IHotelSystemFacade
{
    /// <precondition>destination non-empty; startDate < endDate; guestCount >= 1</precondition>
    /// <postcondition>Returns available hotels from HotelService search endpoint; empty list if none found</postcondition>
    Task<List<HotelOptionDto>> SearchHotelsAsync(
        string destination, DateOnly startDate, DateOnly endDate, int guestCount, string? authToken);

    /// <precondition>hotelId and roomTypeId are valid GUIDs; startDate < endDate</precondition>
    /// <postcondition>Returns room detail including inventoryId and rowVersion token required for booking</postcondition>
    Task<RoomDetailDto> GetRoomDetailAsync(
        Guid hotelId, Guid roomTypeId, DateOnly startDate, DateOnly endDate);

    /// <precondition>contextState has all booking fields populated; authToken is a valid user JWT</precondition>
    /// <postcondition>Room booked via HotelService; returns bookingId and status</postcondition>
    Task<BookingResultDto> BookRoomAsync(ContextState contextState, string authToken);
}

public class RoomDetailDto
{
    public Guid InventoryId { get; set; }
    public string RowVersion { get; set; } = string.Empty;
}

public class BookingResultDto
{
    public Guid BookingId { get; set; }
    public string Status { get; set; } = string.Empty;
}
