using DataAccessObjects.DAOs;
using Services.Interfaces;

namespace Services.Implementations;

public class CommentReactionReader : ICommentReactionReader
{
    public (bool UserHasLiked, string? UserReactionType, IReadOnlyDictionary<string, int> ReactionCounts) GetSummary(
        Guid commentId,
        Guid? viewerUserId)
    {
        try
        {
            var liked = false;
            string? reactionType = null;
            if (viewerUserId.HasValue)
            {
                liked = CommentDAO.HasLiked(viewerUserId.Value, commentId);
                reactionType = CommentDAO.GetUserReaction(viewerUserId.Value, commentId);
            }

            var counts = CommentDAO.GetReactionCounts(commentId);
            return (liked, reactionType, counts);
        }
        catch
        {
            return (false, null, new Dictionary<string, int>());
        }
    }
}
