namespace Services;

/// <summary>Chuẩn hóa mức độ escalation (moderator + compliance): STANDARD &lt; HIGH &lt; CRITICAL.</summary>
public static class EscalationUrgencyHelper
{
    public const string Standard = "STANDARD";
    public const string High = "HIGH";
    public const string Critical = "CRITICAL";

    public static int TierRank(string? tier)
    {
        var t = Normalize(tier);
        return t switch
        {
            Critical => 3,
            High => 2,
            _ => 1
        };
    }

    public static string Normalize(string? tier)
    {
        var t = (tier ?? Standard).Trim().ToUpperInvariant();
        if (t == Critical) return Critical;
        if (t == High) return High;
        return Standard;
    }

    /// <summary>UI / admin chỉ còn 2 mức: CRITICAL và STANDARD — HIGH gộp vào STANDARD.</summary>
    public static string ToDisplayTier(string? tier) =>
        Normalize(tier) == Critical ? Critical : Standard;

    /// <summary>Lấy mức cao nhất trong danh sách (theo loại đơn, thời gian chờ, v.v.).</summary>
    public static string Merge(params string?[] tiers)
    {
        string best = Standard;
        var r = 1;
        foreach (var x in tiers)
        {
            var tr = TierRank(x);
            if (tr > r)
            {
                r = tr;
                best = Normalize(x);
            }
        }
        return best;
    }

    /// <summary>Quá 48h → CRITICAL; quá 24h → HIGH (giống một phần logic moderator escalation).</summary>
    public static string ComputeFromRequestAge(DateTime createdAtUtc, DateTime nowUtc)
    {
        var c = createdAtUtc.Kind == DateTimeKind.Utc ? createdAtUtc : createdAtUtc.ToUniversalTime();
        var n = nowUtc.Kind == DateTimeKind.Utc ? nowUtc : nowUtc.ToUniversalTime();
        if ((n - c).TotalHours > 48)
            return Critical;
        if ((n - c).TotalHours > 24)
            return High;
        return Standard;
    }

    /// <summary>Moderator: RELEASE_ASSIGNMENT → CRITICAL; EXTEND_DEADLINE → STANDARD (có thể tăng theo hạn / thời gian).</summary>
    public static string TierForModeratorRequestKind(string? requestKind)
    {
        var k = (requestKind ?? "").Trim().ToUpperInvariant();
        if (k == "RELEASE_ASSIGNMENT") return Critical;
        return Standard;
    }

    /// <summary>Compliance gửi đơn gỡ / giao lại lock báo cáo truyện.</summary>
    public static string TierForComplianceLockReleaseRequest() => High;

    /// <summary>Đơn xử lý tài khoản của compliance đều xếp CRITICAL (BAN/SUSPEND).</summary>
    public static string TierForComplianceAdminActionKind(string? requestKind)
    {
        var k = (requestKind ?? "").Trim().ToUpperInvariant();
        if (k == "BAN_USER") return Critical;
        if (k == "SUSPEND_AUTHOR_WRITING") return Critical;
        return Standard;
    }
}
