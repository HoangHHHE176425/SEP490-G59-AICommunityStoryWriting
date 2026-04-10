using AIStory.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Categories;
using Services.Interfaces;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/categories")]
    [Authorize] // Bắt buộc đăng nhập
    public class CategoriesController : ControllerBase
    {
        private readonly ICategoryService _categoryService;
        private readonly ICloudinaryImageService _cloudinaryImageService;
        private readonly ILogger<CategoriesController> _logger;

        public CategoriesController(
            ICategoryService categoryService,
            ICloudinaryImageService cloudinaryImageService,
            ILogger<CategoriesController> logger)
        {
            _categoryService = categoryService;
            _cloudinaryImageService = cloudinaryImageService;
            _logger = logger;
        }

        /// <summary>Tạo thể loại mới (multipart: Name, Description, IsActive, IconImage) - Chỉ ADMIN</summary>
        [HttpPost]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Create([FromForm] CreateCategoryWithImageRequestDto request)
        {
            try
            {
                string? iconUrl = null;

                if (request.IconImage != null && request.IconImage.Length > 0)
                {
                    if (!_cloudinaryImageService.IsConfigured)
                        return StatusCode(503, new { message = "Upload ảnh chưa được cấu hình (Cloudinary). Thêm Cloudinary:CloudName, ApiKey, ApiSecret trong cấu hình." });
                    try
                    {
                        ValidateIconFile(request.IconImage);
                        iconUrl = await _cloudinaryImageService.UploadImageAsync(
                            request.IconImage,
                            "category-icons",
                            HttpContext.RequestAborted);
                    }
                    catch (ArgumentException ex)
                    {
                        return BadRequest(new { message = ex.Message });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Cloudinary upload category icon failed on create");
                        return BadRequest(new { message = "Không upload được icon: " + ex.Message });
                    }
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

        /// <summary>Lấy thể loại (cho phép xem không cần đăng nhập)</summary>
        [HttpGet]
        [AllowAnonymous]
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

        /// <summary>Lấy thể loại theo ID (cho phép xem không cần đăng nhập)</summary>
        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public IActionResult GetById(Guid id)
        {
            var category = _categoryService.GetById(id);
            return category == null ? NotFound() : Ok(category);
        }

        /// <summary>Lấy thể loại theo slug (cho phép xem không cần đăng nhập)</summary>
        [HttpGet("slug/{slug}")]
        [AllowAnonymous]
        public IActionResult GetBySlug(string slug)
        {
            var category = _categoryService.GetBySlug(slug);
            return category == null ? NotFound() : Ok(category);
        }

        /// <summary>Cập nhật thể loại (multipart: Name, Description, IsActive, IconImage) - Chỉ ADMIN</summary>
        [HttpPut("{id:guid}")]
        [Consumes("multipart/form-data")]
        [Authorize(Roles = "ADMIN")]
        public async Task<IActionResult> Update(Guid id, [FromForm] UpdateCategoryWithImageRequestDto request)
        {
            try
            {
                string? iconUrl = null;

                if (request.IconImage != null && request.IconImage.Length > 0)
                {
                    if (!_cloudinaryImageService.IsConfigured)
                        return StatusCode(503, new { message = "Upload ảnh chưa được cấu hình (Cloudinary). Thêm Cloudinary:CloudName, ApiKey, ApiSecret trong cấu hình." });
                    try
                    {
                        ValidateIconFile(request.IconImage);
                        var existing = _categoryService.GetById(id);
                        if (existing != null && !string.IsNullOrEmpty(existing.IconUrl))
                            TryDeleteLocalIconFile(existing.IconUrl);
                        iconUrl = await _cloudinaryImageService.UploadImageAsync(
                            request.IconImage,
                            "category-icons",
                            HttpContext.RequestAborted);
                    }
                    catch (ArgumentException ex)
                    {
                        return BadRequest(new { message = ex.Message });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Cloudinary upload category icon failed on update");
                        return BadRequest(new { message = "Không upload được icon: " + ex.Message });
                    }
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

        /// <summary>Xóa thể loại - Chỉ ADMIN</summary>
        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "ADMIN")]
        public IActionResult Delete(Guid id)
        {
            try
            {
                var existing = _categoryService.GetById(id);
                if (existing != null && !string.IsNullOrEmpty(existing.IconUrl))
                    TryDeleteLocalIconFile(existing.IconUrl);
                var deleted = _categoryService.Delete(id);
                return deleted ? NoContent() : NotFound();
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Bật/tắt trạng thái active - Chỉ ADMIN</summary>
        [HttpPatch("{id:guid}/toggle-active")]
        [Authorize(Roles = "ADMIN")]
        public IActionResult ToggleActive(Guid id)
        {
            var toggled = _categoryService.ToggleActive(id);
            return toggled ? NoContent() : NotFound();
        }

        private static void ValidateIconFile(IFormFile file)
        {
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg" };
            var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
                throw new ArgumentException("Invalid file type. Allowed: jpg, jpeg, png, gif, webp, svg");
            if (file.Length > 2 * 1024 * 1024)
                throw new ArgumentException("File size exceeds 2MB limit");
        }

        /// <summary>Chỉ xóa file cục bộ cũ (uploads/icons/...); URL Cloudinary bỏ qua.</summary>
        private static void TryDeleteLocalIconFile(string iconUrl)
        {
            if (string.IsNullOrEmpty(iconUrl)) return;
            var rel = iconUrl.TrimStart('/');
            if (!rel.StartsWith("uploads/icons/", StringComparison.OrdinalIgnoreCase))
                return;
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", rel);
            if (System.IO.File.Exists(filePath))
            {
                try { System.IO.File.Delete(filePath); } catch { }
            }
        }
    }
}