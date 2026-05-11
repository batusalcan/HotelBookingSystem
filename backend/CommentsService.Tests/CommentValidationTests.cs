using CommentsService.DTOs;
using CommentsService.Services;
using SharedKernel.Exceptions;

namespace CommentsService.Tests;

public class CommentValidationTests
{
    // Guard clauses fire before any MongoDB access, so we pass null! for both dependencies.
    private static HotelCommentsService Sut() => new(null!, null!);

    private static PostCommentRequest ValidRequest(string text = "Great hotel!", double rating = 8.5) => new()
    {
        Text = text,
        Rating = rating,
        CategoryRatings = new CategoryRatingsDto
        {
            Cleanliness = 9.0,
            Staff = 8.5,
            Facilities = 8.5,
            LocationCondition = 9.0,
            EcoFriendly = 8.0
        },
        TripType = "3 nights"
    };

    [Fact]
    public async Task AddCommentAsync_EmptyUserId_ThrowsAppException()
    {
        var ex = await Assert.ThrowsAsync<AppException>(
            () => Sut().AddCommentAsync("hotel-1", "", "John", ValidRequest()));

        Assert.Contains("UserId", ex.Message);
    }

    [Fact]
    public async Task AddCommentAsync_EmptyText_ThrowsAppException()
    {
        var ex = await Assert.ThrowsAsync<AppException>(
            () => Sut().AddCommentAsync("hotel-1", "user-1", "John", ValidRequest(text: "   ")));

        Assert.Contains("Comment", ex.Message);
    }

    [Fact]
    public async Task AddCommentAsync_RatingAboveTen_ThrowsAppException()
    {
        var ex = await Assert.ThrowsAsync<AppException>(
            () => Sut().AddCommentAsync("hotel-1", "user-1", "John", ValidRequest(rating: 10.1)));

        Assert.Contains("Rating", ex.Message);
    }

    [Fact]
    public async Task AddCommentAsync_RatingBelowOne_ThrowsAppException()
    {
        var ex = await Assert.ThrowsAsync<AppException>(
            () => Sut().AddCommentAsync("hotel-1", "user-1", "John", ValidRequest(rating: 0.9)));

        Assert.Contains("Rating", ex.Message);
    }

    [Fact]
    public async Task GetCommentsAsync_PageLessThanOne_ThrowsAppException()
    {
        var ex = await Assert.ThrowsAsync<AppException>(
            () => Sut().GetCommentsAsync("hotel-1", page: 0, pageSize: 10));

        Assert.Contains("Page", ex.Message);
    }

    [Fact]
    public async Task GetCommentsAsync_PageSizeOutOfRange_ThrowsAppException()
    {
        var ex = await Assert.ThrowsAsync<AppException>(
            () => Sut().GetCommentsAsync("hotel-1", page: 1, pageSize: 101));

        Assert.Contains("PageSize", ex.Message);
    }
}
