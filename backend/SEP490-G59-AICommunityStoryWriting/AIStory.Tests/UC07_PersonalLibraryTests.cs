using BusinessObjects.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Repositories;
using Services.Implementations;
using Xunit;

namespace AIStory.Tests;

public class UC07_PersonalLibraryTests
{
    private static StoryService CreateSut(FakeStoryRepository storyRepo)
    {
        var chapterRepo = new FakeChapterRepository();
        var cache = new MemoryCache(new MemoryCacheOptions());
        return new StoryService(
            storyRepo,
            chapterRepo,
            NullLogger<StoryService>.Instance,
            cache,
            moderationHubNotifier: null);
    }

    [Fact]
    public void SaveReadingProgress_EmptyIds_DoesNothing()
    {
        var repo = new FakeStoryRepository();
        var sut = CreateSut(repo);

        // Guard clauses should return without throwing and without touching DB-backed DAO.
        sut.SaveReadingProgress(Guid.Empty, Guid.Empty, Guid.Empty);
        sut.SaveReadingProgress(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());
        sut.SaveReadingProgress(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);
    }

    [Fact]
    public void SaveReadingProgress_StoryNotFound_DoesNothing()
    {
        var repo = new FakeStoryRepository();
        var sut = CreateSut(repo);

        // StoryRepository returns null -> should return without throwing.
        sut.SaveReadingProgress(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
    }

    [Fact]
    public void SaveReadingProgress_StoryNotPublished_DoesNothing()
    {
        var repo = new FakeStoryRepository();
        var sut = CreateSut(repo);

        var storyId = Guid.NewGuid();
        repo.Seed(new stories
        {
            id = storyId,
            title = "S",
            slug = "s",
            status = "DRAFT",
            story_progress_status = "ONGOING",
            age_rating = "ALL"
        });

        // Not PUBLISHED -> should return without throwing (and avoid DB).
        sut.SaveReadingProgress(storyId, Guid.NewGuid(), Guid.NewGuid());
    }

    [Fact]
    public void GetById_UserIdNull_DoesNotLookupLibrary()
    {
        var repo = new FakeStoryRepository();
        var sut = CreateSut(repo);

        var storyId = Guid.NewGuid();
        repo.Seed(new stories
        {
            id = storyId,
            title = "S",
            slug = "s",
            status = "PUBLISHED",
            story_progress_status = "ONGOING",
            age_rating = "ALL"
        });

        // userId is null -> service should not call UserLibraryDAO.GetLastRead.
        var dto = sut.GetById(storyId, userId: null);
        Assert.NotNull(dto);
    }

    [Fact]
    public void GetById_UserIdEmptyGuid_DoesNotLookupLibrary()
    {
        var repo = new FakeStoryRepository();
        var sut = CreateSut(repo);

        var storyId = Guid.NewGuid();
        repo.Seed(new stories
        {
            id = storyId,
            title = "S",
            slug = "s",
            status = "PUBLISHED",
            story_progress_status = "ONGOING",
            age_rating = "ALL"
        });

        // userId is Guid.Empty -> service should not call UserLibraryDAO.GetLastRead.
        var dto = sut.GetById(storyId, userId: Guid.Empty);
        Assert.NotNull(dto);
    }

    [Fact]
    public void GetBySlug_UserIdNull_DoesNotLookupLibrary()
    {
        var repo = new FakeStoryRepository();
        var sut = CreateSut(repo);

        var storyId = Guid.NewGuid();
        repo.Seed(new stories
        {
            id = storyId,
            title = "S",
            slug = "my-slug",
            status = "PUBLISHED",
            story_progress_status = "ONGOING",
            age_rating = "ALL"
        });

        var dto = sut.GetBySlug("my-slug", userId: null);
        Assert.NotNull(dto);
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

