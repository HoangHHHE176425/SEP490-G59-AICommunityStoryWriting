namespace Services.StoryReporting;

/// <summary>Catalog lý do report bình luận + severity (riêng cho COMMENT).</summary>
public static class CommentReportReasonCatalog
{
    // Dùng chung mã reason với client modal comment (truyền trong reports.reason_category).
    // Điểm severity đồng bộ với StoryReportReasonScores / StoryReportReasonCatalog.
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

    public static int GetSeverityScoreOrDefault(string? code)
    {
        return TryGet(code, out var def) ? def.SeverityScore : GetOther().SeverityScore;
    }

    private static StoryReportReasonDefinition GetOther()
    {
        // Luôn luôn tồn tại trong All
        return ByCode["OTHER"];
    }

    public static string GetDominantReasonLabelVi(string dominantCode)
    {
        return TryGet(dominantCode, out var def) ? def.LabelVi : dominantCode;
    }
}

