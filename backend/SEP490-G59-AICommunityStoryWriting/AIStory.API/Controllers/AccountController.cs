using AIStory.API.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Account;
using Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AIStory.API.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly IAccountService _accountService;
        private readonly ICloudinaryImageService _cloudinaryImageService;

        public AccountController(IAccountService accountService, ICloudinaryImageService cloudinaryImageService)
        {
            _accountService = accountService;
            _cloudinaryImageService = cloudinaryImageService;
        }

        private Guid GetUserIdFromToken()
        {
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out Guid userId))
            {
                throw new Exception("Invalid Token or User ID format");
            }
            return userId;
        }
        [Authorize]
        [HttpDelete("delete")]
        public async Task<IActionResult> DeleteAccount()
        {
            try
            {
                var userId = GetUserIdFromToken();

                await _accountService.DeleteAccountAsync(userId);

                return Ok(new { message = "Tài khoản đã được xóa thành công." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
        [HttpPut("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            try
            {
                Guid userId = GetUserIdFromToken();
                await _accountService.ChangePasswordAsync(userId, request);

                return Ok(new { message = "Đổi mật khẩu thành công!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("profile")]
        public async Task<IActionResult> UpdateProfile([FromBody] UpdateProfileRequest request)
        {
            try
            {
                Guid userId = GetUserIdFromToken();
                await _accountService.UpdateProfileAsync(userId, request);
                return Ok(new { message = "Cập nhật hồ sơ thành công!" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("profile")]
        public async Task<IActionResult> GetMyProfile()
        {
            try
            {
                Guid userId = GetUserIdFromToken();
                var profile = await _accountService.GetProfileAsync(userId);
                return Ok(profile);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("author-onboarding")]
        public async Task<IActionResult> GetAuthorOnboardingStatus()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var status = await _accountService.GetAuthorOnboardingStatusAsync(userId);
                return Ok(status);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("become-author")]
        public async Task<IActionResult> BecomeAuthor()
        {
            try
            {
                var userId = GetUserIdFromToken();
                var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                var userAgent = Request.Headers.UserAgent.ToString();
                var result = await _accountService.BecomeAuthorAsync(userId, ip, userAgent);
                return Ok(result);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        /// <summary>Hồ sơ công khai theo userId (trang tác giả). Khách xem được; không trả email/SĐT/CCCD.</summary>
        [AllowAnonymous]
        [HttpGet("profile/{userId:guid}")]
        public async Task<IActionResult> GetProfileByUserId(Guid userId)
        {
            try
            {
                var profile = await _accountService.GetProfileAsync(userId);
                Guid? viewerId = null;
                if (User?.Identity?.IsAuthenticated == true)
                {
                    var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
                    if (userIdClaim != null && Guid.TryParse(userIdClaim.Value, out var vid))
                        viewerId = vid;
                }
                var isViewingSelf = viewerId.HasValue && viewerId.Value == userId;
                if (!isViewingSelf)
                {
                    profile.Email = string.Empty;
                    profile.Phone = null;
                    profile.IdNumber = null;
                }
                return Ok(profile);
            }
            catch (Exception ex) when (ex.Message.Contains("not found", StringComparison.OrdinalIgnoreCase))
            {
                return NotFound(new { message = "User not found." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("avatar")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAvatar([FromForm] IFormFile avatar)
        {
            if (avatar == null || avatar.Length == 0)
            {
                return BadRequest(new { message = "Vui lòng chọn file ảnh." });
            }

            // Validate extension
            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
            var fileExtension = Path.GetExtension(avatar.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(fileExtension))
            {
                return BadRequest(new { message = $"Invalid file type. Allowed types: {string.Join(", ", allowedExtensions)}" });
            }

            // Validate size (max 2MB)
            if (avatar.Length > 2 * 1024 * 1024)
            {
                return BadRequest(new { message = "File size exceeds 2MB limit" });
            }

            try
            {
                if (!_cloudinaryImageService.IsConfigured)
                    return StatusCode(503, new { message = "Upload ảnh chưa được cấu hình (Cloudinary). Thêm Cloudinary:CloudName, ApiKey, ApiSecret trong cấu hình." });

                Guid userId = GetUserIdFromToken();

                var avatarUrl = await _cloudinaryImageService.UploadImageAsync(
                    avatar,
                    "avatars",
                    HttpContext.RequestAborted);

                await _accountService.UpdateProfileAsync(userId, new UpdateProfileRequest
                {
                    AvatarUrl = avatarUrl
                });

                return Ok(new { message = "Upload avatar thành công!", avatarUrl });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}