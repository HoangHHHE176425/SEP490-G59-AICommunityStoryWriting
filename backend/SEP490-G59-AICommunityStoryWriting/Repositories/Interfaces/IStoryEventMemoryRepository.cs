using BusinessObjects.Entities;

namespace Repositories.Interfaces;

public interface IStoryEventMemoryRepository
{
    List<story_event_memory> GetByStoryId(Guid storyId);
    void Add(Guid storyId, Guid? chapterId, int orderIndex, string description);
    void AddRange(IEnumerable<story_event_memory> events);
    int GetNextOrderIndex(Guid storyId);
    void DeleteByStoryId(Guid storyId);
    void DeleteByChapterId(Guid chapterId);
}
