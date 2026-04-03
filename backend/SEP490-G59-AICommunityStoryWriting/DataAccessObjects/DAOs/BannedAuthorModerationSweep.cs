using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs;

/// <summary>
/// Tác giả BANNED: trả mọi claim STORY/CHAPTER liên quan về hàng đợi và ghi moderation_logs (tránh race với lock).
/// </summary>
public static class BannedAuthorModerationSweep
{
    private static readonly object Gate = new();

    /// <summary>Trả về số cặp (moderator, truyện) đã ghi log (0 nếu không có gì thay đổi).</summary>
    public static int Run()
    {
        lock (Gate)
        {
            using var context = new StoryPlatformDbContext();
            var strategy = context.Database.CreateExecutionStrategy();
            return strategy.Execute(() =>
            {
                using var tx = context.Database.BeginTransaction();
                try
                {
                    var claimed = context.review_assignments
                        .Where(r =>
                            r.status == ReviewAssignmentDAO.StatusClaimed
                            && r.target_type != null
                            && (r.target_type == ReviewAssignmentDAO.TargetTypeStory
                                || r.target_type == ReviewAssignmentDAO.TargetTypeChapter))
                        .ToList();

                    if (claimed.Count == 0)
                    {
                        tx.Commit();
                        return 0;
                    }

                    var chapterRows = claimed
                        .Where(r => r.target_type == ReviewAssignmentDAO.TargetTypeChapter)
                        .ToList();
                    var chapterIds = chapterRows.Select(r => r.target_id).Distinct().ToList();
                    var chapterStoryMap = context.chapters
                        .AsNoTracking()
                        .Where(c => chapterIds.Contains(c.id) && c.story_id != null)
                        .Select(c => new { c.id, StoryId = c.story_id!.Value })
                        .ToDictionary(x => x.id, x => x.StoryId);

                    var storyIds = new HashSet<Guid>();
                    foreach (var r in claimed.Where(r => r.target_type == ReviewAssignmentDAO.TargetTypeStory))
                        storyIds.Add(r.target_id);
                    foreach (var r in chapterRows)
                    {
                        if (chapterStoryMap.TryGetValue(r.target_id, out var sid))
                            storyIds.Add(sid);
                    }

                    if (storyIds.Count == 0)
                    {
                        tx.Commit();
                        return 0;
                    }

                    var bannedStoryIds = (
                        from s in context.stories.AsNoTracking()
                        join u in context.users.AsNoTracking() on s.author_id equals u.id
                        where storyIds.Contains(s.id)
                              && s.author_id != null
                              && u.status != null
                              && u.status.ToUpper() == "BANNED"
                        select s.id).ToHashSet();

                    if (bannedStoryIds.Count == 0)
                    {
                        tx.Commit();
                        return 0;
                    }

                    var now = DateTime.UtcNow;
                    var toRelease = new List<review_assignments>();
                    foreach (var r in claimed)
                    {
                        Guid sid;
                        if (r.target_type == ReviewAssignmentDAO.TargetTypeStory)
                            sid = r.target_id;
                        else if (!chapterStoryMap.TryGetValue(r.target_id, out sid))
                            continue;
                        if (bannedStoryIds.Contains(sid))
                            toRelease.Add(r);
                    }

                    if (toRelease.Count == 0)
                    {
                        tx.Commit();
                        return 0;
                    }

                    foreach (var r in toRelease)
                    {
                        r.status = ReviewAssignmentDAO.StatusCompleted;
                        r.completed_at = now;
                    }

                    context.SaveChanges();

                    var logKeys = new HashSet<(Guid Mod, Guid Story)>();
                    foreach (var r in toRelease)
                    {
                        Guid sid;
                        if (r.target_type == ReviewAssignmentDAO.TargetTypeStory)
                            sid = r.target_id;
                        else if (!chapterStoryMap.TryGetValue(r.target_id, out sid))
                            continue;
                        if (bannedStoryIds.Contains(sid))
                            logKeys.Add((r.assignee_id, sid));
                    }

                    foreach (var (mod, sid) in logKeys)
                        ModerationLogDAO.InsertBanAuthorUnclaimLogIfNotExists(context, mod, sid, now);

                    tx.Commit();
                    return logKeys.Count;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            });
        }
    }
}
