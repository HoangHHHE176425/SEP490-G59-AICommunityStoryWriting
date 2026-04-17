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
    // Singleton: bảng author_ai_token_auto_grant_rules chỉ có 1 dòng.
    // Dòng này vừa dùng để:
    // - cấp token ban đầu khi user trở thành AUTHOR (track bằng selected_user_ids),
    // - gia hạn theo tháng (set ai_token_limit = grant_amount cho tất cả AUTHOR).
    public const string SingletonRuleName = "AUTHOR_AI_TOKEN_SINGLETON";
    private const string PeriodKindMonthlyUtc = "monthly_utc";

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

    public async Task<bool> OnAuthorBecameAuthorAsync(Guid authorUserId, CancellationToken cancellationToken = default)
    {
        if (authorUserId == Guid.Empty) return false;

        var rule = await _db.author_ai_token_auto_grant_rules
            .OrderBy(r => r.created_at_utc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (rule == null) return false;

        var ids = DeserializeSelected(rule.selected_user_ids);
        if (ids.Contains(authorUserId))
            return false; // already tracked

        ids.Add(authorUserId);
        rule.selected_user_ids = ids.Count == 0 ? "[]" : JsonSerializer.Serialize(ids.Distinct().ToList(), JsonOptions);
        rule.updated_at_utc = DateTime.UtcNow;

        // Nếu rule đang bật và có grant_amount thì cấp token ban đầu.
        if (!rule.is_enabled || rule.grant_amount <= 0)
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }

        var user = await _db.users.FirstOrDefaultAsync(u => u.id == authorUserId, cancellationToken).ConfigureAwait(false);
        if (user == null) return false;
        try
        {
            // Không cộng dồn: cấp số dư ban đầu = grant_amount.
            user.ai_token_limit = rule.grant_amount;
        }
        catch (OverflowException)
        {
            user.ai_token_limit = long.MaxValue;
        }
        user.updated_at = DateTime.UtcNow;

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
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
        // Lịch gia hạn kế tiếp: cùng ngày của tháng sau (UTC 00:00).
        // vd chạy ngày 17/4 -> last_run_at_utc = 17/5; đến 17/5 auto chạy -> 17/6...
        var nextRenewal = AddMonthsPreserveDayUtc(UtcStartOfDay(now), 1);

        // Singleton upsert: nếu đã có dòng thì update, không tạo thêm.
        var entity = await _db.author_ai_token_auto_grant_rules
            .OrderBy(r => r.created_at_utc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
        {
            entity = new author_ai_token_auto_grant_rules
            {
                id = Guid.NewGuid(),
                created_at_utc = now,
                selected_user_ids = "[]",
                last_executed_period_key = null,
                last_run_at_utc = nextRenewal,
            };
            _db.author_ai_token_auto_grant_rules.Add(entity);
        }

        entity.is_enabled = request.IsEnabled;
        entity.display_name = SingletonRuleName;
        entity.period_kind = PeriodKindMonthlyUtc;
        entity.grant_limit_field = "lifetime";
        entity.grant_amount = request.GrantAmount;
        entity.apply_to_all_authors = true;
        entity.updated_at_utc = now;
        if (!entity.last_run_at_utc.HasValue) entity.last_run_at_utc = nextRenewal;

        await SyncSelectedUserIdsToAllAuthorsAsync(entity, cancellationToken).ConfigureAwait(false);

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
        entity.display_name = SingletonRuleName;
        entity.period_kind = PeriodKindMonthlyUtc;
        entity.grant_limit_field = "lifetime";
        entity.grant_amount = request.GrantAmount;
        entity.apply_to_all_authors = true;
        // selected_user_ids dùng để track ai đã được cấp token lần đầu khi lên AUTHOR.
        if (string.IsNullOrWhiteSpace(entity.selected_user_ids))
            entity.selected_user_ids = "[]";

        // Nếu chưa có ngày gia hạn, set theo "cùng ngày tháng sau".
        if (!entity.last_run_at_utc.HasValue)
            entity.last_run_at_utc = AddMonthsPreserveDayUtc(UtcStartOfDay(DateTime.UtcNow), 1);

        entity.updated_at_utc = DateTime.UtcNow;
        await SyncSelectedUserIdsToAllAuthorsAsync(entity, cancellationToken).ConfigureAwait(false);
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
            if (await TryExecuteRuleIfDueAsync(rule, cancellationToken).ConfigureAwait(false))
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

        var utcNow = DateTime.UtcNow;
        var n = await ExecuteGrantForRuleAsync(rule, periodKeyForLog: "manual", cancellationToken).ConfigureAwait(false);

        // Sau khi chạy tay: lịch kế tiếp = cùng ngày tháng sau (theo ngày chạy thực tế).
        rule.last_run_at_utc = AddMonthsPreserveDayUtc(UtcStartOfDay(utcNow), 1);
        rule.updated_at_utc = utcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return new AuthorAiTokenAutoGrantRunResultDto { RuleId = rule.id, PeriodKey = "manual", UsersUpdated = n };
    }

    private async Task<bool> TryExecuteRuleIfDueAsync(
        author_ai_token_auto_grant_rules rule,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var due = rule.last_run_at_utc ?? AddMonthsPreserveDayUtc(UtcStartOfDay(utcNow), 1);
        if (utcNow < due)
            return false;

        var n = await ExecuteGrantForRuleAsync(rule, periodKeyForLog: due.ToString("yyyy-MM-dd"), cancellationToken).ConfigureAwait(false);
        rule.last_run_at_utc = AddMonthsPreserveDayUtc(due, 1);
        rule.updated_at_utc = utcNow;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        if (n > 0)
            _logger.LogInformation(
                "Author AI token auto-grant rule {RuleId}: period {PeriodKey}, users updated {N}.",
                rule.id,
                due.ToString("yyyy-MM-dd"),
                n);
        return true;
    }

    private async Task<int> ExecuteGrantForRuleAsync(
        author_ai_token_auto_grant_rules rule,
        string periodKeyForLog,
        CancellationToken cancellationToken)
    {
        // Admin rule: luôn áp dụng cho tất cả AUTHOR.
        var userIds = await _db.users.AsNoTracking()
            .Where(u => (u.role ?? "").ToUpper() == "AUTHOR")
            .Select(u => u.id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (userIds.Count == 0)
        {
            _logger.LogWarning("Author AI token auto-grant rule {RuleId}: no target authors (period {Key}).", rule.id, periodKeyForLog);
            return 0;
        }

        var field = rule.grant_limit_field;
        var amount = rule.grant_amount;
        var updated = 0;

        // Keep selected_user_ids in sync with apply_to_all_authors=true (auditing/UI).
        rule.selected_user_ids = userIds.Count == 0 ? "[]" : JsonSerializer.Serialize(userIds.Distinct().ToList(), JsonOptions);

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
                // Schema mới: không cộng dồn, set số dư = grant_amount.
                u.ai_token_limit = amount;
                u.updated_at = DateTime.UtcNow;
                updated++;
            }
        }

        return updated;
    }

    private static DateTime UtcStartOfDay(DateTime utcNow)
    {
        var d = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc).Date;
        return DateTime.SpecifyKind(d, DateTimeKind.Utc);
    }

    private static DateTime AddMonthsPreserveDayUtc(DateTime utcDate, int months)
    {
        var u = DateTime.SpecifyKind(utcDate, DateTimeKind.Utc);
        var target = u.AddMonths(months);
        var day = Math.Min(u.Day, DateTime.DaysInMonth(target.Year, target.Month));
        return new DateTime(target.Year, target.Month, day, 0, 0, 0, DateTimeKind.Utc);
    }

    // NOTE: Admin auto-grant đã chuyển sang luôn áp dụng cho tất cả AUTHOR.

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

    private async Task SyncSelectedUserIdsToAllAuthorsAsync(author_ai_token_auto_grant_rules rule, CancellationToken cancellationToken)
    {
        if (rule == null) return;
        if (!rule.apply_to_all_authors) return;

        var authorIds = await _db.users.AsNoTracking()
            .Where(u => (u.role ?? "").ToUpper() == "AUTHOR")
            .Select(u => u.id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        rule.selected_user_ids = authorIds.Count == 0 ? "[]" : JsonSerializer.Serialize(authorIds.Distinct().ToList(), JsonOptions);
    }

    private static void ValidateUpsert(AuthorAiTokenAutoGrantRuleUpsertRequest request)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));
        // Admin UI/API: auto monthly + all authors.
        if (request.GrantAmount <= 0)
            throw new ArgumentException("grantAmount phải lớn hơn 0.");
    }

    // NOTE: Admin auto-grant cố định monthly_utc + lifetime.

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
