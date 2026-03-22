using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace DataAccessObjects.DAOs
{
    public static class NotificationDAO
    {
        public static void Add(notifications notification)
        {
            using var context = new StoryPlatformDbContext();
            context.notifications.Add(notification);
            context.SaveChanges();
        }

        public static List<notifications> GetByUserId(Guid userId, int limit = 50, bool onlyUnread = false)
        {
            using var context = new StoryPlatformDbContext();
            IQueryable<notifications> q = context.notifications
                .AsNoTracking()
                .Where(n => n.user_id == userId)
                .OrderByDescending(n => n.created_at);
            if (onlyUnread)
                q = q.Where(n => n.is_read != true);
            return q.Take(limit).ToList();
        }

        public static int GetUnreadCount(Guid userId)
        {
            using var context = new StoryPlatformDbContext();
            return context.notifications
                .Count(n => n.user_id == userId && n.is_read != true);
        }

        public static bool MarkAsRead(Guid notificationId, Guid userId)
        {
            using var context = new StoryPlatformDbContext();
            var n = context.notifications.FirstOrDefault(x => x.id == notificationId && x.user_id == userId);
            if (n == null) return false;
            n.is_read = true;
            context.SaveChanges();
            return true;
        }

        public static void MarkAllAsRead(Guid userId)
        {
            using var context = new StoryPlatformDbContext();
            foreach (var n in context.notifications.Where(x => x.user_id == userId && x.is_read != true))
                n.is_read = true;
            context.SaveChanges();
        }

        /// <summary>Lấy tên hiển thị (nickname hoặc email) của user để dùng trong nội dung thông báo.</summary>
        public static string GetUserDisplayName(Guid userId)
        {
            using var context = new StoryPlatformDbContext();
            var u = context.users.AsNoTracking().Include(x => x.user_profiles).FirstOrDefault(x => x.id == userId);
            if (u == null) return "Người dùng";
            var nickname = u.user_profiles?.nickname?.Trim();
            var email = u.email?.Trim();
            return !string.IsNullOrWhiteSpace(nickname) ? nickname : !string.IsNullOrWhiteSpace(email) ? email : "Người dùng";
        }

        /// <summary>Thông báo khi có người trả lời comment của mình. Gọi sau khi đã thêm comment reply thành công.</summary>
        /// <param name="linkUrlOverride">Nếu null: SPA React <c>/story/{storyId}#comment-...</c>. Chapter comment có thể truyền <c>/chapter?storyId=...&amp;chapterId=...</c>.</param>
        /// <param name="chapterDescriptor">Ví dụ <c>Chương 3: «Tiêu đề»</c> — khi trả lời comment trong chapter (null = comment cấp truyện).</param>
        /// <returns>Bản ghi đã lưu (để gửi SignalR).</returns>
        public static notifications NotifyCommentReply(Guid recipientUserId, string actorDisplayName, Guid storyId, string? storyTitle, Guid newCommentId, string? linkUrlOverride = null, string? chapterDescriptor = null)
        {
            if (string.IsNullOrWhiteSpace(actorDisplayName)) actorDisplayName = "Ai đó";
            var title = "Trả lời bình luận";
            string content;
            if (!string.IsNullOrWhiteSpace(chapterDescriptor))
            {
                var ch = chapterDescriptor.Trim();
                var storyPart = string.IsNullOrWhiteSpace(storyTitle) ? "truyện này" : $"truyện «{storyTitle.Trim()}»";
                content = $"{actorDisplayName} đã trả lời bình luận của bạn trong {ch} của {storyPart}.";
            }
            else
            {
                content = $"{actorDisplayName} đã trả lời bình luận của bạn" + (string.IsNullOrWhiteSpace(storyTitle) ? "." : $" trong truyện «{storyTitle.Trim()}».");
            }
            var linkUrl = string.IsNullOrWhiteSpace(linkUrlOverride)
                ? $"/story/{storyId}#comment-{newCommentId}"
                : linkUrlOverride.Trim();
            var n = new notifications
            {
                id = Guid.NewGuid(),
                user_id = recipientUserId,
                type = "COMMENT_REPLY",
                title = title,
                content = content,
                link_url = linkUrl,
                is_read = false,
                created_at = DateTime.Now
            };
            Add(n);
            return n;
        }

        /// <summary>Thông báo khi có người thả cảm xúc (reaction) vào comment của mình. Chỉ gọi khi đặt/đổi reaction (không gọi khi bỏ reaction).</summary>
        public static void NotifyCommentReaction(Guid recipientUserId, string actorDisplayName, Guid storyId, string? storyTitle, string reactionType)
        {
            if (string.IsNullOrWhiteSpace(actorDisplayName)) actorDisplayName = "Ai đó";
            var reactionLabel = reactionType?.ToUpperInvariant() switch
            {
                "LIKE" => "Thích",
                "DISLIKE" => "Không thích",
                "FUNNY" => "Buồn cười",
                "SAD" => "Buồn",
                "ANGRY" => "Phẫn nộ",
                "LOVE" => "Yêu thích",
                "WOW" => "Wow",
                _ => reactionType ?? "cảm xúc"
            };
            var title = "Reaction bình luận";
            var content = $"{actorDisplayName} đã thả {reactionLabel} vào bình luận của bạn" + (string.IsNullOrWhiteSpace(storyTitle) ? "." : $" trong truyện «{storyTitle.Trim()}».");
            var linkUrl = $"/story/{storyId}";
            Add(new notifications
            {
                id = Guid.NewGuid(),
                user_id = recipientUserId,
                type = "COMMENT_REACTION",
                title = title,
                content = content,
                link_url = linkUrl,
                is_read = false,
                created_at = DateTime.Now
            });
        }

        /// <summary>Gửi thông báo cho tất cả user đang follow story khi có chapter mới được publish. Dùng một context và SaveChanges một lần. Trả về danh sách thông báo đã tạo để caller gửi real-time (SignalR).</summary>
        /// <param name="logger">Nếu có thì ghi log vào host (cùng luồng với EF/core).</param>
        /// <returns>Danh sách notification đã lưu (mỗi item có user_id, id, ...) để gửi push real-time.</returns>
        public static List<notifications> NotifyStoryFollowersNewChapter(Guid storyId, Guid chapterId, string? chapterTitle, string? storyTitle, ILogger? logger = null)
        {
            Console.WriteLine($"[CONSOLE] NotifyStoryFollowersNewChapter ENTER StoryId={storyId} ChapterId={chapterId} ChapterTitle={chapterTitle ?? "(null)"} StoryTitle={storyTitle ?? "(null)"}");
            logger?.LogWarning("[NOTIFY] NotifyStoryFollowersNewChapter ENTER StoryId={StoryId} ChapterId={ChapterId} ChapterTitle={ChapterTitle} StoryTitle={StoryTitle}",
                storyId, chapterId, chapterTitle ?? "(null)", storyTitle ?? "(null)");

            var followerIds = UserLibraryDAO.GetFollowerUserIds(storyId);
            Console.WriteLine($"[CONSOLE] NotifyStoryFollowersNewChapter GetFollowerUserIds StoryId={storyId} FollowerCount={followerIds.Count} UserIds=[{string.Join(", ", followerIds)}]");
            if (followerIds.Count == 0)
            {
                Console.WriteLine($"[CONSOLE] NotifyStoryFollowersNewChapter SKIP: no followers for StoryId={storyId} (kiem tra user_library: story_id, relation_type='FOLLOW')");
                logger?.LogWarning("[NOTIFY] NotifyStoryFollowersNewChapter SKIP: no followers for StoryId={StoryId} (check user_library.relation_type='FOLLOW')", storyId);
                return new List<notifications>();
            }
            logger?.LogWarning("[NOTIFY] NotifyStoryFollowersNewChapter StoryId={StoryId} FollowerCount={Count} UserIds={UserIds}",
                storyId, followerIds.Count, string.Join(", ", followerIds));

            var title = "Truyện có chương mới";
            var content = string.IsNullOrWhiteSpace(storyTitle)
                ? "Truyện bạn theo dõi vừa ra chương mới."
                : $"«{storyTitle.Trim()}» vừa ra chương mới" + (string.IsNullOrWhiteSpace(chapterTitle) ? "." : $": {chapterTitle.Trim()}");
            var linkUrl = $"/Chapters/Read/{chapterId}";
            var now = DateTime.Now;
            var created = new List<notifications>();
            using (var context = new StoryPlatformDbContext())
            {
                foreach (var userId in followerIds)
                {
                    var n = new notifications
                    {
                        id = Guid.NewGuid(),
                        user_id = userId,
                        type = "STORY_CHAPTER_PUBLISHED",
                        title = title,
                        content = content,
                        link_url = linkUrl,
                        is_read = false,
                        created_at = now
                    };
                    created.Add(n);
                    context.notifications.Add(n);
                }
                try
                {
                    context.SaveChanges();
                    Console.WriteLine($"[CONSOLE] NotifyStoryFollowersNewChapter OK: saved {created.Count} notifications for StoryId={storyId}");
                    logger?.LogWarning("[NOTIFY] NotifyStoryFollowersNewChapter OK: saved {Count} notifications for StoryId={StoryId}", created.Count, storyId);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[CONSOLE] NotifyStoryFollowersNewChapter ERROR StoryId={storyId} ex={ex.Message}");
                    logger?.LogError(ex, "NotifyStoryFollowersNewChapter ERROR StoryId={StoryId}", storyId);
                    throw;
                }
            }
            return created;
        }

        /// <summary>Gửi thông báo cho tất cả user đang follow tác giả khi tác giả có chapter mới được publish. Trả về danh sách thông báo đã tạo để caller gửi real-time (SignalR).</summary>
        public static List<notifications> NotifyAuthorFollowersNewChapter(Guid authorId, Guid storyId, Guid chapterId, string? chapterTitle, string? storyTitle, ILogger? logger = null)
        {
            var followerIds = FollowDAO.GetAuthorFollowerIds(authorId);
            if (followerIds.Count == 0)
            {
                logger?.LogInformation("[NOTIFY] NotifyAuthorFollowersNewChapter SKIP: no followers for AuthorId={AuthorId}", authorId);
                return new List<notifications>();
            }
            var authorName = GetUserDisplayName(authorId);
            var title = "Tác giả có chương mới";
            var content = string.IsNullOrWhiteSpace(storyTitle)
                ? $"{authorName} vừa ra chương mới" + (string.IsNullOrWhiteSpace(chapterTitle) ? "." : $": {chapterTitle.Trim()}")
                : $"{authorName} vừa ra chương mới trong «{storyTitle.Trim()}»" + (string.IsNullOrWhiteSpace(chapterTitle) ? "." : $": {chapterTitle.Trim()}");
            var linkUrl = $"/Chapters/Read/{chapterId}";
            var now = DateTime.Now;
            var created = new List<notifications>();
            using (var context = new StoryPlatformDbContext())
            {
                foreach (var userId in followerIds)
                {
                    var n = new notifications
                    {
                        id = Guid.NewGuid(),
                        user_id = userId,
                        type = "AUTHOR_CHAPTER_PUBLISHED",
                        title = title,
                        content = content,
                        link_url = linkUrl,
                        is_read = false,
                        created_at = now
                    };
                    created.Add(n);
                    context.notifications.Add(n);
                }
                context.SaveChanges();
                logger?.LogInformation("[NOTIFY] NotifyAuthorFollowersNewChapter OK: saved {Count} notifications for AuthorId={AuthorId}", created.Count, authorId);
            }
            return created;
        }

        /// <summary>Gửi thông báo cho user đang follow tác giả khi tác giả có truyện mới được xuất bản (duyệt). Trả về danh sách thông báo đã tạo để gửi real-time (SignalR).</summary>
        public static List<notifications> NotifyAuthorFollowersNewStory(Guid authorId, Guid storyId, string? storyTitle, ILogger? logger = null)
        {
            var followerIds = FollowDAO.GetAuthorFollowerIds(authorId);
            if (followerIds.Count == 0)
            {
                logger?.LogInformation("[NOTIFY] NotifyAuthorFollowersNewStory SKIP: no followers for AuthorId={AuthorId}", authorId);
                return new List<notifications>();
            }
            var authorName = GetUserDisplayName(authorId);
            var title = "Tác giả có truyện mới";
            var content = string.IsNullOrWhiteSpace(storyTitle)
                ? $"{authorName} vừa đăng truyện mới."
                : $"{authorName} vừa đăng truyện mới: «{storyTitle.Trim()}»";
            var linkUrl = $"/Home/Story?id={storyId}";
            var now = DateTime.Now;
            var created = new List<notifications>();
            using (var context = new StoryPlatformDbContext())
            {
                foreach (var userId in followerIds)
                {
                    var n = new notifications
                    {
                        id = Guid.NewGuid(),
                        user_id = userId,
                        type = "AUTHOR_STORY_PUBLISHED",
                        title = title,
                        content = content,
                        link_url = linkUrl,
                        is_read = false,
                        created_at = now
                    };
                    created.Add(n);
                    context.notifications.Add(n);
                }
                context.SaveChanges();
                logger?.LogInformation("[NOTIFY] NotifyAuthorFollowersNewStory OK: saved {Count} notifications for AuthorId={AuthorId}", created.Count, authorId);
            }
            return created;
        }

        /// <summary>Thông báo cho tác giả khi có độc giả ủng hộ (donate). Trả về thông báo đã lưu để gửi real-time (SignalR).</summary>
        public static notifications NotifyDonationReceived(Guid recipientUserId, string senderDisplayName, int amount, string? message)
        {
            if (string.IsNullOrWhiteSpace(senderDisplayName)) senderDisplayName = "Người dùng";
            var title = "Ủng hộ từ độc giả";
            var content = $"{senderDisplayName} đã ủng hộ {amount} coin cho bạn.";
            if (!string.IsNullOrWhiteSpace(message))
                content += " Lời nhắn: " + message.Trim();
            var n = new notifications
            {
                id = Guid.NewGuid(),
                user_id = recipientUserId,
                type = "DONATION",
                title = title,
                content = content,
                link_url = "/wallet",
                is_read = false,
                created_at = DateTime.UtcNow
            };
            Add(n);
            return n;
        }
    }
}
