using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Chapters;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/chapters")]
    public class ChaptersController : ControllerBase
    {
        private readonly IChapterService _chapterService;

        public ChaptersController(IChapterService chapterService)
        {
            _chapterService = chapterService;
        }

        /// Tạo chapter mới
        [HttpPost]
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

        /// Lấy danh sách chapters với pagination và filtering
        [HttpGet]
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

        /// Lấy chapter theo ID
        [HttpGet("{id:int}")]
        public IActionResult GetById(int id)
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

        /// Lấy tất cả chapters của một story (không phân trang, sắp xếp theo order_index)
        [HttpGet("story/{storyId:int}")]
        public IActionResult GetByStoryId(int storyId)
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

        /// Lấy chapter theo story ID và order index
        [HttpGet("story/{storyId:int}/order/{orderIndex:int}")]
        public IActionResult GetByStoryIdAndOrderIndex(int storyId, int orderIndex)
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

        /// Cập nhật chapter
        [HttpPut("{id:int}")]
        public IActionResult Update(int id, [FromBody] UpdateChapterRequestDto request)
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

        /// Xóa chapter
        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
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

        /// Publish chapter
        [HttpPost("{id:int}/publish")]
        public IActionResult Publish(int id)
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

        /// Unpublish chapter
        [HttpPost("{id:int}/unpublish")]
        public IActionResult Unpublish(int id)
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

        /// Sắp xếp lại thứ tự chapter
        [HttpPost("{id:int}/reorder")]
        public IActionResult Reorder(int id, [FromBody] int newOrderIndex)
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
