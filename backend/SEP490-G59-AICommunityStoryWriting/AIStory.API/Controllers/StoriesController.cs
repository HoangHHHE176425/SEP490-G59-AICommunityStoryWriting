using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Services.DTOs.Ai;
using Services.DTOs.Stories;
using Services.Exceptions;
using Services.Interfaces;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/stories")]
    [Authorize] // Bắt buộc đăng nhập để xem
    public class StoriesController : ControllerBase
    {
        private readonly IStoryService _storyService;
        private readonly IStoryAiService _storyAiService;
        private readonly ILogger<StoriesController> _logger;

        public StoriesController(IStoryService storyService, IStoryAiService storyAiService, ILogger<StoriesController> logger)
        {
            _storyService = storyService;
            _storyAiService = storyAiService;
            _logger = logger;
        }

        /// <summary>Tạo story mới - Chỉ AUTHOR</summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "AUTHOR")]
        public async Task<IActionResult> Create([FromForm] CreateStoryRequestDto request)
        {
            try
            {
                string? coverUrl = null;

                if (request.CoverImage != null && request.CoverImage.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var fileExtension = Path.GetExtension(request.CoverImage.FileName).ToLower();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        return BadRequest(new { message = "Invalid file type. Allowed types: jpg, jpeg, png, gif, webp" });
                    }

                    if (request.CoverImage.Length > 5 * 1024 * 1024)
                    {
                        return BadRequest(new { message = "File size exceeds 5MB limit" });
                    }

                    var uploadsFolder = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        "uploads",
                        "covers"
                    );

                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var fileName = $"{Guid.NewGuid()}{fileExtension}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using var stream = new FileStream(filePath, FileMode.Create);
                    await request.CoverImage.CopyToAsync(stream);

                    coverUrl = $"/uploads/covers/{fileName}";
                }

                Guid authorId;
                // Tìm user ID từ JWT token (tương tự AccountController)
                var authorIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
                if (authorIdClaim != null && Guid.TryParse(authorIdClaim.Value, out authorId))
                {
                    // Đã đăng nhập (JWT/cookie) → dùng ID từ claim
                }
                else if (request.AuthorId.HasValue)
                {
                    authorId = request.AuthorId.Value;
                }
                else
                {
                    return Unauthorized(new { message = "Author ID (Guid) not found or invalid. Hãy đăng nhập hoặc gửi AuthorId trong request (dev)." });
                }

                var story = _storyService.Create(request, authorId, coverUrl);
                return Created($"api/stories/{story.Id}", story);
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Create story validation failed");
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Create story argument error");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                var inner = GetInnermostException(ex);
                _logger.LogError(ex, "Create story failed: {Message}. Inner: {InnerMessage}", ex.Message, inner?.Message);
                var detail = inner?.Message ?? ex.Message;
                return StatusCode(500, new { message = "Lỗi tạo truyện: " + detail, error = detail });
            }
        }

        private static Exception? GetInnermostException(Exception ex)
        {
            while (ex.InnerException != null)
                ex = ex.InnerException;
            return ex;
        }

        /// <summary>Lấy danh sách stories với pagination và filtering (cho phép xem không cần đăng nhập)</summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult GetAll([FromQuery] StoryQueryDto query)
        {
            try
            {
                var result = _storyService.GetAll(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching stories", error = ex.Message });
            }
        }

        /// <summary>Lấy story theo ID (Guid) (cho phép xem không cần đăng nhập)</summary>
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public IActionResult GetById(Guid id)
        {
            try
            {
                var story = _storyService.GetById(id);
                return story == null ? NotFound(new { message = $"Story with ID {id} not found" }) : Ok(story);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching the story", error = ex.Message });
            }
        }

        /// <summary>Lấy story theo slug (cho phép xem không cần đăng nhập)</summary>
        [HttpGet("slug/{slug}")]
        [AllowAnonymous]
        public IActionResult GetBySlug(string slug)
        {
            try
            {
                var story = _storyService.GetBySlug(slug);
                return story == null ? NotFound(new { message = $"Story with slug '{slug}' not found" }) : Ok(story);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching the story", error = ex.Message });
            }
        }

        /// <summary>Lấy stories theo author (Guid) với pagination (cho phép xem không cần đăng nhập)</summary>
        [HttpGet("author/{authorId:guid}")]
        [AllowAnonymous]
        public IActionResult GetByAuthor(Guid authorId, [FromQuery] StoryQueryDto query)
        {
            try
            {
                var result = _storyService.GetByAuthor(authorId, query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching stories", error = ex.Message });
            }
        }

        /// <summary>Cập nhật story (với hỗ trợ upload ảnh) - Chỉ AUTHOR (chỉ được sửa story của chính mình)</summary>
        [HttpPut("{id:guid}")]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "AUTHOR")]
        public async Task<IActionResult> Update(Guid id, [FromForm] UpdateStoryWithImageRequestDto request)
        {
            try
            {
                string? coverUrl = null;

                if (request.CoverImage != null && request.CoverImage.Length > 0)
                {
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var fileExtension = Path.GetExtension(request.CoverImage.FileName).ToLower();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        return BadRequest(new { message = "Invalid file type. Allowed types: jpg, jpeg, png, gif, webp" });
                    }

                    if (request.CoverImage.Length > 5 * 1024 * 1024)
                    {
                        return BadRequest(new { message = "File size exceeds 5MB limit" });
                    }

                    var uploadsFolder = Path.Combine(
                        Directory.GetCurrentDirectory(),
                        "wwwroot",
                        "uploads",
                        "covers"
                    );

                    if (!Directory.Exists(uploadsFolder))
                        Directory.CreateDirectory(uploadsFolder);

                    var existingStory = _storyService.GetById(id);
                    if (existingStory != null && !string.IsNullOrEmpty(existingStory.CoverImage))
                    {
                        var oldFilePath = Path.Combine(
                            Directory.GetCurrentDirectory(),
                            "wwwroot",
                            existingStory.CoverImage.TrimStart('/')
                        );
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            try
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                            catch { }
                        }
                    }

                    var fileName = $"{Guid.NewGuid()}{fileExtension}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using var stream = new FileStream(filePath, FileMode.Create);
                    await request.CoverImage.CopyToAsync(stream);

                    coverUrl = $"/uploads/covers/{fileName}";
                }

                var updateRequest = new UpdateStoryRequestDto
                {
                    Title = request.Title,
                    Summary = request.Summary,
                    CategoryIds = request.CategoryIds ?? new List<Guid>(),
                    Status = request.Status,
                    AgeRating = request.AgeRating,
                    StoryProgressStatus = request.StoryProgressStatus,
                    CoverImageUrl = coverUrl
                };

                var updated = _storyService.Update(id, updateRequest);
                return updated ? NoContent() : NotFound(new { message = $"Story with ID {id} not found" });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "Update story validation failed");
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                _logger.LogWarning(ex, "Update story argument error");
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                var inner = GetInnermostException(ex);
                _logger.LogError(ex, "Update story failed: {Message}. Inner: {InnerMessage}", ex.Message, inner?.Message);
                var detail = inner?.Message ?? ex.Message;
                return StatusCode(500, new { message = "Lỗi cập nhật truyện: " + detail, error = detail });
            }
        }

        /// <summary>Xóa story - Chỉ AUTHOR (chỉ được xóa story của chính mình)</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult Delete(Guid id)
        {
            try
            {
                var deleted = _storyService.Delete(id);
                return deleted ? NoContent() : NotFound(new { message = $"Story with ID {id} not found" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while deleting the story", error = ex.Message });
            }
        }

        /// <summary>Publish story - Chỉ AUTHOR</summary>
        [HttpPost("{id:guid}/publish")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult Publish(Guid id)
        {
            try
            {
                _logger.LogInformation("Attempting to publish story with ID: {StoryId}", id);
                var published = _storyService.Publish(id);

                if (!published)
                {
                    _logger.LogWarning("Story with ID {StoryId} not found for publishing", id);
                    return NotFound(new { message = $"Story with ID {id} not found" });
                }

                _logger.LogInformation("Successfully published story with ID: {StoryId}", id);
                return NoContent();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error publishing story with ID: {StoryId}. Error: {ErrorMessage}", id, ex.Message);
                var innerException = ex.InnerException;
                var errorDetails = ex.Message;

                if (innerException != null)
                {
                    errorDetails += $" Inner Exception: {innerException.Message}";
                    _logger.LogError("Inner exception: {InnerException}", innerException.Message);
                }

                return StatusCode(500, new
                {
                    message = "An error occurred while publishing the story",
                    error = errorDetails,
                    storyId = id.ToString()
                });
            }
        }

        /// <summary>Unpublish story - Chỉ AUTHOR</summary>
        [HttpPost("{id:guid}/unpublish")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult Unpublish(Guid id)
        {
            try
            {
                var unpublished = _storyService.Unpublish(id);
                return unpublished ? NoContent() : NotFound(new { message = $"Story with ID {id} not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while unpublishing the story", error = ex.Message });
            }
        }

        /// <summary>AI gợi ý chương tiếp theo – đọc các chương đã viết và trả về 3–5 hướng phát triển (chỉ AUTHOR, demo Swagger).</summary>
        [HttpPost("{id:guid}/suggest-next-chapter")]
        [Authorize(Roles = "AUTHOR")]
        [ProducesResponseType(typeof(SuggestNextChapterResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status502BadGateway)]
        [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
        [ProducesResponseType(429, Type = typeof(object))]
        public async Task<IActionResult> SuggestNextChapter(Guid id, CancellationToken cancellationToken)
        {
            Guid userId;
            var sub = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (sub == null || !Guid.TryParse(sub.Value, out userId))
                return Unauthorized(new { message = "User ID not found in token." });

            try
            {
                var result = await _storyAiService.SuggestNextChapterAsync(id, userId, cancellationToken);
                if (result == null)
                    return NotFound(new { message = "Story not found, has no chapters, or Gemini API key is not configured. Check appsettings (Gemini:ApiKey)." });
                return Ok(result);
            }
            catch (RateLimitExceededException ex)
            {
                _logger.LogWarning(ex, "Rate limit exceeded for user {UserId}, story {StoryId}.", userId, id);
                return StatusCode(429, new { message = ex.Message });
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Gemini request failed for story {StoryId}.", id);
                var msg = ex.Message ?? "";
                if (msg.Contains("429") || msg.Contains("Too Many Requests") || msg.Contains("RESOURCE_EXHAUSTED") || msg.Contains("quota"))
                    return StatusCode(503, new { message = "Đã vượt hạn mức Gemini API (rate limit/quota). Vui lòng thử lại sau vài phút hoặc kiểm tra hạn mức tại https://ai.google.dev/gemini-api/docs/rate-limits", error = msg.Length > 300 ? msg[..300] + "..." : msg });
                return StatusCode(502, new { message = "AI service temporarily unavailable.", error = ex.Message });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Suggest next chapter failed for story {StoryId}.", id);
                return StatusCode(500, new { message = "An error occurred while getting AI suggestions.", error = ex.Message });
            }
        }
    }
}