using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    /// <summary>DAO cho review_assignments: lock "Nhận duyệt", queue ai gửi trước duyệt trước.</summary>
    public static class ReviewAssignmentDAO
    {
        public const string StatusClaimed = "CLAIMED";
        public const string StatusCompleted = "COMPLETED";
        public const string TargetTypeStory = "STORY";
        public const string TargetTypeChapter = "CHAPTER";

        /// <summary>Lấy danh sách target_id đang bị lock bởi moderator KHÁC (để loại khỏi queue của moderator hiện tại).</summary>
        public static List<Guid> GetLockedTargetIdsByOthers(string targetType, Guid currentModeratorId)
        {
            using var context = new StoryPlatformDbContext();
            return context.review_assignments
                .AsNoTracking()
                .Where(r => r.target_type == targetType && r.status == StatusClaimed && r.assignee_id != currentModeratorId)
                .Select(r => r.target_id)
                .Distinct()
                .ToList();
        }

        /// <summary>Lấy tất cả target_id đang được claim (bởi bất kỳ ai). Dùng cho lọc "chưa nhận".</summary>
        public static List<Guid> GetLockedTargetIds(string targetType)
        {
            using var context = new StoryPlatformDbContext();
            return context.review_assignments
                .AsNoTracking()
                .Where(r => r.target_type == targetType && r.status == StatusClaimed)
                .Select(r => r.target_id)
                .Distinct()
                .ToList();
        }

        /// <summary>Lấy target_id đang được claim bởi userId. Null userId (ADMIN) = tất cả đã claim.</summary>
        public static List<Guid> GetClaimedTargetIdsByUser(string targetType, Guid? userId)
        {
            using var context = new StoryPlatformDbContext();
            var query = context.review_assignments
                .AsNoTracking()
                .Where(r => r.target_type == targetType && r.status == StatusClaimed);
            if (userId.HasValue)
                query = query.Where(r => r.assignee_id == userId.Value);
            return query.Select(r => r.target_id).Distinct().ToList();
        }

        /// <summary>Lấy assignment đang active (CLAIMED) cho target, nếu có.</summary>
        public static review_assignments? GetActiveAssignment(string targetType, Guid targetId)
        {
            using var context = new StoryPlatformDbContext();
            return context.review_assignments
                .AsNoTracking()
                .FirstOrDefault(r => r.target_type == targetType && r.target_id == targetId && r.status == StatusClaimed);
        }

        /// <summary>Thông tin claim: assignee id, thời điểm nhận, tên hiển thị người duyệt (nickname hoặc email).</summary>
        public static (Guid AssigneeId, DateTime AssignedAt, string DisplayName)? GetClaimInfo(string targetType, Guid targetId)
        {
            using var context = new StoryPlatformDbContext();
            var assignment = context.review_assignments
                .AsNoTracking()
                .Include(r => r.assignee)
                .ThenInclude(u => u.user_profiles)
                .FirstOrDefault(r => r.target_type == targetType && r.target_id == targetId && r.status == StatusClaimed);
            if (assignment?.assignee == null)
                return null;
            var name = assignment.assignee.user_profiles?.nickname?.Trim();
            if (string.IsNullOrEmpty(name))
                name = assignment.assignee.email;
            return (assignment.assignee_id, assignment.assigned_at, name ?? "–");
        }

        /// <summary>Kiểm tra target đã bị claim chưa (bởi bất kỳ ai).</summary>
        public static bool IsLocked(string targetType, Guid targetId)
        {
            return GetActiveAssignment(targetType, targetId) != null;
        }

        /// <summary>Moderator "nhận duyệt" → tạo assignment (lock). Trả về true nếu claim thành công.</summary>
        public static bool TryClaim(string targetType, Guid targetId, Guid moderatorId, string assigneeRole = "MODERATOR")
        {
            using var context = new StoryPlatformDbContext();
            var alreadyClaimed = context.review_assignments
                .Any(r => r.target_type == targetType && r.target_id == targetId && r.status == StatusClaimed);
            if (alreadyClaimed)
                return false;

            context.review_assignments.Add(new review_assignments
            {
                id = Guid.NewGuid(),
                target_type = targetType,
                target_id = targetId,
                assignee_id = moderatorId,
                assignee_role = assigneeRole,
                status = StatusClaimed,
                priority = 0,
                assigned_at = DateTime.UtcNow
            });
            context.SaveChanges();
            return true;
        }

        /// <summary>Đánh dấu assignment hoàn thành (sau khi approve/reject).</summary>
        public static void CompleteAssignment(string targetType, Guid targetId)
        {
            using var context = new StoryPlatformDbContext();
            var assignment = context.review_assignments
                .FirstOrDefault(r => r.target_type == targetType && r.target_id == targetId && r.status == StatusClaimed);
            if (assignment != null)
            {
                assignment.status = StatusCompleted;
                assignment.completed_at = DateTime.UtcNow;
                context.SaveChanges();
            }
        }

        /// <summary>Kiểm tra target có đang được assign cho moderator này không.</summary>
        public static bool IsAssignedTo(string targetType, Guid targetId, Guid moderatorId)
        {
            using var context = new StoryPlatformDbContext();
            return context.review_assignments
                .Any(r => r.target_type == targetType && r.target_id == targetId && r.status == StatusClaimed && r.assignee_id == moderatorId);
        }
    }
}
