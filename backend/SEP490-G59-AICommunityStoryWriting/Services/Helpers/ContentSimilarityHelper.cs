using Microsoft.Extensions.Configuration;

namespace Services.Helpers;

/// <summary>So sánh một văn bản tác giả với nhiều bản AI (embedding cosine hoặc Jaccard từ).</summary>
public static class ContentSimilarityHelper
{
    /// <returns>Điểm cao nhất 0–100 và độ dài chuỗi AI tương ứng.</returns>
    public static async Task<(double BestScore, int BestAiLength)> CompareAuthorToAiOutputsAsync(
        string authorContent,
        IReadOnlyList<string?> aiOutputs,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var trimmedAuthor = (authorContent ?? "").Trim();
        double bestScore = 0;
        int bestAiLength = 0;
        var config = EmbeddingHelper.GetEmbeddingConfig(configuration);

        if (config.HasValue)
        {
            var (baseUrl, apiKey, model) = config.Value;
            var embAuthor = await EmbeddingHelper.GetEmbeddingAsync(trimmedAuthor, baseUrl, apiKey, model, cancellationToken);
            if (embAuthor.Length == 0)
            {
                foreach (var raw in aiOutputs)
                {
                    var aiContent = (raw ?? "").Trim();
                    if (string.IsNullOrEmpty(aiContent)) continue;
                    var s = TextSimilarityPercent(trimmedAuthor, aiContent);
                    if (s > bestScore) { bestScore = s; bestAiLength = aiContent.Length; }
                }
            }
            else
            {
                foreach (var raw in aiOutputs)
                {
                    var aiContent = (raw ?? "").Trim();
                    if (string.IsNullOrEmpty(aiContent)) continue;
                    var embAi = await EmbeddingHelper.GetEmbeddingAsync(aiContent, baseUrl, apiKey, model, cancellationToken);
                    var s = embAi.Length > 0 ? CosineSimilarityPercent(embAuthor, embAi) : TextSimilarityPercent(trimmedAuthor, aiContent);
                    if (s > bestScore) { bestScore = s; bestAiLength = aiContent.Length; }
                }
            }
        }
        else
        {
            foreach (var raw in aiOutputs)
            {
                var aiContent = (raw ?? "").Trim();
                if (string.IsNullOrEmpty(aiContent)) continue;
                var s = TextSimilarityPercent(trimmedAuthor, aiContent);
                if (s > bestScore) { bestScore = s; bestAiLength = aiContent.Length; }
            }
        }

        return (bestScore, bestAiLength);
    }

    private static double CosineSimilarityPercent(float[] a, float[] b)
    {
        if (a.Length == 0 || a.Length != b.Length) return 0;
        double dot = 0, na = 0, nb = 0;
        for (int i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            na += a[i] * a[i];
            nb += b[i] * b[i];
        }
        var denom = Math.Sqrt(na) * Math.Sqrt(nb);
        if (denom <= 0) return 0;
        var cos = dot / denom;
        return Math.Clamp(cos, -1, 1) * 100.0;
    }

    private static double TextSimilarityPercent(string a, string b)
    {
        var setA = new HashSet<string>(SplitWords(a), StringComparer.OrdinalIgnoreCase);
        var setB = new HashSet<string>(SplitWords(b), StringComparer.OrdinalIgnoreCase);
        if (setA.Count == 0 && setB.Count == 0) return 100;
        if (setA.Count == 0 || setB.Count == 0) return 0;
        int intersection = setA.Count(s => setB.Contains(s));
        int union = setA.Count + setB.Count - intersection;
        if (union == 0) return 100;
        return (intersection * 100.0) / union;
    }

    private static IEnumerable<string> SplitWords(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) yield break;
        foreach (var w in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var t = w.Trim();
            if (t.Length > 0) yield return t;
        }
    }
}
