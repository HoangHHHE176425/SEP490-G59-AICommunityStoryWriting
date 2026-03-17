using System.Linq;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Services.DTOs.Chapters;
using Services.DTOs.Comments;
using Services.Interfaces;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/chapters")]
    public class ChaptersController : ControllerBase
    {
        private readonly IChapterService _chapterService;
        private readonly IChapterVersionService _chapterVersionService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IStoryService _storyService;

        public ChaptersController(IChapterService chapterService, IChapterVersionService chapterVersionService, IServiceScopeFactory scopeFactory, IStoryService storyService)
        {
            _chapterService = chapterService;
            _chapterVersionService = chapterVersionService;
            _scopeFactory = scopeFactory;
            _storyService = storyService;
        }

        private Guid? GetCurrentUserId()
        {
            var sub = User.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }

        /// <summary>Tạo chapter mới - Chỉ AUTHOR. Sau khi lưu, Plot Manager (Agent 4) cập nhật memory nếu có nội dung.</summary>
        [HttpPost]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult Create([FromBody] CreateChapterRequestDto request)
        {
            try
            {
                var chapter = _chapterService.Create(request);
                if (!string.IsNullOrWhiteSpace(request.Content) && chapter.StoryId.HasValue)
                    TriggerPlotManagerUpdate(chapter.StoryId.Value, chapter.Id, request.Content);
                return Created($"api/chapters/{chapter.Id}", chapter);
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
                return StatusCode(500, new { message = "An error occurred while creating the chapter", error = ex.Message });
            }
        }

        /// <summary>Lấy danh sách chapters với pagination và filtering (cho phép xem không cần đăng nhập)</summary>
        [HttpGet]
        [AllowAnonymous]
        public IActionResult GetAll([FromQuery] ChapterQueryDto query)
        {
            try
            {
                var result = _chapterService.GetAll(query);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching chapters", error = ex.Message });
            }
        }

        /// <summary>Lấy chapter theo ID (Guid) (bắt buộc đăng nhập để đọc)</summary>
        [HttpGet("{id:guid}")]
        [Authorize]
        public IActionResult GetById(Guid id)
        {
            try
            {
                var chapter = _chapterService.GetById(id);
                if (chapter == null)
                    return NotFound(new { message = $"Chapter with ID {id} not found" });

                var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                          ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (chapter.StoryId.HasValue && Guid.TryParse(sub, out var userId) && userId != Guid.Empty)
                {
                    var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                    var ua = Request.Headers.UserAgent.ToString();
                    _storyService.RecordReadChapter(chapter.StoryId.Value, id, userId, ip, ua);
                }

                return Ok(chapter);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching the chapter", error = ex.Message });
            }
        }

        /// <summary>Lấy tất cả chapters của một story (Guid) (cho phép xem không cần đăng nhập)</summary>
        [HttpGet("story/{storyId:guid}")]
        [AllowAnonymous]
        public IActionResult GetByStoryId(Guid storyId)
        {
            try
            {
                var chapters = _chapterService.GetByStoryId(storyId);
                return Ok(chapters);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching chapters", error = ex.Message });
            }
        }

        /// <summary>Lấy chapter theo story ID (Guid) và order index (cho phép xem không cần đăng nhập)</summary>
        [HttpGet("story/{storyId:guid}/order/{orderIndex:int}")]
        [AllowAnonymous]
        public IActionResult GetByStoryIdAndOrderIndex(Guid storyId, int orderIndex)
        {
            try
            {
                var chapter = _chapterService.GetByStoryIdAndOrderIndex(storyId, orderIndex);
                return chapter == null ? NotFound(new { message = $"Chapter with order index {orderIndex} not found for story {storyId}" }) : Ok(chapter);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching the chapter", error = ex.Message });
            }
        }

        /// <summary>Lấy comment của chapter (cho phép xem không cần đăng nhập).</summary>
        [HttpGet("{id:guid}/comments")]
        [AllowAnonymous]
        public IActionResult GetChapterComments(Guid id)
        {
            try
            {
                var chapter = _chapterService.GetById(id);
                if (chapter == null)
                    return NotFound(new { message = "Chapter not found." });
                var entities = CommentDAO.GetChapterComments(id);
                var currentUserId = GetCurrentUserId();
                var dtos = entities.Select(c => MapToStoryCommentDto(c, currentUserId)).ToList();
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching chapter comments", error = ex.Message });
            }
        }

        /// <summary>Comment chapter. Bắt buộc đăng nhập và đã đọc ít nhất 1 chapter của truyện.</summary>
        [HttpPost("{id:guid}/comments")]
        [Authorize]
        public IActionResult AddChapterComment(Guid id, [FromBody] CreateStoryCommentRequestDto request)
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

                var chapter = _chapterService.GetById(id);
                if (chapter == null || !chapter.StoryId.HasValue)
                    return NotFound(new { message = "Chapter not found." });
                var storyId = chapter.StoryId.Value;
                var story = StoryDAO.GetById(storyId);
                if (story == null || !string.Equals(story.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase))
                    return BadRequest(new { message = "Chỉ có thể comment chapter của truyện đã PUBLISHED." });
                if (!UserActivityLogDAO.HasReadAnyChapterOfStory(userId.Value, storyId))
                    return BadRequest(new { message = "Bạn cần đọc ít nhất một chapter của truyện trước khi comment." });

                comments? parent = null;
                if (request.ParentId.HasValue)
                {
                    parent = CommentDAO.GetById(request.ParentId.Value);
                    if (parent == null || parent.chapter_id != id)
                        return BadRequest(new { message = "ParentId không hợp lệ (phải là comment của chapter này)." });
                }

                var entity = CommentDAO.AddChapterComment(storyId, id, userId.Value, content, request.ParentId);
                if (parent != null && parent.user_id.HasValue && parent.user_id != userId.Value)
                {
                    var replierName = entity.userNavigation?.user_profiles?.nickname?.Trim()
                        ?? entity.userNavigation?.email?.Trim() ?? "Ai đó";
                    try
                    {
                        NotificationDAO.NotifyCommentReply(parent.user_id.Value, replierName, storyId, story.title, entity.id);
                    }
                    catch { /* best effort */ }
                }
                var dto = MapToStoryCommentDto(entity, userId);
                return Created($"/api/chapters/{id}/comments/{dto.Id}", dto);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while adding chapter comment", error = ex.Message });
            }
        }

        /// <summary>Lấy danh sách người đã reaction comment của chapter.</summary>
        [HttpGet("{chapterId:guid}/comments/{commentId:guid}/reactions")]
        [AllowAnonymous]
        public IActionResult GetChapterCommentReactions(Guid chapterId, Guid commentId)
        {
            try
            {
                var comment = CommentDAO.GetById(commentId);
                if (comment == null || comment.chapter_id != chapterId)
                    return NotFound(new { message = "Comment not found or not belong to this chapter." });
                var list = CommentDAO.GetCommentReactions(commentId);
                var dtos = list.Select(x => new CommentReactionUserDto
                {
                    UserId = x.UserId,
                    UserDisplayName = x.DisplayName,
                    ReactionType = x.ReactionType
                }).ToList();
                return Ok(dtos);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while fetching comment reactions", error = ex.Message });
            }
        }

        /// <summary>Đặt reaction cho comment của chapter: LIKE, DISLIKE, FUNNY, SAD, ANGRY, LOVE, WOW.</summary>
        [HttpPost("{chapterId:guid}/comments/{commentId:guid}/reaction")]
        [Authorize]
        public IActionResult SetChapterCommentReaction(Guid chapterId, Guid commentId, [FromBody] SetCommentReactionRequestDto? request)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (!userId.HasValue)
                    return Unauthorized(new { message = "User ID not found in token." });
                var comment = CommentDAO.GetById(commentId);
                if (comment == null || comment.chapter_id != chapterId)
                    return NotFound(new { message = "Comment not found or not belong to this chapter." });
                var storyId = comment.story_id;
                var story = storyId.HasValue ? StoryDAO.GetById(storyId.Value) : null;
                var reactionType = request?.ReactionType;
                var newType = CommentDAO.SetReaction(userId.Value, commentId, reactionType);
                if (!string.IsNullOrWhiteSpace(newType) && comment.user_id.HasValue && comment.user_id != userId.Value)
                {
                    try
                    {
                        var actorName = NotificationDAO.GetUserDisplayName(userId.Value);
                        NotificationDAO.NotifyCommentReaction(comment.user_id.Value, actorName, storyId ?? Guid.Empty, story?.title, newType);
                    }
                    catch { /* best effort */ }
                }
                var counts = CommentDAO.GetReactionCounts(commentId);
                return Ok(new { userReactionType = newType, reactionCounts = counts });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while setting reaction", error = ex.Message });
            }
        }

        private static StoryCommentDto MapToStoryCommentDto(comments c, Guid? currentUserId = null)
        {
            var nickname = c.userNavigation?.user_profiles?.nickname;
            var email = c.userNavigation?.email;
            var display = !string.IsNullOrWhiteSpace(nickname) ? nickname : email;
            var userHasLiked = false;
            IReadOnlyDictionary<string, int>? reactionCounts = null;
            string? userReactionType = null;
            try
            {
                if (currentUserId.HasValue)
                {
                    userHasLiked = CommentDAO.HasLiked(currentUserId.Value, c.id);
                    userReactionType = CommentDAO.GetUserReaction(currentUserId.Value, c.id);
                }
                reactionCounts = CommentDAO.GetReactionCounts(c.id);
            }
            catch
            {
                reactionCounts = new Dictionary<string, int>();
            }
            return new StoryCommentDto
            {
                Id = c.id,
                StoryId = c.story_id ?? Guid.Empty,
                ParentId = c.parent_id,
                UserId = c.user_id ?? Guid.Empty,
                UserDisplayName = display,
                Content = c.content ?? "",
                LikesCount = c.likes_count ?? 0,
                UserHasLiked = userHasLiked,
                ReactionCounts = reactionCounts ?? new Dictionary<string, int>(),
                UserReactionType = userReactionType,
                CreatedAt = c.created_at
            };
        }

        /// <summary>Cập nhật chapter - Chỉ AUTHOR (chỉ được sửa chapter của chính mình)</summary>
        [HttpPut("{id:guid}")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult Update(Guid id, [FromBody] UpdateChapterRequestDto request)
        {
            try
            {
                var updated = _chapterService.Update(id, request);
                if (updated && (request.Content != null || (request.Status?.ToUpper() == "PUBLISHED")))
                {
                    var chapter = _chapterService.GetById(id);
                    if (chapter != null && !string.IsNullOrWhiteSpace(chapter.Content) && chapter.StoryId.HasValue)
                        TriggerPlotManagerUpdate(chapter.StoryId.Value, id, chapter.Content);
                }
                return updated ? NoContent() : NotFound(new { message = $"Chapter with ID {id} not found" });
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
                return StatusCode(500, new { message = "An error occurred while updating the chapter", error = ex.Message });
            }
        }

        /// <summary>Xóa chapter - Chỉ AUTHOR (chỉ được xóa chapter của chính mình)</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult Delete(Guid id)
        {
            try
            {
                var deleted = _chapterService.Delete(id);
                return deleted ? NoContent() : NotFound(new { message = $"Chapter with ID {id} not found" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while deleting the chapter", error = ex.Message });
            }
        }

        /// <summary>Publish chapter - Chỉ AUTHOR</summary>
        [HttpPost("{id:guid}/publish")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult Publish(Guid id)
        {
            try
            {
                var published = _chapterService.Publish(id);
                if (published)
                {
                    var chapter = _chapterService.GetById(id);
                    if (chapter != null && !string.IsNullOrWhiteSpace(chapter.Content) && chapter.StoryId.HasValue)
                        TriggerPlotManagerUpdate(chapter.StoryId.Value, id, chapter.Content);
                }
                return published ? NoContent() : NotFound(new { message = $"Chapter with ID {id} not found" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while publishing the chapter", error = ex.Message });
            }
        }

        /// <summary>Unpublish chapter - Chỉ AUTHOR</summary>
        [HttpPost("{id:guid}/unpublish")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult Unpublish(Guid id)
        {
            try
            {
                var unpublished = _chapterService.Unpublish(id);
                return unpublished ? NoContent() : NotFound(new { message = $"Chapter with ID {id} not found" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while unpublishing the chapter", error = ex.Message });
            }
        }

        /// <summary>Sắp xếp lại thứ tự chapter - Chỉ AUTHOR</summary>
        [HttpPost("{id:guid}/reorder")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult Reorder(Guid id, [FromBody] int newOrderIndex)
        {
            try
            {
                var reordered = _chapterService.Reorder(id, newOrderIndex);
                return reordered ? NoContent() : NotFound(new { message = $"Chapter with ID {id} not found" });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while reordering the chapter", error = ex.Message });
            }
        }

        /// <summary>Xem lý do từ chối chapter - Chỉ AUTHOR (chỉ chapter thuộc truyện của mình).</summary>
        [HttpGet("{id:guid}/rejection-reason")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult GetRejectionReason(Guid id)
        {
            try
            {
                var chapter = _chapterService.GetById(id);
                if (chapter == null)
                    return NotFound(new { message = "Chapter không tồn tại." });
                if (!chapter.StoryId.HasValue)
                    return Forbid();
                var story = _storyService.GetById(chapter.StoryId.Value);
                if (story == null)
                    return Forbid();
                var authorIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
                if (authorIdClaim == null || !Guid.TryParse(authorIdClaim.Value, out var currentUserId) || story.AuthorId != currentUserId)
                    return Forbid();
                if (chapter.Status == "PUBLISHED")
                    return Ok(new { reason = (string?)null, rejectedAt = (DateTime?)null });
                var (reason, rejectedAt) = _chapterService.GetLatestRejectionForChapter(id);
                return Ok(new { reason, rejectedAt });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi lấy lý do từ chối", error = ex.Message });
            }
        }

        // ---------- Chapter Versions (AUTHOR) ----------
        /// <summary>Lấy danh sách version của chapter. Chỉ AUTHOR. Bao gồm cả version đã xuất bản (PUBLISHED) — vẫn hiển thị trong danh sách, không ẩn/xóa.</summary>
        [HttpGet("{chapterId:guid}/versions")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult GetChapterVersions(Guid chapterId)
        {
            var list = _chapterVersionService.GetByChapterId(chapterId).ToList();
            return Ok(list);
        }

        /// <summary>Lấy chi tiết một version. Chỉ AUTHOR.</summary>
        [HttpGet("{chapterId:guid}/versions/{versionId:guid}")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult GetChapterVersion(Guid chapterId, Guid versionId)
        {
            var v = _chapterVersionService.GetById(versionId);
            if (v == null || v.ChapterId != chapterId)
                return NotFound(new { message = "Version không tồn tại." });
            return Ok(v);
        }

        /// <summary>Tạo version mới cho chapter. Chỉ AUTHOR.</summary>
        [HttpPost("{chapterId:guid}/versions")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult CreateChapterVersion(Guid chapterId, [FromBody] CreateChapterVersionRequestDto request)
        {
            var authorId = GetCurrentUserId();
            if (!authorId.HasValue)
                return Unauthorized(new { message = "Không xác định user. Vui lòng đăng nhập." });
            try
            {
                var v = _chapterVersionService.Create(chapterId, authorId.Value, request ?? new CreateChapterVersionRequestDto());
                return v == null ? NotFound(new { message = "Chapter không tồn tại." }) : CreatedAtAction(nameof(GetChapterVersion), new { chapterId, versionId = v.Id }, v);
            }
            catch (UnauthorizedAccessException)
            {
                return Forbid();
            }
        }

        /// <summary>Cập nhật version (chỉ DRAFT). Chỉ AUTHOR.</summary>
        [HttpPut("{chapterId:guid}/versions/{versionId:guid}")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult UpdateChapterVersion(Guid chapterId, Guid versionId, [FromBody] UpdateChapterVersionRequestDto request)
        {
            var authorId = GetCurrentUserId();
            if (!authorId.HasValue)
                return Unauthorized(new { message = "Không xác định user. Vui lòng đăng nhập." });
            if (request == null)
                return BadRequest();
            try
            {
                var ok = _chapterVersionService.Update(versionId, authorId.Value, request);
                return ok ? NoContent() : NotFound(new { message = "Version không tồn tại." });
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Xóa version (chỉ DRAFT). Chỉ AUTHOR.</summary>
        [HttpDelete("{chapterId:guid}/versions/{versionId:guid}")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult DeleteChapterVersion(Guid chapterId, Guid versionId)
        {
            var authorId = GetCurrentUserId();
            if (!authorId.HasValue)
                return Unauthorized(new { message = "Không xác định user. Vui lòng đăng nhập." });
            try
            {
                var ok = _chapterVersionService.Delete(versionId, authorId.Value);
                return ok ? NoContent() : NotFound(new { message = "Version không tồn tại hoặc không thể xóa." });
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Gửi duyệt version: áp dụng nội dung version lên chapter và chuyển chapter sang PENDING_REVIEW. Chỉ AUTHOR.</summary>
        [HttpPost("{chapterId:guid}/versions/{versionId:guid}/submit")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult SubmitChapterVersion(Guid chapterId, Guid versionId)
        {
            var authorId = GetCurrentUserId();
            if (!authorId.HasValue)
                return Unauthorized(new { message = "Không xác định user. Vui lòng đăng nhập." });
            try
            {
                var ok = _chapterVersionService.SubmitForReview(versionId, authorId.Value);
                return ok ? NoContent() : NotFound(new { message = "Version không tồn tại hoặc không thể gửi duyệt." });
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Hủy gửi duyệt version: đưa version và chapter về DRAFT. Chỉ AUTHOR, chỉ version PENDING_REVIEW.</summary>
        [HttpPost("{chapterId:guid}/versions/{versionId:guid}/unsubmit")]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult UnsubmitChapterVersion(Guid chapterId, Guid versionId)
        {
            var authorId = GetCurrentUserId();
            if (!authorId.HasValue)
                return Unauthorized(new { message = "Không xác định user. Vui lòng đăng nhập." });
            try
            {
                var ok = _chapterVersionService.CancelSubmit(versionId, authorId.Value);
                return ok ? NoContent() : NotFound(new { message = "Version không tồn tại hoặc không thể hủy gửi duyệt." });
            }
            catch (UnauthorizedAccessException) { return Forbid(); }
            catch (InvalidOperationException ex) { return BadRequest(new { message = ex.Message }); }
        }

        /// <summary>Gọi Plot Manager (Agent 4) cập nhật memory trong background; không chặn response.</summary>
        private void TriggerPlotManagerUpdate(Guid storyId, Guid chapterId, string content)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var plotManager = scope.ServiceProvider.GetRequiredService<IPlotManagerService>();
                    await plotManager.UpdateMemoryFromChapterAsync(storyId, chapterId, content, reIndexRagAfter: true);
                }
                catch
                {
                    // Best-effort; không làm fail request
                }
            });
        }
    }
}