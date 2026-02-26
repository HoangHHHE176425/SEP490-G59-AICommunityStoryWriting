using Services.DTOs.Chapters;
using Services.DTOs.Moderation;
using Services.DTOs.Stories;

namespace Services.Interfaces
{
    public interface IModerationService
    {
        PagedResultDto<StoryListItemDto> GetPendingStories(int page = 1, int pageSize = 20, string? search = null, string? sortBy = null, string? sortOrder = null);

        PagedResultDto<ChapterListItemDto> GetPendingChapters(int page = 1, int pageSize = 20, Guid? storyId = null, string? search = null, string? sortBy = null, string? sortOrder = null);

        bool ApproveStory(Guid storyId, Guid moderatorId);

        bool RejectStory(Guid storyId, Guid moderatorId, string reason);

        bool ApproveChapter(Guid chapterId, Guid moderatorId);

        bool RejectChapter(Guid chapterId, Guid moderatorId, string reason);
    }
}
