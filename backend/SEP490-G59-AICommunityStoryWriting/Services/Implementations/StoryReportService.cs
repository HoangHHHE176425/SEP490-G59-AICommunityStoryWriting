using BusinessObjects.Entities;
using BusinessObjects.StoryReporting;
using BusinessObjects;
using DataAccessObjects.DAOs;
using Microsoft.EntityFrameworkCore;
using Services;
using Services.DTOs.Notifications;
using Services.DTOs.StoryReports;
using Services.Interfaces;
using Services.StoryReporting;

namespace Services.Implementations;

public class StoryReportService : IStoryReportService
{
    private static readonly string ComplianceTargetType = ReviewAssignmentDAO.TargetTypeComplianceStoryReports;
    private readonly IUserLookup _userLookup;
    private readonly INotificationHubNotifier? _notificationHubNotifier;

    public StoryReportService(IUserLookup userLookup, INotificationHubNotifier? notificationHubNotifier = null)
    {
        _userLookup = userLookup;
        _notificationHubNotifier = notificationHubNotifier;
    }

    public IReadOnlyList<StoryReportReasonOptionDto> GetReasonOptions()
    {
        return StoryReportReasonCatalog.All
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

    public Task<Guid> CreateStoryReportAsync(Guid storyId, Guid reporterId, CreateStoryReportRequestDto request)
    {
        if (!StoryReportReasonCatalog.TryGet(request.ReasonCode, out _))
            throw new ArgumentException("Invalid reason code.");

        if (request.Description != null && request.Description.Length > 200)
            throw new ArgumentException("Ký tự quá dài: mô tả báo cáo tối đa 200 ký tự.");

        if (reporterId == Guid.Empty || !_userLookup.Exists(reporterId))
            throw new InvalidOperationException("USER không tồn tại.");

        var story = StoryDAO.GetById(storyId)
                    ?? throw new InvalidOperationException("Story not found.");

        var st = (story.status ?? "").Trim().ToUpperInvariant();
        if (st != "PUBLISHED")
            throw new InvalidOperationException("Chỉ có thể báo cáo truyện đã PUBLISHED.");

        if (story.author_id == reporterId)
            throw new InvalidOperationException("Bạn không thể báo cáo truyện của chính mình.");

        var code = request.ReasonCode.Trim().ToUpperInvariant();
        var id = StoryReportDAO.AppendStoryReportAggregated(
            storyId,
            reporterId,
            code,
            request.Description);
        // Mỗi người báo cáo mới (1 lần / truyện / user) = 1 thông báo cho tác giả; trùng trả về Guid.Empty.
        if (id != Guid.Empty)
            _ = NotifyStoryAuthorReportedAsync(story, reporterId, request.ReasonCode, request.Description);
        return Task.FromResult(id);
    }

    private async Task NotifyStoryAuthorReportedAsync(stories story, Guid reporterId, string? reasonCode, string? description)
    {
        var authorId = story.author_id;
        if (!authorId.HasValue || authorId.Value == Guid.Empty) return;
        // Dù đã chặn self-report, vẫn guard để không tự thông báo cho chính mình.
        if (authorId.Value == reporterId) return;

        try
        {
            var reporterName = NotificationDAO.GetUserDisplayName(reporterId);
            var reasonVi = StoryReportReasonCatalog.TryGet(reasonCode ?? "", out var reason)
                ? reason.LabelVi
                : (reasonCode ?? "Khác");
            var storyTitle = string.IsNullOrWhiteSpace(story.title) ? "không rõ tiêu đề" : story.title!;
            var detail = string.IsNullOrWhiteSpace(description)
                ? string.Empty
                : $" Chi tiết từ người báo cáo: {description.Trim()}";

            var n = new notifications
            {
                id = Guid.NewGuid(),
                user_id = authorId.Value,
                type = "STORY_REPORTED_TO_AUTHOR",
                // Tiêu đề luôn ghi rõ người báo cáo (Header chỉ nổi bật dòng title).
                title = $"Người báo cáo: {reporterName}",
                content =
                    $"Truyện «{storyTitle}» vừa nhận báo cáo. Người báo cáo: {reporterName}. Vi phạm: {reasonVi}.{detail}",
                link_url = $"/story/{story.id}",
                is_read = false,
                created_at = DateTime.UtcNow
            };
            NotificationDAO.Add(n);

            if (_notificationHubNotifier != null)
            {
                await _notificationHubNotifier.NotifyUserAsync(authorId.Value, new NotificationDto
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

    private static Func<Guid, bool>? BuildStoryClaimPredicate(string? claimFilter, Guid? actingUserId, bool viewerIsAdmin)
    {
        var cf = (claimFilter ?? "all").Trim().ToUpperInvariant();
        var tt = ComplianceTargetType;

        if (viewerIsAdmin)
        {
            if (cf == "UNCLAIMED")
            {
                var locked = ReviewAssignmentDAO.GetLockedTargetIds(tt).ToHashSet();
                return sid => !locked.Contains(sid);
            }

            if (cf == "MINE" && actingUserId.HasValue)
            {
                var mine = ReviewAssignmentDAO.GetClaimedTargetIdsByUser(tt, actingUserId).ToHashSet();
                return sid => mine.Contains(sid);
            }

            return null;
        }

        if (!actingUserId.HasValue)
            return null;

        var uid = actingUserId.Value;
        if (cf == "UNCLAIMED")
        {
            var locked = ReviewAssignmentDAO.GetLockedTargetIds(tt).ToHashSet();
            return sid => !locked.Contains(sid);
        }

        if (cf == "MINE")
        {
            var mine = ReviewAssignmentDAO.GetClaimedTargetIdsByUser(tt, uid).ToHashSet();
            return sid => mine.Contains(sid);
        }

        var other = ReviewAssignmentDAO.GetLockedTargetIdsByOthers(tt, uid).ToHashSet();
        return sid => !other.Contains(sid);
    }

    private static void EnrichQueueClaim(ComplianceStoryReportQueueItemDto dto, Guid? actingUserId, DateTime nowUtc)
    {
        var claim = ReviewAssignmentDAO.GetClaimInfo(ComplianceTargetType, dto.StoryId);
        dto.IsComplianceLocked = claim.HasValue;
        if (!claim.HasValue)
        {
            dto.ComplianceHandlingSlaStatus = null;
            dto.ComplianceHandlingSlaMessageVi = null;
            dto.HoursSinceComplianceClaim = null;
            return;
        }

        dto.ComplianceClaimedByDisplayName = claim.Value.DisplayName;
        dto.ComplianceClaimedAtUtc = ApiDateTime.AsUtcForJson(claim.Value.AssignedAt);
        dto.IsComplianceClaimedByMe = actingUserId.HasValue && claim.Value.AssigneeId == actingUserId.Value;

        var sla = ComplianceReportHandlingSlaHelper.Compute(claim.Value.AssignedAt, nowUtc);
        dto.ComplianceHandlingSlaStatus = sla.Status;
        dto.ComplianceHandlingSlaMessageVi = sla.MessageVi;
        dto.HoursSinceComplianceClaim = Math.Round(sla.HoursSinceClaim, 1);
    }

    public Task<PagedComplianceStoryReportsDto> QueryComplianceAsync(ComplianceStoryReportQueryDto query, Guid? actingUserId, bool viewerIsAdmin)
    {
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 200 ? 20 : query.PageSize;

        string? statusFilter;
        var rawSt = query.Statuses?.Trim();
        if (string.IsNullOrEmpty(rawSt))
            statusFilter = "NEW,IN_REVIEW";
        else if (rawSt.Equals("ALL", StringComparison.OrdinalIgnoreCase))
            statusFilter = null;
        else
            statusFilter = rawSt;

        var all = StoryReportDAO.ListStoryReportsForCompliance(statusFilter);
        if (query.StoryId.HasValue && query.StoryId.Value != Guid.Empty)
            all = all.Where(r => r.target_id == query.StoryId.Value).ToList();

        if (!string.IsNullOrWhiteSpace(query.ReasonCode)
            && StoryReportReasonCatalog.TryGet(query.ReasonCode, out var rc))
        {
            var code = rc.Code.ToUpperInvariant();
            all = all.Where(r => (r.reason_category ?? "").Equals(code, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        if (query.CreatedFromUtc.HasValue)
            all = all.Where(r => r.created_at >= query.CreatedFromUtc.Value).ToList();
        if (query.CreatedToUtc.HasValue)
            all = all.Where(r => r.created_at <= query.CreatedToUtc.Value).ToList();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim();
            var storyIds = all.Select(r => r.target_id).Distinct().ToList();
            var stories = StoryReportDAO.GetStoriesByIds(storyIds);
            var matchedIds = stories.Values
                .Where(s =>
                    (s.title ?? "").Contains(term, StringComparison.OrdinalIgnoreCase)
                    || (s.slug ?? "").Contains(term, StringComparison.OrdinalIgnoreCase))
                .Select(s => s.id)
                .ToHashSet();
            all = all.Where(r => matchedIds.Contains(r.target_id)).ToList();
        }

        var claimPred = BuildStoryClaimPredicate(query.ClaimFilter, actingUserId, viewerIsAdmin);
        if (claimPred != null)
            all = all.Where(r => claimPred(r.target_id)).ToList();

        var now = DateTime.UtcNow;

        if (query.GroupByStory)
        {
            var groups = all.GroupBy(r => r.target_id).ToList();
            var storyMap = StoryReportDAO.GetStoriesByIds(groups.Select(g => g.Key));
            var contribByStory = StoryReportDAO.GetContributorsByStoryIds(groups.Select(g => g.Key));

            var queue = new List<ComplianceStoryReportQueueItemDto>();
            foreach (var g in groups)
            {
                var list = g.ToList();
                var reasonCounts = StoryReportDAO.GetContributorReasonCounts(g.Key);
                if (reasonCounts.Values.Sum() == 0 && list.Count > 0)
                {
                    reasonCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                    foreach (var r in list)
                    {
                        var code = string.IsNullOrWhiteSpace(r.reason_category)
                            ? "OTHER"
                            : r.reason_category.Trim().ToUpperInvariant();
                        var n = r.contributor_count > 0 ? r.contributor_count : 1;
                        reasonCounts.TryGetValue(code, out var prev);
                        reasonCounts[code] = prev + n;
                    }
                }

                var aggSev = StoryReportReasonScores.ComputeAggregatedSeverity(reasonCounts);
                var cnt = list.Sum(x => x.contributor_count > 0 ? x.contributor_count : 1);
                var oldest = list.Min(x => x.created_at);
                var newest = list.Max(x => x.created_at);
                var tw = oldest.HasValue
                    ? StoryReportPriorityCalculator.ComputeTimeWeight(oldest.Value, now)
                    : 0;
                var priority = StoryReportPriorityCalculator.ComputePriorityScore(aggSev, cnt, tw);

                storyMap.TryGetValue(g.Key, out var sEnt);
                contribByStory.TryGetValue(g.Key, out var dbContributors);
                var dto = new ComplianceStoryReportQueueItemDto
                {
                    StoryId = g.Key,
                    StoryTitle = sEnt?.title ?? "(deleted?)",
                    StorySlug = sEnt?.slug,
                    ReportCount = cnt,
                    MaxSeverityScore = aggSev,
                    TimeWeight = tw,
                    PriorityScore = priority,
                    OldestReportAtUtc = oldest,
                    NewestReportAtUtc = newest,
                    DistinctReasonCodes = reasonCounts.Count > 0
                        ? reasonCounts.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToList()
                        : list.Select(x => x.reason_category ?? "").Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    Contributors = BuildContributorDtos(dbContributors, list),
                    StatusesPresent = list.Select(x => x.status ?? "").Where(x => x.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                    OpenReportIds = list
                        .Where(x => StoryReportDAO.IsOpenComplianceStatus(x.status))
                        .Select(x => x.id)
                        .ToList()
                };
                EnrichQueueClaim(dto, actingUserId, now);
                ApplyStoryModerationSnapshot(dto, sEnt);
                queue.Add(dto);
            }

            if (query.FlaggedOnly == true)
                queue = queue.Where(x => x.ComplianceFlagged).ToList();

            if (query.MinPriority.HasValue)
                queue = queue.Where(x => x.PriorityScore >= query.MinPriority.Value).ToList();
            if (query.MaxPriority.HasValue)
                queue = queue.Where(x => x.PriorityScore <= query.MaxPriority.Value).ToList();

            var sort = (query.SortBy ?? "priority_desc").Trim().ToLowerInvariant();
            queue = sort switch
            {
                "priority_asc" => queue.OrderBy(x => x.PriorityScore).ThenBy(x => x.OldestReportAtUtc).ToList(),
                "oldest" => queue.OrderBy(x => x.OldestReportAtUtc).ToList(),
                "newest" => queue.OrderByDescending(x => x.NewestReportAtUtc).ToList(),
                _ => queue.OrderByDescending(x => x.PriorityScore).ThenBy(x => x.OldestReportAtUtc).ToList()
            };

            var total = queue.Count;
            var slice = queue.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            return Task.FromResult(new PagedComplianceStoryReportsDto
            {
                Page = page,
                PageSize = pageSize,
                TotalCount = total,
                QueueItems = slice,
                Rows = Array.Empty<ComplianceStoryReportRowDto>()
            });
        }

        var storyMapFlat = StoryReportDAO.GetStoriesByIds(all.Select(r => r.target_id));
        if (query.FlaggedOnly == true)
            all = all.Where(r => storyMapFlat.TryGetValue(r.target_id, out var sto) && sto.compliance_flagged).ToList();

        var contribByStoryFlat = StoryReportDAO.GetContributorsByStoryIds(all.Select(r => r.target_id));
        var reportsByStory = all.GroupBy(r => r.target_id).ToDictionary(g => g.Key, g => (IReadOnlyList<reports>)g.ToList());
        var rows = all.Select(r =>
        {
            storyMapFlat.TryGetValue(r.target_id, out var s);
            contribByStoryFlat.TryGetValue(r.target_id, out var dbContributors);
            reportsByStory.TryGetValue(r.target_id, out var storyReportList);
            return new ComplianceStoryReportRowDto
            {
                ReportId = r.id,
                StoryId = r.target_id,
                StoryTitle = s?.title ?? "",
                ReporterId = r.reporter_id,
                ReporterEmail = r.reporter?.email,
                ReasonCode = r.reason_category,
                SeverityScore = StoryReportReasonCatalog.GetSeverityScoreOrDefault(r.reason_category),
                Description = r.description,
                Status = r.status,
                AssignedTo = r.assigned_to,
                AssignedToEmail = r.assigned_toNavigation?.email,
                CreatedAtUtc = r.created_at,
                ResolvedAtUtc = r.resolved_at,
                Contributors = BuildContributorDtos(dbContributors, storyReportList ?? Array.Empty<reports>()),
                AuthorId = s?.author_id,
                CommentsDisabled = s?.comments_disabled ?? false,
                ComplianceHidden = s?.compliance_hidden ?? false,
                ComplianceFlagged = s?.compliance_flagged ?? false,
                ComplianceFlagNote = s?.compliance_flag_note
            };
        }).ToList();

        var claimByStory = new Dictionary<Guid, (string? Name, Guid AssigneeId, DateTime AssignedAt)>();
        foreach (var sid in rows.Select(r => r.StoryId).Distinct())
        {
            var claim = ReviewAssignmentDAO.GetClaimInfo(ComplianceTargetType, sid);
            if (claim.HasValue)
                claimByStory[sid] = (claim.Value.DisplayName, claim.Value.AssigneeId, claim.Value.AssignedAt);
        }

        foreach (var row in rows)
        {
            if (!claimByStory.TryGetValue(row.StoryId, out var c))
                continue;
            row.IsComplianceLocked = true;
            row.ComplianceClaimedByDisplayName = c.Name;
            row.IsComplianceClaimedByMe = actingUserId.HasValue && c.AssigneeId == actingUserId.Value;
            row.ComplianceClaimedAtUtc = ApiDateTime.AsUtcForJson(c.AssignedAt);
            var sla = ComplianceReportHandlingSlaHelper.Compute(c.AssignedAt, now);
            row.ComplianceHandlingSlaStatus = sla.Status;
            row.ComplianceHandlingSlaMessageVi = sla.MessageVi;
            row.HoursSinceComplianceClaim = Math.Round(sla.HoursSinceClaim, 1);
        }

        if (query.MinPriority.HasValue || query.MaxPriority.HasValue)
        {
            rows = rows.Where(r =>
            {
                var tw = r.CreatedAtUtc.HasValue
                    ? StoryReportPriorityCalculator.ComputeTimeWeight(r.CreatedAtUtc.Value, now)
                    : 0;
                var p = StoryReportPriorityCalculator.ComputePriorityScore(r.SeverityScore, 1, tw);
                if (query.MinPriority.HasValue && p < query.MinPriority.Value) return false;
                if (query.MaxPriority.HasValue && p > query.MaxPriority.Value) return false;
                return true;
            }).ToList();
        }

        var sortR = (query.SortBy ?? "newest").Trim().ToLowerInvariant();
        rows = sortR switch
        {
            "oldest" => rows.OrderBy(x => x.CreatedAtUtc).ToList(),
            "priority_desc" => rows.OrderByDescending(x =>
                StoryReportPriorityCalculator.ComputePriorityScore(x.SeverityScore, 1,
                    x.CreatedAtUtc.HasValue ? StoryReportPriorityCalculator.ComputeTimeWeight(x.CreatedAtUtc.Value, now) : 0)).ToList(),
            "priority_asc" => rows.OrderBy(x =>
                StoryReportPriorityCalculator.ComputePriorityScore(x.SeverityScore, 1,
                    x.CreatedAtUtc.HasValue ? StoryReportPriorityCalculator.ComputeTimeWeight(x.CreatedAtUtc.Value, now) : 0)).ToList(),
            _ => rows.OrderByDescending(x => x.CreatedAtUtc).ToList()
        };

        var totalR = rows.Count;
        var sliceR = rows.Skip((page - 1) * pageSize).Take(pageSize).ToList();

        return Task.FromResult(new PagedComplianceStoryReportsDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = totalR,
            QueueItems = Array.Empty<ComplianceStoryReportQueueItemDto>(),
            Rows = sliceR
        });
    }

    public Task<bool> UpdateReportStatusAsync(Guid reportId, Guid actorId, string newStatus, bool actorIsAdmin)
    {
        var s = (newStatus ?? "").Trim().ToUpperInvariant();
        if (s is not ("IN_REVIEW" or "RESOLVED" or "DISMISSED" or "NEW"))
            throw new ArgumentException("Invalid status.");

        var r = StoryReportDAO.GetById(reportId) ?? throw new InvalidOperationException("Report not found.");
        if ((r.target_type ?? "") != StoryReportDAO.StoryTargetType)
            throw new InvalidOperationException("Invalid report target.");

        if (!actorIsAdmin)
            throw new InvalidOperationException("Chỉ ADMIN mới được đổi trạng thái báo cáo. Compliance cần yêu cầu admin nếu cần hỗ trợ.");

        r.status = s;
        if (s is "RESOLVED" or "DISMISSED")
        {
            r.resolved_at = DateTime.UtcNow;
            r.compliance_resolved_by = null;
        }
        else
        {
            r.resolved_at = null;
            r.compliance_resolved_by = null;
        }

        if (s == "IN_REVIEW" && r.assigned_to == null)
        {
            r.assigned_to = actorId;
        }

        StoryReportDAO.Update(r);
        MaybeCompleteComplianceLockWhenNoOpenReports(r.target_id);
        return Task.FromResult(true);
    }

    private static void MaybeCompleteComplianceLockWhenNoOpenReports(Guid storyId)
    {
        if (StoryReportDAO.CountOpenStoryReports(storyId) > 0) return;
        ReviewAssignmentDAO.CompleteAssignment(ComplianceTargetType, storyId);
    }

    public Task<ComplianceClaimStoryResultDto> ClaimStoryAsync(Guid storyId, Guid complianceUserId)
    {
        // Compliance: không lưu hạn xử lý (review_deadline_at = null); cảnh báo chỉ theo thời gian từ lúc nhận lock.
        if (!ReviewAssignmentDAO.TryClaim(ComplianceTargetType, storyId, complianceUserId, reviewDeadlineUtc: null, "COMPLIANCE"))
            throw new InvalidOperationException("Truyện đã được compliance officer khác nhận xử lý (đang lock).");

        // Chỉ lock qua review_assignments — không đổi status/assigned_to trên reports để người đọc vẫn gửi thêm báo cáo (contributor_count tăng bình thường).
        var openCount = StoryReportDAO.CountOpenStoryReports(storyId);
        if (openCount == 0)
        {
            ReviewAssignmentDAO.CompleteAssignment(ComplianceTargetType, storyId);
            throw new InvalidOperationException("Không có báo cáo đang mở (NEW/IN_REVIEW) để nhận.");
        }

        var info = ReviewAssignmentDAO.GetClaimInfo(ComplianceTargetType, storyId)
                   ?? throw new InvalidOperationException("Không đọc được thông tin lock vừa tạo.");
        return Task.FromResult(new ComplianceClaimStoryResultDto
        {
            OpenReportCount = openCount,
            ClaimedAtUtc = ApiDateTime.AsUtcForJson(info.AssignedAt)
        });
    }

    public Task<int> ReleaseComplianceStoryClaimAsync(Guid storyId, Guid adminUserId, bool actorIsAdmin)
    {
        if (!actorIsAdmin)
            throw new InvalidOperationException("Chỉ ADMIN mới được gỡ lock. Compliance gửi yêu cầu trong màn Báo cáo vi phạm.");

        var cur = ReviewAssignmentDAO.GetActiveAssignment(ComplianceTargetType, storyId);
        if (cur == null)
            throw new InvalidOperationException("Truyện không đang bị lock compliance.");

        var holderId = cur.assignee_id;
        ReviewAssignmentDAO.CompleteAssignment(ComplianceTargetType, storyId);
        var n = StoryReportDAO.ReopenInReviewReportsForAssignee(storyId, holderId);
        return Task.FromResult(n);
    }

    public async Task<Guid> RequestComplianceLockReleaseAsync(Guid storyId, Guid requesterId, RequestComplianceLockReleaseDto? dto)
    {
        if (!ReviewAssignmentDAO.IsAssignedTo(ComplianceTargetType, storyId, requesterId))
            throw new InvalidOperationException("Bạn không phải người đang giữ lock truyện này.");

        var msg = dto?.Message;
        var id = ComplianceStoryReportLockRequestDAO.CreatePending(
            storyId,
            requesterId,
            msg,
            EscalationUrgencyHelper.TierForComplianceLockReleaseRequest());
        await NotifyAdminsComplianceReleaseRequestedAsync(storyId, requesterId, msg);
        return id;
    }

    public Task<IReadOnlyList<ComplianceLockRequestListItemDto>> AdminListComplianceLockRequestsAsync(string? status)
    {
        var st = string.IsNullOrWhiteSpace(status) ? ComplianceStoryReportLockRequestDAO.StatusPending : status.Trim().ToUpperInvariant();
        var rows = ComplianceStoryReportLockRequestDAO.ListByStatus(st);
        var list = rows.Select(MapComplianceLockRequestRow).ToList();
        return Task.FromResult<IReadOnlyList<ComplianceLockRequestListItemDto>>(list);
    }

    public Task<IReadOnlyList<ComplianceLockRequestListItemDto>> ListMyComplianceLockRequestsAsync(Guid requesterId)
    {
        var rows = ComplianceStoryReportLockRequestDAO.ListByRequesterId(requesterId);
        var list = rows.Select(MapComplianceLockRequestRow).ToList();
        return Task.FromResult<IReadOnlyList<ComplianceLockRequestListItemDto>>(list);
    }

    public Task<IReadOnlyList<ComplianceAdminActionRequestListItemDto>> ListMyComplianceAdminActionRequestsAsync(Guid requesterId)
    {
        var rows = ComplianceAdminActionRequestDAO.ListByRequesterId(requesterId);
        var list = rows.Select(MapComplianceAdminActionRequest).ToList();
        return Task.FromResult<IReadOnlyList<ComplianceAdminActionRequestListItemDto>>(list);
    }

    private static ComplianceLockRequestListItemDto MapComplianceLockRequestRow(compliance_story_report_lock_requests x)
    {
        var name = x.requester?.user_profiles?.nickname?.Trim();
        if (string.IsNullOrEmpty(name))
            name = x.requester?.email;
        var createdUtc = x.created_at.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(x.created_at, DateTimeKind.Utc)
            : x.created_at.ToUniversalTime();
        DateTime? resolvedUtc = null;
        if (x.resolved_at.HasValue)
        {
            var r = x.resolved_at.Value;
            resolvedUtc = r.Kind == DateTimeKind.Unspecified
                ? DateTime.SpecifyKind(r, DateTimeKind.Utc)
                : r.ToUniversalTime();
        }

        return new ComplianceLockRequestListItemDto
        {
            Id = x.id,
            StoryId = x.story_id,
            StoryTitle = x.story?.title,
            RequesterId = x.requester_id,
            RequesterEmail = x.requester?.email,
            RequesterDisplayName = name,
            Message = x.message,
            Status = x.status,
            CreatedAtUtc = createdUtc,
            UrgencyTier = EscalationUrgencyHelper.ToDisplayTier(EscalationUrgencyHelper.Merge(
                EscalationUrgencyHelper.ComputeFromRequestAge(createdUtc, DateTime.UtcNow),
                x.urgency_tier)),
            ResolvedAtUtc = resolvedUtc,
            ResolutionNote = x.resolution_note,
            ResolutionAction = x.resolution_action
        };
    }

    public Task<IReadOnlyList<ComplianceOfficerAssignmentOptionDto>> AdminListComplianceOfficersForAssignmentAsync()
    {
        var rows = UserDAO.ListActiveComplianceOfficersForStoryReportAssignment();
        var list = rows.Select(x => new ComplianceOfficerAssignmentOptionDto
        {
            UserId = x.Id,
            DisplayName = x.DisplayName,
            Email = x.Email,
            OpenStoryReportLocks = x.ComplianceStoryReportLockCount
        }).ToList();
        return Task.FromResult<IReadOnlyList<ComplianceOfficerAssignmentOptionDto>>(list);
    }

    public Task AdminResolveComplianceLockRequestAsync(Guid requestId, Guid adminId, AdminResolveComplianceLockRequestDto dto)
    {
        var decision = (dto.Decision ?? "").Trim().ToUpperInvariant();
        if (decision is not ("APPROVE_UNLOCK" or "APPROVE_REASSIGN" or "REJECT"))
            throw new ArgumentException("Decision không hợp lệ (APPROVE_UNLOCK | APPROVE_REASSIGN | REJECT).");

        if (decision == "REJECT")
        {
            ComplianceStoryReportLockRequestDAO.MarkResolved(requestId, adminId,
                ComplianceStoryReportLockRequestDAO.StatusRejected, dto.AdminNote, "REJECT");
            return Task.CompletedTask;
        }

        var pending = ComplianceStoryReportLockRequestDAO.TryGetPendingStoryAndRequester(requestId)
                      ?? throw new InvalidOperationException("Yêu cầu không tồn tại hoặc đã xử lý.");
        var cur = ReviewAssignmentDAO.GetActiveAssignment(ComplianceTargetType, pending.storyId);
        if (cur == null || cur.assignee_id != pending.requesterId)
            throw new InvalidOperationException("Lock truyện đã thay đổi; không thể duyệt yêu cầu này.");

        if (decision == "APPROVE_UNLOCK")
        {
            ReviewAssignmentDAO.CompleteAssignment(ComplianceTargetType, pending.storyId);
            StoryReportDAO.ReopenInReviewReportsForAssignee(pending.storyId, pending.requesterId);
            ComplianceStoryReportLockRequestDAO.MarkResolved(requestId, adminId,
                ComplianceStoryReportLockRequestDAO.StatusApproved, dto.AdminNote, "UNLOCK");
            return Task.CompletedTask;
        }

        // APPROVE_REASSIGN
        if (!dto.NewAssigneeId.HasValue || dto.NewAssigneeId.Value == Guid.Empty)
            throw new ArgumentException("Thiếu NewAssigneeId khi giao lại.");
        var deadline = dto.ReviewDeadlineAtUtc ?? DateTime.UtcNow.AddDays(7);
        if (deadline.Kind == DateTimeKind.Unspecified)
            deadline = DateTime.SpecifyKind(deadline, DateTimeKind.Utc);
        else if (deadline.Kind == DateTimeKind.Local)
            deadline = deadline.ToUniversalTime();

        if (!UserDAO.IsActiveComplianceOfficer(dto.NewAssigneeId.Value))
            throw new ArgumentException("Người nhận phải là COMPLIANCE đang ACTIVE.");
        if (dto.NewAssigneeId.Value == pending.requesterId)
            throw new ArgumentException("Không giao lại cho chính người gửi yêu cầu.");

        ReviewAssignmentDAO.ReleaseComplianceStoryClaimAndOptionallyReassign(
            pending.storyId,
            pending.requesterId,
            dto.NewAssigneeId.Value,
            deadline);

        ComplianceStoryReportLockRequestDAO.MarkResolved(requestId, adminId,
            ComplianceStoryReportLockRequestDAO.StatusApproved, dto.AdminNote, "REASSIGN");
        return Task.CompletedTask;
    }

    private static void ApplyStoryModerationSnapshot(ComplianceStoryReportQueueItemDto dto, stories? s)
    {
        if (s == null) return;
        dto.AuthorId = s.author_id;
        dto.AuthorDisplayName = s.author_id.HasValue ? NotificationDAO.GetUserDisplayName(s.author_id.Value) : null;
        dto.CommentsDisabled = s.comments_disabled;
        dto.ComplianceHidden = s.compliance_hidden;
        dto.ComplianceFlagged = s.compliance_flagged;
        dto.ComplianceFlagNote = s.compliance_flag_note;
    }

    private static void EnsureComplianceStoryActPermission(Guid storyId, Guid userId, bool actorIsAdmin)
    {
        if (actorIsAdmin) return;
        if (!ReviewAssignmentDAO.IsAssignedTo(ComplianceTargetType, storyId, userId))
            throw new InvalidOperationException("Chỉ compliance đang nhận (lock) truyện này mới thực hiện được thao tác này.");
    }

    public Task SetStoryComplianceFlagAsync(Guid storyId, Guid actorId, bool flagged, string? note, bool actorIsAdmin)
    {
        EnsureComplianceStoryActPermission(storyId, actorId, actorIsAdmin);
        StoryDAO.SetComplianceFlag(storyId, flagged, note, actorId);
        return Task.CompletedTask;
    }

    public Task SetStoryCommentsDisabledAsync(Guid storyId, Guid actorId, bool disabled, bool actorIsAdmin)
    {
        EnsureComplianceStoryActPermission(storyId, actorId, actorIsAdmin);
        var st = StoryDAO.GetById(storyId) ?? throw new InvalidOperationException("Story not found.");
        StoryDAO.SetCommentsDisabled(storyId, disabled);
        ViolationLogDAO.Insert(actorId, st.author_id, "STORY", storyId,
            disabled ? "COMMENTS_DISABLED" : "COMMENTS_ENABLED",
            disabled ? "Đã tắt bình luận truyện (xử lý vi phạm)." : "Đã bật lại bình luận truyện.", null);
        _ = NotifyStoryAuthorComplianceActionAsync(
            st,
            actorId,
            disabled ? "Truyện bị khóa bình luận" : "Truyện được mở lại bình luận",
            disabled
                ? "Xử lý vi phạm viên đã tắt bình luận cho truyện của bạn."
                : "Xử lý vi phạm viên đã bật lại bình luận cho truyện của bạn.");
        return Task.CompletedTask;
    }

    public Task SetStoryComplianceHiddenAsync(Guid storyId, Guid actorId, bool hidden, bool actorIsAdmin)
    {
        EnsureComplianceStoryActPermission(storyId, actorId, actorIsAdmin);
        var st = StoryDAO.GetById(storyId) ?? throw new InvalidOperationException("Story not found.");
        StoryDAO.SetComplianceHidden(storyId, hidden);
        ViolationLogDAO.Insert(actorId, st.author_id, "STORY", storyId,
            hidden ? "STORY_HIDDEN_COMPLIANCE" : "STORY_UNHIDDEN_COMPLIANCE",
            hidden ? "Đã ẩn truyện khỏi danh sách công khai (xử lý vi phạm)." : "Đã hiện lại truyện trên danh sách công khai.", null);
        _ = NotifyStoryAuthorComplianceActionAsync(
            st,
            actorId,
            hidden ? "Truyện bị ẩn khỏi công khai" : "Truyện được hiển thị lại",
            hidden
                ? "Xử lý vi phạm viên đã ẩn truyện của bạn khỏi danh sách công khai."
                : "Xử lý vi phạm viên đã hiển thị lại truyện của bạn trên danh sách công khai.");
        return Task.CompletedTask;
    }

    private async Task NotifyStoryAuthorComplianceActionAsync(stories story, Guid actorId, string title, string content)
    {
        var authorId = story.author_id;
        if (!authorId.HasValue || authorId.Value == Guid.Empty) return;

        try
        {
            var actorName = NotificationDAO.GetUserDisplayName(actorId);
            var storyTitle = string.IsNullOrWhiteSpace(story.title) ? "không rõ tiêu đề" : story.title!;
            var n = new notifications
            {
                id = Guid.NewGuid(),
                user_id = authorId.Value,
                type = "COMPLIANCE_STORY_MODERATION_ACTION",
                title = title,
                content = $"{content} Truyện: \"{storyTitle}\". Người thực hiện: {actorName}.",
                link_url = $"/story/{story.id}",
                is_read = false,
                created_at = DateTime.UtcNow,
            };
            NotificationDAO.Add(n);

            if (_notificationHubNotifier != null)
            {
                await _notificationHubNotifier.NotifyUserAsync(authorId.Value, new NotificationDto
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

    public Task<Guid> RequestComplianceAdminActionAsync(Guid storyId, Guid requesterId, CreateComplianceAdminActionRequestDto dto, bool actorIsAdmin)
    {
        if (actorIsAdmin)
            throw new InvalidOperationException("Admin thực hiện trực tiếp trên người dùng; compliance mới gửi yêu cầu.");
        EnsureComplianceStoryActPermission(storyId, requesterId, actorIsAdmin: false);

        var story = StoryDAO.GetById(storyId) ?? throw new InvalidOperationException("Story not found.");
        var target = dto.TargetUserId ?? story.author_id
            ?? throw new InvalidOperationException("Truyện không có tác giả; cần chỉ định TargetUserId.");

        var kind = (dto.RequestKind ?? "").Trim().ToUpperInvariant();
        if (kind == ComplianceAdminActionRequestDAO.KindSuspendAuthorWriting)
        {
            var reason = dto.Message?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Bắt buộc nhập lý do đề xuất khi yêu cầu tạm đình chỉ quyền viết.");
            if (!dto.ProposedSuspendUntilUtc.HasValue)
                throw new ArgumentException("Cần ProposedSuspendUntilUtc hợp lệ (UTC).");
            var until = dto.ProposedSuspendUntilUtc.Value;
            if (until.Kind == DateTimeKind.Unspecified)
                until = DateTime.SpecifyKind(until, DateTimeKind.Utc);
            else if (until.Kind == DateTimeKind.Local)
                until = until.ToUniversalTime();
            if (until < DateTime.UtcNow.AddDays(1))
                throw new ArgumentException("Thời hạn đình chỉ phải tối thiểu 1 ngày kể từ hiện tại.");
            dto.ProposedSuspendUntilUtc = until;
        }

        var id = ComplianceAdminActionRequestDAO.CreatePending(
            storyId,
            target,
            kind,
            requesterId,
            dto.Message,
            dto.ProposedSuspendUntilUtc,
            EscalationUrgencyHelper.TierForComplianceAdminActionKind(kind));
        _ = NotifyAdminsComplianceAdminActionRequestedAsync(storyId, requesterId, kind, dto.Message);
        return Task.FromResult(id);
    }

    public Task<IReadOnlyList<ViolationLogListItemDto>> ListViolationsForUserAsync(Guid violatorUserId, int take, bool viewerIsComplianceOrAdmin)
    {
        if (!viewerIsComplianceOrAdmin)
            throw new InvalidOperationException("Không có quyền xem lịch sử vi phạm.");
        var rows = ViolationLogDAO.ListByViolator(violatorUserId, take);
        var list = rows.Select(v =>
        {
            var name = v.compliance_officer?.user_profiles?.nickname?.Trim();
            if (string.IsNullOrEmpty(name))
                name = v.compliance_officer?.email;
            return new ViolationLogListItemDto
            {
                Id = v.id,
                CreatedAtUtc = v.created_at,
                TargetType = v.target_type,
                TargetId = v.target_id,
                PenaltyType = v.penalty_type,
                Reason = v.reason,
                PolicyReference = v.policy_reference,
                ComplianceOfficerDisplayName = name
            };
        }).ToList();
        return Task.FromResult<IReadOnlyList<ViolationLogListItemDto>>(list);
    }

    public Task<IReadOnlyList<ComplianceAdminActionRequestListItemDto>> AdminListComplianceAdminActionRequestsAsync(string? status)
    {
        var st = string.IsNullOrWhiteSpace(status) ? ComplianceAdminActionRequestDAO.StatusPending : status.Trim().ToUpperInvariant();
        var rows = ComplianceAdminActionRequestDAO.ListByStatus(st);
        var list = rows.Select(MapComplianceAdminActionRequest).ToList();
        return Task.FromResult<IReadOnlyList<ComplianceAdminActionRequestListItemDto>>(list);
    }

    public Task AdminResolveComplianceAdminActionRequestAsync(Guid requestId, Guid adminId, AdminResolveComplianceAdminActionRequestDto dto)
    {
        if (requestId == Guid.Empty)
            throw new InvalidOperationException("Không tìm thấy comment.");

        if (dto.AdminNote != null && dto.AdminNote.Length > 200)
            throw new ArgumentException("Ký tự quá dài: mô tả tối đa 200 ký tự.");

        var decision = (dto.Decision ?? "").Trim().ToUpperInvariant();
        if (decision is not ("APPROVE" or "REJECT"))
            throw new ArgumentException("Decision phải là APPROVE hoặc REJECT.");

        var row = ComplianceAdminActionRequestDAO.GetTrackedById(requestId)
                  ?? throw new InvalidOperationException("Yêu cầu không tồn tại.");
        if (row.status != ComplianceAdminActionRequestDAO.StatusPending)
            throw new InvalidOperationException("Yêu cầu đã xử lý.");

        // Compliance request do chính user là chủ của nội dung bị báo cáo gửi => không được phép tự báo cáo chính mình.
        if (row.requester_id == row.target_user_id)
            throw new InvalidOperationException("Không thể tự báo cáo chính mình");

        var story = StoryDAO.GetById(row.story_id);
        if (story is null)
            throw new InvalidOperationException("Không tìm thấy truyện.");

        // Compliance resolve phải chỉ áp dụng cho truyện đã PUBLISHED.
        var st = (story.status ?? "").Trim().ToUpperInvariant();
        if (st != "PUBLISHED")
            throw new InvalidOperationException("Truyện chưa được PUBLISH");

        if (decision == "REJECT")
        {
            ComplianceAdminActionRequestDAO.MarkResolved(requestId, adminId,
                ComplianceAdminActionRequestDAO.StatusRejected, dto.AdminNote, "REJECT");
            return Task.CompletedTask;
        }

        var kind = (row.request_kind ?? "").Trim().ToUpperInvariant();
        if (kind == ComplianceAdminActionRequestDAO.KindBanUser)
        {
            UserDAO.SetUserAccountStatus(row.target_user_id, "BANNED");
            BannedAuthorModerationSweep.Run();
            ViolationLogDAO.Insert(adminId, row.target_user_id, "USER", row.target_user_id, "BAN",
                dto.AdminNote ?? row.message, "ADMIN_APPROVE_COMPLIANCE_REQUEST");
            ComplianceAdminActionRequestDAO.MarkResolved(requestId, adminId,
                ComplianceAdminActionRequestDAO.StatusApproved, dto.AdminNote, "BAN_USER");
            return Task.CompletedTask;
        }

        if (kind == ComplianceAdminActionRequestDAO.KindSuspendAuthorWriting)
        {
            var until = dto.SuspendUntilUtc ?? row.proposed_suspend_until_utc;
            if (!until.HasValue || until.Value <= DateTime.UtcNow)
                throw new ArgumentException("Cần SuspendUntilUtc hoặc đề xuất hợp lệ trong tương lai.");
            var u = until.Value;
            if (u.Kind == DateTimeKind.Unspecified)
                u = DateTime.SpecifyKind(u, DateTimeKind.Utc);
            else if (u.Kind == DateTimeKind.Local)
                u = u.ToUniversalTime();
            UserDAO.SetAuthorWritingSuspendedUntil(row.target_user_id, u);
            ViolationLogDAO.Insert(adminId, row.target_user_id, "USER", row.target_user_id, "SUSPEND_AUTHOR_WRITING",
                dto.AdminNote ?? row.message, until.Value.ToString("O"));
            ComplianceAdminActionRequestDAO.MarkResolved(requestId, adminId,
                ComplianceAdminActionRequestDAO.StatusApproved, dto.AdminNote, "SUSPEND_WRITING");
            return Task.CompletedTask;
        }

        throw new InvalidOperationException("Loại yêu cầu không hỗ trợ.");
    }

    private static ComplianceAdminActionRequestListItemDto MapComplianceAdminActionRequest(compliance_admin_action_requests x)
    {
        string? RName(users? u)
        {
            var n = u?.user_profiles?.nickname?.Trim();
            return !string.IsNullOrEmpty(n) ? n : u?.email;
        }

        var createdUtc = x.created_at.Kind == DateTimeKind.Utc ? x.created_at : x.created_at.ToUniversalTime();
        return new ComplianceAdminActionRequestListItemDto
        {
            Id = x.id,
            StoryId = x.story_id,
            StoryTitle = x.story?.title,
            TargetUserId = x.target_user_id,
            TargetUserEmail = x.target_user?.email,
            TargetUserDisplayName = RName(x.target_user),
            RequestKind = x.request_kind,
            Message = x.message,
            ProposedSuspendUntilUtc = x.proposed_suspend_until_utc,
            Status = x.status,
            RequesterId = x.requester_id,
            RequesterDisplayName = RName(x.requester),
            CreatedAtUtc = x.created_at,
            UrgencyTier = EscalationUrgencyHelper.ToDisplayTier(EscalationUrgencyHelper.Merge(
                EscalationUrgencyHelper.ComputeFromRequestAge(createdUtc, DateTime.UtcNow),
                x.urgency_tier)),
            ResolvedAtUtc = x.resolved_at,
            ResolutionNote = x.resolution_note,
            ResolutionAction = x.resolution_action
        };
    }

    private static IReadOnlyList<StoryReportContributorDto> BuildContributorDtos(
        List<StoryReportDAO.StoryReportContributorRecord>? fromDb,
        IReadOnlyList<reports> reportRowsForStory)
    {
        if (fromDb is { Count: > 0 })
            return fromDb.Select(ToContributorDto).ToList();

        if (reportRowsForStory.Count == 0)
            return Array.Empty<StoryReportContributorDto>();

        var primary = reportRowsForStory
            .OrderByDescending(x => x.contributor_count)
            .ThenBy(x => x.created_at)
            .First();
        var cnt = primary.contributor_count > 0 ? primary.contributor_count : 1;
        if (primary.reporter_id is not { } rid || rid == Guid.Empty)
            return Array.Empty<StoryReportContributorDto>();

        var code = string.IsNullOrWhiteSpace(primary.reason_category)
            ? "OTHER"
            : primary.reason_category.Trim().ToUpperInvariant();
        StoryReportReasonCatalog.TryGet(code, out var def);
        return new List<StoryReportContributorDto>
        {
            new()
            {
                UserId = rid,
                UserEmail = primary.reporter?.email,
                ReasonCode = code,
                ReasonLabelVi = def?.LabelVi ?? code,
                Description = primary.description,
                ReportedAtUtc = NormalizeUtc(primary.created_at),
                DetailNote = cnt > 1
                    ? $"Ticket gộp {cnt} người; chưa có dòng chi tiết trong story_report_contributors — chỉ hiển thị người đại diện trên báo cáo."
                    : null
            }
        };
    }

    private static StoryReportContributorDto ToContributorDto(StoryReportDAO.StoryReportContributorRecord r)
    {
        StoryReportReasonCatalog.TryGet(r.ReasonCode, out var def);
        return new StoryReportContributorDto
        {
            UserId = r.UserId,
            UserEmail = r.UserEmail,
            ReasonCode = r.ReasonCode,
            ReasonLabelVi = def?.LabelVi ?? r.ReasonCode,
            Description = r.Description,
            ReportedAtUtc = NormalizeUtc(r.CreatedAtUtc),
            DetailNote = null
        };
    }

    public Task<bool> ComplianceResolveReportAsync(Guid reportId, Guid complianceUserId, ComplianceResolveReportRequestDto? dto)
    {
        var st = NormalizeComplianceResolveStatus(dto?.Status);
        var r = StoryReportDAO.GetById(reportId) ?? throw new InvalidOperationException("Không tìm thấy báo cáo.");
        if ((r.target_type ?? "") != StoryReportDAO.StoryTargetType)
            throw new InvalidOperationException("Không hợp lệ.");
        if (!StoryReportDAO.IsOpenComplianceStatus(r.status))
            throw new InvalidOperationException("Báo cáo không còn ở trạng thái mở (NEW/IN_REVIEW).");
        if (!ReviewAssignmentDAO.IsAssignedTo(ComplianceTargetType, r.target_id, complianceUserId))
            throw new InvalidOperationException("Bạn phải đang nhận (lock) truyện của báo cáo này mới đánh dấu hoàn thành.");

        r.status = st;
        r.resolved_at = DateTime.UtcNow;
        r.compliance_resolved_by = complianceUserId;
        StoryReportDAO.Update(r);
        MaybeCompleteComplianceLockWhenNoOpenReports(r.target_id);
        return Task.FromResult(true);
    }

    public Task<int> ComplianceResolveOpenReportsForStoryAsync(Guid storyId, Guid complianceUserId, ComplianceResolveReportRequestDto? dto)
    {
        if (!ReviewAssignmentDAO.IsAssignedTo(ComplianceTargetType, storyId, complianceUserId))
            throw new InvalidOperationException("Bạn phải đang nhận (lock) truyện này.");
        var st = NormalizeComplianceResolveStatus(dto?.Status);
        List<Guid> reporterIds;
        var hasOpenReports = false;
        using (var context = new StoryPlatformDbContext())
        {
            var openRows = context.reports.AsNoTracking()
                .Where(r =>
                    (r.target_type ?? "") == StoryReportDAO.StoryTargetType
                    && r.target_id == storyId
                    && r.status != null
                    && (r.status.Trim().ToUpper() == "NEW" || r.status.Trim().ToUpper() == "IN_REVIEW")
                    && r.reporter_id != null)
                .Select(r => r.reporter_id)
                .ToList();
            hasOpenReports = openRows.Count > 0;
            reporterIds = openRows
                .Where(x => x.HasValue && x.Value != Guid.Empty)
                .Select(x => x!.Value)
                .Distinct()
                .ToList();
        }
        if (hasOpenReports)
        {
            var contributorMap = StoryReportDAO.GetContributorsByStoryIds(new[] { storyId });
            if (contributorMap.TryGetValue(storyId, out var contributors) && contributors != null)
            {
                reporterIds.AddRange(contributors
                    .Select(x => x.UserId)
                    .Where(x => x != Guid.Empty));
                reporterIds = reporterIds.Distinct().ToList();
            }
        }
        var n = StoryReportDAO.ResolveOpenStoryReportsForCompliance(storyId, complianceUserId, st);
        MaybeCompleteComplianceLockWhenNoOpenReports(storyId);
        if (n > 0 && reporterIds.Count > 0)
            _ = NotifyReportersBulkResolvedAsync(reporterIds, storyId, st);
        return Task.FromResult(n);
    }

    private async Task NotifyReportersBulkResolvedAsync(IReadOnlyCollection<Guid> reporterIds, Guid storyId, string status)
    {
        if (reporterIds == null || reporterIds.Count == 0) return;
        var success = string.Equals(status, "RESOLVED", StringComparison.OrdinalIgnoreCase);
        var title = success ? "Báo cáo truyện đã được xử lý" : "Báo cáo truyện đã được cập nhật";
        var content = success
            ? "Đơn báo cáo truyện bạn đã gửi đã được xử lý bởi xử lý vi phạm viên thành công."
            : "Đơn báo cáo truyện bạn đã gửi được đánh dấu không đủ bằng chứng để xử lý.";

        foreach (var userId in reporterIds.Distinct())
        {
            try
            {
                var n = new notifications
                {
                    id = Guid.NewGuid(),
                    user_id = userId,
                    type = "COMPLIANCE_STORY_REPORT_BULK_RESOLVED",
                    title = title,
                    content = content,
                    link_url = $"/story/{storyId}",
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

    private async Task NotifyAdminsComplianceReleaseRequestedAsync(Guid storyId, Guid requesterId, string? reason)
    {
        List<Guid> adminIds;
        await using (var db = new StoryPlatformDbContext())
        {
            adminIds = await db.users.AsNoTracking()
                .Where(u => (u.role ?? "").ToUpper() == "ADMIN" && (u.status ?? "").ToUpper() == "ACTIVE")
                .Select(u => u.id)
                .ToListAsync();
        }
        if (adminIds.Count == 0) return;

        var story = StoryDAO.GetById(storyId);
        var storyTitle = string.IsNullOrWhiteSpace(story?.title) ? "không rõ tiêu đề" : story!.title!;
        var requesterName = NotificationDAO.GetUserDisplayName(requesterId);
        var content = $"Xử lý vi phạm viên {requesterName} vừa gửi yêu cầu trả đơn về hàng đợi cho truyện \"{storyTitle}\"."
            + (string.IsNullOrWhiteSpace(reason) ? string.Empty : $" Lý do: {reason!.Trim()}");

        foreach (var adminId in adminIds.Distinct())
        {
            try
            {
                var n = new notifications
                {
                    id = Guid.NewGuid(),
                    user_id = adminId,
                    type = "COMPLIANCE_RELEASE_REQUEST",
                    title = "Có yêu cầu trả đơn về hàng đợi",
                    content = content,
                    link_url = "/admin/violation",
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
            catch
            {
                // best effort push; không làm fail nghiệp vụ chính.
            }
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
                    content = $"{requesterName} vừa gửi yêu cầu {requestKindVi} cho truyện đang bị báo cáo.{note}",
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

    public Task<PagedComplianceStoryReportsDto> QueryMyResolvedComplianceReportsAsync(int page, int pageSize, Guid complianceUserId, string? search)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 200 ? 20 : pageSize;

        var (items, total) = StoryReportDAO.ListResolvedByComplianceUser(complianceUserId, page, pageSize, search);
        var storyMap = StoryReportDAO.GetStoriesByIds(items.Select(r => r.target_id));
        var contribByStory = StoryReportDAO.GetContributorsByStoryIds(items.Select(r => r.target_id));
        var reportsByStory = items.GroupBy(r => r.target_id).ToDictionary(g => g.Key, g => (IReadOnlyList<reports>)g.ToList());

        var rows = items.Select(r =>
        {
            storyMap.TryGetValue(r.target_id, out var s);
            contribByStory.TryGetValue(r.target_id, out var dbContributors);
            reportsByStory.TryGetValue(r.target_id, out var storyReportList);
            return new ComplianceStoryReportRowDto
            {
                ReportId = r.id,
                StoryId = r.target_id,
                StoryTitle = s?.title ?? "",
                ReporterId = r.reporter_id,
                ReporterEmail = r.reporter?.email,
                ReasonCode = r.reason_category,
                SeverityScore = StoryReportReasonCatalog.GetSeverityScoreOrDefault(r.reason_category),
                Description = r.description,
                Status = r.status,
                AssignedTo = r.assigned_to,
                AssignedToEmail = r.assigned_toNavigation?.email,
                CreatedAtUtc = r.created_at,
                ResolvedAtUtc = r.resolved_at,
                ComplianceResolvedBy = r.compliance_resolved_by,
                Contributors = BuildContributorDtos(dbContributors, storyReportList ?? Array.Empty<reports>()),
                AuthorId = s?.author_id,
                CommentsDisabled = s?.comments_disabled ?? false,
                ComplianceHidden = s?.compliance_hidden ?? false,
                ComplianceFlagged = s?.compliance_flagged ?? false,
                ComplianceFlagNote = s?.compliance_flag_note
            };
        }).ToList();

        return Task.FromResult(new PagedComplianceStoryReportsDto
        {
            Page = page,
            PageSize = pageSize,
            TotalCount = total,
            QueueItems = Array.Empty<ComplianceStoryReportQueueItemDto>(),
            Rows = rows
        });
    }

    private static string NormalizeComplianceResolveStatus(string? s)
    {
        var t = (s ?? "RESOLVED").Trim().ToUpperInvariant();
        if (t is not ("RESOLVED" or "DISMISSED"))
            throw new ArgumentException("Chỉ RESOLVED hoặc DISMISSED.");
        return t;
    }

    private static DateTime NormalizeUtc(DateTime? dt)
    {
        if (!dt.HasValue) return DateTime.UtcNow;
        var d = dt.Value;
        if (d.Kind == DateTimeKind.Unspecified)
            return DateTime.SpecifyKind(d, DateTimeKind.Utc);
        return d.Kind == DateTimeKind.Local ? d.ToUniversalTime() : d;
    }

    private static DateTime NormalizeUtc(DateTime dt)
    {
        if (dt.Kind == DateTimeKind.Unspecified)
            return DateTime.SpecifyKind(dt, DateTimeKind.Utc);
        return dt.Kind == DateTimeKind.Local ? dt.ToUniversalTime() : dt;
    }
}
