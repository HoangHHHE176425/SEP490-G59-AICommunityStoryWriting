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
    /// <param name="deleteIncludingVersions">true: xóa mọi chapter_versions của chương rồi xóa chương (sau khi user xác nhận).</param>
    bool Delete(Guid id, bool deleteIncludingVersions = false);
    bool Publish(Guid id);
    bool Unpublish(Guid id);
    bool Reorder(Guid id, int newOrderIndex);
    /// <summary>Lấy lý do từ chối gần nhất của chapter (từ moderation_logs), bất kể status hiện tại.</summary>
    (string? reason, DateTime? rejectedAt) GetLatestRejectionForChapter(Guid chapterId);
}