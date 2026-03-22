using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.StoryReports;
using Services.Interfaces;

namespace AIStory.API.Controllers;

[ApiController]
[Route("api/admin/compliance-story-reports")]
[Authorize(Roles = "ADMIN")]
public class AdminComplianceStoryReportsController : ControllerBase
{
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
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Admin resolve compliance admin action request failed");
            return StatusCode(500, new { message = ex.Message });
        }
    }
}
