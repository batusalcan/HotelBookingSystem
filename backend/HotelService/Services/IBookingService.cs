using HotelService.DTOs;

namespace HotelService.Services;

public interface IBookingService
{
    Task<RoomDetailDto> GetRoomDetailAsync(Guid hotelId, Guid roomTypeId, DateOnly? startDate = null, DateOnly? endDate = null);
    Task<BookingConfirmationDto> CreateBookingAsync(CreateBookingRequest request, string userId, bool isAuthenticated);
    Task<List<BookingDto>> GetUserBookingsAsync(string userId);
    Task CancelBookingAsync(Guid bookingId, string userId);
}
