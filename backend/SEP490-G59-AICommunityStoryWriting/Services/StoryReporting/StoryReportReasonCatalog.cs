using BusinessObjects.StoryReporting;

namespace Services.StoryReporting;

/// <summary>Mã lý do báo cáo truyện (lưu trong reports.reason_category) và điểm mức độ.</summary>
public static class StoryReportReasonCatalog
{
    public const string TargetTypeStory = "STORY";

    public static readonly IReadOnlyList<StoryReportReasonDefinition> All = new List<StoryReportReasonDefinition>
    {
        new("COPYRIGHT", "Copyright violation", "Vi phạm bản quyền", "CRITICAL", 100),
        new("SEXUAL_EXPLICIT", "Sexual or explicit content", "Nội dung tình dục / 18+", "CRITICAL", 90),
        new("VIOLENCE_THREATS", "Violence or threats", "Bạo lực / đe dọa", "CRITICAL", 90),
        new("HARASSMENT", "Harassment or bullying", "Quấy rối / bắt nạt", "HIGH", 80),
        new("MISINFORMATION", "Misinformation", "Thông tin sai", "HIGH", 80),
        new("HATE_SPEECH", "Hate speech or discrimination", "Phát ngôn thù ghét / phân biệt", "HIGH", 70),
        new("SPAM_AD", "Spam or advertisement", "Spam / quảng cáo", "HIGH", 70),
        new("OTHER", "Other", "Khác", "MEDIUM", 60)
    };

    private static readonly Dictionary<string, StoryReportReasonDefinition> ByCode =
        All.ToDictionary(x => x.Code, StringComparer.OrdinalIgnoreCase);

    public static bool TryGet(string? code, out StoryReportReasonDefinition def)
    {
        def = null!;
        if (string.IsNullOrWhiteSpace(code)) return false;
        return ByCode.TryGetValue(code.Trim(), out def!);
    }

    public static int GetSeverityScoreOrDefault(string? code) => StoryReportReasonScores.GetScore(code);

    public static string PickHigherReasonCategory(string? current, string incoming) =>
        StoryReportReasonScores.PickHigherCode(current, incoming);
}

public sealed record StoryReportReasonDefinition(string Code, string LabelEn, string LabelVi, string SeverityLevel, int SeverityScore);
