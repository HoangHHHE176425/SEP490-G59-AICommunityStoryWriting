using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
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
    public void CreateStory_AuthorNotFound_Throws()
    {
        var repo = new FakeStoryRepository();
        var sut = CreateSut(repo);

        var ex = Assert.Throws<InvalidOperationException>(() => sut.Create(
            new CreateStoryRequestDto
            {
                Title = "T",
                Summary = "S",
                CategoryIds = new List<Guid> { Guid.NewGuid() }
            },
            authorId: Guid.NewGuid(),
            coverImageUrl: null));

        Assert.Contains("không tồn tại", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateStory_NoCategories_Throws()
    {
        var authorId = Guid.NewGuid();
        try
        {
            InsertUser(authorId);
            var repo = new FakeStoryRepository();
            var sut = CreateSut(repo);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.Create(
                new CreateStoryRequestDto
                {
                    Title = "T",
                    Summary = "S",
                    CategoryIds = new List<Guid>()
                },
                authorId,
                coverImageUrl: null));

            Assert.Contains("thể loại", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            DeleteUserIfExists(authorId);
        }
    }

    [Fact]
    public void CreateStory_CategoryNotFound_Throws()
    {
        var authorId = Guid.NewGuid();
        try
        {
            InsertUser(authorId);
            var repo = new FakeStoryRepository();
            var sut = CreateSut(repo);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.Create(
                new CreateStoryRequestDto
                {
                    Title = "T",
                    Summary = "S",
                    CategoryIds = new List<Guid> { Guid.NewGuid() }
                },
                authorId,
                coverImageUrl: null));

            Assert.Contains("Category", ex.Message);
            Assert.Contains("not found", ex.Message);
        }
        finally
        {
            DeleteUserIfExists(authorId);
        }
    }

    [Fact]
    public void CreateStory_CategoryInactive_Throws()
    {
        var authorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        try
        {
            InsertUser(authorId);
            InsertCategory(categoryId, isActive: false);

            var repo = new FakeStoryRepository();
            var sut = CreateSut(repo);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.Create(
                new CreateStoryRequestDto
                {
                    Title = "T",
                    Summary = "S",
                    CategoryIds = new List<Guid> { categoryId }
                },
                authorId,
                coverImageUrl: null));

            Assert.Contains("not active", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            CategoryDAO.Delete(categoryId);
            DeleteUserIfExists(authorId);
        }
    }

    [Fact]
    public void CreateStory_AuthorWritingSuspended_Throws()
    {
        var authorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        try
        {
            InsertUser(authorId);
            InsertCategory(categoryId, isActive: true);
            UserDAO.SetAuthorWritingSuspendedUntil(authorId, DateTime.UtcNow.AddDays(1));

            var repo = new FakeStoryRepository();
            var sut = CreateSut(repo);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.Create(
                new CreateStoryRequestDto
                {
                    Title = "T",
                    Summary = "S",
                    CategoryIds = new List<Guid> { categoryId }
                },
                authorId,
                coverImageUrl: null));

            Assert.Contains("tạm khóa", ex.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            UserDAO.SetAuthorWritingSuspendedUntil(authorId, null);
            CategoryDAO.Delete(categoryId);
            DeleteUserIfExists(authorId);
        }
    }

    [Fact]
    public void CreateStory_SlugAlreadyExists_Throws()
    {
        var authorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        try
        {
            InsertUser(authorId);
            InsertCategory(categoryId, isActive: true);

            var repo = new FakeStoryRepository();
            var existingId = Guid.NewGuid();
            repo.Seed(new stories
            {
                id = existingId,
                title = "Hello World",
                slug = "hello-world",
                status = "DRAFT",
                story_progress_status = "ONGOING",
                age_rating = "ALL"
            });

            var sut = CreateSut(repo);

            var ex = Assert.Throws<InvalidOperationException>(() => sut.Create(
                new CreateStoryRequestDto
                {
                    Title = "Hello World",
                    Summary = "S",
                    CategoryIds = new List<Guid> { categoryId }
                },
                authorId,
                coverImageUrl: null));

            Assert.Contains("already exists", ex.Message);
        }
        finally
        {
            CategoryDAO.Delete(categoryId);
            DeleteUserIfExists(authorId);
        }
    }

    [Fact]
    public void CreateStory_InvalidAgeRating_Throws()
    {
        var authorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        try
        {
            InsertUser(authorId);
            InsertCategory(categoryId, isActive: true);

            var repo = new FakeStoryRepository();
            var sut = CreateSut(repo);

            var ex = Assert.Throws<ArgumentException>(() => sut.Create(
                new CreateStoryRequestDto
                {
                    Title = "Unique Title Xy",
                    Summary = "S",
                    CategoryIds = new List<Guid> { categoryId },
                    AgeRating = "21+"
                },
                authorId,
                coverImageUrl: null));

            Assert.Contains("Invalid age rating", ex.Message);
        }
        finally
        {
            CategoryDAO.Delete(categoryId);
            DeleteUserIfExists(authorId);
        }
    }

    [Fact]
    public void CreateStory_InvalidProgressStatus_Throws()
    {
        var authorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        try
        {
            InsertUser(authorId);
            InsertCategory(categoryId, isActive: true);

            var repo = new FakeStoryRepository();
            var sut = CreateSut(repo);

            var ex = Assert.Throws<ArgumentException>(() => sut.Create(
                new CreateStoryRequestDto
                {
                    Title = "Unique Title Zz",
                    Summary = "S",
                    CategoryIds = new List<Guid> { categoryId },
                    StoryProgressStatus = "INVALID"
                },
                authorId,
                coverImageUrl: null));

            Assert.Contains("Invalid story progress status", ex.Message);
        }
        finally
        {
            CategoryDAO.Delete(categoryId);
            DeleteUserIfExists(authorId);
        }
    }

    [Fact]
    public void CreateStory_Success_PersistsDraftAndSlug()
    {
        var authorId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        try
        {
            InsertUser(authorId);
            InsertCategory(categoryId, isActive: true);

            var repo = new FakeStoryRepository();
            var sut = CreateSut(repo);

            var dto = sut.Create(
                new CreateStoryRequestDto
                {
                    Title = "Hello World",
                    Summary = "Sum",
                    CategoryIds = new List<Guid> { categoryId },
                    StoryProgressStatus = "ongoing",
                    AgeRating = "16+"
                },
                authorId,
                coverImageUrl: "https://example.com/cover.png");

            Assert.Equal("Hello World", dto.Title);
            Assert.Equal("hello-world", dto.Slug);
            Assert.Equal("DRAFT", dto.Status);
            Assert.Equal("ONGOING", dto.StoryProgressStatus);
            Assert.Equal("16+", dto.AgeRating);
            Assert.Equal(authorId, dto.AuthorId);
            Assert.Equal("https://example.com/cover.png", dto.CoverImage);

            var stored = repo.GetById(dto.Id)!;
            Assert.Equal("hello-world", stored.slug);
            Assert.Equal(authorId, stored.author_id);
            Assert.Equal(categoryId, Assert.Single(repo.LastAddedCategoryIds!));
        }
        finally
        {
            CategoryDAO.Delete(categoryId);
            DeleteUserIfExists(authorId);
        }
    }

    private static void InsertUser(Guid id)
    {
        using var ctx = new StoryPlatformDbContext();
        ctx.users.Add(new users
        {
            id = id,
            email = $"ut-story-{id:N}@x.test",
            password_hash = "x",
            status = "ACTIVE",
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        });
        ctx.SaveChanges();
    }

    private static void InsertCategory(Guid id, bool isActive)
    {
        var suffix = id.ToString("N")[..12];
        CategoryDAO.Add(new categories
        {
            id = id,
            name = "UT Cat " + suffix,
            slug = "ut-st-cat-" + suffix,
            is_active = isActive,
            created_at = DateTime.UtcNow
        });
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

        public IReadOnlyList<Guid>? LastAddedCategoryIds { get; private set; }

        public void Seed(stories s) => _store[s.id] = s;

        public IQueryable<stories> GetAll() => _store.Values.AsQueryable();

        public stories? GetById(Guid id) => _store.TryGetValue(id, out var s) ? s : null;

        public stories? GetBySlug(string slug)
            => _store.Values.FirstOrDefault(s => string.Equals(s.slug, slug, StringComparison.OrdinalIgnoreCase));

        public IReadOnlyList<Guid> GetStoryIdsByCategoryIds(IReadOnlyCollection<Guid> categoryIds)
            => Array.Empty<Guid>();

        public void Add(stories story) => _store[story.id] = story;

        public void Add(stories story, IEnumerable<Guid> categoryIds)
        {
            _store[story.id] = story;
            LastAddedCategoryIds = categoryIds.ToList();
        }

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


//dotnet test .\AIStory.Tests.csproj -c Release --filter FullyQualifiedName~UC05_StoryServiceTests