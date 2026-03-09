using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs;

public static class StoryStoryStateDAO
{
    public static story_story_state? GetByStoryId(Guid storyId)
    {
        using var context = new StoryPlatformDbContext();
        return context.story_story_states
            .AsNoTracking()
            .FirstOrDefault(s => s.story_id == storyId);
    }

    public static void Upsert(Guid storyId, string? stateSnapshotJson)
    {
        using var context = new StoryPlatformDbContext();
        var existing = context.story_story_states.FirstOrDefault(s => s.story_id == storyId);
        if (existing != null)
        {
            existing.state_snapshot_json = stateSnapshotJson;
            existing.updated_at = DateTime.UtcNow;
        }
        else
        {
            context.story_story_states.Add(new story_story_state
            {
                id = Guid.NewGuid(),
                story_id = storyId,
                state_snapshot_json = stateSnapshotJson,
                updated_at = DateTime.UtcNow
            });
        }
        context.SaveChanges();
    }

    public static void DeleteByStoryId(Guid storyId)
    {
        using var context = new StoryPlatformDbContext();
        var list = context.story_story_states.Where(s => s.story_id == storyId).ToList();
        context.story_story_states.RemoveRange(list);
        context.SaveChanges();
    }
}
