using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Repositories;
using Services.DTOs.Chapters;
using Services.Implementations;
using Xunit;

namespace AIStory.Tests;

public class UC06_ChapterServiceTests
{
    private static ChapterService CreateSut(
        FakeChapterRepository chapterRepo,
        FakeChapterVersionRepository versionRepo,
        FakeAiGeneratedContentRepository? aiRepo = null)
    {
        aiRepo ??= new FakeAiGeneratedContentRepository();
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

    private static stories NewTestStory(Guid storyId)
    {
        var suffix = storyId.ToString("N")[..16];
        return new stories
        {
            id = storyId,
            author_id = null,
            title = "UT Create Chapter",
            slug = "ut-ch-" + suffix,
            story_progress_status = "ONGOING",
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        };
    }

    private static void InsertStory(stories story) => StoryDAO.Add(story);

    private static void DeleteStoryIfExists(Guid storyId)
    {
        using var context = new StoryPlatformDbContext();
        var row = context.stories.FirstOrDefault(s => s.id == storyId);
        if (row == null)
            return;
        context.stories.Remove(row);
        context.SaveChanges();
    }

    [Fact]
    public void CreateChapter_StoryNotFound_Throws()
    {
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);
        var missingStoryId = Guid.NewGuid();

        var ex = Assert.Throws<InvalidOperationException>(() => sut.Create(new CreateChapterRequestDto
        {
            Id = Guid.NewGuid(),
            StoryId = missingStoryId,
            Title = "Ch1",
            OrderIndex = 1
        }));

        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CreateChapter_DuplicateOrderIndex_Throws()
    {
        var storyId = Guid.NewGuid();
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);

        try
        {
            InsertStory(NewTestStory(storyId));
            repo.Seed(new chapters
            {
                id = Guid.NewGuid(),
                story_id = storyId,
                title = "Existing",
                order_index = 1,
                status = "DRAFT",
                access_type = "FREE",
                coin_price = 0
            });

            var ex = Assert.Throws<InvalidOperationException>(() => sut.Create(new CreateChapterRequestDto
            {
                Id = Guid.NewGuid(),
                StoryId = storyId,
                Title = "New",
                OrderIndex = 1
            }));
            Assert.Contains("already exists", ex.Message);
        }
        finally
        {
            DeleteStoryIfExists(storyId);
        }
    }

    [Fact]
    public void CreateChapter_InvalidAccessType_Throws()
    {
        var storyId = Guid.NewGuid();
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);

        try
        {
            InsertStory(NewTestStory(storyId));

            var ex = Assert.Throws<ArgumentException>(() => sut.Create(new CreateChapterRequestDto
            {
                Id = Guid.NewGuid(),
                StoryId = storyId,
                Title = "Ch1",
                OrderIndex = 1,
                AccessType = "VIP"
            }));
            Assert.Contains("Invalid access type", ex.Message);
        }
        finally
        {
            DeleteStoryIfExists(storyId);
        }
    }

    [Fact]
    public void CreateChapter_PaidWithNonPositiveCoin_Throws()
    {
        var storyId = Guid.NewGuid();
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);

        try
        {
            InsertStory(NewTestStory(storyId));

            var ex = Assert.Throws<ArgumentException>(() => sut.Create(new CreateChapterRequestDto
            {
                Id = Guid.NewGuid(),
                StoryId = storyId,
                Title = "Ch1",
                OrderIndex = 1,
                AccessType = "PAID",
                CoinPrice = 0
            }));
            Assert.Contains("Coin price must be greater than 0", ex.Message);
        }
        finally
        {
            DeleteStoryIfExists(storyId);
        }
    }

    [Fact]
    public void CreateChapter_Free_ForcesCoinPriceZero()
    {
        var storyId = Guid.NewGuid();
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);

        try
        {
            InsertStory(NewTestStory(storyId));

            var id = Guid.NewGuid();
            var dto = sut.Create(new CreateChapterRequestDto
            {
                Id = id,
                StoryId = storyId,
                Title = "Ch1",
                OrderIndex = 1,
                AccessType = "FREE",
                CoinPrice = 999
            });

            var stored = repo.GetById(dto.Id)!;
            Assert.Equal("FREE", stored.access_type);
            Assert.Equal(0, stored.coin_price);
            Assert.Equal(0, dto.CoinPrice);
            Assert.Equal(id, dto.Id);
        }
        finally
        {
            DeleteStoryIfExists(storyId);
        }
    }

    [Fact]
    public void CreateChapter_InvalidStatus_DefaultsToDraft()
    {
        var storyId = Guid.NewGuid();
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);

        try
        {
            InsertStory(NewTestStory(storyId));

            var id = Guid.NewGuid();
            var dto = sut.Create(new CreateChapterRequestDto
            {
                Id = id,
                StoryId = storyId,
                Title = "Ch1",
                OrderIndex = 1,
                Status = "NOT_A_STATUS"
            });

            Assert.Equal("DRAFT", dto.Status);
            Assert.Null(dto.PublishedAt);
            Assert.Equal(id, dto.Id);
        }
        finally
        {
            DeleteStoryIfExists(storyId);
        }
    }

    [Fact]
    public void CreateChapter_StatusPublished_SetsPublishedAt()
    {
        var storyId = Guid.NewGuid();
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);

        try
        {
            InsertStory(NewTestStory(storyId));

            var id = Guid.NewGuid();
            var dto = sut.Create(new CreateChapterRequestDto
            {
                Id = id,
                StoryId = storyId,
                Title = "Ch1",
                OrderIndex = 1,
                Status = "PUBLISHED"
            });

            Assert.Equal("PUBLISHED", dto.Status);
            Assert.NotNull(dto.PublishedAt);
            var stored = repo.GetById(dto.Id)!;
            Assert.Equal("PUBLISHED", stored.status);
            Assert.NotNull(stored.published_at);
            Assert.Equal(id, dto.Id);
        }
        finally
        {
            DeleteStoryIfExists(storyId);
        }
    }

    [Fact]
    public void CreateChapter_Content_SetsWordCount()
    {
        var storyId = Guid.NewGuid();
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var sut = CreateSut(repo, ver);

        try
        {
            InsertStory(NewTestStory(storyId));

            var id = Guid.NewGuid();
            var dto = sut.Create(new CreateChapterRequestDto
            {
                Id = id,
                StoryId = storyId,
                Title = "Ch1",
                OrderIndex = 1,
                Content = "hello world"
            });

            Assert.True(dto.WordCount >= 2);
            Assert.True(repo.GetById(dto.Id)!.word_count >= 2);
            Assert.Equal(id, dto.Id);
        }
        finally
        {
            DeleteStoryIfExists(storyId);
        }
    }

    [Fact]
    public void CreateChapter_AiGeneratedContentId_UsesAiOutputAndLinksDraft()
    {
        var storyId = Guid.NewGuid();
        var aiId = Guid.NewGuid();
        var repo = new FakeChapterRepository();
        var ver = new FakeChapterVersionRepository();
        var aiRepo = new FakeAiGeneratedContentRepository();
        var sut = CreateSut(repo, ver, aiRepo);

        try
        {
            InsertStory(NewTestStory(storyId));
            aiRepo.Seed(new ai_generated_content
            {
                id = aiId,
                story_id = storyId,
                ai_output = "draft from ai",
                created_at = DateTime.UtcNow
            });

            var id = Guid.NewGuid();
            var dto = sut.Create(new CreateChapterRequestDto
            {
                Id = id,
                StoryId = storyId,
                Title = "From AI",
                OrderIndex = 2,
                AiGeneratedContentId = aiId,
                Content = null
            });

            Assert.Equal("draft from ai", repo.GetById(dto.Id)!.content);
            Assert.Equal(aiId, aiRepo.LastUpdateChapterId);
            Assert.Equal(id, dto.Id);
        }
        finally
        {
            DeleteStoryIfExists(storyId);
        }
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
        private readonly List<chapter_versions> _rows = new();

        public void Seed(chapter_versions v) => _rows.Add(v);

        public IEnumerable<chapter_versions> GetByChapterId(Guid chapterId)
            => _rows.Where(v => v.chapter_id == chapterId).ToList();

        public chapter_versions? GetById(Guid id) => _rows.FirstOrDefault(v => v.id == id);
        public void Add(chapter_versions version) => _rows.Add(version);
        public void Update(chapter_versions version)
        {
            var i = _rows.FindIndex(v => v.id == version.id);
            if (i >= 0) _rows[i] = version;
        }

        public void Delete(Guid id) => _rows.RemoveAll(v => v.id == id);

        public void DeleteAllByChapterId(Guid chapterId) => _rows.RemoveAll(v => v.chapter_id == chapterId);
    }

    private sealed class FakeAiGeneratedContentRepository : IAiGeneratedContentRepository
    {
        private readonly Dictionary<Guid, ai_generated_content> _byId = new();

        public Guid? LastUpdateChapterId { get; private set; }

        public void Seed(ai_generated_content entity) => _byId[entity.id] = entity;

        public ai_generated_content? GetLatestByChapterId(Guid chapterId) => null;

        public IReadOnlyList<ai_generated_content> GetAllByChapterId(Guid chapterId) => Array.Empty<ai_generated_content>();

        public IReadOnlyList<ai_generated_content> GetAllByDraftChapterId(Guid draftChapterId) => Array.Empty<ai_generated_content>();

        public IReadOnlyList<ai_generated_content> GetAllByStoryIdAndChapterIndex(Guid storyId, int chapterIndex, int maxCount = 50)
            => Array.Empty<ai_generated_content>();

        public ai_generated_content? GetById(Guid id) => _byId.TryGetValue(id, out var e) ? e : null;

        public void Add(ai_generated_content entity) => _byId[entity.id] = entity;

        public void UpdateChapterId(Guid id, Guid chapterId, int chapterOrderIndex) => LastUpdateChapterId = id;

        public void BindDraftChapterId(Guid draftChapterId, Guid chapterId, int chapterOrderIndex) { }

        public void DeleteAllByChapterId(Guid chapterId) { }
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
