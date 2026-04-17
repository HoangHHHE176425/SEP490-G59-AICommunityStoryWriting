using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using Repositories.Interfaces;
using Services.DTOs.Admin.Users;
using Services.Interfaces;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/admin/users")]
    [Authorize(Roles = "ADMIN")]
    public class AdminUsersController : ControllerBase
    {
        private readonly IAdminUserService _service;
        private readonly IUserRepository _userRepository;
        private readonly IAuthorAiTokenBudgetService _authorAiTokenBudget;

        public AdminUsersController(
            IAdminUserService service,
            IUserRepository userRepository,
            IAuthorAiTokenBudgetService authorAiTokenBudget)
        {
            _service = service;
            _userRepository = userRepository;
            _authorAiTokenBudget = authorAiTokenBudget;
        }

        private Guid? GetCurrentUserId()
        {
            var sub = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            return Guid.TryParse(sub, out var id) ? id : null;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] AdminUserQueryDto query)
        {
            var result = await _service.GetUsersAsync(query);
            return Ok(result);
        }

        [HttpGet("stats")]
        public async Task<IActionResult> GetStats()
        {
            var stats = await _service.GetStatsAsync();
            return Ok(stats);
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetById(Guid id)
        {
            var user = await _service.GetUserByIdAsync(id);
            return user == null ? NotFound(new { message = "User not found." }) : Ok(user);
        }

        /// <summary>Token AI đã dùng (ai_usage_logs) và các giới hạn admin (tích lũy + theo ngày/tuần/tháng UTC).</summary>
        [HttpGet("{id:guid}/author-ai-token-budget")]
        public async Task<IActionResult> GetAuthorAiTokenBudget(Guid id, CancellationToken cancellationToken)
        {
            var dto = await _authorAiTokenBudget.GetBudgetAsync(id, cancellationToken).ConfigureAwait(false);
            return dto == null
                ? NotFound(new { message = "User not found." })
                : Ok(dto);
        }

        /// <summary>
        /// Đặt hoặc xóa giới hạn token AI. Chỉ cập nhật các trường có trong JSON (null = bỏ giới hạn cột đó).
        /// Khóa: <c>tokenLimit</c>, <c>tokenLimitPerDay</c>, <c>tokenLimitPerWeek</c>, <c>tokenLimitPerMonth</c>.
        /// </summary>
        [HttpPut("{id:guid}/author-ai-token-budget")]
        public async Task<IActionResult> PutAuthorAiTokenBudget(
            Guid id,
            [FromBody] JsonElement body,
            CancellationToken cancellationToken)
        {
            if (body.ValueKind != JsonValueKind.Object)
                return BadRequest(new { message = "Body phải là object JSON." });

            var setLifetime = false;
            var setDay = false;
            var setWeek = false;
            var setMonth = false;
            long? valLifetime = null;
            long? valDay = null;
            long? valWeek = null;
            long? valMonth = null;

            try
            {
                if (body.TryGetProperty("tokenLimit", out var el))
                {
                    setLifetime = true;
                    valLifetime = ParseNullableInt64(el, "tokenLimit");
                }

                if (body.TryGetProperty("tokenLimitPerDay", out var elD))
                {
                    setDay = true;
                    valDay = ParseNullableInt64(elD, "tokenLimitPerDay");
                }

                if (body.TryGetProperty("tokenLimitPerWeek", out var elW))
                {
                    setWeek = true;
                    valWeek = ParseNullableInt64(elW, "tokenLimitPerWeek");
                }

                if (body.TryGetProperty("tokenLimitPerMonth", out var elM))
                {
                    setMonth = true;
                    valMonth = ParseNullableInt64(elM, "tokenLimitPerMonth");
                }
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new { message = ex.Message });
            }

            if (!setLifetime && !setDay && !setWeek && !setMonth)
                return BadRequest(new
                {
                    message =
                        "Cần ít nhất một trường: tokenLimit, tokenLimitPerDay, tokenLimitPerWeek, tokenLimitPerMonth."
                });

            if (setLifetime && valLifetime is < 0
                || setDay && valDay is < 0
                || setWeek && valWeek is < 0
                || setMonth && valMonth is < 0)
                return BadRequest(new { message = "Giới hạn token không được âm." });

            var rows = await _userRepository.SetAuthorAiTokenBudgetLimitsAsync(
                    id,
                    setLifetime,
                    valLifetime,
                    setDay,
                    valDay,
                    setWeek,
                    valWeek,
                    setMonth,
                    valMonth,
                    cancellationToken)
                .ConfigureAwait(false);
            if (rows == 0)
                return NotFound(new { message = "User not found." });

            var dto = await _authorAiTokenBudget.GetBudgetAsync(id, cancellationToken).ConfigureAwait(false);
            return Ok(dto);
        }

        private static long? ParseNullableInt64(JsonElement el, string fieldName)
        {
            if (el.ValueKind == JsonValueKind.Null)
                return null;
            if (el.ValueKind == JsonValueKind.Number && el.TryGetInt64(out var n))
                return n;
            throw new ArgumentException($"{fieldName} phải là số nguyên hoặc null.");
        }

        [HttpGet("{id:guid}/moderator-categories")]
        public async Task<IActionResult> GetModeratorCategories(Guid id)
        {
            try
            {
                var ids = await _service.GetModeratorCategoriesAsync(id);
                return Ok(new { categoryIds = ids });
            }
            catch (InvalidOperationException ex)
            {
                return NotFound(new { message = ex.Message });
            }
        }

        public class SetModeratorCategoriesRequest
        {
            public List<Guid> CategoryIds { get; set; } = new();
        }

        [HttpPut("{id:guid}/moderator-categories")]
        public async Task<IActionResult> SetModeratorCategories(Guid id, [FromBody] SetModeratorCategoriesRequest request)
        {
            var ids = request?.CategoryIds ?? new List<Guid>();
            var ok = await _service.SetModeratorCategoriesAsync(id, ids);
            return ok ? NoContent() : NotFound(new { message = "User not found." });
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AdminCreateUserRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var created = await _service.CreateAsync(request);
                return Created($"api/admin/users/{created.Id}", created);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id:guid}/lock")]
        public async Task<IActionResult> Lock(Guid id)
        {
            var ok = await _service.SetStatusAsync(id, "BANNED");
            return ok ? NoContent() : NotFound(new { message = "User not found." });
        }

        [HttpPost("{id:guid}/unlock")]
        public async Task<IActionResult> Unlock(Guid id)
        {
            var ok = await _service.SetStatusAsync(id, "ACTIVE");
            return ok ? NoContent() : NotFound(new { message = "User not found." });
        }

        [HttpPost("{id:guid}/role")]
        public async Task<IActionResult> SetRole(Guid id, [FromBody] AdminSetUserRoleRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            var currentUserId = GetCurrentUserId();
            if (currentUserId.HasValue && currentUserId.Value == id)
            {
                return BadRequest(new { message = "ADMIN không thể tự thay đổi role của chính mình." });
            }
            var ok = await _service.SetRoleAsync(id, request.Role);
            return ok ? NoContent() : NotFound(new { message = "User not found." });
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var currentUserId = GetCurrentUserId();
            if (currentUserId.HasValue && currentUserId.Value == id)
            {
                return BadRequest(new { message = "Không thể xóa tài khoản đang đăng nhập." });
            }

            var existing = await _service.GetUserByIdAsync(id);
            if (existing == null)
            {
                return NotFound(new { message = "User not found." });
            }

            if (string.Equals(existing.Role, "ADMIN", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new { message = "Không thể xóa tài khoản quản trị viên." });
            }

            var ok = await _service.DeleteAsync(id);
            return ok ? NoContent() : NotFound(new { message = "User not found." });
        }
    }
}

