using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json;
using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.DTOs.Admin;

namespace AIStory.API.Controllers;

/// <summary>Admin cấu hình hạn mức token AI mặc định khi user lần đầu trở thành AUTHOR.</summary>
[ApiController]
[Route("api/admin/ai-usage-stats/author-token-defaults")]
[Authorize(Roles = "ADMIN")]
public sealed class AdminAuthorAiTokenDefaultsController : ControllerBase
{
    private const string SettingsKey = "author_ai_token_defaults_on_become_author";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly StoryPlatformDbContext _db;

    public AdminAuthorAiTokenDefaultsController(StoryPlatformDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var row = await _db.system_settings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.key == SettingsKey, cancellationToken)
            .ConfigureAwait(false);

        if (row == null || string.IsNullOrWhiteSpace(row.value))
            return Ok(new AuthorAiTokenDefaultsOnBecomeAuthorDto());

        try
        {
            var dto = JsonSerializer.Deserialize<AuthorAiTokenDefaultsOnBecomeAuthorDto>(row.value, JsonOptions)
                      ?? new AuthorAiTokenDefaultsOnBecomeAuthorDto();
            return Ok(dto);
        }
        catch
        {
            // Nếu DB có value hỏng, trả empty để UI không vỡ.
            return Ok(new AuthorAiTokenDefaultsOnBecomeAuthorDto());
        }
    }

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] AuthorAiTokenDefaultsOnBecomeAuthorDto body, CancellationToken cancellationToken)
    {
        if (body == null)
            return BadRequest(new { message = "Body là bắt buộc." });

        static bool Invalid(long? v) => v.HasValue && v.Value < 0;
        if (Invalid(body.Lifetime) || Invalid(body.PerDay) || Invalid(body.PerWeek) || Invalid(body.PerMonth))
            return BadRequest(new { message = "Giá trị token không được âm (hoặc null để không set)." });

        Guid? updatedBy = null;
        var sub = User.FindFirst(JwtRegisteredClaimNames.Sub) ?? User.FindFirst(ClaimTypes.NameIdentifier);
        if (sub != null && Guid.TryParse(sub.Value, out var uid))
            updatedBy = uid;

        var json = JsonSerializer.Serialize(body, JsonOptions);
        var row = await _db.system_settings.FirstOrDefaultAsync(x => x.key == SettingsKey, cancellationToken).ConfigureAwait(false);
        if (row == null)
        {
            row = new system_settings
            {
                key = SettingsKey,
                value = json,
                value_type = "json",
                description = "Default AI token limits applied when a user first becomes AUTHOR (null = do not set column).",
                updated_at = DateTime.UtcNow,
                updated_by = updatedBy
            };
            _db.system_settings.Add(row);
        }
        else
        {
            row.value = json;
            row.value_type = "json";
            row.updated_at = DateTime.UtcNow;
            row.updated_by = updatedBy;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Ok(body);
    }
}

