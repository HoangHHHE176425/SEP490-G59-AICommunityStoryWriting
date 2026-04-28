using BusinessObjects.Entities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Repositories;
using Services.Implementations;
using Services.Interfaces;
using System.Text.Json;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT_ApproveChapter
    {
        private readonly ITestOutputHelper _output;

        public UT_ApproveChapter(ITestOutputHelper output) => _output = output;

        private void LogTestCase(string utcId, object? input, object? output, Exception? ex = null)
        {
            _output.WriteLine("");
            _output.WriteLine($"========== {utcId} ==========");
            _output.WriteLine($"INPUT  : {JsonSerializer.Serialize(input)}");
            if (ex != null)
            {
                _output.WriteLine("OUTPUT : ERROR");
                _output.WriteLine($"TYPE   : {ex.GetType().Name}");
                _output.WriteLine($"MSG    : {ex.Message}");
                return;
            }

            _output.WriteLine("OUTPUT : SUCCESS");
            _output.WriteLine($"RESULT : {JsonSerializer.Serialize(output)}");
        }

        private static ModerationService CreateSut(
            List<chapters> chapterStore,
            out Mock<IChapterRepository> chapterRepoMock,
            out Mock<IChapterVersionRepository> versionRepoMock,
            out Mock<IStoryRepository> storyRepoMock)
        {
            storyRepoMock = new Mock<IStoryRepository>(MockBehavior.Strict);
            chapterRepoMock = new Mock<IChapterRepository>(MockBehavior.Strict);
            versionRepoMock = new Mock<IChapterVersionRepository>(MockBehavior.Strict);
            var storyServiceMock = new Mock<IStoryService>(MockBehavior.Strict);
            var chapterServiceMock = new Mock<IChapterService>(MockBehavior.Strict);
            var scopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
            var forfeitMock = new Mock<IReviewDeadlineForfeitureService>(MockBehavior.Strict);
            forfeitMock.Setup(f => f.ProcessOverdueClaims()).Returns(0);

            chapterRepoMock.Setup(x => x.GetById(It.IsAny<Guid>()))
                .Returns((Guid id) => chapterStore.FirstOrDefault(c => c.id == id));
            chapterRepoMock.Setup(x => x.Update(It.IsAny<chapters>()))
                .Callback((chapters c) =>
                {
                    var idx = chapterStore.FindIndex(x => x.id == c.id);
                    if (idx >= 0) chapterStore[idx] = c;
                });
            chapterRepoMock.Setup(x => x.GetByStoryIdAndOrderIndex(It.IsAny<Guid>(), It.IsAny<int>()))
                .Returns((Guid storyId, int index) =>
                    chapterStore.FirstOrDefault(c => c.story_id == storyId && c.order_index == index));

            versionRepoMock.Setup(x => x.GetByChapterId(It.IsAny<Guid>()))
                .Returns((Guid _) => new List<chapter_versions>());

            return new ModerationService(
                storyRepoMock.Object,
                chapterRepoMock.Object,
                versionRepoMock.Object,
                storyServiceMock.Object,
                chapterServiceMock.Object,
                scopeFactoryMock.Object,
                forfeitMock.Object,
                NullLogger<ModerationService>.Instance,
                moderationHubNotifier: null,
                notificationHubNotifier: null,
                approveEnsureClaimed: (_, _, _) => { },
                approveEnsureNoPendingEscalation: (_, _, _) => { },
                approveGetStoryById: storyId => new stories { id = storyId, status = "PUBLISHED", category = new List<categories> { new() { id = Guid.NewGuid(), name = "C1", slug = "c1", is_active = true } } },
                approveCompleteAssignment: _ => { },
                approveMarkPendingVersionsAsPublished: _ => { },
                enableApproveChapterPostSideEffects: false);
        }

        [Fact]
        public void UTCID01_ApproveChapter_Success_WhenPendingReviewAndValidInput()
        {
            // Arrange
            var chapterId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var chapterStore = new List<chapters>
            {
                new()
                {
                    id = chapterId,
                    story_id = Guid.NewGuid(),
                    order_index = 0,
                    status = "PENDING_REVIEW",
                    title = "Chapter 1",
                    content = "valid content"
                }
            };
            var sut = CreateSut(chapterStore, out var chapterRepoMock, out var versionRepoMock, out _);

            // Act
            var ok = sut.ApproveChapter(chapterId, moderatorId, null);
            LogTestCase("UTCID01", new { chapterId, moderatorId }, new { ok, UpdatedStatus = chapterStore[0].status });

            // Assert
            Assert.True(ok);
            Assert.Equal("PUBLISHED", chapterStore[0].status);
            chapterRepoMock.Verify(x => x.Update(It.IsAny<chapters>()), Times.Once);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
            versionRepoMock.Verify(x => x.Add(It.IsAny<chapter_versions>()), Times.Never);
        }

        [Fact]
        public void UTCID02_ApproveChapter_Fail_WhenAllowedCategoryIdsEmpty()
        {
            // Arrange
            var chapterId = Guid.NewGuid();
            var chapterStore = new List<chapters>
            {
                new() { id = chapterId, story_id = Guid.NewGuid(), order_index = 0, status = "PENDING_REVIEW" }
            };
            var sut = CreateSut(chapterStore, out var chapterRepoMock, out _, out _);

            // Act
            var ok = sut.ApproveChapter(chapterId, Guid.NewGuid(), Array.Empty<Guid>());
            LogTestCase("UTCID02", new { chapterId }, new { ok });

            // Assert
            Assert.False(ok);
            chapterRepoMock.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID03_ApproveChapter_Fail_WhenChapterNotFound()
        {
            // Arrange
            var sut = CreateSut(new List<chapters>(), out var chapterRepoMock, out _, out _);
            var chapterId = Guid.NewGuid();

            // Act
            var ok = sut.ApproveChapter(chapterId, Guid.NewGuid(), null);
            LogTestCase("UTCID03", new { chapterId }, new { ok });

            // Assert
            Assert.False(ok);
            chapterRepoMock.Verify(x => x.GetById(chapterId), Times.Once);
            chapterRepoMock.Verify(x => x.Update(It.IsAny<chapters>()), Times.Never);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID04_ApproveChapter_Fail_WhenChapterNotPendingAndNoPendingVersion()
        {
            // Arrange
            var chapterId = Guid.NewGuid();
            var chapterStore = new List<chapters>
            {
                new() { id = chapterId, story_id = Guid.NewGuid(), order_index = 0, status = "DRAFT" }
            };
            var sut = CreateSut(chapterStore, out var chapterRepoMock, out _, out _);

            // Act
            var ok = sut.ApproveChapter(chapterId, Guid.NewGuid(), null);
            LogTestCase("UTCID04", new { chapterId }, new { ok });

            // Assert
            Assert.False(ok);
            chapterRepoMock.Verify(x => x.Update(It.IsAny<chapters>()), Times.Never);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID05_ApproveChapter_Fail_WhenPreviousChapterNotPublished()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var chapterId = Guid.NewGuid();
            var chapterStore = new List<chapters>
            {
                new() { id = Guid.NewGuid(), story_id = storyId, order_index = 0, status = "DRAFT" },
                new() { id = chapterId, story_id = storyId, order_index = 1, status = "PENDING_REVIEW", published_at = null }
            };
            var sut = CreateSut(chapterStore, out var chapterRepoMock, out _, out _);

            // Act
            var ex = Record.Exception(() => sut.ApproveChapter(chapterId, Guid.NewGuid(), null));

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            chapterRepoMock.Verify(x => x.Update(It.IsAny<chapters>()), Times.Never);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID06_ApproveChapter_Success_WhenHasPendingVersionEvenChapterDraft()
        {
            // Arrange
            var chapterId = Guid.NewGuid();
            var chapterStore = new List<chapters>
            {
                new() { id = chapterId, story_id = Guid.NewGuid(), order_index = 0, status = "DRAFT", title = "old", content = "old content", published_at = DateTime.UtcNow.AddDays(-3) }
            };
            var sut = CreateSut(chapterStore, out var chapterRepoMock, out var versionRepoMock, out _);
            versionRepoMock.Setup(x => x.GetByChapterId(chapterId)).Returns(new List<chapter_versions>
            {
                new() { id = Guid.NewGuid(), chapter_id = chapterId, status = "PENDING_REVIEW", title_snapshot = "new title", content_snapshot = "new content words here" }
            });

            // Act
            var ok = sut.ApproveChapter(chapterId, Guid.NewGuid(), null);
            LogTestCase("UTCID06", new { chapterId }, new { ok, UpdatedTitle = chapterStore[0].title });

            // Assert
            Assert.True(ok);
            Assert.Equal("new title", chapterStore[0].title);
            Assert.Equal("PUBLISHED", chapterStore[0].status);
            chapterRepoMock.Verify(x => x.Update(It.IsAny<chapters>()), Times.Once);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID07_ApproveChapter_Fail_WhenClaimCheckThrows()
        {
            // Arrange
            var chapterId = Guid.NewGuid();
            var chapterStore = new List<chapters>
            {
                new() { id = chapterId, story_id = Guid.NewGuid(), order_index = 0, status = "PENDING_REVIEW" }
            };
            var storyRepoMock = new Mock<IStoryRepository>(MockBehavior.Strict);
            var chapterRepoMock = new Mock<IChapterRepository>(MockBehavior.Strict);
            var versionRepoMock = new Mock<IChapterVersionRepository>(MockBehavior.Strict);
            var storyServiceMock = new Mock<IStoryService>(MockBehavior.Strict);
            var chapterServiceMock = new Mock<IChapterService>(MockBehavior.Strict);
            var scopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
            var forfeitMock = new Mock<IReviewDeadlineForfeitureService>(MockBehavior.Strict);
            forfeitMock.Setup(f => f.ProcessOverdueClaims()).Returns(0);
            chapterRepoMock.Setup(x => x.GetById(chapterId)).Returns(chapterStore[0]);
            versionRepoMock.Setup(x => x.GetByChapterId(chapterId)).Returns(new List<chapter_versions>());

            var sut = new ModerationService(
                storyRepoMock.Object,
                chapterRepoMock.Object,
                versionRepoMock.Object,
                storyServiceMock.Object,
                chapterServiceMock.Object,
                scopeFactoryMock.Object,
                forfeitMock.Object,
                NullLogger<ModerationService>.Instance,
                null,
                null,
                (_, _, _) => throw new InvalidOperationException("not assignee"),
                (_, _, _) => { },
                _ => null,
                _ => { },
                _ => { },
                false);

            // Act
            var ex = Record.Exception(() => sut.ApproveChapter(chapterId, Guid.NewGuid(), null));

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            chapterRepoMock.Verify(x => x.Update(It.IsAny<chapters>()), Times.Never);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID08_ApproveChapter_Fail_WhenEscalationCheckThrows()
        {
            // Arrange
            var chapterId = Guid.NewGuid();
            var chapterStore = new List<chapters>
            {
                new() { id = chapterId, story_id = Guid.NewGuid(), order_index = 0, status = "PENDING_REVIEW" }
            };
            var storyRepoMock = new Mock<IStoryRepository>(MockBehavior.Strict);
            var chapterRepoMock = new Mock<IChapterRepository>(MockBehavior.Strict);
            var versionRepoMock = new Mock<IChapterVersionRepository>(MockBehavior.Strict);
            var storyServiceMock = new Mock<IStoryService>(MockBehavior.Strict);
            var chapterServiceMock = new Mock<IChapterService>(MockBehavior.Strict);
            var scopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Strict);
            var forfeitMock = new Mock<IReviewDeadlineForfeitureService>(MockBehavior.Strict);
            forfeitMock.Setup(f => f.ProcessOverdueClaims()).Returns(0);
            chapterRepoMock.Setup(x => x.GetById(chapterId)).Returns(chapterStore[0]);
            versionRepoMock.Setup(x => x.GetByChapterId(chapterId)).Returns(new List<chapter_versions>());

            var sut = new ModerationService(
                storyRepoMock.Object,
                chapterRepoMock.Object,
                versionRepoMock.Object,
                storyServiceMock.Object,
                chapterServiceMock.Object,
                scopeFactoryMock.Object,
                forfeitMock.Object,
                NullLogger<ModerationService>.Instance,
                null,
                null,
                (_, _, _) => { },
                (_, _, _) => throw new InvalidOperationException("pending escalation"),
                _ => null,
                _ => { },
                _ => { },
                false);

            // Act
            var ex = Record.Exception(() => sut.ApproveChapter(chapterId, Guid.NewGuid(), null));

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            chapterRepoMock.Verify(x => x.Update(It.IsAny<chapters>()), Times.Never);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }
    }
}

// dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT_ApproveChapter" --logger "console;verbosity=detailed"