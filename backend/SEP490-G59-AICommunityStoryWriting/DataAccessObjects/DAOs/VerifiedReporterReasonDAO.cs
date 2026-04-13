using BusinessObjects;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs;

/// <summary>Một phiếu báo cáo từ người dùng mà compliance đã xác minh (mỗi dòng = một lần báo cáo).</summary>
public sealed record ComplianceVerifiedUserReportLine(string ReasonCode, string? ReporterDescription);

/// <summary>Đọc chi tiết báo cáo đã xác minh cho email / thống kê — không gộp theo mã lý do.</summary>
public static class VerifiedReporterReasonDAO
{
    private const string CommentTargetType = "COMMENT";

    /// <summary>Mỗi dòng <c>story_report_contributors</c> đã có <c>compliance_verified_at_utc</c>.</summary>
    public static List<ComplianceVerifiedUserReportLine> ListComplianceVerifiedReportLinesForStory(Guid storyId)
    {
        if (storyId == Guid.Empty) return new List<ComplianceVerifiedUserReportLine>();
        using var context = new StoryPlatformDbContext();
        var rows = context.story_report_contributors.AsNoTracking()
            .Where(c => c.story_id == storyId && c.compliance_verified_at_utc != null)
            .OrderBy(c => c.compliance_verified_at_utc)
            .ThenBy(c => c.created_at)
            .ThenBy(c => c.user_id)
            .Select(c => new { c.reason_category, c.description })
            .ToList();

        return rows.Select(x => new ComplianceVerifiedUserReportLine(
            string.IsNullOrWhiteSpace(x.reason_category) ? "OTHER" : x.reason_category.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(x.description) ? null : x.description.Trim())).ToList();
    }

    /// <summary>Mỗi dòng <c>report_evidences</c> đã xác minh (một người báo cáo / một bằng chứng).</summary>
    public static List<ComplianceVerifiedUserReportLine> ListComplianceVerifiedReportLinesForCommentThreads(
        IEnumerable<Guid> commentIds)
    {
        var ids = commentIds?.Where(id => id != Guid.Empty).Distinct().ToList() ?? new List<Guid>();
        if (ids.Count == 0) return new List<ComplianceVerifiedUserReportLine>();

        using var context = new StoryPlatformDbContext();
        var rows = (
            from e in context.report_evidences.AsNoTracking()
            join r in context.reports.AsNoTracking() on e.report_id equals r.id
            where e.report_id != null
                  && ids.Contains(r.target_id)
                  && r.target_type != null
                  && r.target_type.ToUpper() == CommentTargetType
                  && e.compliance_verified_at_utc != null
            orderby e.compliance_verified_at_utc, e.id
            select new { r.reason_category, r.description }).ToList();

        return rows.Select(x => new ComplianceVerifiedUserReportLine(
            string.IsNullOrWhiteSpace(x.reason_category) ? "OTHER" : x.reason_category.Trim().ToUpperInvariant(),
            string.IsNullOrWhiteSpace(x.description) ? null : x.description.Trim())).ToList();
    }
}
