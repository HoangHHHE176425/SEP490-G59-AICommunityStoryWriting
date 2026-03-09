using BusinessObjects.Entities;

namespace Repositories.Interfaces;

public interface IStoryChapterChunkRepository
{
    List<story_chapter_chunks> GetByStoryId(Guid storyId);
    List<story_chapter_chunks> GetByStoryIdWithEmbeddings(Guid storyId);
    List<story_chapter_chunks> GetChunksByIds(IEnumerable<Guid> ids);
    int CountByStoryId(Guid storyId);
    void DeleteByStoryId(Guid storyId);
    void DeleteByChapterId(Guid chapterId);
    void AddRange(IEnumerable<story_chapter_chunks> chunks);
}
