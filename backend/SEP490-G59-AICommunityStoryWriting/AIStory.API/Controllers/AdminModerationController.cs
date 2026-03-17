using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Admin.Moderation;
using Services.DTOs.Chapters;
using Services.DTOs.Stories;
using Services.Interfaces;

namespace AIStory.API.Controllers
{
    /// <summary>Dashboard Admin: theo dõi hoạt động duyệt của Moderator, xem danh sách nội dung đã duyệt/từ chối, moderation logs, hiệu suất moderator.</summary>
    [ApiController]
    [Route("api/admin/moderation")]
    [Authorize(Roles = "ADMIN")]
    public class AdminModerationController : ControllerBase
    {
        public const int DefaultDeadlineDays = 7;
        public const int WarningDaysThreshold = 2; // Còn <= 2 ngày thì Warning (vàng)

        private readonly IModerationService _moderationService;
        private readonly ILogger<AdminModerationController> _logger;

        public AdminModerationController(IModerationService moderationService, ILogger<AdminModerationController> logger)
        {
            _moderationService = moderationService;
            _logger = logger;
        }

        private Guid? GetCurrentUserId()
        {
            var claim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && Guid.TryParse(claim.Value, out var userId) ? userId : null;
        }

        private static string ComputeTimeStatus(DateTime? pendingSince, int deadlineDays = DefaultDeadlineDays)
        {
            if (!pendingSince.HasValue) return "OnTime";
            var deadline = pendingSince.Value.AddDays(deadlineDays);
            var now = DateTime.UtcNow;
            if (now > deadline) return "Overdue";
            var daysLeft = (deadline - now).TotalDays;
            return daysLeft <= WarningDaysThreshold ? "Warning" : "OnTime";
        }

        private static DateTime? ComputeDeadline(DateTime? pendingSince, int deadlineDays = DefaultDeadlineDays)
        {
            return pendingSince.HasValue ? pendingSince.Value.AddDays(deadlineDays) : null;
        }

        /// <summary>Pending Stories (chờ duyệt) — có gắn cờ thời hạn duyệt (7 ngày). Admin thấy tất cả (kể cả đã được moderator claim/lock).</summary>
        [HttpGet("pending-stories")]
        public IActionResult GetPendingStories(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] string? claimFilter = null)
        {
            try
            {
                if (!GetCurrentUserId().HasValue)
                    return Unauthorized(new { message = "Không xác định user." });
                // moderatorId = null: không loại trừ mục đã claim bởi moderator khác → Admin thấy hết (kể cả đang bị lock).
                var result = _moderationService.GetPendingStories(page, pageSize, search, sortBy, sortOrder, categoryIdsFilter: null, moderatorId: null, claimFilter ?? "all");
                foreach (var item in result.Items)
                {
                    item.PendingSince = item.UpdatedAt;
                    item.DeadlineAt = ComputeDeadline(item.UpdatedAt);
                    item.TimeStatus = ComputeTimeStatus(item.UpdatedAt);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin GetPendingStories failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách truyện chờ duyệt", error = ex.Message });
            }
        }

        /// <summary>Pending Chapters (chờ duyệt) — có gắn cờ thời hạn duyệt (7 ngày). Admin thấy tất cả (kể cả đã được moderator claim/lock).</summary>
        [HttpGet("pending-chapters")]
        public IActionResult GetPendingChapters(
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
                if (!GetCurrentUserId().HasValue)
                    return Unauthorized(new { message = "Không xác định user." });
                // moderatorId = null: không loại trừ mục đã claim bởi moderator khác → Admin thấy hết (kể cả đang bị lock).
                var result = _moderationService.GetPendingChapters(page, pageSize, storyId, search, sortBy, sortOrder, categoryIdsFilter: null, moderatorId: null, claimFilter ?? "all");
                foreach (var item in result.Items)
                {
                    var since = item.UpdatedAt ?? item.CreatedAt;
                    item.PendingSince = since;
                    item.DeadlineAt = ComputeDeadline(since);
                    item.TimeStatus = ComputeTimeStatus(since);
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin GetPendingChapters failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách chapter chờ duyệt", error = ex.Message });
            }
        }

        /// <summary>Approved Stories — có thể lọc theo Moderator, Date.</summary>
        [HttpGet("approved-stories")]
        public IActionResult GetApprovedStories(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] Guid? moderatorId = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var result = _moderationService.GetReviewedStories(page, pageSize, "PUBLISHED", search, sortBy, sortOrder, categoryIdsFilter: null, currentUserId, isAdmin: true, moderatorIdFilter: moderatorId, dateFrom, dateTo);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin GetApprovedStories failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách truyện đã duyệt", error = ex.Message });
            }
        }

        /// <summary>Rejected Stories — có thể lọc theo Moderator, Date.</summary>
        [HttpGet("rejected-stories")]
        public IActionResult GetRejectedStories(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] Guid? moderatorId = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var result = _moderationService.GetReviewedStories(page, pageSize, "REJECTED", search, sortBy, sortOrder, categoryIdsFilter: null, currentUserId, isAdmin: true, moderatorIdFilter: moderatorId, dateFrom, dateTo);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin GetRejectedStories failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách truyện bị từ chối", error = ex.Message });
            }
        }

        /// <summary>Approved Chapters — có thể lọc theo Moderator, Date.</summary>
        [HttpGet("approved-chapters")]
        public IActionResult GetApprovedChapters(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] Guid? moderatorId = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var result = _moderationService.GetReviewedChapters(page, pageSize, "PUBLISHED", search, sortBy, sortOrder, categoryIdsFilter: null, currentUserId, isAdmin: true, moderatorIdFilter: moderatorId, dateFrom, dateTo);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin GetApprovedChapters failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách chapter đã duyệt", error = ex.Message });
            }
        }

        /// <summary>Rejected Chapters — có thể lọc theo Moderator, Date.</summary>
        [HttpGet("rejected-chapters")]
        public IActionResult GetRejectedChapters(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null,
            [FromQuery] Guid? moderatorId = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null)
        {
            try
            {
                var currentUserId = GetCurrentUserId();
                var result = _moderationService.GetReviewedChapters(page, pageSize, "REJECTED", search, sortBy, sortOrder, categoryIdsFilter: null, currentUserId, isAdmin: true, moderatorIdFilter: moderatorId, dateFrom, dateTo);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin GetRejectedChapters failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách chapter bị từ chối", error = ex.Message });
            }
        }

        /// <summary>Moderation Logs — lọc theo Moderator, Date, Action, TargetType.</summary>
        [HttpGet("logs")]
        public IActionResult GetModerationLogs(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] Guid? moderatorId = null,
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null,
            [FromQuery] string? action = null,
            [FromQuery] string? targetType = null)
        {
            try
            {
                var (logs, total) = ModerationLogDAO.GetModerationLogsPage(moderatorId, dateFrom, dateTo, action, targetType, page, pageSize);
                var items = new List<ModerationLogEntryDto>();
                foreach (var log in logs)
                {
                    string? targetTitle = null;
                    if (log.target_id.HasValue)
                    {
                        if (string.Equals(log.target_type, "STORY", StringComparison.OrdinalIgnoreCase))
                            targetTitle = StoryDAO.GetById(log.target_id.Value)?.title;
                        else if (string.Equals(log.target_type, "CHAPTER", StringComparison.OrdinalIgnoreCase))
                            targetTitle = ChapterDAO.GetById(log.target_id.Value)?.title;
                    }
                    items.Add(new ModerationLogEntryDto
                    {
                        Id = log.id,
                        TargetType = log.target_type ?? "",
                        TargetId = log.target_id,
                        TargetTitle = targetTitle,
                        Action = log.action,
                        ModeratorId = log.moderator_id,
                        ModeratorName = log.moderator_id.HasValue ? NotificationDAO.GetUserDisplayName(log.moderator_id.Value) : null,
                        CreatedAt = log.created_at,
                        RejectionReason = log.rejection_reason,
                        ProcessingTimeMs = log.processing_time_ms
                    });
                }
                return Ok(new { items, totalCount = total, page, pageSize });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin GetModerationLogs failed");
                return StatusCode(500, new { message = "Lỗi lấy moderation logs", error = ex.Message });
            }
        }

        /// <summary>Moderator Performance — Approved, Rejected, Total (để phát hiện moderator làm sai nhiều).</summary>
        [HttpGet("moderator-performance")]
        public IActionResult GetModeratorPerformance(
            [FromQuery] DateTime? dateFrom = null,
            [FromQuery] DateTime? dateTo = null)
        {
            try
            {
                var rows = ModerationLogDAO.GetModeratorPerformance(dateFrom, dateTo);
                var list = rows.Select(r => new ModeratorPerformanceDto
                {
                    ModeratorId = r.ModeratorId,
                    ModeratorName = NotificationDAO.GetUserDisplayName(r.ModeratorId),
                    ApprovedCount = r.ApprovedCount,
                    RejectedCount = r.RejectedCount
                }).ToList();
                return Ok(list);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin GetModeratorPerformance failed");
                return StatusCode(500, new { message = "Lỗi lấy thống kê moderator", error = ex.Message });
            }
        }
    }
}
