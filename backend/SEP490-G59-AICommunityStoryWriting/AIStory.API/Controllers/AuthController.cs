using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Auth; // Đảm bảo namespace chứa VerifyOtpRequest
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
                // SỬA MESSAGE: Báo người dùng check mail thay vì bảo login ngay
                return Ok(new { message = "Registration successful. Please check your email for OTP verification." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        // THÊM API MỚI: Xác thực OTP
        [HttpPost("verify-otp")]
        public async Task<IActionResult> VerifyOtp([FromBody] VerifyOtpRequest request)
        {
            try
            {
                await _authService.VerifyAccountAsync(request);
                return Ok(new { message = "Account verified successfully. You can now login." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            try
            {
                var response = await _authService.LoginAsync(request);

                // Store refresh token in HttpOnly cookie (professional approach)
                if (!string.IsNullOrEmpty(response.RefreshToken))
                {
                    SetRefreshTokenCookie(response.RefreshToken);
                }

                // Return access token only (refresh in cookie)
                return Ok(new AccessTokenResponse { AccessToken = response.AccessToken });
            }
            catch (Exception ex)
            {

                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> Refresh()
        {
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];
                if (string.IsNullOrEmpty(refreshToken))
                {
                    return Unauthorized(new { message = "Missing refresh token." });
                }

                var response = await _authService.RefreshAsync(refreshToken);
                if (!string.IsNullOrEmpty(response.RefreshToken))
                {
                    SetRefreshTokenCookie(response.RefreshToken);
                }

                return Ok(new AccessTokenResponse { AccessToken = response.AccessToken });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { message = ex.Message });
            }
        }

        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            try
            {
                var refreshToken = Request.Cookies["refreshToken"];
                if (!string.IsNullOrEmpty(refreshToken))
                {
                    await _authService.LogoutAsync(refreshToken);
                }

                DeleteRefreshTokenCookie();
                return Ok(new { message = "Logged out." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                await _authService.ForgotPasswordAsync(request);

                return Ok(new { message = "Nếu email tồn tại, mã OTP đã được gửi." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                await _authService.ResetPasswordAsync(request);
                return Ok(new { message = "Đặt lại mật khẩu thành công. Vui lòng đăng nhập lại." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private void SetRefreshTokenCookie(string refreshToken)
        {
            var options = new CookieOptions
            {
                HttpOnly = true,
                Secure = true, // required for SameSite=None in modern browsers
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddDays(30),
                Path = "/"
            };

            Response.Cookies.Append("refreshToken", refreshToken, options);
        }

        private void DeleteRefreshTokenCookie()
        {
            var options = new CookieOptions
            {
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/"
            };
            Response.Cookies.Delete("refreshToken", options);
        }
    }
}