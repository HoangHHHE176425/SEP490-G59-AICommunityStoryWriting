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

        /// <summary>Thông tin claim: assignee id, thời điểm nhận, tên hiển thị, hạn duyệt (nếu có).</summary>
        public static (Guid AssigneeId, DateTime AssignedAt, string DisplayName, DateTime? ReviewDeadlineAt)? GetClaimInfo(string targetType, Guid targetId)
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
            return (assignment.assignee_id, assignment.assigned_at, name ?? "–", assignment.review_deadline_at);
        }

        /// <summary>Batch: claim đang CLAIMED cho nhiều target (tránh N+1 khi list chapter).</summary>
        public static Dictionary<Guid, (Guid AssigneeId, DateTime AssignedAt, string DisplayName, DateTime? ReviewDeadlineAt)> GetActiveClaimInfosByTargetIds(
            string targetType,
            IReadOnlyList<Guid> targetIds)
        {
            var result = new Dictionary<Guid, (Guid, DateTime, string, DateTime?)>();
            if (targetIds == null || targetIds.Count == 0)
                return result;

            var idSet = targetIds.ToHashSet();
            using var context = new StoryPlatformDbContext();
            var rows = context.review_assignments
                .AsNoTracking()
                .Include(r => r.assignee)
                .ThenInclude(u => u.user_profiles)
                .Where(r => r.target_type == targetType && r.status == StatusClaimed && idSet.Contains(r.target_id))
                .ToList();

            foreach (var assignment in rows)
            {
                if (assignment.assignee == null)
                    continue;
                var name = assignment.assignee.user_profiles?.nickname?.Trim();
                if (string.IsNullOrEmpty(name))
                    name = assignment.assignee.email;
                result[assignment.target_id] = (assignment.assignee_id, assignment.assigned_at, name ?? "–", assignment.review_deadline_at);
            }

            return result;
        }

        /// <summary>Kiểm tra target đã bị claim chưa (bởi bất kỳ ai).</summary>
        public static bool IsLocked(string targetType, Guid targetId)
        {
            return GetActiveAssignment(targetType, targetId) != null;
        }

        /// <summary>Moderator "nhận duyệt" → tạo assignment (lock). Trả về true nếu claim thành công.</summary>
        public static bool TryClaim(string targetType, Guid targetId, Guid moderatorId, DateTime reviewDeadlineUtc, string assigneeRole = "MODERATOR")
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
                assigned_at = DateTime.UtcNow,
                review_deadline_at = reviewDeadlineUtc
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

        /// <summary>Số mục đang CLAIMED theo từng assignee (tải moderator).</summary>
        public static Dictionary<Guid, int> GetClaimedAssignmentCountsByAssignee()
        {
            using var context = new StoryPlatformDbContext();
            return context.review_assignments
                .AsNoTracking()
                .Where(r => r.status == StatusClaimed)
                .GroupBy(r => r.assignee_id)
                .ToDictionary(g => g.Key, g => g.Count());
        }

        /// <summary>Id các chương đang CLAIMED bởi moderator, thuộc <paramref name="storyId"/> — sắp xếp <c>order_index</c> giảm dần (chương số cao trước).</summary>
        public static List<Guid> GetMyClaimedChapterIdsForStoryOrderedByOrderIndexDesc(Guid storyId, Guid moderatorId)
        {
            using var context = new StoryPlatformDbContext();
            return (from r in context.review_assignments.AsNoTracking()
                    join c in context.chapters.AsNoTracking() on r.target_id equals c.id
                    where r.target_type == TargetTypeChapter && r.status == StatusClaimed
                          && r.assignee_id == moderatorId && c.story_id == storyId
                    orderby c.order_index descending
                    select c.id).ToList();
        }

        /// <summary>Kiểm tra target có đang được assign cho moderator này không.</summary>
        public static bool IsAssignedTo(string targetType, Guid targetId, Guid moderatorId)
        {
            using var context = new StoryPlatformDbContext();
            return context.review_assignments
                .Any(r => r.target_type == targetType && r.target_id == targetId && r.status == StatusClaimed && r.assignee_id == moderatorId);
        }

        /// <summary>Cập nhật hạn duyệt cho assignment đang CLAIMED (sau khi admin duyệt gia hạn).</summary>
        public static bool UpdateReviewDeadline(string targetType, Guid targetId, DateTime newDeadlineUtc)
        {
            using var context = new StoryPlatformDbContext();
            var assignment = context.review_assignments
                .FirstOrDefault(r => r.target_type == targetType && r.target_id == targetId && r.status == StatusClaimed);
            if (assignment == null)
                return false;
            assignment.review_deadline_at = newDeadlineUtc;
            context.SaveChanges();
            return true;
        }

        /// <summary>
        /// Kết thúc assignment CLAIMED hiện tại của <paramref name="expectedAssigneeId"/>; tùy chọn tạo CLAIM mới (cùng transaction).
        /// </summary>
        public static void ReleaseClaimAndOptionallyReassign(
            string targetType,
            Guid targetId,
            Guid expectedAssigneeId,
            Guid? newAssigneeId,
            DateTime? newDeadlineUtc)
        {
            using var context = new StoryPlatformDbContext();
            // SqlServerRetryingExecutionStrategy: transaction phải nằm trong Execute(...).
            context.Database.CreateExecutionStrategy().Execute(() =>
            {
                using var tx = context.Database.BeginTransaction();
                try
                {
                    var cur = context.review_assignments
                        .FirstOrDefault(r => r.target_type == targetType && r.target_id == targetId && r.status == StatusClaimed);
                    if (cur == null || cur.assignee_id != expectedAssigneeId)
                        throw new InvalidOperationException("Assignment đã thay đổi; không thể duyệt đơn này.");

                    cur.status = StatusCompleted;
                    cur.completed_at = DateTime.UtcNow;
                    context.SaveChanges();

                    if (newAssigneeId.HasValue && newAssigneeId.Value != Guid.Empty)
                    {
                        if (!newDeadlineUtc.HasValue)
                            throw new ArgumentException("Thiếu hạn duyệt khi giao cho người nhận mới.");

                        var assigneeUser = context.users.FirstOrDefault(u => u.id == newAssigneeId.Value);
                        if (assigneeUser == null)
                            throw new ArgumentException("Không tìm thấy người được giao.");
                        var roleUpper = (assigneeUser.role ?? "").ToUpperInvariant();
                        if (roleUpper != "MODERATOR")
                            throw new ArgumentException("Chỉ giao lock duyệt cho tài khoản moderator.");
                        if (string.Equals(assigneeUser.status, "ACTIVE", StringComparison.OrdinalIgnoreCase) != true)
                            throw new ArgumentException("Tài khoản không hoạt động.");

                        var dupe = context.review_assignments.Any(r => r.target_type == targetType && r.target_id == targetId && r.status == StatusClaimed);
                        if (dupe)
                            throw new InvalidOperationException("Không gán được: mục đã có người nhận duyệt.");

                        context.review_assignments.Add(new review_assignments
                        {
                            id = Guid.NewGuid(),
                            target_type = targetType,
                            target_id = targetId,
                            assignee_id = newAssigneeId.Value,
                            assignee_role = "MODERATOR",
                            status = StatusClaimed,
                            priority = 0,
                            assigned_at = DateTime.UtcNow,
                            review_deadline_at = newDeadlineUtc.Value
                        });
                        context.SaveChanges();
                    }

                    tx.Commit();
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
