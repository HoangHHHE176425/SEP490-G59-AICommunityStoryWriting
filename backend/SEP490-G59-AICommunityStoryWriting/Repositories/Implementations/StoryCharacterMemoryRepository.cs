using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Repositories.Interfaces;

namespace Repositories.Implementations;

public class StoryCharacterMemoryRepository : IStoryCharacterMemoryRepository
{
    public List<story_character_memory> GetByStoryId(Guid storyId)
        => StoryCharacterMemoryDAO.GetByStoryId(storyId);

    public void Upsert(Guid storyId, string characterName, string? stateJson)
        => StoryCharacterMemoryDAO.Upsert(storyId, characterName, stateJson);

    public void DeleteByStoryId(Guid storyId)
        => StoryCharacterMemoryDAO.DeleteByStoryId(storyId);
}
