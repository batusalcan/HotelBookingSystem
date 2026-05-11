namespace CommentsService.DTOs;

public class PostCommentRequest
{
    public string Text { get; set; } = null!;
    public double Rating { get; set; }
    public CategoryRatingsDto CategoryRatings { get; set; } = new();
    public string? TripType { get; set; }
}

public class CategoryRatingsDto
{
    public double Cleanliness { get; set; }
    public double Staff { get; set; }
    public double Facilities { get; set; }
    public double LocationCondition { get; set; }
    public double EcoFriendly { get; set; }
}
