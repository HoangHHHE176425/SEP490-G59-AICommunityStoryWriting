using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Moderation;
using Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AIStory.API.Controllers
{
    /// <summary>API kiểm duyệt: moderator duyệt hoặc từ chối truyện/chapter (có lý do khi từ chối).</summary>
    [ApiController]
    [Route("api/moderator")]
    [Authorize(Roles = "MODERATOR,ADMIN")]
    public class ModeratorController : ControllerBase
    {
        private readonly IModerationService _moderationService;
        private readonly ILogger<ModeratorController> _logger;

        public ModeratorController(IModerationService moderationService, ILogger<ModeratorController> logger)
        {
            _moderationService = moderationService;
            _logger = logger;
        }

        private Guid? GetCurrentUserId()
        {
            var claim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && Guid.TryParse(claim.Value, out var userId))
                return userId;
            return null;
        }

        /// <summary>Lấy danh sách truyện đang chờ duyệt (PENDING_REVIEW). Hỗ trợ search, sort, phân trang.</summary>
        [HttpGet("stories/pending")]
        public IActionResult GetPendingStories(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null)
        {
            try
            {
                var result = _moderationService.GetPendingStories(page, pageSize, search, sortBy, sortOrder);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPendingStories failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách truyện chờ duyệt", error = ex.Message });
            }
        }

        /// <summary>Lấy danh sách chapter đang chờ duyệt (PENDING_REVIEW). Hỗ trợ lọc storyId, search, sort, phân trang.</summary>
        [HttpGet("chapters/pending")]
        public IActionResult GetPendingChapters(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] Guid? storyId = null,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null)
        {
            try
            {
                var result = _moderationService.GetPendingChapters(page, pageSize, storyId, search, sortBy, sortOrder);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPendingChapters failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách chapter chờ duyệt", error = ex.Message });
            }
        }

        /// <summary>Đồng ý duyệt truyện → status = PUBLISHED.</summary>
        [HttpPost("stories/{id:guid}/approve")]
        public IActionResult ApproveStory(Guid id)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });

            try
            {
                var ok = _moderationService.ApproveStory(id, moderatorId.Value);
                if (!ok)
                    return NotFound(new { message = "Truyện không tồn tại hoặc không ở trạng thái chờ duyệt (PENDING_REVIEW)." });
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApproveStory {StoryId} failed", id);
                return StatusCode(500, new { message = "Lỗi duyệt truyện", error = ex.Message });
            }
        }

        /// <summary>Từ chối truyện (bắt buộc gửi lý do trong body).</summary>
        [HttpPost("stories/{id:guid}/reject")]
        public IActionResult RejectStory(Guid id, [FromBody] RejectRequestDto request)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
            if (request == null || string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { message = "Vui lòng nhập lý do từ chối (reason)." });

            try
            {
                var ok = _moderationService.RejectStory(id, moderatorId.Value, request.Reason.Trim());
                if (!ok)
                    return NotFound(new { message = "Truyện không tồn tại hoặc không ở trạng thái chờ duyệt (PENDING_REVIEW)." });
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RejectStory {StoryId} failed", id);
                return StatusCode(500, new { message = "Lỗi từ chối truyện", error = ex.Message });
            }
        }

        /// <summary>Đồng ý duyệt chapter → status = PUBLISHED.</summary>
        [HttpPost("chapters/{id:guid}/approve")]
        public IActionResult ApproveChapter(Guid id)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });

            try
            {
                var ok = _moderationService.ApproveChapter(id, moderatorId.Value);
                if (!ok)
                    return NotFound(new { message = "Chapter không tồn tại hoặc không ở trạng thái chờ duyệt (PENDING_REVIEW)." });
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApproveChapter {ChapterId} failed", id);
                return StatusCode(500, new { message = "Lỗi duyệt chapter", error = ex.Message });
            }
        }

        /// <summary>Từ chối chapter (bắt buộc gửi lý do trong body).</summary>
        [HttpPost("chapters/{id:guid}/reject")]
        public IActionResult RejectChapter(Guid id, [FromBody] RejectRequestDto request)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
            if (request == null || string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { message = "Vui lòng nhập lý do từ chối (reason)." });

            try
            {
                var ok = _moderationService.RejectChapter(id, moderatorId.Value, request.Reason.Trim());
                if (!ok)
                    return NotFound(new { message = "Chapter không tồn tại hoặc không ở trạng thái chờ duyệt (PENDING_REVIEW)." });
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RejectChapter {ChapterId} failed", id);
                return StatusCode(500, new { message = "Lỗi từ chối chapter", error = ex.Message });
            }
        }
    }
}
