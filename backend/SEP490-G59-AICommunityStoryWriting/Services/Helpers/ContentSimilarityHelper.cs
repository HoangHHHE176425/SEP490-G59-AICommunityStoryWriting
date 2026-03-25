using Microsoft.Extensions.Configuration;

namespace Services.Helpers;

/// <summary>So sánh theo % copy chữ (text-only), không dùng semantic embedding.</summary>
public static class ContentSimilarityHelper
{
    private const int NGramSize = 5;
    private const int LongSpanMinWords = 12;

    /// <returns>Điểm cao nhất 0–100 và độ dài chuỗi AI tương ứng.</returns>
    public static Task<(double BestScore, int BestAiLength)> CompareAuthorToAiOutputsAsync(
        string authorContent,
        IReadOnlyList<string?> aiOutputs,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        _ = configuration;
        _ = cancellationToken;
        var trimmedAuthor = (authorContent ?? "").Trim();
        double bestScore = 0;
        int bestAiLength = 0;

        foreach (var raw in aiOutputs)
        {
            var aiContent = (raw ?? "").Trim();
            if (string.IsNullOrEmpty(aiContent)) continue;
            var s = CopySimilarityPercent(trimmedAuthor, aiContent);
            if (s > bestScore) { bestScore = s; bestAiLength = aiContent.Length; }
        }

        return Task.FromResult((bestScore, bestAiLength));
    }

    /// <summary>
    /// Copy score = 0.7 * N-gram overlap + 0.3 * long matching spans.
    /// N-gram bắt copy cụm từ; long span bắt copy đoạn dài liên tiếp.
    /// </summary>
    private static double CopySimilarityPercent(string authorText, string aiText)
    {
        var authorTokens = Tokenize(authorText);
        var aiTokens = Tokenize(aiText);
        if (authorTokens.Length == 0 || aiTokens.Length == 0) return 0;

        var ngram = NGramOverlapPercent(authorTokens, aiTokens, NGramSize);
        var span = LongSpanPercent(authorTokens, aiTokens, LongSpanMinWords);
        var score = 0.7 * ngram + 0.3 * span;
        return Math.Clamp(score, 0, 100);
    }

    private static double NGramOverlapPercent(string[] authorTokens, string[] aiTokens, int n)
    {
        if (authorTokens.Length < n || aiTokens.Length < n) return 0;
        var aiNgrams = new HashSet<string>(StringComparer.Ordinal);
        for (int i = 0; i <= aiTokens.Length - n; i++)
            aiNgrams.Add(string.Join(' ', aiTokens, i, n));

        int total = authorTokens.Length - n + 1;
        int matched = 0;
        for (int i = 0; i <= authorTokens.Length - n; i++)
        {
            var gram = string.Join(' ', authorTokens, i, n);
            if (aiNgrams.Contains(gram)) matched++;
        }
        return total <= 0 ? 0 : (matched * 100.0) / total;
    }

    private static double LongSpanPercent(string[] authorTokens, string[] aiTokens, int minSpanWords)
    {
        int n = authorTokens.Length;
        int m = aiTokens.Length;
        if (n == 0 || m == 0) return 0;

        var dp = new int[n + 1, m + 1];
        var covered = new bool[n];

        for (int i = 1; i <= n; i++)
        {
            for (int j = 1; j <= m; j++)
            {
                if (authorTokens[i - 1].Equals(aiTokens[j - 1], StringComparison.Ordinal))
                {
                    dp[i, j] = dp[i - 1, j - 1] + 1;
                    var len = dp[i, j];
                    if (len >= minSpanWords)
                    {
                        var start = i - len;
                        var end = i - 1;
                        for (int k = start; k <= end; k++)
                            covered[k] = true;
                    }
                }
                else
                {
                    dp[i, j] = 0;
                }
            }
        }

        int coveredCount = covered.Count(x => x);
        return (coveredCount * 100.0) / n;
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
