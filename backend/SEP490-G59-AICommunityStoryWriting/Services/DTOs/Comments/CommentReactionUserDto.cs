namespace Services.DTOs.Comments;

/// <summary>Một người đã reaction comment: tên hiển thị và loại reaction.</summary>
public class CommentReactionUserDto
{
    public Guid UserId { get; set; }
    public string? UserDisplayName { get; set; }
    public string ReactionType { get; set; } = null!; // LIKE, DISLIKE, FUNNY, SAD, ANGRY, LOVE, WOW
}
