using BusinessObjects;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AIStory.API.Controllers
{
    /// <summary>API công khai cho trang chủ / thống kê cộng đồng.</summary>
    [ApiController]
    [Route("api/community")]
    public sealed class CommunityController : ControllerBase
    {
        private readonly StoryPlatformDbContext _db;

        public CommunityController(StoryPlatformDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Thống kê tổng quan: truyện PUBLISHED, số tác giả (distinct author_id có ít nhất 1 truyện đã xuất bản), tổng lượt xem (sum total_views các truyện đó).
        /// Không cần đăng nhập.
        /// </summary>
        [HttpGet("stats")]
        [AllowAnonymous]
        public async Task<IActionResult> GetStats(CancellationToken cancellationToken)
        {
            try
            {
                var published = _db.stories.AsNoTracking()
                    .Where(s => s.status != null && s.status.ToUpper() == "PUBLISHED");

                var publishedStoriesCount = await published.CountAsync(cancellationToken);

                var totalViews = await published.SumAsync(
                    s => (long)(s.total_views ?? 0L),
                    cancellationToken);

                var authorsCount = await published
                    .Where(s => s.author_id != null && s.author_id != Guid.Empty)
                    .Select(s => s.author_id!.Value)
                    .Distinct()
                    .CountAsync(cancellationToken);

                return Ok(new
                {
                    publishedStoriesCount,
                    authorsCount,
                    totalViews
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Không lấy được thống kê cộng đồng.", error = ex.Message });
            }
        }
    }
}
