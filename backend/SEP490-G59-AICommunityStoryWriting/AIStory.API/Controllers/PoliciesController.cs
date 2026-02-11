using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.Interfaces;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/policies")]
    public class PoliciesController : ControllerBase
    {
        private readonly IPolicyService _policyService;

        public PoliciesController(IPolicyService policyService)
        {
            _policyService = policyService;
        }

        /// <summary>
        /// Lấy policy active theo type (USER/AUTHOR/AI/...)
        /// </summary>
        [AllowAnonymous]
        [HttpGet("active")]
        public async Task<IActionResult> GetActive([FromQuery] string type)
        {
            var policy = await _policyService.GetActivePolicyAsync(type);
            if (policy == null) return NotFound(new { message = $"No active policy for type '{type}'." });
            return Ok(policy);
        }

        /// <summary>
        /// Dành cho màn hình "đăng ký làm tác giả": lấy policy active theo type và trạng thái đã accept của user hiện tại.
        /// </summary>
        [Authorize(Policy = "AuthorOnly")]
        [HttpGet("me/author-status")]
        public async Task<IActionResult> GetMyAuthorStatus([FromQuery] string type = "AUTHOR")
        {
            var userId = GetUserIdFromToken();
            var status = await _policyService.GetMyAuthorPolicyStatusAsync(userId, type);
            if (status == null) return NotFound(new { message = $"No active policy for type '{type}'." });
            return Ok(status);
        }

        /// <summary>
        /// Accept policy (lưu vào author_policy_acceptances). Dùng khi user apply làm tác giả hoặc tác giả accept các policy liên quan.
        /// </summary>
        [Authorize(Policy = "AuthorOnly")]
        [HttpPost("{policyId:guid}/accept-author")]
        public async Task<IActionResult> AcceptAsAuthor([FromRoute] Guid policyId)
        {
            var userId = GetUserIdFromToken();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var userAgent = Request.Headers.UserAgent.ToString();

            try
            {
                var created = await _policyService.AcceptPolicyAsAuthorAsync(userId, policyId, ip, userAgent);
                return Ok(new
                {
                    accepted = true,
                    alreadyAccepted = !created
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private Guid GetUserIdFromToken()
        {
            var userIdClaim =
                User.FindFirst(JwtRegisteredClaimNames.Sub) ??
                User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !Guid.TryParse(userIdClaim.Value, out Guid userId))
            {
                throw new Exception("Invalid Token or User ID format");
            }
            return userId;
        }
    }
}

