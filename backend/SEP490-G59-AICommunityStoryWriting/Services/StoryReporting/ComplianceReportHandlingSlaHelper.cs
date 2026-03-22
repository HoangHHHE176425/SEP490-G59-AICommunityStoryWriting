namespace Services.StoryReporting;

/// <summary>Mức cảnh báo theo thời gian đã trôi kể từ lúc nhận lock (không dùng hạn xử lý).</summary>
public static class ComplianceReportHandlingSlaHelper
{
    public const string TierOk = "OK";
    public const string TierNotice = "NOTICE";
    public const string TierWarning = "WARNING";
    public const string TierSevere = "SEVERE";
    public const string TierCritical = "CRITICAL";

    /// <summary>Chỉ dựa trên số giờ từ <paramref name="claimedAtUtc"/> tới <paramref name="nowUtc"/>.</summary>
    public static (string Status, string? MessageVi, double HoursSinceClaim) Compute(DateTime claimedAtUtc, DateTime nowUtc)
    {
        var claimed = ToUtc(claimedAtUtc);
        nowUtc = ToUtc(nowUtc);
        var age = nowUtc - claimed;
        var hours = age.TotalHours;

        if (hours >= 48)
            return (TierCritical, "Đã nhận lock 48 giờ trở lên — ưu tiên xử lý hoặc liên hệ admin.", hours);
        if (hours >= 24)
            return (TierSevere, "Đã nhận lock từ 24 giờ — nên xử lý sớm.", hours);
        if (hours >= 12)
            return (TierWarning, "Đã nhận lock từ 12 giờ.", hours);
        if (hours >= 6)
            return (TierNotice, "Đã nhận lock từ 6 giờ.", hours);

        return (TierOk, null, hours);
    }

    private static DateTime ToUtc(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Unspecified)
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime() : dt;
    }
}
