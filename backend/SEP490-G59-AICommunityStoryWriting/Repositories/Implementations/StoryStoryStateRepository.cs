using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public class StoryStoryStateRepository : IStoryStoryStateRepository
{
    public story_story_state? GetByStoryId(Guid storyId)
        => StoryStoryStateDAO.GetByStoryId(storyId);

    public void Upsert(Guid storyId, string? stateSnapshotJson)
        => StoryStoryStateDAO.Upsert(storyId, stateSnapshotJson);

    public void DeleteByStoryId(Guid storyId)
        => StoryStoryStateDAO.DeleteByStoryId(storyId);
}
