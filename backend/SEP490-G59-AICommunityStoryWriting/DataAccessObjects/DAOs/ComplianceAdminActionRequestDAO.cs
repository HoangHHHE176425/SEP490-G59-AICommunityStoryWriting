using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs;

public static class ComplianceAdminActionRequestDAO
{
    public const string StatusPending = "PENDING";
    public const string StatusApproved = "APPROVED";
    public const string StatusRejected = "REJECTED";

    public const string KindBanUser = "BAN_USER";
    public const string KindSuspendAuthorWriting = "SUSPEND_AUTHOR_WRITING";

    public static bool HasPendingForStoryAndKind(Guid storyId, string kind, Guid targetUserId)
    {
        using var context = new StoryPlatformDbContext();
        var k = kind.Trim().ToUpperInvariant();
        return context.compliance_admin_action_requests.AsNoTracking()
            .Any(x => x.story_id == storyId && x.target_user_id == targetUserId
                && x.status == StatusPending
                && x.request_kind != null && x.request_kind.ToUpper() == k);
    }

    public static Guid CreatePending(
        Guid storyId,
        Guid targetUserId,
        string requestKind,
        Guid requesterId,
        string? message,
        DateTime? proposedSuspendUntilUtc,
        string? urgencyTier = null)
    {
        using var context = new StoryPlatformDbContext();
        var kind = (requestKind ?? "").Trim().ToUpperInvariant();
        if (kind != KindBanUser && kind != KindSuspendAuthorWriting)
            throw new ArgumentException("Invalid request kind.");

        if (HasPendingForStoryAndKind(storyId, kind, targetUserId))
            throw new InvalidOperationException("Đã có yêu cầu cùng loại đang chờ admin cho truyện / user này.");

        var tier = NormalizeEscalationUrgencyTier(urgencyTier);
        var row = new compliance_admin_action_requests
        {
            id = Guid.NewGuid(),
            story_id = storyId,
            target_user_id = targetUserId,
            request_kind = kind,
            message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
            proposed_suspend_until_utc = proposedSuspendUntilUtc,
            status = StatusPending,
            requester_id = requesterId,
            created_at = DateTime.UtcNow,
            urgency_tier = tier
        };
        context.compliance_admin_action_requests.Add(row);
        context.SaveChanges();
        return row.id;
    }

    public static List<compliance_admin_action_requests> ListByStatus(string status)
    {
        using var context = new StoryPlatformDbContext();
        var st = (status ?? StatusPending).Trim().ToUpperInvariant();
        return context.compliance_admin_action_requests.AsNoTracking()
            .Include(x => x.story)
            .Include(x => x.target_user)
            .ThenInclude(u => u!.user_profiles)
            .Include(x => x.requester)
            .ThenInclude(u => u!.user_profiles)
            .Where(x => x.status == st)
            .OrderBy(x => x.created_at)
            .ToList();
    }

    public static List<compliance_admin_action_requests> ListByRequesterId(Guid requesterId)
    {
        using var context = new StoryPlatformDbContext();
        return context.compliance_admin_action_requests.AsNoTracking()
            .Include(x => x.story)
            .Include(x => x.target_user)
            .ThenInclude(u => u!.user_profiles)
            .Include(x => x.requester)
            .ThenInclude(u => u!.user_profiles)
            .Where(x => x.requester_id == requesterId)
            .OrderByDescending(x => x.created_at)
            .ToList();
    }

    public static compliance_admin_action_requests? GetTrackedById(Guid id)
    {
        using var context = new StoryPlatformDbContext();
        return context.compliance_admin_action_requests.FirstOrDefault(x => x.id == id);
    }

    public static void MarkResolved(Guid id, Guid adminId, string finalStatus, string? resolutionNote, string? resolutionAction)
    {
        using var context = new StoryPlatformDbContext();
        var row = context.compliance_admin_action_requests.FirstOrDefault(x => x.id == id);
        if (row == null || row.status != StatusPending)
            throw new InvalidOperationException("Yêu cầu không tồn tại hoặc đã xử lý.");
        row.status = finalStatus;
        row.resolved_at = DateTime.UtcNow;
        row.resolved_by_id = adminId;
        row.resolution_note = string.IsNullOrWhiteSpace(resolutionNote) ? null : resolutionNote.Trim();
        row.resolution_action = string.IsNullOrWhiteSpace(resolutionAction) ? null : resolutionAction.Trim().ToUpperInvariant();
        context.SaveChanges();
    }

    private static string NormalizeEscalationUrgencyTier(string? tier)
    {
        var t = (tier ?? "STANDARD").Trim().ToUpperInvariant();
        if (t == "CRITICAL") return "CRITICAL";
        if (t == "HIGH") return "HIGH";
        return "STANDARD";
    }
}
