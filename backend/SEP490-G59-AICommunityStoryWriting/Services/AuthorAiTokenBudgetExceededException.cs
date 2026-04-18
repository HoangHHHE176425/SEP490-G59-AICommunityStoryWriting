namespace Services;

/// <summary>Vượt giới hạn token AI do admin đặt trên bảng <c>users</c>.</summary>
public sealed class AuthorAiTokenBudgetExceededException : InvalidOperationException
{
    public long UsedTokens { get; }
    public long LimitTokens { get; }
    public AuthorAiTokenBudgetPeriodKind Period { get; }

    public AuthorAiTokenBudgetExceededException(long usedTokens, long limitTokens, AuthorAiTokenBudgetPeriodKind period)
        : base(BuildMessage(usedTokens, limitTokens, period))
    {
        UsedTokens = usedTokens;
        LimitTokens = limitTokens;
        Period = period;
    }

    private static string BuildMessage(long usedTokens, long limitTokens, AuthorAiTokenBudgetPeriodKind period)
    {
        if (limitTokens <= 0)
            return "Tài khoản bạn đã sử dụng hết token AI. Vui lòng đợi đến kỳ cấp token tiếp theo.";

        var scope = period switch
        {
            AuthorAiTokenBudgetPeriodKind.Lifetime => "tích lũy toàn thời gian",
            AuthorAiTokenBudgetPeriodKind.PerDayUtc => "trong ngày (UTC)",
            AuthorAiTokenBudgetPeriodKind.PerWeekUtc => "trong tuần (UTC, từ Thứ Hai 00:00)",
            AuthorAiTokenBudgetPeriodKind.PerMonthUtc => "trong tháng lịch (UTC)",
            _ => "AI"
        };
        return
            $"Đã đạt giới hạn token ({usedTokens:N0} / {limitTokens:N0}) theo mức {scope}. Vui lòng đợi đến kỳ cấp token tiếp theo.";
    }
}
