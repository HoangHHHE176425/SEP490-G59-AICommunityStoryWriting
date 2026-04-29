using BusinessObjects.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Services.DTOs.Comments;
using Services.Implementations;
using Services.Interfaces;
using System.Text.Json;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT_CreateCommentStory
    {
        private readonly ITestOutputHelper _output;

        public UT_CreateCommentStory(ITestOutputHelper output) => _output = output;

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
            }
            else
            {
                _output.WriteLine("OUTPUT : SUCCESS");
                _output.WriteLine($"RESULT : {JsonSerializer.Serialize(output)}");
            }
        }

        private static StoryCommentPostService CreatePostServiceSut(
            out Mock<IStoryLookup> storyLookupMock,
            out Mock<IUserLookup> userLookupMock,
            out Mock<IUserActivityLookup> userActivityMock,
            out Mock<IStoryCommentCommand> commentCommandMock,
            out Mock<ICommentReactionReader> reactionReaderMock,
            out Mock<INotificationHubNotifier> notifierMock)
        {
            storyLookupMock = new Mock<IStoryLookup>(MockBehavior.Strict);
            userLookupMock = new Mock<IUserLookup>(MockBehavior.Strict);
            userActivityMock = new Mock<IUserActivityLookup>(MockBehavior.Strict);
            commentCommandMock = new Mock<IStoryCommentCommand>(MockBehavior.Strict);
            reactionReaderMock = new Mock<ICommentReactionReader>(MockBehavior.Strict);
            notifierMock = new Mock<INotificationHubNotifier>(MockBehavior.Loose);

            return new StoryCommentPostService(
                storyLookupMock.Object,
                userLookupMock.Object,
                userActivityMock.Object,
                commentCommandMock.Object,
                reactionReaderMock.Object,
                notifierMock.Object,
                NullLogger<StoryCommentPostService>.Instance);
        }

        [Fact]
        public async Task UTCID01_AddAsync_Success_WhenRootCommentValid()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var story = new stories
            {
                id = storyId,
                author_id = Guid.NewGuid(),
                title = "Story",
                slug = "story",
                status = "PUBLISHED",
                comments_disabled = false
            };
            var commenterNav = new users
            {
                id = userId,
                email = "reader@test.local",
                role = "USER",
                created_at = DateTime.UtcNow
            };
            var savedEntity = new comments
            {
                id = commentId,
                user_id = userId,
                story_id = storyId,
                chapter_id = null,
                parent_id = null,
                content = "abc happy path comment",
                likes_count = 0,
                status = "APPROVED",
                created_at = DateTime.UtcNow,
                userNavigation = commenterNav
            };
            var sut = CreatePostServiceSut(
                out var storyLookup,
                out var userLookup,
                out var userActivity,
                out var commentCmd,
                out var reactionReader,
                out _);
            storyLookup.Setup(x => x.GetById(storyId)).Returns(story);
            userLookup.Setup(x => x.Exists(userId)).Returns(true);
            userActivity.Setup(x => x.HasReadAnyChapterOfStory(userId, storyId)).Returns(true);
            commentCmd
                .Setup(x => x.AddStoryComment(storyId, userId, "abc happy path comment", null))
                .Returns(savedEntity);
            reactionReader
                .Setup(x => x.GetSummary(commentId, userId))
                .Returns((false, (string?)null, new Dictionary<string, int>()));

            // Act
            var outcome = await sut.AddAsync(storyId, userId, "abc happy path comment", null, default);
            LogTestCase("UTCID01", new { storyId, userId, content = "abc happy path comment", parentId = (Guid?)null }, outcome);

            // Assert
            Assert.Equal(StoryCommentPostStatus.Success, outcome.Status);
            Assert.NotNull(outcome.Dto);
            Assert.Equal(commentId, outcome.Dto.Id);
            Assert.Equal(storyId, outcome.Dto.StoryId);
            Assert.Null(outcome.Dto.ParentId);
            Assert.Equal(userId, outcome.Dto.UserId);
            Assert.Equal("abc happy path comment", outcome.Dto.Content);

            storyLookup.Verify(x => x.GetById(storyId), Times.Once);
            userActivity.Verify(x => x.HasReadAnyChapterOfStory(userId, storyId), Times.Once);
            commentCmd.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            commentCmd.Verify(x => x.AddStoryComment(storyId, userId, "abc happy path comment", null), Times.Once);
        }

        [Fact]
        public async Task UTCID02_AddAsync_Fail_WhenStoryNotFound()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var sut = CreatePostServiceSut(
                out var storyLookup,
                out var userLookup,
                out var userActivity,
                out var commentCmd,
                out var reactionReader,
                out _);
            storyLookup.Setup(x => x.GetById(storyId)).Returns((stories?)null);

            // Act
            var outcome = await sut.AddAsync(storyId, userId, "content", null, default);
            LogTestCase("UTCID02", new { storyId, userId }, outcome);

            // Assert
            Assert.Equal(StoryCommentPostStatus.StoryNotFound, outcome.Status);
            Assert.Null(outcome.Dto);
            userLookup.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
            commentCmd.Verify(x => x.AddStoryComment(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
            reactionReader.Verify(x => x.GetSummary(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact]
        public async Task UTCID03_AddAsync_Fail_WhenStoryCommentsDisabled()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var sut = CreatePostServiceSut(
                out var storyLookup,
                out _,
                out var userActivity,
                out var commentCmd,
                out var reactionReader,
                out _);
            storyLookup.Setup(x => x.GetById(storyId)).Returns(new stories
            {
                id = storyId,
                status = "PUBLISHED",
                comments_disabled = true
            });

            // Act
            var outcome = await sut.AddAsync(storyId, userId, "content", null, default);
            LogTestCase("UTCID03", new { storyId, userId }, outcome);

            // Assert
            Assert.Equal(StoryCommentPostStatus.Rejected, outcome.Status);
            Assert.Null(outcome.Dto);
            userActivity.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
            commentCmd.Verify(x => x.AddStoryComment(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
            reactionReader.Verify(x => x.GetSummary(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact]
        public async Task UTCID04_AddAsync_Fail_WhenStoryNotPublished()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var sut = CreatePostServiceSut(
                out var storyLookup,
                out _,
                out var userActivity,
                out var commentCmd,
                out var reactionReader,
                out _);
            storyLookup.Setup(x => x.GetById(storyId)).Returns(new stories
            {
                id = storyId,
                status = "DRAFT",
                comments_disabled = false
            });

            // Act
            var outcome = await sut.AddAsync(storyId, userId, "content", null, default);
            LogTestCase("UTCID04", new { storyId, userId }, outcome);

            // Assert
            Assert.Equal(StoryCommentPostStatus.Rejected, outcome.Status);
            Assert.Null(outcome.Dto);
            userActivity.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
            commentCmd.Verify(x => x.AddStoryComment(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
            reactionReader.Verify(x => x.GetSummary(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact]
        public async Task UTCID05_AddAsync_Fail_WhenUserNotExists()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var sut = CreatePostServiceSut(
                out var storyLookup,
                out var userLookup,
                out var userActivity,
                out var commentCmd,
                out var reactionReader,
                out _);
            storyLookup.Setup(x => x.GetById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED", comments_disabled = false });
            userLookup.Setup(x => x.Exists(userId)).Returns(false);

            // Act
            var outcome = await sut.AddAsync(storyId, userId, "content", null, default);
            LogTestCase("UTCID05", new { storyId, userId }, outcome);

            // Assert
            Assert.Equal(StoryCommentPostStatus.Rejected, outcome.Status);
            Assert.Null(outcome.Dto);
            userActivity.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
            commentCmd.Verify(x => x.AddStoryComment(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
            reactionReader.Verify(x => x.GetSummary(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact]
        public async Task UTCID06_AddAsync_Fail_WhenUserHasNotReadAnyChapter()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var sut = CreatePostServiceSut(
                out var storyLookup,
                out var userLookup,
                out var userActivity,
                out var commentCmd,
                out var reactionReader,
                out _);
            storyLookup.Setup(x => x.GetById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED", comments_disabled = false });
            userLookup.Setup(x => x.Exists(userId)).Returns(true);
            userActivity.Setup(x => x.HasReadAnyChapterOfStory(userId, storyId)).Returns(false);

            // Act
            var outcome = await sut.AddAsync(storyId, userId, "content", null, default);
            LogTestCase("UTCID06", new { storyId, userId }, outcome);

            // Assert
            Assert.Equal(StoryCommentPostStatus.Rejected, outcome.Status);
            Assert.Null(outcome.Dto);
            commentCmd.Verify(x => x.AddStoryComment(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
            reactionReader.Verify(x => x.GetSummary(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact]
        public async Task UTCID07_AddAsync_Fail_WhenParentCommentInvalid()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var parentId = Guid.NewGuid();
            var sut = CreatePostServiceSut(
                out var storyLookup,
                out var userLookup,
                out var userActivity,
                out var commentCmd,
                out var reactionReader,
                out _);
            storyLookup.Setup(x => x.GetById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED", comments_disabled = false });
            userLookup.Setup(x => x.Exists(userId)).Returns(true);
            userActivity.Setup(x => x.HasReadAnyChapterOfStory(userId, storyId)).Returns(true);
            commentCmd.Setup(x => x.GetById(parentId)).Returns((comments?)null);

            // Act
            var outcome = await sut.AddAsync(storyId, userId, "content", parentId, default);
            LogTestCase("UTCID07", new { storyId, userId, parentId }, outcome);

            // Assert
            Assert.Equal(StoryCommentPostStatus.Rejected, outcome.Status);
            Assert.Null(outcome.Dto);
            commentCmd.Verify(x => x.AddStoryComment(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
            reactionReader.Verify(x => x.GetSummary(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact]
        public async Task UTCID08_AddAsync_Success_WhenReplyCommentValid()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var parentId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var sut = CreatePostServiceSut(
                out var storyLookup,
                out var userLookup,
                out var userActivity,
                out var commentCmd,
                out var reactionReader,
                out _);
            storyLookup.Setup(x => x.GetById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED", comments_disabled = false, author_id = Guid.NewGuid(), title = "Story" });
            userLookup.Setup(x => x.Exists(userId)).Returns(true);
            userActivity.Setup(x => x.HasReadAnyChapterOfStory(userId, storyId)).Returns(true);
            commentCmd.Setup(x => x.GetById(parentId)).Returns(new comments
            {
                id = parentId,
                story_id = storyId,
                chapter_id = null,
                user_id = Guid.NewGuid(),
                content = "parent"
            });
            commentCmd.Setup(x => x.AddStoryComment(storyId, userId, "reply content", parentId)).Returns(new comments
            {
                id = commentId,
                story_id = storyId,
                parent_id = parentId,
                user_id = userId,
                content = "reply content",
                created_at = DateTime.UtcNow,
                status = "APPROVED",
                userNavigation = new users { id = userId, email = "u@test.local", role = "USER", created_at = DateTime.UtcNow }
            });
            reactionReader.Setup(x => x.GetSummary(commentId, userId)).Returns((false, (string?)null, new Dictionary<string, int>()));

            // Act
            var outcome = await sut.AddAsync(storyId, userId, "reply content", parentId, default);
            LogTestCase("UTCID08", new { storyId, userId, parentId }, outcome);

            // Assert
            Assert.Equal(StoryCommentPostStatus.Success, outcome.Status);
            Assert.NotNull(outcome.Dto);
            Assert.Equal(parentId, outcome.Dto.ParentId);
            commentCmd.Verify(x => x.AddStoryComment(storyId, userId, "reply content", parentId), Times.Once);
        }

        [Fact]
        public async Task UTCID09_AddAsync_Fail_WhenParentIsChapterComment()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var parentId = Guid.NewGuid();
            var sut = CreatePostServiceSut(
                out var storyLookup,
                out var userLookup,
                out var userActivity,
                out var commentCmd,
                out var reactionReader,
                out _);
            storyLookup.Setup(x => x.GetById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED", comments_disabled = false });
            userLookup.Setup(x => x.Exists(userId)).Returns(true);
            userActivity.Setup(x => x.HasReadAnyChapterOfStory(userId, storyId)).Returns(true);
            commentCmd.Setup(x => x.GetById(parentId)).Returns(new comments
            {
                id = parentId,
                story_id = storyId,
                chapter_id = Guid.NewGuid(),
                user_id = Guid.NewGuid(),
                content = "chapter level parent"
            });

            // Act
            var outcome = await sut.AddAsync(storyId, userId, "reply content", parentId, default);
            LogTestCase("UTCID09", new { storyId, userId, parentId }, outcome);

            // Assert
            Assert.Equal(StoryCommentPostStatus.Rejected, outcome.Status);
            Assert.Null(outcome.Dto);
            commentCmd.Verify(x => x.AddStoryComment(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
            reactionReader.Verify(x => x.GetSummary(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
        }
    }
}

// dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT_CreateCommentStory" --logger "console;verbosity=detailed"