using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.EntityFrameworkCore;
using Services;
using Services.DTOs.CommentReports;
using Services.DTOs.Notifications;
using Services.DTOs.StoryReports;
using Services.Interfaces;
using Services.StoryReporting;
using BusinessObjects;

namespace Services.Implementations;

public class CommentReportService : ICommentReportService
{
    private const string CommentTargetType = "COMMENT";
    private static readonly string[] DefaultOpenStatuses = { "NEW", "IN_REVIEW" };
    private static readonly string ComplianceTargetType = ReviewAssignmentDAO.TargetTypeComplianceCommentReports;
    private readonly IUserLookup _userLookup;
    private readonly INotificationHubNotifier? _notificationHubNotifier;

    public CommentReportService(IUserLookup userLookup, INotificationHubNotifier? notificationHubNotifier = null)
    {
        _userLookup = userLookup;
        _notificationHubNotifier = notificationHubNotifier;
    }

    private static readonly Regex CommentReportAdminMessageTagRegex = new(
        @"\[COMMENT_REPORT:([0-9a-fA-F-]{36})\]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static void AddCommentIdsFromComplianceAdminMessage(string? message, HashSet<Guid> sink)
    {
        if (string.IsNullOrEmpty(message)) return;
        foreach (Match m in CommentReportAdminMessageTagRegex.Matches(message))
        {
            if (Guid.TryParse(m.Groups[1].Value, out var id) && id != Guid.Empty)
                sink.Add(id);
        }
    }

    private static bool HasPendingAdminActionForCommentThread(Guid commentId) =>
        ComplianceAdminActionRequestDAO.HasPendingCommentReportAdminAction(commentId);

    private static bool IsAuthorWritingSuspensionStillActive(DateTime? untilUtc)
    {
        if (!untilUtc.HasValue) return false;
        var u = untilUtc.Value;
        if (u.Kind == DateTimeKind.Unspecified)
            u = DateTime.SpecifyKind(u, DateTimeKind.Utc);
        return u.ToUniversalTime() > DateTime.UtcNow;
    }

    private static async Task EnsureComplianceMayDismissOpenCommentReportsAsync(Guid commentId, StoryPlatformDbContext context)
    {
        if (ComplianceAdminActionRequestDAO.CommentThreadHasApprovedBanUserRequest(commentId))
            throw new InvalidOperationException(
                "Đã có yêu cầu chặn tài khoản (gắn thread này) được quản trị viên chấp nhận; bắt buộc chọn «Đã xử lý thành công».");

        var comment = await context.comments.AsNoTracking().FirstOrDefaultAsync(c => c.id == commentId)
            ?? throw new InvalidOperationException("Không tìm thấy bình luận.");

        var commentStatus = (comment.status ?? "").Trim().ToUpperInvariant();
        var threadHidden = commentStatus == "HIDDEN_PARENT" || commentStatus == "HIDDEN";
        if (threadHidden)
            throw new InvalidOperationException(
                "Chuỗi bình luận vẫn đang bị ẩn; hãy hoàn tác ẩn thread trước khi chọn «Không xử lý được».");

        var sid = comment.story_id;
        if (!sid.HasValue || sid.Value == Guid.Empty) return;

        if (ComplianceAdminActionRequestDAO.ListStoryIdsWithApprovedBanUserStoryCompliance(new[] { sid.Value }).Contains(sid.Value))
            throw new InvalidOperationException(
                "Đã có yêu cầu chặn tài khoản được quản trị viên chấp nhận (luồng báo cáo truyện); bắt buộc chọn «Đã xử lý thành công».");

        var story = await context.stories.AsNoTracking().FirstOrDefaultAsync(s => s.id == sid.Value)
            ?? throw new InvalidOperationException("Không tìm thấy truyện.");

        if (story.comments_disabled)
            throw new InvalidOperationException(
                "Bình luận truyện vẫn đang bị khóa; hãy mở lại bình luận trước khi chọn «Không xử lý được».");
        if (story.compliance_hidden)
            throw new InvalidOperationException(
                "Truyện vẫn đang ẩn khỏi người dùng thường; hãy hiển thị lại trước khi chọn «Không xử lý được».");
        if (story.author_id is Guid aid)
        {
            var snap = UserDAO.GetUsersModerationSnapshot(new[] { aid });
            if (snap.TryGetValue(aid, out var m) && IsAuthorWritingSuspensionStillActive(m.AuthorWritingSuspendedUntil))
                throw new InvalidOperationException(
                    "Tác giả truyện vẫn đang bị tạm khóa quyền viết; hãy mở lại trước khi chọn «Không xử lý được».");
        }
    }

    private static void EnsureNotBlockedByPendingCommentLockRelease(Guid commentId, bool actorIsAdmin)
    {
        if (actorIsAdmin) return;
        if (ComplianceReportLockRequestDAO.HasPendingForTarget(ComplianceReportLockRequestDAO.TargetTypeComment, commentId))
            throw new InvalidOperationException("Đã gửi yêu cầu admin gỡ lock; tạm không thể thao tác thêm cho thread comment này.");
    }

    public IReadOnlyList<StoryReportReasonOptionDto> GetReasonOptions()
    {
        return CommentReportReasonCatalog.All
            .Select(x => new StoryReportReasonOptionDto
            {
                Code = x.Code,
                Label = x.LabelEn,
                LabelVi = x.LabelVi,
                SeverityLevel = x.SeverityLevel,
                SeverityScore = x.SeverityScore
            })
            .ToList();
    }

    public async Task<Guid> CreateCommentReportAsync(
        Guid commentId,
        Guid reporterId,
        CreateCommentReportRequestDto request,
        Guid? expectedStoryId = null,
        Guid? expectedChapterId = null)
    {
        if (request == null) throw new ArgumentException("Request is required.");
        if (commentId == Guid.Empty)
            throw new InvalidOperationException("Không tìm thấy comment.");
        if (!CommentReportReasonCatalog.TryGet(request.ReasonCode, out _))
            throw new ArgumentException("Invalid reason code.");

        if (request.Description != null && request.Description.Length > 200)
            throw new ArgumentException("Ký tự quá dài: mô tả báo cáo tối đa 200 ký tự.");

        if (reporterId == Guid.Empty || !_userLookup.Exists(reporterId))
            throw new InvalidOperationException("USER không tồn tại.");

        var comment = CommentDAO.GetById(commentId) ?? throw new InvalidOperationException("Không tìm thấy comment.");

        if (expectedStoryId.HasValue && comment.story_id != expectedStoryId.Value)
            throw new InvalidOperationException("Comment not belong to this story.");

        if (expectedChapterId.HasValue && comment.chapter_id != expectedChapterId.Value)
            throw new InvalidOperationException("Comment not belong to this chapter.");

        if (comment.user_id is null || comment.user_id.Value == reporterId)
            throw new InvalidOperationException("Bạn không thể báo cáo bình luận của chính mình.");

        // Chỉ được report comment của role AUTHOR/USER (các role khác KHÔNG cho phép report).
        var targetUserId = comment.user_id.Value;
        await using (var roleCtx = new StoryPlatformDbContext())
        {
            var targetRole = await roleCtx.users.AsNoTracking()
                .Where(u => u.id == targetUserId)
                .Select(u => u.role)
                .FirstOrDefaultAsync();

            var roleUpper = (targetRole ?? "").Trim().ToUpperInvariant();
            if (roleUpper != "AUTHOR" && roleUpper != "USER")
                throw new InvalidOperationException("Bạn không thể báo cáo bình luận này.");
        }

        var storyId = comment.story_id ?? throw new InvalidOperationException("Comment has no story_id.");
        var story = StoryDAO.GetById(storyId) ?? throw new InvalidOperationException("Story not found.");
        var st = (story.status ?? "").Trim().ToUpperInvariant();
        if (st != "PUBLISHED")
            throw new InvalidOperationException("Chỉ có thể báo cáo bình luận của truyện đã PUBLISHED.");

        // Prevent duplicates: 1 user / 1 comment (regardless resolved status).
        // Dùng report_evidences để lưu "who reported" khi ta gộp report theo reason.
        await using var context = new StoryPlatformDbContext();

        var code = request.ReasonCode.Trim().ToUpperInvariant();
        var desc = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        var reporterIdStr = reporterId.ToString();

        var already = await context.report_evidences.AsNoTracking()
            .Where(e => e.report_id != null)
            .Join(
                context.reports.AsNoTracking(),
                e => e.report_id!.Value,
                r => r.id,
                (e, r) => new { e, r }
            )
            .AnyAsync(x =>
                x.e.evidence_text == reporterIdStr &&
                x.r.target_type == CommentTargetType &&
                x.r.target_id == commentId);

        if (!already)
        {
            // Legacy data: thời điểm trước khi dùng report_evidences để chống trùng.
            var legacyAlready = await context.reports.AsNoTracking().AnyAsync(r =>
                r.target_type == CommentTargetType &&
                r.target_id == commentId &&
                r.reporter_id == reporterId);
            already = legacyAlready;
        }

        if (already)
            throw new InvalidOperationException("Bạn đã báo cáo bình luận này trước đó.");

        // Grouping: gộp report comment theo (commentId, reasonCategory).
        // Vì chỉ vậy chúng ta mới giảm số "report rows" thay vì tạo 1 row cho mỗi user.
        var row = await context.reports.FirstOrDefaultAsync(r =>
            r.target_type == CommentTargetType &&
            r.target_id == commentId &&
            (r.status == "NEW" || r.status == "IN_REVIEW") &&
            ((r.reason_category ?? "").ToUpper()) == code);

        if (row == null)
        {
            row = new reports
            {
                id = Guid.NewGuid(),
                reporter_id = reporterId,
                target_type = CommentTargetType,
                target_id = commentId,
                reason_category = code,
                description = desc,
                status = "NEW",
                created_at = DateTime.UtcNow,
                contributor_count = 1
            };
            context.reports.Add(row);
        }
        else
        {
            row.reporter_id = reporterId; // lưu reporter mới nhất để hiển thị
            if (desc != null) row.description = desc; // cập nhật mô tả mới nhất nếu có
            row.contributor_count += 1;
        }

        // Track contributor by evidence row (để chống report trùng user/comment).
        context.report_evidences.Add(new report_evidences
        {
            id = Guid.NewGuid(),
            report_id = row.id,
            evidence_url = null,
            evidence_text = reporterIdStr
        });

        await context.SaveChangesAsync();
        _ = NotifyCommentOwnerReportedAsync(comment, reporterId, request.ReasonCode, request.Description);
        return row.id;
    }

    public async Task<bool> ComplianceResolveReportAsync(
        Guid reportId,
        Guid complianceUserId,
        ComplianceResolveCommentReportRequestDto? dto,
        bool actorIsAdmin)
    {
        var st = NormalizeResolveStatus(dto?.Status);
        var hide = dto?.HideComment ?? true;
        var includeReplies = dto?.IncludeReplies ?? true;
        // Theo yêu cầu: resolve từng report KHÔNG tự đóng ticket/ẩn thread.
        // Việc "đóng ticket" sẽ chỉ làm khi COMPLIANCE bấm bulk resolve-all-open.

        await using var context = new StoryPlatformDbContext();
        var r = await context.reports.FirstOrDefaultAsync(x => x.id == reportId);
        if (r == null) throw new InvalidOperationException("Report not found.");
        if ((r.target_type ?? "").Trim().ToUpperInvariant() != CommentTargetType)
            throw new InvalidOperationException("Invalid report target.");

        EnsureNotBlockedByPendingCommentLockRelease(r.target_id, actorIsAdmin);

        if (!actorIsAdmin && !ReviewAssignmentDAO.IsAssignedTo(ComplianceTargetType, r.target_id, complianceUserId))
            throw new InvalidOperationException("Chỉ compliance đang nhận (lock) comment này mới đánh dấu hoàn thành.");

        r.status = st;
        r.resolved_at = DateTime.UtcNow;
        r.compliance_resolved_by = complianceUserId;
        await context.SaveChangesAsync();

        // Lưu thao tác hiển thị comment giống bên story:
        // Chỉ khi RESOLVED và HideComment=true mới ẩn thread; KHÔNG tự đóng ticket ở đây.
        if (st == "RESOLVED" && hide)
        {
            await SetCommentThreadHiddenAsync(
                r.target_id,
                complianceUserId,
                hidden: true,
                includeReplies: includeReplies,
                actorIsAdmin: actorIsAdmin);
        }

        return true;
    }

    private static string NormalizeResolveStatus(string? status)
    {
        var s = (status ?? "RESOLVED").Trim().ToUpperInvariant();
        if (s is not ("RESOLVED" or "DISMISSED"))
            throw new ArgumentException("Only RESOLVED or DISMISSED.");
        return s;
    }

    public async Task SetCommentThreadHiddenAsync(
        Guid commentId,
        Guid actorUserId,
        bool hidden,
        bool includeReplies,
        bool actorIsAdmin = false)
    {
        EnsureNotBlockedByPendingCommentLockRelease(commentId, actorIsAdmin);

        var comment = CommentDAO.GetById(commentId) ?? throw new InvalidOperationException("Comment not found.");
        var scopeStoryId = comment.story_id ?? throw new InvalidOperationException("Comment has no story_id.");
        var scopeChapterId = comment.chapter_id;
        var rootStatus = hidden ? "HIDDEN_PARENT" : "APPROVED";
        var descendantStatus = hidden ? "HIDDEN" : "APPROVED";

        await using var context = new StoryPlatformDbContext();
        var scope = await context.comments
            .AsNoTracking()
            .Where(c => c.story_id == scopeStoryId && c.chapter_id == scopeChapterId)
            .Select(c => new { c.id, c.parent_id, c.user_id })
            .ToListAsync();

        var childrenByParent = new Dictionary<Guid, List<Guid>>();
        foreach (var x in scope)
        {
            if (!x.parent_id.HasValue) continue;
            if (!childrenByParent.TryGetValue(x.parent_id.Value, out var list))
            {
                list = new List<Guid>();
                childrenByParent[x.parent_id.Value] = list;
            }
            list.Add(x.id);
        }

        var toUpdate = new HashSet<Guid> { commentId };
        if (includeReplies)
        {
            var stack = new Stack<Guid>();
            stack.Push(commentId);
            while (stack.Count > 0)
            {
                var cur = stack.Pop();
                if (childrenByParent.TryGetValue(cur, out var kids))
                {
                    foreach (var k in kids)
                    {
                        if (toUpdate.Add(k))
                            stack.Push(k);
                    }
                }
            }
        }

        var ids = toUpdate.ToList();
        var rows = await context.comments.Where(c => ids.Contains(c.id)).ToListAsync();
        foreach (var row in rows)
        {
            if (row.id == commentId)
                row.status = rootStatus;
            else
                row.status = includeReplies ? descendantStatus : row.status;
        }

        await context.SaveChangesAsync();

        if (comment.user_id is Guid violatorId)
        {
            ViolationLogDAO.Insert(
                actorUserId,
                violatorId,
                "COMMENT",
                commentId,
                hidden ? "COMMENT_HIDDEN" : "COMMENT_UNHIDDEN",
                hidden ? "Đã ẩn bình luận (xử lý vi phạm)." : "Đã hiện lại bình luận.",
                null);
            _ = NotifyCommentOwnerComplianceActionAsync(comment, actorUserId, hidden);
        }
    }

    private async Task NotifyCommentOwnerReportedAsync(comments comment, Guid reporterId, string? reasonCode, string? description)
    {
        if (comment.user_id is not Guid ownerId || ownerId == Guid.Empty) return;
        if (ownerId == reporterId) return;

        try
        {
            var reporterName = NotificationDAO.GetUserDisplayName(reporterId);
            var reasonVi = CommentReportReasonCatalog.TryGet(reasonCode ?? "", out var reason)
                ? reason.LabelVi
                : (reasonCode ?? "Khác");
            var detail = string.IsNullOrWhiteSpace(description)
                ? string.Empty
                : $" Chi tiết từ người báo cáo: {description.Trim()}";
            var targetUrl = comment.story_id.HasValue ? $"/story/{comment.story_id.Value}" : "/notifications";

            var n = new notifications
            {
                id = Guid.NewGuid(),
                user_id = ownerId,
                type = "COMMENT_REPORTED_TO_OWNER",
                title = $"Người báo cáo: {reporterName}",
                content =
                    $"Bình luận của bạn vừa bị báo cáo. Người báo cáo: {reporterName}. Vi phạm: {reasonVi}.{detail}",
                link_url = targetUrl,
                is_read = false,
                created_at = DateTime.UtcNow
            };
            NotificationDAO.Add(n);

            if (_notificationHubNotifier != null)
            {
                await _notificationHubNotifier.NotifyUserAsync(ownerId, new NotificationDto
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
        catch
        {
            // best effort push; không làm fail nghiệp vụ chính.
        }
    }

    private async Task NotifyCommentOwnerComplianceActionAsync(comments comment, Guid actorUserId, bool hidden)
    {
        if (comment.user_id is not Guid ownerId || ownerId == Guid.Empty) return;

        try
        {
            var actorName = NotificationDAO.GetUserDisplayName(actorUserId);
            var targetUrl = comment.story_id.HasValue ? $"/story/{comment.story_id.Value}" : "/notifications";
            var n = new notifications
            {
                id = Guid.NewGuid(),
                user_id = ownerId,
                type = "COMPLIANCE_COMMENT_MODERATION_ACTION",
                title = hidden ? "Bình luận của bạn đã bị ẩn" : "Bình luận của bạn đã được hiển thị lại",
                content = hidden
                    ? $"Xử lý vi phạm viên {actorName} đã ẩn bình luận của bạn do vi phạm."
                    : $"Xử lý vi phạm viên {actorName} đã hiển thị lại bình luận của bạn.",
                link_url = targetUrl,
                is_read = false,
                created_at = DateTime.UtcNow
            };
            NotificationDAO.Add(n);

            if (_notificationHubNotifier != null)
            {
                await _notificationHubNotifier.NotifyUserAsync(ownerId, new NotificationDto
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
        catch
        {
            // best effort push; không làm fail nghiệp vụ chính.
        }
    }

    private async Task NotifyUserAuthorWritingSuspendedComplianceAsync(
        Guid targetUserId,
        Guid actorUserId,
        bool suspended,
        comments comment)
    {
        if (targetUserId == Guid.Empty) return;
        try
        {
            var actorName = NotificationDAO.GetUserDisplayName(actorUserId);
            var targetUrl = comment.story_id.HasValue ? $"/story/{comment.story_id.Value}" : "/notifications";
            var n = new notifications
            {
                id = Guid.NewGuid(),
                user_id = targetUserId,
                type = "COMPLIANCE_AUTHOR_WRITING_MODERATION",
                title = suspended ? "Tạm khóa quyền viết" : "Đã mở lại quyền viết",
                content = suspended
                    ? $"Xử lý vi phạm viên {actorName} đã tạm khóa quyền đăng truyện và chương của bạn."
                    : $"Xử lý vi phạm viên {actorName} đã cho phép bạn đăng truyện và chương trở lại.",
                link_url = targetUrl,
                is_read = false,
                created_at = DateTime.UtcNow
            };
            NotificationDAO.Add(n);

            if (_notificationHubNotifier != null)
            {
                await _notificationHubNotifier.NotifyUserAsync(targetUserId, new NotificationDto
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
        catch
        {
            // best effort push; không làm fail nghiệp vụ chính.
        }
    }

    public Task SetAuthorWritingSuspendedByComplianceAsync(
        Guid commentId,
        Guid actorUserId,
        SetComplianceCommentAuthorWritingSuspendedRequestDto dto,
        bool actorIsAdmin)
    {
        if (dto == null) throw new ArgumentException("Body is required.");
        var comment = CommentDAO.GetById(commentId) ?? throw new InvalidOperationException("Comment not found.");
        if (comment.user_id is null)
            throw new InvalidOperationException("Comment has no user_id.");

        if (!actorIsAdmin && !ReviewAssignmentDAO.IsAssignedTo(ComplianceTargetType, commentId, actorUserId))
            throw new InvalidOperationException("Chỉ compliance đang nhận (lock) comment này mới thực hiện được thao tác này.");

        EnsureNotBlockedByPendingCommentLockRelease(commentId, actorIsAdmin);

        var targetUserId = dto.TargetUserId ?? comment.user_id.Value;
        if (targetUserId == Guid.Empty)
            throw new ArgumentException("TargetUserId không hợp lệ.");

        if (dto.Value && UserDAO.IsAccountBanned(targetUserId))
            throw new InvalidOperationException(
                "Tài khoản này đã bị chặn; không áp dụng tạm khóa quyền viết.");

        var until = dto.Value ? DateTime.UtcNow.AddYears(100) : (DateTime?)null;
        UserDAO.SetAuthorWritingSuspendedUntil(targetUserId, until);
        ViolationLogDAO.Insert(actorUserId, targetUserId, "USER", targetUserId,
            dto.Value ? "SUSPEND_AUTHOR_WRITING" : "AUTHOR_WRITING_ENABLED",
            dto.Value ? "Tạm khóa quyền viết (compliance, báo cáo comment)." : "Đã mở lại quyền viết (compliance, báo cáo comment).",
            null);
        _ = NotifyUserAuthorWritingSuspendedComplianceAsync(targetUserId, actorUserId, dto.Value, comment);
        return Task.CompletedTask;
    }

    public async Task<Guid> RequestAdminActionAsync(
        Guid commentId,
        Guid requesterId,
        CreateComplianceAdminActionRequestDto dto,
        bool actorIsAdmin)
    {
        if (dto == null) throw new ArgumentException("Body is required.");
        if (string.IsNullOrWhiteSpace(dto.RequestKind))
            throw new ArgumentException("requestKind is required.");

        var comment = CommentDAO.GetById(commentId) ?? throw new InvalidOperationException("Comment not found.");
        var storyId = comment.story_id ?? throw new InvalidOperationException("Comment has no story_id.");
        if (comment.user_id is null) throw new InvalidOperationException("Comment has no user_id.");

        if (!actorIsAdmin && !ReviewAssignmentDAO.IsAssignedTo(ComplianceTargetType, commentId, requesterId))
            throw new InvalidOperationException("Chỉ compliance đang nhận (lock) comment này mới được gửi yêu cầu admin.");

        EnsureNotBlockedByPendingCommentLockRelease(commentId, actorIsAdmin);

        var targetUserId = dto.TargetUserId ?? comment.user_id.Value;
        var kind = dto.RequestKind.Trim().ToUpperInvariant();
        if (kind == ComplianceAdminActionRequestDAO.KindSuspendAuthorWriting)
            throw new InvalidOperationException(
                "Tạm khóa quyền viết do compliance bật/tắt trực tiếp (POST .../author-writing-suspended); không gửi đơn lên admin.");

        if (UserDAO.IsAccountBanned(targetUserId))
            throw new InvalidOperationException(
                "Tài khoản này đã bị chặn; không thể gửi yêu cầu chặn tài khoản.");

        if (string.Equals(kind, ComplianceAdminActionRequestDAO.KindBanUser, StringComparison.OrdinalIgnoreCase))
            ComplianceBanUserReasonRules.EnsureBanUserMessageOrThrow(dto.Message);

        var sourceTag = ComplianceAdminActionRequestDAO.FormatCommentReportSourceTag(commentId);
        var enrichedMessage = string.IsNullOrWhiteSpace(dto.Message)
            ? sourceTag
            : $"{sourceTag} {dto.Message.Trim()}";

        var urgencyTier = EscalationUrgencyHelper.TierForComplianceAdminActionKind(kind);

        var id = ComplianceAdminActionRequestDAO.CreatePending(
            storyId,
            targetUserId,
            kind,
            requesterId,
            enrichedMessage,
            dto.ProposedSuspendUntilUtc,
            urgencyTier);
        _ = NotifyAdminsComplianceAdminActionRequestedAsync(storyId, requesterId, kind, dto.Message);

        await Task.Yield();
        return id;
    }

    public async Task<Guid> RequestComplianceCommentLockReleaseAsync(
        Guid commentId,
        Guid requesterId,
        RequestComplianceLockReleaseDto? dto)
    {
        if (!ReviewAssignmentDAO.IsAssignedTo(ComplianceTargetType, commentId, requesterId))
            throw new InvalidOperationException("Bạn không phải người đang giữ lock comment thread này.");

        var comment = CommentDAO.GetById(commentId) ?? throw new InvalidOperationException("Comment not found.");
        var storyId = comment.story_id ?? throw new InvalidOperationException("Comment has no story_id.");

        var msg = dto?.Message;
        var id = ComplianceReportLockRequestDAO.CreatePending(
            ComplianceReportLockRequestDAO.TargetTypeComment,
            commentId,
            requesterId,
            msg);
        await NotifyAdminsComplianceCommentLockReleaseRequestedAsync(storyId, commentId, requesterId, msg);
        return id;
    }

    private async Task NotifyAdminsComplianceCommentLockReleaseRequestedAsync(
        Guid storyId,
        Guid commentId,
        Guid requesterId,
        string? reason)
    {
        try
        {
            await using var db = new StoryPlatformDbContext();
            var adminIds = await db.users.AsNoTracking()
                .Where(u => (u.role ?? "").ToUpper() == "ADMIN" && (u.status ?? "").ToUpper() == "ACTIVE")
                .Select(u => u.id)
                .Distinct()
                .ToListAsync();
            if (adminIds.Count == 0) return;

            var requesterName = NotificationDAO.GetUserDisplayName(requesterId);
            var note = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" Lý do: {reason.Trim()}";
            var content =
                $"Xử lý vi phạm viên {requesterName} vừa gửi yêu cầu gỡ lock báo cáo bình luận (comment {commentId}).{note}";

            foreach (var adminId in adminIds)
            {
                var n = new notifications
                {
                    id = Guid.NewGuid(),
                    user_id = adminId,
                    type = "COMPLIANCE_COMMENT_LOCK_RELEASE_REQUEST",
                    title = "Yêu cầu gỡ lock báo cáo comment",
                    content = content,
                    link_url = $"/story/{storyId}",
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
            // best effort
        }
    }

    private async Task NotifyAdminsComplianceAdminActionRequestedAsync(Guid storyId, Guid requesterId, string kind, string? reason)
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

            var requesterName = NotificationDAO.GetUserDisplayName(requesterId);
            var requestKindVi = string.Equals(kind, ComplianceAdminActionRequestDAO.KindSuspendAuthorWriting, StringComparison.OrdinalIgnoreCase)
                ? "tạm đình chỉ quyền viết"
                : "chặn tài khoản";
            var note = string.IsNullOrWhiteSpace(reason) ? string.Empty : $" Lý do: {reason.Trim()}";

            foreach (var adminId in adminIds)
            {
                var n = new notifications
                {
                    id = Guid.NewGuid(),
                    user_id = adminId,
                    type = "COMPLIANCE_ADMIN_ACTION_REQUESTED",
                    title = "Có đơn mới từ xử lý vi phạm viên",
                    content = $"{requesterName} vừa gửi yêu cầu {requestKindVi} từ báo cáo bình luận.{note}",
                    link_url = $"/story/{storyId}",
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

    public Task<ComplianceClaimCommentResultDto> ClaimCommentReportsAsync(
        Guid commentId,
        Guid complianceUserId)
    {
        EnsureNotBlockedByPendingCommentLockRelease(commentId, actorIsAdmin: false);

        var openCount = CountOpenCommentReports(commentId);
        if (openCount == 0)
            throw new InvalidOperationException("Không có báo cáo comment đang mở để nhận.");

        if (!ReviewAssignmentDAO.TryClaim(ComplianceTargetType, commentId, complianceUserId, reviewDeadlineUtc: null, assigneeRole: "COMPLIANCE"))
            throw new InvalidOperationException("Comment report đã được compliance khác nhận xử lý (đang lock).");

        var claim = ReviewAssignmentDAO.GetClaimInfo(ComplianceTargetType, commentId)
                   ?? throw new InvalidOperationException("Không đọc được thông tin lock vừa tạo.");

        return Task.FromResult(new ComplianceClaimCommentResultDto
        {
            OpenReportCount = openCount,
            ClaimedAtUtc = ApiDateTime.AsUtcForJson(claim.AssignedAt)
        });
    }

    public Task<int> ReleaseComplianceCommentClaimAsync(
        Guid commentId,
        Guid adminUserId)
    {
        var cur = ReviewAssignmentDAO.GetActiveAssignment(ComplianceTargetType, commentId);
        if (cur == null)
            throw new InvalidOperationException("Comment report không đang bị lock compliance.");

        ReviewAssignmentDAO.CompleteAssignment(ComplianceTargetType, commentId);
        return Task.FromResult(0);
    }

    public async Task<int> ComplianceResolveAllOpenCommentReportsAsync(
        Guid commentId,
        Guid complianceUserId,
        ComplianceResolveCommentReportRequestDto? dto,
        bool actorIsAdmin)
    {
        var st = NormalizeResolveStatus(dto?.Status);
        var hide = dto?.HideComment ?? true;
        var includeReplies = dto?.IncludeReplies ?? true;

        await using var context = new StoryPlatformDbContext();

        EnsureNotBlockedByPendingCommentLockRelease(commentId, actorIsAdmin);

        if (!actorIsAdmin && HasPendingAdminActionForCommentThread(commentId))
            throw new InvalidOperationException("Đang có yêu cầu gửi admin chờ xử lý, không thể đóng ticket comment thread này.");

        if (string.Equals(st, "DISMISSED", StringComparison.OrdinalIgnoreCase))
            await EnsureComplianceMayDismissOpenCommentReportsAsync(commentId, context);

        // Enforce lock: chỉ compliance đang nhận mới được đóng loạt.
        if (!actorIsAdmin && !ReviewAssignmentDAO.IsAssignedTo(ComplianceTargetType, commentId, complianceUserId))
            throw new InvalidOperationException("Chỉ compliance đang nhận (lock) comment này mới được đóng ticket.");

        var openReports = await context.reports
            .Where(r =>
                r.target_type == CommentTargetType
                && r.target_id == commentId
                && r.status != null
                && (r.status.Trim().ToUpper() == "NEW" || r.status.Trim().ToUpper() == "IN_REVIEW"))
            .ToListAsync();

        // Nếu không còn open report thì chỉ release lock (không ẩn lại/ẩn thêm),
        // để "lưu lại thao tác" mà compliance đã chọn ở dropdown trước đó.
        if (openReports.Count > 0 && st == "RESOLVED" && hide)
        {
            // Ẩn thread một lần; các report sẽ được mark RESOLVED.
            // SetCommentThreadHiddenAsync dùng scope story/chapter nên không phụ thuộc từng report.
            await SetCommentThreadHiddenAsync(commentId, complianceUserId, hidden: true, includeReplies, actorIsAdmin);
        }

        if (openReports.Count > 0)
        {
            foreach (var r in openReports)
            {
                r.status = st;
                r.resolved_at = DateTime.UtcNow;
                r.compliance_resolved_by = complianceUserId;
            }

            await context.SaveChangesAsync();
        }

        // Close lock khi không còn open report.
        await MaybeCompleteCommentComplianceLockWhenNoOpenReportsAsync(commentId, complianceUserId, actorIsAdmin);

        // Lấy đầy đủ người báo cáo từ các report mở vừa xử lý:
        // - reporter_id đại diện của từng row
        // - toàn bộ contributor trong report_evidences (evidence_text chứa reporterId dạng Guid)
        var reporterIds = new HashSet<Guid>(
            openReports
                .Select(r => r.reporter_id ?? Guid.Empty)
                .Where(id => id != Guid.Empty));

        var openReportIds = openReports.Select(r => r.id).Distinct().ToList();
        if (openReportIds.Count > 0)
        {
            var contributorIdRaw = await context.report_evidences.AsNoTracking()
                .Where(e => e.report_id.HasValue && openReportIds.Contains(e.report_id.Value))
                .Select(e => e.evidence_text)
                .ToListAsync();

            foreach (var raw in contributorIdRaw)
            {
                if (Guid.TryParse(raw, out var uid) && uid != Guid.Empty)
                    reporterIds.Add(uid);
            }
        }

        if (openReports.Count > 0 && reporterIds.Count > 0)
            _ = NotifyCommentReportersBulkResolvedAsync(reporterIds.ToList(), commentId, st);

        return openReports.Count;
    }

    public async Task<int> SetComplianceCommentReportEvidenceVerifiedAsync(
        Guid commentId,
        Guid actorUserId,
        SetComplianceCommentReportEvidenceVerifiedRequestDto dto,
        bool actorIsAdmin)
    {
        if (dto == null) throw new ArgumentException("Request is required.");

        var toVerify = (dto.VerifyEvidenceIds ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();
        var toUnverify = (dto.UnverifyEvidenceIds ?? Array.Empty<Guid>()).Where(id => id != Guid.Empty).Distinct().ToList();
        if (toVerify.Count == 0 && toUnverify.Count == 0)
            return 0;
        if (toVerify.Intersect(toUnverify).Any())
            throw new ArgumentException("Không được trùng evidence giữa đánh dấu và gỡ đánh dấu.");

        EnsureNotBlockedByPendingCommentLockRelease(commentId, actorIsAdmin);

        if (!actorIsAdmin && !ReviewAssignmentDAO.IsAssignedTo(ComplianceTargetType, commentId, actorUserId))
            throw new InvalidOperationException("Chỉ compliance đang nhận (lock) comment này mới được đánh dấu xác minh.");

        var allIds = toVerify.Concat(toUnverify).Distinct().ToList();

        await using var context = new StoryPlatformDbContext();
        var rows = await context.report_evidences
            .Include(e => e.report)
            .Where(e => allIds.Contains(e.id))
            .ToListAsync();

        if (rows.Count != allIds.Count)
            throw new InvalidOperationException("Một hoặc nhiều bản ghi evidence không tồn tại.");

        foreach (var e in rows)
        {
            var r = e.report;
            if (r == null
                || !string.Equals((r.target_type ?? "").Trim(), CommentTargetType, StringComparison.OrdinalIgnoreCase)
                || r.target_id != commentId)
            {
                throw new InvalidOperationException("Evidence không thuộc comment thread này.");
            }
        }

        var now = DateTime.UtcNow;
        var touched = 0;
        foreach (var e in rows)
        {
            if (toVerify.Contains(e.id))
            {
                e.compliance_verified_at_utc = now;
                e.compliance_verified_by_user_id = actorUserId;
                touched++;
            }
            else if (toUnverify.Contains(e.id))
            {
                e.compliance_verified_at_utc = null;
                e.compliance_verified_by_user_id = null;
                touched++;
            }
        }

        await context.SaveChangesAsync();
        return touched;
    }

    private async Task NotifyCommentReportersBulkResolvedAsync(IReadOnlyCollection<Guid> reporterIds, Guid commentId, string status)
    {
        if (reporterIds == null || reporterIds.Count == 0) return;
        var success = string.Equals(status, "RESOLVED", StringComparison.OrdinalIgnoreCase);
        var title = success ? "Báo cáo bình luận đã được xử lý" : "Báo cáo bình luận đã được cập nhật";
        var content = success
            ? "Đơn báo cáo bình luận bạn đã gửi đã được xử lý bởi xử lý vi phạm viên thành công."
            : "Đơn báo cáo bình luận bạn đã gửi được đánh dấu không đủ bằng chứng để xử lý.";

        foreach (var userId in reporterIds.Distinct())
        {
            try
            {
                var n = new notifications
                {
                    id = Guid.NewGuid(),
                    user_id = userId,
                    type = "COMPLIANCE_COMMENT_REPORT_BULK_RESOLVED",
                    title = title,
                    content = content,
                    link_url = $"/notifications",
                    is_read = false,
                    created_at = DateTime.UtcNow
                };
                NotificationDAO.Add(n);
                if (_notificationHubNotifier != null)
                {
                    await _notificationHubNotifier.NotifyUserAsync(userId, new NotificationDto
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
            catch
            {
                // best effort push; không làm fail nghiệp vụ chính.
            }
        }
    }

    private async Task MaybeCompleteCommentComplianceLockWhenNoOpenReportsAsync(
        Guid commentId,
        Guid complianceUserId,
        bool actorIsAdmin)
    {
        var openCount = await CountOpenCommentReportsAsync(commentId);
        if (openCount > 0) return;

        var cur = ReviewAssignmentDAO.GetActiveAssignment(ComplianceTargetType, commentId);
        if (cur == null) return;

        // Nếu admin gọi resolve thì vẫn cho phép close lock (đúng kỳ vọng "đóng ticket").
        if (actorIsAdmin || cur.assignee_id == complianceUserId)
            ReviewAssignmentDAO.CompleteAssignment(ComplianceTargetType, commentId);
    }

    private int CountOpenCommentReports(Guid commentId)
    {
        using var context = new StoryPlatformDbContext();
        return context.reports.AsNoTracking().Count(r =>
            ((r.target_type ?? "").ToUpper()) == CommentTargetType
            && r.target_id == commentId
            && r.status != null
            && (r.status.Trim().ToUpper() == "NEW" || r.status.Trim().ToUpper() == "IN_REVIEW"));
    }

    private async Task<int> CountOpenCommentReportsAsync(Guid commentId)
    {
        await using var context = new StoryPlatformDbContext();
        return await context.reports.AsNoTracking().CountAsync(r =>
            ((r.target_type ?? "").ToUpper()) == CommentTargetType
            && r.target_id == commentId
            && r.status != null
            && (r.status.Trim().ToUpper() == "NEW" || r.status.Trim().ToUpper() == "IN_REVIEW"));
    }

    public async Task<PagedComplianceCommentReportsDto> QueryComplianceOpenCommentReportsAsync(
        int page,
        int pageSize,
        string? statusCsv = null,
        string? search = null,
        Guid? actingUserId = null,
        bool viewerIsAdmin = false,
        string? claimFilter = null)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var statuses = ParseStatuses(statusCsv);
        if (statuses.Count == 0)
            statuses = DefaultOpenStatuses.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var nowUtc = DateTime.UtcNow;

        await using var context = new StoryPlatformDbContext();

        // Lấy tất cả report comment đang mở theo filter (không paginate theo report).
        // Sau đó nhóm theo commentId để tính Priority giống report story.
        var openReports = await context.reports.AsNoTracking()
            .Where(r =>
                ((r.target_type ?? "").ToUpper()) == CommentTargetType
                && r.status != null
                && statuses.Contains(r.status.Trim().ToUpper()))
            .Select(r => new
            {
                ReportId = r.id,
                CommentId = r.target_id,
                ReasonCode = r.reason_category,
                Description = r.description,
                Status = r.status,
                CreatedAtUtc = r.created_at,
                ReporterId = r.reporter_id,
                ContributorCount = r.contributor_count
            })
            .ToListAsync();

        var term = !string.IsNullOrWhiteSpace(search) ? search.Trim() : null;

        // Hành vi cần giống story:
        // Khi COMPLIANCE resolve từng report (không bulk), vẫn giữ ticket hiển thị theo lock
        // cho tới khi COMPLIANCE "Xong hết ticket" (bulk) để release assignment.
        // => Nếu một comment thread đang được claim nhưng không còn open reports,
        // vẫn đưa thread đó vào queue hiển thị.
        var openCommentIds = openReports
            .Select(r => r.CommentId)
            .Distinct()
            .ToHashSet();

        var claimedTargetIds = viewerIsAdmin
            ? ReviewAssignmentDAO.GetLockedTargetIds(ComplianceTargetType)
            : (actingUserId.HasValue
                ? ReviewAssignmentDAO.GetClaimedTargetIdsByUser(ComplianceTargetType, actingUserId)
                : new List<Guid>());

        var claimedNoOpenIds = claimedTargetIds
            .Where(id => !openCommentIds.Contains(id))
            .Distinct()
            .ToList();

        if (claimedNoOpenIds.Count > 0)
        {
            var closedReportsForClaimed = await context.reports.AsNoTracking()
                .Where(r =>
                    ((r.target_type ?? "").ToUpper()) == CommentTargetType
                    && claimedNoOpenIds.Contains(r.target_id)
                    && r.status != null)
                .Select(r => new
                {
                    ReportId = r.id,
                    CommentId = r.target_id,
                    ReasonCode = r.reason_category,
                    Description = r.description,
                    Status = r.status,
                    CreatedAtUtc = r.created_at,
                    ReporterId = r.reporter_id,
                    ContributorCount = r.contributor_count
                })
                .ToListAsync();

            if (closedReportsForClaimed.Count > 0)
                openReports.AddRange(closedReportsForClaimed);
        }

        if (term != null)
        {
            var searchCommentIds = openReports.Select(x => x.CommentId).Distinct().ToList();
            if (searchCommentIds.Count > 0)
            {
                var commentSearchRows = await context.comments.AsNoTracking()
                    .Include(c => c.userNavigation)
                        .ThenInclude(u => u!.user_profiles)
                    .Where(c => searchCommentIds.Contains(c.id))
                    .Select(c => new
                    {
                        c.id,
                        c.story_id,
                        Nick = c.userNavigation != null && c.userNavigation.user_profiles != null
                            ? c.userNavigation.user_profiles.nickname
                            : null,
                        Email = c.userNavigation != null ? c.userNavigation.email : null
                    })
                    .ToListAsync();

                var storyIdsForSearch = commentSearchRows
                    .Select(x => x.story_id ?? Guid.Empty)
                    .Where(x => x != Guid.Empty)
                    .Distinct()
                    .ToList();
                var storyTitleById = await context.stories.AsNoTracking()
                    .Where(s => storyIdsForSearch.Contains(s.id))
                    .ToDictionaryAsync(s => s.id, s => s.title ?? string.Empty);

                var matchedCommentIds = commentSearchRows
                    .Where(x =>
                    {
                        var displayName = string.IsNullOrWhiteSpace(x.Nick) ? x.Email : x.Nick;
                        var storyTitle = x.story_id.HasValue && storyTitleById.TryGetValue(x.story_id.Value, out var t)
                            ? t
                            : string.Empty;
                        return (!string.IsNullOrWhiteSpace(storyTitle)
                                && storyTitle.Contains(term, StringComparison.OrdinalIgnoreCase))
                            || (!string.IsNullOrWhiteSpace(displayName)
                                && displayName.Contains(term, StringComparison.OrdinalIgnoreCase));
                    })
                    .Select(x => x.id)
                    .ToHashSet();

                openReports = openReports
                    .Where(x => matchedCommentIds.Contains(x.CommentId))
                    .ToList();
            }
        }

        var groups = openReports
            .GroupBy(r => r.CommentId)
            .Select(g =>
            {
                var reasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                foreach (var r in g)
                {
                    var code = string.IsNullOrWhiteSpace(r.ReasonCode)
                        ? "OTHER"
                        : r.ReasonCode.Trim().ToUpperInvariant();
                    if (!CommentReportReasonCatalog.TryGet(code, out _))
                        code = "OTHER";

                    var cnt = r.ContributorCount > 0 ? r.ContributorCount : 1;
                    if (reasonCounts.TryGetValue(code, out var prev))
                        reasonCounts[code] = prev + cnt;
                    else
                        reasonCounts[code] = cnt;
                }

                var (dominantCode, aggregatedSeverity) =
                    CommentReportReasonScores.ComputeDominantAndAggregatedSeverity(reasonCounts);

                var reportCount = reasonCounts.Values.Sum();
                var oldest = g.Min(x => x.CreatedAtUtc ?? nowUtc);
                var newest = g
                    .OrderByDescending(x => x.CreatedAtUtc ?? nowUtc)
                    .First();

                var timeWeight = StoryReportPriorityCalculator.ComputeTimeWeight(oldest, nowUtc);
                var priority = StoryReportPriorityCalculator.ComputePriorityScore(
                    aggregatedSeverity,
                    reportCount,
                    timeWeight);

                var statusesPresent = g
                    .Select(x => x.Status ?? "")
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new
                {
                    CommentId = g.Key,
                    DominantCode = dominantCode,
                    AggregatedSeverity = aggregatedSeverity,
                    PriorityScore = priority,
                    ReportCount = reportCount,
                    TimeWeight = timeWeight,
                    OldestReportAtUtc = oldest,
                    Representative = newest,
                    StatusesPresent = statusesPresent,
                    ReasonCounts = reasonCounts
                };
            })
            .OrderByDescending(x => x.PriorityScore)
            .ThenBy(x => x.OldestReportAtUtc)
            .ToList();

        // Lọc theo lock (giống report truyện): all | unclaimed | mine
        var cf = (claimFilter ?? "all").Trim().ToUpperInvariant();
        var tt = ComplianceTargetType;
        if (viewerIsAdmin)
        {
            if (cf == "UNCLAIMED")
            {
                var locked = ReviewAssignmentDAO.GetLockedTargetIds(tt).ToHashSet();
                groups = groups.Where(g => !locked.Contains(g.CommentId)).ToList();
            }
            else if (cf == "MINE" && actingUserId.HasValue)
            {
                var mine = ReviewAssignmentDAO.GetClaimedTargetIdsByUser(tt, actingUserId).ToHashSet();
                groups = groups.Where(g => mine.Contains(g.CommentId)).ToList();
            }
        }
        else if (actingUserId.HasValue)
        {
            var uid = actingUserId.Value;
            if (cf == "UNCLAIMED")
            {
                var locked = ReviewAssignmentDAO.GetLockedTargetIds(tt).ToHashSet();
                groups = groups.Where(g => !locked.Contains(g.CommentId)).ToList();
            }
            else if (cf == "MINE")
            {
                var mine = ReviewAssignmentDAO.GetClaimedTargetIdsByUser(tt, actingUserId).ToHashSet();
                groups = groups.Where(g => mine.Contains(g.CommentId)).ToList();
            }
            else
            {
                var other = ReviewAssignmentDAO.GetLockedTargetIdsByOthers(tt, uid).ToHashSet();
                groups = groups.Where(g => !other.Contains(g.CommentId)).ToList();
            }
        }

        var total = groups.Count;
        var slice = groups
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var commentIds = slice.Select(x => x.CommentId).ToList();
        var claimInfos = ReviewAssignmentDAO.GetActiveClaimInfosByTargetIds(ComplianceTargetType, commentIds);

        // Lấy danh sách người đã report + summary lý do theo từng comment thread
        // (giống story: 1 row đại diện, nhưng hiển thị đầy đủ contributors & reasons).
        var reportsForPage = await context.reports.AsNoTracking()
            .Where(r =>
                ((r.target_type ?? "").ToUpper()) == CommentTargetType
                && commentIds.Contains(r.target_id)
                && r.status != null)
            .Select(r => new
            {
                ReportId = r.id,
                CommentId = r.target_id,
                Status = r.status,
                ReporterId = r.reporter_id,
                Description = r.description,
                ReasonCode = r.reason_category,
                CreatedAtUtc = r.created_at
            })
            .ToListAsync();

        // Nếu comment thread vẫn còn open report (NEW/IN_REVIEW) thì chỉ lấy evidence của open reports.
        // Ngược lại (thread chỉ còn lại các report đã RESOLVED/DISMISSED) thì lấy evidence của tất cả report.
        var reportRowsForEvidence = reportsForPage
            .Where(r =>
                openCommentIds.Contains(r.CommentId)
                    ? statuses.Contains((r.Status ?? "").Trim().ToUpper())
                    : true)
            .ToList();

        var reportIdsForPage = reportRowsForEvidence.Select(x => x.ReportId).ToList();
        var reportIdToCommentId = reportRowsForEvidence.ToDictionary(x => x.ReportId, x => x.CommentId);

        var evidenceForPage = await context.report_evidences.AsNoTracking()
            .Where(e => e.report_id != null && reportIdsForPage.Contains(e.report_id.Value))
            .Join(
                context.reports.AsNoTracking(),
                e => e.report_id!.Value,
                r => r.id,
                (e, r) => new { e, r })
            .Where(x => ((x.r.target_type ?? "").ToUpper()) == CommentTargetType)
            .Select(x => new
            {
                x.e.id,
                ReportId = x.e.report_id!.Value,
                x.e.evidence_text,
                x.e.compliance_verified_at_utc,
                CommentId = x.r.target_id,
                x.r.reason_category,
                x.r.description,
                x.r.created_at
            })
            .ToListAsync();

        var reporterIdsByCommentId = new Dictionary<Guid, HashSet<Guid>>();
        var allReporterIds = new HashSet<Guid>();
        foreach (var ev in evidenceForPage)
        {
            if (string.IsNullOrWhiteSpace(ev.evidence_text)) continue;
            if (!Guid.TryParse(ev.evidence_text, out var reporterId)) continue;
            if (!reportIdToCommentId.TryGetValue(ev.ReportId, out var cid)) continue;

            if (!reporterIdsByCommentId.TryGetValue(cid, out var set))
            {
                set = new HashSet<Guid>();
                reporterIdsByCommentId[cid] = set;
            }
            if (set.Add(reporterId))
                allReporterIds.Add(reporterId);
        }

        var reporterNameByUserId = new Dictionary<Guid, string>();
        if (allReporterIds.Count > 0)
        {
            var reporterUserRows = await context.users.AsNoTracking()
                .Include(u => u.user_profiles)
                .Where(u => allReporterIds.Contains(u.id))
                .Select(u => new
                {
                    u.id,
                    nickname = u.user_profiles != null ? u.user_profiles.nickname : null,
                    email = u.email
                })
                .ToListAsync();

            reporterNameByUserId = reporterUserRows.ToDictionary(
                x => x.id,
                x => !string.IsNullOrWhiteSpace(x.nickname) ? x.nickname!.Trim() : (x.email ?? "").Trim());
        }

        var reporterNamesByCommentId = new Dictionary<Guid, IReadOnlyList<string>>();
        foreach (var cid in commentIds)
        {
            if (!reporterIdsByCommentId.TryGetValue(cid, out var ids) || ids.Count == 0)
            {
                reporterNamesByCommentId[cid] = Array.Empty<string>();
                continue;
            }

            var names = ids
                .Select(rid =>
                    reporterNameByUserId.TryGetValue(rid, out var nm) && !string.IsNullOrWhiteSpace(nm)
                        ? nm
                        : rid.ToString())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToList();

            reporterNamesByCommentId[cid] = names;
        }

        var reporterDetailsByCommentId = evidenceForPage
            .Where(x => commentIds.Contains(x.CommentId))
            .Where(x => !string.IsNullOrWhiteSpace(x.evidence_text) && Guid.TryParse(x.evidence_text, out _))
            .GroupBy(x => x.CommentId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<ComplianceCommentReporterDetailDto>)g
                    .Select(x =>
                    {
                        var reporterId = Guid.Parse(x.evidence_text!);
                        var displayName = (reporterId != Guid.Empty
                            && reporterNameByUserId.TryGetValue(reporterId, out var nm)
                            && !string.IsNullOrWhiteSpace(nm))
                            ? nm
                            : reporterId.ToString();

                        var code = string.IsNullOrWhiteSpace(x.reason_category) ? "OTHER" : x.reason_category.Trim().ToUpperInvariant();
                        if (!CommentReportReasonCatalog.TryGet(code, out _))
                            code = "OTHER";
                        var label = CommentReportReasonCatalog.GetDominantReasonLabelVi(code);
                        return new ComplianceCommentReporterDetailDto
                        {
                            EvidenceId = x.id,
                            ReportId = x.ReportId,
                            ReporterUserId = reporterId,
                            ReporterDisplayName = displayName,
                            ReportedAtUtc = x.created_at.HasValue ? ApiDateTime.AsUtcForJson(x.created_at.Value) : null,
                            Description = x.description,
                            ReasonLabelVi = label,
                            IsComplianceEvidenceVerified = x.compliance_verified_at_utc != null
                        };
                    })
                    .OrderBy(x => x.ReportedAtUtc ?? DateTime.MinValue)
                    .ThenBy(x => x.ReporterDisplayName, StringComparer.OrdinalIgnoreCase)
                    .ToList());

        var comments = await context.comments.AsNoTracking()
            .Include(c => c.userNavigation)
                .ThenInclude(u => u!.user_profiles)
            .Where(c => commentIds.Contains(c.id))
            .ToListAsync();

        // Đơn từ comment report có tag [COMMENT_REPORT:commentId] — chỉ thread đó hiển thị pending,
        // không lan sang mọi báo cáo comment khác cùng truyện / cùng user.
        var commentIdsWithPendingCommentAdminAction = new HashSet<Guid>();
        {
            var storyIdsForPending = comments
                .Select(c => c.story_id ?? Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (storyIdsForPending.Count > 0)
            {
                var pendingRows = await context.compliance_admin_action_requests.AsNoTracking()
                    .Where(x =>
                        (x.status ?? "").Trim().ToUpper() == ComplianceAdminActionRequestDAO.StatusPending
                        && storyIdsForPending.Contains(x.story_id)
                        && x.message != null
                        && x.message.Contains(ComplianceAdminActionRequestDAO.CommentReportMessageTagPrefix)
                        && x.request_kind != null
                        && (
                            x.request_kind.Trim().ToUpper() == ComplianceAdminActionRequestDAO.KindBanUser
                            || x.request_kind.Trim().ToUpper() == ComplianceAdminActionRequestDAO.KindSuspendAuthorWriting
                        ))
                    .Select(x => x.message)
                    .ToListAsync();

                foreach (var msg in pendingRows)
                    AddCommentIdsFromComplianceAdminMessage(msg, commentIdsWithPendingCommentAdminAction);
            }
        }

        var commentIdsWithApprovedBanFromThread = new HashSet<Guid>();
        {
            var storyIdsForApprovedBan = comments
                .Select(c => c.story_id ?? Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (storyIdsForApprovedBan.Count > 0)
            {
                var approvedBanRows = await context.compliance_admin_action_requests.AsNoTracking()
                    .Where(x =>
                        (x.status ?? "").Trim().ToUpper() == ComplianceAdminActionRequestDAO.StatusApproved
                        && storyIdsForApprovedBan.Contains(x.story_id)
                        && x.message != null
                        && x.message.Contains(ComplianceAdminActionRequestDAO.CommentReportMessageTagPrefix)
                        && x.request_kind != null
                        && x.request_kind.Trim().ToUpper() == ComplianceAdminActionRequestDAO.KindBanUser)
                    .Select(x => x.message)
                    .ToListAsync();

                foreach (var msg in approvedBanRows)
                    AddCommentIdsFromComplianceAdminMessage(msg, commentIdsWithApprovedBanFromThread);
            }
        }

        var commentIdsWithPendingLockRelease = new HashSet<Guid>();
        if (commentIds.Count > 0)
        {
            var lockPend = ComplianceReportLockRequestDAO.StatusPending;
            var lockTt = ComplianceReportLockRequestDAO.TargetTypeComment;
            var lockTargets = await context.compliance_report_lock_requests.AsNoTracking()
                .Where(x =>
                    (x.status ?? "").Trim().ToUpper() == lockPend
                    && x.target_type == lockTt
                    && commentIds.Contains(x.target_id))
                .Select(x => x.target_id)
                .ToListAsync();
            foreach (var lid in lockTargets)
                commentIdsWithPendingLockRelease.Add(lid);
        }

        // Cảnh báo: nếu thread chứa reply của ADMIN/MODERATOR thì compliance khác sẽ được cảnh báo.
        // (Tính theo BFS trong phạm vi (story_id, chapter_id) của comment thread.)
        var warningByCommentId = new Dictionary<Guid, (bool HasStaff, string? Note)>();
        var rootsByScope = comments.GroupBy(c => new { storyId = c.story_id ?? Guid.Empty, chapterId = c.chapter_id })
            .ToList();

        foreach (var scope in rootsByScope)
        {
            var scopeStoryId = scope.Key.storyId;
            var scopeChapterId = scope.Key.chapterId;
            var rootIds = scope.Select(c => c.id).ToHashSet();

            var scopeComments = await context.comments.AsNoTracking()
                .Include(c => c.userNavigation)
                .Where(c => (c.story_id ?? Guid.Empty) == scopeStoryId && c.chapter_id == scopeChapterId)
                .Select(c => new { c.id, c.parent_id, role = c.userNavigation != null ? c.userNavigation.role : null })
                .ToListAsync();

            var childrenByParent = new Dictionary<Guid, List<Guid>>();
            foreach (var sc in scopeComments)
            {
                if (!sc.parent_id.HasValue) continue;
                if (!childrenByParent.TryGetValue(sc.parent_id.Value, out var list))
                {
                    list = new List<Guid>();
                    childrenByParent[sc.parent_id.Value] = list;
                }
                list.Add(sc.id);
            }

            var roleById = scopeComments.ToDictionary(x => x.id, x => x.role, EqualityComparer<Guid>.Default);

            foreach (var rootId in rootIds)
            {
                var toVisit = new Stack<Guid>();
                toVisit.Push(rootId);
                var visited = new HashSet<Guid>();
                var hasStaff = false;

                while (toVisit.Count > 0 && !hasStaff)
                {
                    var cur = toVisit.Pop();
                    if (!visited.Add(cur)) continue;

                    if (roleById.TryGetValue(cur, out var roleVal))
                    {
                        var roleUpper = (roleVal ?? "").Trim().ToUpperInvariant();
                        if (roleUpper == "ADMIN" || roleUpper == "MODERATOR")
                        {
                            hasStaff = true;
                            break;
                        }
                    }

                    if (childrenByParent.TryGetValue(cur, out var kids))
                    {
                        foreach (var k in kids)
                            toVisit.Push(k);
                    }
                }

                warningByCommentId[rootId] = new ValueTuple<bool, string?>(
                    hasStaff,
                    hasStaff ? "Cảnh báo: thread có reply của ADMIN/MODERATOR." : null);
            }
        }

        var storyIds = comments.Select(c => c.story_id ?? Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToList();
        var storyEntities = await context.stories.AsNoTracking()
            .Where(s => storyIds.Contains(s.id))
            .Select(s => new { s.id, s.title, s.comments_disabled, s.compliance_hidden, s.author_id })
            .ToListAsync();
        var stories = storyEntities.ToDictionary(x => x.id, x => x.title);
        var storyMetaById = storyEntities.ToDictionary(x => x.id);
        var storyAuthorIds = storyEntities.Where(x => x.author_id.HasValue).Select(x => x.author_id!.Value).Distinct().ToList();
        var storyAuthorSnap = storyAuthorIds.Count > 0
            ? UserDAO.GetUsersModerationSnapshot(storyAuthorIds)
            : new Dictionary<Guid, (string? Status, DateTime? AuthorWritingSuspendedUntil)>();
        var approvedBanStorySet = ComplianceAdminActionRequestDAO.ListStoryIdsWithApprovedBanUserStoryCompliance(storyIds);

        var rows = slice.Select(g =>
        {
            var comment = comments.FirstOrDefault(c => c.id == g.CommentId);
            if (comment == null)
            {
                return new ComplianceCommentReportRowDto
                {
                    ReportId = g.Representative.ReportId,
                    CommentId = g.CommentId,
                    StoryId = Guid.Empty,
                    ChapterId = null,
                    CommentUserId = Guid.Empty,
                    CommentContent = null,
                    IsCommentThreadHidden = false,
                    ReasonCode = g.DominantCode,
                    ReasonLabelVi = CommentReportReasonCatalog.GetDominantReasonLabelVi(g.DominantCode),
                    SeverityScore = g.AggregatedSeverity,
                    PriorityScore = g.PriorityScore,
                    ReportCount = g.ReportCount,
                    TimeWeight = g.TimeWeight,
                    Description = g.Representative.Description,
                    Status = g.Representative.Status,
                    ReporterId = g.Representative.ReporterId ?? Guid.Empty,
                    ReporterEmail = null,
                    CreatedAtUtc = g.OldestReportAtUtc,

                    IsComplianceLocked = false,
                    IsComplianceClaimedByMe = false,
                    ComplianceClaimedByDisplayName = null,
                    ComplianceClaimedAtUtc = null,
                    ComplianceHandlingSlaStatus = null,
                    ComplianceHandlingSlaMessageVi = null,
                    HoursSinceComplianceClaim = null,
                    ReporterDisplayNames = reporterNamesByCommentId.TryGetValue(g.CommentId, out var rn0) ? rn0 : Array.Empty<string>(),
                    ReporterDetails = reporterDetailsByCommentId.TryGetValue(g.CommentId, out var rd0) ? rd0 : Array.Empty<ComplianceCommentReporterDetailDto>(),
                    ReasonSummaryVi = g.ReasonCounts
                        .OrderByDescending(kv => kv.Value)
                        .Select(kv => CommentReportReasonCatalog.GetDominantReasonLabelVi(kv.Key) + " (" + kv.Value + ")")
                        .ToList()
                    ,
                    HasPendingAdminActionRequest = false,
                    HasPendingLockReleaseRequest = false,
                    StoryCommentsDisabled = false,
                    StoryComplianceHidden = false,
                    StoryAuthorWritingSuspendedUntilUtc = null,
                    HasApprovedAdminBanRequest = false
                };
            }

            var storyId = comment.story_id ?? Guid.Empty;
            stories.TryGetValue(storyId, out var storyTitle);

            var storyCommentsDisabled = false;
            var storyComplianceHidden = false;
            DateTime? storyAuthorWritingSuspendedUntilUtc = null;
            if (storyId != Guid.Empty && storyMetaById.TryGetValue(storyId, out var sm))
            {
                storyCommentsDisabled = sm.comments_disabled;
                storyComplianceHidden = sm.compliance_hidden;
                if (sm.author_id is Guid auid && storyAuthorSnap.TryGetValue(auid, out var au))
                    storyAuthorWritingSuspendedUntilUtc = ApiDateTime.AsUtcForJson(au.AuthorWritingSuspendedUntil);
            }

            var hasApprovedAdminBanRequest = commentIdsWithApprovedBanFromThread.Contains(comment.id)
                || (storyId != Guid.Empty && approvedBanStorySet.Contains(storyId));

            var commentUserId = comment.user_id ?? Guid.Empty;
            var displayName = comment.userNavigation?.user_profiles?.nickname?.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = comment.userNavigation?.email?.Trim();
            var commentStatus = (comment.status ?? string.Empty).Trim().ToUpperInvariant();
            var isCommentThreadHidden = commentStatus == "HIDDEN_PARENT" || commentStatus == "HIDDEN";

            var isLocked = claimInfos.TryGetValue(comment.id, out var claimInfo);
            var isClaimedByMe = actingUserId.HasValue && isLocked && claimInfo.AssigneeId == actingUserId.Value;

            (string status, string? msgVi, double hoursSince) sla = default;
            if (isLocked)
                sla = ComplianceReportHandlingSlaHelper.Compute(claimInfo.AssignedAt, nowUtc);

            return new ComplianceCommentReportRowDto
            {
                ReportId = g.Representative.ReportId,
                CommentId = comment.id,
                StoryId = storyId,
                ChapterId = comment.chapter_id,
                StoryTitle = storyTitle,
                CommentUserId = commentUserId,
                CommentUserDisplayName = displayName,
                CommentUserEmail = comment.userNavigation?.email,
                CommentContent = comment.content,
                IsCommentThreadHidden = isCommentThreadHidden,
                ReasonCode = g.DominantCode,
                ReasonLabelVi = CommentReportReasonCatalog.GetDominantReasonLabelVi(g.DominantCode),
                SeverityScore = g.AggregatedSeverity,
                PriorityScore = g.PriorityScore,
                ReportCount = g.ReportCount,
                TimeWeight = g.TimeWeight,
                Description = g.Representative.Description,
                Status = g.Representative.Status,
                ReporterId = g.Representative.ReporterId ?? Guid.Empty,
                ReporterEmail = null,
                CreatedAtUtc = g.OldestReportAtUtc,

                IsComplianceLocked = isLocked,
                IsComplianceClaimedByMe = isClaimedByMe,
                ComplianceClaimedByDisplayName = isLocked ? claimInfo.DisplayName : null,
                ComplianceClaimedAtUtc = isLocked ? ApiDateTime.AsUtcForJson(claimInfo.AssignedAt) : null,
                ComplianceHandlingSlaStatus = isLocked ? sla.status : null,
                ComplianceHandlingSlaMessageVi = isLocked ? sla.msgVi : null,
                HoursSinceComplianceClaim = isLocked ? sla.hoursSince : null,
                ReporterDisplayNames = reporterNamesByCommentId.TryGetValue(comment.id, out var rn1) ? rn1 : Array.Empty<string>(),
                ReporterDetails = reporterDetailsByCommentId.TryGetValue(comment.id, out var rd1) ? rd1 : Array.Empty<ComplianceCommentReporterDetailDto>(),
                ReasonSummaryVi = g.ReasonCounts
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => CommentReportReasonCatalog.GetDominantReasonLabelVi(kv.Key) + " (" + kv.Value + ")")
                    .ToList(),
                HasAdminOrModeratorReplyInThread = warningByCommentId.TryGetValue(comment.id, out var w) && w.HasStaff,
                AdminOrModeratorReplyWarningVi = warningByCommentId.TryGetValue(comment.id, out var w2) ? w2.Note : null,
                HasPendingAdminActionRequest = commentIdsWithPendingCommentAdminAction.Contains(comment.id),
                HasPendingLockReleaseRequest = commentIdsWithPendingLockRelease.Contains(comment.id),
                StoryCommentsDisabled = storyCommentsDisabled,
                StoryComplianceHidden = storyComplianceHidden,
                StoryAuthorWritingSuspendedUntilUtc = storyAuthorWritingSuspendedUntilUtc,
                HasApprovedAdminBanRequest = hasApprovedAdminBanRequest
            };
        }).ToList();

        EnrichCommentUserModeration(rows);

        return new PagedComplianceCommentReportsDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Rows = rows
        };
    }

    private static void EnrichCommentUserModeration(IReadOnlyList<ComplianceCommentReportRowDto> rows)
    {
        var ids = rows.Where(x => x.CommentUserId != Guid.Empty).Select(x => x.CommentUserId).Distinct().ToList();
        if (ids.Count == 0) return;
        var snap = UserDAO.GetUsersModerationSnapshot(ids);
        foreach (var row in rows)
        {
            if (row.CommentUserId == Guid.Empty || !snap.TryGetValue(row.CommentUserId, out var m)) continue;
            row.CommentUserAccountStatus = m.Status;
            row.CommentUserWritingSuspendedUntilUtc = ApiDateTime.AsUtcForJson(m.AuthorWritingSuspendedUntil);
        }
    }

    private static HashSet<string> ParseStatuses(string? csv)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(csv)) return set;
        foreach (var p in csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            set.Add(p.ToUpperInvariant());
        return set;
    }
}

