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

        public AccountController(IAccountService accountService)
        {
            _accountService = accountService;
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

        [HttpGet("profile/{userId:guid}")]
        public async Task<IActionResult> GetProfileByUserId(Guid userId)
        {
            try
            {
                var profile = await _accountService.GetProfileAsync(userId);
                if (profile == null)
                    return NotFound(new { message = "User not found." });
                return Ok(profile);
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
                Guid userId = GetUserIdFromToken();

                var uploadsFolder = Path.Combine(
                    Directory.GetCurrentDirectory(),
                    "wwwroot",
                    "uploads",
                    "avatars"
                );

                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{userId}_{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await avatar.CopyToAsync(stream);
                }

                var avatarUrl = $"/uploads/avatars/{fileName}";

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