using HotelService.DTOs;
using SharedKernel.Models;

namespace HotelService.Services;

public interface IHotelSearchService
{
    Task<(PaginatedResult<HotelSearchResult> Result, bool CacheHit)> SearchAsync(HotelSearchRequest request, bool isAuthenticated);
}
