using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Repositories;
using Services.DTOs.Chapters;
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

        public ModerationService(
            IStoryRepository storyRepository,
            IChapterRepository chapterRepository,
            IStoryService storyService,
            IChapterService chapterService,
            IModerationHubNotifier? moderationHubNotifier = null)
        {
            _storyRepository = storyRepository;
            _chapterRepository = chapterRepository;
            _storyService = storyService;
            _chapterService = chapterService;
            _moderationHubNotifier = moderationHubNotifier;
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

            // Truyện có ít nhất một chương PENDING_REVIEW cũng hiển thị trong danh sách chờ duyệt (dù truyện đã PUBLISHED)
            List<Guid>? alsoIncludeStoryIds = null;
            var chapterQuery = new ChapterQueryDto
            {
                Status = "PENDING_REVIEW",
                Page = 1,
                PageSize = 10000,
                StoryIds = categoryIdsFilter != null && categoryIdsFilter.Count > 0
                    ? _storyRepository.GetStoryIdsByCategoryIds(categoryIdsFilter).ToList()
                    : null
            };
            var pendingChaptersResult = _chapterService.GetAll(chapterQuery);
            var storyIdsWithPendingChapters = pendingChaptersResult.Items
                .Select(c => c.StoryId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
            if (storyIdsWithPendingChapters.Count > 0)
                alsoIncludeStoryIds = storyIdsWithPendingChapters;

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
                IncludeStoryIds = includeStoryIds != null && includeStoryIds.Count > 0 ? includeStoryIds : null,
                AlsoIncludeStoryIds = alsoIncludeStoryIds
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

        public bool ClaimStory(Guid storyId, Guid moderatorId, IReadOnlyList<Guid>? allowedCategoryIds = null)
        {
            if (allowedCategoryIds != null && allowedCategoryIds.Count == 0)
                return false;
            var story = _storyRepository.GetById(storyId);
            if (story == null)
                return false;
            // Cho phép claim khi truyện PENDING_REVIEW hoặc khi truyện có ít nhất một chương PENDING_REVIEW (truyện đã có chương publish nhưng còn chương chờ duyệt)
            if (story.status != "PENDING_REVIEW")
            {
                var storyChapters = _chapterRepository.GetByStoryId(storyId);
                if (!storyChapters.Any(c => string.Equals(c.status, "PENDING_REVIEW", StringComparison.OrdinalIgnoreCase)))
                    return false;
            }
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
            NotifyStoryResult(story, "APPROVED", null);
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
            NotifyStoryResult(story, "REJECTED", reason.Trim());
            _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
            return true;
        }

        public bool ApproveChapter(Guid chapterId, Guid moderatorId, IReadOnlyList<Guid>? allowedCategoryIds = null)
        {
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

            chapter.status = "PUBLISHED";
            chapter.published_at = DateTime.Now;
            chapter.updated_at = DateTime.Now;
            _chapterRepository.Update(chapter);

            // Cập nhật last_published_at của story nếu cần
            if (chapter.story_id.HasValue)
            {
                var story = StoryDAO.GetById(chapter.story_id.Value);
                if (story != null)
                {
                    story.last_published_at = DateTime.Now;
                    StoryDAO.Update(story);
                }
            }

            ReviewAssignmentDAO.CompleteAssignment(ReviewAssignmentDAO.TargetTypeChapter, chapterId);
            LogModeration("CHAPTER", chapterId, "APPROVED", moderatorId, null);
            NotifyChapterResult(chapter, "APPROVED", null);
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
            NotifyChapterResult(chapter, "REJECTED", reason.Trim());
            _ = _moderationHubNotifier?.NotifyPendingListChangedAsync();
            return true;
        }

        private static void NotifyStoryResult(stories story, string action, string? rejectionReason)
        {
            if (story.author_id == null) return;
            var title = action == "APPROVED"
                ? "Truyện đã được duyệt"
                : "Truyện bị từ chối";
            var content = action == "APPROVED"
                ? $"Truyện \"{story.title}\" đã được phê duyệt và xuất bản."
                : $"Truyện \"{story.title}\" không được phê duyệt. Lý do: {rejectionReason}";
            var linkUrl = $"/Stories/Details/{story.id}";
            if (action == "REJECTED") linkUrl = $"/Stories/Details/{story.id}"; // Author xem truyện để thấy lý do
            NotificationDAO.Add(new notifications
            {
                id = Guid.NewGuid(),
                user_id = story.author_id,
                type = "STORY_" + action,
                title = title,
                content = content,
                link_url = linkUrl,
                is_read = false,
                created_at = DateTime.Now
            });
        }

        private static void NotifyChapterResult(chapters chapter, string action, string? rejectionReason)
        {
            var story = chapter.story_id.HasValue ? StoryDAO.GetById(chapter.story_id.Value) : null;
            if (story?.author_id == null) return;
            var title = action == "APPROVED"
                ? "Chapter đã được duyệt"
                : "Chapter bị từ chối";
            var content = action == "APPROVED"
                ? $"Chapter \"{chapter.title}\" đã được phê duyệt và xuất bản."
                : $"Chapter \"{chapter.title}\" không được phê duyệt. Lý do: {rejectionReason}";
            var linkUrl = chapter.story_id.HasValue ? $"/Stories/Details/{chapter.story_id}" : "/Chapters/Index";
            if (action == "REJECTED") linkUrl = $"/Chapters/Index?storyId={chapter.story_id}";
            NotificationDAO.Add(new notifications
            {
                id = Guid.NewGuid(),
                user_id = story.author_id,
                type = "CHAPTER_" + action,
                title = title,
                content = content,
                link_url = linkUrl,
                is_read = false,
                created_at = DateTime.Now
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
    }
}
