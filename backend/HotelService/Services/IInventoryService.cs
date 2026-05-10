using HotelService.DTOs;
using HotelService.Entities;

namespace HotelService.Services;

public interface IInventoryService
{
    Task<Hotel> CreateHotelAsync(CreateHotelRequest request);
    Task<Hotel> UpdateHotelAsync(Guid hotelId, UpdateHotelRequest request);
    Task<IEnumerable<Hotel>> GetAllHotelsAsync();
    Task<RoomType> CreateRoomTypeAsync(Guid hotelId, CreateRoomTypeRequest request);
    Task<IEnumerable<RoomType>> GetRoomTypesAsync(Guid hotelId);
    Task UpsertInventoryAsync(UpsertInventoryRequest request);
    Task<IEnumerable<CapacityReportItem>> GetLowCapacityReportAsync(int days);
}
