using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public static class ModerationLogDAO
    {
        public static void Add(moderation_logs log)
        {
            using var context = new StoryPlatformDbContext();
            context.moderation_logs.Add(log);
            context.SaveChanges();
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

        /// <summary>Lấy trang log kiểm duyệt (Admin) với bộ lọc. Trả về danh sách entity để controller map sang DTO và điền title/moderator name.</summary>
        public static (List<moderation_logs> Logs, int TotalCount) GetModerationLogsPage(Guid? moderatorId, DateTime? dateFrom, DateTime? dateTo, string? action, string? targetType, int page, int pageSize)
        {
            using var context = new StoryPlatformDbContext();
            var query = context.moderation_logs.AsNoTracking().Where(m => true);
            if (moderatorId.HasValue)
                query = query.Where(m => m.moderator_id == moderatorId.Value);
            if (dateFrom.HasValue)
                query = query.Where(m => m.created_at >= dateFrom.Value);
            if (dateTo.HasValue)
                query = query.Where(m => m.created_at <= dateTo.Value);
            var actionUpper = (action ?? "").Trim().ToUpperInvariant();
            var targetTypeUpper = (targetType ?? "").Trim().ToUpperInvariant();
            if (!string.IsNullOrEmpty(actionUpper))
                query = query.Where(m => m.action != null && m.action.ToUpper() == actionUpper);
            if (!string.IsNullOrEmpty(targetTypeUpper))
                query = query.Where(m => m.target_type != null && m.target_type.ToUpper() == targetTypeUpper);

            var total = query.Count();
            var list = query
                .OrderByDescending(m => m.created_at)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
            return (list, total);
        }

        /// <summary>Thống kê theo moderator: số APPROVED, REJECTED (Admin).</summary>
        public static List<(Guid ModeratorId, int ApprovedCount, int RejectedCount)> GetModeratorPerformance(DateTime? dateFrom, DateTime? dateTo)
        {
            using var context = new StoryPlatformDbContext();
            var query = context.moderation_logs
                .AsNoTracking()
                .Where(m => m.moderator_id.HasValue && m.action != null &&
                    (m.action.ToUpper() == "APPROVED" || m.action.ToUpper() == "REJECTED"));
            if (dateFrom.HasValue)
                query = query.Where(m => m.created_at >= dateFrom.Value);
            if (dateTo.HasValue)
                query = query.Where(m => m.created_at <= dateTo.Value);

            var grouped = query
                .GroupBy(m => m.moderator_id!.Value)
                .Select(g => new { ModeratorId = g.Key, Approved = g.Count(m => m.action != null && m.action.ToUpper() == "APPROVED"), Rejected = g.Count(m => m.action != null && m.action.ToUpper() == "REJECTED") })
                .ToList();
            return grouped.Select(x => (x.ModeratorId, x.Approved, x.Rejected)).ToList();
        }
    }
}
