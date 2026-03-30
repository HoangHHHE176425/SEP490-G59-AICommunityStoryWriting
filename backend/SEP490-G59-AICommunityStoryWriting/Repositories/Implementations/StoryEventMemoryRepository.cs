using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public class StoryEventMemoryRepository : IStoryEventMemoryRepository
{
    public List<story_event_memory> GetByStoryId(Guid storyId)
        => StoryEventMemoryDAO.GetByStoryId(storyId);

    public void Add(Guid storyId, Guid? chapterId, int orderIndex, string description)
        => StoryEventMemoryDAO.Add(storyId, chapterId, orderIndex, description);

    public void AddRange(IEnumerable<story_event_memory> events)
        => StoryEventMemoryDAO.AddRange(events);

    public int GetNextOrderIndex(Guid storyId)
        => StoryEventMemoryDAO.GetNextOrderIndex(storyId);

    public void DeleteByStoryId(Guid storyId)
        => StoryEventMemoryDAO.DeleteByStoryId(storyId);

    public void DeleteByChapterId(Guid chapterId)
        => StoryEventMemoryDAO.DeleteByChapterId(chapterId);
}
