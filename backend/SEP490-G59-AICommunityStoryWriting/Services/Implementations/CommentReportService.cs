using System.Collections.Generic;
using System.Linq;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.EntityFrameworkCore;
using Services;
using Services.DTOs.CommentReports;
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

    private bool HasPendingAdminActionForCommentThread(Guid commentId)
    {
        var comment = CommentDAO.GetById(commentId);
        if (comment == null) return false;
        var storyId = comment.story_id ?? Guid.Empty;
        var targetUserId = comment.user_id ?? Guid.Empty;
        if (storyId == Guid.Empty || targetUserId == Guid.Empty) return false;

        return ComplianceAdminActionRequestDAO.HasPendingForStoryAndKind(
                   storyId,
                   ComplianceAdminActionRequestDAO.KindBanUser,
                   targetUserId)
               || ComplianceAdminActionRequestDAO.HasPendingForStoryAndKind(
                   storyId,
                   ComplianceAdminActionRequestDAO.KindSuspendAuthorWriting,
                   targetUserId);
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
        if (!CommentReportReasonCatalog.TryGet(request.ReasonCode, out _))
            throw new ArgumentException("Invalid reason code.");

        var comment = CommentDAO.GetById(commentId) ?? throw new InvalidOperationException("Comment not found.");

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

        if (HasPendingAdminActionForCommentThread(r.target_id))
            throw new InvalidOperationException("Đã gửi yêu cầu lên admin, không thể thao tác thêm trên comment report này.");

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
                includeReplies: includeReplies);
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
        bool includeReplies)
    {
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
                hidden ? "Compliance ẩn comment." : "Compliance hiện lại comment.",
                null);
        }
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

        var targetUserId = dto.TargetUserId ?? comment.user_id.Value;
        var kind = dto.RequestKind.Trim().ToUpperInvariant();
        var sourceTag = $"[COMMENT_REPORT:{commentId}]";
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

        await Task.Yield();
        return id;
    }

    public Task<ComplianceClaimCommentResultDto> ClaimCommentReportsAsync(
        Guid commentId,
        Guid complianceUserId)
    {
        if (HasPendingAdminActionForCommentThread(commentId))
            throw new InvalidOperationException("Đã gửi yêu cầu lên admin, không thể thao tác thêm trên comment report này.");

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
        if (HasPendingAdminActionForCommentThread(commentId))
            throw new InvalidOperationException("Đã gửi yêu cầu lên admin, không thể thao tác thêm trên comment report này.");

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

        if (HasPendingAdminActionForCommentThread(commentId))
            throw new InvalidOperationException("Đã gửi yêu cầu lên admin, không thể thao tác thêm trên comment report này.");

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
            await SetCommentThreadHiddenAsync(commentId, complianceUserId, hidden: true, includeReplies);
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

        return openReports.Count;
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
        bool viewerIsAdmin = false)
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
        if (term != null)
        {
            openReports = openReports
                .Where(r =>
                    (r.ReasonCode ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (r.Description ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)
                    || r.ReportId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

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

            if (term != null)
            {
                closedReportsForClaimed = closedReportsForClaimed
                    .Where(r =>
                        (r.ReasonCode ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)
                        || (r.Description ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)
                        || r.ReportId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
                    .ToList();
            }

            if (closedReportsForClaimed.Count > 0)
                openReports.AddRange(closedReportsForClaimed);
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
            .Select(r => new { ReportId = r.id, CommentId = r.target_id, Status = r.status })
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

        var evidenceForPage = new List<(Guid ReportId, string? EvidenceText)>();
        if (reportIdsForPage.Count > 0)
        {
            evidenceForPage = await context.report_evidences.AsNoTracking()
                .Where(e => e.report_id != null && reportIdsForPage.Contains(e.report_id.Value))
                .Select(e => new ValueTuple<Guid, string?>(e.report_id!.Value, e.evidence_text))
                .ToListAsync();
        }

        var reporterIdsByCommentId = new Dictionary<Guid, HashSet<Guid>>();
        var allReporterIds = new HashSet<Guid>();
        foreach (var ev in evidenceForPage)
        {
            if (string.IsNullOrWhiteSpace(ev.EvidenceText)) continue;
            if (!Guid.TryParse(ev.EvidenceText, out var reporterId)) continue;
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

        var comments = await context.comments.AsNoTracking()
            .Include(c => c.userNavigation)
                .ThenInclude(u => u!.user_profiles)
            .Where(c => commentIds.Contains(c.id))
            .ToListAsync();

        // Nếu compliance đã gửi đơn lên admin (PENDING) với BAN_USER / SUSPEND_AUTHOR_WRITING
        // cho (storyId + targetUserId) liên quan comment thread này,
        // thì comment report thread đó sẽ không cho phép thao tác tiếp trong UI.
        var pendingAdminKeySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        {
            var storyIdsForPending = comments
                .Select(c => c.story_id ?? Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            var targetUserIdsForPending = comments
                .Select(c => c.user_id ?? Guid.Empty)
                .Where(id => id != Guid.Empty)
                .Distinct()
                .ToList();

            if (storyIdsForPending.Count > 0 && targetUserIdsForPending.Count > 0)
            {
                var pendingRows = await context.compliance_admin_action_requests.AsNoTracking()
                    .Where(x =>
                        (x.status ?? "").Trim().ToUpper() == ComplianceAdminActionRequestDAO.StatusPending
                        && storyIdsForPending.Contains(x.story_id)
                        && targetUserIdsForPending.Contains(x.target_user_id)
                        && x.request_kind != null
                        && (
                            x.request_kind.Trim().ToUpper() == ComplianceAdminActionRequestDAO.KindBanUser
                            || x.request_kind.Trim().ToUpper() == ComplianceAdminActionRequestDAO.KindSuspendAuthorWriting
                        ))
                    .Select(x => new { x.story_id, x.target_user_id })
                    .Distinct()
                    .ToListAsync();

                foreach (var row in pendingRows)
                {
                    pendingAdminKeySet.Add(row.story_id + "|" + row.target_user_id);
                }
            }
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
        var stories = await context.stories.AsNoTracking()
            .Where(s => storyIds.Contains(s.id))
            .ToDictionaryAsync(s => s.id, s => s.title);

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
                    CommentUserId = Guid.Empty,
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
                    ReasonSummaryVi = g.ReasonCounts
                        .OrderByDescending(kv => kv.Value)
                        .Select(kv => CommentReportReasonCatalog.GetDominantReasonLabelVi(kv.Key) + " (" + kv.Value + ")")
                        .ToList()
                    ,
                    HasPendingAdminActionRequest = false
                };
            }

            var storyId = comment.story_id ?? Guid.Empty;
            stories.TryGetValue(storyId, out var storyTitle);

            var commentUserId = comment.user_id ?? Guid.Empty;
            var displayName = comment.userNavigation?.user_profiles?.nickname?.Trim();
            if (string.IsNullOrWhiteSpace(displayName))
                displayName = comment.userNavigation?.email?.Trim();

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
                StoryTitle = storyTitle,
                CommentUserId = commentUserId,
                CommentUserDisplayName = displayName,
                CommentUserEmail = comment.userNavigation?.email,
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
                ReasonSummaryVi = g.ReasonCounts
                    .OrderByDescending(kv => kv.Value)
                    .Select(kv => CommentReportReasonCatalog.GetDominantReasonLabelVi(kv.Key) + " (" + kv.Value + ")")
                    .ToList(),
                HasAdminOrModeratorReplyInThread = warningByCommentId.TryGetValue(comment.id, out var w) && w.HasStaff,
                AdminOrModeratorReplyWarningVi = warningByCommentId.TryGetValue(comment.id, out var w2) ? w2.Note : null,
                HasPendingAdminActionRequest = pendingAdminKeySet.Contains(storyId + "|" + commentUserId)
            };
        }).ToList();

        return new PagedComplianceCommentReportsDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            Rows = rows
        };
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

