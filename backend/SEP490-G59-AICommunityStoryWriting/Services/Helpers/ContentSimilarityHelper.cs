using Microsoft.Extensions.Configuration;

namespace Services.Helpers;

/// <summary>So sánh theo mức trùng chữ: Jaccard trên tập n-gram từ (không dùng embedding).</summary>
public static class ContentSimilarityHelper
{
    /// <summary>Số từ trong mỗi n-gram (word shingle).</summary>
    private const int DefaultWordShingleSize = 5;

    /// <returns>Điểm cao nhất 0–100 (Jaccard × 100) và độ dài chuỗi AI tương ứng.</returns>
    public static Task<(double BestScore, int BestAiLength)> CompareAuthorToAiOutputsAsync(
        string authorContent,
        IReadOnlyList<string?> aiOutputs,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        _ = cancellationToken;
        var n = configuration.GetValue("ChapterCompare:WordShingleSize", DefaultWordShingleSize);
        if (n < 2) n = 2;
        if (n > 12) n = 12;

        var trimmedAuthor = (authorContent ?? "").Trim();
        double bestScore = 0;
        int bestAiLength = 0;

        foreach (var raw in aiOutputs)
        {
            var aiContent = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(aiContent)) continue;
            var s = WordShingleJaccardPercent(trimmedAuthor, aiContent, n);
            if (s > bestScore) { bestScore = s; bestAiLength = aiContent.Length; }
        }

        return Task.FromResult((bestScore, bestAiLength));
    }

    /// <summary>
    /// Jaccard trên tập n-gram từ: |S(A) ∩ S(B)| / |S(A) ∪ S(B)|, nhân 100.
    /// Nếu một bên quá ngắn (&lt; n từ), dùng n' = min(n, lenA, lenB) tối thiểu 1 để vẫn có thể so.
    /// </summary>
    private static double WordShingleJaccardPercent(string authorText, string aiText, int n)
    {
        var aTokens = Tokenize(authorText);
        var bTokens = Tokenize(aiText);
        if (aTokens.Length == 0 || bTokens.Length == 0) return 0;

        var effectiveN = Math.Min(n, Math.Min(aTokens.Length, bTokens.Length));
        effectiveN = Math.Max(1, effectiveN);

        var setA = BuildWordShingleSet(aTokens, effectiveN);
        var setB = BuildWordShingleSet(bTokens, effectiveN);
        if (setA.Count == 0 || setB.Count == 0) return 0;

        var intersection = 0;
        foreach (var x in setA)
        {
            if (setB.Contains(x))
                intersection++;
        }

        var union = setA.Count + setB.Count - intersection;
        if (union <= 0) return 0;
        return Math.Clamp(intersection * 100.0 / union, 0, 100);
    }

    private static HashSet<string> BuildWordShingleSet(string[] tokens, int n)
    {
        var set = new HashSet<string>(StringComparer.Ordinal);
        if (tokens.Length < n) return set;
        for (int i = 0; i <= tokens.Length - n; i++)
            set.Add(string.Join(' ', tokens, i, n));
        return set;
    }

    private static string[] Tokenize(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return Array.Empty<string>();
        var normalized = NormalizeText(text);
        return normalized
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 0)
            .ToArray();
    }

    private static string NormalizeText(string text)
    {
        var chars = text.ToLowerInvariant()
            .Select(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch) ? ch : ' ')
            .ToArray();
        var compact = new string(chars);
        var parts = compact.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        return string.Join(' ', parts);
    }
}
