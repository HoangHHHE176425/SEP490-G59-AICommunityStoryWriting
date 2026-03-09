using BusinessObjects;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace DataAccessObjects.DAOs;

public static class StoryChapterChunkDAO
{
    public static List<story_chapter_chunks> GetByStoryId(Guid storyId)
    {
        using var context = new StoryPlatformDbContext();
        return context.story_chapter_chunks
            .AsNoTracking()
            .Where(c => c.story_id == storyId)
            .OrderBy(c => c.chapter_id)
            .ThenBy(c => c.chunk_index)
            .ToList();
    }

    public static List<story_chapter_chunks> GetByStoryIdWithEmbeddings(Guid storyId)
    {
        using var context = new StoryPlatformDbContext();
        return context.story_chapter_chunks
            .AsNoTracking()
            .Where(c => c.story_id == storyId && c.embedding_json != null)
            .OrderBy(c => c.chapter_id)
            .ThenBy(c => c.chunk_index)
            .ToList();
    }

    public static List<story_chapter_chunks> GetChunksByIds(IEnumerable<Guid> ids)
    {
        var idList = ids.Distinct().ToList();
        if (idList.Count == 0) return new List<story_chapter_chunks>();
        using var context = new StoryPlatformDbContext();
        return context.story_chapter_chunks
            .AsNoTracking()
            .Where(c => idList.Contains(c.id))
            .OrderBy(c => c.chapter_id)
            .ThenBy(c => c.chunk_index)
            .ToList();
    }

    public static int CountByStoryId(Guid storyId)
    {
        using var context = new StoryPlatformDbContext();
        return context.story_chapter_chunks.Count(c => c.story_id == storyId);
    }

    public static void DeleteByStoryId(Guid storyId)
    {
        using var context = new StoryPlatformDbContext();
        var list = context.story_chapter_chunks.Where(c => c.story_id == storyId).ToList();
        context.story_chapter_chunks.RemoveRange(list);
        context.SaveChanges();
    }

    public static void DeleteByChapterId(Guid chapterId)
    {
        using var context = new StoryPlatformDbContext();
        var list = context.story_chapter_chunks.Where(c => c.chapter_id == chapterId).ToList();
        context.story_chapter_chunks.RemoveRange(list);
        context.SaveChanges();
    }

    public static void AddRange(IEnumerable<story_chapter_chunks> chunks)
    {
        using var context = new StoryPlatformDbContext();
        context.story_chapter_chunks.AddRange(chunks);
        context.SaveChanges();
    }
}
