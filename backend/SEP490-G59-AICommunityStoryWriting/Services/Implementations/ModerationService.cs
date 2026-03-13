using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.Extensions.Logging;
using Repositories;
using Services.DTOs.Chapters;
using Services.DTOs.Notifications;
using Services.DTOs.Stories;
using Services.Interfaces;

namespace Services.Implementations
{
    public class ModerationService : IModerationService
    {
        private readonly IStoryRepository _storyRepository;
        private readonly IChapterRepository _chapterRepository;
        private readonly IStoryService _storyService;
        private readonly IChapterService _chapterService;
        private readonly IModerationHubNotifier? _moderationHubNotifier;
        private readonly INotificationHubNotifier? _notificationHubNotifier;
        private readonly ILogger<ModerationService> _logger;

        public ModerationService(
            IStoryRepository storyRepository,
            IChapterRepository chapterRepository,
            IStoryService storyService,
            IChapterService chapterService,
            ILogger<ModerationService> logger,
            IModerationHubNotifier? moderationHubNotifier = null,
            INotificationHubNotifier? notificationHubNotifier = null)
        {
            _storyRepository = storyRepository;
            _chapterRepository = chapterRepository;
            _storyService = storyService;
            _chapterService = chapterService;
            _logger = logger;
            _moderationHubNotifier = moderationHubNotifier;
            _notificationHubNotifier = notificationHubNotifier;
        }

        public PagedResultDto<StoryListItemDto> GetPendingStories(int page = 1, int pageSize = 20, string? search = null, string? sortBy = null, string? sortOrder = null, IReadOnlyList<Guid>? categoryIdsFilter = null, Guid? moderatorId = null, string? claimFilter = null)
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

            var query = new StoryQueryDto
            {
                Status = "PENDING_REVIEW",
                Page = page,
                PageSize = pageSize,
                Search = search,
                SortBy = !string.IsNullOrWhiteSpace(sortBy) ? sortBy : "updated_at",
                SortOrder = !string.IsNullOrWhiteSpace(sortOrder) ? sortOrder : "asc",
                CategoryIds = categoryIdsFilter != null ? categoryIdsFilter.ToList() : null,
                ExcludeStoryIds = excludeStoryIds != null && excludeStoryIds.Count > 0 ? excludeStoryIds : null,
                IncludeStoryIds = includeStoryIds != null && includeStoryIds.Count > 0 ? includeStoryIds : null
            };
            var result = _storyService.GetAll(query);
            foreach (var item in result.Items)
            {
                var claim = ReviewAssignmentDAO.GetClaimInfo(ReviewAssignmentDAO.TargetTypeStory, item.Id);
                if (claim.HasValue)
                {
                    item.ClaimedAt = claim.Value.AssignedAt;
                    item.ClaimedByDisplayName = claim.Value.DisplayName;
                    item.IsClaimedByMe = moderatorId.HasValue && claim.Value.AssigneeId == moderatorId.Value;
                }
            }
            return result;
        }

        public PagedResultDto<ChapterListItemDto> GetPendingChapters(int page = 1, int pageSize = 20, Guid? storyId = null, string? search = null, string? sortBy = null, string? sortOrder = null, IReadOnlyList<Guid>? categoryIdsFilter = null, Guid? moderatorId = null, string? claimFilter = null)
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

            var query = new ChapterQueryDto
            {
                Status = "PENDING_REVIEW",
                StoryId = storyId,
                StoryIds = storyIdsFilter,
                ExcludeChapterIds = excludeChapterIds != null && excludeChapterIds.Count > 0 ? excludeChapterIds : null,
                IncludeChapterIds = includeChapterIds != null && includeChapterIds.Count > 0 ? includeChapterIds : null,
                Page = page,
                PageSize = pageSize,
                Search = search,
                SortBy = !string.IsNullOrWhiteSpace(sortBy) ? sortBy : "created_at",
                SortOrder = !string.IsNullOrWhiteSpace(sortOrder) ? sortOrder : "asc"
            };
            var result = _chapterService.GetAll(query);
            foreach (var item in result.Items)
            {
                var claim = ReviewAssignmentDAO.GetClaimInfo(ReviewAssignmentDAO.TargetTypeChapter, item.Id);
                if (claim.HasValue)
                {
                    item.ClaimedAt = claim.Value.AssignedAt;
                    item.ClaimedByDisplayName = claim.Value.DisplayName;
                    item.IsClaimedByMe = moderatorId.HasValue && claim.Value.AssigneeId == moderatorId.Value;
                }
            }
            return result;
        }

        public PagedResultDto<StoryListItemDto> GetReviewedStories(int page, int pageSize, string status, string? search, string? sortBy, string? sortOrder, IReadOnlyList<Guid>? categoryIdsFilter, Guid? moderatorId, bool isAdmin)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            var statusUpper = (status ?? "").Trim().ToUpperInvariant();
            if (statusUpper != "PUBLISHED" && statusUpper != "REJECTED")
                return new PagedResultDto<StoryListItemDto> { Items = new List<StoryListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };

            List<Guid>? includeStoryIds = null;
            List<string>? statusIn = null;
            if (statusUpper == "REJECTED")
            {
                // Tab "Từ chối": hiển thị theo hành động cuối = REJECTED (vẫn hiển thị sau khi tác giả gửi lại PENDING_REVIEW cho đến khi moderator duyệt).
                includeStoryIds = DataAccessObjects.DAOs.ModerationLogDAO.GetTargetIdsWhereLastActionIs("STORY", "REJECTED", isAdmin ? null : moderatorId);
                if (includeStoryIds == null || includeStoryIds.Count == 0)
                    return new PagedResultDto<StoryListItemDto> { Items = new List<StoryListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
                statusIn = new List<string> { "REJECTED", "PENDING_REVIEW" };
            }
            else if (!isAdmin && moderatorId.HasValue)
            {
                var action = "APPROVED";
                includeStoryIds = DataAccessObjects.DAOs.ModerationLogDAO.GetTargetIdsByModeratorAndAction(moderatorId.Value, "STORY", action);
                if (includeStoryIds == null || includeStoryIds.Count == 0)
                    return new PagedResultDto<StoryListItemDto> { Items = new List<StoryListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
            }

            var query = new StoryQueryDto
            {
                Status = statusIn == null ? statusUpper : null,
                StatusIn = statusIn,
                Page = page,
                PageSize = pageSize,
                Search = search,
                SortBy = !string.IsNullOrWhiteSpace(sortBy) ? sortBy : "updated_at",
                SortOrder = !string.IsNullOrWhiteSpace(sortOrder) ? sortOrder : "desc",
                CategoryIds = isAdmin ? (categoryIdsFilter != null ? categoryIdsFilter.ToList() : null) : null,
                IncludeStoryIds = includeStoryIds
            };
            return _storyService.GetAll(query);
        }

        public PagedResultDto<ChapterListItemDto> GetReviewedChapters(int page, int pageSize, string status, string? search, string? sortBy, string? sortOrder, IReadOnlyList<Guid>? categoryIdsFilter, Guid? moderatorId, bool isAdmin)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            var statusUpper = (status ?? "").Trim().ToUpperInvariant();
            if (statusUpper != "PUBLISHED" && statusUpper != "REJECTED")
                return new PagedResultDto<ChapterListItemDto> { Items = new List<ChapterListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };

            List<Guid>? storyIdsFilter = null;
            List<Guid>? includeChapterIds = null;
            List<string>? statusIn = null;

            if (isAdmin && categoryIdsFilter != null && categoryIdsFilter.Count > 0)
                storyIdsFilter = _storyRepository.GetStoryIdsByCategoryIds(categoryIdsFilter).ToList();
            if (statusUpper == "REJECTED")
            {
                // Tab "Từ chối": hiển thị theo hành động cuối = REJECTED (vẫn hiển thị sau khi tác giả gửi lại PENDING_REVIEW cho đến khi moderator duyệt).
                includeChapterIds = DataAccessObjects.DAOs.ModerationLogDAO.GetTargetIdsWhereLastActionIs("CHAPTER", "REJECTED", isAdmin ? null : moderatorId);
                if (includeChapterIds == null || includeChapterIds.Count == 0)
                    return new PagedResultDto<ChapterListItemDto> { Items = new List<ChapterListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
                statusIn = new List<string> { "REJECTED", "PENDING_REVIEW" };
            }
            else if (!isAdmin && moderatorId.HasValue)
            {
                var action = "APPROVED";
                includeChapterIds = DataAccessObjects.DAOs.ModerationLogDAO.GetTargetIdsByModeratorAndAction(moderatorId.Value, "CHAPTER", action);
                if (includeChapterIds == null || includeChapterIds.Count == 0)
                    return new PagedResultDto<ChapterListItemDto> { Items = new List<ChapterListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
            }

            if (!isAdmin && (categoryIdsFilter == null || categoryIdsFilter.Count == 0))
                storyIdsFilter = null;

            if (isAdmin && categoryIdsFilter != null && categoryIdsFilter.Count == 0)
                return new PagedResultDto<ChapterListItemDto> { Items = new List<ChapterListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };
            if (storyIdsFilter != null && storyIdsFilter.Count == 0)
                return new PagedResultDto<ChapterListItemDto> { Items = new List<ChapterListItemDto>(), TotalCount = 0, Page = page, PageSize = pageSize };

            var query = new ChapterQueryDto
            {
                Status = statusIn == null ? statusUpper : null,
                StatusIn = statusIn,
                StoryIds = storyIdsFilter,
                IncludeChapterIds = includeChapterIds,
                Page = page,
                PageSize = pageSize,
                Search = search,
                SortBy = !string.IsNullOrWhiteSpace(sortBy) ? sortBy : "updated_at",
                SortOrder = !string.IsNullOrWhiteSpace(sortOrder) ? sortOrder : "desc"
            };
            return _chapterService.GetAll(query);
        }

        public bool ClaimStory(Guid storyId, Guid moderatorId, IReadOnlyList<Guid>? allowedCategoryIds = null)
        {
            if (allowedCategoryIds != null && allowedCategoryIds.Count == 0)
                return false;
            var story = _storyRepository.GetById(storyId);
            if (story == null || story.status != "PENDING_REVIEW")
                return false;
            if (allowedCategoryIds != null && allowedCategoryIds.Count > 0 && !story.category.Any(c => allowedCategoryIds.Contains(c.id)))
                return false;
            if (ReviewAssignmentDAO.IsLocked(ReviewAssignmentDAO.TargetTypeStory, storyId))
                return false;
            var ok = ReviewAssignmentDAO.TryClaim(ReviewAssignmentDAO.TargetTypeStory, storyId, moderatorId);
            if (ok)
                _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
            return ok;
        }

        public bool ClaimChapter(Guid chapterId, Guid moderatorId, IReadOnlyList<Guid>? allowedCategoryIds = null)
        {
            if (allowedCategoryIds != null && allowedCategoryIds.Count == 0)
                return false;
            var chapter = _chapterRepository.GetById(chapterId);
            if (chapter == null || chapter.status != "PENDING_REVIEW")
                return false;
            if (allowedCategoryIds != null && allowedCategoryIds.Count > 0 && chapter.story_id.HasValue)
            {
                var story = StoryDAO.GetById(chapter.story_id.Value);
                if (story == null || !story.category.Any(c => allowedCategoryIds.Contains(c.id)))
                    return false;
            }
            if (ReviewAssignmentDAO.IsLocked(ReviewAssignmentDAO.TargetTypeChapter, chapterId))
                return false;
            var ok = ReviewAssignmentDAO.TryClaim(ReviewAssignmentDAO.TargetTypeChapter, chapterId, moderatorId);
            if (ok)
                _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
            return ok;
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
            if (ReviewAssignmentDAO.IsLocked(ReviewAssignmentDAO.TargetTypeStory, storyId) && !ReviewAssignmentDAO.IsAssignedTo(ReviewAssignmentDAO.TargetTypeStory, storyId, moderatorId))
                return false;

            story.status = "PUBLISHED";
            story.published_at = DateTime.Now;
            story.last_published_at = DateTime.Now;
            story.updated_at = DateTime.Now;
            _storyRepository.Update(story);

            ReviewAssignmentDAO.CompleteAssignment(ReviewAssignmentDAO.TargetTypeStory, storyId);
            LogModeration("STORY", storyId, "APPROVED", moderatorId, null);
            var storyNotif = NotifyStoryResult(story, "APPROVED", null);
            if (storyNotif != null) _ = PushAuthorNotificationAsync(storyNotif);
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
            if (ReviewAssignmentDAO.IsLocked(ReviewAssignmentDAO.TargetTypeStory, storyId) && !ReviewAssignmentDAO.IsAssignedTo(ReviewAssignmentDAO.TargetTypeStory, storyId, moderatorId))
                return false;

            story.status = "REJECTED";
            story.updated_at = DateTime.Now;
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
            if (chapter.status != "PENDING_REVIEW")
            {
                Console.WriteLine($"[CONSOLE] ApproveChapter RETURN FALSE: status not PENDING_REVIEW (current={chapter.status})");
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
            if (ReviewAssignmentDAO.IsLocked(ReviewAssignmentDAO.TargetTypeChapter, chapterId) && !ReviewAssignmentDAO.IsAssignedTo(ReviewAssignmentDAO.TargetTypeChapter, chapterId, moderatorId))
            {
                Console.WriteLine($"[CONSOLE] ApproveChapter RETURN FALSE: chapter locked by another moderator");
                return false;
            }

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

            chapter.status = "PUBLISHED";
            chapter.published_at = DateTime.Now;
            chapter.updated_at = DateTime.Now;
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
            if (chapter.status != "PENDING_REVIEW")
                return false;
            if (allowedCategoryIds != null && allowedCategoryIds.Count > 0 && chapter.story_id.HasValue)
            {
                var story = StoryDAO.GetById(chapter.story_id.Value);
                if (story == null || !story.category.Any(c => allowedCategoryIds.Contains(c.id)))
                    return false;
            }
            if (ReviewAssignmentDAO.IsLocked(ReviewAssignmentDAO.TargetTypeChapter, chapterId) && !ReviewAssignmentDAO.IsAssignedTo(ReviewAssignmentDAO.TargetTypeChapter, chapterId, moderatorId))
                return false;

            chapter.status = "REJECTED";
            chapter.updated_at = DateTime.Now;
            _chapterRepository.Update(chapter);

            ReviewAssignmentDAO.CompleteAssignment(ReviewAssignmentDAO.TargetTypeChapter, chapterId);
            LogModeration("CHAPTER", chapterId, "REJECTED", moderatorId, reason.Trim());
            var chapterNotif = NotifyChapterResult(chapter, "REJECTED", reason.Trim());
            if (chapterNotif != null) _ = PushAuthorNotificationAsync(chapterNotif);
            _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
            return true;
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
                created_at = DateTime.Now
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
                created_at = DateTime.Now
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
    }
}
