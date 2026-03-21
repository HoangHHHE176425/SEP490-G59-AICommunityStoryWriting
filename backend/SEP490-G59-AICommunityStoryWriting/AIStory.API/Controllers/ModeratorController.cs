using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Moderation;
using Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DataAccessObjects.DAOs;

namespace AIStory.API.Controllers
{
    /// <summary>API kiểm duyệt: moderator duyệt hoặc từ chối truyện/chapter (có lý do khi từ chối). Moderator có thể nhận duyệt tất cả category.</summary>
    [ApiController]
    [Route("api/moderator")]
    [Authorize(Roles = "MODERATOR,ADMIN")]
    public class ModeratorController : ControllerBase
    {
        private readonly IModerationService _moderationService;
        private readonly IChapterVersionService _chapterVersionService;
        private readonly IReviewEscalationService _reviewEscalationService;
        private readonly ILogger<ModeratorController> _logger;

        public ModeratorController(
            IModerationService moderationService,
            IChapterVersionService chapterVersionService,
            IReviewEscalationService reviewEscalationService,
            ILogger<ModeratorController> logger)
        {
            _moderationService = moderationService;
            _chapterVersionService = chapterVersionService;
            _reviewEscalationService = reviewEscalationService;
            _logger = logger;
        }

        private Guid? GetCurrentUserId()
        {
            var claim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && Guid.TryParse(claim.Value, out var userId))
                return userId;
            return null;
        }

        private bool IsAdmin() => User.IsInRole("ADMIN");

        /// <summary>Lấy danh sách truyện đang chờ duyệt (PENDING_REVIEW). Moderator và ADMIN đều thấy tất cả.</summary>
        [HttpGet("stories/pending")]
        public async Task<IActionResult> GetPendingStories(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? claimFilter = null,
            [FromQuery] string? timeStatus = null)
        {
            try
            {
                var moderatorId = GetCurrentUserId();
                if (!moderatorId.HasValue)
                    return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
                var result = _moderationService.GetPendingStories(page, pageSize, search, sortBy, sortOrder, categoryIdsFilter: null, moderatorId, claimFilter, timeStatus);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPendingStories failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách truyện chờ duyệt", error = ex.Message });
            }
        }

        /// <summary>Lấy danh sách chapter đang chờ duyệt (PENDING_REVIEW). Moderator và ADMIN đều thấy tất cả.</summary>
        [HttpGet("chapters/pending")]
        public async Task<IActionResult> GetPendingChapters(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] Guid? storyId = null,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? claimFilter = null,
            [FromQuery] string? timeStatus = null)
        {
            try
            {
                var moderatorId = GetCurrentUserId();
                if (!moderatorId.HasValue)
                    return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
                var result = _moderationService.GetPendingChapters(page, pageSize, storyId, search, sortBy, sortOrder, categoryIdsFilter: null, moderatorId, claimFilter, timeStatus);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPendingChapters failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách chapter chờ duyệt", error = ex.Message });
            }
        }

        /// <summary>Lịch sử truyện đã duyệt (PUBLISHED) hoặc từ chối (REJECTED). Moderator thấy truyện do mình duyệt/từ chối; ADMIN thấy tất cả.</summary>
        [HttpGet("stories/reviewed")]
        public async Task<IActionResult> GetReviewedStories(
            [FromQuery] string status = "PUBLISHED",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null)
        {
            try
            {
                var moderatorId = GetCurrentUserId();
                var isAdmin = IsAdmin();
                var result = _moderationService.GetReviewedStories(page, pageSize, status, search, sortBy, sortOrder, categoryIdsFilter: null, moderatorId, isAdmin);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetReviewedStories failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách truyện đã duyệt/từ chối", error = ex.Message });
            }
        }

        /// <summary>Lịch sử chapter đã duyệt (PUBLISHED) hoặc từ chối (REJECTED). Moderator thấy chapter do mình duyệt/từ chối; ADMIN thấy tất cả.</summary>
        [HttpGet("chapters/reviewed")]
        public async Task<IActionResult> GetReviewedChapters(
            [FromQuery] string status = "PUBLISHED",
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null)
        {
            try
            {
                var moderatorId = GetCurrentUserId();
                var isAdmin = IsAdmin();
                var result = _moderationService.GetReviewedChapters(page, pageSize, status, search, sortBy, sortOrder, categoryIdsFilter: null, moderatorId, isAdmin);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetReviewedChapters failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách chapter đã duyệt/từ chối", error = ex.Message });
            }
        }

        /// <summary>
        /// Lịch sử phiên bản chương bị từ chối (version). 
        /// - Moderator: chỉ thấy version do mình từ chối (reviewed_by = moderator).
        /// - Admin: thấy tất cả.
        /// </summary>
        [HttpGet("chapter-versions/rejected-history")]
        public IActionResult GetRejectedChapterVersionsHistory()
        {
            try
            {
                var moderatorId = GetCurrentUserId();
                if (!moderatorId.HasValue)
                    return Unauthorized(new { message = "Không xác định được moderator (JWT)." });

                // Lịch sử từ chối version: lấy tất cả version đã từng bị từ chối (không lọc theo moderator để tránh thiếu dữ liệu).
                var list = ChapterVersionDAO.GetRejectedHistory(null)
                    .Select(v => new RejectedChapterVersionItemDto
                    {
                        Id = v.id,
                        ChapterId = v.chapter_id,
                        StoryId = v.chapter?.story_id,
                        StoryTitle = v.chapter?.story?.title,
                        ChapterTitle = v.chapter?.title,
                        ChapterOrderIndex = v.chapter != null ? v.chapter.order_index : null,
                        VersionNumber = v.version_number,
                        TitleSnapshot = v.title_snapshot,
                        Status = v.status,
                        WordCount = string.IsNullOrWhiteSpace(v.content_snapshot)
                            ? 0
                            : v.content_snapshot.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length,
                        RejectionReason = v.rejection_reason,
                        RejectedAt = v.reviewed_at ?? v.created_at
                    })
                    .ToList();
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetRejectedChapterVersionsHistory failed");
                return StatusCode(500, new { message = "Lỗi lấy lịch sử từ chối phiên bản", error = ex.Message });
            }
        }

        /// <summary>Moderator "nhận duyệt" truyện → lock, người khác không thấy trong queue. Body bắt buộc: <c>reviewDeadlineAt</c> (UTC, ISO 8601) — hạn hoàn thành duyệt.</summary>
        [HttpPost("stories/{id:guid}/claim")]
        public async Task<IActionResult> ClaimStory(Guid id, [FromBody] ModeratorClaimRequestDto? request)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
            if (request == null || request.ReviewDeadlineAt == default)
                return BadRequest(new { message = "Vui lòng chọn hạn duyệt (reviewDeadlineAt) trong body." });

            try
            {
                var ok = _moderationService.ClaimStory(id, moderatorId.Value, request.ReviewDeadlineAt, allowedCategoryIds: null);
                if (!ok)
                    return NotFound(new { message = "Truyện không tồn tại, không ở trạng thái chờ duyệt, hoặc đã được moderator khác nhận duyệt." });
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ClaimStory {StoryId} failed", id);
                return StatusCode(500, new { message = "Lỗi nhận duyệt truyện", error = ex.Message });
            }
        }

        /// <summary>Đồng ý duyệt truyện → status = PUBLISHED. Nếu đã claim thì chỉ assignee mới duyệt được.</summary>
        [HttpPost("stories/{id:guid}/approve")]
        public async Task<IActionResult> ApproveStory(Guid id)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });

            try
            {
                var ok = _moderationService.ApproveStory(id, moderatorId.Value, allowedCategoryIds: null);
                if (!ok)
                    return NotFound(new { message = "Truyện không tồn tại hoặc không ở trạng thái chờ duyệt (PENDING_REVIEW)." });
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApproveStory {StoryId} failed", id);
                return StatusCode(500, new { message = "Lỗi duyệt truyện", error = ex.Message });
            }
        }

        /// <summary>Từ chối truyện (bắt buộc gửi lý do trong body).</summary>
        [HttpPost("stories/{id:guid}/reject")]
        public async Task<IActionResult> RejectStory(Guid id, [FromBody] RejectRequestDto request)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
            if (request == null || string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { message = "Vui lòng nhập lý do từ chối (reason)." });

            try
            {
                var ok = _moderationService.RejectStory(id, moderatorId.Value, request.Reason.Trim(), allowedCategoryIds: null);
                if (!ok)
                    return NotFound(new { message = "Truyện không tồn tại hoặc không ở trạng thái chờ duyệt (PENDING_REVIEW)." });
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "RejectStory {StoryId} failed", id);
                return StatusCode(500, new { message = "Lỗi từ chối truyện", error = ex.Message });
            }
        }

        /// <summary>Moderator "nhận duyệt" chapter → lock. Body bắt buộc: <c>reviewDeadlineAt</c> (UTC) — hạn hoàn thành duyệt.</summary>
        [HttpPost("chapters/{id:guid}/claim")]
        public async Task<IActionResult> ClaimChapter(Guid id, [FromBody] ModeratorClaimRequestDto? request)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
            if (request == null || request.ReviewDeadlineAt == default)
                return BadRequest(new { message = "Vui lòng chọn hạn duyệt (reviewDeadlineAt) trong body." });

            try
            {
                var ok = _moderationService.ClaimChapter(id, moderatorId.Value, request.ReviewDeadlineAt, allowedCategoryIds: null);
                if (!ok)
                    return NotFound(new { message = "Chapter không tồn tại, không ở trạng thái chờ duyệt, hoặc đã được moderator khác nhận duyệt." });
                return NoContent();
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ClaimChapter {ChapterId} failed", id);
                return StatusCode(500, new { message = "Lỗi nhận duyệt chapter", error = ex.Message });
            }
        }

        /// <summary>Đồng ý duyệt chapter → status = PUBLISHED. Nếu đã claim thì chỉ assignee mới duyệt được.</summary>
        [HttpPost("chapters/{id:guid}/approve")]
        public async Task<IActionResult> ApproveChapter(Guid id)
        {
            Console.WriteLine($"[CONSOLE] ApproveChapter API called ChapterId={id}");
            _logger.LogWarning("[NOTIFY] ApproveChapter API called ChapterId={ChapterId}", id);
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });

            try
            {
                var ok = _moderationService.ApproveChapter(id, moderatorId.Value, allowedCategoryIds: null);
                Console.WriteLine($"[CONSOLE] ApproveChapter result ok={ok}");
                if (!ok)
                    return NotFound(new { message = "Chapter không tồn tại hoặc không ở trạng thái chờ duyệt (PENDING_REVIEW)." });
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[CONSOLE] ApproveChapter EXCEPTION ChapterId={id} ex={ex.Message}");
                _logger.LogError(ex, "ApproveChapter {ChapterId} failed", id);
                return StatusCode(500, new { message = "Lỗi duyệt chapter", error = ex.Message });
            }
        }

        /// <summary>Từ chối chapter (bắt buộc gửi lý do trong body).</summary>
        [HttpPost("chapters/{id:guid}/reject")]
        public async Task<IActionResult> RejectChapter(Guid id, [FromBody] RejectRequestDto request)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
            if (request == null || string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { message = "Vui lòng nhập lý do từ chối (reason)." });

            try
            {
                var ok = _moderationService.RejectChapter(id, moderatorId.Value, request.Reason.Trim(), allowedCategoryIds: null);
                if (!ok)
                    return NotFound(new { message = "Chapter không tồn tại hoặc không ở trạng thái chờ duyệt (PENDING_REVIEW)." });
                return NoContent();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
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

        /// <summary>Moderator xem danh sách version của một chapter.</summary>
        [HttpGet("chapters/{id:guid}/versions")]
        public IActionResult GetChapterVersions(Guid id)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
            var list = _chapterVersionService.GetByChapterId(id)
                .Where(v =>
                    string.Equals(v.Status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(v.Status, "PUBLISHED", StringComparison.OrdinalIgnoreCase))
                .ToList();
            return Ok(list);
        }

        /// <summary>Moderator xem chi tiết một version (nội dung snapshot).</summary>
        [HttpGet("chapters/{chapterId:guid}/versions/{versionId:guid}")]
        public IActionResult GetChapterVersion(Guid chapterId, Guid versionId)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
            var v = _chapterVersionService.GetById(versionId);
            if (v == null || v.ChapterId != chapterId)
                return NotFound(new { message = "Version không tồn tại." });
            return Ok(v);
        }

        /// <summary>Nội dung chapter cho màn duyệt: bản gốc đã xuất bản + bản version chờ duyệt (khi chapter đã PUBLISHED và có version gửi chỉnh sửa). Moderator dùng để xem 2 phiên bản.</summary>
        [HttpGet("chapters/{id:guid}/review-content")]
        public IActionResult GetChapterReviewContent(Guid id)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
            var content = _moderationService.GetChapterReviewContent(id);
            if (content == null)
                return NotFound(new { message = "Chapter không tồn tại." });
            return Ok(content);
        }

        /// <summary>Moderator đang nhận duyệt mục này không, hạn hiện tại, đã có đơn báo cáo chờ admin chưa.</summary>
        [HttpGet("review-assignment/self")]
        public IActionResult GetSelfReviewAssignment([FromQuery] string targetType, [FromQuery] Guid targetId)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
            try
            {
                var dto = _reviewEscalationService.GetSelfAssignment(targetType, targetId, moderatorId.Value);
                return Ok(dto);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Gửi báo cáo lên admin: gia hạn hạn duyệt hoặc hủy nhận (chuyển cho người khác).</summary>
        [HttpPost("review-escalations")]
        public IActionResult SubmitReviewEscalation([FromBody] ModeratorSubmitReviewEscalationDto? request)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
            if (request == null)
                return BadRequest(new { message = "Body không hợp lệ." });
            try
            {
                var id = _reviewEscalationService.Submit(moderatorId.Value, request);
                return Ok(new { id });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "SubmitReviewEscalation failed");
                return StatusCode(500, new { message = "Lỗi gửi báo cáo", error = ex.Message });
            }
        }
    }
}
