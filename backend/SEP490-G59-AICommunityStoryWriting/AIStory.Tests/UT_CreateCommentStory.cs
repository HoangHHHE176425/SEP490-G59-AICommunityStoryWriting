using BusinessObjects.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Services.DTOs.Comments;
using Services.Implementations;
using Services.Interfaces;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT_CreateCommentStory
    {
        public class TestLogger<T> : ILogger<T>
        {
            private readonly ITestOutputHelper _output;
            public TestLogger(ITestOutputHelper output) => _output = output;
            public IDisposable BeginScope<TState>(TState state) => null!;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => _output.WriteLine(formatter(state, exception));
        }

        private readonly ITestOutputHelper _output;

        public UT_CreateCommentStory(ITestOutputHelper output) => _output = output;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private void LogTestCase(string utcId, object? input, object? output, Exception? ex = null, string? spec = null)
        {
            _output.WriteLine("");
            _output.WriteLine($"========== {utcId} ==========");
            if (!string.IsNullOrWhiteSpace(spec))
                _output.WriteLine($"SPEC   : {spec}");
            _output.WriteLine($"INPUT  : {JsonSerializer.Serialize(input, _jsonOptions)}");
            if (ex != null)
            {
                _output.WriteLine("OUTPUT : ERROR");
                _output.WriteLine($"Exception type: {ex.GetType().Name}");
                _output.WriteLine($"Message: {ex.Message}");
            }
            else
            {
                _output.WriteLine("OUTPUT : SUCCESS");
                _output.WriteLine($"RESULT : {JsonSerializer.Serialize(output, _jsonOptions)}");
            }
        }

        private StoryCommentPostService CreatePostServiceSut(
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

            var logger = new TestLogger<StoryCommentPostService>(_output);
            return new StoryCommentPostService(
                storyLookupMock.Object,
                userLookupMock.Object,
                userActivityMock.Object,
                commentCommandMock.Object,
                reactionReaderMock.Object,
                notifierMock.Object,
                logger);
        }

        [Fact]
        public async Task UTCID01_AddAsync_Success_WhenRootCommentValid()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var rawContent = "   Truyện rất cuốn, mong tác giả ra chương mới sớm!   ";
            var expectedTrimmedContent = "Truyện rất cuốn, mong tác giả ra chương mới sớm!";
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
                content = expectedTrimmedContent,
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
                .Setup(x => x.AddStoryComment(storyId, userId, expectedTrimmedContent, null))
                .Returns(savedEntity);
            reactionReader
                .Setup(x => x.GetSummary(commentId, userId))
                .Returns((false, (string?)null, new Dictionary<string, int>()));

            // Act
            var outcome = await sut.AddAsync(storyId, userId, rawContent, null, default);
            LogTestCase(
                "UTCID01",
                new { storyId, userId, content = rawContent, parentId = (Guid?)null },
                outcome,
                spec: "Story/User hợp lệ, user đã đọc >= 1 chapter, ParentId = null, content được trim -> tạo comment thành công.");

            // Assert
            Assert.Equal(StoryCommentPostStatus.Success, outcome.Status);
            Assert.NotNull(outcome.Dto);
            Assert.Equal(commentId, outcome.Dto.Id);
            Assert.Equal(storyId, outcome.Dto.StoryId);
            Assert.Null(outcome.Dto.ParentId);
            Assert.Equal(userId, outcome.Dto.UserId);
            Assert.Equal(expectedTrimmedContent, outcome.Dto.Content);

            storyLookup.Verify(x => x.GetById(storyId), Times.Once);
            userLookup.Verify(x => x.Exists(userId), Times.Once);
            userActivity.Verify(x => x.HasReadAnyChapterOfStory(userId, storyId), Times.Once);
            commentCmd.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            commentCmd.Verify(x => x.AddStoryComment(storyId, userId, expectedTrimmedContent, null), Times.Once);
        }

        [Fact]
        public async Task UTCID02_AddAsync_Success_WhenReplyCommentValidWithParentId()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var parentId = Guid.Parse("00000000-0000-0000-0000-000000000001");
            var rawContent = "   Mình đồng ý với bình luận ở trên, lập luận rất hợp lý.   ";
            var expectedTrimmedContent = "Mình đồng ý với bình luận ở trên, lập luận rất hợp lý.";
            var sut = CreatePostServiceSut(
                out var storyLookup,
                out var userLookup,
                out var userActivity,
                out var commentCmd,
                out var reactionReader,
                out _);
            storyLookup.Setup(x => x.GetById(storyId)).Returns(new stories
            {
                id = storyId,
                status = "PUBLISHED",
                comments_disabled = false,
                author_id = Guid.NewGuid(),
                title = "Truyện kiếm hiệp"
            });
            userLookup.Setup(x => x.Exists(userId)).Returns(true);
            userActivity.Setup(x => x.HasReadAnyChapterOfStory(userId, storyId)).Returns(true);
            commentCmd.Setup(x => x.GetById(parentId)).Returns(new comments
            {
                id = parentId,
                story_id = storyId,
                chapter_id = null,
                user_id = Guid.NewGuid(),
                content = "Bình luận gốc trước đó."
            });
            commentCmd.Setup(x => x.AddStoryComment(storyId, userId, expectedTrimmedContent, parentId)).Returns(new comments
            {
                id = commentId,
                user_id = userId,
                story_id = storyId,
                parent_id = parentId,
                content = expectedTrimmedContent,
                likes_count = 0,
                status = "APPROVED",
                created_at = DateTime.UtcNow,
                userNavigation = new users { id = userId, email = "reader2@test.local", role = "USER", created_at = DateTime.UtcNow }
            });
            reactionReader.Setup(x => x.GetSummary(commentId, userId))
                .Returns((false, (string?)null, new Dictionary<string, int>()));

            // Act
            var outcome = await sut.AddAsync(storyId, userId, rawContent, parentId, default);
            LogTestCase(
                "UTCID02",
                new { storyId, userId, content = rawContent, parentId },
                outcome,
                spec: "Giống UTCID01 nhưng ParentId = 1 (reply hợp lệ) -> tạo comment thành công.");

            // Assert
            Assert.Equal(StoryCommentPostStatus.Success, outcome.Status);
            Assert.NotNull(outcome.Dto);
            Assert.Equal(commentId, outcome.Dto.Id);
            Assert.Equal(parentId, outcome.Dto.ParentId);
            Assert.Equal(expectedTrimmedContent, outcome.Dto.Content);
            storyLookup.Verify(x => x.GetById(storyId), Times.Once);
            userLookup.Verify(x => x.Exists(userId), Times.Once);
            userActivity.Verify(x => x.HasReadAnyChapterOfStory(userId, storyId), Times.Once);
            commentCmd.Verify(x => x.GetById(parentId), Times.Once);
            commentCmd.Verify(x => x.AddStoryComment(storyId, userId, expectedTrimmedContent, parentId), Times.Once);
            reactionReader.Verify(x => x.GetSummary(commentId, userId), Times.Once);
        }

        [Fact]
        public async Task UTCID03_AddAsync_Fail_WhenContentNull()
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
            storyLookup.Setup(x => x.GetById(storyId)).Returns(new stories
            {
                id = storyId,
                status = "PUBLISHED",
                comments_disabled = false
            });
            userLookup.Setup(x => x.Exists(userId)).Returns(true);
            userActivity.Setup(x => x.HasReadAnyChapterOfStory(userId, storyId)).Returns(true);

            // Act
            var outcome = await sut.AddAsync(storyId, userId, null!, null, default);
            LogTestCase(
                "UTCID03",
                new { storyId, userId, content = (string?)null, parentId = (Guid?)null },
                outcome,
                spec: "Content = null -> fail, không tạo comment.");

            // Assert
            Assert.Equal(StoryCommentPostStatus.Rejected, outcome.Status);
            Assert.Null(outcome.Dto);
            storyLookup.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            userLookup.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
            userActivity.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
            commentCmd.Verify(x => x.AddStoryComment(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
            reactionReader.Verify(x => x.GetSummary(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact]
        public async Task UTCID04_AddAsync_Fail_WhenUserIdNotExists()
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
            storyLookup.Setup(x => x.GetById(storyId)).Returns(new stories
            {
                id = storyId,
                status = "PUBLISHED",
                comments_disabled = false
            });
            userLookup.Setup(x => x.Exists(userId)).Returns(false);

            // Act
            var outcome = await sut.AddAsync(storyId, userId, "Mình muốn để lại cảm nhận.", null, default);
            LogTestCase(
                "UTCID04",
                new { storyId, userId, content = "Mình muốn để lại cảm nhận.", parentId = (Guid?)null },
                outcome,
                spec: "UserId không tồn tại trong hệ thống -> fail, không tạo comment.");

            // Assert
            Assert.Equal(StoryCommentPostStatus.Rejected, outcome.Status);
            Assert.Null(outcome.Dto);
            userLookup.Verify(x => x.Exists(userId), Times.Once);
            userActivity.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
            commentCmd.Verify(x => x.AddStoryComment(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
            reactionReader.Verify(x => x.GetSummary(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact]
        public async Task UTCID05_AddAsync_Fail_WhenStoryNotExists()
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
            var outcome = await sut.AddAsync(storyId, userId, "Không thấy truyện để bình luận.", null, default);
            LogTestCase(
                "UTCID05",
                new { storyId, userId, content = "Không thấy truyện để bình luận.", parentId = (Guid?)null },
                outcome,
                spec: "Story không tồn tại -> fail, không tạo comment.");

            // Assert
            Assert.Equal(StoryCommentPostStatus.StoryNotFound, outcome.Status);
            Assert.Null(outcome.Dto);
            userLookup.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
            userActivity.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
            commentCmd.Verify(x => x.AddStoryComment(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
            reactionReader.Verify(x => x.GetSummary(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact]
        public async Task UTCID06_AddAsync_Fail_WhenContentTooLong()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var tooLongContent = new string('x', 2001);
            var sut = CreatePostServiceSut(
                out var storyLookup,
                out var userLookup,
                out var userActivity,
                out var commentCmd,
                out var reactionReader,
                out _);

            // Act
            var outcome = await sut.AddAsync(storyId, userId, tooLongContent, null, default);
            LogTestCase(
                "UTCID06",
                new { storyId, userId, contentLength = tooLongContent.Length, parentId = (Guid?)null },
                outcome,
                spec: "Content > 2000 ký tự -> fail, không tạo comment.");

            // Assert
            Assert.Equal(StoryCommentPostStatus.Rejected, outcome.Status);
            Assert.Null(outcome.Dto);
            storyLookup.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            userLookup.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
            commentCmd.Verify(x => x.AddStoryComment(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
            reactionReader.Verify(x => x.GetSummary(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
            userActivity.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task UTCID07_AddAsync_Fail_WhenContentWhitespaceOnly()
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
            var whitespaceContent = "   \t \r\n   ";

            // Act
            var outcome = await sut.AddAsync(storyId, userId, whitespaceContent, null, default);
            LogTestCase(
                "UTCID07",
                new { storyId, userId, content = whitespaceContent, parentId = (Guid?)null },
                outcome,
                spec: "Content chỉ có khoảng trắng -> fail, không tạo comment.");

            // Assert
            Assert.Equal(StoryCommentPostStatus.Rejected, outcome.Status);
            Assert.Null(outcome.Dto);
            storyLookup.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            userLookup.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
            userActivity.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
            commentCmd.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            commentCmd.Verify(x => x.AddStoryComment(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
            reactionReader.Verify(x => x.GetSummary(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
        }

        [Fact]
        public async Task UTCID08_AddAsync_Fail_WhenUserHasNotReadStory()
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
            storyLookup.Setup(x => x.GetById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED", comments_disabled = false, author_id = Guid.NewGuid(), title = "Truyện kiếm hiệp" });
            userLookup.Setup(x => x.Exists(userId)).Returns(true);
            userActivity.Setup(x => x.HasReadAnyChapterOfStory(userId, storyId)).Returns(false);

            // Act
            var outcome = await sut.AddAsync(storyId, userId, "Mình chưa đọc chương nào nên không nên bình luận.", null, default);
            LogTestCase(
                "UTCID08",
                new { storyId, userId, content = "Mình chưa đọc chương nào nên không nên bình luận.", parentId = (Guid?)null },
                outcome,
                spec: "User chưa đọc bất kỳ chapter nào của truyện -> fail, không tạo comment.");

            // Assert
            Assert.Equal(StoryCommentPostStatus.Rejected, outcome.Status);
            Assert.Null(outcome.Dto);
            storyLookup.Verify(x => x.GetById(storyId), Times.Once);
            userLookup.Verify(x => x.Exists(userId), Times.Once);
            userActivity.Verify(x => x.HasReadAnyChapterOfStory(userId, storyId), Times.Once);
            commentCmd.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            commentCmd.Verify(x => x.AddStoryComment(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
            reactionReader.Verify(x => x.GetSummary(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
        }

    }
}

// dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT_CreateCommentStory" --logger "console;verbosity=detailed"