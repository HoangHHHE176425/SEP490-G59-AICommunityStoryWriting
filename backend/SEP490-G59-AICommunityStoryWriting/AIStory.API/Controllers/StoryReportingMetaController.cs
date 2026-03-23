using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace AIStory.API.Controllers;

[ApiController]
[Route("api/story-reporting")]
public class StoryReportingMetaController : ControllerBase
{
    private readonly IStoryReportService _storyReportService;

    public StoryReportingMetaController(IStoryReportService storyReportService)
    {
        _storyReportService = storyReportService;
    }

    /// <summary>Danh sách lý do báo cáo + điểm mức độ (public).</summary>
    [HttpGet("reasons")]
    [AllowAnonymous]
    public IActionResult GetReasons()
    {
        return Ok(_storyReportService.GetReasonOptions());
    }
}
