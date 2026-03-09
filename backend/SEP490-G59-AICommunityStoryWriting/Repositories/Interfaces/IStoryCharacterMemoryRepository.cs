using BusinessObjects.Entities;

namespace Repositories.Interfaces;

public interface IStoryCharacterMemoryRepository
{
    List<story_character_memory> GetByStoryId(Guid storyId);
    void Upsert(Guid storyId, string characterName, string? stateJson);
    void DeleteByStoryId(Guid storyId);
}
