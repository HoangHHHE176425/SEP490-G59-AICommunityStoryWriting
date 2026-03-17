using BusinessObjects.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Repositories;
using Services.DTOs.Chapters;
using Services.Implementations;
using Xunit;

namespace AIStory.Tests;

public class UC06_ChapterServiceTests
{
    private static ChapterService CreateSut(FakeChapterRepository chapterRepo, FakeChapterVersionRepository versionRepo)
    {
        var aiRepo = new FakeAiGeneratedContentRepository();
        var scopeFactory = new FakeServiceScopeFactory();
        return new ChapterService(
            chapterRepo,
            versionRepo,
            aiRepo,
            scopeFactory,
            NullLogger<ChapterService>.Instance,
            moderationHubNotifier: null,
            notificationHubNotifier: null);
    }

    [Fact]
    public void EditChapter_InvalidStatus_Throws()
    {
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);

        var id = Guid.NewGuid();
        repo.Seed(new chapters { id = id, story_id = Guid.NewGuid(), title = "Old", order_index = 1, status = "DRAFT", access_type = "FREE", coin_price = 0 });

        var ex = Assert.Throws<ArgumentException>(() => sut.Update(id, new UpdateChapterRequestDto
        {
            Title = "New",
            Status = "NOT_A_STATUS"
        }));
        Assert.Contains("Invalid status", ex.Message);
    }

    [Fact]
    public void EditChapter_InvalidAccessType_Throws()
    {
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);

        var id = Guid.NewGuid();
        repo.Seed(new chapters { id = id, story_id = Guid.NewGuid(), title = "Old", order_index = 1, status = "DRAFT", access_type = "FREE", coin_price = 0 });

        var ex = Assert.Throws<ArgumentException>(() => sut.Update(id, new UpdateChapterRequestDto
        {
            Title = "New",
            AccessType = "VIP"
        }));
        Assert.Contains("Invalid access type", ex.Message);
    }

    [Fact]
    public void EditChapter_SetPaidWithNonPositiveCoin_Throws()
    {
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);

        var id = Guid.NewGuid();
        repo.Seed(new chapters { id = id, story_id = Guid.NewGuid(), title = "Old", order_index = 1, status = "DRAFT", access_type = "FREE", coin_price = 0 });

        var ex = Assert.Throws<ArgumentException>(() => sut.Update(id, new UpdateChapterRequestDto
        {
            Title = "New",
            AccessType = "PAID",
            CoinPrice = 0
        }));
        Assert.Contains("Coin price must be greater than 0", ex.Message);
    }

    [Fact]
    public void EditChapter_SetFree_ForcesCoinPriceZero()
    {
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);

        var id = Guid.NewGuid();
        repo.Seed(new chapters { id = id, story_id = Guid.NewGuid(), title = "Old", order_index = 1, status = "DRAFT", access_type = "PAID", coin_price = 10 });

        var ok = sut.Update(id, new UpdateChapterRequestDto
        {
            Title = "New",
            AccessType = "FREE",
            CoinPrice = 999
        });

        Assert.True(ok);
        var updated = repo.GetById(id)!;
        Assert.Equal("FREE", updated.access_type);
        Assert.Equal(0, updated.coin_price);
    }

    [Fact]
    public void EditChapter_UpdateCoinPriceOnFree_Throws()
    {
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);

        var id = Guid.NewGuid();
        repo.Seed(new chapters { id = id, story_id = Guid.NewGuid(), title = "Old", order_index = 1, status = "DRAFT", access_type = "FREE", coin_price = 0 });

        var ex = Assert.Throws<ArgumentException>(() => sut.Update(id, new UpdateChapterRequestDto
        {
            Title = "New",
            CoinPrice = 5
        }));
        Assert.Contains("Cannot set coin price for FREE", ex.Message);
    }

    [Fact]
    public void EditChapter_StatusToPublished_SetsPublishedAt()
    {
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);

        var id = Guid.NewGuid();
        repo.Seed(new chapters { id = id, story_id = null, title = "Old", order_index = 1, status = "DRAFT", access_type = "FREE", coin_price = 0, published_at = null });

        var ok = sut.Update(id, new UpdateChapterRequestDto
        {
            Title = "New",
            Status = "PUBLISHED"
        });

        Assert.True(ok);
        var updated = repo.GetById(id)!;
        Assert.Equal("PUBLISHED", updated.status);
        Assert.NotNull(updated.published_at);
    }

    [Fact]
    public void EditChapter_StatusFromPublishedToDraft_ClearsPublishedAt()
    {
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);

        var id = Guid.NewGuid();
        repo.Seed(new chapters
        {
            id = id,
            story_id = null,
            title = "Old",
            order_index = 1,
            status = "PUBLISHED",
            access_type = "FREE",
            coin_price = 0,
            published_at = DateTime.UtcNow.AddDays(-1)
        });

        var ok = sut.Update(id, new UpdateChapterRequestDto
        {
            Title = "New",
            Status = "DRAFT"
        });

        Assert.True(ok);
        var updated = repo.GetById(id)!;
        Assert.Equal("DRAFT", updated.status);
        Assert.Null(updated.published_at);
    }

    [Fact]
    public void EditChapter_ContentProvided_RecomputesWordCount()
    {
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);

        var id = Guid.NewGuid();
        repo.Seed(new chapters { id = id, story_id = null, title = "Old", order_index = 1, status = "DRAFT", access_type = "FREE", coin_price = 0, word_count = 0 });

        var ok = sut.Update(id, new UpdateChapterRequestDto
        {
            Title = "New",
            Content = "hello world"
        });

        Assert.True(ok);
        var updated = repo.GetById(id)!;
        Assert.True(updated.word_count >= 2);
    }

    [Fact]
    public void EditChapter_OrderIndexDuplicate_Throws()
    {
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);

        var storyId = Guid.NewGuid();
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        repo.Seed(new chapters { id = id1, story_id = storyId, title = "C1", order_index = 1, status = "DRAFT", access_type = "FREE", coin_price = 0 });
        repo.Seed(new chapters { id = id2, story_id = storyId, title = "C2", order_index = 2, status = "DRAFT", access_type = "FREE", coin_price = 0 });

        var ex = Assert.Throws<InvalidOperationException>(() => sut.Update(id2, new UpdateChapterRequestDto
        {
            Title = "C2 updated",
            OrderIndex = 1
        }));
        Assert.Contains("already exists", ex.Message);
    }

    private sealed class FakeChapterRepository : IChapterRepository
    {
        private readonly Dictionary<Guid, chapters> _store = new();

        public void Seed(chapters c) => _store[c.id] = c;

        public IQueryable<chapters> GetAll() => _store.Values.AsQueryable();

        public chapters? GetById(Guid id) => _store.TryGetValue(id, out var c) ? c : null;

        public IEnumerable<chapters> GetByStoryId(Guid storyId) => _store.Values.Where(c => c.story_id == storyId);

        public chapters? GetByStoryIdAndOrderIndex(Guid storyId, int orderIndex)
            => _store.Values.FirstOrDefault(c => c.story_id == storyId && c.order_index == orderIndex);

        public void Add(chapters chapter) => _store[chapter.id] = chapter;

        public void Update(chapters chapter) => _store[chapter.id] = chapter;

        public void Delete(Guid id) => _store.Remove(id);

        public void DeleteByStoryId(Guid storyId)
        {
            foreach (var id in _store.Values.Where(c => c.story_id == storyId).Select(c => c.id).ToList())
                _store.Remove(id);
        }
    }

    private sealed class FakeChapterVersionRepository : IChapterVersionRepository
    {
        public IEnumerable<chapter_versions> GetByChapterId(Guid chapterId) => Array.Empty<chapter_versions>();
        public chapter_versions? GetById(Guid id) => null;
        public void Add(chapter_versions version) { }
        public void Update(chapter_versions version) { }
        public void Delete(Guid id) { }
    }

    private sealed class FakeAiGeneratedContentRepository : IAiGeneratedContentRepository
    {
        public ai_generated_content? GetLatestByChapterId(Guid chapterId) => null;
        public IReadOnlyList<ai_generated_content> GetAllByChapterId(Guid chapterId) => Array.Empty<ai_generated_content>();
        public ai_generated_content? GetById(Guid id) => null;
        public void Add(ai_generated_content entity) { }
        public void UpdateChapterId(Guid id, Guid chapterId) { }
    }

    private sealed class FakeServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeServiceScope();

        private sealed class FakeServiceScope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new FakeServiceProvider();
            public void Dispose() { }
        }

        private sealed class FakeServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }
    }
}

