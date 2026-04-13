using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using DataAccessObjects.DAOs;
using Services.DTOs.Authors;
using Services.DTOs.Stories;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/authors")]
    [Authorize]
    public class AuthorsController : ControllerBase
    {
        private Guid? GetCurrentUserId()
        {
            var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }

        /// <summary>Kiểm tra user hiện tại đã theo dõi tác giả (authorId) chưa.</summary>
        [HttpGet("{authorId:guid}/following")]
        [AllowAnonymous]
        public IActionResult GetFollowing(Guid authorId)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Ok(new { following = false });
            var following = FollowDAO.IsFollowingAuthor(userId.Value, authorId);
            return Ok(new { following });
        }

        /// <summary>Đếm số lượng người theo dõi một author.</summary>
        [HttpGet("{authorId:guid}/followers-count")]
        [AllowAnonymous]
        public IActionResult GetFollowersCount(Guid authorId)
        {
            var count = FollowDAO.GetAuthorFollowerCount(authorId);
            return Ok(new { followersCount = count });
        }

        /// <summary>Số follower mới từ 00:00 Thứ Hai tuần này (giờ máy chủ).</summary>
        [HttpGet("{authorId:guid}/followers-this-week")]
        [AllowAnonymous]
        public IActionResult GetNewFollowersThisWeek(Guid authorId)
        {
            var count = FollowDAO.GetAuthorNewFollowerCountThisCalendarWeek(authorId);
            return Ok(new { newFollowersThisWeek = count });
        }

        /// <summary>Danh sách người theo dõi tác giả (phân trang).</summary>
        [HttpGet("{authorId:guid}/followers")]
        [AllowAnonymous]
        public IActionResult GetFollowers(
            Guid authorId,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? search = null)
        {
            var (items, totalCount) = FollowDAO.GetAuthorFollowers(authorId, page, pageSize, search);
            var mapped = items.Select(x => new AuthorFollowerListItemDto
            {
                UserId = x.UserId,
                DisplayName = x.DisplayName,
                Email = x.Email,
                AvatarUrl = x.AvatarUrl,
                FollowedAt = x.FollowedAt
            }).ToList();

            return Ok(new PagedResultDto<AuthorFollowerListItemDto>
            {
                Items = mapped,
                TotalCount = totalCount,
                Page = page < 1 ? 1 : page,
                PageSize = pageSize < 1 ? 20 : Math.Min(pageSize, 100)
            });
        }

        /// <summary>Theo dõi tác giả. Khi tác giả có truyện/chương mới sẽ nhận thông báo.</summary>
        [HttpPost("{authorId:guid}/follow")]
        public IActionResult FollowAuthor(Guid authorId)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized(new { message = "User ID not found in token." });
            try
            {
                FollowDAO.FollowAuthor(userId.Value, authorId);
                return Ok(new { following = true, message = "Đã theo dõi tác giả." });
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("chính mình"))
            {
                return BadRequest(new { message = "Không thể theo dõi chính mình." });
            }
        }

        /// <summary>Bỏ theo dõi tác giả.</summary>
        [HttpDelete("{authorId:guid}/follow")]
        public IActionResult UnfollowAuthor(Guid authorId)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
                return Unauthorized(new { message = "User ID not found in token." });
            FollowDAO.UnfollowAuthor(userId.Value, authorId);
            return Ok(new { following = false, message = "Đã bỏ theo dõi tác giả." });
        }
    }
}
