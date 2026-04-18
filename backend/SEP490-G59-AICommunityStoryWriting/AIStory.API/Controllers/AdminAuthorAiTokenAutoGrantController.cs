using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Services.DTOs.Admin;
using Services.Interfaces;

namespace AIStory.API.Controllers;

[ApiController]
[Route("api/admin/author-ai-token-auto-grants")]
[Authorize(Roles = "ADMIN")]
public sealed class AdminAuthorAiTokenAutoGrantController : ControllerBase
{
    private readonly IAuthorAiTokenAutoGrantService _service;

    public AdminAuthorAiTokenAutoGrantController(IAuthorAiTokenAutoGrantService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var list = await _service.ListRulesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken cancellationToken)
    {
        var row = await _service.GetByIdAsync(id, cancellationToken).ConfigureAwait(false);
        return row == null ? NotFound(new { message = "Không tìm thấy quy tắc." }) : Ok(row);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] AuthorAiTokenAutoGrantRuleUpsertRequest request, CancellationToken cancellationToken)
    {
        if (request == null) return BadRequest(new { message = "Body không hợp lệ." });
        try
        {
            var created = await _service.CreateAsync(request, cancellationToken).ConfigureAwait(false);
            return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] AuthorAiTokenAutoGrantRuleUpsertRequest request, CancellationToken cancellationToken)
    {
        if (request == null) return BadRequest(new { message = "Body không hợp lệ." });
        try
        {
            var updated = await _service.UpdateAsync(id, request, cancellationToken).ConfigureAwait(false);
            return updated == null ? NotFound(new { message = "Không tìm thấy quy tắc." }) : Ok(updated);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var ok = await _service.DeleteAsync(id, cancellationToken).ConfigureAwait(false);
        return ok ? NoContent() : NotFound(new { message = "Không tìm thấy quy tắc." });
    }

    /// <summary>Chạy ngay một quy tắc cho chu kỳ UTC hiện tại (cộng token theo cấu hình).</summary>
    [HttpPost("{id:guid}/run-now")]
    public async Task<IActionResult> RunNow(Guid id, CancellationToken cancellationToken)
    {
        try
        {
            var result = await _service.RunRuleNowAsync(id, cancellationToken).ConfigureAwait(false);
            return result == null ? NotFound(new { message = "Không tìm thấy quy tắc." }) : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
