using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs;

public static class ComplianceReportLockRequestDAO
{
    public const string StatusPending = "PENDING";
    public const string StatusApproved = "APPROVED";
    public const string StatusRejected = "REJECTED";
    public const string StatusCancelled = "CANCELLED";

    public const string TargetTypeStory = "STORY";
    public const string TargetTypeComment = "COMMENT";
    public const string TargetTypeChapter = "CHAPTER";
    public const string TargetTypeAppeal = "APPEAL";

    public static bool HasPendingForTarget(string targetType, Guid targetId)
    {
        using var context = new StoryPlatformDbContext();
        var tt = (targetType ?? "").Trim().ToUpperInvariant();
        return context.compliance_report_lock_requests.AsNoTracking()
            .Any(x => (x.status ?? "").Trim().ToUpper() == StatusPending
                && x.target_type != null
                && x.target_type.ToUpper() == tt
                && x.target_id == targetId);
    }

    /// <summary>Danh sách story_id đang có yêu cầu gỡ lock (target_type STORY) trạng thái PENDING.</summary>
    public static HashSet<Guid> ListPendingStoryTargetIds(IReadOnlyCollection<Guid> storyIds)
    {
        if (storyIds == null || storyIds.Count == 0) return new HashSet<Guid>();
        var ids = storyIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return new HashSet<Guid>();
        using var context = new StoryPlatformDbContext();
        var tt = TargetTypeStory.ToUpperInvariant();
        return context.compliance_report_lock_requests.AsNoTracking()
            .Where(x => (x.status ?? "").Trim().ToUpper() == StatusPending
                && x.target_type != null
                && x.target_type.ToUpper() == tt
                && ids.Contains(x.target_id))
            .Select(x => x.target_id)
            .ToHashSet();
    }

    public static Guid CreatePending(string targetType, Guid targetId, Guid requesterId, string? message)
    {
        using var context = new StoryPlatformDbContext();
        var tt = (targetType ?? "").Trim().ToUpperInvariant();
        if (tt is not (TargetTypeStory or TargetTypeComment or TargetTypeChapter or TargetTypeAppeal))
            throw new ArgumentException("target_type không hợp lệ.");

        if (context.compliance_report_lock_requests.Any(x =>
                (x.status ?? "").Trim().ToUpper() == StatusPending
                && x.target_type != null
                && x.target_type.ToUpper() == tt
                && x.target_id == targetId))
            throw new InvalidOperationException("Đã có yêu cầu gỡ lock chờ admin cho mục tiêu này.");

        var row = new compliance_report_lock_requests
        {
            id = Guid.NewGuid(),
            target_type = tt,
            target_id = targetId,
            requester_id = requesterId,
            message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            status = StatusPending,
            created_at = DateTime.UtcNow
        };
        context.compliance_report_lock_requests.Add(row);
        context.SaveChanges();
        return row.id;
    }

    public static List<compliance_report_lock_requests> ListByStatus(string status)
    {
        using var context = new StoryPlatformDbContext();
        var st = (status ?? StatusPending).Trim().ToUpperInvariant();
        return context.compliance_report_lock_requests.AsNoTracking()
            .Where(x => x.status != null && x.status.ToUpper() == st)
            .OrderBy(x => x.created_at)
            .ToList();
    }

    public static List<compliance_report_lock_requests> ListByRequesterId(Guid requesterId)
    {
        using var context = new StoryPlatformDbContext();
        return context.compliance_report_lock_requests.AsNoTracking()
            .Where(x => x.requester_id == requesterId)
            .OrderByDescending(x => x.created_at)
            .ToList();
    }

    public static compliance_report_lock_requests? GetTrackedById(Guid id)
    {
        using var context = new StoryPlatformDbContext();
        return context.compliance_report_lock_requests.FirstOrDefault(x => x.id == id);
    }

    public static (string TargetType, Guid TargetId, Guid RequesterId)? TryGetPendingTargetAndRequester(Guid id)
    {
        using var context = new StoryPlatformDbContext();
        var row = context.compliance_report_lock_requests.AsNoTracking()
            .FirstOrDefault(x => x.id == id && (x.status ?? "").Trim().ToUpper() == StatusPending);
        if (row == null) return null;
        var tt = (row.target_type ?? "").Trim().ToUpperInvariant();
        return (tt, row.target_id, row.requester_id);
    }

    public static void MarkResolved(Guid id, Guid adminId, string finalStatus, string? resolutionNote, string? resolutionAction)
    {
        using var context = new StoryPlatformDbContext();
        var row = context.compliance_report_lock_requests.FirstOrDefault(x => x.id == id);
        if (row == null || (row.status ?? "").Trim().ToUpper() != StatusPending)
            throw new InvalidOperationException("Yêu cầu không tồn tại hoặc đã xử lý.");
        row.status = finalStatus;
        row.resolved_at = DateTime.UtcNow;
        row.resolved_by_id = adminId;
        row.resolution_note = string.IsNullOrWhiteSpace(resolutionNote) ? null : resolutionNote.Trim();
        row.resolution_action = string.IsNullOrWhiteSpace(resolutionAction) ? null : resolutionAction.Trim().ToUpperInvariant();
        context.SaveChanges();
    }
}
