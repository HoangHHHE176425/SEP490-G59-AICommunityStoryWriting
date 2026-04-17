using BusinessObjects;
using Microsoft.EntityFrameworkCore;
using Services;
using Services.DTOs.Admin;
using Services.Interfaces;

namespace Services.Implementations;

public sealed class AuthorAiTokenBudgetService : IAuthorAiTokenBudgetService
{
    private readonly StoryPlatformDbContext _db;

    public AuthorAiTokenBudgetService(StoryPlatformDbContext db)
    {
        _db = db;
    }

    public async Task<AuthorAiTokenBudgetDto?> GetBudgetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var row = await _db.users.AsNoTracking()
            .Where(u => u.id == userId)
            .Select(u => new
            {
                u.author_ai_token_limit,
                u.author_ai_token_limit_per_day,
                u.author_ai_token_limit_per_week,
                u.author_ai_token_limit_per_month
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (row == null)
            return null;

        var now = DateTime.UtcNow;
        var dayStart = UtcStartOfDay(now);
        var weekStart = UtcStartOfWeekMonday(now);
        var monthStart = UtcStartOfMonth(now);

        var usedLifetime = await SumTokensUsedAsync(userId, fromUtcInclusive: null, cancellationToken).ConfigureAwait(false);
        var usedDay = await SumTokensUsedAsync(userId, dayStart, cancellationToken).ConfigureAwait(false);
        var usedWeek = await SumTokensUsedAsync(userId, weekStart, cancellationToken).ConfigureAwait(false);
        var usedMonth = await SumTokensUsedAsync(userId, monthStart, cancellationToken).ConfigureAwait(false);

        var limit = row.author_ai_token_limit;
        long? remainingLifetime = limit.HasValue ? Math.Max(0L, limit.Value - usedLifetime) : null;

        var ld = row.author_ai_token_limit_per_day;
        long? remDay = ld.HasValue ? Math.Max(0L, ld.Value - usedDay) : null;

        var lw = row.author_ai_token_limit_per_week;
        long? remWeek = lw.HasValue ? Math.Max(0L, lw.Value - usedWeek) : null;

        var lm = row.author_ai_token_limit_per_month;
        long? remMonth = lm.HasValue ? Math.Max(0L, lm.Value - usedMonth) : null;

        return new AuthorAiTokenBudgetDto
        {
            TokensUsed = usedLifetime,
            TokenLimit = limit,
            TokensRemaining = remainingLifetime,
            Unlimited = !limit.HasValue,

            TokensUsedTodayUtc = usedDay,
            TokenLimitPerDay = ld,
            TokensRemainingPerDay = remDay,
            UnlimitedPerDay = !ld.HasValue,

            TokensUsedThisWeekUtc = usedWeek,
            TokenLimitPerWeek = lw,
            TokensRemainingPerWeek = remWeek,
            UnlimitedPerWeek = !lw.HasValue,

            TokensUsedThisMonthUtc = usedMonth,
            TokenLimitPerMonth = lm,
            TokensRemainingPerMonth = remMonth,
            UnlimitedPerMonth = !lm.HasValue
        };
    }

    public async Task EnsureWithinBudgetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var row = await _db.users.AsNoTracking()
            .Where(u => u.id == userId)
            .Select(u => new
            {
                u.author_ai_token_limit,
                u.author_ai_token_limit_per_day,
                u.author_ai_token_limit_per_week,
                u.author_ai_token_limit_per_month
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (row == null)
            return;

        var now = DateTime.UtcNow;
        var dayStart = UtcStartOfDay(now);
        var weekStart = UtcStartOfWeekMonday(now);
        var monthStart = UtcStartOfMonth(now);

        if (row.author_ai_token_limit_per_day is { } limDay)
        {
            var used = await SumTokensUsedAsync(userId, dayStart, cancellationToken).ConfigureAwait(false);
            if (used >= limDay)
                throw new AuthorAiTokenBudgetExceededException(used, limDay, AuthorAiTokenBudgetPeriodKind.PerDayUtc);
        }

        if (row.author_ai_token_limit_per_week is { } limWeek)
        {
            var used = await SumTokensUsedAsync(userId, weekStart, cancellationToken).ConfigureAwait(false);
            if (used >= limWeek)
                throw new AuthorAiTokenBudgetExceededException(used, limWeek, AuthorAiTokenBudgetPeriodKind.PerWeekUtc);
        }

        if (row.author_ai_token_limit_per_month is { } limMonth)
        {
            var used = await SumTokensUsedAsync(userId, monthStart, cancellationToken).ConfigureAwait(false);
            if (used >= limMonth)
                throw new AuthorAiTokenBudgetExceededException(used, limMonth, AuthorAiTokenBudgetPeriodKind.PerMonthUtc);
        }

        if (row.author_ai_token_limit is { } limLife)
        {
            var used = await SumTokensUsedAsync(userId, fromUtcInclusive: null, cancellationToken).ConfigureAwait(false);
            if (used >= limLife)
                throw new AuthorAiTokenBudgetExceededException(used, limLife, AuthorAiTokenBudgetPeriodKind.Lifetime);
        }
    }

    private static DateTime UtcStartOfDay(DateTime utcNow)
    {
        var d = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc).Date;
        return DateTime.SpecifyKind(d, DateTimeKind.Utc);
    }

    private static DateTime UtcStartOfMonth(DateTime utcNow)
    {
        var u = DateTime.SpecifyKind(utcNow, DateTimeKind.Utc);
        return new DateTime(u.Year, u.Month, 1, 0, 0, 0, DateTimeKind.Utc);
    }

    /// <summary>Tuần bắt đầu Thứ Hai 00:00 UTC (ISO week).</summary>
    private static DateTime UtcStartOfWeekMonday(DateTime utcNow)
    {
        var day = UtcStartOfDay(utcNow);
        var dow = (int)day.DayOfWeek;
        var diff = (dow - (int)DayOfWeek.Monday + 7) % 7;
        return day.AddDays(-diff);
    }

    private async Task<long> SumTokensUsedAsync(Guid userId, DateTime? fromUtcInclusive, CancellationToken cancellationToken)
    {
        var q = _db.ai_usage_logs.AsNoTracking().Where(x => x.user_id == userId);
        if (fromUtcInclusive.HasValue)
        {
            var from = fromUtcInclusive.Value;
            q = q.Where(x => x.created_at != null && x.created_at >= from);
        }

        var sum = await q.SumAsync(x => (long?)(x.total_tokens ?? 0), cancellationToken).ConfigureAwait(false);
        return sum ?? 0L;
    }
}
