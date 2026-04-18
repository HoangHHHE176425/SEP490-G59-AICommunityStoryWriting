namespace Services.DTOs.Admin;

/// <summary>Token đã dùng (bảng <c>ai_usage_logs</c>) so với các giới hạn admin trên <c>users</c>. Ngày/tuần/tháng theo UTC.</summary>
public sealed class AuthorAiTokenBudgetDto
{
    public long TokensUsed { get; init; }
    public long? TokenLimit { get; init; }
    public long? TokensRemaining { get; init; }
    public bool Unlimited { get; init; }

    public long TokensUsedTodayUtc { get; init; }
    public long? TokenLimitPerDay { get; init; }
    public long? TokensRemainingPerDay { get; init; }
    public bool UnlimitedPerDay { get; init; }

    public long TokensUsedThisWeekUtc { get; init; }
    public long? TokenLimitPerWeek { get; init; }
    public long? TokensRemainingPerWeek { get; init; }
    public bool UnlimitedPerWeek { get; init; }

    public long TokensUsedThisMonthUtc { get; init; }
    public long? TokenLimitPerMonth { get; init; }
    public long? TokensRemainingPerMonth { get; init; }
    public bool UnlimitedPerMonth { get; init; }
}
