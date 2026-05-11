using CommentsService.DTOs;
using CommentsService.Models;

namespace CommentsService.Services;

public interface IHotelCommentsService
{
    /// <summary>
    /// Returns paginated reviews with category breakdown for a hotel.
    /// </summary>
    Task<object> GetCommentsAsync(string hotelId, int page, int pageSize);

    /// <summary>
    /// Appends a new authenticated review, recalculating all aggregate scores.
    /// </summary>
    Task<ReviewEntry> AddCommentAsync(string hotelId, string userId, string author, PostCommentRequest request);
}
