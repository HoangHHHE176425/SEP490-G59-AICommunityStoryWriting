using DataAccessObjects.DAOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Repositories.Interfaces;
using Services.DTOs.Moderation;
using Services.DTOs.Stories;
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
        private readonly IStoryService _storyService;
        private readonly IModeratorCategoryAssignmentRepository _moderatorCategoryRepo;
        private readonly ILogger<ModeratorController> _logger;

        public ModeratorController(IModerationService moderationService, IStoryService storyService, IModeratorCategoryAssignmentRepository moderatorCategoryRepo, ILogger<ModeratorController> logger)
        {
            _moderationService = moderationService;
            _storyService = storyService;
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

        /// <summary>Lịch sử đã duyệt / từ chối: truyện do moderator này duyệt (từ moderator_logs). ADMIN thấy tất cả truyện theo status + category.</summary>
        [HttpGet("stories/reviewed")]
        public async Task<IActionResult> GetReviewedStories(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null)
        {
            try
            {
                IReadOnlyList<Guid>? categoryIdsFilter = null;
                List<Guid>? includeStoryIdsFromLogs = null;
                if (!IsAdmin())
                {
                    var moderatorId = GetCurrentUserId();
                    if (!moderatorId.HasValue)
                        return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
                    categoryIdsFilter = await _moderatorCategoryRepo.GetCategoryIdsAsync(moderatorId.Value);
                    var statusUpper = (status ?? "").Trim().ToUpperInvariant();
                    var action = statusUpper == "PUBLISHED" ? "APPROVED" : "REJECTED";
                    includeStoryIdsFromLogs = ModerationLogDAO.GetTargetIdsByModeratorAndAction(moderatorId.Value, "STORY", action);
                    if (includeStoryIdsFromLogs.Count == 0)
                        return Ok(new PagedResultDto<StoryListItemDto> { Items = Array.Empty<StoryListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize });
                }
                var statusUpperQuery = (status ?? "").Trim().ToUpperInvariant();
                if (string.IsNullOrEmpty(statusUpperQuery) || (statusUpperQuery != "PUBLISHED" && statusUpperQuery != "REJECTED"))
                    return BadRequest(new { message = "status phải là PUBLISHED hoặc REJECTED." });
                // Khi lấy từ moderator_logs (IncludeStoryIds) thì không lọc category để hiển thị đủ truyện moderator đã duyệt/từ chối.
                var query = new StoryQueryDto
                {
                    Page = page,
                    PageSize = pageSize,
                    Search = search,
                    Status = statusUpperQuery,
                    SortBy = !string.IsNullOrWhiteSpace(sortBy) ? sortBy : "updated_at",
                    SortOrder = !string.IsNullOrWhiteSpace(sortOrder) ? sortOrder : "desc",
                    CategoryIds = includeStoryIdsFromLogs != null ? null : categoryIdsFilter?.ToList(),
                    IncludeStoryIds = includeStoryIdsFromLogs
                };
                var result = _storyService.GetAll(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetReviewedStories failed");
                return StatusCode(500, new { message = "Lỗi lấy lịch sử đã duyệt", error = ex.Message });
            }
        }

        /// <summary>Lịch sử chương đã duyệt/từ chối: chương do moderator này duyệt (từ moderator_logs). ADMIN thấy tất cả theo status + category.</summary>
        [HttpGet("chapters/reviewed")]
        public async Task<IActionResult> GetReviewedChapters(
            [FromQuery] string? status = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null)
        {
            try
            {
                IReadOnlyList<Guid>? categoryIdsFilter = null;
                IReadOnlyList<Guid>? reviewedByModeratorChapterIds = null;
                if (!IsAdmin())
                {
                    var moderatorId = GetCurrentUserId();
                    if (!moderatorId.HasValue)
                        return Unauthorized(new { message = "Không xác định được moderator (JWT)." });
                    categoryIdsFilter = await _moderatorCategoryRepo.GetCategoryIdsAsync(moderatorId.Value);
                    var statusUpper = (status ?? "REJECTED").Trim().ToUpperInvariant();
                    var action = statusUpper == "PUBLISHED" ? "APPROVED" : "REJECTED";
                    reviewedByModeratorChapterIds = ModerationLogDAO.GetTargetIdsByModeratorAndAction(moderatorId.Value, "CHAPTER", action);
                }
                var statusUpperQuery = (status ?? "REJECTED").Trim().ToUpperInvariant();
                if (statusUpperQuery != "PUBLISHED" && statusUpperQuery != "REJECTED")
                    return BadRequest(new { message = "status phải là PUBLISHED hoặc REJECTED." });
                var result = _moderationService.GetReviewedChapters(page, pageSize, statusUpperQuery, search, sortBy, sortOrder, categoryIdsFilter, reviewedByModeratorChapterIds);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetReviewedChapters failed");
                return StatusCode(500, new { message = "Lỗi lấy lịch sử chương đã duyệt/từ chối", error = ex.Message });
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
            var moderatorId = GetCurrentUserId();
            if (!moderatorId.HasValue)
                return Unauthorized(new { message = "Không xác định được moderator (JWT)." });

            IReadOnlyList<Guid>? allowedCategoryIds = null;
            if (!IsAdmin())
                allowedCategoryIds = await _moderatorCategoryRepo.GetCategoryIdsAsync(moderatorId.Value);

            try
            {
                var ok = _moderationService.ApproveChapter(id, moderatorId.Value, allowedCategoryIds);
                if (!ok)
                    return NotFound(new { message = "Chapter không tồn tại, không ở trạng thái chờ duyệt (PENDING_REVIEW), hoặc không thuộc category bạn được gán." });
                return NoContent();
            }
            catch (Exception ex)
            {
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
