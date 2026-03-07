using BusinessObjects.Entities;

namespace Repositories.Interfaces;

public interface IStoryStoryStateRepository
{
    story_story_state? GetByStoryId(Guid storyId);
    void Upsert(Guid storyId, string? stateSnapshotJson);
    void DeleteByStoryId(Guid storyId);
}
