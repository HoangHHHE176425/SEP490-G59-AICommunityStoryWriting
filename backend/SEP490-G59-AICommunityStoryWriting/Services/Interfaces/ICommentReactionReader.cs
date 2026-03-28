namespace Services.Interfaces;

/// <summary>Đọc like/reaction hiển thị trên comment — tách khỏi CommentDAO cho unit test.</summary>
public interface ICommentReactionReader
{
    (bool UserHasLiked, string? UserReactionType, IReadOnlyDictionary<string, int> ReactionCounts) GetSummary(
        Guid commentId,
        Guid? viewerUserId);
}
