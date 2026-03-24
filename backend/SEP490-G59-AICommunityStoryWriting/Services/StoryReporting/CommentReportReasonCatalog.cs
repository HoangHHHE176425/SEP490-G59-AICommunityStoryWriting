namespace Services.StoryReporting;

/// <summary>Catalog lý do report bình luận + severity (riêng cho COMMENT).</summary>
public static class CommentReportReasonCatalog
{
    // Dùng chung mã reason với client modal comment (truyền trong reports.reason_category).
    // Severity theo yêu cầu:
    // - Hate speech or discrimination: 80
    // - Harassment or bullying: 70
    // - Violence or threats: 90
    // - Sexual or explicit content: 75
    // - Spam or advertisement: 40
    // - Misinformation: 40
    // - Other: 30
    public static readonly IReadOnlyList<StoryReportReasonDefinition> All = new List<StoryReportReasonDefinition>
    {
        new("HATE_SPEECH", "Hate speech or discrimination", "Phát ngôn thù ghét / phân biệt", "HIGH", 80),
        new("HARASSMENT", "Harassment or bullying", "Quấy rối / bắt nạt", "HIGH", 70),
        new("VIOLENCE_THREATS", "Violence or threats", "Bạo lực / đe dọa", "CRITICAL", 90),
        new("SEXUAL_EXPLICIT", "Sexual or explicit content", "Nội dung tình dục / 18+", "HIGH", 75),
        new("SPAM_AD", "Spam or advertisement", "Spam / quảng cáo", "MEDIUM", 40),
        new("MISINFORMATION", "Misinformation", "Thông tin sai", "MEDIUM", 40),
        new("OTHER", "Other", "Khác", "LOW", 30)
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

