using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Interfaces;
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
        private readonly IModeratorCategoryAssignmentRepository _moderatorCategoryRepo;
        private readonly ILogger<ModeratorController> _logger;

        public ModeratorController(IModerationService moderationService, IModeratorCategoryAssignmentRepository moderatorCategoryRepo, ILogger<ModeratorController> logger)
        {
            _moderationService = moderationService;
            _moderatorCategoryRepo = moderatorCategoryRepo;
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

        /// <summary>Lấy danh sách truyện đang chờ duyệt (PENDING_REVIEW). Moderator chỉ thấy truyện thuộc category được gán; ADMIN thấy tất cả.</summary>
        [HttpGet("stories/pending")]
        public async Task<IActionResult> GetPendingStories(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? claimFilter = null)
        {
            try
            {
                IReadOnlyList<Guid>? categoryIdsFilter = null;
                var moderatorId = GetCurrentUserId();
                if (!IsAdmin())
                {
                    if (!moderatorId.HasValue)
                        return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
                    categoryIdsFilter = await _moderatorCategoryRepo.GetCategoryIdsAsync(moderatorId.Value);
                }
                var result = _moderationService.GetPendingStories(page, pageSize, search, sortBy, sortOrder, categoryIdsFilter, moderatorId, claimFilter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPendingStories failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách truyện chờ duyệt", error = ex.Message });
            }
        }

        /// <summary>Lấy danh sách chapter đang chờ duyệt (PENDING_REVIEW). Moderator chỉ thấy chapter thuộc truyện có category được gán; ADMIN thấy tất cả.</summary>
        [HttpGet("chapters/pending")]
        public async Task<IActionResult> GetPendingChapters(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] Guid? storyId = null,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? claimFilter = null)
        {
            try
            {
                IReadOnlyList<Guid>? categoryIdsFilter = null;
                var moderatorId = GetCurrentUserId();
                if (!IsAdmin())
                {
                    if (!moderatorId.HasValue)
                        return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
                    categoryIdsFilter = await _moderatorCategoryRepo.GetCategoryIdsAsync(moderatorId.Value);
                }
                var result = _moderationService.GetPendingChapters(page, pageSize, storyId, search, sortBy, sortOrder, categoryIdsFilter, moderatorId, claimFilter);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPendingChapters failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách chapter chờ duyệt", error = ex.Message });
            }
        }

        /// <summary>Lịch sử truyện đã duyệt (PUBLISHED) hoặc từ chối (REJECTED). Moderator chỉ thấy truyện do mình duyệt/từ chối; ADMIN thấy theo category.</summary>
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
                IReadOnlyList<Guid>? categoryIdsFilter = null;
                var moderatorId = GetCurrentUserId();
                var isAdmin = IsAdmin();
                if (!isAdmin && moderatorId.HasValue)
                    categoryIdsFilter = await _moderatorCategoryRepo.GetCategoryIdsAsync(moderatorId.Value);
                var result = _moderationService.GetReviewedStories(page, pageSize, status, search, sortBy, sortOrder, categoryIdsFilter, moderatorId, isAdmin);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetReviewedStories failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách truyện đã duyệt/từ chối", error = ex.Message });
            }
        }

        /// <summary>Lịch sử chapter đã duyệt (PUBLISHED) hoặc từ chối (REJECTED). Moderator chỉ thấy chapter do mình duyệt/từ chối; ADMIN thấy theo category.</summary>
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
                IReadOnlyList<Guid>? categoryIdsFilter = null;
                var moderatorId = GetCurrentUserId();
                var isAdmin = IsAdmin();
                if (!isAdmin && moderatorId.HasValue)
                    categoryIdsFilter = await _moderatorCategoryRepo.GetCategoryIdsAsync(moderatorId.Value);
                var result = _moderationService.GetReviewedChapters(page, pageSize, status, search, sortBy, sortOrder, categoryIdsFilter, moderatorId, isAdmin);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetReviewedChapters failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách chapter đã duyệt/từ chối", error = ex.Message });
            }
        }

        /// <summary>Moderator "nhận duyệt" truyện → lock, người khác không thấy trong queue. Queue: ai gửi trước duyệt trước (FIFO).</summary>
        [HttpPost("stories/{id:guid}/claim")]
        public async Task<IActionResult> ClaimStory(Guid id)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });

            IReadOnlyList<Guid>? allowedCategoryIds = null;
            if (!IsAdmin())
                allowedCategoryIds = await _moderatorCategoryRepo.GetCategoryIdsAsync(moderatorId.Value);

            try
            {
                var ok = _moderationService.ClaimStory(id, moderatorId.Value, allowedCategoryIds);
                if (!ok)
                    return NotFound(new { message = "Truyện không tồn tại, không ở trạng thái chờ duyệt, không thuộc category bạn được gán, hoặc đã được moderator khác nhận duyệt." });
                return NoContent();
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

            IReadOnlyList<Guid>? allowedCategoryIds = null;
            if (!IsAdmin())
                allowedCategoryIds = await _moderatorCategoryRepo.GetCategoryIdsAsync(moderatorId.Value);

            try
            {
                var ok = _moderationService.ApproveStory(id, moderatorId.Value, allowedCategoryIds);
                if (!ok)
                    return NotFound(new { message = "Truyện không tồn tại, không ở trạng thái chờ duyệt (PENDING_REVIEW), hoặc không thuộc category bạn được gán." });
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ApproveStory {StoryId} failed", id);
                return StatusCode(500, new { message = "Lỗi duyệt truyện", error = ex.Message });
            }
        }

        /// <summary>Từ chối truyện (bắt buộc gửi lý do trong body). Moderator chỉ từ chối được truyện thuộc category được gán.</summary>
        [HttpPost("stories/{id:guid}/reject")]
        public async Task<IActionResult> RejectStory(Guid id, [FromBody] RejectRequestDto request)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
            if (request == null || string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { message = "Vui lòng nhập lý do từ chối (reason)." });

            IReadOnlyList<Guid>? allowedCategoryIds = null;
            if (!IsAdmin())
                allowedCategoryIds = await _moderatorCategoryRepo.GetCategoryIdsAsync(moderatorId.Value);

            try
            {
                var ok = _moderationService.RejectStory(id, moderatorId.Value, request.Reason.Trim(), allowedCategoryIds);
                if (!ok)
                    return NotFound(new { message = "Truyện không tồn tại, không ở trạng thái chờ duyệt (PENDING_REVIEW), hoặc không thuộc category bạn được gán." });
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

        /// <summary>Moderator "nhận duyệt" chapter → lock. Queue: ai gửi trước duyệt trước (FIFO).</summary>
        [HttpPost("chapters/{id:guid}/claim")]
        public async Task<IActionResult> ClaimChapter(Guid id)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });

            IReadOnlyList<Guid>? allowedCategoryIds = null;
            if (!IsAdmin())
                allowedCategoryIds = await _moderatorCategoryRepo.GetCategoryIdsAsync(moderatorId.Value);

            try
            {
                var ok = _moderationService.ClaimChapter(id, moderatorId.Value, allowedCategoryIds);
                if (!ok)
                    return NotFound(new { message = "Chapter không tồn tại, không ở trạng thái chờ duyệt, không thuộc category bạn được gán, hoặc đã được moderator khác nhận duyệt." });
                return NoContent();
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

            IReadOnlyList<Guid>? allowedCategoryIds = null;
            if (!IsAdmin())
                allowedCategoryIds = await _moderatorCategoryRepo.GetCategoryIdsAsync(moderatorId.Value);

            try
            {
                var ok = _moderationService.ApproveChapter(id, moderatorId.Value, allowedCategoryIds);
                Console.WriteLine($"[CONSOLE] ApproveChapter result ok={ok}");
                if (!ok)
                    return NotFound(new { message = "Chapter không tồn tại, không ở trạng thái chờ duyệt (PENDING_REVIEW), hoặc không thuộc category bạn được gán." });
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

        /// <summary>Từ chối chapter (bắt buộc gửi lý do trong body). Moderator chỉ từ chối được chapter thuộc truyện có category được gán.</summary>
        [HttpPost("chapters/{id:guid}/reject")]
        public async Task<IActionResult> RejectChapter(Guid id, [FromBody] RejectRequestDto request)
        {
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
            if (request == null || string.IsNullOrWhiteSpace(request.Reason))
                return BadRequest(new { message = "Vui lòng nhập lý do từ chối (reason)." });

            IReadOnlyList<Guid>? allowedCategoryIds = null;
            if (!IsAdmin())
                allowedCategoryIds = await _moderatorCategoryRepo.GetCategoryIdsAsync(moderatorId.Value);

            try
            {
                var ok = _moderationService.RejectChapter(id, moderatorId.Value, request.Reason.Trim(), allowedCategoryIds);
                if (!ok)
                    return NotFound(new { message = "Chapter không tồn tại, không ở trạng thái chờ duyệt (PENDING_REVIEW), hoặc không thuộc category bạn được gán." });
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
    }
}
