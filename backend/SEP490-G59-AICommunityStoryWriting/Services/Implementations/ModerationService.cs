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

        public ModerationService(
            IStoryRepository storyRepository,
            IChapterRepository chapterRepository,
            IStoryService storyService,
            IChapterService chapterService)
        {
            _storyRepository = storyRepository;
            _chapterRepository = chapterRepository;
            _storyService = storyService;
            _chapterService = chapterService;
        }

        public PagedResultDto<StoryListItemDto> GetPendingStories(int page = 1, int pageSize = 20, string? search = null, string? sortBy = null, string? sortOrder = null)
        {
            var query = new StoryQueryDto
            {
                Status = "PENDING_REVIEW",
                Page = page,
                PageSize = pageSize,
                Search = search,
                SortBy = !string.IsNullOrWhiteSpace(sortBy) ? sortBy : "updated_at",
                SortOrder = !string.IsNullOrWhiteSpace(sortOrder) ? sortOrder : "desc"
            };
            return _storyService.GetAll(query);
        }

        public PagedResultDto<ChapterListItemDto> GetPendingChapters(int page = 1, int pageSize = 20, Guid? storyId = null, string? search = null, string? sortBy = null, string? sortOrder = null)
        {
            var query = new ChapterQueryDto
            {
                Status = "PENDING_REVIEW",
                StoryId = storyId,
                Page = page,
                PageSize = pageSize,
                Search = search,
                SortBy = !string.IsNullOrWhiteSpace(sortBy) ? sortBy : "created_at",
                SortOrder = !string.IsNullOrWhiteSpace(sortOrder) ? sortOrder : "desc"
            };
            return _chapterService.GetAll(query);
        }

        public bool ApproveStory(Guid storyId, Guid moderatorId)
        {
            var story = _storyRepository.GetById(storyId);
            if (story == null)
                return false;
            if (story.status != "PENDING_REVIEW")
                return false;

            story.status = "PUBLISHED";
            story.published_at = DateTime.Now;
            story.last_published_at = DateTime.Now;
            story.updated_at = DateTime.Now;
            _storyRepository.Update(story);

            LogModeration("STORY", storyId, "APPROVED", moderatorId, null);
            NotifyStoryResult(story, "APPROVED", null);
            return true;
        }

        public bool RejectStory(Guid storyId, Guid moderatorId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Lý do từ chối là bắt buộc.", nameof(reason));

            var story = _storyRepository.GetById(storyId);
            if (story == null)
                return false;
            if (story.status != "PENDING_REVIEW")
                return false;

            story.status = "REJECTED";
            story.updated_at = DateTime.Now;
            _storyRepository.Update(story);

            LogModeration("STORY", storyId, "REJECTED", moderatorId, reason.Trim());
            NotifyStoryResult(story, "REJECTED", reason.Trim());
            return true;
        }

        public bool ApproveChapter(Guid chapterId, Guid moderatorId)
        {
            var chapter = _chapterRepository.GetById(chapterId);
            if (chapter == null)
                return false;
            if (chapter.status != "PENDING_REVIEW")
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

            LogModeration("CHAPTER", chapterId, "APPROVED", moderatorId, null);
            NotifyChapterResult(chapter, "APPROVED", null);
            return true;
        }

        public bool RejectChapter(Guid chapterId, Guid moderatorId, string reason)
        {
            if (string.IsNullOrWhiteSpace(reason))
                throw new ArgumentException("Lý do từ chối là bắt buộc.", nameof(reason));

            var chapter = _chapterRepository.GetById(chapterId);
            if (chapter == null)
                return false;
            if (chapter.status != "PENDING_REVIEW")
                return false;

            chapter.status = "REJECTED";
            chapter.updated_at = DateTime.Now;
            _chapterRepository.Update(chapter);

            LogModeration("CHAPTER", chapterId, "REJECTED", moderatorId, reason.Trim());
            NotifyChapterResult(chapter, "REJECTED", reason.Trim());
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
