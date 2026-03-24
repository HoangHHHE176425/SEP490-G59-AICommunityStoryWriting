namespace Services.StoryReporting;

public static class StoryReportPriorityCalculator
{
    /// <summary>PriorityScore = SeverityScore + (ReportCount × 5) + TimeWeight</summary>
    public static double ComputePriorityScore(double severityScore, int reportCount, int timeWeight) =>
        severityScore + reportCount * 5 + timeWeight;

    /// <summary>
    /// Trọng số theo thời gian từ báo cáo đầu tiên (oldest pending) đến hiện tại (UTC).
    /// Bổ sung: &gt;= 48h → 20 để report quá hạn vẫn được đẩy lên.
    /// </summary>
    public static int ComputeTimeWeight(DateTime oldestReportUtc, DateTime nowUtc)
    {
        if (oldestReportUtc.Kind == DateTimeKind.Unspecified)
            oldestReportUtc = DateTime.SpecifyKind(oldestReportUtc, DateTimeKind.Utc);
        if (nowUtc.Kind == DateTimeKind.Unspecified)
            nowUtc = DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc);

        var age = nowUtc - oldestReportUtc;
        if (age < TimeSpan.Zero) age = TimeSpan.Zero;

        if (age < TimeSpan.FromHours(1)) return 0;
        if (age < TimeSpan.FromHours(6)) return 5;
        if (age < TimeSpan.FromHours(24)) return 10;
        if (age < TimeSpan.FromHours(48)) return 15;
        return 20;
    }
}
