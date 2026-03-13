using Services.DTOs.Chapters;

namespace Services.Interfaces
{
    public interface IChapterVersionService
    {
        IReadOnlyList<ChapterVersionListItemDto> GetByChapterId(Guid chapterId);
        ChapterVersionDetailDto? GetById(Guid id);
        ChapterVersionDetailDto? Create(Guid chapterId, Guid authorId, CreateChapterVersionRequestDto request);
        bool Update(Guid id, Guid authorId, UpdateChapterVersionRequestDto request);
        bool Delete(Guid id, Guid authorId);
        bool SubmitForReview(Guid versionId, Guid authorId);
    }
}
