namespace Services.Helpers;

/// <summary>Trích cả câu (hoặc tới ngắt đoạn) chứa một đoạn khớp — dùng chung chính tả và từ cấm.</summary>
public static class SentenceContextExtractor
{
    public static string? TryExtractSentenceContainingNeedle(string text, string needle)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(needle)) return null;
        needle = needle.Trim();
        if (needle.Length == 0) return null;

        var idx = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        return TryExtractSentenceContainingNeedleAt(text, needle, idx);
    }

    public static string? TryExtractSentenceContainingNeedleAt(string text, string needle, int matchIndex)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(needle)) return null;
        needle = needle.Trim();
        if (needle.Length == 0 || matchIndex < 0 || matchIndex + needle.Length > text.Length)
            return null;
        if (string.Compare(text, matchIndex, needle, 0, needle.Length, StringComparison.OrdinalIgnoreCase) != 0)
            return null;

        return TryExtractSentenceAtSpan(text, matchIndex, needle.Length);
    }

    /// <summary>Trích câu chứa đoạn [matchIndex, matchIndex + matchLength) (không kiểm tra nội dung ô).</summary>
    public static string? TryExtractSentenceAtSpan(string text, int matchIndex, int matchLength)
    {
        if (string.IsNullOrWhiteSpace(text) || matchIndex < 0 || matchLength <= 0 || matchIndex + matchLength > text.Length)
            return null;

        var start = FindSentenceStartIndex(text, matchIndex);
        var end = FindSentenceEndExclusive(text, matchIndex + matchLength);
        if (end <= start) return null;
        var s = text[start..end].Trim();
        return s.Length == 0 ? null : s;
    }

    public static string? TryShortContextSnippetAt(string text, int matchIndex, int matchLength, int contextChars = 24)
    {
        if (string.IsNullOrWhiteSpace(text) || matchIndex < 0 || matchLength <= 0 || matchIndex + matchLength > text.Length)
            return null;

        var start = Math.Max(0, matchIndex - contextChars);
        var end = Math.Min(text.Length, matchIndex + matchLength + contextChars);
        var snippet = text[start..end].Trim();
        if (snippet.Length == 0) return null;
        return (start > 0 ? "..." : "") + snippet + (end < text.Length ? "..." : "");
    }

    public static string? TryExtractContextSnippetAt(string text, string needle, int matchIndex)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(needle)) return null;
        needle = needle.Trim();
        if (needle.Length == 0 || matchIndex < 0 || matchIndex + needle.Length > text.Length)
            return null;
        if (string.Compare(text, matchIndex, needle, 0, needle.Length, StringComparison.OrdinalIgnoreCase) != 0)
            return null;

        return TryShortContextSnippetAt(text, matchIndex, needle.Length, 24);
    }

    public static string? TryShortContextSnippetContainingNeedle(string text, string needle)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(needle)) return null;
        needle = needle.Trim();
        if (needle.Length == 0) return null;
        var idx = text.IndexOf(needle, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return null;
        return TryExtractContextSnippetAt(text, needle, idx);
    }

    private static int FindSentenceStartIndex(string text, int matchStart)
    {
        if (matchStart <= 0) return 0;
        for (var i = matchStart - 1; i >= 0; i--)
        {
            if (IsParagraphBreakAt(text, i))
                return SkipLeadingSentenceWhitespace(text, i + 1, matchStart);
            if (IsSentenceTerminatorAt(text, i))
                return SkipLeadingSentenceWhitespace(text, i + 1, matchStart);
        }

        return 0;
    }

    private static int SkipLeadingSentenceWhitespace(string text, int from, int matchStart)
    {
        var s = from;
        while (s < matchStart && char.IsWhiteSpace(text[s]))
            s++;
        return s;
    }

    private static int FindSentenceEndExclusive(string text, int searchFrom)
    {
        var i = searchFrom;
        while (i < text.Length)
        {
            if (IsParagraphBreakBefore(text, i))
                return i;

            if (text[i] == '.' && i + 2 < text.Length && text[i + 1] == '.' && text[i + 2] == '.')
            {
                var end = i + 3;
                end = AppendClosingQuotes(text, end);
                return end;
            }

            if (IsSentenceTerminatorAt(text, i))
            {
                var end = i + 1;
                end = AppendClosingQuotes(text, end);
                return end;
            }

            i++;
        }

        return text.Length;
    }

    private static int AppendClosingQuotes(string text, int end)
    {
        while (end < text.Length && IsClosingQuoteChar(text[end]))
            end++;
        return end;
    }

    private static bool IsClosingQuoteChar(char c)
        => c is '"' or '\'' or '\u00BB' or '\u201D' or '\u2019';

    private static bool IsParagraphBreakBefore(string text, int i)
    {
        if (i + 1 < text.Length && text[i] == '\n' && text[i + 1] == '\n')
            return true;
        return i + 3 < text.Length
               && text[i] == '\r' && text[i + 1] == '\n'
               && text[i + 2] == '\r' && text[i + 3] == '\n';
    }

    private static bool IsParagraphBreakAt(string text, int i)
    {
        if (i > 0 && text[i - 1] == '\n' && text[i] == '\n')
            return true;
        return i >= 3
               && text[i - 3] == '\r' && text[i - 2] == '\n'
               && text[i - 1] == '\r' && text[i] == '\n';
    }

    private static bool IsSentenceTerminatorAt(string text, int i)
    {
        var c = text[i];
        if (c == '!' || c == '?' || c == '\u2026')
            return true;
        if (c != '.')
            return false;
        if (i > 0 && char.IsDigit(text[i - 1]) && i + 1 < text.Length && char.IsDigit(text[i + 1]))
            return false;
        return true;
    }
}
