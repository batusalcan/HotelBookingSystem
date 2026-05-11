using CommentsService.Data;
using CommentsService.DTOs;
using CommentsService.Models;
using MongoDB.Driver;
using SharedKernel.Exceptions;

namespace CommentsService.Services;

public class HotelCommentsService(MongoDbContext db, ILogger<HotelCommentsService> logger) : IHotelCommentsService
{
    /// <summary>
    /// Gets paginated comments for a hotel.
    /// Precondition: hotelId is non-empty; page >= 1; pageSize in [1, 100].
    /// Postcondition: Returns flat JSON with categoryBreakdown + paginated comments[]. Throws NotFoundException if hotel has no review document.
    /// </summary>
    public async Task<object> GetCommentsAsync(string hotelId, int page, int pageSize)
    {
        if (string.IsNullOrWhiteSpace(hotelId)) throw new AppException("HotelId is required.");
        if (page < 1) throw new AppException("Page must be >= 1.");
        if (pageSize < 1 || pageSize > 100) throw new AppException("PageSize must be between 1 and 100.");

        var document = await db.HotelReviews
            .Find(h => h.HotelId == hotelId)
            .FirstOrDefaultAsync()
            ?? throw new NotFoundException($"No reviews found for hotel '{hotelId}'.");

        var totalReviews = document.Reviews.Count;
        var comments = document.Reviews
            .OrderByDescending(r => r.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new CommentResponseDto
            {
                ReviewId = r.ReviewId,
                Author = r.Author,
                TripType = r.TripType,
                Text = r.Text,
                Rating = r.Rating,
                Date = r.Date,
                IsVerified = r.IsVerified
            })
            .ToList();

        return new
        {
            hotelId,
            totalReviews,
            overallScore = document.OverallScore,
            categoryBreakdown = document.CategoryScores,
            page,
            totalPages = (int)Math.Ceiling((double)totalReviews / pageSize),
            comments
        };
    }

    /// <summary>
    /// Appends a new verified review for a hotel and recalculates all aggregate scores.
    /// Precondition: userId is non-empty; request.Text is non-empty; Rating in [1.0, 10.0]; all categoryRatings in [1.0, 10.0].
    /// Postcondition: Review appended to document; overallScore and all 5 categoryScores recalculated atomically in-memory then persisted. Returns the new ReviewEntry.
    /// </summary>
    public async Task<ReviewEntry> AddCommentAsync(string hotelId, string userId, string author, PostCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(userId)) throw new AppException("UserId is required.");
        if (string.IsNullOrWhiteSpace(request.Text)) throw new AppException("Comment text is required.");
        if (request.Rating < 1.0 || request.Rating > 10.0)
            throw new AppException("Rating must be between 1.0 and 10.0.");

        ValidateCategoryRatings(request.CategoryRatings);

        var newEntry = new ReviewEntry
        {
            ReviewId = Guid.NewGuid().ToString(),
            UserId = userId,
            Author = string.IsNullOrWhiteSpace(author) ? "Anonymous" : author,
            Text = request.Text.Trim(),
            Rating = Math.Round(request.Rating, 1),
            CategoryRatings = new CategoryScores
            {
                Cleanliness = Math.Round(request.CategoryRatings.Cleanliness, 1),
                Staff = Math.Round(request.CategoryRatings.Staff, 1),
                Facilities = Math.Round(request.CategoryRatings.Facilities, 1),
                LocationCondition = Math.Round(request.CategoryRatings.LocationCondition, 1),
                EcoFriendly = Math.Round(request.CategoryRatings.EcoFriendly, 1)
            },
            TripType = request.TripType?.Trim(),
            IsVerified = true,
            Date = DateTime.UtcNow
        };

        var document = await db.HotelReviews
            .Find(h => h.HotelId == hotelId)
            .FirstOrDefaultAsync();

        if (document is null)
        {
            document = new HotelReview
            {
                HotelId = hotelId,
                Reviews = [newEntry],
                TotalReviews = 1,
                OverallScore = newEntry.Rating,
                CategoryScores = newEntry.CategoryRatings
            };
            await db.HotelReviews.InsertOneAsync(document);
            logger.LogInformation("Created new review document for hotel {HotelId}", hotelId);
        }
        else
        {
            document.Reviews.Add(newEntry);
            Recalculate(document);
            await db.HotelReviews.ReplaceOneAsync(h => h.HotelId == hotelId, document);
            logger.LogInformation("Appended review to hotel {HotelId}; new total {Total}", hotelId, document.TotalReviews);
        }

        return newEntry;
    }

    private static void ValidateCategoryRatings(CategoryRatingsDto r)
    {
        if (r.Cleanliness < 1.0 || r.Cleanliness > 10.0)
            throw new AppException("Cleanliness rating must be between 1.0 and 10.0.");
        if (r.Staff < 1.0 || r.Staff > 10.0)
            throw new AppException("Staff rating must be between 1.0 and 10.0.");
        if (r.Facilities < 1.0 || r.Facilities > 10.0)
            throw new AppException("Facilities rating must be between 1.0 and 10.0.");
        if (r.LocationCondition < 1.0 || r.LocationCondition > 10.0)
            throw new AppException("LocationCondition rating must be between 1.0 and 10.0.");
        if (r.EcoFriendly < 1.0 || r.EcoFriendly > 10.0)
            throw new AppException("EcoFriendly rating must be between 1.0 and 10.0.");
    }

    private static void Recalculate(HotelReview doc)
    {
        var reviews = doc.Reviews;
        doc.TotalReviews = reviews.Count;
        doc.OverallScore = Math.Round(reviews.Average(r => r.Rating), 2);
        doc.CategoryScores = new CategoryScores
        {
            Cleanliness = Math.Round(reviews.Average(r => r.CategoryRatings.Cleanliness), 2),
            Staff = Math.Round(reviews.Average(r => r.CategoryRatings.Staff), 2),
            Facilities = Math.Round(reviews.Average(r => r.CategoryRatings.Facilities), 2),
            LocationCondition = Math.Round(reviews.Average(r => r.CategoryRatings.LocationCondition), 2),
            EcoFriendly = Math.Round(reviews.Average(r => r.CategoryRatings.EcoFriendly), 2)
        };
    }
}
