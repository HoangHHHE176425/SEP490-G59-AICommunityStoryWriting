using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading.Channels;
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
        private readonly IChapterCheckService _chapterCheckService;
        private readonly IChapterCompareService _chapterCompareService;
        private readonly IStoryRagService _ragService;
        private readonly IStoryRepository _storyRepository;
        private readonly IAISuggestRateLimitService _rateLimitService;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AIController> _logger;

        public AIController(
            IAINextChapterService aiNextChapterService,
            IAICoCreationService aiCoCreationService,
            IAIConsistencyCheckService aiConsistencyCheckService,
            IChapterCheckService chapterCheckService,
            IChapterCompareService chapterCompareService,
            IStoryRagService ragService,
            IStoryRepository storyRepository,
            IAISuggestRateLimitService rateLimitService,
            IWebHostEnvironment env,
            ILogger<AIController> logger)
        {
            _aiNextChapterService = aiNextChapterService;
            _aiCoCreationService = aiCoCreationService;
            _aiConsistencyCheckService = aiConsistencyCheckService;
            _chapterCheckService = chapterCheckService;
            _chapterCompareService = chapterCompareService;
            _ragService = ragService;
            _storyRepository = storyRepository;
            _rateLimitService = rateLimitService;
            _env = env;
            _logger = logger;
        }

        /// <summary>Xem giới hạn sử dụng AI (mặc định 3 lần/24h). Trả về: limitPerDay, usedInWindow, remaining, resetsAtUtc.</summary>
        [HttpGet("usage-limit")]
        public IActionResult GetUsageLimit()
        {
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(new { message = "Vui lòng đăng nhập." });
            var info = _rateLimitService.GetDailyLimitInfo(userId);
            return Ok(new
            {
                limitPerDay = info.LimitPerDay,
                usedInWindow = info.UsedInWindow,
                remaining = info.Remaining,
                resetsAtUtc = info.ResetsAtUtc
            });
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
                    message = "Bạn đã đạt giới hạn sử dụng AI trong ngày (3 lần/24h). Vui lòng thử lại sau.",
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

        /// <summary>Đồng sáng tác (SSE): ý tưởng tác giả → Agent 1 (dàn ý) → Agent 2 (nội dung) → Agent 3 (kiểm duyệt). Trả về stream event: progress từng bước, cuối cùng event result chứa CoCreationResponse. Client đọc response body theo chuẩn SSE (event: progress / result / error).</summary>
        [Produces("text/event-stream")]
        [HttpPost("co-create")]
        public async Task CoCreate([FromBody] CoCreationRequest request, CancellationToken cancellationToken)
        {
            if (request.StoryId == Guid.Empty)
            {
                Response.StatusCode = 400;
                await Response.WriteAsJsonAsync(new { message = "StoryId là bắt buộc." }, cancellationToken);
                return;
            }
            // AuthorIdea là tùy chọn (option 1: để trống → AI tự viết theo mạch truyện).

            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var authorUserId))
            {
                Response.StatusCode = 401;
                await Response.WriteAsJsonAsync(new { message = "Không xác định được người dùng. Vui lòng đăng nhập lại." }, cancellationToken);
                return;
            }

            if (!_rateLimitService.TryAcquire(authorUserId, out var retryAfterSeconds))
            {
                Response.StatusCode = 429;
                Response.Headers.RetryAfter = retryAfterSeconds.ToString();
                await Response.WriteAsJsonAsync(new
                {
                    message = "Bạn đã đạt giới hạn sử dụng AI trong ngày (3 lần/24h). Vui lòng thử lại sau.",
                    retryAfterSeconds
                }, cancellationToken);
                return;
            }

            Response.ContentType = "text/event-stream";
            Response.Headers.CacheControl = "no-cache";
            Response.Headers.Connection = "keep-alive";
            await Response.StartAsync(cancellationToken);

            var jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            };
            var channel = Channel.CreateUnbounded<(string EventType, object Data)>(new UnboundedChannelOptions { SingleReader = true, SingleWriter = false });
            var progress = new Progress<CoCreateProgressEvent>(evt => channel.Writer.TryWrite(("progress", evt)));

            async Task WriteSseEventAsync(string eventType, object data, CancellationToken ct)
            {
                var json = JsonSerializer.Serialize(data, jsonOptions);
                var payload = $"event: {eventType}\ndata: {json}\n\n";
                var bytes = Encoding.UTF8.GetBytes(payload);
                await Response.Body.WriteAsync(bytes, ct);
                await Response.Body.FlushAsync(ct);
            }

            async Task ConsumeProgressChannelAsync(CancellationToken ct)
            {
                try
                {
                    await foreach (var item in channel.Reader.ReadAllAsync(ct))
                        await WriteSseEventAsync(item.EventType, item.Data, ct);
                }
                catch (OperationCanceledException) { }
            }

            var consumerTask = ConsumeProgressChannelAsync(cancellationToken);
            try
            {
                CoCreationResponse response;
                try
                {
                    response = await _aiCoCreationService.CoCreateAsync(request, authorUserId, cancellationToken, progress);
                }
                finally
                {
                    channel.Writer.Complete();
                }
                await consumerTask;
                await WriteSseEventAsync("result", response, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Co-create cancelled for StoryId={StoryId}", request.StoryId);
            }
            catch (UnauthorizedAccessException ex)
            {
                try { await consumerTask; } catch (OperationCanceledException) { }
                try { await WriteSseEventAsync("error", new { message = ex.Message }, cancellationToken); } catch { }
            }
            catch (InvalidOperationException ex)
            {
                try { await consumerTask; } catch (OperationCanceledException) { }
                try { await WriteSseEventAsync("error", new { message = ex.Message }, cancellationToken); } catch { }
            }
            catch (Exception ex)
            {
                try { await consumerTask; } catch (OperationCanceledException) { }
                _logger.LogError(ex, "AI co-create failed for StoryId={StoryId}", request.StoryId);
                var message = _env.IsDevelopment() ? (ex.InnerException?.Message ?? ex.Message) : "Lỗi khi gọi dịch vụ AI. Vui lòng thử lại sau.";
                try { await WriteSseEventAsync("error", new { message }, cancellationToken); } catch { }
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
                    message = "Bạn đã đạt giới hạn sử dụng AI trong ngày (3 lần/24h). Vui lòng thử lại sau.",
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

        /// <summary>Kiểm tra chương: lỗi chính tả, vi phạm chính sách, nội dung không phù hợp. Trả về danh sách lỗi và gợi ý sửa.</summary>
        [HttpPost("check-chapter")]
        public async Task<IActionResult> CheckChapter([FromBody] CheckChapterRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { message = "Content (nội dung chương) là bắt buộc." });

            Guid? userId = null;
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var uid))
                userId = uid;

            try
            {
                var response = await _chapterCheckService.CheckAsync(request, userId, cancellationToken);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI check-chapter failed");
                var message = _env.IsDevelopment() ? (ex.InnerException?.Message ?? ex.Message) : "Lỗi khi kiểm tra chương. Vui lòng thử lại sau.";
                return StatusCode(500, new { message });
            }
        }

        /// <summary>So sánh <c>chapters.content</c> theo <c>ChapterId</c> (BE tự lấy story + order_index) với các bản AI cùng <c>chapter_index</c>; độ giống 0–100% (lấy max). Chỉ tác giả truyện.</summary>
        [HttpPost("compare-chapter")]
        public async Task<IActionResult> CompareChapter([FromBody] CompareChapterRequest request, CancellationToken cancellationToken)
        {
            if (request.ChapterId == Guid.Empty)
                return BadRequest(new { message = "ChapterId là bắt buộc." });

            Guid? userId = null;
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var uid))
                userId = uid;
            if (!userId.HasValue)
                return Unauthorized(new { message = "Vui lòng đăng nhập để so sánh chương." });

            try
            {
                var response = await _chapterCompareService.CompareAsync(request, userId, cancellationToken);
                return Ok(response);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Compare chapter failed for ChapterId={ChapterId}", request.ChapterId);
                var message = _env.IsDevelopment() ? (ex.InnerException?.Message ?? ex.Message) : "Lỗi khi so sánh chương. Vui lòng thử lại sau.";
                return StatusCode(500, new { message });
            }
        }
    }
}
