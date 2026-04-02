using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.CommentReports;
using Services.DTOs.StoryReports;
using Services.Interfaces;

namespace AIStory.API.Controllers;

[ApiController]
[Route("api")]
public class CommentReportsController : ControllerBase
{
    private readonly ICommentReportService _commentReportService;

    public CommentReportsController(ICommentReportService commentReportService)
    {
        _commentReportService = commentReportService;
    }

    private Guid? GetCurrentUserId()
    {
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    [HttpGet("comment-reporting/reasons")]
    [AllowAnonymous]
    public async Task<IActionResult> GetCommentReportReasons()
    {
        try
        {
            IReadOnlyList<StoryReportReasonOptionDto> list = _commentReportService.GetReasonOptions();
            return Ok(list);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Lỗi tải lý do report.", error = ex.Message });
        }
    }

    [HttpPost("stories/{storyId:guid}/comments/{commentId:guid}/reports")]
    [Authorize]
    public async Task<IActionResult> ReportStoryComment(Guid storyId, Guid commentId, [FromBody] CreateCommentReportRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(new { message = "Cần đăng nhập để báo cáo." });
        if (request == null || string.IsNullOrWhiteSpace(request.ReasonCode))
            return BadRequest(new { message = "ReasonCode is required." });
        if (commentId == Guid.Empty)
            return BadRequest(new { message = "Không tìm thấy comment." });

        try
        {
            var reportId = await _commentReportService.CreateCommentReportAsync(
                commentId,
                userId.Value,
                request,
                expectedStoryId: storyId);

            return Ok(new { id = reportId, message = "Đã gửi báo cáo." });
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
            return StatusCode(500, new { message = "Không gửi được báo cáo.", error = ex.Message });
        }
    }

    [HttpPost("chapters/{chapterId:guid}/comments/{commentId:guid}/reports")]
    [Authorize]
    public async Task<IActionResult> ReportChapterComment(Guid chapterId, Guid commentId, [FromBody] CreateCommentReportRequestDto request)
    {
        var userId = GetCurrentUserId();
        if (!userId.HasValue) return Unauthorized(new { message = "Cần đăng nhập để báo cáo." });
        if (request == null || string.IsNullOrWhiteSpace(request.ReasonCode))
            return BadRequest(new { message = "ReasonCode is required." });
        if (commentId == Guid.Empty)
            return BadRequest(new { message = "Không tìm thấy comment." });

        try
        {
            var reportId = await _commentReportService.CreateCommentReportAsync(
                commentId,
                userId.Value,
                request,
                expectedChapterId: chapterId);

            return Ok(new { id = reportId, message = "Đã gửi báo cáo." });
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
            return StatusCode(500, new { message = "Không gửi được báo cáo.", error = ex.Message });
        }
    }
}

