namespace BusinessObjects.StoryReporting;

/// <summary>Điểm severity đồng bộ với Services.StoryReporting.StoryReportReasonCatalog.</summary>
public static class StoryReportReasonScores
{
    private static readonly Dictionary<string, int> ByCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["COPYRIGHT"] = 100,
        ["SEXUAL_EXPLICIT"] = 90,
        ["VIOLENCE_THREATS"] = 90,
        ["HARASSMENT"] = 80,
        ["MISINFORMATION"] = 80,
        ["HATE_SPEECH"] = 70,
        ["SPAM_AD"] = 70,
        ["OTHER"] = 60
    };

    public static int GetScore(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return ByCode["OTHER"];
        var k = code.Trim();
        return ByCode.TryGetValue(k, out var s) ? s : ByCode["OTHER"];
    }

    /// <summary>
    /// Điểm severity gộp khi nhiều người chọn lý do khác nhau:
    /// max(severity của lý do có nhiều phiếu nhất, trung bình có trọng số theo số phiếu từng lý do).
    /// Hòa số phiếu → lý do có severity cao hơn được coi là dominant.
    /// </summary>
    public static double ComputeAggregatedSeverity(IReadOnlyDictionary<string, int> reasonCounts)
    {
        if (reasonCounts == null || reasonCounts.Count == 0)
            return GetScore(null);

        var total = 0;
        foreach (var n in reasonCounts.Values)
            total += n;
        if (total <= 0)
            return GetScore(null);

        double weightedSum = 0;
        foreach (var kv in reasonCounts)
        {
            var code = string.IsNullOrWhiteSpace(kv.Key) ? "OTHER" : kv.Key.Trim().ToUpperInvariant();
            weightedSum += GetScore(code) * kv.Value;
        }

        var weightedAvg = weightedSum / total;

        var maxCount = 0;
        foreach (var kv in reasonCounts)
            if (kv.Value > maxCount) maxCount = kv.Value;

        string dominantCode = "OTHER";
        var bestScore = -1;
        foreach (var kv in reasonCounts)
        {
            if (kv.Value != maxCount) continue;
            var code = string.IsNullOrWhiteSpace(kv.Key) ? "OTHER" : kv.Key.Trim().ToUpperInvariant();
            var sc = GetScore(code);
            if (sc > bestScore || (sc == bestScore && string.CompareOrdinal(code, dominantCode) < 0))
            {
                bestScore = sc;
                dominantCode = code;
            }
        }

        var dominantSeverity = GetScore(dominantCode);
        return Math.Max(dominantSeverity, weightedAvg);
    }

    /// <summary>Giữ mã lý do có điểm cao hơn (hòa → giữ current).</summary>
    public static string PickHigherCode(string? current, string incoming)
    {
        var inc = (incoming ?? "").Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(inc)) inc = "OTHER";
        var sInc = GetScore(inc);
        var sCur = GetScore(current);
        if (sInc > sCur) return inc;
        return string.IsNullOrWhiteSpace(current) ? "OTHER" : current.Trim().ToUpperInvariant();
    }
}
