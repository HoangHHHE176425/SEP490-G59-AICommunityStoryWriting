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

        /// <summary>Tạo thể loại mới (multipart: Name, Description, IsActive, IconImage)</summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] CreateCategoryWithImageRequestDto request)
        {
            try
            {
                string? iconUrl = null;

                if (request.IconImage != null && request.IconImage.Length > 0)
                {
                    iconUrl = await SaveIconFile(request.IconImage);
                }

                var dto = new CreateCategoryRequestDto
                {
                    Name = request.Name,
                    Description = request.Description,
                    IsActive = request.IsActive,
                    IconUrl = iconUrl
                };

                var category = _categoryService.Create(dto);
                return Created($"api/categories/{category.Id}", category);
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
                // Log the full exception for debugging
                return StatusCode(500, new { message = "Đã xảy ra lỗi khi tạo thể loại", error = ex.Message });
            }
        }

        /// <summary>Lấy thể loại</summary>
        [HttpGet]
        public IActionResult GetAll(
            [FromQuery] bool includeInactive = false,
            [FromQuery] Guid? parentId = null,
            [FromQuery] bool rootsOnly = false,
            [FromQuery] int? page = null,
            [FromQuery] int? pageSize = null,
            [FromQuery] string? search = null,
            [FromQuery] bool? isActive = null,
            [FromQuery] string? sortBy = null,
            [FromQuery] string? sortOrder = null)
        {
            // If pagination parameters are provided, use new paginated endpoint
            if (page.HasValue || pageSize.HasValue || !string.IsNullOrWhiteSpace(search) || isActive.HasValue || !string.IsNullOrWhiteSpace(sortBy))
            {
                var query = new CategoryQueryDto
                {
                    Page = page ?? 1,
                    PageSize = pageSize ?? 20,
                    Search = search,
                    IsActive = isActive ?? (includeInactive ? null : true),
                    SortBy = sortBy ?? "name",
                    SortOrder = sortOrder ?? "asc"
                };
                var result = _categoryService.GetAll(query);
                return Ok(result);
            }

            // Otherwise use old endpoint for backward compatibility
            var categories = _categoryService.GetAll(includeInactive, parentId, rootsOnly);
            return Ok(categories);
        }

        /// <summary>Lấy thể loại theo ID</summary>
        [HttpGet("{id:guid}")]
        public IActionResult GetById(Guid id)
        {
            var category = _categoryService.GetById(id);
            return category == null ? NotFound() : Ok(category);
        }

        /// <summary>Lấy thể loại theo slug</summary>
        [HttpGet("slug/{slug}")]
        public IActionResult GetBySlug(string slug)
        {
            var category = _categoryService.GetBySlug(slug);
            return category == null ? NotFound() : Ok(category);
        }

        /// <summary>Cập nhật thể loại (multipart: Name, Description, IsActive, IconImage)</summary>
        [HttpPut("{id:guid}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update(Guid id, [FromForm] UpdateCategoryWithImageRequestDto request)
        {
            try
            {
                string? iconUrl = null;

                if (request.IconImage != null && request.IconImage.Length > 0)
                {
                    var existing = _categoryService.GetById(id);
                    if (existing != null && !string.IsNullOrEmpty(existing.IconUrl))
                    {
                        DeleteIconFile(existing.IconUrl);
                    }
                    iconUrl = await SaveIconFile(request.IconImage);
                }
                else
                {
                    var existing = _categoryService.GetById(id);
                    if (existing != null && !string.IsNullOrEmpty(existing.IconUrl))
                        iconUrl = existing.IconUrl;
                }

                var dto = new UpdateCategoryRequestDto
                {
                    Name = request.Name,
                    Description = request.Description,
                    IsActive = request.IsActive,
                    IconUrl = iconUrl
                };

                var updated = _categoryService.Update(id, dto);
                return updated ? NoContent() : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Xóa thể loại</summary>
        [HttpDelete("{id:guid}")]
        public IActionResult Delete(Guid id)
        {
            try
            {
                var existing = _categoryService.GetById(id);
                if (existing != null && !string.IsNullOrEmpty(existing.IconUrl))
                {
                    DeleteIconFile(existing.IconUrl);
                }
                var deleted = _categoryService.Delete(id);
                return deleted ? NoContent() : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Bật/tắt trạng thái active</summary>
        [HttpPatch("{id:guid}/toggle-active")]
        public IActionResult ToggleActive(Guid id)
        {
            var toggled = _categoryService.ToggleActive(id);
            return toggled ? NoContent() : NotFound();
        }

        private static async Task<string> SaveIconFile(IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg" };
            var fileExtension = Path.GetExtension(file.FileName).ToLower();
            if (!allowedExtensions.Contains(fileExtension))
            {
                throw new ArgumentException("Invalid file type. Allowed: jpg, jpeg, png, gif, webp, svg");
            }
            if (file.Length > 2 * 1024 * 1024) // 2MB for icons
            {
                throw new ArgumentException("File size exceeds 2MB limit");
            }

            var uploadsFolder = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "uploads",
                "icons"
            );

            if (!Directory.Exists(uploadsFolder))
                Directory.CreateDirectory(uploadsFolder);

            var fileName = $"{Guid.NewGuid()}{fileExtension}";
            var filePath = Path.Combine(uploadsFolder, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return $"/uploads/icons/{fileName}";
        }

        private static void DeleteIconFile(string iconUrl)
        {
            if (string.IsNullOrEmpty(iconUrl)) return;
            var filePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                iconUrl.TrimStart('/')
            );
            if (System.IO.File.Exists(filePath))
            {
                try { System.IO.File.Delete(filePath); } catch { }
            }
        }
    }
}
