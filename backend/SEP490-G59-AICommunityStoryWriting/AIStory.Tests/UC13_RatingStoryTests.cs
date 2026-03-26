using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Repositories;
using Services.Implementations;
using Xunit;

namespace AIStory.Tests;

public class UC13_RatingStoryTests
{
    private static StoryService CreateSut(FakeStoryRepository storyRepo)
    {
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new StoryService(
            storyRepo,
            new FakeChapterRepository(),
            NullLogger<StoryService>.Instance,
            cache,
            moderationHubNotifier: null);
    }

    [Fact]
    public void RateStory_EmptyStoryId_Throws()
    {
        var sut = CreateSut(new FakeStoryRepository());
        var ex = Assert.Throws<InvalidOperationException>(() =>
            sut.RateStory(Guid.Empty, Guid.NewGuid(), 5, null));
        Assert.Contains("StoryId", ex.Message);
    }

    [Fact]
    public void RateStory_EmptyUserId_Throws()
    {
        var sut = CreateSut(new FakeStoryRepository());
        var ex = Assert.Throws<InvalidOperationException>(() =>
            sut.RateStory(Guid.NewGuid(), Guid.Empty, 5, null));
        Assert.Contains("UserId", ex.Message);
    }

    [Fact]
    public void RateStory_StarValueTooLow_Throws()
    {
        var storyId = Guid.NewGuid();
        var repo = new FakeStoryRepository();
        repo.Seed(PublishedStory(storyId));
        var sut = CreateSut(repo);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            sut.RateStory(storyId, Guid.NewGuid(), 0, null));
        Assert.Contains("StarValue", ex.Message);
    }

    [Fact]
    public void RateStory_StarValueTooHigh_Throws()
    {
        var storyId = Guid.NewGuid();
        var repo = new FakeStoryRepository();
        repo.Seed(PublishedStory(storyId));
        var sut = CreateSut(repo);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            sut.RateStory(storyId, Guid.NewGuid(), 6, null));
        Assert.Contains("StarValue", ex.Message);
    }

    [Fact]
    public void RateStory_StoryNotFound_Throws()
    {
        var sut = CreateSut(new FakeStoryRepository());
        var ex = Assert.Throws<InvalidOperationException>(() =>
            sut.RateStory(Guid.NewGuid(), Guid.NewGuid(), 5, null));
        Assert.Contains("không tồn tại", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RateStory_StoryNotPublished_Throws()
    {
        var storyId = Guid.NewGuid();
        var repo = new FakeStoryRepository();
        repo.Seed(new stories
        {
            id = storyId,
            title = "Draft S",
            slug = "draft-s",
            status = "DRAFT",
            story_progress_status = "ONGOING",
            age_rating = "ALL"
        });
        var sut = CreateSut(repo);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            sut.RateStory(storyId, Guid.NewGuid(), 5, null));
        Assert.Contains("PUBLISHED", ex.Message);
    }

    [Fact]
    public void RateStory_NoChapterRead_Throws()
    {
        var storyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var repo = new FakeStoryRepository();
        repo.Seed(PublishedStory(storyId));
        var sut = CreateSut(repo);

        var ex = Assert.Throws<InvalidOperationException>(() =>
            sut.RateStory(storyId, userId, 5, "hi"));
        Assert.Contains("chapter", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RateStory_AfterReadChapter_ReturnsAverageAndSecondCallThrowsAlreadyRated()
    {
        var storyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();

        try
        {
            InsertUser(userId);
            InsertPublishedStoryInDb(storyId);

            var repo = new FakeStoryRepository();
            repo.Seed(PublishedStory(storyId));
            var sut = CreateSut(repo);

            UserActivityLogDAO.LogReadChapter(userId, storyId, chapterId);

            var (avg, count) = sut.RateStory(storyId, userId, 5, "great");
            Assert.Equal(5m, avg);
            Assert.Equal(1, count);

            var dup = Assert.Throws<InvalidOperationException>(() =>
                sut.RateStory(storyId, userId, 4, null));
            Assert.Contains("đã đánh giá", dup.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteRatingIfExists(userId, storyId);
            DeleteActivityLogsForUserStory(userId, storyId);
            DeleteStoryIfExists(storyId);
            DeleteUserIfExists(userId);
        }
    }

    private static stories PublishedStory(Guid id)
    {
        var suffix = id.ToString("N")[..12];
        return new stories
        {
            id = id,
            title = "UT Pub",
            slug = "ut-rate-" + suffix,
            status = "PUBLISHED",
            story_progress_status = "ONGOING",
            age_rating = "ALL"
        };
    }

    private static void InsertUser(Guid id)
    {
        using var ctx = new StoryPlatformDbContext();
        ctx.users.Add(new users
        {
            id = id,
            email = $"ut-rate-{id:N}@x.test",
            password_hash = "x",
            status = "ACTIVE",
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        });
        ctx.SaveChanges();
    }

    private static void InsertPublishedStoryInDb(Guid storyId)
    {
        var s = PublishedStory(storyId);
        s.created_at = DateTime.UtcNow;
        s.updated_at = DateTime.UtcNow;
        StoryDAO.Add(s);
    }

    private static void DeleteRatingIfExists(Guid userId, Guid storyId)
    {
        using var ctx = new StoryPlatformDbContext();
        var row = ctx.ratings.FirstOrDefault(r => r.user_id == userId && r.story_id == storyId);
        if (row == null)
            return;
        ctx.ratings.Remove(row);
        ctx.SaveChanges();
    }

    private static void DeleteActivityLogsForUserStory(Guid userId, Guid storyId)
    {
        var raw = storyId.ToString();
        using var ctx = new StoryPlatformDbContext();
        var logs = ctx.user_activity_logs.Where(l => l.user_id == userId && l.raw_data == raw).ToList();
        if (logs.Count == 0)
            return;
        ctx.user_activity_logs.RemoveRange(logs);
        ctx.SaveChanges();
    }

    private static void DeleteStoryIfExists(Guid storyId)
    {
        using var ctx = new StoryPlatformDbContext();
        var row = ctx.stories.FirstOrDefault(s => s.id == storyId);
        if (row == null)
            return;
        ctx.stories.Remove(row);
        ctx.SaveChanges();
    }

    private static void DeleteUserIfExists(Guid userId)
    {
        using var ctx = new StoryPlatformDbContext();
        var row = ctx.users.FirstOrDefault(u => u.id == userId);
        if (row == null)
            return;
        ctx.users.Remove(row);
        ctx.SaveChanges();
    }

    private sealed class FakeStoryRepository : IStoryRepository
    {
        private readonly Dictionary<Guid, stories> _store = new();

        public void Seed(stories s) => _store[s.id] = s;

        public IQueryable<stories> GetAll() => _store.Values.AsQueryable();

        public stories? GetById(Guid id) => _store.TryGetValue(id, out var s) ? s : null;

        public stories? GetBySlug(string slug)
            => _store.Values.FirstOrDefault(s => string.Equals(s.slug, slug, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<Guid> GetStoryIdsByCategoryIds(IReadOnlyCollection<Guid> categoryIds) => Array.Empty<Guid>();

        public void Add(stories story) => _store[story.id] = story;

        public void Add(stories story, IEnumerable<Guid> categoryIds) => _store[story.id] = story;

        public void Update(stories story) => _store[story.id] = story;

        public void Delete(Guid id) => _store.Remove(id);

        public void IncrementViewCount(Guid storyId) { }
    }

    private sealed class FakeChapterRepository : IChapterRepository
    {
        public IQueryable<chapters> GetAll() => Array.Empty<chapters>().AsQueryable();
        public chapters? GetById(Guid id) => null;
        public IEnumerable<chapters> GetByStoryId(Guid storyId) => Array.Empty<chapters>();
        public chapters? GetByStoryIdAndOrderIndex(Guid storyId, int orderIndex) => null;
        public void Add(chapters chapter) { }
        public void Update(chapters chapter) { }
        public void Delete(Guid id) { }
        public void DeleteByStoryId(Guid storyId) { }
    }
}
