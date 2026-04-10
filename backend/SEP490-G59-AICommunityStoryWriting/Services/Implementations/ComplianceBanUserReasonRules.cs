using System.Text.RegularExpressions;

namespace Services.Implementations;

/// <summary>Quy tắc nội dung «Lý do đề xuất» khi compliance gửi yêu cầu chặn tài khoản (BAN_USER).</summary>
internal static class ComplianceBanUserReasonRules
{
    public const int MinWords = 50;

    public static int CountWords(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return Regex.Matches(text.Trim(), @"\S+").Count;
    }

    public static void EnsureBanUserMessageOrThrow(string? message)
    {
        var n = CountWords(message);
        if (n < MinWords)
            throw new ArgumentException($"Lý do đề xuất cần tối thiểu {MinWords} từ (hiện có {n} từ).");
    }
}
