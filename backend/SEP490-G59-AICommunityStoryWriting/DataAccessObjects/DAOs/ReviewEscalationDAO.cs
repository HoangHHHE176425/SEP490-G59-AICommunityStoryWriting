using System;
using System.Collections.Generic;
using System.Linq;
using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs
{
    public static class ReviewEscalationDAO
    {
        public const string StatusPending = "PENDING";
        public const string StatusApproved = "APPROVED";
        public const string StatusRejected = "REJECTED";
        public const string KindExtend = "EXTEND_DEADLINE";
        public const string KindRelease = "RELEASE_ASSIGNMENT";

        public static bool HasPendingForTarget(string targetType, Guid targetId)
        {
            using var context = new StoryPlatformDbContext();
            return context.review_escalation_requests
                .AsNoTracking()
                .Any(r => r.target_type == targetType && r.target_id == targetId && r.status == StatusPending);
        }

        /// <summary>Số đơn EXTEND_DEADLINE đã gửi (mọi trạng thái) kể từ lúc nhận duyệt hiện tại — giới hạn 1 lần/phiên claim.</summary>
        public static int CountExtendDeadlineRequestsForSenderSince(
            string targetType,
            Guid targetId,
            Guid senderId,
            DateTime assignmentStartedAtUtc)
        {
            var at = assignmentStartedAtUtc.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(assignmentStartedAtUtc, DateTimeKind.Utc)
                : assignmentStartedAtUtc.ToUniversalTime();

            using var context = new StoryPlatformDbContext();
            return context.review_escalation_requests
                .AsNoTracking()
                .Count(r =>
                    r.target_type == targetType
                    && r.target_id == targetId
                    && r.sender_id == senderId
                    && r.request_kind == KindExtend
                    && r.created_at >= at);
        }

        /// <summary>Tất cả target_id đang có đơn PENDING (dùng batch khi build danh sách chờ duyệt).</summary>
        public static HashSet<Guid> GetPendingTargetIds(string targetType)
        {
            using var context = new StoryPlatformDbContext();
            return context.review_escalation_requests
                .AsNoTracking()
                .Where(r => r.target_type == targetType && r.status == StatusPending)
                .Select(r => r.target_id)
                .ToHashSet();
        }

        public static review_escalation_requests Insert(review_escalation_requests row)
        {
            using var context = new StoryPlatformDbContext();
            context.review_escalation_requests.Add(row);
            context.SaveChanges();
            return row;
        }

        public static List<review_escalation_requests> ListByStatus(string status)
        {
            using var context = new StoryPlatformDbContext();
            return context.review_escalation_requests
                .AsNoTracking()
                .Where(r => r.status == status)
                .OrderByDescending(r => r.created_at)
                .ToList();
        }

        /// <summary>Đơn đã xử lý (APPROVED / REJECTED), mới nhất trước.</summary>
        public static List<review_escalation_requests> ListResolvedHistory(int skip, int take)
        {
            using var context = new StoryPlatformDbContext();
            take = take < 1 ? 100 : (take > 500 ? 500 : take);
            skip = skip < 0 ? 0 : skip;
            return context.review_escalation_requests
                .AsNoTracking()
                .Where(r => r.status == StatusApproved || r.status == StatusRejected)
                .OrderByDescending(r => r.resolved_at ?? r.created_at)
                .Skip(skip)
                .Take(take)
                .ToList();
        }

        public static int CountResolvedHistory()
        {
            using var context = new StoryPlatformDbContext();
            return context.review_escalation_requests
                .Count(r => r.status == StatusApproved || r.status == StatusRejected);
        }

        public static review_escalation_requests? GetByIdForUpdate(Guid id)
        {
            using var context = new StoryPlatformDbContext();
            return context.review_escalation_requests
                .FirstOrDefault(r => r.id == id);
        }

        public static void UpdateRow(review_escalation_requests row)
        {
            using var context = new StoryPlatformDbContext();
            context.review_escalation_requests.Update(row);
            context.SaveChanges();
        }

        /// <summary>Log đơn escalation: lọc + tìm kiếm + phân trang.</summary>
        public static (List<review_escalation_requests> Items, int TotalCount) SearchPage(
            string? search,
            string? status,
            string? requestKind,
            string? targetType,
            Guid? senderId,
            Guid? resolverId,
            DateTime? createdFrom,
            DateTime? createdTo,
            DateTime? resolvedFrom,
            DateTime? resolvedTo,
            string? sortBy,
            string? sortOrder,
            int page,
            int pageSize)
        {
            using var context = new StoryPlatformDbContext();
            var q = context.review_escalation_requests.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(status))
            {
                var st = status.Trim().ToUpperInvariant();
                q = q.Where(r => r.status != null && r.status.ToUpper() == st);
            }

            if (!string.IsNullOrWhiteSpace(requestKind))
            {
                var k = requestKind.Trim().ToUpperInvariant();
                q = q.Where(r => r.request_kind != null && r.request_kind.ToUpper() == k);
            }

            if (!string.IsNullOrWhiteSpace(targetType))
            {
                var tt = targetType.Trim().ToUpperInvariant();
                q = q.Where(r => r.target_type != null && r.target_type.ToUpper() == tt);
            }

            if (senderId.HasValue)
                q = q.Where(r => r.sender_id == senderId.Value);

            if (resolverId.HasValue)
                q = q.Where(r => r.resolver_id == resolverId.Value);

            if (createdFrom.HasValue)
                q = q.Where(r => r.created_at >= createdFrom.Value);

            if (createdTo.HasValue)
                q = q.Where(r => r.created_at <= createdTo.Value);

            if (resolvedFrom.HasValue)
                q = q.Where(r => r.resolved_at != null && r.resolved_at >= resolvedFrom.Value);

            if (resolvedTo.HasValue)
                q = q.Where(r => r.resolved_at != null && r.resolved_at <= resolvedTo.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var s = search.Trim();
                if (Guid.TryParse(s, out var g))
                {
                    q = q.Where(r =>
                        r.id == g || r.target_id == g || r.sender_id == g || r.resolver_id == g);
                }
                else
                {
                    q = q.Where(r =>
                        r.reason.Contains(s) ||
                        (r.target_type == ReviewAssignmentDAO.TargetTypeStory &&
                         context.stories.Any(st => st.id == r.target_id && st.title.Contains(s))) ||
                        (r.target_type == ReviewAssignmentDAO.TargetTypeChapter &&
                         context.chapters.Any(ch => ch.id == r.target_id && ch.title.Contains(s))));
                }
            }

            var total = q.Count();

            var sortAsc = string.Equals(sortOrder, "asc", StringComparison.OrdinalIgnoreCase);
            IQueryable<review_escalation_requests> ordered;
            if (string.Equals(sortBy, "resolved_at", StringComparison.OrdinalIgnoreCase))
            {
                ordered = sortAsc
                    ? q.OrderBy(r => r.resolved_at).ThenBy(r => r.id)
                    : q.OrderByDescending(r => r.resolved_at).ThenByDescending(r => r.id);
            }
            else
            {
                ordered = sortAsc
                    ? q.OrderBy(r => r.created_at).ThenBy(r => r.id)
                    : q.OrderByDescending(r => r.created_at).ThenByDescending(r => r.id);
            }

            page = page < 1 ? 1 : page;
            pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;
            var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).ToList();
            return (items, total);
        }

        /// <summary>
        /// Batch: REJECTED + <paramref name="requestKind"/> gần nhất theo target (moderator là sender).
        /// </summary>
        public static Dictionary<Guid, (string? Note, DateTime? ResolvedAt)> GetLatestRejectedByTargetsForSenderAndRequestKind(
            Guid senderId,
            string targetType,
            IReadOnlyCollection<Guid> targetIds,
            string requestKind)
        {
            var result = new Dictionary<Guid, (string? Note, DateTime? ResolvedAt)>();
            if (targetIds == null || targetIds.Count == 0)
                return result;

            var set = targetIds as HashSet<Guid> ?? targetIds.ToHashSet();
            using var context = new StoryPlatformDbContext();
            var rows = context.review_escalation_requests
                .AsNoTracking()
                .Where(r =>
                    r.sender_id == senderId
                    && r.target_type == targetType
                    && r.status == StatusRejected
                    && r.request_kind == requestKind
                    && set.Contains(r.target_id))
                .Select(r => new { r.target_id, r.resolver_note, r.resolved_at, r.created_at })
                .ToList();

            foreach (var g in rows.GroupBy(r => r.target_id))
            {
                var top = g.OrderByDescending(x => x.resolved_at ?? x.created_at).First();
                var at = top.resolved_at ?? top.created_at;
                result[g.Key] = (top.resolver_note, at);
            }

            return result;
        }

        /// <summary>
        /// Batch: RELEASE_ASSIGNMENT + REJECTED — lý do admin từ chối đơn hủy nhận duyệt.
        /// </summary>
        public static Dictionary<Guid, (string? Note, DateTime? ResolvedAt)> GetLatestRejectedReleaseByTargetsForSender(
            Guid senderId,
            string targetType,
            IReadOnlyCollection<Guid> targetIds) =>
            GetLatestRejectedByTargetsForSenderAndRequestKind(senderId, targetType, targetIds, KindRelease);

        /// <summary>
        /// Batch: EXTEND_DEADLINE + REJECTED — lý do admin từ chối đơn xin gia hạn.
        /// </summary>
        public static Dictionary<Guid, (string? Note, DateTime? ResolvedAt)> GetLatestRejectedExtendByTargetsForSender(
            Guid senderId,
            string targetType,
            IReadOnlyCollection<Guid> targetIds) =>
            GetLatestRejectedByTargetsForSenderAndRequestKind(senderId, targetType, targetIds, KindExtend);
    }
}
