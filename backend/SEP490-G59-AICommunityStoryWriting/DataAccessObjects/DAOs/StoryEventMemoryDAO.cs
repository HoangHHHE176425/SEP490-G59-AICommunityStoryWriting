using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs;

public static class StoryEventMemoryDAO
{
    public static List<story_event_memory> GetByStoryId(Guid storyId)
    {
        using var context = new StoryPlatformDbContext();
        return context.story_event_memory
            .AsNoTracking()
            .Where(c => c.story_id == storyId)
            .OrderBy(c => c.order_index)
            .ThenBy(c => c.created_at)
            .ToList();
    }

    public static void Add(Guid storyId, Guid? chapterId, int orderIndex, string description)
    {
        using var context = new StoryPlatformDbContext();
        context.story_event_memory.Add(new story_event_memory
        {
            id = Guid.NewGuid(),
            story_id = storyId,
            chapter_id = chapterId,
            order_index = orderIndex,
            description = description,
            created_at = DateTime.UtcNow
        });
        context.SaveChanges();
    }

    public static void AddRange(IEnumerable<story_event_memory> events)
    {
        using var context = new StoryPlatformDbContext();
        context.story_event_memory.AddRange(events);
        context.SaveChanges();
    }

    public static int GetNextOrderIndex(Guid storyId)
    {
        using var context = new StoryPlatformDbContext();
        var max = context.story_event_memory
            .Where(c => c.story_id == storyId)
            .Select(c => c.order_index)
            .DefaultIfEmpty(-1)
            .Max();
        return max + 1;
    }

    public static void DeleteByStoryId(Guid storyId)
    {
        using var context = new StoryPlatformDbContext();
        var list = context.story_event_memory.Where(c => c.story_id == storyId).ToList();
        context.story_event_memory.RemoveRange(list);
        context.SaveChanges();
    }
}
