using Services.DTOs.Chapters;
using Services.DTOs.Stories;

public interface IChapterService
{
    ChapterResponseDto Create(CreateChapterRequestDto request);
    PagedResultDto<ChapterListItemDto> GetAll(ChapterQueryDto query);
    ChapterResponseDto? GetById(Guid id);
    IEnumerable<ChapterListItemDto> GetByStoryId(Guid storyId);
    ChapterResponseDto? GetByStoryIdAndOrderIndex(Guid storyId, int orderIndex);
    bool Update(Guid id, UpdateChapterRequestDto request);
    bool Delete(Guid id);
    bool Publish(Guid id);
    bool Unpublish(Guid id);
    bool Reorder(Guid id, int newOrderIndex);
}
