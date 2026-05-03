using System.Net;
using System.Text.RegularExpressions;

namespace Services.Helpers;

/// <summary>Chuẩn hóa nội dung chương trước khi đưa cho AI: bỏ markup/thuộc tính rác và trả về plain text theo đoạn.</summary>
public static class ChapterContentNormalizer
{
    public static string NormalizeForAi(string? rawHtmlOrText, int maxChars)
    {
        if (string.IsNullOrWhiteSpace(rawHtmlOrText))
            return string.Empty;

        var s = rawHtmlOrText.Trim();

        // Bỏ toàn bộ block script/style nếu có.
        s = Regex.Replace(s, @"<script\b[^>]*>[\s\S]*?</script>", " ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<style\b[^>]*>[\s\S]*?</style>", " ", RegexOptions.IgnoreCase);

        // Quy đổi các tag xuống dòng thành ngắt đoạn để giữ nhịp đọc.
        s = Regex.Replace(s, @"<\s*br\s*/?\s*>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*/\s*p\s*>", "\n\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*p\b[^>]*>", string.Empty, RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*/\s*div\s*>", "\n\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*div\b[^>]*>", string.Empty, RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*/\s*li\s*>", "\n", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*li\b[^>]*>", "- ", RegexOptions.IgnoreCase);

        // Xóa toàn bộ tag HTML còn lại, giữ lại nội dung text.
        s = Regex.Replace(s, @"<[^>]+>", " ");

        // Decode HTML entities (&nbsp;, &amp;...).
        s = WebUtility.HtmlDecode(s);
        s = s.Replace('\u00A0', ' ');

        // Chuẩn hóa khoảng trắng/ngắt dòng.
        s = Regex.Replace(s, @"[ \t\f\v]+", " ");
        s = Regex.Replace(s, @"\r\n?", "\n");
        s = Regex.Replace(s, @"\n{3,}", "\n\n");
        s = Regex.Replace(s, @"[ ]+\n", "\n");
        s = Regex.Replace(s, @"\n[ ]+", "\n");
        s = s.Trim();

        if (maxChars > 0 && s.Length > maxChars)
            s = s[..maxChars] + "\n[... nội dung bị cắt cho phân tích ...]";

        return s;
    }

    /// <summary>
    /// Plain text chỉ để đếm từ: gỡ HTML, decode entity.
    /// Không dùng <see cref="NormalizeForAi"/> (tránh chèn «- » trước &lt;li&gt; — làm tăng số từ so với màn đọc/editor dùng DOM).
    /// </summary>
    public static string PlainTextForWordCount(string? rawHtmlOrText)
    {
        if (string.IsNullOrWhiteSpace(rawHtmlOrText))
            return string.Empty;

        var s = rawHtmlOrText.Trim();

        s = Regex.Replace(s, @"<script\b[^>]*>[\s\S]*?</script>", " ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<style\b[^>]*>[\s\S]*?</style>", " ", RegexOptions.IgnoreCase);

        s = Regex.Replace(s, @"<\s*br\s*/?\s*>", " ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*/\s*p\s*>", " ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*p\b[^>]*>", " ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*/\s*div\s*>", " ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*div\b[^>]*>", " ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*/\s*li\s*>", " ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"<\s*li\b[^>]*>", " ", RegexOptions.IgnoreCase);

        s = Regex.Replace(s, @"<[^>]+>", " ");

        s = WebUtility.HtmlDecode(s);
        s = s.Replace('\u00A0', ' ');

        s = Regex.Replace(s, @"\s+", " ");
        return s.Trim();
    }

    /// <summary>Đếm «từ» = cụm tách bằng khoảng trắng trên plain text; khớp quy ước editor/reader (không đếm tiền tố bullet từ &lt;li&gt;).</summary>
    public static int CountWords(string? rawHtmlOrText)
    {
        var plain = PlainTextForWordCount(rawHtmlOrText);
        if (string.IsNullOrWhiteSpace(plain))
            return 0;
        return plain.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
