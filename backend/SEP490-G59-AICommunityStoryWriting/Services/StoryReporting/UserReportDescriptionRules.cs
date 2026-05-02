namespace Services.StoryReporting;

/// <summary>
/// Quy tắc mô tả khi người dùng gửi báo cáo truyện / bình luận (đồng bộ ý nghĩa với FE: đếm từ theo khoảng trắng).
/// </summary>
public static class UserReportDescriptionRules
{
    public const int MinWords = 50;
    public const int MaxLength = 200;

    public static int CountWords(string trimmedText)
    {
        if (string.IsNullOrEmpty(trimmedText)) return 0;
        return trimmedText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    /// <summary>
    /// Ném <see cref="ArgumentException"/> nếu thiếu mô tả, dưới <see cref="MinWords"/> từ, hoặc vượt <see cref="MaxLength"/> ký tự (sau trim).
    /// </summary>
    public static void ValidateDescription(string? description)
    {
        var trimmed = (description ?? "").Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            throw new ArgumentException("Vui lòng nhập mô tả báo cáo (tối thiểu 50 từ).");

        var wc = CountWords(trimmed);
        if (wc < MinWords)
            throw new ArgumentException($"Mô tả báo cáo cần ít nhất {MinWords} từ (hiện tại: {wc} từ).");

        if (trimmed.Length > MaxLength)
            throw new ArgumentException($"Mô tả báo cáo tối đa {MaxLength} ký tự.");
    }
}
