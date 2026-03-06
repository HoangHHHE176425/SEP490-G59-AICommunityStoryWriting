using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Services.DTOs.Comments;
using Services.DTOs.Stories;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/stories")]
    [Authorize] // Bắt buộc đăng nhập để xem
    public class StoriesController : ControllerBase
    {
        private readonly IStoryService _storyService;
        private readonly ILogger<StoriesController> _logger;

        public StoriesController(IStoryService storyService, ILogger<StoriesController> logger)
        {
            _storyService = storyService;
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

        /// <summary>Định danh viewer để chống spam lượt xem: ưu tiên UserId, không đăng nhập thì dùng IP.</summary>
        private string GetViewerKey()
        {
            var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!string.IsNullOrEmpty(sub))
                return "u:" + sub;
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "anon";
            return "ip:" + ip;
        }

        private Guid? GetCurrentUserId()
        {
            var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
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

        /// <summary>Lấy story theo ID (Guid) (cho phép xem không cần đăng nhập). Ghi nhận lượt xem (chống spam: 1 lượt/viewer/24h).</summary>
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public IActionResult GetById(Guid id)
        {
            try
            {
                var story = _storyService.GetById(id);
                if (story == null)
                    return NotFound(new { message = $"Story with ID {id} not found" });
                var viewerKey = GetViewerKey();
                _storyService.RecordViewIfAllowed(id, viewerKey);
                var userId = GetCurrentUserId();
                if (userId.HasValue)
                {
                    var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                    var ua = Request.Headers.UserAgent.ToString();
                    _storyService.RecordReadStory(id, userId.Value, ip, ua);
                }
                story.UserIsFollowing = GetCurrentUserId() is Guid uid ? UserLibraryDAO.IsFollowing(uid, id) : null;
                return Ok(story);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching the story", error = ex.Message });
            }
        }

        /// <summary>Lấy story theo slug (cho phép xem không cần đăng nhập). Ghi nhận lượt xem (chống spam: 1 lượt/viewer/24h).</summary>
        [HttpGet("slug/{slug}")]
        [AllowAnonymous]
        public IActionResult GetBySlug(string slug)
        {
            try
            {
                var story = _storyService.GetBySlug(slug);
                if (story == null)
                    return NotFound(new { message = $"Story with slug '{slug}' not found" });
                var viewerKey = GetViewerKey();
                _storyService.RecordViewIfAllowed(story.Id, viewerKey);
                var userId = GetCurrentUserId();
                if (userId.HasValue)
                {
                    var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                    var ua = Request.Headers.UserAgent.ToString();
                    _storyService.RecordReadStory(story.Id, userId.Value, ip, ua);
                }
                story.UserIsFollowing = GetCurrentUserId() is Guid uid ? UserLibraryDAO.IsFollowing(uid, story.Id) : null;
                return Ok(story);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching the story", error = ex.Message });
            }
        }

        /// <summary>Theo dõi story (chỉ story PUBLISHED). Khi có chapter mới sẽ nhận thông báo.</summary>
        [HttpPost("{id:guid}/follow")]
        [Authorize]
        public IActionResult FollowStory(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User ID not found in token." });
                var story = StoryDAO.GetById(id);
                if (story == null)
                    return NotFound(new { message = "Story not found." });
                if (!string.Equals(story.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Chỉ có thể theo dõi truyện đã xuất bản (PUBLISHED)." });
                UserLibraryDAO.Follow(userId.Value, id);
                return Ok(new { following = true, message = "Đã theo dõi truyện." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while following story", error = ex.Message });
            }
        }

        /// <summary>Bỏ theo dõi story.</summary>
        [HttpDelete("{id:guid}/follow")]
        [Authorize]
        public IActionResult UnfollowStory(Guid id)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User ID not found in token." });
                UserLibraryDAO.Unfollow(userId.Value, id);
                return Ok(new { following = false, message = "Đã bỏ theo dõi." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while unfollowing story", error = ex.Message });
            }
        }

        /// <summary>Đánh giá story (1..5 sao). Chặn nếu user chưa đọc story.</summary>
        [HttpPost("{id:guid}/ratings")]
        [Authorize]
        public IActionResult RateStory(Guid id, [FromBody] RateStoryRequestDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User ID not found in token." });
                var (avg, count) = _storyService.RateStory(id, userId.Value, request.StarValue, request.ReviewText);
                return Ok(new { avgRating = avg, ratingCount = count });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while rating the story", error = ex.Message });
            }
        }

        /// <summary>Lấy comment của story (cho phép xem không cần đăng nhập). Nếu đăng nhập sẽ có userHasLiked.</summary>
        [HttpGet("{id:guid}/comments")]
        [AllowAnonymous]
        public IActionResult GetStoryComments(Guid id)
        {
            try
            {
                var story = StoryDAO.GetById(id);
                if (story == null)
                    return NotFound(new { message = $"Story with ID {id} not found" });
                var entities = CommentDAO.GetStoryComments(id);
                var currentUserId = GetCurrentUserId();
                var dtos = entities.Select(c => MapToStoryCommentDto(c, currentUserId)).ToList();
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching comments", error = ex.Message });
            }
        }

        /// <summary>Comment story. Bắt buộc login + đã đọc ít nhất 1 chapter mới được comment.</summary>
        [HttpPost("{id:guid}/comments")]
        [Authorize]
        public IActionResult AddStoryComment(Guid id, [FromBody] CreateStoryCommentRequestDto request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User ID not found in token." });

                if (request == null || string.IsNullOrWhiteSpace(request.Content))
                    return BadRequest(new { message = "Nội dung comment không được để trống." });
                var content = request.Content.Trim();
                if (content.Length > 2000)
                    return BadRequest(new { message = "Nội dung comment tối đa 2000 ký tự." });

                var story = StoryDAO.GetById(id);
                if (story == null)
                    return NotFound(new { message = $"Story with ID {id} not found" });
                if (!string.Equals(story.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Chỉ có thể comment truyện đã PUBLISHED." });

                if (!UserActivityLogDAO.HasReadAnyChapterOfStory(userId.Value, id))
                    return BadRequest(new { message = "Bạn cần đọc ít nhất một chapter trước khi comment." });

                if (request.ParentId.HasValue)
                {
                    var parent = CommentDAO.GetById(request.ParentId.Value);
                    if (parent == null || parent.story_id != id)
                        return BadRequest(new { message = "ParentId không hợp lệ." });
                }

                var entity = CommentDAO.AddStoryComment(id, userId.Value, content, request.ParentId);
                var dto = MapToStoryCommentDto(entity, userId);
                return Created($"/api/stories/{id}/comments/{dto.Id}", dto);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while adding comment", error = ex.Message });
            }
        }

        private static StoryCommentDto MapToStoryCommentDto(comments c, Guid? currentUserId = null)
        {
            var nickname = c.userNavigation?.user_profiles?.nickname;
            var email = c.userNavigation?.email;
            var display = !string.IsNullOrWhiteSpace(nickname) ? nickname : email;
            var userHasLiked = currentUserId.HasValue && CommentDAO.HasLiked(currentUserId.Value, c.id);
            return new StoryCommentDto
            {
                Id = c.id,
                StoryId = c.story_id ?? Guid.Empty,
                ParentId = c.parent_id,
                UserId = c.user_id ?? Guid.Empty,
                UserDisplayName = display,
                Content = c.content,
                LikesCount = c.likes_count ?? 0,
                UserHasLiked = userHasLiked,
                CreatedAt = c.created_at
            };
        }

        /// <summary>Thả cảm xúc (like) comment: 1 user chỉ 1 lần/comment. Gọi lại để bỏ like.</summary>
        [HttpPost("{storyId:guid}/comments/{commentId:guid}/like")]
        [Authorize]
        public IActionResult ToggleCommentLike(Guid storyId, Guid commentId)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User ID not found in token." });
                var comment = CommentDAO.GetById(commentId);
                if (comment == null || comment.story_id != storyId)
                    return NotFound(new { message = "Comment not found or not belong to this story." });
                var liked = CommentDAO.ToggleLike(userId.Value, commentId);
                var updated = CommentDAO.GetById(commentId);
                return Ok(new { liked, likesCount = updated?.likes_count ?? 0 });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while toggling like", error = ex.Message });
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
                    CoverImageUrl = coverUrl,
                    ChangeSummary = request.ChangeSummary
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

        /// <summary>Xem lý do từ chối truyện - Chỉ AUTHOR (chỉ truyện của mình).</summary>
        [HttpGet("{id:guid}/rejection-reason")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult GetRejectionReason(Guid id)
        {
            try
            {
                var story = _storyService.GetById(id);
                if (story == null)
                    return NotFound(new { message = "Truyện không tồn tại." });
                var authorIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
                if (authorIdClaim == null || !Guid.TryParse(authorIdClaim.Value, out var currentUserId) || story.AuthorId != currentUserId)
                    return Forbid();
                if (story.Status != "REJECTED")
                    return Ok(new { reason = (string?)null, rejectedAt = (DateTime?)null });
                return Ok(new { reason = story.RejectionReason, rejectedAt = story.RejectedAt });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi lấy lý do từ chối", error = ex.Message });
            }
        }
    }
}