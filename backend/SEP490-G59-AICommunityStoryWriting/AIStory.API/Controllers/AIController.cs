using System.IdentityModel.Tokens.Jwt;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using AIStory.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Text.Json.Serialization;
using Repositories;
using Services;
using Services.DTOs.Admin;
using Services.DTOs.AI;
using Services.Interfaces;

namespace AIStory.API.Controllers
{
    public sealed class AiUsageLimitItemResponse
    {
        [JsonPropertyName("limitPerDay")]
        public int LimitPerDay { get; set; }
        [JsonPropertyName("usedInWindow")]
        public int UsedInWindow { get; set; }
        [JsonPropertyName("remaining")]
        public int Remaining { get; set; }
        [JsonPropertyName("resetsAtUtc")]
        public DateTime? ResetsAtUtc { get; set; }
    }

    public sealed class AiUsageLimitResponse
    {
        [JsonPropertyName("suggestNextChapter")]
        public AiUsageLimitItemResponse SuggestNextChapter { get; set; } = new();
        [JsonPropertyName("coCreate")]
        public AiUsageLimitItemResponse CoCreate { get; set; } = new();

        // Legacy root fields (mirror suggestNextChapter)
        [JsonPropertyName("limitPerDay")]
        public int LimitPerDay { get; set; }
        [JsonPropertyName("usedInWindow")]
        public int UsedInWindow { get; set; }
        [JsonPropertyName("remaining")]
        public int Remaining { get; set; }
        [JsonPropertyName("resetsAtUtc")]
        public DateTime? ResetsAtUtc { get; set; }

        /// <summary>Chi tiết token AI đã dùng so với hạn (ngày/tuần/tháng/tích lũy). Null nếu không lấy được.</summary>
        [JsonPropertyName("authorTokenBudget")]
        public AuthorAiTokenBudgetDto? AuthorTokenBudget { get; set; }

        /// <summary>True khi số dư token AI đã cạn (khớp logic <see cref="IAuthorAiTokenBudgetService.EnsureWithinBudgetAsync"/>).</summary>
        [JsonPropertyName("authorTokenBudgetBlocked")]
        public bool AuthorTokenBudgetBlocked { get; set; }
    }

    /// <summary>API AI: gợi ý chương tiếp theo, đồng sáng tác (dàn ý + viết + guardrail).</summary>
    [ApiController]
    [Route("api/ai")]
    [Authorize(Policy = "AuthorOnly")]
    public class AIController : ControllerBase
    {
        private readonly IAINextChapterService _aiNextChapterService;
        private readonly IAICoCreationService _aiCoCreationService;
        private readonly IChapterCheckService _chapterCheckService;
        private readonly IChapterCompareService _chapterCompareService;
        private readonly IChapterVersionAiCompareService _chapterVersionAiCompareService;
        private readonly IStoryRagService _ragService;
        private readonly IStoryRepository _storyRepository;
        private readonly IAISuggestRateLimitService _rateLimitService;
        private readonly IAuthorAiTokenBudgetService _authorAiTokenBudget;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<AIController> _logger;

        public AIController(
            IAINextChapterService aiNextChapterService,
            IAICoCreationService aiCoCreationService,
            IChapterCheckService chapterCheckService,
            IChapterCompareService chapterCompareService,
            IChapterVersionAiCompareService chapterVersionAiCompareService,
            IStoryRagService ragService,
            IStoryRepository storyRepository,
            IAISuggestRateLimitService rateLimitService,
            IAuthorAiTokenBudgetService authorAiTokenBudget,
            IConfiguration configuration,
            IWebHostEnvironment env,
            ILogger<AIController> logger)
        {
            _aiNextChapterService = aiNextChapterService;
            _aiCoCreationService = aiCoCreationService;
            _chapterCheckService = chapterCheckService;
            _chapterCompareService = chapterCompareService;
            _chapterVersionAiCompareService = chapterVersionAiCompareService;
            _ragService = ragService;
            _storyRepository = storyRepository;
            _rateLimitService = rateLimitService;
            _authorAiTokenBudget = authorAiTokenBudget;
            _configuration = configuration;
            _env = env;
            _logger = logger;
        }

        private static object BuildTokenBudgetExceededPayload(AuthorAiTokenBudgetExceededException ex) => new
        {
            message = string.IsNullOrWhiteSpace(ex.Message) ? "Tài khoản bạn đã sử dụng hết token AI. Vui lòng đợi đến kỳ cấp token tiếp theo." : ex.Message,
            tokensUsed = ex.UsedTokens,
            tokenLimit = ex.LimitTokens,
            period = ex.Period.ToString()
        };

        private static bool IsAuthorTokenBudgetBlocked(AuthorAiTokenBudgetDto b)
            => (b.TokensRemaining ?? 0) <= 0;

        /// <summary>Xem giới hạn sử dụng AI (mặc định 3 lần/24h mỗi loại). Hai bộ đếm tách: suggest-next-chapter và co-create.</summary>
        [HttpGet("usage-limit")]
        public async Task<IActionResult> GetUsageLimit(CancellationToken cancellationToken)
        {
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Unauthorized(new { message = "Vui lòng đăng nhập." });
            var suggest = _rateLimitService.GetDailyLimitInfo(userId, AiRateLimitKind.SuggestNextChapter);
            var coCreate = _rateLimitService.GetDailyLimitInfo(userId, AiRateLimitKind.CoCreate);

            AuthorAiTokenBudgetDto? authorBudget = null;
            try
            {
                authorBudget = await _authorAiTokenBudget.GetBudgetAsync(userId, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "GetBudgetAsync failed for UserId={UserId}", userId);
            }

            var payload = new AiUsageLimitResponse
            {
                SuggestNextChapter = new AiUsageLimitItemResponse
                {
                    LimitPerDay = suggest.LimitPerDay,
                    UsedInWindow = suggest.UsedInWindow,
                    Remaining = suggest.Remaining,
                    ResetsAtUtc = suggest.ResetsAtUtc
                },
                CoCreate = new AiUsageLimitItemResponse
                {
                    LimitPerDay = coCreate.LimitPerDay,
                    UsedInWindow = coCreate.UsedInWindow,
                    Remaining = coCreate.Remaining,
                    ResetsAtUtc = coCreate.ResetsAtUtc
                },
                LimitPerDay = suggest.LimitPerDay,
                UsedInWindow = suggest.UsedInWindow,
                Remaining = suggest.Remaining,
                ResetsAtUtc = suggest.ResetsAtUtc,
                AuthorTokenBudget = authorBudget,
                AuthorTokenBudgetBlocked = authorBudget != null && IsAuthorTokenBudgetBlocked(authorBudget)
            };

            return Ok(payload);
        }

        /// <summary>Gợi ý 3 hướng đi khác nhau cho chương tiếp theo. Chỉ tác giả của truyện được gọi. Kiểm soát bằng token budget của tác giả.</summary>
        [HttpPost("suggest-next-chapter")]
        public async Task<IActionResult> SuggestNextChapter([FromBody] SuggestNextChapterRequest request, CancellationToken cancellationToken)
        {
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var authorUserId))
                return Unauthorized(new { message = "Không xác định được người dùng. Vui lòng đăng nhập lại." });

            try
            {
                var response = await _aiNextChapterService.SuggestNextChapterAsync(request, authorUserId, cancellationToken);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (AuthorAiTokenBudgetExceededException ex)
            {
                return StatusCode(403, BuildTokenBudgetExceededPayload(ex));
            }
            catch (AuthorAiEstimatedTokensInsufficientException ex)
            {
                return StatusCode(403, new
                {
                    message = ex.Message,
                    tokensRemaining = ex.TokensRemaining,
                    minRequiredTokens = ex.MinRequiredTokens
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return StatusCode(403, new { message = ex.Message });
            }
            catch (InvalidOperationException ex)
            {
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

        /// <summary>Đồng sáng tác: ý tưởng tác giả → Agent 1 (dàn ý) → Agent 2 (nội dung) → guardrail từ cấm. Trả về JSON kết quả hoặc lỗi chuẩn HTTP.</summary>
        [HttpPost("co-create")]
        public async Task<IActionResult> CoCreate([FromBody] CoCreationRequest request, CancellationToken cancellationToken)
        {
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out var authorUserId))
            {
                return Unauthorized(new { message = "Không xác định được người dùng. Vui lòng đăng nhập lại." });
            }

            try
            {
                var response = await _aiCoCreationService.CoCreateAsync(request, authorUserId, cancellationToken);
                return Ok(response);
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (AuthorAiTokenBudgetExceededException ex)
            {
                return StatusCode(403, BuildTokenBudgetExceededPayload(ex));
            }
            catch (AuthorAiEstimatedTokensInsufficientException ex)
            {
                return StatusCode(403, new
                {
                    message = ex.Message,
                    tokensRemaining = ex.TokensRemaining,
                    minRequiredTokens = ex.MinRequiredTokens
                });
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Co-create cancelled for StoryId={StoryId}", request.StoryId);
                return StatusCode(499, new { message = "Yêu cầu đã bị hủy." });
            }
            catch (UnauthorizedAccessException ex)
            {
                const string missingUserMsg = "Không xác định được người dùng. Vui lòng đăng nhập lại.";
                if (string.Equals(ex.Message, missingUserMsg, StringComparison.Ordinal))
                    return Unauthorized(new { message = ex.Message });
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
                var message = _env.IsDevelopment()
                    ? (ex.InnerException?.Message ?? ex.Message)
                    : "Lỗi khi gọi dịch vụ AI. Vui lòng thử lại sau.";
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
                await _ragService.EnsureIndexedAsync(request.StoryId, request.UpToChapterId, cancellationToken);
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

        /// <summary>Chỉ kiểm tra từ cấm / guardrail (không gọi AI chính tả).</summary>
        [HttpPost("check-chapter-banned-words")]
        public async Task<IActionResult> CheckChapterBannedWords([FromBody] CheckChapterBannedWordsRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { message = "Content (nội dung chương) là bắt buộc." });

            Guid? userId = null;
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var uid))
                userId = uid;

            try
            {
                var response = await _chapterCheckService.CheckBannedWordsOnlyAsync(request, userId, cancellationToken);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI check-chapter-banned-words failed");
                var message = _env.IsDevelopment() ? (ex.InnerException?.Message ?? ex.Message) : "Lỗi khi kiểm tra từ cấm. Vui lòng thử lại sau.";
                return StatusCode(500, new { message });
            }
        }

        /// <summary>Chỉ kiểm tra chính tả (AI); không chạy guardrail từ cấm.</summary>
        [HttpPost("check-chapter-spelling")]
        public async Task<IActionResult> CheckChapterSpelling([FromBody] CheckChapterSpellingRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { message = "Content (nội dung chương) là bắt buộc." });

            Guid? userId = null;
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var uid))
                userId = uid;

            try
            {
                var response = await _chapterCheckService.CheckSpellingOnlyAsync(request, userId, cancellationToken);
                return Ok(response);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AI check-chapter-spelling failed");
                var message = _env.IsDevelopment() ? (ex.InnerException?.Message ?? ex.Message) : "Lỗi khi kiểm tra chính tả. Vui lòng thử lại sau.";
                return StatusCode(500, new { message });
            }
        }

        /// <summary>So sánh nội dung truyền vào theo <c>ChapterId</c> với các bản AI của chính chapter đó; độ giống 0–100%. Chỉ tác giả truyện. Không ghi DB.</summary>
        [HttpPost("compare-chapter")]
        public async Task<IActionResult> CompareChapter([FromBody] CompareChapterRequest request, CancellationToken cancellationToken)
        {
            if (request.ChapterId == Guid.Empty)
                return BadRequest(new { message = "ChapterId là bắt buộc." });
            if (string.IsNullOrWhiteSpace(request.Content))
                return BadRequest(new { message = "Content là bắt buộc." });

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
