using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using AIStory.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.AI;
using Services.Interfaces;

namespace AIStory.API.Controllers
{
    /// <summary>API AI: gợi ý chương tiếp theo + đồng sáng tác (3 agent).</summary>
    [ApiController]
    [Route("api/ai")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    public class AIController : ControllerBase
    {
        private readonly IAINextChapterService _aiNextChapterService;
        private readonly IAICoCreationService _aiCoCreationService;
        private readonly IAISuggestRateLimitService _rateLimitService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AIController> _logger;

        public AIController(
            IAINextChapterService aiNextChapterService,
            IAICoCreationService aiCoCreationService,
            IAISuggestRateLimitService rateLimitService,
            IWebHostEnvironment env,
            ILogger<AIController> logger)
        {
            _aiNextChapterService = aiNextChapterService;
            _aiCoCreationService = aiCoCreationService;
            _rateLimitService = rateLimitService;
            _env = env;
            _logger = logger;
        }

        /// <summary>Gợi ý 3 hướng đi khác nhau cho chương tiếp theo. Chỉ tác giả của truyện được gọi. Có giới hạn số lần gọi theo user (tránh 429).</summary>
        [HttpPost("suggest-next-chapter")]
        public async Task<IActionResult> SuggestNextChapter([FromBody] SuggestNextChapterRequest request, CancellationToken cancellationToken)
        {
            if (request.StoryId == Guid.Empty)
                return BadRequest(new { message = "StoryId là bắt buộc." });

            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var authorUserId))
                return Unauthorized(new { message = "Không xác định được người dùng. Vui lòng đăng nhập lại." });

            if (!_rateLimitService.TryAcquire(authorUserId, out var retryAfterSeconds))
            {
                Response.Headers.RetryAfter = retryAfterSeconds.ToString();
                return StatusCode(429, new
                {
                    message = "Bạn đã gọi gợi ý chương quá nhiều lần. Vui lòng thử lại sau.",
                    retryAfterSeconds
                });
            }

            try
            {
                var response = await _aiNextChapterService.SuggestNextChapterAsync(request, authorUserId, cancellationToken);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("không tồn tại"))
                    return NotFound(new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI suggest-next-chapter failed for StoryId={StoryId}", request.StoryId);
                var message = "Lỗi khi gọi dịch vụ AI. Vui lòng thử lại sau.";
                if (_env.IsDevelopment())
                {
                    var detail = ex.InnerException?.Message ?? ex.Message;
                    return StatusCode(500, new { message, detail });
                }
                return StatusCode(500, new { message });
            }
        }

        /// <summary>Đồng sáng tác: ý tưởng tác giả → Agent 1 (dàn ý) → Agent 2 (nội dung) → Agent 3 (kiểm duyệt). Có vòng sửa tối đa 2 lần.</summary>
        [HttpPost("co-create")]
        public async Task<IActionResult> CoCreate([FromBody] CoCreationRequest request, CancellationToken cancellationToken)
        {
            if (request.StoryId == Guid.Empty)
                return BadRequest(new { message = "StoryId là bắt buộc." });
            if (string.IsNullOrWhiteSpace(request.AuthorIdea))
                return BadRequest(new { message = "AuthorIdea (ý tưởng) là bắt buộc." });

            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var authorUserId))
                return Unauthorized(new { message = "Không xác định được người dùng. Vui lòng đăng nhập lại." });

            if (!_rateLimitService.TryAcquire(authorUserId, out var retryAfterSeconds))
            {
                Response.Headers.RetryAfter = retryAfterSeconds.ToString();
                return StatusCode(429, new
                {
                    message = "Bạn đã gọi AI quá nhiều lần. Vui lòng thử lại sau.",
                    retryAfterSeconds
                });
            }

            try
            {
                var response = await _aiCoCreationService.CoCreateAsync(request, authorUserId, cancellationToken);
                return Ok(response);
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("không tồn tại"))
                    return NotFound(new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI co-create failed for StoryId={StoryId}", request.StoryId);
                var message = "Lỗi khi gọi dịch vụ AI. Vui lòng thử lại sau.";
                if (_env.IsDevelopment())
                {
                    var detail = ex.InnerException?.Message ?? ex.Message;
                    return StatusCode(500, new { message, detail });
                }
                return StatusCode(500, new { message });
            }
        }
    }
}
