using System.ComponentModel.DataAnnotations;

namespace CommentsService.DTOs;

public class PostCommentRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(2000)]
    public string Text { get; set; } = null!;

    [Range(1.0, 10.0)]
    public double Rating { get; set; }

    [Required]
    public CategoryRatingsDto CategoryRatings { get; set; } = new();

    [MaxLength(100)]
    public string? TripType { get; set; }
}

public class CategoryRatingsDto
{
    [Range(1.0, 10.0)] public double Cleanliness { get; set; }
    [Range(1.0, 10.0)] public double Staff { get; set; }
    [Range(1.0, 10.0)] public double Facilities { get; set; }
    [Range(1.0, 10.0)] public double LocationCondition { get; set; }
    [Range(1.0, 10.0)] public double EcoFriendly { get; set; }
}
