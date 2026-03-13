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
    }
}
