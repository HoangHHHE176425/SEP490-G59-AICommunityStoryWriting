using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Services.DTOs.Stories;

[ApiController]
[Route("api/stories")]
public class StoriesController : ControllerBase
{
    private readonly IStoryService _storyService;

    public StoriesController(IStoryService storyService)
    {
        _storyService = storyService;
    }

    // ================================
    // 1. CREATE STORY
    // ================================
    [HttpPost]
    [Consumes("multipart/form-data")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    public IActionResult CreateStory(
    [FromForm] string title,
    [FromForm] string summary,
    [FromForm] int categoryId,
    [FromForm] int? expectedChapters,
    [FromForm] int? releaseFrequencyDays,
    [FromForm] string? ageRating,
    [FromForm] IFormFile? coverImage
)
    {
        string? coverUrl = coverImage != null
            ? $"https://fake-storage/{Guid.NewGuid()}.jpg"
            : null;

        var authorIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        int authorId = int.TryParse(authorIdClaim, out var id) ? id : 1;

        var story = _storyService.Create(
            title,
            summary,
            categoryId,
            authorId,        
            coverUrl,
            expectedChapters ?? 0,
            releaseFrequencyDays ?? 7,
            ageRating ?? "ALL"
        );

        return Created(
            $"api/stories/{story.id}",
            new
            {
                storyId = story.id,
                status = story.status,
                slug = story.slug
            }
        );
    }

    // ================================
    // 2. GET ALL STORIES
    // ================================
    [HttpGet]
    public IActionResult GetAll()
        => Ok(_storyService.GetAll());

    // ================================
    // 3. GET STORY BY ID
    // ================================
    [HttpGet("{id:int}")]
    public IActionResult GetById(int id)
    {
        var story = _storyService.GetById(id);
        return story == null ? NotFound() : Ok(story);
    }

    // ================================
    // 4. GET STORIES BY AUTHOR
    // ================================
    [HttpGet("author/{authorId:int}")]
    public IActionResult GetByAuthor(int authorId)
        => Ok(_storyService.GetByAuthor(authorId));

    // ================================
    // 5. UPDATE STORY
    // ================================
    [HttpPut("{id:int}")]
    public IActionResult Update(int id, [FromBody] UpdateStoryRequest request)
    {
        var updated = _storyService.Update(
            id,
            request.Title,
            request.Summary,
            request.CategoryId,
            request.Status
        );

        return updated ? NoContent() : NotFound();
    }

    // ================================
    // 6. DELETE STORY
    // ================================
    [HttpDelete("{id:int}")]
    public IActionResult Delete(int id)
    {
        var deleted = _storyService.Delete(id);
        return deleted ? NoContent() : NotFound();
    }
}
