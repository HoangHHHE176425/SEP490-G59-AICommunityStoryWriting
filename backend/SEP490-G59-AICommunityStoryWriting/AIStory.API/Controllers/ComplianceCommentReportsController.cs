using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.CommentReports;
using Services.DTOs.StoryReports;
using Services.Interfaces;

namespace AIStory.API.Controllers;

[ApiController]
[Route("api/compliance/comment-reports")]
[Authorize(Roles = "COMPLIANCE,ADMIN")]
public class ComplianceCommentReportsController : ControllerBase
{
    private readonly ICommentReportService _commentReportService;

    public ComplianceCommentReportsController(ICommentReportService commentReportService)
    {
        _commentReportService = commentReportService;
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    [HttpGet]
    public async Task<IActionResult> QueryOpenCommentReports(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = "NEW,IN_REVIEW",
        [FromQuery] string? search = null,
        [FromQuery] string? claimFilter = null)
    {
        try
        {
            var actingUserId = GetCurrentUserId();
            var viewerIsAdmin = User.IsInRole("ADMIN");
            var result = await _commentReportService.QueryComplianceOpenCommentReportsAsync(page, pageSize, status, search, actingUserId, viewerIsAdmin, claimFilter);
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi tải danh sách report comment.", error = ex.Message });
        }
    }

    [HttpPost("{reportId:guid}/resolve")]
    public async Task<IActionResult> ResolveCommentReport(Guid reportId, [FromBody] ComplianceResolveCommentReportRequestDto? body)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(new { message = "Cần đăng nhập." });
        var actorIsAdmin = User.IsInRole("ADMIN");
        try
        {
            await _commentReportService.ComplianceResolveReportAsync(reportId, userId.Value, body, actorIsAdmin);
            return Ok(new { message = "Đã cập nhật trạng thái & (nếu RESOLVED) ẩn comment." });
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
            return StatusCode(500, new { message = "Lỗi khi xử lý report.", error = ex.Message });
        }
    }

    [HttpPost("comments/{commentId:guid}/hidden")]
    public async Task<IActionResult> SetCommentHidden(Guid commentId, [FromBody] SetCommentThreadHiddenRequestDto? body)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(new { message = "Cần đăng nhập." });
        if (body == null) body = new SetCommentThreadHiddenRequestDto();
        var actorIsAdmin = User.IsInRole("ADMIN");
        try
        {
            await _commentReportService.SetCommentThreadHiddenAsync(commentId, userId.Value, body.Value, body.IncludeReplies, actorIsAdmin);
            return Ok(new { message = "Đã cập nhật trạng thái ẩn/hiện comment." });
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
            return StatusCode(500, new { message = "Lỗi khi ẩn/hiện comment.", error = ex.Message });
        }
    }

    /// <summary>Bật/tắt tạm khóa quyền viết (mặc định user của comment; có thể gửi TargetUserId), không qua đơn admin.</summary>
    [HttpPost("comments/{commentId:guid}/author-writing-suspended")]
    public async Task<IActionResult> SetAuthorWritingSuspended(
        Guid commentId,
        [FromBody] SetComplianceCommentAuthorWritingSuspendedRequestDto? body)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(new { message = "Cần đăng nhập." });
        if (body == null) return BadRequest(new { message = "Body is required." });
        var actorIsAdmin = User.IsInRole("ADMIN");
        try
        {
            await _commentReportService.SetAuthorWritingSuspendedByComplianceAsync(
                commentId, userId.Value, body, actorIsAdmin);
            return Ok(new { message = "Đã cập nhật." });
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
            return StatusCode(500, new { message = "Lỗi khi cập nhật quyền viết.", error = ex.Message });
        }
    }

    [Authorize(Roles = "COMPLIANCE")]
    [HttpPost("comments/{commentId:guid}/admin-action-requests")]
    public async Task<IActionResult> RequestAdminAction(Guid commentId, [FromBody] CreateComplianceAdminActionRequestDto? body)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(new { message = "Cần đăng nhập." });
        if (body == null) return BadRequest(new { message = "Body is required." });
        var actorIsAdmin = User.IsInRole("ADMIN");

        try
        {
            var id = await _commentReportService.RequestAdminActionAsync(commentId, userId.Value, body, actorIsAdmin);
            return Ok(new { id, message = "Đã gửi yêu cầu lên admin." });
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
            return StatusCode(500, new { message = "Lỗi khi gửi yêu cầu admin.", error = ex.Message });
        }
    }

    /// <summary>COMPLIANCE: đóng toàn bộ ticket đang mở của comment thread.</summary>
    [HttpPost("comments/{commentId:guid}/resolve-all-open")]
    public async Task<IActionResult> ResolveAllOpenCommentReports(Guid commentId, [FromBody] ComplianceResolveCommentReportRequestDto? body)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(new { message = "Cần đăng nhập." });
        var actorIsAdmin = User.IsInRole("ADMIN");

        try
        {
            var count = await _commentReportService.ComplianceResolveAllOpenCommentReportsAsync(
                commentId,
                userId.Value,
                body,
                actorIsAdmin);
            return Ok(new { message = "Đã đóng toàn bộ ticket comment.", closedCount = count });
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
            return StatusCode(500, new { message = "Lỗi khi đóng ticket comment.", error = ex.Message });
        }
    }

    /// <summary>COMPLIANCE: lưu đánh dấu xác minh cho từng request báo cáo (report_evidences) trong thread.</summary>
    [HttpPost("comments/{commentId:guid}/evidence-verification")]
    public async Task<IActionResult> SetCommentReportEvidenceVerification(
        Guid commentId,
        [FromBody] SetComplianceCommentReportEvidenceVerifiedRequestDto? body)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(new { message = "Cần đăng nhập." });
        if (body == null) return BadRequest(new { message = "Body is required." });
        var actorIsAdmin = User.IsInRole("ADMIN");

        try
        {
            var n = await _commentReportService.SetComplianceCommentReportEvidenceVerifiedAsync(
                commentId,
                userId.Value,
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
            return StatusCode(500, new { message = "Lỗi khi lưu đánh dấu xác minh.", error = ex.Message });
        }
    }

    /// <summary>COMPLIANCE đang giữ lock: gửi yêu cầu admin gỡ lock (sau khi gửi, thread bị chặn thao tác tới khi admin xử lý).</summary>
    [Authorize(Roles = "COMPLIANCE")]
    [HttpPost("comments/{commentId:guid}/request-release")]
    public async Task<IActionResult> RequestCommentLockRelease(Guid commentId, [FromBody] RequestComplianceLockReleaseDto? body)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(new { message = "Cần đăng nhập." });
        try
        {
            var id = await _commentReportService.RequestComplianceCommentLockReleaseAsync(commentId, userId.Value, body);
            return Ok(new { message = "Đã gửi yêu cầu lên admin.", requestId = id });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi khi gửi yêu cầu gỡ lock.", error = ex.Message });
        }
    }

    [Authorize(Roles = "COMPLIANCE")]
    [HttpPost("comments/{commentId:guid}/claim")]
    public async Task<IActionResult> ClaimCommentReports(Guid commentId)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(new { message = "Cần đăng nhập." });

        try
        {
            var res = await _commentReportService.ClaimCommentReportsAsync(commentId, userId.Value);
            return Ok(new
            {
                message = "Đã nhận lock comment reports.",
                res.OpenReportCount,
                res.ClaimedAtUtc
            });
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
            return StatusCode(500, new { message = "Lỗi khi nhận lock comment.", error = ex.Message });
        }
    }
}

