using System.Globalization;
using System.Text.Json;
using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Services.DTOs.Admin;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class AuthorAiTokenAutoGrantService : IAuthorAiTokenAutoGrantService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
    };

    private readonly StoryPlatformDbContext _db;
    private readonly ILogger<AuthorAiTokenAutoGrantService> _logger;

    public AuthorAiTokenAutoGrantService(StoryPlatformDbContext db, ILogger<AuthorAiTokenAutoGrantService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AuthorAiTokenAutoGrantRuleDto>> ListRulesAsync(CancellationToken cancellationToken = default)
    {
        var rows = await _db.author_ai_token_auto_grant_rules.AsNoTracking()
            .OrderByDescending(r => r.updated_at_utc)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        return rows.Select(MapToDto).ToList();
    }

    public async Task<AuthorAiTokenAutoGrantRuleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var row = await _db.author_ai_token_auto_grant_rules.AsNoTracking()
            .FirstOrDefaultAsync(r => r.id == id, cancellationToken)
            .ConfigureAwait(false);
        return row == null ? null : MapToDto(row);
    }

    public async Task<AuthorAiTokenAutoGrantRuleDto> CreateAsync(
        AuthorAiTokenAutoGrantRuleUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUpsert(request);
        var now = DateTime.UtcNow;
        var entity = new author_ai_token_auto_grant_rules
        {
            id = Guid.NewGuid(),
            is_enabled = request.IsEnabled,
            display_name = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim(),
            period_kind = NormalizePeriodKind(request.PeriodKind),
            grant_limit_field = NormalizeLimitField(request.GrantLimitField),
            grant_amount = request.GrantAmount,
            apply_to_all_authors = request.ApplyToAllAuthors,
            selected_user_ids = SerializeSelected(request),
            last_executed_period_key = null,
            last_run_at_utc = null,
            created_at_utc = now,
            updated_at_utc = now,
        };
        _db.author_ai_token_auto_grant_rules.Add(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapToDto(entity);
    }

    public async Task<AuthorAiTokenAutoGrantRuleDto?> UpdateAsync(
        Guid id,
        AuthorAiTokenAutoGrantRuleUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        ValidateUpsert(request);
        var entity = await _db.author_ai_token_auto_grant_rules.FirstOrDefaultAsync(r => r.id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity == null) return null;

        entity.is_enabled = request.IsEnabled;
        entity.display_name = string.IsNullOrWhiteSpace(request.DisplayName) ? null : request.DisplayName.Trim();
        entity.period_kind = NormalizePeriodKind(request.PeriodKind);
        entity.grant_limit_field = NormalizeLimitField(request.GrantLimitField);
        entity.grant_amount = request.GrantAmount;
        entity.apply_to_all_authors = request.ApplyToAllAuthors;
        entity.selected_user_ids = SerializeSelected(request);
        entity.updated_at_utc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return MapToDto(entity);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _db.author_ai_token_auto_grant_rules.FirstOrDefaultAsync(r => r.id == id, cancellationToken)
            .ConfigureAwait(false);
        if (entity == null) return false;
        _db.author_ai_token_auto_grant_rules.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task<int> ProcessDueRulesAsync(CancellationToken cancellationToken = default)
    {
        var rules = await _db.author_ai_token_auto_grant_rules
            .Where(r => r.is_enabled)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var executed = 0;
        foreach (var rule in rules)
        {
            if (await TryExecuteRuleIfNewPeriodAsync(rule, cancellationToken).ConfigureAwait(false))
                executed++;
        }

        if (executed > 0)
            _logger.LogInformation("Author AI token auto-grant: applied {Count} rule(s) for new UTC period(s).", executed);
        return executed;
    }

    public async Task<AuthorAiTokenAutoGrantRunResultDto?> RunRuleNowAsync(Guid ruleId, CancellationToken cancellationToken = default)
    {
        var rule = await _db.author_ai_token_auto_grant_rules.FirstOrDefaultAsync(r => r.id == ruleId, cancellationToken)
            .ConfigureAwait(false);
        if (rule == null) return null;
        var periodKey = GetCurrentPeriodKeyUtc(DateTime.UtcNow, rule.period_kind);
        var n = await ExecuteGrantForRuleAsync(rule, periodKey, cancellationToken).ConfigureAwait(false);
        rule.last_executed_period_key = periodKey;
        rule.last_run_at_utc = DateTime.UtcNow;
        rule.updated_at_utc = DateTime.UtcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new AuthorAiTokenAutoGrantRunResultDto { RuleId = rule.id, PeriodKey = periodKey, UsersUpdated = n };
    }

    private async Task<bool> TryExecuteRuleIfNewPeriodAsync(
        author_ai_token_auto_grant_rules rule,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var currentKey = GetCurrentPeriodKeyUtc(utcNow, rule.period_kind);
        if (string.Equals(rule.last_executed_period_key, currentKey, StringComparison.Ordinal))
            return false;

        var n = await ExecuteGrantForRuleAsync(rule, currentKey, cancellationToken).ConfigureAwait(false);
        rule.last_executed_period_key = currentKey;
        rule.last_run_at_utc = utcNow;
        rule.updated_at_utc = utcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (n > 0)
            _logger.LogInformation(
                "Author AI token auto-grant rule {RuleId}: period {PeriodKey}, users updated {N}.",
                rule.id,
                currentKey,
                n);
        return true;
    }

    private async Task<int> ExecuteGrantForRuleAsync(
        author_ai_token_auto_grant_rules rule,
        string periodKeyForLog,
        CancellationToken cancellationToken)
    {
        var userIds = await ResolveTargetAuthorUserIdsAsync(rule, cancellationToken).ConfigureAwait(false);
        if (userIds.Count == 0)
        {
            _logger.LogWarning("Author AI token auto-grant rule {RuleId}: no target authors (period {Key}).", rule.id, periodKeyForLog);
            return 0;
        }

        var field = rule.grant_limit_field;
        var amount = rule.grant_amount;
        var updated = 0;

        foreach (var chunk in userIds.Chunk(200))
        {
            var users = await _db.users
                .Where(u => chunk.Contains(u.id))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
            foreach (var u in users)
            {
                if (!string.Equals(u.role, "AUTHOR", StringComparison.OrdinalIgnoreCase))
                    continue;
                ApplyAdditive(u, field, amount);
                u.updated_at = DateTime.UtcNow;
                updated++;
            }
        }

        return updated;
    }

    private static void ApplyAdditive(users u, string field, long delta)
    {
        switch (field)
        {
            case "lifetime":
                u.author_ai_token_limit = AddToNullableLimit(u.author_ai_token_limit, delta);
                break;
            case "per_day":
                u.author_ai_token_limit_per_day = AddToNullableLimit(u.author_ai_token_limit_per_day, delta);
                break;
            case "per_week":
                u.author_ai_token_limit_per_week = AddToNullableLimit(u.author_ai_token_limit_per_week, delta);
                break;
            case "per_month":
                u.author_ai_token_limit_per_month = AddToNullableLimit(u.author_ai_token_limit_per_month, delta);
                break;
            default:
                throw new InvalidOperationException($"Unknown grant_limit_field: {field}");
        }
    }

    /// <summary>Null = không giới hạn; lần cộng đầu đặt = delta. Đã có giới hạn thì cộng thêm.</summary>
    private static long? AddToNullableLimit(long? current, long delta)
    {
        if (!current.HasValue)
            return delta;
        try
        {
            return checked(current.Value + delta);
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }

    private async Task<List<Guid>> ResolveTargetAuthorUserIdsAsync(
        author_ai_token_auto_grant_rules rule,
        CancellationToken cancellationToken)
    {
        if (rule.apply_to_all_authors)
        {
            return await _db.users.AsNoTracking()
                .Where(u => (u.role ?? "").ToUpper() == "AUTHOR")
                .Select(u => u.id)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        var ids = DeserializeSelected(rule.selected_user_ids);
        return ids.Distinct().ToList();
    }

    private static List<Guid> DeserializeSelected(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<Guid>();
        try
        {
            var list = JsonSerializer.Deserialize<List<Guid>>(json, JsonOptions);
            return list ?? new List<Guid>();
        }
        catch
        {
            try
            {
                var strings = JsonSerializer.Deserialize<List<string>>(json, JsonOptions);
                if (strings == null) return new List<Guid>();
                var result = new List<Guid>();
                foreach (var s in strings)
                {
                    if (Guid.TryParse(s, out var g)) result.Add(g);
                }

                return result;
            }
            catch
            {
                return new List<Guid>();
            }
        }
    }

    private static string? SerializeSelected(AuthorAiTokenAutoGrantRuleUpsertRequest request)
    {
        if (request.ApplyToAllAuthors) return null;
        var ids = (request.SelectedUserIds ?? new List<Guid>()).Where(x => x != Guid.Empty).Distinct().ToList();
        return ids.Count == 0 ? "[]" : JsonSerializer.Serialize(ids, JsonOptions);
    }

    private static void ValidateUpsert(AuthorAiTokenAutoGrantRuleUpsertRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        _ = NormalizePeriodKind(request.PeriodKind);
        _ = NormalizeLimitField(request.GrantLimitField);
        if (request.GrantAmount <= 0)
            throw new ArgumentException("grantAmount phải lớn hơn 0.");
        if (!request.ApplyToAllAuthors)
        {
            var ids = request.SelectedUserIds ?? new List<Guid>();
            if (ids.Count == 0 || ids.All(x => x == Guid.Empty))
                throw new ArgumentException("Khi không chọn \"tất cả tác giả\", cần ít nhất một selectedUserId.");
        }
    }

    private static string NormalizePeriodKind(string raw)
    {
        var s = (raw ?? "").Trim().ToLowerInvariant();
        return s switch
        {
            "daily_utc" => "daily_utc",
            "weekly_utc" => "weekly_utc",
            "monthly_utc" => "monthly_utc",
            _ => throw new ArgumentException("periodKind phải là daily_utc, weekly_utc hoặc monthly_utc."),
        };
    }

    private static string NormalizeLimitField(string raw)
    {
        var s = (raw ?? "").Trim().ToLowerInvariant();
        return s switch
        {
            "lifetime" => "lifetime",
            "per_day" => "per_day",
            "per_week" => "per_week",
            "per_month" => "per_month",
            _ => throw new ArgumentException("grantLimitField phải là lifetime, per_day, per_week hoặc per_month."),
        };
    }

    private static string GetCurrentPeriodKeyUtc(DateTime utcNow, string periodKind)
    {
        var u = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return periodKind switch
        {
            "daily_utc" => u.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "weekly_utc" => $"{ISOWeek.GetYear(u)}-W{ISOWeek.GetWeekOfYear(u):D2}",
            "monthly_utc" => u.ToString("yyyy-MM", CultureInfo.InvariantCulture),
            _ => throw new ArgumentOutOfRangeException(nameof(periodKind), periodKind, null),
        };
    }

    private static AuthorAiTokenAutoGrantRuleDto MapToDto(author_ai_token_auto_grant_rules r) => new()
    {
        Id = r.id,
        IsEnabled = r.is_enabled,
        DisplayName = r.display_name,
        PeriodKind = r.period_kind,
        GrantLimitField = r.grant_limit_field,
        GrantAmount = r.grant_amount,
        ApplyToAllAuthors = r.apply_to_all_authors,
        SelectedUserIds = DeserializeSelected(r.selected_user_ids),
        LastExecutedPeriodKey = r.last_executed_period_key,
        LastRunAtUtc = r.last_run_at_utc,
        CreatedAtUtc = r.created_at_utc,
        UpdatedAtUtc = r.updated_at_utc,
    };
}
