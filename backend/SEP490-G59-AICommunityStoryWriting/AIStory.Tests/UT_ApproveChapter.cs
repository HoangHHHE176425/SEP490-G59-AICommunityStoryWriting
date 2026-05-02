using BusinessObjects.Entities;
using BusinessObjects;
using DataAccessObjects.DAOs;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Repositories;
using Services.Implementations;
using Services.Interfaces;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT_ApproveChapter
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

        public UT_ApproveChapter(ITestOutputHelper output) => _output = output;

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
                return;
            }

            _output.WriteLine("OUTPUT : SUCCESS");
            _output.WriteLine($"RESULT : {JsonSerializer.Serialize(output, _jsonOptions)}");
        }

        private ModerationService CreateSut(
            List<chapters> chapterStore,
            out Mock<IChapterRepository> chapterRepoMock,
            out Mock<IChapterVersionRepository> versionRepoMock,
            out Mock<IStoryRepository> storyRepoMock,
            Action<string, Guid, Guid>? approveEnsureClaimed = null,
            Action<string, Guid, Guid>? approveEnsureNoPendingEscalation = null,
            Func<Guid, stories?>? approveGetStoryById = null)
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
                new TestLogger<ModerationService>(_output),
                moderationHubNotifier: null,
                notificationHubNotifier: null,
                approveEnsureClaimed: approveEnsureClaimed ?? ((_, _, _) => { }),
                approveEnsureNoPendingEscalation: approveEnsureNoPendingEscalation ?? ((_, _, _) => { }),
                approveGetStoryById: approveGetStoryById ?? (storyId => new stories { id = storyId, status = "PUBLISHED", category = new List<categories> { new() { id = Guid.NewGuid(), name = "C1", slug = "c1", is_active = true } } }),
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
            var businessStatus = string.Equals(chapterStore[0].status, "PUBLISHED", StringComparison.OrdinalIgnoreCase)
                ? "APPROVED"
                : chapterStore[0].status;
            LogTestCase(
                "UTCID01",
                new { chapterId, moderatorId, allowedCategoryIds = (IReadOnlyList<Guid>?)null },
                new { ok, Status = businessStatus, RawStatus = chapterStore[0].status },
                spec: "Chapter tồn tại, status PENDING_REVIEW, moderator claim hợp lệ -> approve thành công.");

            // Assert
            Assert.True(ok);
            Assert.Equal("APPROVED", businessStatus);
            chapterRepoMock.Verify(x => x.Update(It.IsAny<chapters>()), Times.Once);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
            versionRepoMock.Verify(x => x.Add(It.IsAny<chapter_versions>()), Times.Never);
        }

        [Fact]
        public void UTCID02_ApproveChapter_Fail_WhenChapterNotClaimedByModerator()
        {
            // Arrange
            var chapterId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var chapterStore = new List<chapters>
            {
                new() { id = chapterId, story_id = Guid.NewGuid(), order_index = 0, status = "PENDING_REVIEW" }
            };
            var sut = CreateSut(
                chapterStore,
                out var chapterRepoMock,
                out _,
                out _,
                approveEnsureClaimed: ModerationService.EnsureModeratorHasClaimedForReview);

            // Act
            var ex = Record.Exception(() => sut.ApproveChapter(chapterId, moderatorId, null));
            LogTestCase(
                "UTCID02",
                new { chapterId, moderatorId, allowedCategoryIds = (IReadOnlyList<Guid>?)null },
                null,
                ex,
                spec: "Chapter tồn tại, PENDING_REVIEW nhưng chưa được moderator claim -> fail.");

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            chapterRepoMock.Verify(x => x.GetById(chapterId), Times.Once);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
            chapterRepoMock.Verify(x => x.Update(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID03_ApproveChapter_Fail_WhenUserRoleHasNoApprovePermission()
        {
            // Arrange
            var chapterId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            Guid claimedModeratorId;
            using (var db = new StoryPlatformDbContext())
            {
                claimedModeratorId = db.users.Select(u => u.id).FirstOrDefault();
            }
            Assert.NotEqual(Guid.Empty, claimedModeratorId);
            var chapterStore = new List<chapters>
            {
                new() { id = chapterId, story_id = Guid.NewGuid(), order_index = 0, status = "PENDING_REVIEW" }
            };
            // Tạo trạng thái "đã claim bởi moderator khác" để đi đúng nhánh "không có quyền duyệt".
            var claimed = ReviewAssignmentDAO.TryClaim(
                ReviewAssignmentDAO.TargetTypeChapter,
                chapterId,
                claimedModeratorId,
                reviewDeadlineUtc: DateTime.UtcNow.AddHours(4));
            Assert.True(claimed);

            var sut = CreateSut(
                chapterStore,
                out var chapterRepoMock,
                out _,
                out _,
                approveEnsureClaimed: ModerationService.EnsureModeratorHasClaimedForReview);

            // Act
            var ex = Record.Exception(() => sut.ApproveChapter(chapterId, moderatorId, null));
            LogTestCase(
                "UTCID03",
                new { chapterId, moderatorId, claimedModeratorId, allowedCategoryIds = (IReadOnlyList<Guid>?)null },
                null,
                ex,
                spec: "Chapter đã claim bởi moderator khác, user hiện tại không có quyền duyệt -> fail với message thật từ service.");

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Contains("Chỉ moderator đã nhận duyệt", ex.Message);
            Assert.False(string.IsNullOrWhiteSpace(ex.Message));
            chapterRepoMock.Verify(x => x.GetById(chapterId), Times.Once);
            chapterRepoMock.Verify(x => x.Update(It.IsAny<chapters>()), Times.Never);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
            ReviewAssignmentDAO.CompleteAssignment(ReviewAssignmentDAO.TargetTypeChapter, chapterId);
        }

        [Fact]
        public void UTCID04_ApproveChapter_Fail_WhenChapterNotPendingAndNoPendingVersion()
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
                    order_index = 3,
                    status = "DRAFT",
                    title = "Chương 4: Cổ trận không hồi đáp",
                    content = "Minh Dạ thử kích hoạt cổ trận lần nữa, nhưng linh văn trên vách đá đã tắt hẳn."
                }
            };
            var sut = CreateSut(chapterStore, out var chapterRepoMock, out _, out _);

            // Act
            var ok = sut.ApproveChapter(chapterId, moderatorId, null);
            LogTestCase(
                "UTCID04",
                new { chapterId, moderatorId, CurrentStatus = chapterStore[0].status, allowedCategoryIds = (IReadOnlyList<Guid>?)null },
                new { ok },
                spec: "Chapter tồn tại nhưng status = DRAFT (không phải PENDING_REVIEW) và không có version chờ duyệt -> approve thất bại.");

            // Assert
            Assert.False(ok);
            Assert.Equal("DRAFT", chapterStore[0].status);
            chapterRepoMock.Verify(x => x.Update(It.IsAny<chapters>()), Times.Never);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID05_ApproveChapter_Fail_WhenChapterNotFound()
        {
            // Arrange
            var chapterId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var chapterStore = new List<chapters>();
            var sut = CreateSut(chapterStore, out var chapterRepoMock, out _, out _);

            // Act
            var ok = sut.ApproveChapter(chapterId, moderatorId, null);
            LogTestCase(
                "UTCID05",
                new { chapterId, moderatorId, allowedCategoryIds = (IReadOnlyList<Guid>?)null },
                new { ok },
                spec: "ChapterId không tồn tại (repository trả về null) -> approve thất bại.");

            // Assert
            Assert.False(ok);
            chapterRepoMock.Verify(x => x.GetById(chapterId), Times.Once);
            chapterRepoMock.Verify(x => x.Update(It.IsAny<chapters>()), Times.Never);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID06_ApproveChapter_Fail_WhenAllowedCategoryIdsEmpty()
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
                    title = "Chương 1: Lệnh triệu hồi",
                    content = "Gió lạnh thổi qua cổng thành khi lệnh triệu hồi được ban xuống."
                }
            };
            var sut = CreateSut(chapterStore, out var chapterRepoMock, out _, out _);
            IReadOnlyList<Guid> emptyCategories = new List<Guid>();

            // Act
            var ex = Record.Exception(() => sut.ApproveChapter(chapterId, moderatorId, emptyCategories));
            LogTestCase(
                "UTCID06",
                new { chapterId, moderatorId, allowedCategoryIds = emptyCategories },
                null,
                ex,
                spec: "Chapter PENDING_REVIEW, claim pass (mock), allowedCategoryIds = [] → cấu hình không hợp lệ.");

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Equal("Cấu hình không hợp lệ", ex.Message);
            chapterRepoMock.Verify(x => x.GetById(chapterId), Times.Once);
            chapterRepoMock.Verify(x => x.Update(It.IsAny<chapters>()), Times.Never);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID07_ApproveChapter_Fail_WhenCategoryNotOnStory()
        {
            // Arrange
            var chapterId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var categoryOnStory = Guid.NewGuid();
            var categoryRequested = Guid.NewGuid();
            var chapterStore = new List<chapters>
            {
                new()
                {
                    id = chapterId,
                    story_id = storyId,
                    order_index = 0,
                    status = "PENDING_REVIEW",
                    title = "Chương 2: Ảnh chạng vạng",
                    content = "Ánh sáng cuối con đường mòn khiến bóng cây kéo dài như những vết cắt."
                }
            };
            var sut = CreateSut(
                chapterStore,
                out var chapterRepoMock,
                out _,
                out _,
                approveGetStoryById: sid => sid == storyId
                    ? new stories
                    {
                        id = storyId,
                        status = "PUBLISHED",
                        category = new List<categories>
                        {
                            new() { id = categoryOnStory, name = "Kiếm hiệp", slug = "kiem-hiep", is_active = true }
                        }
                    }
                    : null);
            IReadOnlyList<Guid> allowed = new List<Guid> { categoryRequested };

            // Act
            var ok = sut.ApproveChapter(chapterId, moderatorId, allowed);
            LogTestCase(
                "UTCID07",
                new
                {
                    chapterId,
                    moderatorId,
                    allowedCategoryIds = allowed,
                    categoryOnStory,
                    note = "categoryRequested không có trên truyện → category không tồn tại trong phạm vi duyệt."
                },
                new { ok },
                spec: "Chapter PENDING_REVIEW; truyện có category khác với allowedCategoryIds → fail (không duyệt).");

            // Assert
            Assert.False(ok);
            chapterRepoMock.Verify(x => x.GetById(chapterId), Times.Once);
            chapterRepoMock.Verify(x => x.Update(It.IsAny<chapters>()), Times.Never);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

        [Fact]
        public void UTCID08_ApproveChapter_Fail_WhenMustApproveInOrder()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var chapterIdOrder0 = Guid.NewGuid();
            var chapterIdOrder1 = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var chapterStore = new List<chapters>
            {
                new()
                {
                    id = chapterIdOrder0,
                    story_id = storyId,
                    order_index = 0,
                    status = "DRAFT",
                    title = "Chương 0: Mở đầu",
                    published_at = null
                },
                new()
                {
                    id = chapterIdOrder1,
                    story_id = storyId,
                    order_index = 1,
                    status = "PENDING_REVIEW",
                    title = "Chương 1: Chưa tới lượt",
                    published_at = null
                }
            };
            var sut = CreateSut(chapterStore, out var chapterRepoMock, out _, out _);

            // Act — duyệt chương 1 trong khi chương 0 chưa PUBLISHED
            var ex = Record.Exception(() => sut.ApproveChapter(chapterIdOrder1, moderatorId, null));
            LogTestCase(
                "UTCID08",
                new { chapterId = chapterIdOrder1, moderatorId, storyId, previousChapterOrder0 = chapterIdOrder0 },
                null,
                ex,
                spec: "Publish lần đầu: phải duyệt chương theo thứ tự — chương trước chưa PUBLISHED → fail.");

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Contains("Phải duyệt chương theo thứ tự", ex.Message);
            chapterRepoMock.Verify(x => x.GetByStoryIdAndOrderIndex(storyId, 0), Times.Once);
            chapterRepoMock.Verify(x => x.Update(It.IsAny<chapters>()), Times.Never);
            chapterRepoMock.Verify(x => x.Add(It.IsAny<chapters>()), Times.Never);
        }

    }
}

// dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT_ApproveChapter" --logger "console;verbosity=detailed"