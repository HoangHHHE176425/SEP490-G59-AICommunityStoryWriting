using System.Linq;
using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.EntityFrameworkCore;
using Repositories;
using Services;
using Services.DTOs.Notifications;
using Services.DTOs.Admin;
using Services.DTOs.Moderation;
using Services.Interfaces;

namespace Services.Implementations
{
    public class ReviewEscalationService : IReviewEscalationService
    {
        /// <summary>Từ chối đơn escalation: ghi chú admin bắt buộc, tối thiểu ký tự (đồng bộ FE).</summary>
        private const int AdminRejectNoteMinLength = 10;
        private const int MinHoursUntilDeadline = 24;
        private const int MaxDeadlineDaysAhead = 366;
        private const double WarningDaysThreshold = 2; // escalation list tier (admin)

        private readonly IStoryRepository _storyRepository;
        private readonly IChapterRepository _chapterRepository;
        private readonly IChapterVersionRepository _versionRepository;
        private readonly IModerationHubNotifier? _moderationHubNotifier;
        private readonly INotificationHubNotifier? _notificationHubNotifier;

        public ReviewEscalationService(
            IStoryRepository storyRepository,
            IChapterRepository chapterRepository,
            IChapterVersionRepository versionRepository,
            IModerationHubNotifier? moderationHubNotifier = null,
            INotificationHubNotifier? notificationHubNotifier = null)
        {
            _storyRepository = storyRepository;
            _chapterRepository = chapterRepository;
            _versionRepository = versionRepository;
            _moderationHubNotifier = moderationHubNotifier;
            _notificationHubNotifier = notificationHubNotifier;
        }

        public ReviewAssignmentSelfDto GetSelfAssignment(string targetType, Guid targetId, Guid userId)
        {
            var tt = NormalizeTargetType(targetType);
            var assigned = ReviewAssignmentDAO.IsAssignedTo(tt, targetId, userId);
            DateTime? deadline = null;
            if (assigned)
            {
                var info = ReviewAssignmentDAO.GetClaimInfo(tt, targetId);
                if (info.HasValue)
                    deadline = info.Value.ReviewDeadlineAt ?? info.Value.AssignedAt.AddDays(7);
            }

            var authorSubmitted = GetAuthorSubmissionUtcForReviewTarget(tt, targetId);
            DateTime? policySuggested = authorSubmitted.HasValue
                ? authorSubmitted.Value.AddDays(ModeratorReviewSlaHelper.PolicyDaysAfterAuthorSubmit)
                : null;
            var effectiveFallback = deadline ?? policySuggested;

            return new ReviewAssignmentSelfDto
            {
                IsAssignedToMe = assigned,
                ReviewDeadlineAt = ApiDateTime.AsUtcForJson(deadline),
                AuthorSubmittedAtUtc = ApiDateTime.AsUtcForJson(authorSubmitted),
                PolicySuggestedDeadlineAt = ApiDateTime.AsUtcForJson(policySuggested),
                TimeStatus = ModeratorReviewSlaHelper.ComputeSlaTimeStatus(authorSubmitted, effectiveFallback),
                HasPendingEscalation = ReviewEscalationDAO.HasPendingForTarget(tt, targetId)
            };
        }

        public Guid Submit(Guid senderId, ModeratorSubmitReviewEscalationDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));
            var tt = NormalizeTargetType(dto.TargetType);
            var kind = (dto.RequestKind ?? "").Trim().ToUpperInvariant();

            var assignedOk = ReviewAssignmentDAO.IsAssignedTo(tt, dto.TargetId, senderId);
            if (kind == ReviewEscalationDAO.KindRelease
                && string.Equals(tt, ReviewAssignmentDAO.TargetTypeStory, StringComparison.OrdinalIgnoreCase)
                && !assignedOk)
            {
                assignedOk = SenderHoldsAnyStoryOrChapterClaimOnStory(dto.TargetId, senderId);
            }

            if (!assignedOk)
                throw new InvalidOperationException("Chỉ moderator đang nhận duyệt mục này mới được gửi báo cáo.");
            if (ReviewEscalationDAO.HasPendingForTarget(tt, dto.TargetId))
                throw new InvalidOperationException("Đã có đơn chờ admin xử lý cho mục này.");
            if (kind == ReviewEscalationDAO.KindRelease
                && string.Equals(tt, ReviewAssignmentDAO.TargetTypeStory, StringComparison.OrdinalIgnoreCase))
            {
                foreach (var chId in ReviewAssignmentDAO.GetMyClaimedChapterIdsForStoryOrderedByOrderIndexDesc(dto.TargetId, senderId))
                {
                    if (ReviewEscalationDAO.HasPendingForTarget(ReviewAssignmentDAO.TargetTypeChapter, chId))
                        throw new InvalidOperationException("Có chương đang có đơn báo cáo chờ quản trị viên xử lý. Vui lòng chờ xử lý hoặc rút đơn chương trước khi gửi đơn trả cả truyện về hàng đợi.");
                }
            }

            if (!TargetStillInReviewQueue(tt, dto.TargetId))
                throw new InvalidOperationException("Nội dung không còn ở trạng thái chờ duyệt phù hợp.");
            if (kind != ReviewEscalationDAO.KindExtend && kind != ReviewEscalationDAO.KindRelease)
                throw new ArgumentException("requestKind phải là EXTEND_DEADLINE hoặc RELEASE_ASSIGNMENT.");
            var reason = (dto.Reason ?? "").Trim();
            if (reason.Length < 10)
                throw new ArgumentException("Lý do báo cáo cần ít nhất 10 ký tự.");

            DateTime? proposed = null;
            if (kind == ReviewEscalationDAO.KindExtend)
            {
                if (!dto.ProposedDeadlineAt.HasValue)
                    throw new ArgumentException("Gia hạn cần gửi proposedDeadlineAt.");
                proposed = NormalizeToUtc(dto.ProposedDeadlineAt.Value);
                ValidateNewDeadline(proposed.Value);
                var claim = ReviewAssignmentDAO.GetClaimInfo(tt, dto.TargetId);
                if (claim.HasValue)
                {
                    var currentDeadline = claim.Value.ReviewDeadlineAt ?? claim.Value.AssignedAt.AddDays(7);
                    currentDeadline = NormalizeToUtc(currentDeadline);
                    if (proposed.Value <= currentDeadline)
                        throw new ArgumentException("Hạn đề xuất gia hạn phải muộn hơn hạn duyệt hiện tại của bạn (hạn đã chọn khi nhận duyệt).");
                }
            }

            var row = new review_escalation_requests
            {
                id = Guid.NewGuid(),
                target_type = tt,
                target_id = dto.TargetId,
                sender_id = senderId,
                request_kind = kind,
                reason = reason,
                proposed_deadline_at = proposed,
                status = ReviewEscalationDAO.StatusPending,
                created_at = DateTime.UtcNow,
                sender_urgency_tier = EscalationUrgencyHelper.TierForModeratorRequestKind(kind)
            };
            ReviewEscalationDAO.Insert(row);
            _ = NotifyAdminsModeratorEscalationRequestedAsync(dto.TargetId, senderId, kind, reason);
            return row.id;
        }

        private async Task NotifyAdminsModeratorEscalationRequestedAsync(Guid targetId, Guid senderId, string requestKind, string reason)
        {
            try
            {
                await using var db = new StoryPlatformDbContext();
                var adminIds = await db.users.AsNoTracking()
                    .Where(u => u.role != null && u.role.ToUpper() == "ADMIN" && u.status != null && u.status.ToUpper() == "ACTIVE")
                    .Select(u => u.id)
                    .Distinct()
                    .ToListAsync();
                if (adminIds.Count == 0) return;

                var senderName = NotificationDAO.GetUserDisplayName(senderId);
                var kindVi = string.Equals(requestKind, ReviewEscalationDAO.KindExtend, StringComparison.OrdinalIgnoreCase)
                    ? "xin gia hạn duyệt"
                    : "xin trả đơn về hàng đợi";

                foreach (var adminId in adminIds)
                {
                    var n = new notifications
                    {
                        id = Guid.NewGuid(),
                        user_id = adminId,
                        type = "MODERATOR_ESCALATION_REQUESTED",
                        title = "Có đơn mới từ kiểm duyệt viên",
                        content = $"{senderName} vừa gửi đơn {kindVi}. Lý do: {reason}",
                        link_url = "/admin?tab=review-escalations",
                        is_read = false,
                        created_at = DateTime.UtcNow
                    };
                    NotificationDAO.Add(n);
                    if (_notificationHubNotifier != null)
                    {
                        await _notificationHubNotifier.NotifyUserAsync(adminId, new NotificationDto
                        {
                            Id = n.id,
                            Type = n.type,
                            Title = n.title,
                            Content = n.content,
                            LinkUrl = n.link_url,
                            IsRead = false,
                            CreatedAt = n.created_at
                        });
                    }
                }
            }
            catch
            {
                // best effort push; không làm fail nghiệp vụ chính.
            }
        }

        public IReadOnlyList<ReviewEscalationListItemDto> ListPendingForAdmin(string? urgencyTier = null)
        {
            var rows = ReviewEscalationDAO.ListByStatus(ReviewEscalationDAO.StatusPending);
            var list = rows.Select(r => MapToListItem(r)).ToList();
            if (!string.IsNullOrWhiteSpace(urgencyTier))
            {
                var u = urgencyTier.Trim().ToUpperInvariant();
                list = list.Where(x => string.Equals(x.UrgencyTier, u, StringComparison.OrdinalIgnoreCase)).ToList();
            }
            list = list
                .OrderByDescending(x => x.UrgencyTier == "CRITICAL")
                .ThenByDescending(x => x.UrgencyTier == "HIGH")
                .ThenBy(x => x.CurrentAssignmentDeadlineAt ?? DateTime.MaxValue)
                .ThenBy(x => x.CreatedAt)
                .ToList();
            return list;
        }

        public IReadOnlyList<ReviewEscalationListItemDto> ListResolvedHistoryForAdmin(int skip = 0, int take = 200)
        {
            var rows = ReviewEscalationDAO.ListResolvedHistory(skip, take);
            return rows.Select(r => MapToListItem(r)).ToList();
        }

        public int CountResolvedHistory() => ReviewEscalationDAO.CountResolvedHistory();

        public PagedResultDto<ReviewEscalationListItemDto> SearchEscalationLogForAdmin(ReviewEscalationLogQueryDto query)
        {
            query ??= new ReviewEscalationLogQueryDto();
            var page = query.Page < 1 ? 1 : query.Page;
            var pageSize = query.PageSize is < 1 or > 100 ? 20 : query.PageSize;

            static DateTime? EndOfDayIfMidnight(DateTime? dt)
            {
                if (!dt.HasValue) return null;
                var d = dt.Value;
                if (d.TimeOfDay != TimeSpan.Zero) return d;
                return d.Date.AddDays(1).AddTicks(-1);
            }

            var createdTo = EndOfDayIfMidnight(query.CreatedTo);
            var resolvedTo = EndOfDayIfMidnight(query.ResolvedTo);

            var (rows, total) = ReviewEscalationDAO.SearchPage(
                query.Search,
                query.Status,
                query.RequestKind,
                query.TargetType,
                query.SenderId,
                query.ResolverId,
                query.CreatedFrom,
                createdTo,
                query.ResolvedFrom,
                resolvedTo,
                query.SortBy,
                query.SortOrder,
                page,
                pageSize);

            var items = rows.Select(MapToListItem).ToList();
            return new PagedResultDto<ReviewEscalationListItemDto>
            {
                Items = items,
                TotalCount = total,
                Page = page,
                PageSize = pageSize
            };
        }

        public (int critical, int high, int standard) CountPendingUrgencyBuckets()
        {
            var list = ListPendingForAdmin(null);
            return (
                list.Count(x => x.UrgencyTier == "CRITICAL"),
                list.Count(x => x.UrgencyTier == "HIGH"),
                list.Count(x => x.UrgencyTier == "STANDARD"));
        }

        public void Resolve(Guid resolverId, Guid requestId, AdminResolveReviewEscalationDto dto)
        {
            if (dto == null)
                throw new ArgumentNullException(nameof(dto));

            var row = ReviewEscalationDAO.GetByIdForUpdate(requestId);
            if (row == null)
                throw InvalidOp("Không tìm thấy đơn.");
            if (row.status != ReviewEscalationDAO.StatusPending)
                throw InvalidOp("Đơn đã được xử lý.");

            if (!dto.Approve)
            {
                var rejectNote = (dto.AdminNote ?? string.Empty).Trim();
                if (rejectNote.Length < AdminRejectNoteMinLength)
                    throw new ArgumentException($"Khi từ chối đơn, ghi chú admin bắt buộc và tối thiểu {AdminRejectNoteMinLength} ký tự.");
                if (rejectNote.Length > 2000)
                    throw new ArgumentException("Ghi chú admin không được vượt quá 2000 ký tự.");

                row.status = ReviewEscalationDAO.StatusRejected;
                row.resolver_id = resolverId;
                row.resolver_note = rejectNote;
                row.resolved_at = DateTime.UtcNow;
                ReviewEscalationDAO.UpdateRow(row);
                _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
                return;
            }

            Guid? newAssignee = dto.ReassignToUserId;
            if (newAssignee.HasValue && newAssignee.Value == Guid.Empty)
                newAssignee = null;

            // RELEASE + STORY + moderator đang giữ ít nhất một chương: một đơn duyệt = trả tất cả chương + lock truyện (nếu có).
            if (row.request_kind == ReviewEscalationDAO.KindRelease
                && string.Equals(row.target_type, ReviewAssignmentDAO.TargetTypeStory, StringComparison.OrdinalIgnoreCase))
            {
                var bulkChapterIds = ReviewAssignmentDAO.GetMyClaimedChapterIdsForStoryOrderedByOrderIndexDesc(row.target_id, row.sender_id);
                if (bulkChapterIds.Count > 0)
                {
                    if (!SenderHoldsAnyStoryOrChapterClaimOnStory(row.target_id, row.sender_id))
                        throw InvalidOp("Assignment đã thay đổi; không thể duyệt đơn này.");
                    if (newAssignee.HasValue)
                        throw InvalidOp("Đơn trả toàn bộ chương về hàng đợi không hỗ trợ giao trực tiếp cho moderator khác. Vui lòng để trống người nhận.");
                    foreach (var chId in bulkChapterIds)
                    {
                        if (ReviewAssignmentDAO.IsAssignedTo(ReviewAssignmentDAO.TargetTypeChapter, chId, row.sender_id))
                            ReviewAssignmentDAO.ReleaseClaimAndOptionallyReassign(
                                ReviewAssignmentDAO.TargetTypeChapter, chId, row.sender_id, null, null);
                    }
                    if (ReviewAssignmentDAO.IsAssignedTo(ReviewAssignmentDAO.TargetTypeStory, row.target_id, row.sender_id))
                        ReviewAssignmentDAO.ReleaseClaimAndOptionallyReassign(
                            ReviewAssignmentDAO.TargetTypeStory, row.target_id, row.sender_id, null, null);
                    row.status = ReviewEscalationDAO.StatusApproved;
                    row.resolver_id = resolverId;
                    row.resolver_note = string.IsNullOrWhiteSpace(dto.AdminNote) ? null : dto.AdminNote.Trim();
                    row.confirmed_deadline_at = null;
                    row.resolved_at = DateTime.UtcNow;
                    ReviewEscalationDAO.UpdateRow(row);
                    _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
                    return;
                }
            }

            if (!ReviewAssignmentDAO.IsAssignedTo(row.target_type, row.target_id, row.sender_id))
                throw InvalidOp("Assignment đã thay đổi; không thể duyệt đơn này.");

            if (row.request_kind == ReviewEscalationDAO.KindRelease)
            {
                if (newAssignee.HasValue)
                {
                    if (newAssignee.Value == row.sender_id)
                        throw InvalidOp("Chọn người nhận duyệt khác người gửi đơn, hoặc để trống để trả về hàng đợi.");
                    if (!UserDAO.IsActiveModerator(newAssignee.Value))
                        throw InvalidOp("Chỉ có thể giao lock duyệt cho tài khoản moderator đang hoạt động.");
                    if (!dto.ConfirmedDeadlineAt.HasValue)
                        throw new ArgumentException("Khi giao cho người khác cần chọn hạn duyệt (confirmedDeadlineAt).");
                    var ddl = NormalizeToUtc(dto.ConfirmedDeadlineAt.Value);
                    var authorAt = GetAuthorSubmissionUtcForReviewTarget(row.target_type, row.target_id);
                    ValidateReassignDeadlineAgainstAuthorSubmission(ddl, authorAt);
                    ValidateNewDeadline(ddl);
                    ReviewAssignmentDAO.ReleaseClaimAndOptionallyReassign(
                        row.target_type, row.target_id, row.sender_id, newAssignee, ddl);
                    row.confirmed_deadline_at = ddl;
                }
                else
                {
                    ReviewAssignmentDAO.ReleaseClaimAndOptionallyReassign(
                        row.target_type, row.target_id, row.sender_id, null, null);
                    row.confirmed_deadline_at = null;
                }

                row.status = ReviewEscalationDAO.StatusApproved;
                row.resolver_id = resolverId;
                row.resolver_note = string.IsNullOrWhiteSpace(dto.AdminNote) ? null : dto.AdminNote.Trim();
                row.resolved_at = DateTime.UtcNow;
                ReviewEscalationDAO.UpdateRow(row);
                _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
                return;
            }

            if (row.request_kind == ReviewEscalationDAO.KindExtend)
            {
                var deadline = dto.ConfirmedDeadlineAt.HasValue
                    ? NormalizeToUtc(dto.ConfirmedDeadlineAt.Value)
                    : (row.proposed_deadline_at ?? throw InvalidOp("Thiếu hạn gia hạn."));
                ValidateNewDeadline(deadline);
                if (!ReviewAssignmentDAO.UpdateReviewDeadline(row.target_type, row.target_id, deadline))
                    throw InvalidOp("Không cập nhật được hạn duyệt (assignment không còn active).");
                row.status = ReviewEscalationDAO.StatusApproved;
                row.resolver_id = resolverId;
                row.resolver_note = string.IsNullOrWhiteSpace(dto.AdminNote) ? null : dto.AdminNote.Trim();
                row.confirmed_deadline_at = deadline;
                row.resolved_at = DateTime.UtcNow;
                ReviewEscalationDAO.UpdateRow(row);
                _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
                return;
            }

            throw InvalidOp("Loại đơn không hợp lệ.");
        }

        private ReviewEscalationListItemDto MapToListItem(review_escalation_requests r)
        {
            var claim = ReviewAssignmentDAO.GetClaimInfo(r.target_type, r.target_id);
            DateTime? assignmentDeadline = null;
            if (claim.HasValue)
                assignmentDeadline = claim.Value.ReviewDeadlineAt ?? claim.Value.AssignedAt.AddDays(7);

            var title = ResolveTargetTitle(r.target_type, r.target_id);
            var now = DateTime.UtcNow;
            var created = r.created_at.Kind == DateTimeKind.Utc ? r.created_at : r.created_at.ToUniversalTime();
            var authorSubmitted = GetAuthorSubmissionUtcForReviewTarget(r.target_type, r.target_id);

            List<Guid>? releaseAffectedChapterIds = null;
            if (string.Equals(r.status, ReviewEscalationDAO.StatusPending, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.request_kind, ReviewEscalationDAO.KindRelease, StringComparison.OrdinalIgnoreCase)
                && string.Equals(r.target_type, ReviewAssignmentDAO.TargetTypeStory, StringComparison.OrdinalIgnoreCase))
            {
                var claimedDesc = ReviewAssignmentDAO.GetMyClaimedChapterIdsForStoryOrderedByOrderIndexDesc(r.target_id, r.sender_id);
                if (claimedDesc.Count > 0)
                    releaseAffectedChapterIds = Enumerable.Reverse(claimedDesc).ToList();
            }

            return new ReviewEscalationListItemDto
            {
                Id = r.id,
                TargetType = r.target_type,
                TargetId = r.target_id,
                TargetTitle = title,
                RequestKind = r.request_kind,
                Reason = r.reason,
                ProposedDeadlineAt = AsUtcForJson(r.proposed_deadline_at),
                Status = r.status,
                // Kind=Utc → JSON có "Z"; tránh client parse chuỗi không offset như giờ local.
                CreatedAt = AsUtcForJson(created),
                SenderId = r.sender_id,
                SenderName = NotificationDAO.GetUserDisplayName(r.sender_id),
                CurrentAssignmentDeadlineAt = AsUtcForJson(assignmentDeadline),
                AuthorSubmittedAtUtc = AsUtcForJson(authorSubmitted),
                UrgencyTier = ComputeUrgencyTier(now, assignmentDeadline, created, r.status, r.request_kind),
                ResolverId = r.resolver_id,
                ResolverName = r.resolver_id.HasValue ? NotificationDAO.GetUserDisplayName(r.resolver_id.Value) : null,
                ResolverNote = r.resolver_note,
                ResolvedAt = AsUtcForJson(r.resolved_at),
                ConfirmedDeadlineAt = AsUtcForJson(r.confirmed_deadline_at),
                ReleaseAffectedChapterIds = releaseAffectedChapterIds
            };
        }

        /// <summary>Chuẩn hóa UTC + DateTimeKind.Utc để System.Text.Json ghi ISO kèm Z.</summary>
        private static DateTime AsUtcForJson(DateTime dt)
        {
            var utc = dt.Kind == DateTimeKind.Utc ? dt : dt.ToUniversalTime();
            return DateTime.SpecifyKind(utc, DateTimeKind.Utc);
        }

        private static DateTime? AsUtcForJson(DateTime? dt) => dt.HasValue ? AsUtcForJson(dt.Value) : null;

        /// <summary>Mốc gửi duyệt: submitted_for_review_at (fallback ước lượng nếu chưa có cột / dữ liệu cũ).</summary>
        private DateTime? GetAuthorSubmissionUtcForReviewTarget(string targetType, Guid targetId) =>
            ModeratorReviewSlaHelper.GetAuthorSubmittedUtc(targetType, targetId, _storyRepository, _chapterRepository, _versionRepository);

        private static void ValidateReassignDeadlineAgainstAuthorSubmission(DateTime deadlineUtc, DateTime? authorSubmissionUtc)
        {
            if (!authorSubmissionUtc.HasValue)
                return;
            if (deadlineUtc < authorSubmissionUtc.Value)
                throw new ArgumentException("Hạn duyệt không được trước thời điểm tác giả gửi duyệt.");
        }

        private static string ComputeUrgencyTier(DateTime nowUtc, DateTime? assignmentDeadline, DateTime createdAtUtc, string status, string requestKind)
        {
            if (!string.Equals(status, ReviewEscalationDAO.StatusPending, StringComparison.OrdinalIgnoreCase))
                return "STANDARD";
            if (string.Equals(requestKind, ReviewEscalationDAO.KindRelease, StringComparison.OrdinalIgnoreCase))
                return "CRITICAL";
            var deadline = assignmentDeadline ?? createdAtUtc.AddDays(7);
            var dl = deadline.Kind == DateTimeKind.Utc ? deadline : deadline.ToUniversalTime();
            if (nowUtc > dl)
                return "CRITICAL";
            if ((nowUtc - createdAtUtc).TotalHours > 48)
                return "CRITICAL";
            if ((dl - nowUtc).TotalDays <= WarningDaysThreshold)
                return "HIGH";
            if ((nowUtc - createdAtUtc).TotalHours > 24)
                return "HIGH";
            return "STANDARD";
        }

        private static string? ResolveTargetTitle(string targetType, Guid targetId)
        {
            if (string.Equals(targetType, ReviewAssignmentDAO.TargetTypeStory, StringComparison.OrdinalIgnoreCase))
                return StoryDAO.GetById(targetId)?.title;
            if (string.Equals(targetType, ReviewAssignmentDAO.TargetTypeChapter, StringComparison.OrdinalIgnoreCase))
                return ChapterDAO.GetById(targetId)?.title;
            return null;
        }

        private static bool SenderHoldsAnyStoryOrChapterClaimOnStory(Guid storyId, Guid senderId)
        {
            if (ReviewAssignmentDAO.IsAssignedTo(ReviewAssignmentDAO.TargetTypeStory, storyId, senderId))
                return true;
            return ReviewAssignmentDAO.GetMyClaimedChapterIdsForStoryOrderedByOrderIndexDesc(storyId, senderId).Count > 0;
        }

        private bool TargetStillInReviewQueue(string targetType, Guid targetId)
        {
            if (string.Equals(targetType, ReviewAssignmentDAO.TargetTypeStory, StringComparison.OrdinalIgnoreCase))
            {
                var s = _storyRepository.GetById(targetId);
                if (s == null)
                    return false;
                if (string.Equals(s.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase))
                    return true;
                // Truyện đã xuất bản nhưng vẫn có chương / version chờ duyệt (đơn RELEASE cấp truyện từ moderator chỉ giữ chương).
                foreach (var ch in _chapterRepository.GetByStoryId(targetId))
                {
                    if (string.Equals(ch.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase))
                        return true;
                    if (_versionRepository.GetByChapterId(ch.id).Any(v =>
                            string.Equals(v.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase)))
                        return true;
                }
                return false;
            }
            if (string.Equals(targetType, ReviewAssignmentDAO.TargetTypeChapter, StringComparison.OrdinalIgnoreCase))
            {
                var chapter = _chapterRepository.GetById(targetId);
                if (chapter == null)
                    return false;
                var hasPendingVersion = _versionRepository.GetByChapterId(targetId)
                    .Any(v => string.Equals(v.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase));
                return string.Equals(chapter.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase) || hasPendingVersion;
            }
            return false;
        }

        private static string NormalizeTargetType(string targetType)
        {
            var t = (targetType ?? "").Trim().ToUpperInvariant();
            if (t == "STORY" || t == ReviewAssignmentDAO.TargetTypeStory)
                return ReviewAssignmentDAO.TargetTypeStory;
            if (t == "CHAPTER" || t == ReviewAssignmentDAO.TargetTypeChapter)
                return ReviewAssignmentDAO.TargetTypeChapter;
            throw new ArgumentException("targetType phải là STORY hoặc CHAPTER.");
        }

        private static DateTime NormalizeToUtc(DateTime value) => value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        private static void ValidateNewDeadline(DateTime deadlineUtc)
        {
            var now = DateTime.UtcNow;
            if (deadlineUtc <= now.AddHours(MinHoursUntilDeadline))
                throw new ArgumentException("Hạn mới phải sau ít nhất 24 giờ kể từ hiện tại.");
            if (deadlineUtc > now.AddDays(MaxDeadlineDaysAhead))
                throw new ArgumentException($"Hạn không được vượt quá {MaxDeadlineDaysAhead} ngày.");
        }

        private static InvalidOperationException InvalidOp(string m) => new(m);
    }
}
