using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Chapters;
using Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/chapters")]
    public class ChaptersController : ControllerBase
    {
        private readonly IChapterService _chapterService;
        private readonly IStoryService _storyService;

        public ChaptersController(IChapterService chapterService, IStoryService storyService)
        {
            _chapterService = chapterService;
            _storyService = storyService;
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
    }
}