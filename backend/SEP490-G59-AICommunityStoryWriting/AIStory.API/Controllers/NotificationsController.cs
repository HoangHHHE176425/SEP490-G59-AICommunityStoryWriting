using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Notifications;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/notifications")]
    [Authorize]
    public class NotificationsController : ControllerBase
    {
        private static NotificationDto MapToDto(notifications n)
        {
            return new NotificationDto
            {
                Id = n.id,
                Type = n.type,
                Title = n.title,
                Content = n.content,
                LinkUrl = n.link_url,
                IsRead = n.is_read == true,
                CreatedAt = n.created_at
            };
        }

        private Guid? GetCurrentUserId()
        {
            var claim = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
            if (claim != null && Guid.TryParse(claim.Value, out var userId))
                return userId;
            return null;
        }

        /// <summary>Lấy danh sách thông báo của user đăng nhập.</summary>
        [HttpGet]
        public IActionResult GetList([FromQuery] int limit = 50, [FromQuery] bool onlyUnread = false)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized(new { message = "Không xác định user." });

            var list = NotificationDAO.GetByUserId(userId.Value, limit, onlyUnread);
            return Ok(list.Select(MapToDto));
        }

        /// <summary>Số thông báo chưa đọc.</summary>
        [HttpGet("unread-count")]
        public IActionResult GetUnreadCount()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized(new { message = "Không xác định user." });

            var count = NotificationDAO.GetUnreadCount(userId.Value);
            return Ok(new { count });
        }

        /// <summary>Đánh dấu một thông báo đã đọc.</summary>
        [HttpPatch("{id:guid}/read")]
        public IActionResult MarkAsRead(Guid id)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized(new { message = "Không xác định user." });

            var ok = NotificationDAO.MarkAsRead(id, userId.Value);
            if (!ok)
                return NotFound(new { message = "Thông báo không tồn tại hoặc không thuộc về bạn." });
            return NoContent();
        }

        /// <summary>Đánh dấu tất cả thông báo đã đọc.</summary>
        [HttpPost("mark-all-read")]
        public IActionResult MarkAllAsRead()
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized(new { message = "Không xác định user." });

            NotificationDAO.MarkAllAsRead(userId.Value);
            return NoContent();
        }
    }
}
