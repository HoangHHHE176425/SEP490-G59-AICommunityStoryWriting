using BusinessObjects.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Repositories;
using Services.DTOs.Stories;
using Services.Implementations;
using Xunit;

namespace AIStory.Tests;

public class UC05_StoryServiceTests
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
    public void EditStory_NotFound_ReturnsFalse()
    {
        var repo = new FakeStoryRepository();
        var sut = CreateSut(repo);

        var ok = sut.Update(Guid.NewGuid(), new UpdateStoryRequestDto
        {
            Title = "New title",
            Summary = "New summary",
            CategoryIds = new List<Guid>()
        });

        Assert.False(ok);
    }

    [Fact]
    public void EditStory_InvalidStatus_Throws()
    {
        var repo = new FakeStoryRepository();
        var sut = CreateSut(repo);

        var id = Guid.NewGuid();
        repo.Seed(new stories
        {
            id = id,
            title = "Old",
            slug = "old",
            status = "DRAFT",
            story_progress_status = "ONGOING",
            age_rating = "ALL"
        });

        var ex = Assert.Throws<ArgumentException>(() => sut.Update(id, new UpdateStoryRequestDto
        {
            Title = "Old",
            Summary = "s",
            Status = "NOT_A_STATUS",
            CategoryIds = new List<Guid>()
        }));
        Assert.Contains("Invalid status", ex.Message);
    }

    [Fact]
    public void EditStory_InvalidAgeRating_Throws()
    {
        var repo = new FakeStoryRepository();
        var sut = CreateSut(repo);

        var id = Guid.NewGuid();
        repo.Seed(new stories
        {
            id = id,
            title = "Old",
            slug = "old",
            status = "DRAFT",
            story_progress_status = "ONGOING",
            age_rating = "ALL"
        });

        var ex = Assert.Throws<ArgumentException>(() => sut.Update(id, new UpdateStoryRequestDto
        {
            Title = "Old",
            Summary = "s",
            AgeRating = "21+",
            CategoryIds = new List<Guid>()
        }));
        Assert.Contains("Invalid age rating", ex.Message);
    }

    [Fact]
    public void EditStory_InvalidProgressStatus_Throws()
    {
        var repo = new FakeStoryRepository();
        var sut = CreateSut(repo);

        var id = Guid.NewGuid();
        repo.Seed(new stories
        {
            id = id,
            title = "Old",
            slug = "old",
            status = "DRAFT",
            story_progress_status = "ONGOING",
            age_rating = "ALL"
        });

        var ex = Assert.Throws<ArgumentException>(() => sut.Update(id, new UpdateStoryRequestDto
        {
            Title = "Old",
            Summary = "s",
            StoryProgressStatus = "INVALID",
            CategoryIds = new List<Guid>()
        }));
        Assert.Contains("Invalid story progress status", ex.Message);
    }

    [Fact]
    public void EditStory_TitleChange_SlugDuplicate_Throws()
    {
        var repo = new FakeStoryRepository();
        var sut = CreateSut(repo);

        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();

        repo.Seed(new stories
        {
            id = id1,
            title = "Hello World",
            slug = "hello-world",
            status = "DRAFT",
            story_progress_status = "ONGOING",
            age_rating = "ALL"
        });
        repo.Seed(new stories
        {
            id = id2,
            title = "Other",
            slug = "other",
            status = "DRAFT",
            story_progress_status = "ONGOING",
            age_rating = "ALL"
        });

        var ex = Assert.Throws<InvalidOperationException>(() => sut.Update(id2, new UpdateStoryRequestDto
        {
            Title = "Hello World",
            Summary = "s",
            CategoryIds = new List<Guid>()
        }));
        Assert.Contains("already exists", ex.Message);
    }

    [Fact]
    public void EditStory_ValidStatus_Uppercases()
    {
        var repo = new FakeStoryRepository();
        var sut = CreateSut(repo);

        var id = Guid.NewGuid();
        repo.Seed(new stories
        {
            id = id,
            title = "Old",
            slug = "old",
            status = "DRAFT",
            story_progress_status = "ONGOING",
            age_rating = "ALL"
        });

        var ok = sut.Update(id, new UpdateStoryRequestDto
        {
            Title = "Old",
            Summary = "s",
            Status = "published",
            CategoryIds = new List<Guid>()
        });

        Assert.True(ok);
        var updated = repo.GetById(id)!;
        Assert.Equal("PUBLISHED", updated.status);
        Assert.NotNull(updated.updated_at);
    }

    [Fact]
    public void EditStory_TitleChange_UpdatesSlug()
    {
        var repo = new FakeStoryRepository();
        var sut = CreateSut(repo);

        var id = Guid.NewGuid();
        repo.Seed(new stories
        {
            id = id,
            title = "Old",
            slug = "old",
            status = "DRAFT",
            story_progress_status = "ONGOING",
            age_rating = "ALL"
        });

        var ok = sut.Update(id, new UpdateStoryRequestDto
        {
            Title = "Hello World",
            Summary = "s",
            CategoryIds = new List<Guid>()
        });

        Assert.True(ok);
        var updated = repo.GetById(id)!;
        Assert.Equal("hello-world", updated.slug);
        Assert.Equal("Hello World", updated.title);
    }

    private sealed class FakeStoryRepository : IStoryRepository
    {
        private readonly Dictionary<Guid, stories> _store = new();

        public void Seed(stories s) => _store[s.id] = s;

        public IQueryable<stories> GetAll() => _store.Values.AsQueryable();

        public stories? GetById(Guid id) => _store.TryGetValue(id, out var s) ? s : null;

        public stories? GetBySlug(string slug)
            => _store.Values.FirstOrDefault(s => string.Equals(s.slug, slug, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<Guid> GetStoryIdsByCategoryIds(IReadOnlyCollection<Guid> categoryIds)
            => Array.Empty<Guid>();

        public void Add(stories story) => _store[story.id] = story;

        public void Add(stories story, IEnumerable<Guid> categoryIds) => _store[story.id] = story;

        public void Update(stories story) => _store[story.id] = story;

        public void Delete(Guid id) => _store.Remove(id);

        public void IncrementViewCount(Guid storyId)
        {
            // not needed for these tests
        }
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

//dotnet test .\AIStory.Tests.csproj -c Release --filter FullyQualifiedName~UC11_ReadPublicStoryTests