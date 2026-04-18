namespace Services;

/// <summary>Phạm vi giới hạn token AI (đối chiếu <c>ai_usage_logs.created_at</c> theo UTC).</summary>
public enum AuthorAiTokenBudgetPeriodKind
{
    /// <summary>Tổng tích lũy (toàn bộ log).</summary>
    Lifetime,

    /// <summary>Từ 00:00 UTC của ngày hiện tại.</summary>
    PerDayUtc,

    /// <summary>Từ 00:00 UTC Thứ Hai của tuần chứa ngày hiện tại.</summary>
    PerWeekUtc,

    /// <summary>Từ 00:00 UTC ngày 1 của tháng lịch hiện tại.</summary>
    PerMonthUtc
}
