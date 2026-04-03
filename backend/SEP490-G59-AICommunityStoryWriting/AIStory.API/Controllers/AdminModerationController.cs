using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Admin;
using Services.DTOs.Admin.Moderation;
using Services.DTOs.Chapters;
using Services.DTOs.Moderation;
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
        private readonly IModerationService _moderationService;
        private readonly IReviewEscalationService _reviewEscalationService;
        private readonly IAdminUnifiedEscalationService _adminUnifiedEscalationService;
        private readonly ILogger<AdminModerationController> _logger;

        public AdminModerationController(
            IModerationService moderationService,
            IReviewEscalationService reviewEscalationService,
            IAdminUnifiedEscalationService adminUnifiedEscalationService,
            ILogger<AdminModerationController> logger)
        {
            _moderationService = moderationService;
            _reviewEscalationService = reviewEscalationService;
            _adminUnifiedEscalationService = adminUnifiedEscalationService;
            _logger = logger;
        }

        private Guid? GetCurrentUserId()
        {
            var claim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            return claim != null && Guid.TryParse(claim.Value, out var userId) ? userId : null;
        }

        /// <summary>Pending Stories (chờ duyệt) — hạn/cờ thời gian: hạn moderator chọn khi đã nhận; chưa nhận thì gợi ý +7 ngày từ lúc gửi. Admin thấy tất cả (kể cả đã claim).</summary>
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
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin GetPendingStories failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách truyện chờ duyệt", error = ex.Message });
            }
        }

        /// <summary>Pending Chapters (chờ duyệt) — hạn/cờ thời gian như truyện. Admin thấy tất cả (kể cả đã claim).</summary>
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

        /// <summary>Moderation Logs — theo dõi hoạt động moderator: lọc đầy đủ, tìm kiếm, sắp xếp, phân trang.</summary>
        [HttpGet("logs")]
        public IActionResult GetModerationLogs([FromQuery] ModerationLogQueryDto? query)
        {
            try
            {
                if (!GetCurrentUserId().HasValue)
                    return Unauthorized(new { message = "Không xác định user." });

                query ??= new ModerationLogQueryDto();
                var page = query.Page < 1 ? 1 : query.Page;
                var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

                static DateTime? EndOfDayIfMidnight(DateTime? dt)
                {
                    if (!dt.HasValue) return null;
                    var d = dt.Value;
                    if (d.TimeOfDay != TimeSpan.Zero) return d;
                    return d.Date.AddDays(1).AddTicks(-1);
                }

                var dateTo = EndOfDayIfMidnight(query.DateTo);

                var (logs, total) = ModerationLogDAO.SearchModerationLogsPage(
                    query.Search,
                    query.ModeratorId,
                    query.DateFrom,
                    dateTo,
                    query.Action,
                    query.TargetType,
                    query.TargetId,
                    query.ProcessingTimeMinMs,
                    query.ProcessingTimeMaxMs,
                    query.SortBy,
                    query.SortOrder,
                    page,
                    pageSize);

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

                return Ok(new global::Services.DTOs.Admin.PagedResultDto<ModerationLogEntryDto>
                {
                    Items = items,
                    TotalCount = total,
                    Page = page,
                    PageSize = pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin GetModerationLogs failed");
                return StatusCode(500, new { message = "Lỗi lấy moderation logs", error = ex.Message });
            }
        }

        /// <summary>Moderator đang hoạt động — chọn khi duyệt đơn RELEASE và giao lại lock duyệt (không gồm admin).</summary>
        [HttpGet("moderators-for-assignment")]
        public IActionResult GetModeratorsForAssignment()
        {
            try
            {
                var rows = UserDAO.ListActiveModeratorsForAssignment();
                var items = rows.Select(x => new { id = x.Id, displayName = x.DisplayName, claimedAssignmentCount = x.ClaimedAssignmentCount }).ToList();
                return Ok(new { items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetModeratorsForAssignment failed");
                return StatusCode(500, new { message = "Lỗi danh sách người nhận duyệt", error = ex.Message });
            }
        }

        /// <summary>Đơn chờ admin — moderator + compliance (lock + hành động tài khoản), một danh sách. Lọc urgencyTier: CRITICAL | HIGH | STANDARD.</summary>
        [HttpGet("review-escalations/pending-unified")]
        public async Task<IActionResult> GetPendingUnifiedEscalations([FromQuery] string? urgencyTier = null)
        {
            try
            {
                if (!GetCurrentUserId().HasValue)
                    return Unauthorized(new { message = "Không xác định user." });
                var r = await _adminUnifiedEscalationService.GetPendingUnifiedAsync(urgencyTier);
                return Ok(new
                {
                    items = r.Items,
                    counts = new { critical = r.Critical, standard = r.Standard }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPendingUnifiedEscalations failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách đơn gửi admin", error = ex.Message });
            }
        }

        /// <summary>Đơn báo cáo hạn duyệt từ moderator — chỉ PENDING. Lọc urgencyTier: CRITICAL | STANDARD.</summary>
        [HttpGet("review-escalations/pending")]
        public IActionResult GetPendingReviewEscalations([FromQuery] string? urgencyTier = null)
        {
            try
            {
                if (!GetCurrentUserId().HasValue)
                    return Unauthorized(new { message = "Không xác định user." });
                var items = _reviewEscalationService.ListPendingForAdmin(urgencyTier);
                var counts = _reviewEscalationService.CountPendingUrgencyBuckets();
                return Ok(new
                {
                    items,
                    counts = new { critical = counts.critical, standard = counts.standard }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetPendingReviewEscalations failed");
                return StatusCode(500, new { message = "Lỗi lấy danh sách đơn báo cáo", error = ex.Message });
            }
        }

        /// <summary>Lịch sử đơn đã xử lý (APPROVED / REJECTED) — xem lại sau khi admin xử lý.</summary>
        [HttpGet("review-escalations/history")]
        public IActionResult GetReviewEscalationHistory([FromQuery] int skip = 0, [FromQuery] int take = 200)
        {
            try
            {
                if (!GetCurrentUserId().HasValue)
                    return Unauthorized(new { message = "Không xác định user." });
                var items = _reviewEscalationService.ListResolvedHistoryForAdmin(skip, take);
                var total = _reviewEscalationService.CountResolvedHistory();
                return Ok(new { items, totalCount = total, skip, take });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetReviewEscalationHistory failed");
                return StatusCode(500, new { message = "Lỗi lấy lịch sử đơn", error = ex.Message });
            }
        }

        /// <summary>Log đầy đủ đơn escalation moderator: lọc, tìm kiếm, phân trang (review_escalation_requests).</summary>
        [HttpGet("review-escalations/log")]
        public IActionResult GetReviewEscalationLog([FromQuery] ReviewEscalationLogQueryDto query)
        {
            try
            {
                if (!GetCurrentUserId().HasValue)
                    return Unauthorized(new { message = "Không xác định user." });
                var result = _reviewEscalationService.SearchEscalationLogForAdmin(query ?? new ReviewEscalationLogQueryDto());
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetReviewEscalationLog failed");
                return StatusCode(500, new { message = "Lỗi lấy log đơn escalation", error = ex.Message });
            }
        }

        /// <summary>Log thống nhất đơn gửi admin: moderator escalation + compliance gỡ lock + compliance hành động tài khoản.</summary>
        [HttpGet("review-escalations/unified-log")]
        public async Task<IActionResult> GetUnifiedEscalationLog([FromQuery] UnifiedEscalationLogQueryDto query)
        {
            try
            {
                if (!GetCurrentUserId().HasValue)
                    return Unauthorized(new { message = "Không xác định user." });
                var result = await _adminUnifiedEscalationService.SearchUnifiedLogAsync(query ?? new UnifiedEscalationLogQueryDto());
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "GetUnifiedEscalationLog failed");
                return StatusCode(500, new { message = "Lỗi lấy log đơn gửi admin", error = ex.Message });
            }
        }

        /// <summary>Admin xử lý đơn: duyệt (gia hạn / hủy lock) hoặc từ chối.</summary>
        [HttpPost("review-escalations/{id:guid}/resolve")]
        public IActionResult ResolveReviewEscalation(Guid id, [FromBody] AdminResolveReviewEscalationDto? body)
        {
            var adminId = GetCurrentUserId();
            if (!adminId.HasValue)
                return Unauthorized(new { message = "Không xác định user." });
            if (body == null)
                return BadRequest(new { message = "Body không hợp lệ." });
            try
            {
                _reviewEscalationService.Resolve(adminId.Value, id, body);
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
                _logger.LogError(ex, "ResolveReviewEscalation {RequestId} failed", id);
                return StatusCode(500, new { message = "Lỗi xử lý đơn", error = ex.Message });
            }
        }

        /// <summary>Moderator Performance — thống kê theo moderator: lọc, tìm kiếm, sắp xếp, phân trang.</summary>
        [HttpGet("moderator-performance")]
        public IActionResult GetModeratorPerformance([FromQuery] ModeratorPerformanceQueryDto? query)
        {
            try
            {
                query ??= new ModeratorPerformanceQueryDto();
                var page = query.Page < 1 ? 1 : query.Page;
                var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

                static DateTime? EndOfDayIfMidnight(DateTime? dt)
                {
                    if (!dt.HasValue) return null;
                    var d = dt.Value;
                    if (d.TimeOfDay != TimeSpan.Zero) return d;
                    return d.Date.AddDays(1).AddTicks(-1);
                }

                var dateTo = EndOfDayIfMidnight(query.DateTo);

                var rows = ModerationLogDAO.GetModeratorPerformanceAggregates(query.DateFrom, dateTo, query.TargetType);
                var list = rows.Select(r => new ModeratorPerformanceDto
                {
                    ModeratorId = r.ModeratorId,
                    ModeratorName = NotificationDAO.GetUserDisplayName(r.ModeratorId),
                    ApprovedCount = r.ApprovedCount,
                    RejectedCount = r.RejectedCount,
                    StoryApprovedCount = r.StoryApprovedCount,
                    StoryRejectedCount = r.StoryRejectedCount,
                    ChapterApprovedCount = r.ChapterApprovedCount,
                    ChapterRejectedCount = r.ChapterRejectedCount
                }).ToList();

                if (query.ModeratorId.HasValue)
                    list = list.Where(x => x.ModeratorId == query.ModeratorId.Value).ToList();

                if (!string.IsNullOrWhiteSpace(query.Search))
                {
                    var s = query.Search.Trim();
                    if (Guid.TryParse(s, out var g))
                    {
                        list = list.Where(x => x.ModeratorId == g).ToList();
                    }
                    else
                    {
                        var idMatches = UserDAO.SearchUserIdsByEmailOrNickname(s);
                        var idSet = new HashSet<Guid>(idMatches);
                        list = list.Where(x =>
                            idSet.Contains(x.ModeratorId) ||
                            (!string.IsNullOrEmpty(x.ModeratorName) &&
                             x.ModeratorName.Contains(s, StringComparison.OrdinalIgnoreCase))).ToList();
                    }
                }

                if (query.MinTotalActions.HasValue && query.MinTotalActions.Value > 0)
                    list = list.Where(x => x.Total >= query.MinTotalActions.Value).ToList();

                var sortAsc = string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
                var sortBy = (query.SortBy ?? "total").Trim().ToLowerInvariant();
                list = sortBy switch
                {
                    "approved" => sortAsc
                        ? list.OrderBy(x => x.ApprovedCount).ThenBy(x => x.ModeratorName).ToList()
                        : list.OrderByDescending(x => x.ApprovedCount).ThenBy(x => x.ModeratorName).ToList(),
                    "rejected" => sortAsc
                        ? list.OrderBy(x => x.RejectedCount).ThenBy(x => x.ModeratorName).ToList()
                        : list.OrderByDescending(x => x.RejectedCount).ThenBy(x => x.ModeratorName).ToList(),
                    "reject_ratio" => sortAsc
                        ? list.OrderBy(x => x.RejectRatio ?? 0).ThenBy(x => x.ModeratorName).ToList()
                        : list.OrderByDescending(x => x.RejectRatio ?? 0).ThenBy(x => x.ModeratorName).ToList(),
                    "story_approved" => sortAsc
                        ? list.OrderBy(x => x.StoryApprovedCount).ThenBy(x => x.ModeratorName).ToList()
                        : list.OrderByDescending(x => x.StoryApprovedCount).ThenBy(x => x.ModeratorName).ToList(),
                    "chapter_approved" => sortAsc
                        ? list.OrderBy(x => x.ChapterApprovedCount).ThenBy(x => x.ModeratorName).ToList()
                        : list.OrderByDescending(x => x.ChapterApprovedCount).ThenBy(x => x.ModeratorName).ToList(),
                    "name" => sortAsc
                        ? list.OrderBy(x => x.ModeratorName ?? "", StringComparer.OrdinalIgnoreCase).ToList()
                        : list.OrderByDescending(x => x.ModeratorName ?? "", StringComparer.OrdinalIgnoreCase).ToList(),
                    _ => sortAsc
                        ? list.OrderBy(x => x.Total).ThenBy(x => x.ModeratorName).ToList()
                        : list.OrderByDescending(x => x.Total).ThenBy(x => x.ModeratorName).ToList()
                };

                var totalCount = list.Count;
                var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();

                return Ok(new global::Services.DTOs.Admin.PagedResultDto<ModeratorPerformanceDto>
                {
                    Items = items,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Admin GetModeratorPerformance failed");
                return StatusCode(500, new { message = "Lỗi lấy thống kê moderator", error = ex.Message });
            }
        }
    }
}
