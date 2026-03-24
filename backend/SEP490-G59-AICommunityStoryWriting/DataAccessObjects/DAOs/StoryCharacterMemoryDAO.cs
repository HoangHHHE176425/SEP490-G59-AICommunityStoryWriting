using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs;

public static class StoryCharacterMemoryDAO
{
    public static List<story_character_memory> GetByStoryId(Guid storyId)
    {
        using var context = new StoryPlatformDbContext();
        return context.story_character_memory
            .AsNoTracking()
            .Where(c => c.story_id == storyId)
            .OrderBy(c => c.character_name)
            .ToList();
    }

    public static void Upsert(Guid storyId, string characterName, string? stateJson)
    {
        using var context = new StoryPlatformDbContext();
        var existing = context.story_character_memory
            .FirstOrDefault(c => c.story_id == storyId && c.character_name == characterName);
        if (existing != null)
        {
            existing.state_json = stateJson;
            existing.updated_at = DateTime.UtcNow;
        }
        else
        {
            context.story_character_memory.Add(new story_character_memory
            {
                id = Guid.NewGuid(),
                story_id = storyId,
                character_name = characterName,
                state_json = stateJson,
                updated_at = DateTime.UtcNow
            });
        }
        context.SaveChanges();
    }

    public static void DeleteByStoryId(Guid storyId)
    {
        using var context = new StoryPlatformDbContext();
        var list = context.story_character_memory.Where(c => c.story_id == storyId).ToList();
        context.story_character_memory.RemoveRange(list);
        context.SaveChanges();
    }
}
