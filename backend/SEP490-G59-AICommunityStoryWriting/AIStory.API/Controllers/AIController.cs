using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using AIStory.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Repositories;
using Services.DTOs.AI;
using Services.Interfaces;

namespace AIStory.API.Controllers
{
    /// <summary>API AI: gợi ý chương tiếp theo, đồng sáng tác (3 agent), kiểm tra nhất quán.</summary>
    [ApiController]
    [Route("api/ai")]
    [Authorize(Roles = "AUTHOR,ADMIN")]
    public class AIController : ControllerBase
    {
        private readonly IAINextChapterService _aiNextChapterService;
        private readonly IAICoCreationService _aiCoCreationService;
        private readonly IAIConsistencyCheckService _aiConsistencyCheckService;
        private readonly IStoryRagService _ragService;
        private readonly IStoryRepository _storyRepository;
        private readonly IAISuggestRateLimitService _rateLimitService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AIController> _logger;

        public AIController(
            IAINextChapterService aiNextChapterService,
            IAICoCreationService aiCoCreationService,
            IAIConsistencyCheckService aiConsistencyCheckService,
            IStoryRagService ragService,
            IStoryRepository storyRepository,
            IAISuggestRateLimitService rateLimitService,
            IWebHostEnvironment env,
            ILogger<AIController> logger)
        {
            _aiNextChapterService = aiNextChapterService;
            _aiCoCreationService = aiCoCreationService;
            _aiConsistencyCheckService = aiConsistencyCheckService;
            _ragService = ragService;
            _storyRepository = storyRepository;
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

        /// <summary>Kiểm tra nhất quán: so sánh bản nháp chương với cốt truyện (các chương trước). Phát hiện mâu thuẫn như nhân vật đã chết lại xuất hiện, sự kiện sai logic.</summary>
        [HttpPost("check-consistency")]
        public async Task<IActionResult> CheckConsistency([FromBody] ConsistencyCheckRequest request, CancellationToken cancellationToken)
        {
            if (request.StoryId == Guid.Empty)
                return BadRequest(new { message = "StoryId là bắt buộc." });
            if (string.IsNullOrWhiteSpace(request.DraftContent))
                return BadRequest(new { message = "DraftContent (nội dung bản nháp chương) là bắt buộc." });

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
                var response = await _aiConsistencyCheckService.CheckConsistencyAsync(request, authorUserId, cancellationToken);
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
                _logger.LogError(ex, "AI check-consistency failed for StoryId={StoryId}", request.StoryId);
                var message = "Lỗi khi gọi dịch vụ AI. Vui lòng thử lại sau.";
                if (_env.IsDevelopment())
                {
                    var detail = ex.InnerException?.Message ?? ex.Message;
                    return StatusCode(500, new { message, detail });
                }
                return StatusCode(500, new { message });
            }
        }

        /// <summary>Kiểm tra trạng thái RAG của truyện: available, chunkCount, hasVectorIndex, embeddingConfigured.</summary>
        [HttpGet("rag-status")]
        public IActionResult GetRagStatus([FromQuery] Guid storyId)
        {
            if (storyId == Guid.Empty)
                return BadRequest(new { message = "storyId là bắt buộc." });
            var story = _storyRepository.GetById(storyId);
            if (story == null)
                return NotFound(new { message = "Truyện không tồn tại." });
            var status = _ragService.GetRagStatus(storyId);
            return Ok(status);
        }

        /// <summary>Index truyện cho RAG (chunk + embedding). Gọi sau khi thêm/sửa chương để đồng sáng tác tìm đúng đoạn liên quan. Cần cấu hình AI:EmbeddingBaseUrl và AI:EmbeddingApiKey.</summary>
        [HttpPost("index-rag")]
        public async Task<IActionResult> IndexRag([FromBody] IndexRagRequest request, CancellationToken cancellationToken)
        {
            if (request.StoryId == Guid.Empty)
                return BadRequest(new { message = "StoryId là bắt buộc." });

            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var authorUserId))
                return Unauthorized(new { message = "Không xác định được người dùng. Vui lòng đăng nhập lại." });

            var story = _storyRepository.GetById(request.StoryId);
            if (story == null)
                return NotFound(new { message = "Truyện không tồn tại." });
            if (story.author_id != authorUserId)
                return StatusCode(403, new { message = "Chỉ tác giả của truyện mới được index RAG." });

            try
            {
                await _ragService.EnsureIndexedAsync(request.StoryId, request.AfterChapterId, cancellationToken);
                return Ok(new { message = "Đã index xong cho RAG.", storyId = request.StoryId });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
                if (ex.Message.Contains("429"))
                    return StatusCode(429, new { message = ex.Message });
                if (ex.Message.Contains("không tồn tại"))
                    return NotFound(new { message = ex.Message });
                return BadRequest(new { message = ex.Message });
            }
            catch (HttpRequestException ex) when (ex.Message.Contains("429"))
            {
                return StatusCode(429, new { message = "API embedding trả về 429 (quá nhiều request). Vui lòng đợi 1–2 phút rồi gọi lại POST /api/ai/index-rag." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Index RAG failed for StoryId={StoryId}", request.StoryId);
                return StatusCode(500, new { message = "Lỗi khi index RAG. Vui lòng thử lại sau." });
            }
        }
    }
}
