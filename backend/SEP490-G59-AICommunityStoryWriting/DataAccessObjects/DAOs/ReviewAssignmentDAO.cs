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

        /// <summary>Lock nhận xử lý báo cáo truyện (COMPLIANCE) — target_id = story_id.</summary>
        public const string TargetTypeComplianceStoryReports = "COMPLIANCE_STORY_REPORTS";

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

        /// <summary>Kiểm tra target đã bị claim chưa (bởi bất kỳ ai).</summary>
        public static bool IsLocked(string targetType, Guid targetId)
        {
            return GetActiveAssignment(targetType, targetId) != null;
        }

        /// <summary>Moderator / compliance "nhận" → tạo assignment. <paramref name="reviewDeadlineUtc"/> null = không hạn (vd. compliance báo cáo truyện).</summary>
        public static bool TryClaim(string targetType, Guid targetId, Guid moderatorId, DateTime? reviewDeadlineUtc, string assigneeRole = "MODERATOR")
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

        /// <summary>Số assignment CLAIMED theo assignee, lọc theo <paramref name="targetType"/> (vd: COMPLIANCE_STORY_REPORTS).</summary>
        public static Dictionary<Guid, int> GetClaimedAssignmentCountsByAssigneeForTargetType(string targetType)
        {
            using var context = new StoryPlatformDbContext();
            return context.review_assignments
                .AsNoTracking()
                .Where(r => r.status == StatusClaimed && r.target_type == targetType)
                .GroupBy(r => r.assignee_id)
                .ToDictionary(g => g.Key, g => g.Count());
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

        /// <summary>
        /// Giống <see cref="ReleaseClaimAndOptionallyReassign"/> nhưng cho lock compliance báo cáo truyện (role COMPLIANCE).
        /// </summary>
        public static void ReleaseComplianceStoryClaimAndOptionallyReassign(
            Guid storyId,
            Guid expectedAssigneeId,
            Guid? newAssigneeId,
            DateTime? newDeadlineUtc)
        {
            const string targetType = TargetTypeComplianceStoryReports;
            using var context = new StoryPlatformDbContext();
            context.Database.CreateExecutionStrategy().Execute(() =>
            {
                using var tx = context.Database.BeginTransaction();
                try
                {
                    var cur = context.review_assignments
                        .FirstOrDefault(r => r.target_type == targetType && r.target_id == storyId && r.status == StatusClaimed);
                    if (cur == null || cur.assignee_id != expectedAssigneeId)
                        throw new InvalidOperationException("Assignment đã thay đổi; không thể xử lý.");

                    cur.status = StatusCompleted;
                    cur.completed_at = DateTime.UtcNow;
                    context.SaveChanges();

                    if (newAssigneeId.HasValue && newAssigneeId.Value != Guid.Empty)
                    {
                        if (!newDeadlineUtc.HasValue)
                            throw new ArgumentException("Thiếu hạn xử lý khi giao cho compliance khác.");

                        var assigneeUser = context.users.FirstOrDefault(u => u.id == newAssigneeId.Value);
                        if (assigneeUser == null)
                            throw new ArgumentException("Không tìm thấy người được giao.");
                        var roleUpper = (assigneeUser.role ?? "").ToUpperInvariant();
                        if (roleUpper != "COMPLIANCE")
                            throw new ArgumentException("Chỉ giao lock cho tài khoản COMPLIANCE.");
                        if (string.Equals(assigneeUser.status, "ACTIVE", StringComparison.OrdinalIgnoreCase) != true)
                            throw new ArgumentException("Tài khoản không hoạt động.");
                        if (newAssigneeId.Value == expectedAssigneeId)
                            throw new ArgumentException("Không giao lại cho chính người gửi yêu cầu.");

                        var dupe = context.review_assignments.Any(r => r.target_type == targetType && r.target_id == storyId && r.status == StatusClaimed);
                        if (dupe)
                            throw new InvalidOperationException("Không gán được: truyện đã có người nhận lock.");

                        context.review_assignments.Add(new review_assignments
                        {
                            id = Guid.NewGuid(),
                            target_type = targetType,
                            target_id = storyId,
                            assignee_id = newAssigneeId.Value,
                            assignee_role = "COMPLIANCE",
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
