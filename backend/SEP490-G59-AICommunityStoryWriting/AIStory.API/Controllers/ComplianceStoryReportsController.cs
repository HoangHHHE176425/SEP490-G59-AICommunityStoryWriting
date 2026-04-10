using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BusinessObjects;
using Services.DTOs.StoryReports;
using Services.DTOs.Admin.Compliance;
using Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AIStory.API.Controllers;

[ApiController]
[Route("api/compliance/story-reports")]
[Authorize(Roles = "COMPLIANCE,ADMIN")]
public class ComplianceStoryReportsController : ControllerBase
{
    private const string SrcReportResolution = "REPORT_RESOLUTION";
    private const string SrcAdminActionRequest = "ADMIN_ACTION_REQUEST";
    private const string SrcLockRequest = "LOCK_REQUEST";
    private const string SrcViolationAction = "VIOLATION_ACTION";

    private readonly IStoryReportService _storyReportService;
    private readonly ILogger<ComplianceStoryReportsController> _logger;

    public ComplianceStoryReportsController(IStoryReportService storyReportService, ILogger<ComplianceStoryReportsController> logger)
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

    /// <summary>Báo cáo do chính tôi (COMPLIANCE) đã đánh dấu xử lý xong.</summary>
    [HttpGet("my-resolved-history")]
    public async Task<IActionResult> GetMyResolvedHistory([FromQuery] int page = 1, [FromQuery] int pageSize = 20, [FromQuery] string? search = null)
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue)
            return Unauthorized();
        try
        {
            var result = await _storyReportService.QueryMyResolvedComplianceReportsAsync(page, pageSize, uid.Value, search);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetMyResolvedHistory failed");
            return StatusCode(500, new { message = "Lỗi tải lịch sử.", error = ex.Message });
        }
    }

    /// <summary>Nhật ký hoạt động của chính compliance hiện tại (bao gồm xử lý report, gửi đơn, thao tác vi phạm).</summary>
    [HttpGet("my-activity-logs")]
    public async Task<IActionResult> GetMyActivityLogs(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? source = null,
        [FromQuery] string? action = null)
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue)
            return Unauthorized();

        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        await using var db = new StoryPlatformDbContext();

        var reportsQ = db.reports.AsNoTracking()
            .Where(r => r.compliance_resolved_by == uid.Value && r.resolved_at != null)
            .Select(r => new ComplianceLogItemDto
            {
                Source = SrcReportResolution,
                RowId = r.id,
                TargetType = r.target_type,
                TargetId = r.target_id,
                Status = r.status,
                Action = r.status,
                Message = r.status == "RESOLVED"
                    ? "Đã xử lý toàn bộ phiếu báo cáo đang mở."
                    : (r.status == "DISMISSED"
                        ? "Đã kết luận không đủ bằng chứng để xử lý."
                        : "Đã xử lý phiếu báo cáo."),
                CreatedAtUtc = r.resolved_at!.Value
            });

        var actionQ = db.compliance_admin_action_requests.AsNoTracking()
            .Where(x => x.requester_id == uid.Value)
            .Select(x => new ComplianceLogItemDto
            {
                Source = SrcAdminActionRequest,
                RowId = x.id,
                TargetType = "STORY",
                TargetId = x.story_id,
                Status = x.status,
                Action = x.request_kind,
                Message = x.message,
                CreatedAtUtc = x.created_at
            });

        var lockQ = db.compliance_report_lock_requests.AsNoTracking()
            .Where(x => x.requester_id == uid.Value)
            .Select(x => new ComplianceLogItemDto
            {
                Source = SrcLockRequest,
                RowId = x.id,
                TargetType = x.target_type,
                TargetId = x.target_id,
                Status = x.status,
                Action = x.resolution_action,
                Message = x.message,
                CreatedAtUtc = x.created_at
            });

        var violationQ = db.violation_logs.AsNoTracking()
            .Where(v => v.compliance_officer_id == uid.Value && v.created_at != null)
            .Select(v => new ComplianceLogItemDto
            {
                Source = SrcViolationAction,
                RowId = v.id,
                TargetType = v.target_type,
                TargetId = v.target_id,
                Status = "DONE",
                Action = v.penalty_type,
                Message = v.reason,
                CreatedAtUtc = v.created_at!.Value
            });

        var q = reportsQ.Concat(actionQ).Concat(lockQ).Concat(violationQ);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var s = search.Trim();
            if (Guid.TryParse(s, out var g))
                q = q.Where(x => x.RowId == g || x.TargetId == g);
            else
                q = q.Where(x =>
                    (x.Source != null && x.Source.Contains(s)) ||
                    (x.Action != null && x.Action.Contains(s)) ||
                    (x.Message != null && x.Message.Contains(s)) ||
                    (x.Status != null && x.Status.Contains(s)));
        }

        if (!string.IsNullOrWhiteSpace(source))
        {
            var src = source.Trim().ToUpper();
            if (src != "ALL")
                q = q.Where(x => x.Source != null && x.Source.ToUpper() == src);
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var act = action.Trim().ToUpper();
            if (act != "ALL")
                q = q.Where(x => x.Action != null && x.Action.ToUpper() == act);
        }

        q = q.OrderByDescending(x => x.CreatedAtUtc).ThenByDescending(x => x.RowId);
        var total = await q.CountAsync();
        var rows = await q.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

        return Ok(new
        {
            items = rows,
            totalCount = total,
            page,
            pageSize
        });
    }

    [HttpGet]
    public async Task<IActionResult> Query([FromQuery] ComplianceStoryReportQueryDto query)
    {
        try
        {
            var uid = GetCurrentUserId();
            var isAdmin = User.IsInRole("ADMIN");
            var result = await _storyReportService.QueryComplianceAsync(query, uid, isAdmin);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compliance query failed");
            return StatusCode(500, new { message = "Lỗi tải danh sách báo cáo.", error = ex.Message });
        }
    }

    /// <summary>COMPLIANCE đang lock truyện: đánh dấu một báo cáo đã xử lý (RESOLVED / DISMISSED).</summary>
    [HttpPost("{reportId:guid}/resolve")]
    public async Task<IActionResult> ComplianceResolveReport(Guid reportId, [FromBody] ComplianceResolveReportRequestDto? body)
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue)
            return Unauthorized();
        try
        {
            await _storyReportService.ComplianceResolveReportAsync(reportId, uid.Value, body, User.IsInRole("ADMIN"));
            return Ok(new { message = "Đã cập nhật trạng thái báo cáo." });
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
            _logger.LogError(ex, "ComplianceResolveReport failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>COMPLIANCE: lưu đánh dấu xác minh cho từng người báo (story_report_contributors).</summary>
    [HttpPost("stories/{storyId:guid}/contributor-verification")]
    public async Task<IActionResult> SetStoryContributorVerification(
        Guid storyId,
        [FromBody] SetComplianceStoryContributorVerifiedRequestDto? body)
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue)
            return Unauthorized();
        if (body == null)
            return BadRequest(new { message = "Body is required." });
        var actorIsAdmin = User.IsInRole("ADMIN");
        try
        {
            var n = await _storyReportService.SetComplianceStoryContributorVerifiedAsync(
                storyId,
                uid.Value,
                body,
                actorIsAdmin);
            return Ok(new { message = "Đã cập nhật đánh dấu xác minh.", updatedCount = n });
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
            _logger.LogError(ex, "SetStoryContributorVerification failed");
            return StatusCode(500, new { message = "Lỗi khi lưu đánh dấu xác minh.", error = ex.Message });
        }
    }

    /// <summary>COMPLIANCE đang lock truyện: đóng hết mọi báo cáo mở (NEW/IN_REVIEW) của truyện.</summary>
    [HttpPost("stories/{storyId:guid}/resolve-all-open")]
    public async Task<IActionResult> ComplianceResolveAllOpenForStory(Guid storyId, [FromBody] ComplianceResolveReportRequestDto? body)
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue)
            return Unauthorized();
        try
        {
            var n = await _storyReportService.ComplianceResolveOpenReportsForStoryAsync(storyId, uid.Value, body, User.IsInRole("ADMIN"));
            return Ok(new { message = n > 0 ? $"Đã đóng {n} báo cáo." : "Không còn báo cáo mở.", closedCount = n });
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
            _logger.LogError(ex, "ComplianceResolveAllOpenForStory failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [Authorize(Roles = "ADMIN")]
    [HttpPatch("{reportId:guid}")]
    public async Task<IActionResult> UpdateStatus(Guid reportId, [FromBody] UpdateStoryReportStatusRequestDto body)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();
        if (body == null || string.IsNullOrWhiteSpace(body.Status))
            return BadRequest(new { message = "Status is required." });
        try
        {
            var isAdmin = User.IsInRole("ADMIN");
            await _storyReportService.UpdateReportStatusAsync(reportId, userId.Value, body.Status, isAdmin);
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

    /// <summary>Nhận xử lý: lock truyện (không hạn xử lý; cảnh báo theo thời gian từ lúc nhận).</summary>
    [HttpPost("stories/{storyId:guid}/claim")]
    public async Task<IActionResult> ClaimStory(Guid storyId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();
        try
        {
            var result = await _storyReportService.ClaimStoryAsync(storyId, userId.Value);
            return Ok(new
            {
                message = $"Đã lock truyện ({result.OpenReportCount} báo cáo đang mở). Thời điểm nhận (UTC): {result.ClaimedAtUtc:O}. Người đọc vẫn có thể báo cáo thêm.",
                claimedCount = result.OpenReportCount,
                claimedAtUtc = result.ClaimedAtUtc
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claim story reports failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>Chỉ ADMIN gỡ lock tại đây. Compliance dùng POST request-release.</summary>
    [Authorize(Roles = "ADMIN")]
    [HttpPost("stories/{storyId:guid}/release-claim")]
    public async Task<IActionResult> ReleaseClaim(Guid storyId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();
        try
        {
            var isAdmin = User.IsInRole("ADMIN");
            var n = await _storyReportService.ReleaseComplianceStoryClaimAsync(storyId, userId.Value, isAdmin);
            return Ok(new { message = n > 0 ? $"Đã bỏ lock; {n} báo cáo trở lại NEW." : "Đã bỏ lock.", reopenedCount = n });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Release compliance claim failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>Compliance đang giữ lock gửi yêu cầu admin (gỡ lock / giao lại).</summary>
    [HttpPost("stories/{storyId:guid}/request-release")]
    public async Task<IActionResult> RequestRelease(Guid storyId, [FromBody] RequestComplianceLockReleaseDto? body)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();
        try
        {
            var id = await _storyReportService.RequestComplianceLockReleaseAsync(storyId, userId.Value, body);
            return Ok(new { message = "Đã gửi yêu cầu lên admin.", requestId = id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Request compliance release failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpPost("stories/{storyId:guid}/flag")]
    public async Task<IActionResult> SetFlag(Guid storyId, [FromBody] SetComplianceStoryFlagRequestDto? body)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();
        var isAdmin = User.IsInRole("ADMIN");
        try
        {
            await _storyReportService.SetStoryComplianceFlagAsync(storyId, userId.Value, body?.Flagged ?? false, body?.Note, isAdmin);
            return Ok(new { message = body?.Flagged == true ? "Đã gắn cờ." : "Đã bỏ cờ." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("stories/{storyId:guid}/comments-disabled")]
    public async Task<IActionResult> SetCommentsDisabled(Guid storyId, [FromBody] SetComplianceStoryBoolRequestDto? body)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();
        var isAdmin = User.IsInRole("ADMIN");
        try
        {
            await _storyReportService.SetStoryCommentsDisabledAsync(storyId, userId.Value, body?.Value ?? false, isAdmin);
            return Ok(new { message = "Đã cập nhật." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("stories/{storyId:guid}/compliance-hidden")]
    public async Task<IActionResult> SetComplianceHidden(Guid storyId, [FromBody] SetComplianceStoryBoolRequestDto? body)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();
        var isAdmin = User.IsInRole("ADMIN");
        try
        {
            await _storyReportService.SetStoryComplianceHiddenAsync(storyId, userId.Value, body?.Value ?? false, isAdmin);
            return Ok(new { message = "Đã cập nhật." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Bật/tắt tạm khóa quyền viết của tác giả truyện (compliance/admin, không qua đơn admin).</summary>
    [HttpPost("stories/{storyId:guid}/author-writing-suspended")]
    public async Task<IActionResult> SetAuthorWritingSuspended(Guid storyId, [FromBody] SetComplianceStoryBoolRequestDto? body)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();
        var isAdmin = User.IsInRole("ADMIN");
        try
        {
            await _storyReportService.SetAuthorWritingSuspendedByComplianceAsync(
                storyId, userId.Value, body?.Value ?? false, isAdmin);
            return Ok(new { message = "Đã cập nhật." });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("stories/{storyId:guid}/admin-action-requests")]
    public async Task<IActionResult> RequestAdminAction(Guid storyId, [FromBody] CreateComplianceAdminActionRequestDto? body)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized();
        if (body == null || string.IsNullOrWhiteSpace(body.RequestKind))
            return BadRequest(new { message = "RequestKind is required." });
        try
        {
            var id = await _storyReportService.RequestComplianceAdminActionAsync(storyId, userId.Value, body, User.IsInRole("ADMIN"));
            return Ok(new { message = "Đã gửi yêu cầu lên admin.", requestId = id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Đơn gỡ lock do chính tôi gửi (mọi trạng thái, kèm ghi chú khi admin xử lý).</summary>
    [HttpGet("my-lock-requests")]
    public async Task<IActionResult> ListMyLockRequests()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();
        try
        {
            var list = await _storyReportService.ListMyComplianceLockRequestsAsync(userId.Value);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListMyLockRequests failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    /// <summary>Đơn BAN / tạm đình chỉ viết do chính tôi gửi (mọi trạng thái).</summary>
    [HttpGet("my-admin-action-requests")]
    public async Task<IActionResult> ListMyAdminActionRequests()
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue)
            return Unauthorized();
        try
        {
            var list = await _storyReportService.ListMyComplianceAdminActionRequestsAsync(userId.Value);
            return Ok(list);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ListMyAdminActionRequests failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }

    [HttpGet("users/{userId:guid}/violations")]
    public async Task<IActionResult> ListUserViolations(Guid userId, [FromQuery] int take = 80)
    {
        var ok = User.IsInRole("ADMIN") || User.IsInRole("COMPLIANCE");
        try
        {
            var list = await _storyReportService.ListViolationsForUserAsync(userId, take, ok);
            return Ok(list);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
