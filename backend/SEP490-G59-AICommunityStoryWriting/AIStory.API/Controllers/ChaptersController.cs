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

        /// <summary>Tạo chapter mới - Chỉ AUTHOR</summary>
        [HttpPost]
        [Authorize(Roles = "AUTHOR")]
        public IActionResult Create([FromBody] CreateChapterRequestDto request)
        {
            try
            {
                var chapter = _chapterService.Create(request);
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

        /// <summary>Lấy chapter theo ID (Guid) (cho phép xem không cần đăng nhập)</summary>
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public IActionResult GetById(Guid id)
        {
            try
            {
                var chapter = _chapterService.GetById(id);
                return chapter == null ? NotFound(new { message = $"Chapter with ID {id} not found" }) : Ok(chapter);
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
                if (chapter.Status != "REJECTED")
                    return Ok(new { reason = (string?)null, rejectedAt = (DateTime?)null });
                return Ok(new { reason = chapter.RejectionReason, rejectedAt = chapter.RejectedAt });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Lỗi lấy lý do từ chối", error = ex.Message });
            }
        }
    }
}