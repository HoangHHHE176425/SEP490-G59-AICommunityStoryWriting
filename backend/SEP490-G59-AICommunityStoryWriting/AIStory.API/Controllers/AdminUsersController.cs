using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

        public AdminUsersController(IAdminUserService service)
        {
            _service = service;
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
            var ok = await _service.SetRoleAsync(id, request.Role);
            return ok ? NoContent() : NotFound(new { message = "User not found." });
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var ok = await _service.DeleteAsync(id);
            return ok ? NoContent() : NotFound(new { message = "User not found." });
        }
    }
}

