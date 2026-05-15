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

    /// <precondition>authToken is a valid user JWT</precondition>
    /// <postcondition>Returns all bookings for the authenticated user</postcondition>
    Task<List<AiBookingDto>> GetUserBookingsAsync(string authToken);

    /// <precondition>bookingId exists and belongs to the authenticated user; authToken is valid</precondition>
    /// <postcondition>Booking cancelled; inventory restored via HotelService</postcondition>
    Task CancelBookingAsync(Guid bookingId, string authToken);
}

public class RoomDetailDto
{
    public Guid InventoryId { get; set; }
    public uint RowVersion { get; set; }
}

public class BookingResultDto
{
    public Guid BookingId { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class AiBookingDto
{
    public Guid BookingId { get; set; }
    public string HotelName { get; set; } = string.Empty;
    public string RoomTypeName { get; set; } = string.Empty;
    public DateOnly CheckInDate { get; set; }
    public DateOnly CheckOutDate { get; set; }
    public int GuestCount { get; set; }
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}
