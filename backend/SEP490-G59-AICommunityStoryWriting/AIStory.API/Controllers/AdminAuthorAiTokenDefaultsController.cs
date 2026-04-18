using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.DTOs.Admin;
using System.Text.Json;

namespace AIStory.API.Controllers;

/// <summary>
/// Admin cấu hình token AI mặc định cấp cho user khi lần đầu trở thành AUTHOR.
/// Lưu chung trong singleton rule của <c>author_ai_token_auto_grant_rules</c>.
/// </summary>
[ApiController]
[Route("api/admin/ai-usage-stats/author-token-defaults")]
[Authorize(Roles = "ADMIN")]
public sealed class AdminAuthorAiTokenDefaultsController : ControllerBase
{
    private const string RuleName = global::Services.Implementations.AuthorAiTokenAutoGrantService.SingletonRuleName;
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
        var rule = await _db.author_ai_token_auto_grant_rules.AsNoTracking()
            .OrderBy(r => r.created_at_utc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Map về DTO cũ để không vỡ UI hiện tại (chỉ dùng Lifetime).
        return Ok(new AuthorAiTokenDefaultsOnBecomeAuthorDto
        {
            Lifetime = rule != null && rule.is_enabled && rule.grant_amount > 0 ? rule.grant_amount : null,
            PerDay = null,
            PerWeek = null,
            PerMonth = null
        });
    }

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] AuthorAiTokenDefaultsOnBecomeAuthorDto body, CancellationToken cancellationToken)
    {
        if (body == null)
            return BadRequest(new { message = "Body là bắt buộc." });

        static bool Invalid(long? v) => v.HasValue && v.Value < 0;
        if (Invalid(body.Lifetime) || Invalid(body.PerDay) || Invalid(body.PerWeek) || Invalid(body.PerMonth))
            return BadRequest(new { message = "Giá trị token không được âm (hoặc null để không set)." });

        var amount = body.Lifetime ?? 0;
        var rule = await _db.author_ai_token_auto_grant_rules
            .OrderBy(r => r.created_at_utc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (rule == null)
        {
            var authorIds = await _db.users.AsNoTracking()
                .Where(u => (u.role ?? "").ToUpper() == "AUTHOR")
                .Select(u => u.id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            rule = new author_ai_token_auto_grant_rules
            {
                id = Guid.NewGuid(),
                is_enabled = amount > 0,
                display_name = RuleName,
                period_kind = "monthly_utc",
                grant_limit_field = "lifetime",
                grant_amount = amount,
                apply_to_all_authors = true,
                selected_user_ids = authorIds.Count == 0 ? "[]" : JsonSerializer.Serialize(authorIds.Distinct().ToList(), JsonOptions),
                created_at_utc = DateTime.UtcNow,
                updated_at_utc = DateTime.UtcNow
            };
            _db.author_ai_token_auto_grant_rules.Add(rule);
        }
        else
        {
            rule.is_enabled = amount > 0;
            rule.grant_amount = amount;
            rule.display_name = RuleName;
            rule.apply_to_all_authors = true;
            rule.period_kind = "monthly_utc";
            rule.grant_limit_field = "lifetime";
            var authorIds = await _db.users.AsNoTracking()
                .Where(u => (u.role ?? "").ToUpper() == "AUTHOR")
                .Select(u => u.id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            rule.selected_user_ids = authorIds.Count == 0 ? "[]" : JsonSerializer.Serialize(authorIds.Distinct().ToList(), JsonOptions);
            rule.updated_at_utc = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        // Trả DTO cũ (Lifetime) cho UI hiện tại.
        return Ok(new AuthorAiTokenDefaultsOnBecomeAuthorDto { Lifetime = amount > 0 ? amount : null, PerDay = null, PerWeek = null, PerMonth = null });
    }
}

