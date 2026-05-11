using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CommentsService.Models;

public class HotelReview
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = null!;

    [BsonElement("hotelId")]
    public string HotelId { get; set; } = null!;

    [BsonElement("totalReviews")]
    public int TotalReviews { get; set; }

    [BsonElement("overallScore")]
    public double OverallScore { get; set; }

    [BsonElement("categoryScores")]
    public CategoryScores CategoryScores { get; set; } = new();

    [BsonElement("reviews")]
    public List<ReviewEntry> Reviews { get; set; } = [];
}

public class CategoryScores
{
    [BsonElement("cleanliness")]
    public double Cleanliness { get; set; }

    [BsonElement("staff")]
    public double Staff { get; set; }

    [BsonElement("facilities")]
    public double Facilities { get; set; }

    [BsonElement("locationCondition")]
    public double LocationCondition { get; set; }

    [BsonElement("ecoFriendly")]
    public double EcoFriendly { get; set; }
}

public class ReviewEntry
{
    [BsonElement("reviewId")]
    public string ReviewId { get; set; } = null!;

    [BsonElement("userId")]
    public string UserId { get; set; } = null!;

    [BsonElement("author")]
    public string Author { get; set; } = null!;

    [BsonElement("text")]
    public string Text { get; set; } = null!;

    [BsonElement("rating")]
    public double Rating { get; set; }

    [BsonElement("categoryRatings")]
    public CategoryScores CategoryRatings { get; set; } = new();

    [BsonElement("tripType")]
    public string? TripType { get; set; }

    [BsonElement("isVerified")]
    public bool IsVerified { get; set; } = true;

    [BsonElement("date")]
    public DateTime Date { get; set; } = DateTime.UtcNow;

    [BsonElement("hotelReply")]
    public HotelReply? HotelReply { get; set; }
}

public class HotelReply
{
    [BsonElement("repliedBy")]
    public string RepliedBy { get; set; } = null!;

    [BsonElement("replyText")]
    public string ReplyText { get; set; } = null!;

    [BsonElement("replyDate")]
    public DateTime ReplyDate { get; set; }
}
