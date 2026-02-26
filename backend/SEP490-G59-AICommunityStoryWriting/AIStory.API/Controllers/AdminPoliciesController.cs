using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Admin.Policies;
using Services.Interfaces;

namespace AIStory.API.Controllers
{
    [ApiController]
    [Route("api/admin/policies")]
    [Authorize(Roles = "ADMIN")]
    public class AdminPoliciesController : ControllerBase
    {
        private readonly IAdminPolicyService _service;

        public AdminPoliciesController(IAdminPolicyService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] AdminPolicyQueryDto query)
        {
            var result = await _service.GetPoliciesAsync(query);
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
            var policy = await _service.GetPolicyByIdAsync(id);
            return policy == null ? NotFound(new { message = "Policy not found." }) : Ok(policy);
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] AdminCreatePolicyRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var created = await _service.CreateAsync(request);
                return Created($"api/admin/policies/{created.Id}", created);
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPut("{id:guid}")]
        public async Task<IActionResult> Update(Guid id, [FromBody] AdminUpdatePolicyRequest request)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);
            try
            {
                var ok = await _service.UpdateAsync(id, request);
                return ok ? NoContent() : NotFound(new { message = "Policy not found." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id:guid}/activate")]
        public async Task<IActionResult> Activate(Guid id)
        {
            try
            {
                var ok = await _service.ActivateAsync(id);
                return ok ? NoContent() : NotFound(new { message = "Policy not found." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpPost("{id:guid}/deactivate")]
        public async Task<IActionResult> Deactivate(Guid id)
        {
            try
            {
                var ok = await _service.DeactivateAsync(id);
                return ok ? NoContent() : NotFound(new { message = "Policy not found." });
            }
            catch (Exception ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpDelete("{id:guid}")]
        public async Task<IActionResult> Delete(Guid id)
        {
            try
            {
                var ok = await _service.DeleteAsync(id);
                return ok ? NoContent() : NotFound(new { message = "Policy not found." });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }
    }
}

