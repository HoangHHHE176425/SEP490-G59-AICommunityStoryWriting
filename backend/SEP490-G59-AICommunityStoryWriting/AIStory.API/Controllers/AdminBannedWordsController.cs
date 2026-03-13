using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BusinessObjects.Entities;
using Repositories;
using Services.DTOs.Admin.BannedWords;
using Services.Implementations;

namespace AIStory.API.Controllers;

/// <summary>Admin quản lý từ cấm dùng cho check-chapter (guardrail). Lưu trong ai_sensitive_words, category = BannedWord.</summary>
[ApiController]
[Route("api/admin/banned-words")]
[Authorize(Roles = "ADMIN")]
public class AdminBannedWordsController : ControllerBase
{
    private readonly IAiSensitiveWordsRepository _repository;

    public AdminBannedWordsController(IAiSensitiveWordsRepository repository)
    {
        _repository = repository;
    }

    /// <summary>Lấy danh sách từ cấm (check-chapter). Có thể lọc theo category; mặc định BannedWord.</summary>
    [HttpGet]
    public IActionResult GetAll([FromQuery] string? category = null)
    {
        var c = string.IsNullOrWhiteSpace(category) ? ContentGuardrailService.BannedWordCategory : category.Trim();
        var list = _repository.GetAll(c);
        var dtos = list.Select(w => new BannedWordItemDto
        {
            Id = w.id,
            Word = w.word ?? "",
            Category = w.category,
            CreatedAt = w.created_at
        }).ToList();
        return Ok(dtos);
    }

    /// <summary>Lấy một từ cấm theo id.</summary>
    [HttpGet("{id:guid}")]
    public IActionResult GetById(Guid id)
    {
        var w = _repository.GetById(id);
        if (w == null) return NotFound(new { message = "Từ cấm không tồn tại." });
        return Ok(new BannedWordItemDto
        {
            Id = w.id,
            Word = w.word ?? "",
            Category = w.category,
            CreatedAt = w.created_at
        });
    }

    /// <summary>Thêm từ cấm (phục vụ check-chapter). Category mặc định BannedWord.</summary>
    [HttpPost]
    public IActionResult Add([FromBody] AddBannedWordRequest request)
    {
        if (request == null || string.IsNullOrWhiteSpace(request.Word))
            return BadRequest(new { message = "Word là bắt buộc." });

        var word = request.Word.Trim();
        if (word.Length > 100)
            return BadRequest(new { message = "Từ không được quá 100 ký tự." });

        var category = string.IsNullOrWhiteSpace(request.Category) ? ContentGuardrailService.BannedWordCategory : request.Category.Trim();
        if (category.Length > 50) category = category[..50];

        var entity = new ai_sensitive_words
        {
            id = Guid.NewGuid(),
            word = word,
            category = category,
            created_at = DateTime.UtcNow
        };
        _repository.Add(entity);
        return CreatedAtAction(nameof(GetById), new { id = entity.id }, new BannedWordItemDto
        {
            Id = entity.id,
            Word = entity.word,
            Category = entity.category,
            CreatedAt = entity.created_at
        });
    }

    /// <summary>Xóa từ cấm theo id.</summary>
    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id)
    {
        var ok = _repository.Delete(id);
        return ok ? NoContent() : NotFound(new { message = "Từ cấm không tồn tại." });
    }
}
