using BusinessObjects.StoryReporting;

namespace Services.StoryReporting;

/// <summary>Mã lý do báo cáo truyện (lưu trong reports.reason_category) và điểm mức độ.</summary>
public static class StoryReportReasonCatalog
{
    public const string TargetTypeStory = "STORY";

    public static readonly IReadOnlyList<StoryReportReasonDefinition> All = new List<StoryReportReasonDefinition>
    {
        new("COPYRIGHT", "Copyright violation", "Xâm phạm bản quyền nội dung", "CRITICAL", 100),
        new("SEXUAL_EXPLICIT", "Sexual or explicit content", "Nội dung khiêu dâm / 18+", "CRITICAL", 90),
        new("VIOLENCE_THREATS", "Violence or threats", "Nội dung bạo lực hoặc đe dọa", "CRITICAL", 90),
        new("HARASSMENT", "Harassment or bullying", "Quấy rối hoặc bắt nạt", "HIGH", 80),
        new("MISINFORMATION", "Misinformation", "Thông tin sai sự thật / gây hiểu lầm", "HIGH", 80),
        new("HATE_SPEECH", "Hate speech or discrimination", "Ngôn từ thù ghét / phân biệt đối xử", "HIGH", 70),
        new("SPAM_AD", "Spam or advertisement", "Spam / quảng cáo trái phép", "HIGH", 70),
        new("OTHER", "Other", "Lý do khác", "MEDIUM", 60)
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
