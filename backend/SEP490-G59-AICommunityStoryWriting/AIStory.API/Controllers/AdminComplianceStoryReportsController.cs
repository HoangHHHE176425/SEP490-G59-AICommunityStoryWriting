using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BusinessObjects;
using DataAccessObjects.DAOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.DTOs.Admin;
using Services.DTOs.Admin.Compliance;
using Services.DTOs.StoryReports;
using Services.Interfaces;

namespace AIStory.API.Controllers;

[ApiController]
[Route("api/admin/compliance-story-reports")]
[Authorize(Roles = "ADMIN")]
public class AdminComplianceStoryReportsController : ControllerBase
{
    private const string SrcReportResolution = "REPORT_RESOLUTION";
    private const string SrcAdminActionRequest = "ADMIN_ACTION_REQUEST";
    private const string SrcLockRequest = "LOCK_REQUEST";
    private const string SrcViolationAction = "VIOLATION_ACTION";
    private readonly IStoryReportService _storyReportService;
    private readonly ILogger<AdminComplianceStoryReportsController> _logger;

    public AdminComplianceStoryReportsController(
        IStoryReportService storyReportService,
        ILogger<AdminComplianceStoryReportsController> logger)
    {
        _storyReportService = storyReportService;
        _logger = logger;
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    /// <summary>Yêu cầu gỡ lock / giao lại từ compliance (mặc định PENDING).</summary>
    [HttpGet("lock-requests")]
    public async Task<IActionResult> ListLockRequests([FromQuery] string? status = "PENDING")
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue) return Unauthorized();
        try
        {
            var list = await _storyReportService.AdminListComplianceLockRequestsAsync(status);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin list compliance lock requests failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>COMPLIANCE đang hoạt động + số truyện đang giữ lock báo cáo (để cân tải).</summary>
    [HttpGet("compliance-officers")]
    public async Task<IActionResult> ListComplianceOfficers()
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue) return Unauthorized();
        try
        {
            var list = await _storyReportService.AdminListComplianceOfficersForAssignmentAsync();
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin list compliance officers failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>Duyệt yêu cầu: APPROVE_UNLOCK | APPROVE_REASSIGN | REJECT.</summary>
    [HttpPost("lock-requests/{requestId:guid}/resolve")]
    public async Task<IActionResult> ResolveLockRequest(Guid requestId, [FromBody] AdminResolveComplianceLockRequestDto body)
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue) return Unauthorized();
        if (body == null || string.IsNullOrWhiteSpace(body.Decision))
            return BadRequest(new { message = "Decision is required." });
        try
        {
            await _storyReportService.AdminResolveComplianceLockRequestAsync(requestId, uid.Value, body);
            return Ok(new { message = "Đã xử lý yêu cầu." });
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
            _logger.LogError(ex, "Admin resolve compliance lock request failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>Gỡ lock trực tiếp (không qua yêu cầu) — giống moderator bỏ nhận.</summary>
    [HttpPost("stories/{storyId:guid}/release-claim")]
    public async Task<IActionResult> AdminReleaseClaim(Guid storyId)
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue) return Unauthorized();
        try
        {
            var n = await _storyReportService.ReleaseComplianceStoryClaimAsync(storyId, uid.Value, actorIsAdmin: true);
            return Ok(new { message = n > 0 ? $"Đã gỡ lock; {n} báo cáo IN_REVIEW → NEW." : "Đã gỡ lock.", reopenedCount = n });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Đổi trạng thái báo cáo (chỉ admin).</summary>
    [HttpPatch("{reportId:guid}/status")]
    public async Task<IActionResult> AdminUpdateReportStatus(Guid reportId, [FromBody] UpdateStoryReportStatusRequestDto body)
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue) return Unauthorized();
        if (body == null || string.IsNullOrWhiteSpace(body.Status))
            return BadRequest(new { message = "Status is required." });
        try
        {
            await _storyReportService.UpdateReportStatusAsync(reportId, uid.Value, body.Status, actorIsAdmin: true);
            return Ok(new { message = "Đã cập nhật trạng thái." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("admin-action-requests")]
    public async Task<IActionResult> ListAdminActionRequests([FromQuery] string? status = "PENDING")
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue) return Unauthorized();
        try
        {
            var list = await _storyReportService.AdminListComplianceAdminActionRequestsAsync(status);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin list compliance admin action requests failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("admin-action-requests/{requestId:guid}/resolve")]
    public async Task<IActionResult> ResolveAdminActionRequest(Guid requestId, [FromBody] AdminResolveComplianceAdminActionRequestDto body)
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue) return Unauthorized();
        if (body == null || string.IsNullOrWhiteSpace(body.Decision))
            return BadRequest(new { message = "Decision is required." });
        var decision = (body.Decision ?? "").Trim().ToUpperInvariant();
        if (body.AdminNote != null && body.AdminNote.Length > 2000)
            return BadRequest(new { message = "Ký tự quá dài: mô tả tối đa 2000 ký tự." });
        // Resolve đơn compliance admin-action (chặn tài khoản / đình chỉ viết) không dùng StoryReportReasonCatalog;
        // lý do chi tiết nằm ở nội dung đơn + ghi chú admin.
        if (requestId == Guid.Empty)
        {
            _logger.LogWarning("Không tìm thấy comment.");
            return BadRequest(new { message = "Không tìm thấy comment." });
        }
        try
        {
            await _storyReportService.AdminResolveComplianceAdminActionRequestAsync(requestId, uid.Value, body);
            return Ok(new { message = "Đã xử lý yêu cầu." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            if (string.Equals(ex.Message, "Không tìm thấy truyện.", StringComparison.Ordinal))
                _logger.LogWarning("Không tìm thấy truyện");
            else if (string.Equals(ex.Message, "Truyện chưa được PUBLISH", StringComparison.Ordinal))
                _logger.LogWarning("Truyện chưa được PUBLISH");
            else if (string.Equals(ex.Message, "Không thể tự báo cáo chính mình", StringComparison.Ordinal))
                _logger.LogWarning("Không thể tự báo cáo chính mình");
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin resolve compliance admin action request failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>Admin: log hoạt động compliance (resolve report + gửi đơn admin + lock request).</summary>
    [HttpGet("compliance-logs")]
    public async Task<IActionResult> GetComplianceLogs([FromQuery] ComplianceLogQueryDto? query)
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue) return Unauthorized();
        query ??= new ComplianceLogQueryDto();

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        static DateTime? EndOfDayIfMidnight(DateTime? dt)
        {
            if (!dt.HasValue) return null;
            var d = dt.Value;
            return d.TimeOfDay == TimeSpan.Zero ? d.Date.AddDays(1).AddTicks(-1) : d;
        }

        var to = EndOfDayIfMidnight(query.DateTo);
        await using var db = new StoryPlatformDbContext();
        var complianceUsersQ = db.users.AsNoTracking()
            .Where(u => u.role != null && u.role.ToUpper() == "COMPLIANCE")
            .Select(u => u.id);

        var reportsQ = db.reports.AsNoTracking()
            .Where(r => r.compliance_resolved_by != null && r.resolved_at != null)
            .Where(r => complianceUsersQ.Contains(r.compliance_resolved_by!.Value))
            .Select(r => new ComplianceLogItemDto
            {
                Source = SrcReportResolution,
                RowId = r.id,
                ComplianceUserId = r.compliance_resolved_by!.Value,
                ComplianceUserName = null,
                TargetType = r.target_type,
                TargetId = r.target_id,
                Status = r.status,
                Action = r.status,
                Message = r.description,
                CreatedAtUtc = r.resolved_at!.Value,
                ResolvedAtUtc = r.resolved_at
            });

        var actionQ = db.compliance_admin_action_requests.AsNoTracking()
            .Where(x => complianceUsersQ.Contains(x.requester_id))
            .Select(x => new ComplianceLogItemDto
            {
                Source = SrcAdminActionRequest,
                RowId = x.id,
                ComplianceUserId = x.requester_id,
                ComplianceUserName = null,
                TargetType = x.request_kind == "BAN_USER" || x.request_kind == "SUSPEND_AUTHOR_WRITING"
                    ? "USER"
                    : "STORY",
                TargetId = x.request_kind == "BAN_USER" || x.request_kind == "SUSPEND_AUTHOR_WRITING"
                    ? x.target_user_id
                    : x.story_id,
                Status = x.status,
                Action = x.request_kind,
                Message = x.message,
                CreatedAtUtc = x.created_at,
                ResolvedAtUtc = x.resolved_at
            });

        var lockQ = db.compliance_report_lock_requests.AsNoTracking()
            .Where(x => complianceUsersQ.Contains(x.requester_id))
            .Select(x => new ComplianceLogItemDto
            {
                Source = SrcLockRequest,
                RowId = x.id,
                ComplianceUserId = x.requester_id,
                ComplianceUserName = null,
                TargetType = x.target_type,
                TargetId = x.target_id,
                Status = x.status,
                Action = x.resolution_action,
                Message = x.message,
                CreatedAtUtc = x.created_at,
                ResolvedAtUtc = x.resolved_at
            });

        var violationQ = db.violation_logs.AsNoTracking()
            .Where(v => v.compliance_officer_id != null && v.created_at != null)
            .Where(v => complianceUsersQ.Contains(v.compliance_officer_id!.Value))
            .Select(v => new ComplianceLogItemDto
            {
                Source = SrcViolationAction,
                RowId = v.id,
                ComplianceUserId = v.compliance_officer_id!.Value,
                ComplianceUserName = null,
                TargetType = v.target_type,
                TargetId = v.target_id,
                Status = "DONE",
                Action = v.penalty_type,
                Message = v.reason,
                CreatedAtUtc = v.created_at!.Value,
                ResolvedAtUtc = v.created_at
            });

        IQueryable<ComplianceLogItemDto> q = (query.Source ?? "").Trim().ToUpperInvariant() switch
        {
            SrcReportResolution => reportsQ,
            SrcAdminActionRequest => actionQ,
            SrcLockRequest => lockQ,
            SrcViolationAction => violationQ,
            _ => reportsQ.Concat(actionQ).Concat(lockQ).Concat(violationQ)
        };

        if (query.ComplianceUserId.HasValue)
            q = q.Where(x => x.ComplianceUserId == query.ComplianceUserId.Value);
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var st = query.Status.Trim().ToUpperInvariant();
            q = q.Where(x => x.Status != null && x.Status.ToUpper() == st);
        }
        if (query.DateFrom.HasValue)
            q = q.Where(x => x.CreatedAtUtc >= query.DateFrom.Value);
        if (to.HasValue)
            q = q.Where(x => x.CreatedAtUtc <= to.Value);
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            if (Guid.TryParse(s, out var g))
                q = q.Where(x => x.RowId == g || x.ComplianceUserId == g || x.TargetId == g);
            else
                q = q.Where(x =>
                    (x.Message != null && x.Message.Contains(s)) ||
                    (x.Action != null && x.Action.Contains(s)) ||
                    (x.Status != null && x.Status.Contains(s)));
        }

        var sortAsc = string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        var sortBy = (query.SortBy ?? "created_at").Trim().ToLowerInvariant();
        q = sortBy switch
        {
            "source" => sortAsc ? q.OrderBy(x => x.Source).ThenBy(x => x.CreatedAtUtc) : q.OrderByDescending(x => x.Source).ThenByDescending(x => x.CreatedAtUtc),
            "status" => sortAsc ? q.OrderBy(x => x.Status).ThenBy(x => x.CreatedAtUtc) : q.OrderByDescending(x => x.Status).ThenByDescending(x => x.CreatedAtUtc),
            _ => sortAsc ? q.OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.RowId) : q.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.RowId)
        };

        var total = await q.CountAsync();
        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();
        var storyIds = rows
            .Where(x => string.Equals((x.TargetType ?? "").Trim(), "STORY", StringComparison.OrdinalIgnoreCase) && x.TargetId.HasValue)
            .Select(x => x.TargetId!.Value)
            .Distinct()
            .ToList();
        var commentIds = rows
            .Where(x => string.Equals((x.TargetType ?? "").Trim(), "COMMENT", StringComparison.OrdinalIgnoreCase) && x.TargetId.HasValue)
            .Select(x => x.TargetId!.Value)
            .Distinct()
            .ToList();
        var userIds = rows
            .Where(x => string.Equals((x.TargetType ?? "").Trim(), "USER", StringComparison.OrdinalIgnoreCase) && x.TargetId.HasValue)
            .Select(x => x.TargetId!.Value)
            .Distinct()
            .ToList();

        var storyMap = await db.stories.AsNoTracking()
            .Where(s => storyIds.Contains(s.id))
            .Select(s => new { s.id, s.title, s.author_id })
            .ToDictionaryAsync(s => s.id, s => new { s.title, s.author_id });

        var commentMap = await db.comments.AsNoTracking()
            .Where(c => commentIds.Contains(c.id))
            .Select(c => new { c.id, c.story_id, c.user_id })
            .ToDictionaryAsync(c => c.id, c => new { c.story_id, c.user_id });

        var commentStoryIds = commentMap.Values
            .Where(x => x.story_id.HasValue)
            .Select(x => x.story_id!.Value)
            .Distinct()
            .Where(id => !storyMap.ContainsKey(id))
            .ToList();
        if (commentStoryIds.Count > 0)
        {
            var extraStoryMap = await db.stories.AsNoTracking()
                .Where(s => commentStoryIds.Contains(s.id))
                .Select(s => new { s.id, s.title, s.author_id })
                .ToDictionaryAsync(s => s.id, s => new { s.title, s.author_id });
            foreach (var kv in extraStoryMap) storyMap[kv.Key] = kv.Value;
        }

        foreach (var r in rows)
        {
            r.ComplianceUserName = NotificationDAO.GetUserDisplayName(r.ComplianceUserId);
            var t = (r.TargetType ?? "").Trim().ToUpperInvariant();
            if (t == "STORY" && r.TargetId.HasValue)
            {
                var id = r.TargetId.Value;
                if (storyMap.TryGetValue(id, out var s))
                {
                    var title = string.IsNullOrWhiteSpace(s.title) ? id.ToString() : s.title!;
                    r.TargetLabel = $"Truyện: {title}";
                    if (s.author_id.HasValue && s.author_id.Value != Guid.Empty)
                        r.OwnerLabel = $"Tác giả: {NotificationDAO.GetUserDisplayName(s.author_id.Value)}";
                }
                else
                {
                    r.TargetLabel = $"Truyện: {id}";
                }
            }
            else if (t == "COMMENT" && r.TargetId.HasValue)
            {
                var id = r.TargetId.Value;
                r.TargetLabel = $"Bình luận: {id}";
                if (commentMap.TryGetValue(id, out var c))
                {
                    if (c.user_id.HasValue && c.user_id.Value != Guid.Empty)
                        r.OwnerLabel = $"Người bình luận: {NotificationDAO.GetUserDisplayName(c.user_id.Value)}";
                    if (c.story_id.HasValue && storyMap.TryGetValue(c.story_id.Value, out var s) && !string.IsNullOrWhiteSpace(s.title))
                        r.TargetLabel = $"Bình luận trong truyện: {s.title}";
                }
            }
            else if (t == "USER" && r.TargetId.HasValue)
            {
                var id = r.TargetId.Value;
                var display = NotificationDAO.GetUserDisplayName(id);
                r.TargetLabel = $"Tài khoản: {display}";
                r.OwnerLabel = $"Chủ tài khoản: {display}";
            }
            else if (r.TargetId.HasValue)
            {
                r.TargetLabel = $"{(string.IsNullOrWhiteSpace(r.TargetType) ? "Đối tượng" : r.TargetType)}: {r.TargetId.Value}";
            }
        }

        return Ok(new PagedResultDto<ComplianceLogItemDto>
        {
            Items = rows,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }

    /// <summary>Admin: thống kê hiệu suất compliance (resolve report + gửi đơn admin + lock request).</summary>
    [HttpGet("compliance-performance")]
    public async Task<IActionResult> GetCompliancePerformance([FromQuery] CompliancePerformanceQueryDto? query)
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue) return Unauthorized();
        query ??= new CompliancePerformanceQueryDto();

        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

        static DateTime? EndOfDayIfMidnight(DateTime? dt)
        {
            if (!dt.HasValue) return null;
            var d = dt.Value;
            return d.TimeOfDay == TimeSpan.Zero ? d.Date.AddDays(1).AddTicks(-1) : d;
        }

        var to = EndOfDayIfMidnight(query.DateTo);
        await using var db = new StoryPlatformDbContext();

        var reportRows = await db.reports.AsNoTracking()
            .Where(r => r.compliance_resolved_by != null && r.resolved_at != null)
            .Where(r => !query.DateFrom.HasValue || r.resolved_at >= query.DateFrom.Value)
            .Where(r => !to.HasValue || r.resolved_at <= to.Value)
            .Select(r => new
            {
                UserId = r.compliance_resolved_by!.Value,
                Status = (r.status ?? "").Trim().ToUpper(),
                TargetType = (r.target_type ?? "").Trim().ToUpper()
            })
            .ToListAsync();

        var adminActionRows = await db.compliance_admin_action_requests.AsNoTracking()
            .Where(x => !query.DateFrom.HasValue || x.created_at >= query.DateFrom.Value)
            .Where(x => !to.HasValue || x.created_at <= to.Value)
            .Select(x => x.requester_id)
            .ToListAsync();

        var lockReqRows = await db.compliance_report_lock_requests.AsNoTracking()
            .Where(x => !query.DateFrom.HasValue || x.created_at >= query.DateFrom.Value)
            .Where(x => !to.HasValue || x.created_at <= to.Value)
            .Select(x => x.requester_id)
            .ToListAsync();

        var userIds = reportRows.Select(x => x.UserId)
            .Concat(adminActionRows)
            .Concat(lockReqRows)
            .Distinct()
            .ToList();

        var list = userIds.Select(id =>
        {
            var rows = reportRows.Where(x => x.UserId == id).ToList();
            var resolved = rows.Count(x => x.Status == "RESOLVED");
            var dismissed = rows.Count(x => x.Status == "DISMISSED");
            return new CompliancePerformanceDto
            {
                ComplianceUserId = id,
                ComplianceUserName = NotificationDAO.GetUserDisplayName(id),
                ResolvedCount = resolved,
                DismissedCount = dismissed,
                StoryReportResolvedCount = rows.Count(x => x.TargetType == "STORY" && x.Status == "RESOLVED"),
                CommentReportResolvedCount = rows.Count(x => x.TargetType == "COMMENT" && x.Status == "RESOLVED"),
                AdminActionRequestCount = adminActionRows.Count(x => x == id),
                LockRequestCount = lockReqRows.Count(x => x == id)
            };
        }).ToList();

        if (query.ComplianceUserId.HasValue)
            list = list.Where(x => x.ComplianceUserId == query.ComplianceUserId.Value).ToList();
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            if (Guid.TryParse(s, out var g))
                list = list.Where(x => x.ComplianceUserId == g).ToList();
            else
                list = list.Where(x => (x.ComplianceUserName ?? "").Contains(s, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        var sortAsc = string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        var sortBy = (query.SortBy ?? "total").Trim().ToLowerInvariant();
        list = sortBy switch
        {
            "resolved" => sortAsc ? list.OrderBy(x => x.ResolvedCount).ThenBy(x => x.ComplianceUserName).ToList() : list.OrderByDescending(x => x.ResolvedCount).ThenBy(x => x.ComplianceUserName).ToList(),
            "dismissed" => sortAsc ? list.OrderBy(x => x.DismissedCount).ThenBy(x => x.ComplianceUserName).ToList() : list.OrderByDescending(x => x.DismissedCount).ThenBy(x => x.ComplianceUserName).ToList(),
            "admin_actions" => sortAsc ? list.OrderBy(x => x.AdminActionRequestCount).ThenBy(x => x.ComplianceUserName).ToList() : list.OrderByDescending(x => x.AdminActionRequestCount).ThenBy(x => x.ComplianceUserName).ToList(),
            "lock_requests" => sortAsc ? list.OrderBy(x => x.LockRequestCount).ThenBy(x => x.ComplianceUserName).ToList() : list.OrderByDescending(x => x.LockRequestCount).ThenBy(x => x.ComplianceUserName).ToList(),
            "name" => sortAsc ? list.OrderBy(x => x.ComplianceUserName ?? "", StringComparer.OrdinalIgnoreCase).ToList() : list.OrderByDescending(x => x.ComplianceUserName ?? "", StringComparer.OrdinalIgnoreCase).ToList(),
            _ => sortAsc ? list.OrderBy(x => x.Total).ThenBy(x => x.ComplianceUserName).ToList() : list.OrderByDescending(x => x.Total).ThenBy(x => x.ComplianceUserName).ToList()
        };

        var total = list.Count;
        var items = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
        return Ok(new PagedResultDto<CompliancePerformanceDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        });
    }
}
