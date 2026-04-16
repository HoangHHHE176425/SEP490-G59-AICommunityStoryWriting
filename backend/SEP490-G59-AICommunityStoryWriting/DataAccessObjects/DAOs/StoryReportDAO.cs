using BusinessObjects;
using BusinessObjects.Entities;
using BusinessObjects.StoryReporting;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs;

public static class StoryReportDAO
{
    public const string StoryTargetType = "STORY";

    public static readonly string[] OpenStatuses = { "NEW", "IN_REVIEW" };

    public sealed record StoryReportContributorRecord(
        Guid StoryId,
        Guid UserId,
        string? UserEmail,
        string ReasonCode,
        string? Description,
        DateTime CreatedAtUtc,
        DateTime? ComplianceVerifiedAtUtc);

    /// <summary>Chi tiết từng người báo cáo (cho màn compliance).</summary>
    public static Dictionary<Guid, List<StoryReportContributorRecord>> GetContributorsByStoryIds(IEnumerable<Guid> storyIds)
    {
        var idList = storyIds.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<Guid, List<StoryReportContributorRecord>>();

        using var context = new StoryPlatformDbContext();
        var raw = (
            from c in context.story_report_contributors.AsNoTracking()
            join u in context.users.AsNoTracking() on c.user_id equals u.id
            where idList.Contains(c.story_id)
            orderby c.story_id, c.created_at
            select new
            {
                c.story_id,
                c.user_id,
                Email = u.email,
                c.reason_category,
                c.description,
                c.created_at,
                c.compliance_verified_at_utc
            }).ToList();

        var rows = raw.Select(x => new StoryReportContributorRecord(
            x.story_id,
            x.user_id,
            x.Email,
            string.IsNullOrWhiteSpace(x.reason_category) ? "OTHER" : x.reason_category.Trim().ToUpperInvariant(),
            x.description,
            x.created_at,
            x.compliance_verified_at_utc)).ToList();

        return rows
            .GroupBy(r => r.StoryId)
            .ToDictionary(g => g.Key, g => g.ToList());
    }

    /// <summary>Số lần chọn từng mã lý do (1 dòng / user) — dùng tính severity gộp cho queue.</summary>
    public static Dictionary<string, int> GetContributorReasonCounts(Guid storyId)
    {
        using var context = new StoryPlatformDbContext();
        var rows = context.story_report_contributors.AsNoTracking()
            .Where(c => c.story_id == storyId)
            .GroupBy(c => c.reason_category)
            .Select(g => new { Reason = g.Key, Cnt = g.Count() })
            .ToList();

        var dict = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            var code = string.IsNullOrWhiteSpace(row.Reason) ? "OTHER" : row.Reason.Trim().ToUpperInvariant();
            dict.TryGetValue(code, out var prev);
            dict[code] = prev + row.Cnt;
        }

        return dict;
    }

    /// <summary>
    /// Ghi nhận user đã báo cáo (1 lần / truyện / đời); gộp vào đúng 1 dòng <c>reports</c> mở cho truyện.
    /// </summary>
    public static Guid AppendStoryReportAggregated(Guid storyId, Guid userId, string reasonCodeNormalized, string? description)
    {
        using var context = new StoryPlatformDbContext();
        var strategy = context.Database.CreateExecutionStrategy();
        return strategy.Execute(() =>
        {
            using var tx = context.Database.BeginTransaction();
            try
            {
                if (context.story_report_contributors.AsNoTracking()
                        .Any(c => c.story_id == storyId && c.user_id == userId))
                    return Guid.Empty;

                context.story_report_contributors.Add(new story_report_contributors
                {
                    story_id = storyId,
                    user_id = userId,
                    reason_category = reasonCodeNormalized,
                    description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                    created_at = DateTime.UtcNow
                });

                try
                {
                    context.SaveChanges();
                }
                catch (DbUpdateException)
                {
                    return Guid.Empty;
                }

                var open = context.reports.FirstOrDefault(r =>
                    r.target_type == StoryTargetType
                    && r.target_id == storyId
                    && r.status != null
                    && (r.status == "NEW" || r.status == "IN_REVIEW"));

                if (open != null)
                {
                    var prev = open.contributor_count > 0 ? open.contributor_count : 1;
                    open.contributor_count = prev + 1;
                    open.reason_category = StoryReportReasonScores.PickHigherCode(open.reason_category, reasonCodeNormalized);
                    open.description = MergeDescriptions(open.description, description);
                    context.SaveChanges();
                    tx.Commit();
                    return open.id;
                }

                var row = new reports
                {
                    id = Guid.NewGuid(),
                    reporter_id = userId,
                    target_type = StoryTargetType,
                    target_id = storyId,
                    reason_category = reasonCodeNormalized,
                    description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                    status = "NEW",
                    created_at = DateTime.UtcNow,
                    contributor_count = 1
                };
                context.reports.Add(row);
                context.SaveChanges();
                tx.Commit();
                return row.id;
            }
            catch
            {
                tx.Rollback();
                throw;
            }
        });
    }

    private static string? MergeDescriptions(string? existing, string? add)
    {
        if (string.IsNullOrWhiteSpace(add)) return existing;
        var a = add.Trim();
        if (string.IsNullOrWhiteSpace(existing)) return a.Length > 4000 ? a[..4000] : a;
        var sep = "\n---\n";
        var merged = existing.Trim() + sep + a;
        return merged.Length > 4000 ? merged[..4000] : merged;
    }

    public static reports? GetById(Guid id)
    {
        using var context = new StoryPlatformDbContext();
        return context.reports
            .Include(r => r.reporter)
            .Include(r => r.assigned_toNavigation)
            .FirstOrDefault(r => r.id == id);
    }

    public static List<reports> ListStoryReportsForCompliance(string? statusesCsv)
    {
        using var context = new StoryPlatformDbContext();
        var list = context.reports.AsNoTracking()
            .Include(r => r.reporter)
            .Include(r => r.assigned_toNavigation)
            .Where(r => r.target_type == StoryTargetType)
            .OrderByDescending(r => r.created_at)
            .ToList();

        if (statusesCsv == null) return list;

        var statuses = ParseStatuses(statusesCsv);
        if (statuses.Count == 0) return list;

        return list
            .Where(r => r.status != null && statuses.Contains(r.status.Trim().ToUpperInvariant()))
            .ToList();
    }

    public static Dictionary<Guid, stories> GetStoriesByIds(IEnumerable<Guid> ids)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return new Dictionary<Guid, stories>();
        using var context = new StoryPlatformDbContext();
        return context.stories.AsNoTracking()
            .Where(s => idList.Contains(s.id))
            .ToDictionary(s => s.id);
    }

    public static void Update(reports entity)
    {
        using var context = new StoryPlatformDbContext();
        context.reports.Update(entity);
        context.SaveChanges();
    }

    public static bool IsOpenComplianceStatus(string? status)
    {
        var s = (status ?? "").Trim().ToUpperInvariant();
        return s is "NEW" or "IN_REVIEW";
    }

    /// <summary>Báo cáo truyện đã đóng bởi compliance (lịch sử).</summary>
    public static (List<reports> Items, int Total) ListResolvedByComplianceUser(
        Guid complianceUserId, int page, int pageSize, string? search)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        using var context = new StoryPlatformDbContext();
        var q = from r in context.reports.AsNoTracking()
                join s in context.stories.AsNoTracking() on r.target_id equals s.id
                where r.target_type == StoryTargetType
                      && r.compliance_resolved_by == complianceUserId
                      && r.status != null
                      && (r.status == "RESOLVED" || r.status == "DISMISSED")
                select new { r, s };

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            q = q.Where(x =>
                (x.s.title ?? "").Contains(term)
                || (x.s.slug ?? "").Contains(term)
                || (x.r.reason_category ?? "").Contains(term)
                || x.r.id.ToString().Contains(term));
        }

        var ordered = q
            .OrderByDescending(x => x.r.resolved_at ?? x.r.created_at)
            .Select(x => x.r.id);

        var total = ordered.Count();
        var ids = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        var items = context.reports.AsNoTracking()
            .Include(r => r.reporter)
            .Include(r => r.assigned_toNavigation)
            .Where(r => ids.Contains(r.id))
            .ToList();
        var order = ids.Select((id, i) => (id, i)).ToDictionary(x => x.id, x => x.i);
        items.Sort((a, b) => order[a.id].CompareTo(order[b.id]));

        return (items, total);
    }

    /// <summary>Đánh dấu mọi ticket NEW/IN_REVIEW của truyện là đã xử lý (compliance).</summary>
    public static int ResolveOpenStoryReportsForCompliance(Guid storyId, Guid complianceUserId, string newStatus)
    {
        using var context = new StoryPlatformDbContext();
        var rows = context.reports
            .Where(r =>
                r.target_type == StoryTargetType
                && r.target_id == storyId
                && r.status != null
                && (
                    r.status.Trim().ToUpper().StartsWith("NEW")
                    || r.status.Trim().ToUpper().StartsWith("IN_REVIEW")
                ))
            .ToList();
        if (rows.Count == 0) return 0;
        var now = DateTime.UtcNow;
        foreach (var r in rows)
        {
            r.status = newStatus;
            r.resolved_at = now;
            r.compliance_resolved_by = complianceUserId;
        }

        context.SaveChanges();
        return rows.Count;
    }

    public static int CountOpenStoryReports(Guid storyId)
    {
        using var context = new StoryPlatformDbContext();
        return context.reports.AsNoTracking().Count(r =>
            r.target_type == StoryTargetType
            && r.target_id == storyId
            && r.status != null
            && (
                r.status.Trim().ToUpper().StartsWith("NEW")
                || r.status.Trim().ToUpper().StartsWith("IN_REVIEW")
            ));
    }

    /// <summary>
    /// Khi đã đóng hết ticket mở (NEW/IN_REVIEW) của truyện, xóa contributor cycle cũ
    /// để vòng báo cáo mới không kéo theo người báo cáo từ các đợt trước.
    /// </summary>
    public static int ClearContributorsIfNoOpenReports(Guid storyId)
    {
        using var context = new StoryPlatformDbContext();
        var hasOpen = context.reports.AsNoTracking().Any(r =>
            r.target_type == StoryTargetType
            && r.target_id == storyId
            && r.status != null
            && (
                r.status.Trim().ToUpper().StartsWith("NEW")
                || r.status.Trim().ToUpper().StartsWith("IN_REVIEW")
            ));
        if (hasOpen) return 0;

        var rows = context.story_report_contributors
            .Where(c => c.story_id == storyId)
            .ToList();
        if (rows.Count == 0) return 0;

        context.story_report_contributors.RemoveRange(rows);
        context.SaveChanges();
        return rows.Count;
    }

    public static int ReopenInReviewReportsForAssignee(Guid storyId, Guid assigneeId)
    {
        using var context = new StoryPlatformDbContext();
        var list = context.reports
            .Where(r =>
                r.target_type == StoryTargetType
                && r.target_id == storyId
                && r.status != null
                && r.status.Trim().ToUpper().StartsWith("IN_REVIEW")
                && r.assigned_to == assigneeId)
            .ToList();
        foreach (var r in list)
        {
            r.status = "NEW";
            r.assigned_to = null;
        }

        if (list.Count == 0) return 0;
        context.SaveChanges();
        return list.Count;
    }

    private static HashSet<string> ParseStatuses(string? csv)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(csv)) return set;
        foreach (var p in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            set.Add(p.ToUpperInvariant());
        return set;
    }
}
