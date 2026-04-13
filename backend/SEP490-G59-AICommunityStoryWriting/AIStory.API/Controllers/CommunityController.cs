using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/community")]
    public class CommunityController : ControllerBase
    {
        private readonly IStoryService _storyService;

        public CommunityController(IStoryService storyService)
        {
            _storyService = storyService;
        }

        /// <summary>
        /// Thống kê công khai: truyện PUBLISHED và không bị ẩn compliance (cùng logic danh sách công khai);
        /// authorsCount = số user role AUTHOR, không tính BANNED (không yêu cầu có truyện publish).
        /// </summary>
        [HttpGet("stats")]
        [AllowAnonymous]
        public IActionResult GetStats()
        {
            try
            {
                var stats = _storyService.GetPublicCommunityStats();
                return Ok(stats);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { message = "Không tải được thống kê cộng đồng.", error = ex.Message });
            }
        }
    }
}
