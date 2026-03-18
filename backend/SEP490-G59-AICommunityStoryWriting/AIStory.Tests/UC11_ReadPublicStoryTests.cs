using BusinessObjects.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Repositories;
using Services.Implementations;
using Xunit;

namespace AIStory.Tests;

public class UC11_ReadPublicStoryTests
{
    private static (StoryService sut, FakeStoryRepository repo) CreateSut()
    {
        var repo = new FakeStoryRepository();
        var chapterRepo = new FakeChapterRepository();
        var cache = new MemoryCache(new MemoryCacheOptions());
        var sut = new StoryService(
            repo,
            chapterRepo,
            NullLogger<StoryService>.Instance,
            cache,
            moderationHubNotifier: null);
        return (sut, repo);
    }

    [Fact]
    public void ReadStory_GetById_NotFound_ReturnsNull()
    {
        var (sut, _) = CreateSut();
        var dto = sut.GetById(Guid.NewGuid(), userId: null);
        Assert.Null(dto);
    }

    [Fact]
    public void ReadStory_GetBySlug_NotFound_ReturnsNull()
    {
        var (sut, _) = CreateSut();
        var dto = sut.GetBySlug("missing-slug", userId: null);
        Assert.Null(dto);
    }

    [Fact]
    public void ReadStory_RecordViewIfAllowed_EmptyViewerKey_DoesNothing()
    {
        var (sut, repo) = CreateSut();
        var storyId = Guid.NewGuid();
        repo.Seed(new stories { id = storyId, title = "S", slug = "s", status = "PUBLISHED", story_progress_status = "ONGOING", age_rating = "ALL" });

        sut.RecordViewIfAllowed(storyId, "");
        sut.RecordViewIfAllowed(storyId, "   ");

        Assert.Equal(0, repo.IncrementViewCountCalls);
    }

    [Fact]
    public void ReadStory_RecordViewIfAllowed_StoryNotPublished_DoesNothing()
    {
        var (sut, repo) = CreateSut();
        var storyId = Guid.NewGuid();
        repo.Seed(new stories { id = storyId, title = "S", slug = "s", status = "DRAFT", story_progress_status = "ONGOING", age_rating = "ALL" });

        sut.RecordViewIfAllowed(storyId, "viewer-1");

        Assert.Equal(0, repo.IncrementViewCountCalls);
    }

    [Fact]
    public void ReadStory_RecordViewIfAllowed_FirstTime_IncrementsOnce()
    {
        var (sut, repo) = CreateSut();
        var storyId = Guid.NewGuid();
        repo.Seed(new stories { id = storyId, title = "S", slug = "s", status = "PUBLISHED", story_progress_status = "ONGOING", age_rating = "ALL" });

        sut.RecordViewIfAllowed(storyId, "viewer-1");

        Assert.Equal(1, repo.IncrementViewCountCalls);
    }

    [Fact]
    public void ReadStory_RecordViewIfAllowed_SameViewerWithinCooldown_IncrementsOnlyOnce()
    {
        var (sut, repo) = CreateSut();
        var storyId = Guid.NewGuid();
        repo.Seed(new stories { id = storyId, title = "S", slug = "s", status = "PUBLISHED", story_progress_status = "ONGOING", age_rating = "ALL" });

        sut.RecordViewIfAllowed(storyId, "viewer-1");
        sut.RecordViewIfAllowed(storyId, "viewer-1");

        Assert.Equal(1, repo.IncrementViewCountCalls);
    }

    [Fact]
    public void ReadStory_RecordViewIfAllowed_DifferentViewers_IncrementsPerViewer()
    {
        var (sut, repo) = CreateSut();
        var storyId = Guid.NewGuid();
        repo.Seed(new stories { id = storyId, title = "S", slug = "s", status = "PUBLISHED", story_progress_status = "ONGOING", age_rating = "ALL" });

        sut.RecordViewIfAllowed(storyId, "viewer-1");
        sut.RecordViewIfAllowed(storyId, "viewer-2");

        Assert.Equal(2, repo.IncrementViewCountCalls);
    }

    private sealed class FakeStoryRepository : IStoryRepository
    {
        private readonly Dictionary<Guid, stories> _store = new();
        public int IncrementViewCountCalls { get; private set; }

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
        public void IncrementViewCount(Guid storyId) => IncrementViewCountCalls++;
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

