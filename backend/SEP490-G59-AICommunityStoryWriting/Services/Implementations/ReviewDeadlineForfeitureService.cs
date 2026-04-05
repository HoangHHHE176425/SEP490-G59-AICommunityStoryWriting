using DataAccessObjects.DAOs;
using Microsoft.Extensions.Logging;
using Services.Interfaces;

namespace Services.Implementations;

public class ReviewDeadlineForfeitureService : IReviewDeadlineForfeitureService
{
    /// <summary>
    /// FE thường gọi song song GET pending stories + pending chapters; background service cũng quét định kỳ.
    /// Không có khóa thì nhiều luồng cùng thấy chưa có moderation_logs và mỗi luồng chèn một dòng → trùng 5–6 bản ghi.
    /// </summary>
    private static readonly object ForfeitSweepGate = new();

    private readonly IModerationHubNotifier? _moderationHubNotifier;
    private readonly ILogger<ReviewDeadlineForfeitureService> _logger;

    public ReviewDeadlineForfeitureService(
        ILogger<ReviewDeadlineForfeitureService> logger,
        IModerationHubNotifier? moderationHubNotifier = null)
    {
        _logger = logger;
        _moderationHubNotifier = moderationHubNotifier;
    }

    public int ProcessOverdueClaims()
    {
        lock (ForfeitSweepGate)
        {
            var utcNow = DateTime.UtcNow;
            var rows = ReviewAssignmentDAO.ListOverdueModerationClaims(utcNow);
            if (rows.Count == 0)
                return 0;

            var processed = 0;
            foreach (var (targetType, targetId, assigneeId) in rows)
            {
                try
                {
                    var storyId = ResolveStoryId(targetType, targetId);
                    if (!storyId.HasValue)
                    {
                        _logger.LogWarning(
                            "Deadline forfeit: không xác định story cho target {TargetType} {TargetId} — vẫn trả claim nhưng không ghi log chặn tái nhận",
                            targetType,
                            targetId);
                    }

                    if (!ModerationLogDAO.TryForfeitOverdueModerationClaim(
                            targetType,
                            targetId,
                            assigneeId,
                            utcNow,
                            storyId))
                        continue;

                    processed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Deadline forfeit failed for {TargetType} {TargetId} assignee {AssigneeId}",
                        targetType,
                        targetId,
                        assigneeId);
                }
            }

            if (processed > 0)
            {
                _logger.LogInformation("Review deadline forfeit: processed {Count} overdue assignment(s)", processed);
                _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
            }

            return processed;
        }
    }

    private static Guid? ResolveStoryId(string targetType, Guid targetId)
    {
        if (string.Equals(targetType, ReviewAssignmentDAO.TargetTypeStory, StringComparison.OrdinalIgnoreCase))
            return targetId;
        if (string.Equals(targetType, ReviewAssignmentDAO.TargetTypeChapter, StringComparison.OrdinalIgnoreCase))
            return ChapterDAO.GetById(targetId)?.story_id;
        return null;
    }
}
