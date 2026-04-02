using System.Linq;
using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public static class ModerationLogDAO
    {
        /// <summary>Hệ thống tự trả đơn về hàng đợi vì moderator quá hạn duyệt — ghi nhận để chặn moderator nhận lại cùng truyện. Tối đa 20 ký tự (cột <c>action</c> trong DB / Fluent).</summary>
        public const string ActionAutoUnassignedDeadline = "AUTO_FORFEIT_DL";

        /// <summary>Mã dài trước đây — vượt quá cột DB nên insert có thể lỗi; vẫn dùng khi kiểm tra chặn nếu đã có bản ghi.</summary>
        public const string ActionAutoUnassignedDeadlineLegacyLong = "AUTO_UNASSIGNED_DEADLINE";

        public const string DeadlineForfeitRejectionReasonVi = "Kiểm duyệt viên để quá hạn duyệt deadline";

        /// <summary>Điều kiện EF (dịch được SQL): log chặn tái nhận sau quá hạn cho cặp moderator + truyện.</summary>
        private static IQueryable<moderation_logs> WhereDeadlineForfeitBlockForStory(
            IQueryable<moderation_logs> query,
            Guid moderatorId,
            Guid storyId)
        {
            var storyType = ReviewAssignmentDAO.TargetTypeStory;
            return query.Where(m =>
                m.moderator_id == moderatorId
                && m.target_id == storyId
                && m.target_type != null
                && m.target_type.ToUpper() == storyType
                && m.action != null
                && (m.action == ActionAutoUnassignedDeadline
                    || m.action == ActionAutoUnassignedDeadlineLegacyLong
                    || m.action.ToUpper() == ActionAutoUnassignedDeadline
                    || m.action.ToUpper() == ActionAutoUnassignedDeadlineLegacyLong));
        }

        public static void Add(moderation_logs log)
        {
            using var context = new StoryPlatformDbContext();
            context.moderation_logs.Add(log);
            context.SaveChanges();
        }

        /// <summary>Moderator đã bị hệ thống thu hồi claim do quá hạn — không được nhận duyệt lại cùng truyện.</summary>
        public static void AddDeadlineForfeitLog(Guid moderatorId, Guid storyId)
        {
            if (HasDeadlineForfeitBlockOnStory(moderatorId, storyId))
                return;
            Add(new moderation_logs
            {
                moderator_id = moderatorId,
                target_type = ReviewAssignmentDAO.TargetTypeStory,
                target_id = storyId,
                action = ActionAutoUnassignedDeadline,
                rejection_reason = DeadlineForfeitRejectionReasonVi,
                created_at = DateTime.UtcNow
            });
        }

        /// <summary>
        /// SQL Server: tối đa một dòng log chặn quá hạn cho cặp (moderator, truyện). Dùng trong transaction hiện tại;
        /// UPDLOCK/HOLDLOCK giảm phantom khi nhiều instance API chạy song song.
        /// </summary>
        private static void InsertDeadlineForfeitLogIfNotExists(StoryPlatformDbContext context, Guid moderatorId, Guid storyId, DateTime createdAtUtc)
        {
            var storyType = ReviewAssignmentDAO.TargetTypeStory;
            var action = ActionAutoUnassignedDeadline;
            var leg = ActionAutoUnassignedDeadlineLegacyLong;
            var reason = DeadlineForfeitRejectionReasonVi;

            context.Database.ExecuteSqlInterpolated($@"
IF NOT EXISTS (
    SELECT 1 FROM [moderation_logs] WITH (UPDLOCK, HOLDLOCK)
    WHERE [moderator_id] = {moderatorId}
      AND [target_id] = {storyId}
      AND [target_type] IS NOT NULL AND UPPER([target_type]) = UPPER({storyType})
      AND [action] IS NOT NULL
      AND ([action] = {action} OR [action] = {leg}
           OR UPPER([action]) = UPPER({action}) OR UPPER([action]) = UPPER({leg}))
)
INSERT INTO [moderation_logs] ([moderator_id], [target_type], [target_id], [action], [rejection_reason], [created_at])
VALUES ({moderatorId}, {storyType}, {storyId}, {action}, {reason}, {createdAtUtc})");
        }

        /// <summary>Hoàn thành claim quá hạn và (nếu có story) ghi tối đa một moderation_logs trong một transaction.</summary>
        public static bool TryForfeitOverdueModerationClaim(
            string targetType,
            Guid targetId,
            Guid assigneeId,
            DateTime utcNow,
            Guid? storyIdForDeadlineBlock)
        {
            using var context = new StoryPlatformDbContext();
            var strategy = context.Database.CreateExecutionStrategy();
            return strategy.Execute(() =>
            {
                using var tx = context.Database.BeginTransaction();
                try
                {
                    var cur = context.review_assignments.FirstOrDefault(r =>
                        r.target_type == targetType
                        && r.target_id == targetId
                        && r.status == ReviewAssignmentDAO.StatusClaimed
                        && r.assignee_id == assigneeId);
                    if (cur == null)
                    {
                        tx.Rollback();
                        return false;
                    }

                    if (cur.review_deadline_at == null || cur.review_deadline_at >= utcNow)
                    {
                        tx.Rollback();
                        return false;
                    }

                    cur.status = ReviewAssignmentDAO.StatusCompleted;
                    cur.completed_at = utcNow;
                    context.SaveChanges();

                    if (storyIdForDeadlineBlock.HasValue)
                        InsertDeadlineForfeitLogIfNotExists(context, assigneeId, storyIdForDeadlineBlock.Value, utcNow);

                    tx.Commit();
                    return true;
                }
                catch
                {
                    tx.Rollback();
                    throw;
                }
            });
        }

        public static bool HasDeadlineForfeitBlockOnStory(Guid moderatorId, Guid storyId)
        {
            using var context = new StoryPlatformDbContext();
            return WhereDeadlineForfeitBlockForStory(context.moderation_logs.AsNoTracking(), moderatorId, storyId).Any();
        }

        public static (string? reason, DateTime? rejectedAt) GetLatestRejection(string targetType, Guid targetId)
        {
            using var context = new StoryPlatformDbContext();
            var log = context.moderation_logs
                .AsNoTracking()
                .Where(m => m.target_type == targetType && m.target_id == targetId && m.action == "REJECTED")
                .OrderByDescending(m => m.created_at)
                .Select(m => new { m.rejection_reason, m.created_at })
                .FirstOrDefault();
            return log != null ? (log.rejection_reason, log.created_at) : (null, null);
        }

        /// <summary>Mọi bản ghi REJECTED cho từng target (vd. CHAPTER), sắp xếp theo thời gian tăng dần (lịch sử).</summary>
        public static Dictionary<Guid, List<(string? Reason, DateTime? RejectedAt, Guid? ModeratorId)>> GetRejectionHistoriesByTargetIds(
            string targetType,
            IReadOnlyList<Guid> targetIds)
        {
            if (targetIds == null || targetIds.Count == 0)
                return new Dictionary<Guid, List<(string?, DateTime?, Guid?)>>();

            var idSet = targetIds.ToHashSet();
            using var context = new StoryPlatformDbContext();
            var logs = context.moderation_logs
                .AsNoTracking()
                .Where(m =>
                    m.target_type == targetType &&
                    m.target_id.HasValue &&
                    idSet.Contains(m.target_id.Value) &&
                    m.action != null &&
                    m.action.ToUpper() == "REJECTED")
                .OrderBy(m => m.created_at ?? DateTime.MinValue)
                .ThenBy(m => m.id)
                .Select(m => new { m.target_id, m.rejection_reason, m.created_at, m.moderator_id })
                .ToList();

            var dict = targetIds.Distinct().ToDictionary(id => id, _ => new List<(string?, DateTime?, Guid?)>());
            foreach (var log in logs)
            {
                var tid = log.target_id!.Value;
                if (!dict.ContainsKey(tid)) continue;
                dict[tid].Add((log.rejection_reason, log.created_at, log.moderator_id));
            }

            return dict;
        }

        /// <summary>Lấy danh sách target_id từ moderator_logs do moderator này duyệt/từ chối (action = APPROVED hoặc REJECTED).</summary>
        public static List<Guid> GetTargetIdsByModeratorAndAction(Guid moderatorId, string targetType, string action)
        {
            using var context = new StoryPlatformDbContext();
            return context.moderation_logs
                .AsNoTracking()
                .Where(m => m.moderator_id == moderatorId && m.target_type == targetType && m.action == action && m.target_id.HasValue)
                .Select(m => m.target_id!.Value)
                .Distinct()
                .ToList();
        }

        /// <summary>Lấy danh sách target_id mà hành động cuối cùng (theo created_at) là action. Dùng cho tab "Từ chối" để vẫn hiển thị sau khi tác giả gửi lại (PENDING_REVIEW) cho đến khi moderator duyệt (APPROVED).</summary>
        /// <param name="moderatorId">Nếu null (admin): mọi target có last action = action; nếu có giá trị: chỉ target do moderator này thực hiện action cuối.</param>
        public static List<Guid> GetTargetIdsWhereLastActionIs(string targetType, string action, Guid? moderatorId = null)
        {
            using var context = new StoryPlatformDbContext();
            var logs = context.moderation_logs
                .AsNoTracking()
                .Where(m => m.target_type == targetType && m.target_id.HasValue)
                .OrderByDescending(m => m.created_at)
                .ToList();

            var seen = new HashSet<Guid>();
            var result = new List<Guid>();
            foreach (var m in logs)
            {
                var id = m.target_id!.Value;
                if (seen.Contains(id)) continue;
                seen.Add(id);
                if (string.Equals(m.action, action, StringComparison.OrdinalIgnoreCase) &&
                    (!moderatorId.HasValue || m.moderator_id == moderatorId.Value))
                    result.Add(id);
            }
            return result;
        }

        /// <summary>Lấy danh sách target_id từ moderation_logs với bộ lọc (Admin: theo moderator, khoảng ngày, action).</summary>
        public static List<Guid> GetTargetIdsFiltered(string targetType, Guid? moderatorId, DateTime? dateFrom, DateTime? dateTo, string action)
        {
            var actionUpper = (action ?? "").Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(actionUpper)) return new List<Guid>();
            using var context = new StoryPlatformDbContext();
            var query = context.moderation_logs
                .AsNoTracking()
                .Where(m => m.target_type == targetType && m.target_id.HasValue && m.action != null && m.action.ToUpper() == actionUpper);
            if (moderatorId.HasValue)
                query = query.Where(m => m.moderator_id == moderatorId.Value);
            if (dateFrom.HasValue)
                query = query.Where(m => m.created_at >= dateFrom.Value);
            if (dateTo.HasValue)
                query = query.Where(m => m.created_at <= dateTo.Value);
            return query.Select(m => m.target_id!.Value).Distinct().ToList();
        }

        /// <summary>Lấy thông tin log (created_at, moderator_id) cho từng target — bản ghi mới nhất theo action.</summary>
        public static Dictionary<Guid, (DateTime CreatedAt, Guid? ModeratorId)> GetLogInfoByTargets(string targetType, IReadOnlyList<Guid> targetIds, string action)
        {
            if (targetIds == null || targetIds.Count == 0)
                return new Dictionary<Guid, (DateTime, Guid?)>();
            var actionUpper = (action ?? "").Trim().ToUpperInvariant();
            using var context = new StoryPlatformDbContext();
            var logs = context.moderation_logs
                .AsNoTracking()
                .Where(m => m.target_type == targetType && m.target_id.HasValue &&
                    targetIds.Contains(m.target_id.Value) &&
                    m.action != null && m.action.ToUpper() == actionUpper)
                .OrderByDescending(m => m.created_at)
                .Select(m => new { m.target_id, m.created_at, m.moderator_id })
                .ToList();
            var result = new Dictionary<Guid, (DateTime, Guid?)>();
            foreach (var m in logs)
            {
                var id = m.target_id!.Value;
                if (!result.ContainsKey(id) && m.created_at.HasValue)
                    result[id] = (m.created_at.Value, m.moderator_id);
            }
            return result;
        }

        /// <summary>Log kiểm duyệt (Admin): lọc, tìm kiếm, sắp xếp, phân trang — theo dõi hoạt động moderator.</summary>
        public static (List<moderation_logs> Logs, int TotalCount) SearchModerationLogsPage(
            string? search,
            Guid? moderatorId,
            DateTime? dateFrom,
            DateTime? dateTo,
            string? action,
            string? targetType,
            Guid? targetId,
            int? processingTimeMinMs,
            int? processingTimeMaxMs,
            string? sortBy,
            string? sortOrder,
            int page,
            int pageSize)
        {
            using var context = new StoryPlatformDbContext();
            var query = context.moderation_logs.AsNoTracking().AsQueryable();

            if (moderatorId.HasValue)
                query = query.Where(m => m.moderator_id == moderatorId.Value);

            if (dateFrom.HasValue)
                query = query.Where(m => m.created_at >= dateFrom.Value);

            if (dateTo.HasValue)
                query = query.Where(m => m.created_at <= dateTo.Value);

            var actionUpper = (action ?? "").Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(actionUpper))
                query = query.Where(m => m.action != null && m.action.ToUpper() == actionUpper);

            var targetTypeUpper = (targetType ?? "").Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(targetTypeUpper))
                query = query.Where(m => m.target_type != null && m.target_type.ToUpper() == targetTypeUpper);

            if (targetId.HasValue)
                query = query.Where(m => m.target_id == targetId.Value);

            if (processingTimeMinMs.HasValue)
                query = query.Where(m => m.processing_time_ms != null && m.processing_time_ms >= processingTimeMinMs.Value);

            if (processingTimeMaxMs.HasValue)
                query = query.Where(m => m.processing_time_ms != null && m.processing_time_ms <= processingTimeMaxMs.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                if (long.TryParse(s, out var logId))
                {
                    query = query.Where(m => m.id == logId);
                }
                else if (Guid.TryParse(s, out var g))
                {
                    query = query.Where(m => m.moderator_id == g || m.target_id == g);
                }
                else
                {
                    var ttStory = ReviewAssignmentDAO.TargetTypeStory;
                    var ttChapter = ReviewAssignmentDAO.TargetTypeChapter;
                    query = query.Where(m =>
                        (m.rejection_reason != null && m.rejection_reason.Contains(s)) ||
                        (m.target_type == ttStory && m.target_id.HasValue &&
                         context.stories.Any(st => st.id == m.target_id && st.title.Contains(s))) ||
                        (m.target_type == ttChapter && m.target_id.HasValue &&
                         context.chapters.Any(ch => ch.id == m.target_id && ch.title.Contains(s))));
                }
            }

            var total = query.Count();

            var sortAsc = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);
            IQueryable<moderation_logs> ordered;
            if (string.Equals(sortBy, "id", StringComparison.OrdinalIgnoreCase))
            {
                ordered = sortAsc
                    ? query.OrderBy(m => m.id)
                    : query.OrderByDescending(m => m.id);
            }
            else if (string.Equals(sortBy, "processing_time_ms", StringComparison.OrdinalIgnoreCase))
            {
                ordered = sortAsc
                    ? query.OrderBy(m => m.processing_time_ms ?? int.MaxValue).ThenBy(m => m.id)
                    : query.OrderByDescending(m => m.processing_time_ms ?? -1).ThenByDescending(m => m.id);
            }
            else
            {
                ordered = sortAsc
                    ? query.OrderBy(m => m.created_at).ThenBy(m => m.id)
                    : query.OrderByDescending(m => m.created_at).ThenByDescending(m => m.id);
            }

            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            var list = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return (list, total);
        }

        /// <summary>Thống kê theo moderator (APPROVED/REJECTED + breakdown STORY/CHAPTER) — dùng cho Moderator Performance (admin).</summary>
        public static List<ModeratorPerformanceStatsRow> GetModeratorPerformanceAggregates(
            DateTime? dateFrom,
            DateTime? dateTo,
            string? targetTypeFilter)
        {
            using var context = new StoryPlatformDbContext();
            var ttStory = ReviewAssignmentDAO.TargetTypeStory;
            var ttChapter = ReviewAssignmentDAO.TargetTypeChapter;

            var query = context.moderation_logs
                .AsNoTracking()
                .Where(m => m.moderator_id.HasValue && m.action != null &&
                    (m.action.ToUpper() == "APPROVED" || m.action.ToUpper() == "REJECTED"));
            if (dateFrom.HasValue)
                query = query.Where(m => m.created_at >= dateFrom.Value);
            if (dateTo.HasValue)
                query = query.Where(m => m.created_at <= dateTo.Value);
            if (!string.IsNullOrWhiteSpace(targetTypeFilter))
            {
                var tt = targetTypeFilter.Trim().ToUpperInvariant();
                query = query.Where(m => m.target_type != null && m.target_type.ToUpper() == tt);
            }

            var grouped = query
                .GroupBy(m => m.moderator_id!.Value)
                .Select(g => new
                {
                    ModeratorId = g.Key,
                    ApprovedCount = g.Count(m => m.action != null && m.action.ToUpper() == "APPROVED"),
                    RejectedCount = g.Count(m => m.action != null && m.action.ToUpper() == "REJECTED"),
                    StoryApprovedCount = g.Count(m => m.action != null && m.action.ToUpper() == "APPROVED" && m.target_type == ttStory),
                    StoryRejectedCount = g.Count(m => m.action != null && m.action.ToUpper() == "REJECTED" && m.target_type == ttStory),
                    ChapterApprovedCount = g.Count(m => m.action != null && m.action.ToUpper() == "APPROVED" && m.target_type == ttChapter),
                    ChapterRejectedCount = g.Count(m => m.action != null && m.action.ToUpper() == "REJECTED" && m.target_type == ttChapter)
                })
                .ToList();

            return grouped.Select(x => new ModeratorPerformanceStatsRow
            {
                ModeratorId = x.ModeratorId,
                ApprovedCount = x.ApprovedCount,
                RejectedCount = x.RejectedCount,
                StoryApprovedCount = x.StoryApprovedCount,
                StoryRejectedCount = x.StoryRejectedCount,
                ChapterApprovedCount = x.ChapterApprovedCount,
                ChapterRejectedCount = x.ChapterRejectedCount
            }).ToList();
        }
    }

    public sealed class ModeratorPerformanceStatsRow
    {
        public Guid ModeratorId { get; set; }
        public int ApprovedCount { get; set; }
        public int RejectedCount { get; set; }
        public int StoryApprovedCount { get; set; }
        public int StoryRejectedCount { get; set; }
        public int ChapterApprovedCount { get; set; }
        public int ChapterRejectedCount { get; set; }
    }
}
