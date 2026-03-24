using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using DataAccessObjects.DAOs;
using Services.DTOs.Account;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using BusinessObjects;
using BusinessObjects.Entities;

namespace AIStory.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class LibraryController : ControllerBase
    {
        private Guid GetUserIdFromToken()
        {
            var claim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim == null || !Guid.TryParse(claim.Value, out var userId))
                throw new InvalidOperationException("Invalid token or user ID.");
            return userId;
        }

        /// <summary>Lấy thư viện của user: truyện đang theo dõi, tác giả đang theo dõi, lịch sử đọc dở.</summary>
        [HttpGet]
        public IActionResult GetMyLibrary()
        {
            var userId = GetUserIdFromToken();
            var result = new MyLibraryResponseDto();

            // 1. Truyện đang theo dõi (FOLLOW)
            var followedStoryIds = UserLibraryDAO.GetFollowedStoryIds(userId);
            foreach (var storyId in followedStoryIds)
            {
                var story = StoryDAO.GetById(storyId);
                if (story == null) continue;
                if (!string.Equals(story.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase)) continue;
                var authorName = story.author_id.HasValue ? NotificationDAO.GetUserDisplayName(story.author_id.Value) : null;
                var publishedCount = ChapterDAO.GetPublishedCountByStoryId(storyId);
                var latestChapterAt = ChapterDAO.GetLatestUpdatedAtByStoryId(storyId);
                result.FollowedStories.Add(new FollowedStoryItemDto
                {
                    Id = story.id,
                    Title = story.title ?? "",
                    Slug = story.slug,
                    CoverImage = story.cover_image,
                    Summary = story.summary,
                    AuthorId = story.author_id,
                    AuthorName = authorName,
                    Status = story.status,
                    PublishedChaptersCount = publishedCount,
                    LatestUpdatedAt = latestChapterAt ?? story.updated_at
                });
            }

            // 2. Tác giả đang theo dõi
            var followedAuthorIds = FollowDAO.GetFollowedAuthorIds(userId);
            foreach (var authorId in followedAuthorIds)
            {
                var name = NotificationDAO.GetUserDisplayName(authorId);
                result.FollowedAuthors.Add(new FollowedAuthorItemDto
                {
                    AuthorId = authorId,
                    AuthorName = name ?? "Tác giả"
                });
            }

            // 3. Lịch sử đọc dở (READING)
            var readingEntries = UserLibraryDAO.GetReadingProgressEntries(userId);
            foreach (var (storyId, chapterId, lastReadAt) in readingEntries)
            {
                var story = StoryDAO.GetById(storyId);
                var chapter = ChapterDAO.GetById(chapterId);
                if (story == null) continue;
                result.ReadingHistory.Add(new ReadingHistoryItemDto
                {
                    StoryId = storyId,
                    StoryTitle = story.title ?? "",
                    CoverImage = story.cover_image,
                    LastReadChapterId = chapterId,
                    LastReadChapterTitle = chapter?.title,
                    LastReadChapterOrder = chapter?.order_index,
                    LastReadAt = lastReadAt
                });
            }

            return Ok(result);
        }
    }
}
