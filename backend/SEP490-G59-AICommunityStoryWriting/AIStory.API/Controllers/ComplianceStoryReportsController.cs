using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.StoryReports;
using Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AIStory.API.Controllers;

[ApiController]
[Route("api/compliance/story-reports")]
[Authorize(Roles = "COMPLIANCE,ADMIN")]
public class ComplianceStoryReportsController : ControllerBase
{
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
            await _storyReportService.ComplianceResolveReportAsync(reportId, uid.Value, body);
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

    /// <summary>COMPLIANCE đang lock truyện: đóng hết mọi báo cáo mở (NEW/IN_REVIEW) của truyện.</summary>
    [HttpPost("stories/{storyId:guid}/resolve-all-open")]
    public async Task<IActionResult> ComplianceResolveAllOpenForStory(Guid storyId, [FromBody] ComplianceResolveReportRequestDto? body)
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue)
            return Unauthorized();
        try
        {
            var n = await _storyReportService.ComplianceResolveOpenReportsForStoryAsync(storyId, uid.Value, body);
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
