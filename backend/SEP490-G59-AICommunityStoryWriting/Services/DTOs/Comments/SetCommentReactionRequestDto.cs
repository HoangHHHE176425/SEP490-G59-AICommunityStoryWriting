namespace Services.DTOs.Comments
{
    /// <summary>Body cho API đặt reaction comment. ReactionType: LIKE, DISLIKE, FUNNY, SAD, ANGRY, LOVE, WOW; null hoặc rỗng = bỏ reaction.</summary>
    public class SetCommentReactionRequestDto
    {
        public string? ReactionType { get; set; }
    }
}
