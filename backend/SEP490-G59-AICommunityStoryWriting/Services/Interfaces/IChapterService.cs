using Services.DTOs.Chapters;
using Services.DTOs.Stories;

public interface IChapterService
{
    ChapterResponseDto Create(CreateChapterRequestDto request);
    PagedResultDto<ChapterListItemDto> GetAll(ChapterQueryDto query);
    ChapterResponseDto? GetById(int id);
    IEnumerable<ChapterListItemDto> GetByStoryId(int storyId);
    ChapterResponseDto? GetByStoryIdAndOrderIndex(int storyId, int orderIndex);
    bool Update(int id, UpdateChapterRequestDto request);
    bool Delete(int id);
    bool Publish(int id);
    bool Unpublish(int id);
    bool Reorder(int id, int newOrderIndex);
}
