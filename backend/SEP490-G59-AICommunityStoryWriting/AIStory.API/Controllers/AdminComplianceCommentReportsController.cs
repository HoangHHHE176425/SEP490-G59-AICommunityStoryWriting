using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace AIStory.API.Controllers;

[ApiController]
[Route("api/admin/compliance-comment-reports")]
[Authorize(Roles = "ADMIN")]
public class AdminComplianceCommentReportsController : ControllerBase
{
    private readonly ICommentReportService _commentReportService;

    public AdminComplianceCommentReportsController(ICommentReportService commentReportService)
    {
        _commentReportService = commentReportService;
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    /// <summary>Gỡ lock claim comment reports trực tiếp (không qua yêu cầu).</summary>
    [HttpPost("comments/{commentId:guid}/release-claim")]
    public async Task<IActionResult> AdminReleaseClaim(Guid commentId)
    {
        var uid = GetCurrentUserId();
        if (!uid.HasValue) return Unauthorized();
        try
        {
            var reopenedCount = await _commentReportService.ReleaseComplianceCommentClaimAsync(commentId, uid.Value);
            return Ok(new { message = "Đã gỡ lock comment reports.", reopenedCount });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

