using DataAccessObjects.DAOs;
using Repositories;

namespace Services.Implementations
{
    /// <summary>SLA moderator: mốc từ lúc tác giả gửi duyệt (submitted_for_review_at). Mức cảnh báo chỉ theo thời gian đã chờ.</summary>
    public static class ModeratorReviewSlaHelper
    {
        public const int PolicyDaysAfterAuthorSubmit = 7;
        /// <summary>Cảnh báo nhẹ khi còn &lt; 3 ngày tới hạn duyệt (review_deadline_at).</summary>
        private const double DeadlineWarningHours = 72;
        /// <summary>Cảnh báo gấp khi còn ≤ 24 giờ.</summary>
        private const double DeadlineCriticalHours = 24;

        public static DateTime NormalizeToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        /// <summary>Thời điểm tác giả gửi bản chờ duyệt hiện tại (ưu tiên submitted_for_review_at).</summary>
        public static DateTime? GetAuthorSubmittedUtc(
            string targetType,
            Guid targetId,
            IStoryRepository storyRepository,
            IChapterRepository chapterRepository,
            IChapterVersionRepository versionRepository)
        {
            if (string.Equals(targetType, ReviewAssignmentDAO.TargetTypeStory, StringComparison.OrdinalIgnoreCase))
            {
                var s = storyRepository.GetById(targetId);
                if (s == null)
                    return null;
                if (s.submitted_for_review_at.HasValue)
                    return NormalizeToUtc(s.submitted_for_review_at.Value);
                var t = s.updated_at ?? s.created_at;
                return t.HasValue ? NormalizeToUtc(t.Value) : null;
            }

            if (string.Equals(targetType, ReviewAssignmentDAO.TargetTypeChapter, StringComparison.OrdinalIgnoreCase))
            {
                var c = chapterRepository.GetById(targetId);
                if (c == null)
                    return null;
                if (c.submitted_for_review_at.HasValue)
                    return NormalizeToUtc(c.submitted_for_review_at.Value);
                var rawBase = c.updated_at ?? c.created_at;
                DateTime? baseUtc = rawBase.HasValue ? NormalizeToUtc(rawBase.Value) : null;
                var pendingTimes = versionRepository.GetByChapterId(targetId)
                    .Where(v => string.Equals(v.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase))
                    .Select(v => v.created_at)
                    .Where(t => t.HasValue)
                    .Select(t => NormalizeToUtc(t!.Value))
                    .ToList();
                if (pendingTimes.Count == 0)
                    return baseUtc;
                var maxV = pendingTimes.Max();
                if (baseUtc.HasValue)
                    return maxV > baseUtc.Value ? maxV : baseUtc.Value;
                return maxV;
            }

            return null;
        }

        /// <summary>
        /// Mức cảnh báo SLA. Ưu tiên <paramref name="fallbackDeadlineUtc"/> (hạn duyệt moderator / review_deadline_at hoặc gợi ý +7 ngày)
        /// khi có — khớp quản lý xuất bản; chỉ dùng mốc tác giả gửi khi không có hạn fallback.
        /// </summary>
        public static string ComputeSlaTimeStatus(DateTime? authorSubmittedUtc, DateTime? fallbackDeadlineUtc)
        {
            var now = DateTime.UtcNow;
            if (fallbackDeadlineUtc.HasValue)
                return ComputeTimeStatusFromDeadlineOnly(fallbackDeadlineUtc, now);

            if (authorSubmittedUtc.HasValue)
            {
                var elapsed = now - NormalizeToUtc(authorSubmittedUtc.Value);
                if (elapsed.TotalDays >= 7)
                    return "Overdue";
                if (elapsed.TotalDays >= 4)
                    return "Critical";
                if (elapsed.TotalDays >= 2)
                    return "Warning";
                return "OnTime";
            }

            return "OnTime";
        }

        private static string ComputeTimeStatusFromDeadlineOnly(DateTime? deadlineUtc, DateTime nowUtc)
        {
            if (!deadlineUtc.HasValue)
                return "OnTime";
            var deadline = NormalizeToUtc(deadlineUtc.Value);
            if (nowUtc > deadline)
                return "Overdue";
            var hoursLeft = (deadline - nowUtc).TotalHours;
            if (hoursLeft <= DeadlineCriticalHours)
                return "Critical";
            if (hoursLeft < DeadlineWarningHours)
                return "Warning";
            return "OnTime";
        }
    }
}
