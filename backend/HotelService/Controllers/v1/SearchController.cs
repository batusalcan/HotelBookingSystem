using HotelService.DTOs;
using HotelService.Services;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Models;

namespace HotelService.Controllers.v1;

[ApiController]
[Route("api/v1/search")]
public class SearchController(IHotelSearchService searchService) : ControllerBase
{
    [HttpGet("hotels")]
    public async Task<IActionResult> SearchHotels([FromQuery] HotelSearchRequest request)
    {
        bool isAuthenticated = User.Identity?.IsAuthenticated ?? false;
        var results = await searchService.SearchAsync(request, isAuthenticated);
        return Ok(ApiResponse<object>.Ok(new
        {
            results.Page,
            results.TotalPages,
            results.TotalRecords,
            DiscountApplied = isAuthenticated,
            Data = results.Data
        }));
    }
}
