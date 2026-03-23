using System.Collections.Generic;

namespace Services.StoryReporting;

public static class CommentReportReasonScores
{
    private const string OtherCode = "OTHER";

    /// <summary>
    /// Công thức theo yêu cầu:
    /// SeverityScore = max(dominantReasonSeverity, weightedAverage)
    /// Trong đó dominantReason là lý do có số phiếu lớn nhất; nếu hòa, lấy severity lớn nhất trong nhóm hòa.
    /// </summary>
    public static double ComputeAggregatedSeverity(IReadOnlyDictionary<string, int> reasonCounts)
    {
        if (reasonCounts == null || reasonCounts.Count == 0)
            return GetSeverity(OtherCode);

        var normalized = Normalize(reasonCounts);
        var total = normalized.Values.Sum();
        if (total <= 0)
            return GetSeverity(OtherCode);

        double weightedSum = 0;
        foreach (var (code, cnt) in normalized)
        {
            weightedSum += GetSeverity(code) * cnt;
        }

        var weightedAverage = weightedSum / total;

        var maxCount = normalized.Values.Max();
        var dominantSeverity = normalized
            .Where(kv => kv.Value == maxCount)
            .Select(kv => GetSeverity(kv.Key))
            .DefaultIfEmpty(GetSeverity(OtherCode))
            .Max();

        return Math.Max(dominantSeverity, weightedAverage);
    }

    /// <summary>Trả về dominantCode + severity tổng để dùng cho hiển thị.</summary>
    public static (string DominantCode, double AggregatedSeverity) ComputeDominantAndAggregatedSeverity(
        IReadOnlyDictionary<string, int> reasonCounts)
    {
        if (reasonCounts == null || reasonCounts.Count == 0)
            return (OtherCode, GetSeverity(OtherCode));

        var normalized = Normalize(reasonCounts);
        var total = normalized.Values.Sum();
        if (total <= 0)
            return (OtherCode, GetSeverity(OtherCode));

        var maxCount = normalized.Values.Max();
        var candidates = normalized
            .Where(kv => kv.Value == maxCount)
            .Select(kv => kv.Key)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // dominant code = candidate có severity cao nhất (tie -> ổn định theo code ordinal)
        var dominantCode = candidates
            .OrderByDescending(c => GetSeverity(c))
            .ThenBy(c => c, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault() ?? OtherCode;

        var aggregated = ComputeAggregatedSeverity(normalized);
        return (dominantCode, aggregated);
    }

    private static Dictionary<string, int> Normalize(IReadOnlyDictionary<string, int> reasonCounts)
    {
        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in reasonCounts)
        {
            var code = NormalizeCode(kv.Key);
            var cnt = kv.Value;
            if (cnt <= 0) continue;
            if (dict.TryGetValue(code, out var prev))
                dict[code] = prev + cnt;
            else
                dict[code] = cnt;
        }
        return dict;
    }

    private static string NormalizeCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return OtherCode;
        var t = code.Trim().ToUpperInvariant();
        return CommentReportReasonCatalog.TryGet(t, out _) ? t : OtherCode;
    }

    private static int GetSeverity(string code) => CommentReportReasonCatalog.GetSeverityScoreOrDefault(code);
}

