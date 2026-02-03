using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Categories;
using Services.Interfaces;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/categories")]
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;

        public CategoriesController(ICategoryService categoryService)
        {
            _categoryService = categoryService;
        }

        /// Tạo thể loại mới
        [HttpPost]
        public IActionResult Create([FromBody] CreateCategoryRequestDto request)
        {
            try
            {
                var category = _categoryService.Create(request);
                return Created($"api/categories/{category.Id}", category);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// Lấy tất cả thể loại (chỉ lấy các thể loại đang active)
        [HttpGet]
        public IActionResult GetAll([FromQuery] bool includeInactive = false)
        {
            var categories = _categoryService.GetAll(includeInactive);
            return Ok(categories);
        }

        /// Lấy thể loại theo ID
        [HttpGet("{id:int}")]
        public IActionResult GetById(int id)
        {
            var category = _categoryService.GetById(id);
            return category == null ? NotFound() : Ok(category);
        }

        /// Lấy thể loại theo slug
        [HttpGet("slug/{slug}")]
        public IActionResult GetBySlug(string slug)
        {
            var category = _categoryService.GetBySlug(slug);
            return category == null ? NotFound() : Ok(category);
        }

        /// Cập nhật thể loại
        [HttpPut("{id:int}")]
        public IActionResult Update(int id, [FromBody] UpdateCategoryRequestDto request)
        {
            try
            {
                var updated = _categoryService.Update(id, request);
                return updated ? NoContent() : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// Xóa thể loại
        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
        {
            try
            {
                var deleted = _categoryService.Delete(id);
                return deleted ? NoContent() : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// Bật/tắt trạng thái active của thể loại
        [HttpPatch("{id:int}/toggle-active")]
        public IActionResult ToggleActive(int id)
        {
            var toggled = _categoryService.ToggleActive(id);
            return toggled ? NoContent() : NotFound();
        }
    }
}
