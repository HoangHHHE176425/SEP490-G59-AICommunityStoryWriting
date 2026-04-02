using System;
using System.Collections.Generic;
using System.Linq;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Repositories;
using Services.DTOs.Chapters;
using Services.DTOs.Moderation;
using Services.DTOs.Notifications;
using Services;
using Services.DTOs.Stories;
using Services.Interfaces;

namespace Services.Implementations
{
    public class ModerationService : IModerationService
    {
        private const int DefaultPolicyDeadlineDays = 7;
        private const int MinHoursUntilDeadline = 24;
        private const int MaxDeadlineDaysAhead = 366;
        private const int ModeratorQueueInMemoryCap = 5000;

        private readonly IStoryRepository _storyRepository;
        private readonly IChapterRepository _chapterRepository;
        private readonly IChapterVersionRepository _versionRepository;
        private readonly IStoryService _storyService;
        private readonly IChapterService _chapterService;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IModerationHubNotifier? _moderationHubNotifier;
        private readonly INotificationHubNotifier? _notificationHubNotifier;
        private readonly ILogger<ModerationService> _logger;

        public ModerationService(
            IStoryRepository storyRepository,
            IChapterRepository chapterRepository,
            IChapterVersionRepository versionRepository,
            IStoryService storyService,
            IChapterService chapterService,
            IServiceScopeFactory scopeFactory,
            ILogger<ModerationService> logger,
            IModerationHubNotifier? moderationHubNotifier = null,
            INotificationHubNotifier? notificationHubNotifier = null)
        {
            _storyRepository = storyRepository;
            _chapterRepository = chapterRepository;
            _versionRepository = versionRepository;
            _storyService = storyService;
            _chapterService = chapterService;
            _scopeFactory = scopeFactory;
            _logger = logger;
            _moderationHubNotifier = moderationHubNotifier;
            _notificationHubNotifier = notificationHubNotifier;
        }

        public PagedResultDto<StoryListItemDto> GetPendingStories(int page = 1, int pageSize = 20, string? search = null, string? sortBy = null, string? sortOrder = null, IReadOnlyList<Guid>? categoryIdsFilter = null, Guid? moderatorId = null, string? claimFilter = null, string? timeStatusFilter = null)
        {
            if (categoryIdsFilter != null && categoryIdsFilter.Count == 0)
                return new PagedResultDto<StoryListItemDto> { Items = new List<StoryListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };

            var filter = (claimFilter ?? "all").Trim().ToUpperInvariant();
            List<Guid>? excludeStoryIds = null;
            List<Guid>? includeStoryIds = null;

            if (filter == "UNCLAIMED")
                excludeStoryIds = ReviewAssignmentDAO.GetLockedTargetIds(ReviewAssignmentDAO.TargetTypeStory);
            else if (filter == "CLAIMED")
            {
                includeStoryIds = ReviewAssignmentDAO.GetClaimedTargetIdsByUser(ReviewAssignmentDAO.TargetTypeStory, moderatorId);
                if (includeStoryIds.Count == 0)
                    return new PagedResultDto<StoryListItemDto> { Items = new List<StoryListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
            }
            else
            {
                excludeStoryIds = moderatorId.HasValue
                    ? ReviewAssignmentDAO.GetLockedTargetIdsByOthers(ReviewAssignmentDAO.TargetTypeStory, moderatorId.Value)
                    : new List<Guid>();
            }

            var sortByNorm = string.IsNullOrWhiteSpace(sortBy) ? "updated_at" : sortBy.Trim();
            var sortOrderNorm = string.IsNullOrWhiteSpace(sortOrder) ? "asc" : sortOrder.Trim();
            var useMemory = NeedsInMemoryModeratorQueueProcessing(sortByNorm, timeStatusFilter);
            var dbSortBy = string.Equals(sortByNorm, "deadline_at", StringComparison.OrdinalIgnoreCase) ? "updated_at" : sortByNorm;

            var query = new StoryQueryDto
            {
                Status = "PENDING_REVIEW",
                Page = useMemory ? 1 : page,
                PageSize = useMemory ? ModeratorQueueInMemoryCap : pageSize,
                Search = search,
                SortBy = dbSortBy,
                SortOrder = sortOrderNorm,
                CategoryIds = categoryIdsFilter != null ? categoryIdsFilter.ToList() : null,
                ExcludeStoryIds = excludeStoryIds != null && excludeStoryIds.Count > 0 ? excludeStoryIds : null,
                IncludeStoryIds = includeStoryIds != null && includeStoryIds.Count > 0 ? includeStoryIds : null
            };
            var result = _storyService.GetAll(query);
            var pendingEscalationStoryIds = ReviewEscalationDAO.GetPendingTargetIds(ReviewAssignmentDAO.TargetTypeStory);

            if (useMemory)
            {
                var list = result.Items.ToList();
                foreach (var item in list)
                    EnrichPendingStoryItem(item, moderatorId, pendingEscalationStoryIds);

                if (!string.IsNullOrWhiteSpace(timeStatusFilter))
                {
                    var ts = timeStatusFilter.Trim();
                    list = list.Where(i => string.Equals(i.TimeStatus, ts, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (string.Equals(sortByNorm, "deadline_at", StringComparison.OrdinalIgnoreCase))
                {
                    var asc = string.Equals(sortOrderNorm, "asc", StringComparison.OrdinalIgnoreCase);
                    list = asc
                        ? list.OrderBy(i => i.DeadlineAt ?? DateTime.MaxValue).ToList()
                        : list.OrderByDescending(i => i.DeadlineAt ?? DateTime.MinValue).ToList();
                }

                var total = list.Count;
                var pageItems = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                ApplyAdminRejectedEscalationNotesForStories(pageItems, moderatorId);
                return new PagedResultDto<StoryListItemDto>
                {
                    Items = pageItems,
                    TotalCount = total,
                    Page = page,
                    PageSize = pageSize
                };
            }

            foreach (var item in result.Items)
                EnrichPendingStoryItem(item, moderatorId, pendingEscalationStoryIds);
            ApplyAdminRejectedEscalationNotesForStories(result.Items.ToList(), moderatorId);
            return result;
        }

        private static bool NeedsInMemoryModeratorQueueProcessing(string? sortBy, string? timeStatusFilter) =>
            string.Equals(sortBy, "deadline_at", StringComparison.OrdinalIgnoreCase)
            || !string.IsNullOrWhiteSpace(timeStatusFilter);

        private void EnrichPendingStoryItem(StoryListItemDto item, Guid? moderatorId, HashSet<Guid> pendingEscalationStoryIds)
        {
            var authorSubmitted = ModeratorReviewSlaHelper.GetAuthorSubmittedUtc(
                ReviewAssignmentDAO.TargetTypeStory, item.Id, _storyRepository, _chapterRepository, _versionRepository);
            var pendingSince = item.UpdatedAt;
            item.PendingSince = authorSubmitted ?? pendingSince;
            var claim = ReviewAssignmentDAO.GetClaimInfo(ReviewAssignmentDAO.TargetTypeStory, item.Id);
            if (claim.HasValue)
            {
                item.ClaimedAt = ApiDateTime.AsUtcForJson(claim.Value.AssignedAt);
                item.ClaimedByDisplayName = claim.Value.DisplayName;
                item.IsClaimedByMe = moderatorId.HasValue && claim.Value.AssigneeId == moderatorId.Value;
            }

            item.HasPendingEscalation = pendingEscalationStoryIds.Contains(item.Id);
            var fallbackDeadline = ResolveReviewDeadlineUtc(pendingSince, claim);
            item.DeadlineAt = ApiDateTime.AsUtcForJson(fallbackDeadline);
            item.TimeStatus = ModeratorReviewSlaHelper.ComputeSlaTimeStatus(authorSubmitted, fallbackDeadline);
        }

        public PagedResultDto<ChapterListItemDto> GetPendingChapters(int page = 1, int pageSize = 20, Guid? storyId = null, string? search = null, string? sortBy = null, string? sortOrder = null, IReadOnlyList<Guid>? categoryIdsFilter = null, Guid? moderatorId = null, string? claimFilter = null, string? timeStatusFilter = null)
        {
            if (categoryIdsFilter != null && categoryIdsFilter.Count == 0)
                return new PagedResultDto<ChapterListItemDto> { Items = new List<ChapterListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };

            List<Guid>? storyIdsFilter = null;
            if (categoryIdsFilter != null && categoryIdsFilter.Count > 0)
                storyIdsFilter = _storyRepository.GetStoryIdsByCategoryIds(categoryIdsFilter).ToList();

            var filter = (claimFilter ?? "all").Trim().ToUpperInvariant();
            List<Guid>? excludeChapterIds = null;
            List<Guid>? includeChapterIds = null;

            if (filter == "UNCLAIMED")
                excludeChapterIds = ReviewAssignmentDAO.GetLockedTargetIds(ReviewAssignmentDAO.TargetTypeChapter);
            else if (filter == "CLAIMED")
            {
                includeChapterIds = ReviewAssignmentDAO.GetClaimedTargetIdsByUser(ReviewAssignmentDAO.TargetTypeChapter, moderatorId);
                if (includeChapterIds.Count == 0)
                    return new PagedResultDto<ChapterListItemDto> { Items = new List<ChapterListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
            }
            else
            {
                excludeChapterIds = moderatorId.HasValue
                    ? ReviewAssignmentDAO.GetLockedTargetIdsByOthers(ReviewAssignmentDAO.TargetTypeChapter, moderatorId.Value)
                    : new List<Guid>();
            }

            // Chapter chờ duyệt = (status PENDING_REVIEW) hoặc (status PUBLISHED nhưng có version PENDING_REVIEW — chỉnh sửa sau báo cáo vi phạm).
            var pendingVersionChapterIds = DataAccessObjects.DAOs.ChapterVersionDAO.GetChapterIdsWithPendingReviewVersion();
            if (excludeChapterIds != null && excludeChapterIds.Count > 0)
                pendingVersionChapterIds = pendingVersionChapterIds.Where(id => !excludeChapterIds.Contains(id)).ToList();
            if (includeChapterIds != null && includeChapterIds.Count > 0)
                pendingVersionChapterIds = pendingVersionChapterIds.Where(id => includeChapterIds.Contains(id)).ToList();

            var sortByNorm = string.IsNullOrWhiteSpace(sortBy) ? "updated_at" : sortBy.Trim();
            var sortOrderNorm = string.IsNullOrWhiteSpace(sortOrder) ? "asc" : sortOrder.Trim();
            var useMemory = NeedsInMemoryModeratorQueueProcessing(sortByNorm, timeStatusFilter);
            var dbSortBy = string.Equals(sortByNorm, "deadline_at", StringComparison.OrdinalIgnoreCase) ? "updated_at" : sortByNorm;

            var query = new ChapterQueryDto
            {
                PendingVersionChapterIds = pendingVersionChapterIds.Count > 0 ? pendingVersionChapterIds : null,
                Status = pendingVersionChapterIds.Count == 0 ? "PENDING_REVIEW" : null,
                StoryId = storyId,
                StoryIds = storyIdsFilter,
                ExcludeChapterIds = excludeChapterIds != null && excludeChapterIds.Count > 0 ? excludeChapterIds : null,
                IncludeChapterIds = includeChapterIds != null && includeChapterIds.Count > 0 ? includeChapterIds : null,
                Page = useMemory ? 1 : page,
                PageSize = useMemory ? ModeratorQueueInMemoryCap : pageSize,
                Search = search,
                SortBy = dbSortBy,
                SortOrder = sortOrderNorm
            };
            var result = _chapterService.GetAll(query);
            var pendingEscalationChapterIds = ReviewEscalationDAO.GetPendingTargetIds(ReviewAssignmentDAO.TargetTypeChapter);
            var pendingEscalationStoryIds = ReviewEscalationDAO.GetPendingTargetIds(ReviewAssignmentDAO.TargetTypeStory);

            if (useMemory)
            {
                var list = result.Items.ToList();
                foreach (var item in list)
                    EnrichPendingChapterItem(item, moderatorId, pendingEscalationChapterIds, pendingEscalationStoryIds);

                if (!string.IsNullOrWhiteSpace(timeStatusFilter))
                {
                    var ts = timeStatusFilter.Trim();
                    list = list.Where(i => string.Equals(i.TimeStatus, ts, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                if (string.Equals(sortByNorm, "deadline_at", StringComparison.OrdinalIgnoreCase))
                {
                    var asc = string.Equals(sortOrderNorm, "asc", StringComparison.OrdinalIgnoreCase);
                    list = asc
                        ? list.OrderBy(i => i.DeadlineAt ?? DateTime.MaxValue).ToList()
                        : list.OrderByDescending(i => i.DeadlineAt ?? DateTime.MinValue).ToList();
                }

                var total = list.Count;
                var pageItems = list.Skip((page - 1) * pageSize).Take(pageSize).ToList();
                ApplyAdminRejectedEscalationNotesForChapters(pageItems, moderatorId);
                return new PagedResultDto<ChapterListItemDto>
                {
                    Items = pageItems,
                    TotalCount = total,
                    Page = page,
                    PageSize = pageSize
                };
            }

            foreach (var item in result.Items)
                EnrichPendingChapterItem(item, moderatorId, pendingEscalationChapterIds, pendingEscalationStoryIds);
            ApplyAdminRejectedEscalationNotesForChapters(result.Items.ToList(), moderatorId);
            return result;
        }

        /// <summary>
        /// Còn chương chờ moderator (PENDING_REVIEW hoặc có version PENDING_REVIEW) — đồng bộ tiêu chí GetPendingChapters.
        /// Dùng để ẩn banner &quot;admin từ chối đơn hủy nhận duyệt&quot; sau khi moderator đã xử lý hết chương trong đợt này.
        /// </summary>
        private bool StoryHasAnyChapterPendingModerationReview(Guid storyId)
        {
            var pendingVersionChapterIds = new HashSet<Guid>(DataAccessObjects.DAOs.ChapterVersionDAO.GetChapterIdsWithPendingReviewVersion());
            foreach (var ch in _chapterRepository.GetByStoryId(storyId))
            {
                if (string.Equals(ch.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase))
                    return true;
                if (pendingVersionChapterIds.Contains(ch.id))
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Chỉ hiển thị ghi chú admin (từ chối đơn hủy nhận duyệt / gia hạn) nếu đơn bị xử lý trong phiên nhận duyệt hiện tại:
        /// resolved_at phải &gt;= assigned_at của lock STORY/CHAPTER tương ứng. Tránh hiện lý do đơn cũ sau khi tác giả gửi lại và moderator nhận duyệt mới.
        /// Với đơn RELEASE ở cấp truyện: chỉ gộp từ story khi <paramref name="storyIdsEligibleForStoryLevelRelease"/> chứa story (còn chương chờ duyệt).
        /// </summary>
        private static (string? Note, DateTime? At)? PickBestChapterEscalationRejectionForCurrentClaim(
            Dictionary<Guid, (string? Note, DateTime? ResolvedAt)> chDict,
            Dictionary<Guid, (string? Note, DateTime? ResolvedAt)> stDict,
            Guid chapterId,
            Guid? storyId,
            DateTime? chapterClaimedAt,
            IReadOnlyDictionary<Guid, DateTime> storyClaimAssignedAtByStoryId,
            HashSet<Guid>? storyIdsEligibleForStoryLevelRelease)
        {
            var candidates = new List<(DateTime At, string? Note)>();
            if (chDict.TryGetValue(chapterId, out var cn) && cn.ResolvedAt.HasValue)
            {
                var at = cn.ResolvedAt.Value;
                if (!chapterClaimedAt.HasValue || at >= chapterClaimedAt.Value)
                    candidates.Add((at, cn.Note));
            }

            if (storyId.HasValue && stDict.TryGetValue(storyId.Value, out var sn) && sn.ResolvedAt.HasValue)
            {
                if (storyIdsEligibleForStoryLevelRelease != null && !storyIdsEligibleForStoryLevelRelease.Contains(storyId.Value))
                {
                    // Bỏ qua từ chối RELEASE cấp truyện khi đã xử lý hết chương — vẫn giữ candidate cấp chương ở trên.
                }
                else
                {
                    var at = sn.ResolvedAt.Value;
                    var stClaim = storyClaimAssignedAtByStoryId.TryGetValue(storyId.Value, out var ca) ? (DateTime?)ca : null;
                    // Chỉ giữ note từ chối admin nếu nó thuộc phiên claim hiện tại.
                    // Nếu không có lock STORY thì fallback theo lock CHAPTER hiện tại để tránh kéo note cũ sang đơn mới.
                    var effectiveClaimAt = stClaim ?? chapterClaimedAt;
                    if (effectiveClaimAt.HasValue && at >= effectiveClaimAt.Value)
                        candidates.Add((at, sn.Note));
                }
            }

            if (candidates.Count == 0)
                return null;
            var top = candidates.OrderByDescending(c => c.At).First();
            return (top.Note, top.At);
        }

        private static bool IsAdminEscalationRejectionStillRelevantForStoryClaim(DateTime? rejectionResolvedAt, DateTime? storyClaimedAt)
        {
            if (!rejectionResolvedAt.HasValue)
                return true;
            if (!storyClaimedAt.HasValue)
                return false;
            var rejectedAtUtc = ApiDateTime.AsUtcForJson(rejectionResolvedAt.Value);
            var claimedAtUtc = ApiDateTime.AsUtcForJson(storyClaimedAt.Value);
            return rejectedAtUtc >= claimedAtUtc;
        }

        private static bool IsRejectionWithinCurrentPendingCycle(DateTime? rejectionResolvedAt, DateTime? pendingSinceUtc)
        {
            if (!rejectionResolvedAt.HasValue)
                return false;
            if (!pendingSinceUtc.HasValue)
                return true;
            var rejectedAtUtc = ApiDateTime.AsUtcForJson(rejectionResolvedAt.Value);
            var pendingAtUtc = ApiDateTime.AsUtcForJson(pendingSinceUtc.Value);
            return rejectedAtUtc >= pendingAtUtc;
        }

        private void ApplyAdminRejectedEscalationNotesForStories(IReadOnlyList<StoryListItemDto> items, Guid? moderatorId)
        {
            if (!moderatorId.HasValue || items.Count == 0)
                return;
            var mid = moderatorId.Value;
            var ids = items.Select(i => i.Id).ToList();
            var rel = ReviewEscalationDAO.GetLatestRejectedReleaseByTargetsForSender(mid, ReviewAssignmentDAO.TargetTypeStory, ids);
            var ext = ReviewEscalationDAO.GetLatestRejectedExtendByTargetsForSender(mid, ReviewAssignmentDAO.TargetTypeStory, ids);
            foreach (var item in items)
            {
                var pendingSinceUtc = ModeratorReviewSlaHelper.GetAuthorSubmittedUtc(
                    ReviewAssignmentDAO.TargetTypeStory, item.Id, _storyRepository, _chapterRepository, _versionRepository);
                if (rel.TryGetValue(item.Id, out var r)
                    && IsAdminEscalationRejectionStillRelevantForStoryClaim(r.ResolvedAt, item.ClaimedAt)
                    && IsRejectionWithinCurrentPendingCycle(r.ResolvedAt, pendingSinceUtc)
                    && StoryHasAnyChapterPendingModerationReview(item.Id))
                {
                    item.AdminRejectedReleaseNote = r.Note;
                    item.AdminRejectedReleaseAt = ApiDateTime.AsUtcForJson(r.ResolvedAt);
                    item.IsCurrentClaimRejection = true;
                }

                if (ext.TryGetValue(item.Id, out var e)
                    && IsAdminEscalationRejectionStillRelevantForStoryClaim(e.ResolvedAt, item.ClaimedAt))
                {
                    item.AdminRejectedExtendNote = e.Note;
                    item.AdminRejectedExtendAt = e.ResolvedAt;
                }
            }
        }

        private void ApplyAdminRejectedEscalationNotesForChapters(IReadOnlyList<ChapterListItemDto> items, Guid? moderatorId)
        {
            if (!moderatorId.HasValue || items.Count == 0)
                return;
            var mid = moderatorId.Value;
            var chapterIds = items.Select(i => i.Id).ToList();
            var storyIds = items.Where(i => i.StoryId.HasValue).Select(i => i.StoryId!.Value).Distinct().ToList();
            var empty = new Dictionary<Guid, (string? Note, DateTime? ResolvedAt)>();

            var chRel = ReviewEscalationDAO.GetLatestRejectedReleaseByTargetsForSender(mid, ReviewAssignmentDAO.TargetTypeChapter, chapterIds);
            var stRel = storyIds.Count > 0
                ? ReviewEscalationDAO.GetLatestRejectedReleaseByTargetsForSender(mid, ReviewAssignmentDAO.TargetTypeStory, storyIds)
                : empty;
            var chExt = ReviewEscalationDAO.GetLatestRejectedExtendByTargetsForSender(mid, ReviewAssignmentDAO.TargetTypeChapter, chapterIds);
            var stExt = storyIds.Count > 0
                ? ReviewEscalationDAO.GetLatestRejectedExtendByTargetsForSender(mid, ReviewAssignmentDAO.TargetTypeStory, storyIds)
                : empty;

            var storyClaimAssignedAtByStoryId = storyIds.Count > 0
                ? ReviewAssignmentDAO.GetActiveClaimInfosByTargetIds(ReviewAssignmentDAO.TargetTypeStory, storyIds)
                    .ToDictionary(kv => kv.Key, kv => kv.Value.AssignedAt)
                : new Dictionary<Guid, DateTime>();

            var storyIdsEligibleForStoryLevelRelease = new HashSet<Guid>();
            foreach (var sid in storyIds)
            {
                if (StoryHasAnyChapterPendingModerationReview(sid))
                    storyIdsEligibleForStoryLevelRelease.Add(sid);
            }

            foreach (var item in items)
            {
                var pendingSinceUtc = ModeratorReviewSlaHelper.GetAuthorSubmittedUtc(
                    ReviewAssignmentDAO.TargetTypeChapter, item.Id, _storyRepository, _chapterRepository, _versionRepository);
                var rel = PickBestChapterEscalationRejectionForCurrentClaim(
                    chRel, stRel, item.Id, item.StoryId, item.ClaimedAt, storyClaimAssignedAtByStoryId, storyIdsEligibleForStoryLevelRelease);
                if (rel.HasValue)
                {
                    var atUtc = ApiDateTime.AsUtcForJson(rel.Value.At);
                    if (IsRejectionWithinCurrentPendingCycle(atUtc, pendingSinceUtc))
                    {
                        item.AdminRejectedReleaseNote = rel.Value.Note;
                        item.AdminRejectedReleaseAt = atUtc;
                        item.IsCurrentClaimRejection = true;
                    }
                }

                var ext = PickBestChapterEscalationRejectionForCurrentClaim(
                    chExt, stExt, item.Id, item.StoryId, item.ClaimedAt, storyClaimAssignedAtByStoryId, null);
                if (ext.HasValue)
                {
                    item.AdminRejectedExtendNote = ext.Value.Note;
                    item.AdminRejectedExtendAt = ext.Value.At;
                }
            }
        }

        private void EnrichPendingChapterItem(ChapterListItemDto item, Guid? moderatorId, HashSet<Guid> pendingEscalationChapterIds, HashSet<Guid> pendingEscalationStoryIds)
        {
            var pendingSince = item.UpdatedAt ?? item.CreatedAt;
            var authorSubmitted = ModeratorReviewSlaHelper.GetAuthorSubmittedUtc(
                ReviewAssignmentDAO.TargetTypeChapter, item.Id, _storyRepository, _chapterRepository, _versionRepository);
            item.PendingSince = authorSubmitted ?? pendingSince;

            var claim = ReviewAssignmentDAO.GetClaimInfo(ReviewAssignmentDAO.TargetTypeChapter, item.Id);
            if (claim.HasValue)
            {
                item.ClaimedAt = ApiDateTime.AsUtcForJson(claim.Value.AssignedAt);
                item.ClaimedByDisplayName = claim.Value.DisplayName;
                item.IsClaimedByMe = moderatorId.HasValue && claim.Value.AssigneeId == moderatorId.Value;
            }

            var sid = item.StoryId;
            item.HasPendingEscalation = pendingEscalationChapterIds.Contains(item.Id)
                || (sid.HasValue && pendingEscalationStoryIds.Contains(sid.Value));
            var fallbackDeadline = ResolveReviewDeadlineUtc(pendingSince, claim);
            item.DeadlineAt = ApiDateTime.AsUtcForJson(fallbackDeadline);
            item.TimeStatus = ModeratorReviewSlaHelper.ComputeSlaTimeStatus(authorSubmitted, fallbackDeadline);

            var pendingVersionsList = _versionRepository.GetByChapterId(item.Id)
                .Where(v => string.Equals(v.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase))
                .OrderBy(v => v.version_number)
                .ToList();
            var pendingVersion = pendingVersionsList.FirstOrDefault();
            if (pendingVersion != null)
            {
                item.PendingVersionTitle = string.IsNullOrWhiteSpace(pendingVersion.title_snapshot)
                    ? (item.Title ?? null)
                    : pendingVersion.title_snapshot.Trim();
                item.PendingVersionWordCount = string.IsNullOrWhiteSpace(pendingVersion.content_snapshot)
                    ? 0
                    : pendingVersion.content_snapshot!.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
            }
        }

        public PagedResultDto<StoryListItemDto> GetReviewedStories(int page, int pageSize, string status, string? search, string? sortBy, string? sortOrder, IReadOnlyList<Guid>? categoryIdsFilter, Guid? moderatorId, bool isAdmin, Guid? moderatorIdFilter = null, DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            var statusUpper = (status ?? "").Trim().ToUpperInvariant();
            if (statusUpper != "PUBLISHED" && statusUpper != "REJECTED")
                return new PagedResultDto<StoryListItemDto> { Items = new List<StoryListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };

            // Trong moderation_logs, action là "APPROVED" hoặc "REJECTED", không phải "PUBLISHED".
            var logAction = statusUpper == "PUBLISHED" ? "APPROVED" : statusUpper;
            List<Guid>? includeStoryIds = null;
            List<string>? statusIn = null;
            if (isAdmin && (moderatorIdFilter.HasValue || dateFrom.HasValue || dateTo.HasValue))
            {
                includeStoryIds = DataAccessObjects.DAOs.ModerationLogDAO.GetTargetIdsFiltered("STORY", moderatorIdFilter, dateFrom, dateTo, logAction);
                if (includeStoryIds == null || includeStoryIds.Count == 0)
                    return new PagedResultDto<StoryListItemDto> { Items = new List<StoryListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
                statusIn = statusUpper == "REJECTED" ? new List<string> { "REJECTED", "PENDING_REVIEW" } : null;
            }
            else if (statusUpper == "REJECTED")
            {
                // Lịch sử từ chối: giữ lại kể cả khi item đã được duyệt sau đó.
                // Vì vậy: lấy các target đã từng bị REJECTED (lọc theo moderator nếu không phải admin),
                // và KHÔNG lọc theo status hiện tại (tránh trường hợp đã PUBLISHED thì bị mất khỏi lịch sử).
                includeStoryIds = isAdmin
                    ? DataAccessObjects.DAOs.ModerationLogDAO.GetTargetIdsFiltered("STORY", moderatorId: null, dateFrom: null, dateTo: null, action: "REJECTED")
                    : (moderatorId.HasValue
                        ? DataAccessObjects.DAOs.ModerationLogDAO.GetTargetIdsByModeratorAndAction(moderatorId.Value, "STORY", "REJECTED")
                        : new List<Guid>());
                if (includeStoryIds == null || includeStoryIds.Count == 0)
                    return new PagedResultDto<StoryListItemDto> { Items = new List<StoryListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
                statusIn = null;
            }
            else if (!isAdmin && moderatorId.HasValue)
            {
                includeStoryIds = DataAccessObjects.DAOs.ModerationLogDAO.GetTargetIdsByModeratorAndAction(moderatorId.Value, "STORY", "APPROVED");
                if (includeStoryIds == null || includeStoryIds.Count == 0)
                    return new PagedResultDto<StoryListItemDto> { Items = new List<StoryListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
            }

            var ignoreStatusFilter = statusUpper == "REJECTED" && statusIn == null && includeStoryIds != null;
            var query = new StoryQueryDto
            {
                Status = (statusIn == null && !ignoreStatusFilter) ? statusUpper : null,
                StatusIn = statusIn,
                Page = page,
                PageSize = pageSize,
                Search = search,
                SortBy = !string.IsNullOrWhiteSpace(sortBy) ? sortBy : "updated_at",
                SortOrder = !string.IsNullOrWhiteSpace(sortOrder) ? sortOrder : "desc",
                CategoryIds = isAdmin ? (categoryIdsFilter != null ? categoryIdsFilter.ToList() : null) : null,
                IncludeStoryIds = includeStoryIds
            };
            var result = _storyService.GetAll(query);
            var storyList = result.Items?.ToList() ?? new List<StoryListItemDto>();
            // Tab "Từ chối": dù story hiện đã PUBLISHED, vẫn cần hiển thị lý do từ chối trước đó trong lịch sử.
            if (statusUpper == "REJECTED" && storyList.Count > 0)
            {
                foreach (var item in storyList)
                {
                    var (reason, rejectedAt) = DataAccessObjects.DAOs.ModerationLogDAO.GetLatestRejection("STORY", item.Id);
                    item.RejectionReason = reason;
                    item.RejectedAt = rejectedAt;
                }
            }
            if (storyList.Count > 0 && isAdmin)
            {
                var logInfo = DataAccessObjects.DAOs.ModerationLogDAO.GetLogInfoByTargets("STORY", storyList.Select(s => s.Id).ToList(), logAction);
                foreach (var item in storyList)
                {
                    if (logInfo.TryGetValue(item.Id, out var info))
                    {
                        item.ReviewedAt = info.CreatedAt;
                        item.ReviewedByModeratorName = info.ModeratorId.HasValue ? NotificationDAO.GetUserDisplayName(info.ModeratorId.Value) : null;
                    }
                }
            }
            return result;
        }

        public PagedResultDto<ChapterListItemDto> GetReviewedChapters(int page, int pageSize, string status, string? search, string? sortBy, string? sortOrder, IReadOnlyList<Guid>? categoryIdsFilter, Guid? moderatorId, bool isAdmin, Guid? moderatorIdFilter = null, DateTime? dateFrom = null, DateTime? dateTo = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            var statusUpper = (status ?? "").Trim().ToUpperInvariant();
            if (statusUpper != "PUBLISHED" && statusUpper != "REJECTED")
                return new PagedResultDto<ChapterListItemDto> { Items = new List<ChapterListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };

            List<Guid>? storyIdsFilter = null;
            List<Guid>? includeChapterIds = null;
            List<string>? statusIn = null;

            // Trong moderation_logs, action là "APPROVED" hoặc "REJECTED".
            var logAction = statusUpper == "PUBLISHED" ? "APPROVED" : statusUpper;
            if (isAdmin && categoryIdsFilter != null && categoryIdsFilter.Count > 0)
                storyIdsFilter = _storyRepository.GetStoryIdsByCategoryIds(categoryIdsFilter).ToList();
            if (isAdmin && (moderatorIdFilter.HasValue || dateFrom.HasValue || dateTo.HasValue))
            {
                includeChapterIds = DataAccessObjects.DAOs.ModerationLogDAO.GetTargetIdsFiltered("CHAPTER", moderatorIdFilter, dateFrom, dateTo, logAction);
                if (includeChapterIds == null || includeChapterIds.Count == 0)
                    return new PagedResultDto<ChapterListItemDto> { Items = new List<ChapterListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
                statusIn = statusUpper == "REJECTED" ? new List<string> { "REJECTED", "PENDING_REVIEW" } : null;
            }
            else if (statusUpper == "REJECTED")
            {
                // Lịch sử từ chối: giữ lại kể cả khi chapter đã được duyệt sau đó (PUBLISHED).
                includeChapterIds = isAdmin
                    ? DataAccessObjects.DAOs.ModerationLogDAO.GetTargetIdsFiltered("CHAPTER", moderatorId: null, dateFrom: null, dateTo: null, action: "REJECTED")
                    : (moderatorId.HasValue
                        ? DataAccessObjects.DAOs.ModerationLogDAO.GetTargetIdsByModeratorAndAction(moderatorId.Value, "CHAPTER", "REJECTED")
                        : new List<Guid>());
                if (includeChapterIds == null || includeChapterIds.Count == 0)
                    return new PagedResultDto<ChapterListItemDto> { Items = new List<ChapterListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
                statusIn = null;
            }
            else if (!isAdmin && moderatorId.HasValue)
            {
                includeChapterIds = DataAccessObjects.DAOs.ModerationLogDAO.GetTargetIdsByModeratorAndAction(moderatorId.Value, "CHAPTER", "APPROVED");
                if (includeChapterIds == null || includeChapterIds.Count == 0)
                    return new PagedResultDto<ChapterListItemDto> { Items = new List<ChapterListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
            }

            if (!isAdmin && (categoryIdsFilter == null || categoryIdsFilter.Count == 0))
                storyIdsFilter = null;

            if (isAdmin && categoryIdsFilter != null && categoryIdsFilter.Count == 0)
                return new PagedResultDto<ChapterListItemDto> { Items = new List<ChapterListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
            if (storyIdsFilter != null && storyIdsFilter.Count == 0)
                return new PagedResultDto<ChapterListItemDto> { Items = new List<ChapterListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };

            var ignoreStatusFilter = statusUpper == "REJECTED" && statusIn == null && includeChapterIds != null;
            var query = new ChapterQueryDto
            {
                Status = (statusIn == null && !ignoreStatusFilter) ? statusUpper : null,
                StatusIn = statusIn,
                StoryIds = storyIdsFilter,
                IncludeChapterIds = includeChapterIds,
                Page = page,
                PageSize = pageSize,
                Search = search,
                SortBy = !string.IsNullOrWhiteSpace(sortBy) ? sortBy : "updated_at",
                SortOrder = !string.IsNullOrWhiteSpace(sortOrder) ? sortOrder : "desc"
            };
            var result = _chapterService.GetAll(query);
            var chapterList = result.Items?.ToList() ?? new List<ChapterListItemDto>();
            // Tab "Từ chối": dù chapter hiện đã PUBLISHED, vẫn cần hiển thị lý do từ chối trước đó trong lịch sử.
            if (statusUpper == "REJECTED" && chapterList.Count > 0)
            {
                foreach (var item in chapterList)
                {
                    var (reason, rejectedAt) = DataAccessObjects.DAOs.ModerationLogDAO.GetLatestRejection("CHAPTER", item.Id);
                    item.RejectionReason = reason;
                    item.RejectedAt = rejectedAt;
                }
            }
            if (chapterList.Count > 0 && isAdmin)
            {
                var logInfo = DataAccessObjects.DAOs.ModerationLogDAO.GetLogInfoByTargets("CHAPTER", chapterList.Select(c => c.Id).ToList(), logAction);
                foreach (var item in chapterList)
                {
                    if (logInfo.TryGetValue(item.Id, out var info))
                    {
                        item.ReviewedAt = info.CreatedAt;
                        item.ReviewedByModeratorName = info.ModeratorId.HasValue ? NotificationDAO.GetUserDisplayName(info.ModeratorId.Value) : null;
                    }
                }
            }
            return result;
        }

        public bool ClaimStory(Guid storyId, Guid moderatorId, DateTime reviewDeadlineAtUtc, IReadOnlyList<Guid>? allowedCategoryIds = null)
        {
            var deadlineUtc = NormalizeToUtc(reviewDeadlineAtUtc);
            ValidateModeratorReviewDeadline(deadlineUtc);

            if (allowedCategoryIds != null && allowedCategoryIds.Count == 0)
                return false;
            var story = _storyRepository.GetById(storyId);
            if (story == null || story.status != "PENDING_REVIEW")
                return false;
            if (allowedCategoryIds != null && allowedCategoryIds.Count > 0 && !story.category.Any(c => allowedCategoryIds.Contains(c.id)))
                return false;
            if (ReviewAssignmentDAO.IsLocked(ReviewAssignmentDAO.TargetTypeStory, storyId))
                return false;
            var ok = ReviewAssignmentDAO.TryClaim(ReviewAssignmentDAO.TargetTypeStory, storyId, moderatorId, deadlineUtc);
            if (ok)
                _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
            return ok;
        }

        public bool ClaimChapter(Guid chapterId, Guid moderatorId, DateTime reviewDeadlineAtUtc, IReadOnlyList<Guid>? allowedCategoryIds = null)
        {
            var deadlineUtc = NormalizeToUtc(reviewDeadlineAtUtc);
            ValidateModeratorReviewDeadline(deadlineUtc);

            if (allowedCategoryIds != null && allowedCategoryIds.Count == 0)
                return false;
            var chapter = _chapterRepository.GetById(chapterId);
            if (chapter == null)
                return false;
            var hasPendingVersion = _versionRepository.GetByChapterId(chapterId)
                .Any(v => string.Equals(v.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase));
            // Cho phép nhận duyệt khi: chapter gốc PENDING_REVIEW, hoặc chapter có ít nhất một version PENDING_REVIEW (kể cả chapter đang DRAFT/REJECTED).
            var canClaim = string.Equals(chapter.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase)
                || hasPendingVersion;
            if (!canClaim)
                return false;
            if (allowedCategoryIds != null && allowedCategoryIds.Count > 0 && chapter.story_id.HasValue)
            {
                var story = StoryDAO.GetById(chapter.story_id.Value);
                if (story == null || !story.category.Any(c => allowedCategoryIds.Contains(c.id)))
                    return false;
            }
            if (ReviewAssignmentDAO.IsLocked(ReviewAssignmentDAO.TargetTypeChapter, chapterId))
                return false;
            var ok = ReviewAssignmentDAO.TryClaim(ReviewAssignmentDAO.TargetTypeChapter, chapterId, moderatorId, deadlineUtc);
            if (ok)
                _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
            return ok;
        }

        /// <summary>Bắt buộc đã "Nhận duyệt" (claim) — không cho duyệt/từ chối khi chưa lock hoặc lock cho người khác.</summary>
        private static void EnsureModeratorHasClaimedForReview(string targetType, Guid targetId, Guid moderatorId)
        {
            if (!ReviewAssignmentDAO.IsLocked(targetType, targetId))
                throw new InvalidOperationException("Bạn phải nhận duyệt mục này trước khi duyệt hoặc từ chối.");
            if (!ReviewAssignmentDAO.IsAssignedTo(targetType, targetId, moderatorId))
                throw new InvalidOperationException("Chỉ moderator đã nhận duyệt mới có thể duyệt hoặc từ chối mục này.");
        }

        /// <summary>Moderator đã gửi đơn báo cáo admin và đơn còn PENDING → không cho duyệt/từ chối đến khi admin xử lý.</summary>
        private static void EnsureNoPendingEscalationBlocksModeratorReview(string targetType, Guid targetId, Guid moderatorId)
        {
            if (!ReviewEscalationDAO.HasPendingForTarget(targetType, targetId))
                return;
            if (!ReviewAssignmentDAO.IsAssignedTo(targetType, targetId, moderatorId))
                return;
            throw new InvalidOperationException("Đang có báo cáo chờ admin xử lý — không thể duyệt hoặc từ chối cho đến khi admin quyết định.");
        }

        /// <summary>Đơn escalation gắn <c>STORY</c> (vd. trả cả truyện về hàng đợi) đang PENDING → chặn duyệt/từ chối chương mà moderator đang giữ.</summary>
        private static void EnsureNoPendingStoryEscalationBlocksChapterReview(Guid chapterId, Guid? storyId, Guid moderatorId)
        {
            if (!storyId.HasValue || storyId.Value == Guid.Empty)
                return;
            if (!ReviewEscalationDAO.HasPendingForTarget(ReviewAssignmentDAO.TargetTypeStory, storyId.Value))
                return;
            if (!ReviewAssignmentDAO.IsAssignedTo(ReviewAssignmentDAO.TargetTypeChapter, chapterId, moderatorId))
                return;
            throw new InvalidOperationException("Đang có báo cáo chờ quản trị viên xử lý ở cấp truyện — không thể duyệt hoặc từ chối chương cho đến khi đơn được xử lý.");
        }

        private static DateTime NormalizeToUtc(DateTime value)
        {
            return value.Kind switch
            {
                DateTimeKind.Utc => value,
                DateTimeKind.Local => value.ToUniversalTime(),
                _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
            };
        }

        private static void ValidateModeratorReviewDeadline(DateTime deadlineUtc)
        {
            var now = DateTime.UtcNow;
            if (deadlineUtc <= now.AddHours(MinHoursUntilDeadline))
                throw new ArgumentException("Hạn duyệt phải sau ít nhất 24 giờ kể từ thời điểm hiện tại.");
            if (deadlineUtc > now.AddDays(MaxDeadlineDaysAhead))
                throw new ArgumentException($"Hạn duyệt không được vượt quá {MaxDeadlineDaysAhead} ngày.");
        }

        /// <summary>Đã nhận duyệt: ưu tiên hạn moderator chọn; bản ghi cũ không có cột → hạn = lúc nhận + 7 ngày. Chưa nhận: hạn gợi ý = lúc gửi + 7 ngày.</summary>
        private static DateTime? ResolveReviewDeadlineUtc(DateTime? pendingSince, (Guid AssigneeId, DateTime AssignedAt, string DisplayName, DateTime? ReviewDeadlineAt)? claim)
        {
            if (claim.HasValue)
            {
                if (claim.Value.ReviewDeadlineAt.HasValue)
                    return NormalizeToUtc(claim.Value.ReviewDeadlineAt.Value);
                return claim.Value.AssignedAt.AddDays(DefaultPolicyDeadlineDays);
            }
            if (pendingSince.HasValue)
                return pendingSince.Value.AddDays(DefaultPolicyDeadlineDays);
            return null;
        }

        public bool ApproveStory(Guid storyId, Guid moderatorId, IReadOnlyList<Guid>? allowedCategoryIds = null)
        {
            if (allowedCategoryIds != null && allowedCategoryIds.Count == 0)
                return false;
            var story = _storyRepository.GetById(storyId);
            if (story == null)
                return false;
            if (story.status != "PENDING_REVIEW")
                return false;
            if (allowedCategoryIds != null && allowedCategoryIds.Count > 0 && !story.category.Any(c => allowedCategoryIds.Contains(c.id)))
                return false;
            EnsureModeratorHasClaimedForReview(ReviewAssignmentDAO.TargetTypeStory, storyId, moderatorId);
            EnsureNoPendingEscalationBlocksModeratorReview(ReviewAssignmentDAO.TargetTypeStory, storyId, moderatorId);

            story.status = "PUBLISHED";
            story.published_at = DateTime.Now;
            story.last_published_at = DateTime.Now;
            story.updated_at = DateTime.Now;
            story.submitted_for_review_at = null;
            _storyRepository.Update(story);

            ReviewAssignmentDAO.CompleteAssignment(ReviewAssignmentDAO.TargetTypeStory, storyId);
            LogModeration("STORY", storyId, "APPROVED", moderatorId, null);
            var storyNotif = NotifyStoryResult(story, "APPROVED", null);
            if (storyNotif != null) _ = PushAuthorNotificationAsync(storyNotif);
            if (story.author_id.HasValue)
            {
                var authorNotifications = NotificationDAO.NotifyAuthorFollowersNewStory(story.author_id.Value, storyId, story.title, _logger);
                _ = PushStoryFollowNotificationsAsync(authorNotifications);
            }
            _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
            return true;
        }

        public bool RejectStory(Guid storyId, Guid moderatorId, string reason, IReadOnlyList<Guid>? allowedCategoryIds = null)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Lý do từ chối là bắt buộc.", nameof(reason));
            if (allowedCategoryIds != null && allowedCategoryIds.Count == 0)
                return false;

            var story = _storyRepository.GetById(storyId);
            if (story == null)
                return false;
            if (story.status != "PENDING_REVIEW")
                return false;
            if (allowedCategoryIds != null && allowedCategoryIds.Count > 0 && !story.category.Any(c => allowedCategoryIds.Contains(c.id)))
                return false;
            EnsureModeratorHasClaimedForReview(ReviewAssignmentDAO.TargetTypeStory, storyId, moderatorId);
            EnsureNoPendingEscalationBlocksModeratorReview(ReviewAssignmentDAO.TargetTypeStory, storyId, moderatorId);

            story.status = "REJECTED";
            story.updated_at = DateTime.Now;
            story.submitted_for_review_at = null;
            _storyRepository.Update(story);

            ReviewAssignmentDAO.CompleteAssignment(ReviewAssignmentDAO.TargetTypeStory, storyId);
            LogModeration("STORY", storyId, "REJECTED", moderatorId, reason.Trim());
            var storyNotif = NotifyStoryResult(story, "REJECTED", reason.Trim());
            if (storyNotif != null) _ = PushAuthorNotificationAsync(storyNotif);
            _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
            return true;
        }

        public bool ApproveChapter(Guid chapterId, Guid moderatorId, IReadOnlyList<Guid>? allowedCategoryIds = null)
        {
            Console.WriteLine($"[CONSOLE] ModerationService.ApproveChapter ENTER ChapterId={chapterId}");
            if (allowedCategoryIds != null && allowedCategoryIds.Count == 0)
            {
                Console.WriteLine($"[CONSOLE] ApproveChapter RETURN FALSE: allowedCategoryIds empty");
                return false;
            }
            var chapter = _chapterRepository.GetById(chapterId);
            if (chapter == null)
            {
                Console.WriteLine($"[CONSOLE] ApproveChapter RETURN FALSE: chapter not found ChapterId={chapterId}");
                return false;
            }
            Console.WriteLine($"[CONSOLE] ApproveChapter chapter found Status={chapter.status} StoryId={chapter.story_id}");
            var hasPendingVersion = _versionRepository.GetByChapterId(chapterId)
                .Any(v => string.Equals(v.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase));
            // Cho phép duyệt khi: chapter gốc PENDING_REVIEW, hoặc chapter có ít nhất một version PENDING_REVIEW (kể cả chapter đang DRAFT).
            var canApprove = string.Equals(chapter.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase)
                || hasPendingVersion;
            if (!canApprove)
            {
                Console.WriteLine($"[CONSOLE] ApproveChapter RETURN FALSE: not pending (current={chapter.status}, hasPendingVersion={hasPendingVersion})");
                return false;
            }
            if (allowedCategoryIds != null && allowedCategoryIds.Count > 0 && chapter.story_id.HasValue)
            {
                var story = StoryDAO.GetById(chapter.story_id.Value);
                if (story == null || !story.category.Any(c => allowedCategoryIds.Contains(c.id)))
                {
                    Console.WriteLine($"[CONSOLE] ApproveChapter RETURN FALSE: story not found or category not allowed");
                    return false;
                }
            }
            EnsureModeratorHasClaimedForReview(ReviewAssignmentDAO.TargetTypeChapter, chapterId, moderatorId);
            EnsureNoPendingEscalationBlocksModeratorReview(ReviewAssignmentDAO.TargetTypeChapter, chapterId, moderatorId);

            // Duyệt theo thứ tự CHỈ khi publish lần đầu cho chapter.
            // Nếu chapter đã từng PUBLISHED (published_at có giá trị) và giờ chỉ gửi version mới,
            // thì bỏ qua bước kiểm tra thứ tự.
            var isFirstTimePublish = !chapter.published_at.HasValue;
            var currentIndex = chapter.order_index;
            if (isFirstTimePublish && currentIndex > 0)
            {
                var storyId = chapter.story_id ?? Guid.Empty;
                var previous = _chapterRepository.GetByStoryIdAndOrderIndex(storyId, currentIndex - 1);
                var previousStatus = (previous?.status ?? "").ToUpperInvariant();
                if (previous == null || previousStatus != "PUBLISHED")
                {
                    var missingIndex = currentIndex - 1;
                    throw new InvalidOperationException(
                        $"Phải duyệt chương theo thứ tự. Cần duyệt chương có thứ tự {missingIndex} trước khi duyệt chương {currentIndex}.");
                }
            }

            // Nếu có version PENDING_REVIEW (gửi chỉnh sửa bản đã xuất bản), áp dụng nội dung version lên chapter trước khi duyệt.
            var pendingVersions = _versionRepository.GetByChapterId(chapterId)
                .Where(v => string.Equals(v.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase))
                .ToList();
            if (pendingVersions.Count > 0)
            {
                var v = pendingVersions[0];
                if (!string.IsNullOrWhiteSpace(v.title_snapshot))
                    chapter.title = v.title_snapshot;
                if (v.content_snapshot != null)
                {
                    chapter.content = v.content_snapshot;
                    chapter.word_count = chapter.content.Trim().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length;
                }
            }

            chapter.status = "PUBLISHED";
            chapter.published_at = DateTime.Now;
            chapter.updated_at = DateTime.Now;
            chapter.submitted_for_review_at = null;
            _chapterRepository.Update(chapter);

            // Cập nhật last_published_at của story nếu cần và gửi thông báo cho user follow story
            Console.WriteLine($"[CONSOLE] ApproveChapter ChapterId={chapterId} StoryId={chapter.story_id} HasStoryId={chapter.story_id.HasValue}");
            _logger.LogWarning("[NOTIFY] ApproveChapter ChapterId={ChapterId} StoryId={StoryId} HasStoryId={HasValue}",
                chapterId, chapter.story_id, chapter.story_id.HasValue);
            if (chapter.story_id.HasValue)
            {
                var story = StoryDAO.GetById(chapter.story_id.Value);
                if (story != null)
                {
                    story.last_published_at = DateTime.Now;
                    StoryDAO.Update(story);
                }
                Console.WriteLine($"[CONSOLE] ApproveChapter calling NotifyStoryFollowersNewChapter StoryId={chapter.story_id.Value} ChapterId={chapterId} StoryTitle={story?.title ?? "(null)"}");
                _logger.LogWarning("[NOTIFY] ApproveChapter calling NotifyStoryFollowersNewChapter StoryId={StoryId} ChapterId={ChapterId}", chapter.story_id.Value, chapterId);
                var createdNotifications = NotificationDAO.NotifyStoryFollowersNewChapter(chapter.story_id.Value, chapterId, chapter.title, story?.title, _logger);
                _ = PushStoryFollowNotificationsAsync(createdNotifications);
                if (story?.author_id != null)
                {
                    var authorNotifications = NotificationDAO.NotifyAuthorFollowersNewChapter(story.author_id.Value, chapter.story_id.Value, chapterId, chapter.title, story.title, _logger);
                    _ = PushStoryFollowNotificationsAsync(authorNotifications);
                }
                TriggerRagIndexInBackground(chapter.story_id.Value, chapterId);
                if (!string.IsNullOrWhiteSpace(chapter.content))
                {
                    ChapterMemoryAnalysisScheduler.TrySchedule(
                        _scopeFactory,
                        _logger,
                        chapter.story_id.Value,
                        chapterId,
                        chapter.title,
                        chapter.order_index,
                        chapter.content);
                }
            }
            else
            {
                Console.WriteLine($"[CONSOLE] ApproveChapter SKIP notify: chapter has no story_id ChapterId={chapterId}");
                _logger.LogWarning("[NOTIFY] ApproveChapter SKIP notify: chapter has no story_id ChapterId={ChapterId}", chapterId);
            }

            ReviewAssignmentDAO.CompleteAssignment(ReviewAssignmentDAO.TargetTypeChapter, chapterId);
            DataAccessObjects.DAOs.ChapterVersionDAO.MarkPendingVersionsAsPublished(chapterId);
            LogModeration("CHAPTER", chapterId, "APPROVED", moderatorId, null);
            var chapterNotif = NotifyChapterResult(chapter, "APPROVED", null);
            if (chapterNotif != null) _ = PushAuthorNotificationAsync(chapterNotif);
            _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
            return true;
        }

        public bool RejectChapter(Guid chapterId, Guid moderatorId, string reason, IReadOnlyList<Guid>? allowedCategoryIds = null)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Lý do từ chối là bắt buộc.", nameof(reason));
            if (allowedCategoryIds != null && allowedCategoryIds.Count == 0)
                return false;

            var chapter = _chapterRepository.GetById(chapterId);
            if (chapter == null)
                return false;
            var hasPendingVersionReject = _versionRepository.GetByChapterId(chapterId)
                .Any(v => string.Equals(v.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase));
            // Cho phép từ chối: chương đang PENDING_REVIEW (từ chối chương) HOẶC có ít nhất một version PENDING_REVIEW (từ chối version, bất kể trạng thái chương gốc).
            var canReject = string.Equals(chapter.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase)
                || hasPendingVersionReject;
            if (!canReject)
                throw new InvalidOperationException("Chương không ở trạng thái chờ duyệt hoặc không có phiên bản chờ duyệt (PENDING_REVIEW).");
            if (allowedCategoryIds != null && allowedCategoryIds.Count > 0 && chapter.story_id.HasValue)
            {
                var story = StoryDAO.GetById(chapter.story_id.Value);
                if (story == null || !story.category.Any(c => allowedCategoryIds.Contains(c.id)))
                    return false;
            }
            EnsureModeratorHasClaimedForReview(ReviewAssignmentDAO.TargetTypeChapter, chapterId, moderatorId);
            EnsureNoPendingEscalationBlocksModeratorReview(ReviewAssignmentDAO.TargetTypeChapter, chapterId, moderatorId);
            EnsureNoPendingStoryEscalationBlocksChapterReview(chapterId, chapter.story_id, moderatorId);

            // Có version chờ duyệt: từ chối (các) version đó, không đổi trạng thái chương gốc (dù chương đang PUBLISHED, DRAFT hay REJECTED).
            // Không ghi LogModeration("CHAPTER", ...) ở đây: lý do từ chối version đã lưu trên từng version (rejection_reason). API "lý do từ chối chương" (GetLatestRejection CHAPTER) chỉ dùng cho khi chương gốc bị từ chối, tránh lý do version đè lên lý do chương.
            if (hasPendingVersionReject)
            {
                var pendingVersions = _versionRepository.GetByChapterId(chapterId)
                    .Where(v => string.Equals(v.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase))
                    .ToList();
                foreach (var pv in pendingVersions)
                {
                    pv.status = "REJECTED";
                    pv.rejection_reason = reason.Trim();
                    pv.reviewed_at = DateTime.Now;
                    pv.reviewed_by = moderatorId;
                    _versionRepository.Update(pv);
                }
                chapter.updated_at = DateTime.Now;
                chapter.submitted_for_review_at = null;
                _chapterRepository.Update(chapter);
                ReviewAssignmentDAO.CompleteAssignment(ReviewAssignmentDAO.TargetTypeChapter, chapterId);
                var chapterNotif = NotifyChapterResult(chapter, "REJECTED", reason.Trim());
                if (chapterNotif != null) _ = PushAuthorNotificationAsync(chapterNotif);
                _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
                return true;
            }

            // Chương gốc đang PENDING_REVIEW: từ chối cả chương.
            chapter.status = "REJECTED";
            chapter.updated_at = DateTime.Now;
            chapter.submitted_for_review_at = null;
            _chapterRepository.Update(chapter);

            ReviewAssignmentDAO.CompleteAssignment(ReviewAssignmentDAO.TargetTypeChapter, chapterId);
            LogModeration("CHAPTER", chapterId, "REJECTED", moderatorId, reason.Trim());
            var chapterNotif2 = NotifyChapterResult(chapter, "REJECTED", reason.Trim());
            if (chapterNotif2 != null) _ = PushAuthorNotificationAsync(chapterNotif2);

            // Cùng truyện: các chương PENDING_REVIEW có order_index lớn hơn chương vừa từ chối cũng bị từ chối (lý do chuỗi).
            if (chapter.story_id.HasValue)
                RejectSubsequentPendingChaptersAfterPriorRejected(chapter.story_id.Value, chapter.order_index, moderatorId, reason.Trim(), allowedCategoryIds);

            _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
            return true;
        }

        /// <summary>
        /// Khi moderator từ chối một chương đang PENDING_REVIEW, tự động từ chối mọi chương sau (cùng truyện, cùng đã nhận duyệt bởi moderator này)
        /// với lý do tham chiếu chương trước bị từ chối.
        /// </summary>
        private void RejectSubsequentPendingChaptersAfterPriorRejected(
            Guid storyId,
            int rejectedOrderIndex,
            Guid moderatorId,
            string primaryRejectionReason,
            IReadOnlyList<Guid>? allowedCategoryIds)
        {
            var primary = (primaryRejectionReason ?? string.Empty).Trim();
            if (primary.Length > 1500)
                primary = primary.Substring(0, 1497) + "...";

            var cascadeReason =
                "Không duyệt chương này vì một chương có thứ tự trước trong cùng truyện đã bị từ chối. Lý do từ chối chương trước: "
                + primary;
            if (cascadeReason.Length > 3900)
                cascadeReason = cascadeReason.Substring(0, 3897) + "...";

            var followers = _chapterRepository.GetByStoryId(storyId)
                .Where(c => c.order_index > rejectedOrderIndex
                    && string.Equals(c.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.order_index)
                .ToList();

            foreach (var ch in followers)
            {
                if (allowedCategoryIds != null && allowedCategoryIds.Count > 0 && ch.story_id.HasValue)
                {
                    var st = StoryDAO.GetById(ch.story_id.Value);
                    if (st == null || !st.category.Any(c => allowedCategoryIds.Contains(c.id)))
                        continue;
                }

                if (!ReviewAssignmentDAO.IsLocked(ReviewAssignmentDAO.TargetTypeChapter, ch.id))
                    continue;
                if (!ReviewAssignmentDAO.IsAssignedTo(ReviewAssignmentDAO.TargetTypeChapter, ch.id, moderatorId))
                    continue;

                try
                {
                    EnsureNoPendingEscalationBlocksModeratorReview(ReviewAssignmentDAO.TargetTypeChapter, ch.id, moderatorId);
                    EnsureNoPendingStoryEscalationBlocksChapterReview(ch.id, ch.story_id, moderatorId);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogInformation(ex, "Cascade reject skipped for chapter {ChapterId}: escalation/claim guard.", ch.id);
                    continue;
                }

                var fresh = _chapterRepository.GetById(ch.id);
                if (fresh == null || !string.Equals(fresh.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase))
                    continue;

                fresh.status = "REJECTED";
                fresh.updated_at = DateTime.Now;
                fresh.submitted_for_review_at = null;
                _chapterRepository.Update(fresh);

                ReviewAssignmentDAO.CompleteAssignment(ReviewAssignmentDAO.TargetTypeChapter, fresh.id);
                LogModeration("CHAPTER", fresh.id, "REJECTED", moderatorId, cascadeReason);
                var n = NotifyChapterResult(fresh, "REJECTED", cascadeReason);
                if (n != null) _ = PushAuthorNotificationAsync(n);
            }
        }

        private static notifications? NotifyStoryResult(stories story, string action, string? rejectionReason)
        {
            if (story.author_id == null) return null;
            var title = action == "APPROVED"
                ? "Truyện đã được duyệt"
                : "Truyện bị từ chối";
            var content = action == "APPROVED"
                ? $"Truyện \"{story.title}\" đã được phê duyệt và xuất bản."
                : $"Truyện \"{story.title}\" không được phê duyệt. Lý do: {rejectionReason}";
            var linkUrl = $"/Stories/Details/{story.id}";
            if (action == "REJECTED") linkUrl = $"/Stories/Details/{story.id}"; // Author xem truyện để thấy lý do
            var n = new notifications
            {
                id = Guid.NewGuid(),
                user_id = story.author_id,
                type = "STORY_" + action,
                title = title,
                content = content,
                link_url = linkUrl,
                is_read = false,
                created_at = DateTime.UtcNow
            };
            NotificationDAO.Add(n);
            return n;
        }

        private static notifications? NotifyChapterResult(chapters chapter, string action, string? rejectionReason)
        {
            var story = chapter.story_id.HasValue ? StoryDAO.GetById(chapter.story_id.Value) : null;
            if (story?.author_id == null) return null;
            var title = action == "APPROVED"
                ? "Chapter đã được duyệt"
                : "Chapter bị từ chối";
            var content = action == "APPROVED"
                ? $"Chapter \"{chapter.title}\" đã được phê duyệt và xuất bản."
                : $"Chapter \"{chapter.title}\" không được phê duyệt. Lý do: {rejectionReason}";
            var linkUrl = chapter.story_id.HasValue ? $"/Stories/Details/{chapter.story_id}" : "/Chapters/Index";
            if (action == "REJECTED") linkUrl = $"/Chapters/Index?storyId={chapter.story_id}";
            var n = new notifications
            {
                id = Guid.NewGuid(),
                user_id = story.author_id,
                type = "CHAPTER_" + action,
                title = title,
                content = content,
                link_url = linkUrl,
                is_read = false,
                created_at = DateTime.UtcNow
            };
            NotificationDAO.Add(n);
            return n;
        }

        /// <summary>Push real-time notification tới tác giả (khi duyệt/từ chối truyện hoặc chương) để UI cập nhật ngay không cần reload.</summary>
        private async Task PushAuthorNotificationAsync(notifications n)
        {
            if (n?.user_id == null || _notificationHubNotifier == null) return;
            try
            {
                var dto = new NotificationDto
                {
                    Id = n.id,
                    Type = n.type,
                    Title = n.title,
                    Content = n.content,
                    LinkUrl = n.link_url,
                    IsRead = n.is_read == true,
                    CreatedAt = n.created_at
                };
                await _notificationHubNotifier.NotifyUserAsync(n.user_id.Value, dto);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Push author notification failed. UserId={UserId} NotificationId={NotificationId}", n.user_id, n.id);
            }
        }

        private async Task PushStoryFollowNotificationsAsync(List<notifications> created)
        {
            if (created == null || created.Count == 0 || _notificationHubNotifier == null)
                return;
            foreach (var n in created)
            {
                if (n.user_id == null) continue;
                try
                {
                    var dto = new NotificationDto
                    {
                        Id = n.id,
                        Type = n.type,
                        Title = n.title,
                        Content = n.content,
                        LinkUrl = n.link_url,
                        IsRead = n.is_read == true,
                        CreatedAt = n.created_at
                    };
                    await _notificationHubNotifier.NotifyUserAsync(n.user_id.Value, dto);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Push notification to follower failed. UserId={UserId} NotificationId={NotificationId}", n.user_id, n.id);
                }
            }
        }

        /// <summary>Chạy index RAG cho truyện trong nền khi moderator duyệt chương (PUBLISHED). RAG dùng cho co-create / suggest-next-chapter.</summary>
        private void TriggerRagIndexInBackground(Guid storyId, Guid chapterId)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();
                    var rag = scope.ServiceProvider.GetRequiredService<IStoryRagService>();
                    await rag.EnsureIndexedAsync(storyId, chapterId, default);
                    _logger.LogInformation("RAG index completed after chapter approve StoryId={StoryId} ChapterId={ChapterId}", storyId, chapterId);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "RAG index after chapter approve failed StoryId={StoryId} ChapterId={ChapterId}", storyId, chapterId);
                }
            });
        }

        private static void LogModeration(string targetType, Guid targetId, string action, Guid moderatorId, string? rejectionReason)
        {
            var log = new moderation_logs
            {
                moderator_id = moderatorId,
                target_type = targetType,
                target_id = targetId,
                action = action,
                rejection_reason = rejectionReason,
                created_at = DateTime.Now
            };
            ModerationLogDAO.Add(log);
        }

        public ChapterReviewContentDto? GetChapterReviewContent(Guid chapterId)
        {
            var chapter = _chapterRepository.GetById(chapterId);
            if (chapter == null) return null;

            var dto = new ChapterReviewContentDto
            {
                ChapterId = chapterId,
                ChapterStatus = chapter.status,
                OriginalTitle = chapter.title,
                OriginalContent = chapter.content
            };

            var pendingVersions = _versionRepository.GetByChapterId(chapterId)
                .Where(v => string.Equals(v.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase))
                .OrderBy(v => v.version_number)
                .ToList();

            if (pendingVersions.Count > 0)
            {
                dto.HasPendingVersion = true;
                dto.PendingVersions = pendingVersions.Select(v => new PendingVersionItemDto
                {
                    Id = v.id,
                    VersionNumber = v.version_number,
                    TitleSnapshot = v.title_snapshot,
                    ContentSnapshot = v.content_snapshot,
                    Status = v.status
                }).ToList();
            }

            return dto;
        }
    }
}
