using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Services.DTOs.Stories;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/stories")]
    public class StoriesController : ControllerBase
    {
        private readonly IStoryService _storyService;

        public StoriesController(IStoryService storyService)
        {
            _storyService = storyService;
        }

        /// Tạo story mới
        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create([FromForm] CreateStoryRequestDto request)
        {
            try
            {
                string? coverUrl = null;

                if (request.CoverImage != null && request.CoverImage.Length > 0)
                {
                    // Validate file type
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var fileExtension = Path.GetExtension(request.CoverImage.FileName).ToLower();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        return BadRequest(new { message = "Invalid file type. Allowed types: jpg, jpeg, png, gif, webp" });
                    }

                    // Validate file size (max 5MB)
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

                // Get author ID from claims or use default for testing
                var authorIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var authorId = authorIdClaim != null ? int.Parse(authorIdClaim) : 1;

                var story = _storyService.Create(request, authorId, coverUrl);
                return Created($"api/stories/{story.Id}", story);
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
                return StatusCode(500, new { message = "An error occurred while creating the story", error = ex.Message });
            }
        }

        /// Lấy danh sách stories với pagination và filtering
        [HttpGet]
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

        /// Lấy story theo ID
        [HttpGet("{id:int}")]
        public IActionResult GetById(int id)
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

        /// Lấy story theo slug
        [HttpGet("slug/{slug}")]
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

        /// Lấy stories theo author với pagination
        [HttpGet("author/{authorId:int}")]
        public IActionResult GetByAuthor(int authorId, [FromQuery] StoryQueryDto query)
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

        /// Cập nhật story (với hỗ trợ upload ảnh)
        [HttpPut("{id:int}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Update(int id, [FromForm] UpdateStoryWithImageRequestDto request)
        {
            try
            {
                string? coverUrl = null;

                // Handle cover image upload if provided
                if (request.CoverImage != null && request.CoverImage.Length > 0)
                {
                    // Validate file type
                    var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                    var fileExtension = Path.GetExtension(request.CoverImage.FileName).ToLower();
                    if (!allowedExtensions.Contains(fileExtension))
                    {
                        return BadRequest(new { message = "Invalid file type. Allowed types: jpg, jpeg, png, gif, webp" });
                    }

                    // Validate file size (max 5MB)
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

                    // Delete old cover image if exists
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
                            catch
                            {
                                // Ignore deletion errors
                            }
                        }
                    }

                    var fileName = $"{Guid.NewGuid()}{fileExtension}";
                    var filePath = Path.Combine(uploadsFolder, fileName);

                    using var stream = new FileStream(filePath, FileMode.Create);
                    await request.CoverImage.CopyToAsync(stream);

                    coverUrl = $"/uploads/covers/{fileName}";
                }

                // Convert to UpdateStoryRequestDto
                var updateRequest = new UpdateStoryRequestDto
                {
                    Title = request.Title,
                    Summary = request.Summary,
                    CategoryId = request.CategoryId,
                    Status = request.Status,
                    SaleType = request.SaleType,
                    AccessType = request.AccessType,
                    AgeRating = request.AgeRating,
                    ExpectedChapters = request.ExpectedChapters,
                    ReleaseFrequencyDays = request.ReleaseFrequencyDays,
                    CoverImageUrl = coverUrl
                };

                var updated = _storyService.Update(id, updateRequest);
                return updated ? NoContent() : NotFound(new { message = $"Story with ID {id} not found" });
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
                return StatusCode(500, new { message = "An error occurred while updating the story", error = ex.Message });
            }
        }

        /// Xóa story
        [HttpDelete("{id:int}")]
        public IActionResult Delete(int id)
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

        /// Publish story
        [HttpPost("{id:int}/publish")]
        public IActionResult Publish(int id)
        {
            try
            {
                var published = _storyService.Publish(id);
                return published ? NoContent() : NotFound(new { message = $"Story with ID {id} not found" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "An error occurred while publishing the story", error = ex.Message });
            }
        }

        /// Unpublish story
        [HttpPost("{id:int}/unpublish")]
        public IActionResult Unpublish(int id)
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
    }
}
