using BusinessObjects.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Repositories;
using Services.DTOs.Chapters;
using Services.Implementations;
using Services.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT_CreateChapter
    {
        private readonly ITestOutputHelper _output;

        public UT_CreateChapter(ITestOutputHelper output) => _output = output;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        private void LogTestCase(string utcId, string spec, object? input, object? output, Exception? ex = null)
        {
            _output.WriteLine("");
            _output.WriteLine($"========== {utcId} ==========");
            _output.WriteLine($"SPEC   : {spec}");
            _output.WriteLine($"INPUT  : {JsonSerializer.Serialize(input, _jsonOptions)}");

            if (ex != null)
            {
                _output.WriteLine($"Exception type: {ex.GetType().Name}");
                _output.WriteLine($"Message: {ex.Message}");
            }
            else
            {
                _output.WriteLine($"RESULT : {JsonSerializer.Serialize(output, _jsonOptions)}");
            }
        }

        private static ChapterService CreateSut(
            stories story,
            List<chapters> chapterStore,
            out Mock<IChapterRepository> chapterRepoMock,
            out Mock<IStoryLookup> storyLookupMock,
            out Mock<IUserLookup> userLookupMock,
            out Mock<IChapterVersionRepository> versionRepoMock,
            out Mock<IAiGeneratedContentRepository> aiRepoMock)
        {
            chapterRepoMock = new Mock<IChapterRepository>(MockBehavior.Strict);
            storyLookupMock = new Mock<IStoryLookup>(MockBehavior.Strict);
            userLookupMock = new Mock<IUserLookup>(MockBehavior.Strict);
            versionRepoMock = new Mock<IChapterVersionRepository>(MockBehavior.Strict);
            aiRepoMock = new Mock<IAiGeneratedContentRepository>(MockBehavior.Strict);

            storyLookupMock.Setup(x => x.GetById(It.IsAny<Guid>()))
                .Returns((Guid id) => id == story.id ? story : null);
            storyLookupMock.Setup(x => x.Update(It.IsAny<stories>()));

            userLookupMock.Setup(x => x.IsAuthorWritingSuspended(It.IsAny<Guid>())).Returns(false);
            userLookupMock.Setup(x => x.Exists(It.IsAny<Guid>())).Returns(true);

            versionRepoMock.Setup(x => x.GetByChapterId(It.IsAny<Guid>())).Returns(Array.Empty<chapter_versions>());

            aiRepoMock.Setup(x => x.GetById(It.IsAny<Guid>())).Returns((ai_generated_content?)null);
            aiRepoMock.Setup(x => x.BindDraftChapterId(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()));
            aiRepoMock.Setup(x => x.UpdateChapterId(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<int>()));

            chapterRepoMock.Setup(x => x.GetAll()).Returns(() => chapterStore.AsQueryable());
            chapterRepoMock.Setup(x => x.GetById(It.IsAny<Guid>())).Returns((Guid id) => chapterStore.FirstOrDefault(c => c.id == id));
            chapterRepoMock.Setup(x => x.GetByStoryId(It.IsAny<Guid>())).Returns((Guid sid) => chapterStore.Where(c => c.story_id == sid).ToList());
            chapterRepoMock.Setup(x => x.GetPublishedByStoryId(It.IsAny<Guid>()))
                .Returns((Guid sid) => chapterStore.Where(c => c.story_id == sid && string.Equals(c.status, "PUBLISHED", StringComparison.OrdinalIgnoreCase)).ToList());
            chapterRepoMock.Setup(x => x.GetByStoryIdAndOrderIndex(It.IsAny<Guid>(), It.IsAny<int>()))
                .Returns((Guid sid, int idx) => chapterStore.FirstOrDefault(c => c.story_id == sid && c.order_index == idx));
            chapterRepoMock.Setup(x => x.Add(It.IsAny<chapters>()))
                .Callback((chapters c) => chapterStore.Add(c));
            chapterRepoMock.Setup(x => x.Update(It.IsAny<chapters>()));
            chapterRepoMock.Setup(x => x.Delete(It.IsAny<Guid>()));
            chapterRepoMock.Setup(x => x.DeleteByStoryId(It.IsAny<Guid>()));

            return new ChapterService(
                chapterRepoMock.Object,
                versionRepoMock.Object,
                aiRepoMock.Object,
                userLookupMock.Object,
                storyLookupMock.Object,
                NullLogger<ChapterService>.Instance);
        }

        private static CreateChapterRequestDto BuildRequest(Guid storyId, int orderIndex = 1) => new()
        {
            Id = Guid.NewGuid(),
            StoryId = storyId,
            Title = $"Chapter {orderIndex}",
            Content = new string('a', 600),
            OrderIndex = orderIndex,
            AccessType = "FREE",
            CoinPrice = 0,
            Status = "DRAFT"
        };

        private static stories BuildStory(Guid authorId, string progress = "ONGOING", int views = 1000) => new()
        {
            id = Guid.NewGuid(),
            title = "Story A",
            author_id = authorId,
            story_progress_status = progress,
            total_views = views,
            compliance_hidden = false
        };

        [Fact]
        public void UTCID01_Create_Success_WhenFreeChapterInputValid()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var story = BuildStory(authorId);
            var store = new List<chapters>();
            var sut = CreateSut(story, store, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);

            // Act
            var dto = sut.Create(req, authorId);
            LogTestCase("UTCID01", "FREE chapter hợp lệ tạo thành công.", req, dto);

            // Assert
            Assert.NotNull(dto);
            Assert.Equal(req.Id, dto.Id);
            Assert.Equal("FREE", dto.AccessType);
            Assert.Equal(0, dto.CoinPrice);
            Assert.Single(store);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Once);
        }

        [Fact]
        public void UTCID02_Create_Success_WhenPaidChapterValidAndViewsEnough()
        {
            // Arrange
            var authorId = Guid.NewGuid();
            var story = BuildStory(authorId, views: 600);
            var store = new List<chapters>();
            var sut = CreateSut(story, store, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.AccessType = "PAID";
            req.CoinPrice = 25;

            // Act
            var dto = sut.Create(req, authorId);
            LogTestCase("UTCID02", "PAID chapter hợp lệ (views >= 500) tạo thành công.", req, dto);

            // Assert
            Assert.NotNull(dto);
            Assert.Equal("PAID", dto.AccessType);
            Assert.Equal(25, dto.CoinPrice);
            Assert.Single(store);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Once);
        }

        [Fact]
        public void UTCID03_Create_Fail_WhenAuthorIdEmpty()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>();
            var sut = CreateSut(story, store, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);

            // Act
            var ex = Record.Exception(() => sut.Create(req, Guid.Empty));
            LogTestCase("UTCID03", "AuthorId rỗng phải fail.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID04_Create_Fail_WhenStoryNotFound()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>();
            var sut = CreateSut(story, store, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(Guid.NewGuid(), 1);

            // Act
            var ex = Record.Exception(() => sut.Create(req, ownerId));
            LogTestCase("UTCID04", "Story không tồn tại phải fail.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID05_Create_Fail_WhenCallerNotStoryOwner()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>();
            var sut = CreateSut(story, store, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            var otherAuthor = Guid.NewGuid();

            // Act
            var ex = Record.Exception(() => sut.Create(req, otherAuthor));
            LogTestCase("UTCID05", "User không phải owner truyện phải fail.", new { req, otherAuthor }, null, ex);

            // Assert
            Assert.NotNull(ex);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID06_Create_Fail_WhenAuthorWritingSuspended()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>();
            var sut = CreateSut(story, store, out var chapterRepoMock, out _, out var userLookupMock, out _, out _);
            userLookupMock.Setup(x => x.IsAuthorWritingSuspended(ownerId)).Returns(true);
            var req = BuildRequest(story.id, 1);

            // Act
            var ex = Record.Exception(() => sut.Create(req, ownerId));
            LogTestCase("UTCID06", "Tác giả đang bị suspend viết phải fail.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID07_Create_Fail_WhenStoryProgressHiatus()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId, progress: "HIATUS");
            var store = new List<chapters>();
            var sut = CreateSut(story, store, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);

            // Act
            var ex = Record.Exception(() => sut.Create(req, ownerId));
            LogTestCase("UTCID07", "Story trạng thái HIATUS không được tạo chapter.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID08_Create_Fail_WhenOrderIndexAlreadyExists()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>
            {
                new()
                {
                    id = Guid.NewGuid(),
                    story_id = story.id,
                    title = "Existing",
                    order_index = 1,
                    content = new string('x', 600),
                    status = "DRAFT",
                    access_type = "FREE",
                    coin_price = 0
                }
            };
            var sut = CreateSut(story, store, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);

            // Act
            var ex = Record.Exception(() => sut.Create(req, ownerId));
            LogTestCase("UTCID08", "Trùng order index phải fail.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Single(store);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID09_Create_Fail_WhenTitleMissing()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>();
            var sut = CreateSut(story, store, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.Title = "  ";

            // Act
            var ex = Record.Exception(() => sut.Create(req, ownerId));
            LogTestCase("UTCID09", "Title null/whitespace phải fail.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID10_Create_Fail_WhenTitleDuplicatedInStory()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>
            {
                new()
                {
                    id = Guid.NewGuid(),
                    story_id = story.id,
                    title = "Duplicate title",
                    order_index = 1,
                    content = new string('y', 600),
                    status = "DRAFT",
                    access_type = "FREE",
                    coin_price = 0
                }
            };
            var sut = CreateSut(story, store, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 2);
            req.Title = "Duplicate title";

            // Act
            var ex = Record.Exception(() => sut.Create(req, ownerId));
            LogTestCase("UTCID10", "Trùng title trong cùng story phải fail.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID11_Create_Fail_WhenAccessTypeInvalid()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>();
            var sut = CreateSut(story, store, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.AccessType = "VIP";

            // Act
            var ex = Record.Exception(() => sut.Create(req, ownerId));
            LogTestCase("UTCID11", "AccessType không hợp lệ phải fail.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID12_Create_Fail_WhenPaidCoinPriceOutOfRange()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId, views: 1000);
            var store = new List<chapters>();
            var sut = CreateSut(story, store, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.AccessType = "PAID";
            req.CoinPrice = 5;

            // Act
            var ex = Record.Exception(() => sut.Create(req, ownerId));
            LogTestCase("UTCID12", "PAID coin ngoài range 10-100 phải fail.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID13_Create_Fail_WhenPaidButStoryViewsBelow500()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId, views: 499);
            var store = new List<chapters>();
            var sut = CreateSut(story, store, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.AccessType = "PAID";
            req.CoinPrice = 10;

            // Act
            var ex = Record.Exception(() => sut.Create(req, ownerId));
            LogTestCase("UTCID13", "PAID nhưng story views < 500 phải fail.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID14_Create_Fail_WhenFreeButCoinPriceGreaterThanZero()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>();
            var sut = CreateSut(story, store, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.AccessType = "FREE";
            req.CoinPrice = 20;

            // Act
            var ex = Record.Exception(() => sut.Create(req, ownerId));
            LogTestCase("UTCID14", "FREE nhưng coin > 0 phải fail.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID15_Create_Fail_WhenContentShorterThan500Chars()
        {
            // Arrange
            var ownerId = Guid.NewGuid();
            var story = BuildStory(ownerId);
            var store = new List<chapters>();
            var sut = CreateSut(story, store, out var chapterRepoMock, out _, out _, out _, out _);
            var req = BuildRequest(story.id, 1);
            req.Content = new string('z', 499);

            // Act
            var ex = Record.Exception(() => sut.Create(req, ownerId));
            LogTestCase("UTCID15", "Nội dung < 500 ký tự phải fail.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }
    }
}

// dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT_CreateChapter" --logger "console;verbosity=detailed"
