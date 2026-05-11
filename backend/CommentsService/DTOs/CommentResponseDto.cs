namespace CommentsService.DTOs;

public class CommentResponseDto
{
    public string ReviewId { get; set; } = null!;
    public string Author { get; set; } = null!;
    public string? TripType { get; set; }
    public string Text { get; set; } = null!;
    public double Rating { get; set; }
    public DateTime Date { get; set; }
    public bool IsVerified { get; set; }
}
