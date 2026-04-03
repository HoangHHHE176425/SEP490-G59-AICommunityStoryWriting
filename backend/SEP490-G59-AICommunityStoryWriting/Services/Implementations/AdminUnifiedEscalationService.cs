using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.EntityFrameworkCore;
using Services;
using Services.DTOs.Admin;
using Services.Interfaces;

namespace Services.Implementations;

public class AdminUnifiedEscalationService : IAdminUnifiedEscalationService
{
    private const string SrcReview = "REVIEW_ESCALATION";
    private const string SrcLock = "COMPLIANCE_LOCK";
    private const string SrcAction = "COMPLIANCE_ADMIN_ACTION";

    private readonly IReviewEscalationService _reviewEscalation;
    private readonly IStoryReportService _storyReports;
    private readonly StoryPlatformDbContext _db;

    public AdminUnifiedEscalationService(
        IReviewEscalationService reviewEscalation,
        IStoryReportService storyReports,
        StoryPlatformDbContext db)
    {
        _reviewEscalation = reviewEscalation;
        _storyReports = storyReports;
        _db = db;
    }

    public async Task<AdminUnifiedEscalationPendingResponseDto> GetPendingUnifiedAsync(string? urgencyTierFilter)
    {
        var mod = _reviewEscalation.ListPendingForAdmin(null);
        var locks = await _storyReports.AdminListComplianceLockRequestsAsync(ComplianceStoryReportLockRequestDAO.StatusPending);
        var actions = await _storyReports.AdminListComplianceAdminActionRequestsAsync(ComplianceAdminActionRequestDAO.StatusPending);

        var items = new List<AdminUnifiedEscalationPendingItemDto>();
        foreach (var x in mod)
        {
            items.Add(new AdminUnifiedEscalationPendingItemDto
            {
                Source = SrcReview,
                UrgencyTier = EscalationUrgencyHelper.ToDisplayTier(x.UrgencyTier),
                ModeratorEscalation = x
            });
        }

        foreach (var x in locks)
        {
            items.Add(new AdminUnifiedEscalationPendingItemDto
            {
                Source = SrcLock,
                UrgencyTier = EscalationUrgencyHelper.ToDisplayTier(x.UrgencyTier),
                ComplianceLock = x
            });
        }

        foreach (var x in actions)
        {
            items.Add(new AdminUnifiedEscalationPendingItemDto
            {
                Source = SrcAction,
                UrgencyTier = EscalationUrgencyHelper.ToDisplayTier(x.UrgencyTier),
                ComplianceAdminAction = x
            });
        }

        var critical = items.Count(i => i.UrgencyTier == EscalationUrgencyHelper.Critical);
        var standard = items.Count(i => i.UrgencyTier == EscalationUrgencyHelper.Standard);

        if (!string.IsNullOrWhiteSpace(urgencyTierFilter))
        {
            var u = EscalationUrgencyHelper.ToDisplayTier(urgencyTierFilter.Trim());
            items = items.Where(i => string.Equals(i.UrgencyTier, u, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        items = items
            .OrderByDescending(i => i.UrgencyTier == EscalationUrgencyHelper.Critical)
            .ThenBy(GetSortKey)
            .ToList();

        return new AdminUnifiedEscalationPendingResponseDto
        {
            Items = items,
            Critical = critical,
            High = 0,
            Standard = standard
        };
    }

    public async Task<PagedResultDto<UnifiedEscalationLogItemDto>> SearchUnifiedLogAsync(UnifiedEscalationLogQueryDto query)
    {
        query ??= new UnifiedEscalationLogQueryDto();
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

        var ttStory = ReviewAssignmentDAO.TargetTypeStory;

        var modQ = _db.review_escalation_requests.AsNoTracking().Select(r => new LogUnionRow
        {
            Src = SrcReview,
            Id = r.id,
            Status = r.status,
            FilterKind = r.request_kind,
            RowTargetType = r.target_type,
            RowTargetId = r.target_id,
            Title = r.target_type == ttStory
                ? _db.stories.Where(s => s.id == r.target_id).Select(s => s.title).FirstOrDefault()
                : _db.chapters.Where(c => c.id == r.target_id).Select(c => c.title).FirstOrDefault(),
            Text = r.reason,
            SenderId = r.sender_id,
            ResolverId = r.resolver_id,
            CreatedAt = r.created_at,
            ResolvedAt = r.resolved_at,
            ResolverNote = r.resolver_note,
            StoredUrgency = r.sender_urgency_tier ?? EscalationUrgencyHelper.Standard
        });

        var lockQ = _db.compliance_story_report_lock_requests.AsNoTracking().Select(x => new LogUnionRow
        {
            Src = SrcLock,
            Id = x.id,
            Status = x.status,
            FilterKind = "LOCK_RELEASE",
            RowTargetType = ttStory,
            RowTargetId = x.story_id,
            Title = x.story != null ? x.story.title : null,
            Text = x.message,
            SenderId = x.requester_id,
            ResolverId = x.resolved_by_id,
            CreatedAt = x.created_at,
            ResolvedAt = x.resolved_at,
            ResolverNote = x.resolution_note,
            StoredUrgency = x.urgency_tier
        });

        var actQ = _db.compliance_admin_action_requests.AsNoTracking().Select(x => new LogUnionRow
        {
            Src = SrcAction,
            Id = x.id,
            Status = x.status,
            FilterKind = x.request_kind,
            RowTargetType = ttStory,
            RowTargetId = x.story_id,
            Title = x.story != null ? x.story.title : null,
            Text = x.message,
            SenderId = x.requester_id,
            ResolverId = x.resolved_by_id,
            CreatedAt = x.created_at,
            ResolvedAt = x.resolved_at,
            ResolverNote = x.resolution_note,
            StoredUrgency = x.urgency_tier
        });

        var srcFilter = (query.Source ?? "").Trim().ToUpperInvariant();
        IQueryable<LogUnionRow> combined = srcFilter switch
        {
            SrcReview => modQ,
            SrcLock => lockQ,
            SrcAction => actQ,
            _ => modQ.Concat(lockQ).Concat(actQ)
        };

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var st = query.Status.Trim().ToUpperInvariant();
            combined = combined.Where(x => x.Status != null && x.Status.ToUpper() == st);
        }

        if (!string.IsNullOrWhiteSpace(query.RequestKind))
        {
            var rk = query.RequestKind.Trim().ToUpperInvariant();
            combined = combined.Where(x => x.FilterKind != null && x.FilterKind.ToUpper() == rk);
        }

        if (query.SenderId.HasValue)
            combined = combined.Where(x => x.SenderId == query.SenderId.Value);

        if (query.ResolverId.HasValue)
            combined = combined.Where(x => x.ResolverId == query.ResolverId.Value);

        if (query.CreatedFrom.HasValue)
            combined = combined.Where(x => x.CreatedAt >= query.CreatedFrom.Value);

        if (createdTo.HasValue)
            combined = combined.Where(x => x.CreatedAt <= createdTo.Value);

        if (query.ResolvedFrom.HasValue)
            combined = combined.Where(x => x.ResolvedAt != null && x.ResolvedAt >= query.ResolvedFrom.Value);

        if (resolvedTo.HasValue)
            combined = combined.Where(x => x.ResolvedAt != null && x.ResolvedAt <= resolvedTo.Value);

        // Materialize để có thể parse commentId từ message khi là COMPLIANCE_ADMIN_ACTION của report comment.
        var rawRows = await combined.ToListAsync();

        static Guid? ExtractCommentReportTargetId(string? msg)
        {
            if (string.IsNullOrWhiteSpace(msg)) return null;
            const string marker = "[COMMENT_REPORT:";
            var idx = msg.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) return null;
            idx += marker.Length;
            var end = msg.IndexOf(']', idx);
            if (end < 0) return null;
            var chunk = msg.Substring(idx, end - idx).Trim();
            return Guid.TryParse(chunk, out var g) ? g : null;
        }

        foreach (var r in rawRows)
        {
            if (!string.Equals(r.Src, SrcAction, StringComparison.OrdinalIgnoreCase)) continue;
            var cid = ExtractCommentReportTargetId(r.Text);
            if (!cid.HasValue) continue;
            r.RowTargetType = "COMMENT";
            r.RowTargetId = cid.Value;
            r.Title = null; // UI sẽ hiển thị theo targetId khi title null.
        }

        if (!string.IsNullOrWhiteSpace(query.TargetType))
        {
            var tt = query.TargetType.Trim().ToUpperInvariant();
            rawRows = rawRows
                .Where(x => !string.IsNullOrWhiteSpace(x.RowTargetType) && x.RowTargetType.ToUpper() == tt)
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var s = query.Search.Trim();
            if (Guid.TryParse(s, out var g))
            {
                rawRows = rawRows
                    .Where(x => x.Id == g || x.RowTargetId == g || x.SenderId == g || (x.ResolverId.HasValue && x.ResolverId.Value == g))
                    .ToList();
            }
            else
            {
                rawRows = rawRows
                    .Where(x =>
                        (x.Text != null && x.Text.Contains(s)) ||
                        (x.Title != null && x.Title.Contains(s)))
                    .ToList();
            }
        }

        var total = rawRows.Count;

        var sortAsc = string.Equals(query.SortOrder, "asc", StringComparison.OrdinalIgnoreCase);
        var sortBy = (query.SortBy ?? "").Trim().ToLowerInvariant();

        IEnumerable<LogUnionRow> sorted = sortBy == "resolved_at"
            ? (sortAsc
                ? rawRows.OrderBy(x => x.ResolvedAt ?? DateTime.MaxValue).ThenBy(x => x.Id)
                : rawRows.OrderByDescending(x => x.ResolvedAt ?? DateTime.MinValue).ThenByDescending(x => x.Id))
            : (sortAsc
                ? rawRows.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id)
                : rawRows.OrderByDescending(x => x.CreatedAt).ThenByDescending(x => x.Id));

        var rows = sorted
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        var now = DateTime.UtcNow;
        var items = rows.Select(x =>
        {
            var createdUtc = x.CreatedAt.Kind == DateTimeKind.Utc ? x.CreatedAt : x.CreatedAt.ToUniversalTime();
            var tier = EscalationUrgencyHelper.Merge(
                EscalationUrgencyHelper.ComputeFromRequestAge(createdUtc, now),
                x.StoredUrgency);
            return new UnifiedEscalationLogItemDto
            {
                Source = x.Src,
                Id = x.Id,
                Status = x.Status,
                UrgencyTier = tier,
                KindLabel = x.FilterKind,
                TargetType = x.RowTargetType,
                TargetId = x.RowTargetId,
                TargetTitle = x.Title,
                SummaryText = x.Text,
                SenderId = x.SenderId,
                SenderName = NotificationDAO.GetUserDisplayName(x.SenderId),
                ResolverId = x.ResolverId,
                ResolverName = x.ResolverId.HasValue ? NotificationDAO.GetUserDisplayName(x.ResolverId.Value) : null,
                CreatedAt = x.CreatedAt,
                ResolvedAt = x.ResolvedAt,
                ResolverNote = x.ResolverNote
            };
        }).ToList();

        return new PagedResultDto<UnifiedEscalationLogItemDto>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = pageSize
        };
    }

    private static DateTime GetSortKey(AdminUnifiedEscalationPendingItemDto i)
    {
        if (i.ModeratorEscalation != null)
            return i.ModeratorEscalation.CreatedAt.Kind == DateTimeKind.Utc
                ? i.ModeratorEscalation.CreatedAt
                : i.ModeratorEscalation.CreatedAt.ToUniversalTime();
        if (i.ComplianceLock != null)
            return i.ComplianceLock.CreatedAtUtc.Kind == DateTimeKind.Utc
                ? i.ComplianceLock.CreatedAtUtc
                : i.ComplianceLock.CreatedAtUtc.ToUniversalTime();
        if (i.ComplianceAdminAction != null)
            return i.ComplianceAdminAction.CreatedAtUtc.Kind == DateTimeKind.Utc
                ? i.ComplianceAdminAction.CreatedAtUtc
                : i.ComplianceAdminAction.CreatedAtUtc.ToUniversalTime();
        return DateTime.MaxValue;
    }

    private sealed class LogUnionRow
    {
        public string Src { get; set; } = "";
        public Guid Id { get; set; }
        public string? Status { get; set; }
        public string? FilterKind { get; set; }
        public string? RowTargetType { get; set; }
        public Guid RowTargetId { get; set; }
        public string? Title { get; set; }
        public string? Text { get; set; }
        public Guid SenderId { get; set; }
        public Guid? ResolverId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ResolvedAt { get; set; }
        public string? ResolverNote { get; set; }
        public string? StoredUrgency { get; set; }
    }
}
