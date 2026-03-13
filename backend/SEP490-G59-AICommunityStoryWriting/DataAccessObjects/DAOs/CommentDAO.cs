using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public class CommentDAO
    {
        /// <summary>Đếm số comment theo story_id (Guid). Mặc định chỉ đếm comment status = APPROVED.</summary>
        public static int GetCountByStoryId(Guid storyId, string status = "APPROVED")
        {
            using var context = new StoryPlatformDbContext();
            return context.comments.AsNoTracking()
                .Count(c => c.story_id == storyId && c.status == status);
        }

        public static comments? GetById(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            return context.comments.AsNoTracking().FirstOrDefault(c => c.id == id);
        }

        public static IReadOnlyList<comments> GetStoryComments(Guid storyId, string status = "APPROVED")
        {
            using var context = new StoryPlatformDbContext();
            return context.comments.AsNoTracking()
                .Include(c => c.userNavigation)
                .ThenInclude(u => u.user_profiles)
                .Where(c => c.story_id == storyId && c.status == status)
                .OrderBy(c => c.created_at)
                .ToList();
        }

        public static comments AddStoryComment(Guid storyId, Guid userId, string content, Guid? parentId = null, string status = "APPROVED")
        {
            using var context = new StoryPlatformDbContext();

            var entity = new comments
            {
                id = Guid.NewGuid(),
                user_id = userId,
                story_id = storyId,
                chapter_id = null,
                parent_id = parentId,
                content = content,
                likes_count = 0,
                status = status,
                created_at = DateTime.Now,
                updated_at = DateTime.Now
            };

            context.comments.Add(entity);
            context.SaveChanges();

            // Re-load with navigation for mapping in upper layer
            return context.comments.AsNoTracking()
                .Include(c => c.userNavigation)
                .ThenInclude(u => u.user_profiles)
                .First(c => c.id == entity.id);
        }

        /// <summary>Kiểm tra user đã like comment chưa (dựa trên comment_reactions với type LIKE). Trả về false nếu lỗi.</summary>
        public static bool HasLiked(Guid userId, Guid commentId)
        {
            try
            {
                using var context = new StoryPlatformDbContext();
                var count = context.comment_reactions
                    .AsNoTracking()
                    .Count(r => r.user_id == userId
                                && r.comment_id == commentId
                                && r.reaction_type == comment_reactions.ReactionTypes.Like);
                return count > 0;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Bật/tắt like: 1 user chỉ 1 like/comment. Trả về true = đã like, false = đã bỏ like.
        /// Dùng chung bảng comment_reactions với reaction_type = LIKE, không cần bảng commentsusers/comment_likes.
        /// </summary>
        public static bool ToggleLike(Guid userId, Guid commentId)
        {
            using var context = new StoryPlatformDbContext();
            var comment = context.comments.FirstOrDefault(c => c.id == commentId);
            if (comment == null) return false;

            var existing = context.comment_reactions
                .FirstOrDefault(r => r.user_id == userId
                                     && r.comment_id == commentId
                                     && r.reaction_type == comment_reactions.ReactionTypes.Like);

            var alreadyLiked = existing != null;
            if (alreadyLiked)
            {
                context.comment_reactions.Remove(existing);
                comment.likes_count = Math.Max(0, (comment.likes_count ?? 1) - 1);
            }
            else
            {
                context.comment_reactions.Add(new comment_reactions
                {
                    user_id = userId,
                    comment_id = commentId,
                    reaction_type = comment_reactions.ReactionTypes.Like,
                    created_at = DateTime.Now
                });
                comment.likes_count = (comment.likes_count ?? 0) + 1;
            }
            context.SaveChanges();
            return !alreadyLiked;
        }

        // --- Comment reactions (LIKE, DISLIKE, FUNNY, SAD, ANGRY, LOVE, WOW) ---

        /// <summary>Đếm số reaction theo từng type cho một comment. Key = reaction_type, Value = count.</summary>
        public static IReadOnlyDictionary<string, int> GetReactionCounts(Guid commentId)
        {
            using var context = new StoryPlatformDbContext();
            var list = context.comment_reactions
                .AsNoTracking()
                .Where(r => r.comment_id == commentId && r.reaction_type != null)
                .GroupBy(r => r.reaction_type!)
                .Select(g => new { Type = g.Key!, Count = g.Count() })
                .ToList();
            var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var t in comment_reactions.ReactionTypes.All)
                dict[t] = 0;
            foreach (var x in list)
                dict[x.Type] = x.Count;
            return dict;
        }

        /// <summary>Danh sách người đã reaction comment (UserId, DisplayName, ReactionType). Cho xem danh sách khi click tổng reaction.</summary>
        public static IReadOnlyList<(Guid UserId, string? DisplayName, string ReactionType)> GetCommentReactions(Guid commentId)
        {
            using var context = new StoryPlatformDbContext();
            var list = context.comment_reactions
                .AsNoTracking()
                .Include(r => r.user)
                .ThenInclude(u => u!.user_profiles)
                .Where(r => r.comment_id == commentId && r.reaction_type != null)
                .OrderBy(r => r.created_at)
                .ToList();
            return list.Select(r =>
            {
                var nickname = r.user?.user_profiles?.nickname;
                var email = r.user?.email;
                var display = !string.IsNullOrWhiteSpace(nickname) ? nickname : email;
                return (r.user_id, DisplayName: display, r.reaction_type!);
            }).ToList();
        }

        /// <summary>Reaction type mà current user đã chọn cho comment (null nếu chưa reaction).</summary>
        public static string? GetUserReaction(Guid userId, Guid commentId)
        {
            using var context = new StoryPlatformDbContext();
            var r = context.comment_reactions
                .AsNoTracking()
                .FirstOrDefault(x => x.user_id == userId && x.comment_id == commentId);
            return r?.reaction_type;
        }

        /// <summary>Đặt hoặc đổi reaction của user cho comment. reactionType = null hoặc rỗng = bỏ reaction. Trả về reaction_type sau khi lưu (null nếu đã bỏ).</summary>
        public static string? SetReaction(Guid userId, Guid commentId, string? reactionType)
        {
            using var context = new StoryPlatformDbContext();
            var comment = context.comments.FirstOrDefault(c => c.id == commentId);
            if (comment == null) return null;

            var existing = context.comment_reactions
                .FirstOrDefault(r => r.user_id == userId && r.comment_id == commentId);

            if (string.IsNullOrWhiteSpace(reactionType) || !comment_reactions.ReactionTypes.IsValid(reactionType))
            {
                if (existing != null)
                {
                    context.comment_reactions.Remove(existing);
                    comment.likes_count = Math.Max(0, (comment.likes_count ?? 1) - 1);
                    context.SaveChanges();
                }
                return null;
            }

            var type = reactionType!.Trim().ToUpperInvariant();
            if (existing != null)
            {
                if (existing.reaction_type == type) return type;
                existing.reaction_type = type;
                existing.created_at = DateTime.Now;
            }
            else
            {
                context.comment_reactions.Add(new comment_reactions
                {
                    user_id = userId,
                    comment_id = commentId,
                    reaction_type = type,
                    created_at = DateTime.Now
                });
                comment.likes_count = (comment.likes_count ?? 0) + 1;
            }
            context.SaveChanges();
            return type;
        }
    }
}

