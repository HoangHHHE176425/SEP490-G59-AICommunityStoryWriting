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
        var bal = await _db.users.AsNoTracking()
            .Where(u => u.id == userId)
            .Select(u => (long?)u.ai_token_limit)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (bal == null) return null;

        // DTO cũ: map về số dư hiện tại để không vỡ consumer.
        return new AuthorAiTokenBudgetDto
        {
            TokensUsed = 0,
            TokenLimit = bal.Value,
            TokensRemaining = bal.Value,
            Unlimited = false,

            TokensUsedTodayUtc = 0,
            TokenLimitPerDay = null,
            TokensRemainingPerDay = null,
            UnlimitedPerDay = true,

            TokensUsedThisWeekUtc = 0,
            TokenLimitPerWeek = null,
            TokensRemainingPerWeek = null,
            UnlimitedPerWeek = true,

            TokensUsedThisMonthUtc = 0,
            TokenLimitPerMonth = null,
            TokensRemainingPerMonth = null,
            UnlimitedPerMonth = true
        };
    }

    public async Task EnsureWithinBudgetAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var bal = await _db.users.AsNoTracking()
            .Where(u => u.id == userId)
            .Select(u => (long?)u.ai_token_limit)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
        if (bal == null) return;
        if (bal.Value <= 0)
            throw new AuthorAiTokenBudgetExceededException(0, 0, AuthorAiTokenBudgetPeriodKind.Lifetime);
    }
}
