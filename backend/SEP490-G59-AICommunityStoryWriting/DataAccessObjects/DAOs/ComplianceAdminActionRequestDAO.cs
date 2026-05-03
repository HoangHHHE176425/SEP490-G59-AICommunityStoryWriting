using System.Text.RegularExpressions;
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

    /// <summary>Tiền tố trong message khi compliance gửi đơn từ báo cáo comment (gắn đúng thread).</summary>
    public const string CommentReportMessageTagPrefix = "[COMMENT_REPORT:";

    private static readonly Regex CommentReportMessageTagRegex = new(
        @"\[COMMENT_REPORT:([0-9a-fA-F-]{36})\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    /// <summary>Trích các commentId từ message đơn admin (tag <see cref="FormatCommentReportSourceTag"/>).</summary>
    public static IReadOnlyList<Guid> ParseCommentIdsFromMessage(string? message)
    {
        if (string.IsNullOrEmpty(message)) return Array.Empty<Guid>();
        var set = new HashSet<Guid>();
        foreach (Match m in CommentReportMessageTagRegex.Matches(message))
        {
            if (Guid.TryParse(m.Groups[1].Value, out var id) && id != Guid.Empty)
                set.Add(id);
        }
        return set.Count == 0 ? Array.Empty<Guid>() : set.ToList();
    }

    public static string FormatCommentReportSourceTag(Guid commentId) =>
        $"{CommentReportMessageTagPrefix}{commentId}]";

    /// <summary>Chỉ đơn có tag comment mới khiến thread đó bị chặn đóng ticket — không lan theo cả story + user.</summary>
    public static bool HasPendingCommentReportAdminAction(Guid commentId)
    {
        using var context = new StoryPlatformDbContext();
        var marker = FormatCommentReportSourceTag(commentId);
        return context.compliance_admin_action_requests.AsNoTracking()
            .Any(x => x.status == StatusPending
                && x.request_kind != null
                && (x.request_kind.ToUpper() == KindBanUser || x.request_kind.ToUpper() == KindSuspendAuthorWriting)
                && x.message != null
                && x.message.Contains(marker));
    }

    /// <summary>Đơn BAN/SUSPEND chờ admin từ luồng báo cáo truyện (message không có tag thread comment).</summary>
    public static bool HasPendingStoryComplianceAdminAction(Guid storyId)
    {
        using var context = new StoryPlatformDbContext();
        var commentTag = CommentReportMessageTagPrefix;
        return context.compliance_admin_action_requests.AsNoTracking()
            .Any(x => x.story_id == storyId
                && x.status == StatusPending
                && x.request_kind != null
                && (x.request_kind.ToUpper() == KindBanUser || x.request_kind.ToUpper() == KindSuspendAuthorWriting)
                && (x.message == null || !x.message.Contains(commentTag)));
    }

    /// <summary>Đơn chặn tài khoản (BAN_USER) đã được admin chấp nhận, gửi từ luồng báo cáo truyện (message không gắn thread comment).</summary>
    public static HashSet<Guid> ListStoryIdsWithApprovedBanUserStoryCompliance(IReadOnlyCollection<Guid> storyIds)
    {
        if (storyIds == null || storyIds.Count == 0) return new HashSet<Guid>();
        var ids = storyIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return new HashSet<Guid>();
        var commentTag = CommentReportMessageTagPrefix;
        using var context = new StoryPlatformDbContext();
        return context.compliance_admin_action_requests.AsNoTracking()
            .Where(x => ids.Contains(x.story_id)
                && x.status == StatusApproved
                && x.request_kind != null
                && x.request_kind.ToUpper() == KindBanUser
                && (x.message == null || !x.message.Contains(commentTag)))
            .Select(x => x.story_id)
            .Distinct()
            .ToHashSet();
    }

    /// <summary>Đơn chặn tài khoản đã duyệt gắn với thread comment (tag [COMMENT_REPORT:…]).</summary>
    public static bool CommentThreadHasApprovedBanUserRequest(Guid commentId)
    {
        if (commentId == Guid.Empty) return false;
        var marker = FormatCommentReportSourceTag(commentId);
        using var context = new StoryPlatformDbContext();
        return context.compliance_admin_action_requests.AsNoTracking()
            .Any(x => x.status == StatusApproved
                && x.request_kind != null
                && x.request_kind.ToUpper() == KindBanUser
                && x.message != null
                && x.message.Contains(marker));
    }

    /// <summary>Batch: story_id có đơn admin story-compliance đang PENDING.</summary>
    public static HashSet<Guid> ListStoryIdsWithPendingStoryComplianceAdminAction(IReadOnlyCollection<Guid> storyIds)
    {
        if (storyIds == null || storyIds.Count == 0) return new HashSet<Guid>();
        var ids = storyIds.Where(id => id != Guid.Empty).Distinct().ToList();
        if (ids.Count == 0) return new HashSet<Guid>();
        var commentTag = CommentReportMessageTagPrefix;
        using var context = new StoryPlatformDbContext();
        return context.compliance_admin_action_requests.AsNoTracking()
            .Where(x => ids.Contains(x.story_id)
                && x.status == StatusPending
                && x.request_kind != null
                && (x.request_kind.ToUpper() == KindBanUser || x.request_kind.ToUpper() == KindSuspendAuthorWriting)
                && (x.message == null || !x.message.Contains(commentTag)))
            .Select(x => x.story_id)
            .Distinct()
            .ToHashSet();
    }

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
