using System.Text.RegularExpressions;

namespace Services.Helpers;

/// <summary>Phát hiện ngôn ngữ bộ truyện từ mẫu văn bản (title, summary, nội dung chương) để AI viết đúng ngôn ngữ.</summary>
public static class StoryLanguageHelper
{
    /// <summary>Ký tự và tổ hợp tiếng Việt (có dấu, đ). Mẫu văn bản chứa nhiều ký tự này thường là Tiếng Việt.</summary>
    private static readonly Regex VietnameseChars = new Regex(
        @"[\u00C0-\u024F\u1E00-\u1EFFđĐ]",
        RegexOptions.Compiled);

    /// <summary>Phát hiện ngôn ngữ chính từ mẫu văn bản (vd. vài trăm đến vài nghìn ký tự đầu context). Trả về "Vietnamese" hoặc "English".</summary>
    public static string DetectFromStoryContext(string? sampleText)
    {
        if (string.IsNullOrWhiteSpace(sampleText))
            return "English";

        var take = Math.Min(2500, sampleText.Length);
        var sample = sampleText[..take];
        var letterCount = 0;
        var vnCount = 0;
        foreach (var c in sample)
        {
            if (char.IsLetter(c))
            {
                letterCount++;
                if (VietnameseChars.IsMatch(c.ToString()))
                    vnCount++;
            }
        }

        if (letterCount < 10)
            return "English";

        var ratio = (double)vnCount / letterCount;
        return ratio >= 0.08 ? "Vietnamese" : "English";
    }

    /// <summary>Chỉ thị ngôn ngữ đưa vào prompt: bắt buộc AI viết (dàn ý, nội dung, feedback) đúng ngôn ngữ bộ truyện.</summary>
    public static string GetLanguageInstruction(string language)
    {
        return language.Equals("Vietnamese", StringComparison.OrdinalIgnoreCase)
            ? "Ngôn ngữ bộ truyện: Tiếng Việt. Bắt buộc viết toàn bộ (dàn ý, nội dung chương, feedback) bằng Tiếng Việt."
            : "Story language: English. You must write all output (outline, chapter content, feedback) in English.";
    }
}
