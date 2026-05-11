using CommentsService.DTOs;
using CommentsService.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SharedKernel.Models;
using System.Security.Claims;

namespace CommentsService.Controllers.v1;

[ApiController]
[Route("api/v1/comments")]
public class CommentsController(IHotelCommentsService commentsService, ILogger<CommentsController> logger) : ControllerBase
{
    /// <summary>
    /// GET /api/v1/comments/{hotelId}?page=1&amp;pageSize=10
    /// Returns paginated reviews with overall and 5-category score breakdown.
    /// Public — no authentication required.
    /// </summary>
    [HttpGet("{hotelId}")]
    public async Task<IActionResult> GetComments(
        string hotelId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10)
    {
        var result = await commentsService.GetCommentsAsync(hotelId, page, pageSize);
        return Ok(ApiResponse<object>.Ok(result));
    }

    /// <summary>
    /// POST /api/v1/comments/{hotelId}
    /// Submits a verified review for the hotel. Requires Bearer JWT (IAM).
    /// Recalculates overallScore and all 5 categoryScores after appending.
    /// </summary>
    [HttpPost("{hotelId}")]
    [Authorize]
    public async Task<IActionResult> PostComment(string hotelId, [FromBody] PostCommentRequest request)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("sub")
            ?? string.Empty;

        var userName = User.FindFirstValue("name")
            ?? User.FindFirstValue(ClaimTypes.Name)
            ?? "Anonymous";

        logger.LogInformation("User {UserId} submitting review for hotel {HotelId}", userId, hotelId);

        var entry = await commentsService.AddCommentAsync(hotelId, userId, userName, request);
        return StatusCode(201, ApiResponse<object>.Ok(entry));
    }
}
