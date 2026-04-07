//using BusinessObjects;
//using BusinessObjects.Entities;
//using Microsoft.EntityFrameworkCore;

//namespace DataAccessObjects.DAOs;

//public static class ComplianceStoryReportLockRequestDAO
//{
//    public const string StatusPending = "PENDING";
//    public const string StatusApproved = "APPROVED";
//    public const string StatusRejected = "REJECTED";

//    public static bool HasPendingForStory(Guid storyId)
//    {
//        using var context = new StoryPlatformDbContext();
//        return context.compliance_story_report_lock_requests.AsNoTracking()
//            .Any(x => x.story_id == storyId && x.status == StatusPending);
//    }

//    public static Guid CreatePending(Guid storyId, Guid requesterId, string? message, string? urgencyTier = null)
//    {
//        using var context = new StoryPlatformDbContext();
//        if (context.compliance_story_report_lock_requests.Any(x => x.story_id == storyId && x.status == StatusPending))
//            throw new InvalidOperationException("Đã có yêu cầu chờ admin cho truyện này.");

//        var tier = NormalizeEscalationUrgencyTier(urgencyTier);
//        var row = new compliance_story_report_lock_requests
//        {
//            id = Guid.NewGuid(),
//            story_id = storyId,
//            requester_id = requesterId,
//            message = string.IsNullOrWhiteSpace(message) ? null : message.Trim(),
//            status = StatusPending,
//            created_at = DateTime.UtcNow,
//            urgency_tier = tier
//        };
//        context.compliance_story_report_lock_requests.Add(row);
//        context.SaveChanges();
//        return row.id;
//    }

//    public static List<compliance_story_report_lock_requests> ListByStatus(string status)
//    {
//        using var context = new StoryPlatformDbContext();
//        var st = (status ?? StatusPending).Trim().ToUpperInvariant();
//        return context.compliance_story_report_lock_requests.AsNoTracking()
//            .Include(x => x.story)
//            .Include(x => x.requester)
//            .ThenInclude(u => u!.user_profiles)
//            .Where(x => x.status == st)
//            .OrderBy(x => x.created_at)
//            .ToList();
//    }

//    public static List<compliance_story_report_lock_requests> ListByRequesterId(Guid requesterId)
//    {
//        using var context = new StoryPlatformDbContext();
//        return context.compliance_story_report_lock_requests.AsNoTracking()
//            .Include(x => x.story)
//            .Include(x => x.requester)
//            .ThenInclude(u => u!.user_profiles)
//            .Where(x => x.requester_id == requesterId)
//            .OrderByDescending(x => x.created_at)
//            .ToList();
//    }

//    public static compliance_story_report_lock_requests? GetTrackedById(Guid id)
//    {
//        using var context = new StoryPlatformDbContext();
//        return context.compliance_story_report_lock_requests
//            .FirstOrDefault(x => x.id == id);
//    }

//    public static (Guid storyId, Guid requesterId)? TryGetPendingStoryAndRequester(Guid id)
//    {
//        using var context = new StoryPlatformDbContext();
//        var row = context.compliance_story_report_lock_requests.AsNoTracking()
//            .FirstOrDefault(x => x.id == id && x.status == StatusPending);
//        if (row == null) return null;
//        return (row.story_id, row.requester_id);
//    }

//    public static void MarkResolved(Guid id, Guid adminId, string finalStatus, string? resolutionNote, string? resolutionAction)
//    {
//        using var context = new StoryPlatformDbContext();
//        var row = context.compliance_story_report_lock_requests.FirstOrDefault(x => x.id == id);
//        if (row == null || row.status != StatusPending)
//            throw new InvalidOperationException("Yêu cầu không tồn tại hoặc đã xử lý.");
//        row.status = finalStatus;
//        row.resolved_at = DateTime.UtcNow;
//        row.resolved_by_id = adminId;
//        row.resolution_note = string.IsNullOrWhiteSpace(resolutionNote) ? null : resolutionNote.Trim();
//        row.resolution_action = string.IsNullOrWhiteSpace(resolutionAction) ? null : resolutionAction.Trim().ToUpperInvariant();
//        context.SaveChanges();
//    }

//    private static string NormalizeEscalationUrgencyTier(string? tier)
//    {
//        var t = (tier ?? "STANDARD").Trim().ToUpperInvariant();
//        if (t == "CRITICAL") return "CRITICAL";
//        if (t == "HIGH") return "HIGH";
//        return "STANDARD";
//    }
//}
