using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
namespace AIStory.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            try
            {
                await _authService.RegisterAsync(request);
                return Ok(new { message = "Registration successful. Please login." });
            }
            catch (Exception ex)
            {
                // Trả về lỗi 400 kèm thông báo từ Service (ví dụ: Email đã tồn tại)
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var response = await _authService.LoginAsync(request);
                return Ok(response);
            }
            catch (Exception ex)
            {
                // Trả về 401 nếu sai mật khẩu/email
                return Unauthorized(new { message = ex.Message });
            }
        }
    }
}