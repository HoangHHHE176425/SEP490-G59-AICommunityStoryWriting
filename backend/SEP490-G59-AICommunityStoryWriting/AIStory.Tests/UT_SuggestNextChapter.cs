using System.Security.Claims;
using AIStory.API.Controllers;
using AIStory.API.Services;
using BusinessObjects.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Repositories;
using Repositories.Interfaces;
using Services;
using Services.DTOs.AI;
using Services.DTOs.Admin;
using Services.Implementations;
using Services.Interfaces;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT_SuggestNextChapter
    {
        private readonly ITestOutputHelper _output;

        private static readonly Guid StoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        private static readonly Guid AuthorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        public UT_SuggestNextChapter(ITestOutputHelper output) => _output = output;

        private void LogTestCase(
            string utcId,
            string spec,
            object? input,
            object? output,
            Exception? ex = null)
        {
            _output.WriteLine("");
            _output.WriteLine($"========== {utcId} ==========");
            _output.WriteLine($"SPEC   : {spec}");
            _output.WriteLine($"INPUT  : {JsonSerializer.Serialize(input, _jsonOptions)}");

            if (ex != null)
            {
                _output.WriteLine("OUTPUT : ERROR");
                _output.WriteLine($"TYPE   : {ex.GetType().Name}");
                _output.WriteLine($"MSG    : {ex.Message}");
            }
            else
            {
                _output.WriteLine("OUTPUT : SUCCESS");
                _output.WriteLine($"RESULT : {JsonSerializer.Serialize(output, _jsonOptions)}");
            }
        }

        private static AINextChapterService CreateService(
            out Mock<IStoryRepository> storyRepo,
            out Mock<IChapterRepository> chapterRepo,
            out Mock<IStoryRagService> ragService,
            out Mock<IStoryMemoryEngine> memoryEngine,
            out Mock<IAIUsageLogRepository> usageLog,
            out Mock<IUserLookup> userLookup,
            out Mock<IAuthorAiTokenBudgetService> budgetMock,
            IConfiguration? configuration = null)
        {
            storyRepo = new Mock<IStoryRepository>(MockBehavior.Strict);
            chapterRepo = new Mock<IChapterRepository>(MockBehavior.Strict);
            ragService = new Mock<IStoryRagService>(MockBehavior.Strict);
            memoryEngine = new Mock<IStoryMemoryEngine>(MockBehavior.Strict);
            usageLog = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
            userLookup = new Mock<IUserLookup>(MockBehavior.Strict);
            budgetMock = new Mock<IAuthorAiTokenBudgetService>(MockBehavior.Strict);
            configuration ??= new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:SuggestMinRequiredTokens"] = "3800"
            }).Build();

            return new AINextChapterService(
                storyRepo.Object,
                chapterRepo.Object,
                ragService.Object,
                memoryEngine.Object,
                usageLog.Object,
                configuration,
                userLookup.Object,
                budgetMock.Object,
                NullLogger<AINextChapterService>.Instance);
        }

        private static (AIController sut, Mock<IAINextChapterService> nextChapterServiceMock) CreateAiControllerForSuggest()
        {
            var nextChapterService = new Mock<IAINextChapterService>(MockBehavior.Strict);
            var coCreateService = new Mock<IAICoCreationService>(MockBehavior.Strict);
            var chapterCheckService = new Mock<IChapterCheckService>(MockBehavior.Strict);
            var chapterCompareService = new Mock<IChapterCompareService>(MockBehavior.Strict);
            var chapterVersionCompareService = new Mock<IChapterVersionAiCompareService>(MockBehavior.Strict);
            var ragService = new Mock<IStoryRagService>(MockBehavior.Strict);
            var storyRepository = new Mock<IStoryRepository>(MockBehavior.Strict);
            var rateLimitService = new Mock<IAISuggestRateLimitService>(MockBehavior.Strict);
            var budgetService = new Mock<IAuthorAiTokenBudgetService>(MockBehavior.Loose);
            var env = new Mock<IWebHostEnvironment>(MockBehavior.Strict);
            env.Setup(x => x.EnvironmentName).Returns("Production");
            var configuration = new ConfigurationBuilder().AddInMemoryCollection().Build();
            var sut = new AIController(
                nextChapterService.Object,
                coCreateService.Object,
                chapterCheckService.Object,
                chapterCompareService.Object,
                chapterVersionCompareService.Object,
                ragService.Object,
                storyRepository.Object,
                rateLimitService.Object,
                budgetService.Object,
                configuration,
                env.Object,
                NullLogger<AIController>.Instance);
            var identity = new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, AuthorId.ToString()) }, "test-auth");
            sut.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };
            return (sut, nextChapterService);
        }

        /// <summary>
        /// UTCID01 — request StoryId rỗng: <see cref="AINextChapterService.SuggestNextChapterAsync"/> ném <see cref="ArgumentException"/>.
        /// </summary>
        [Fact]
        public async Task UTCID01_SuggestNextChapterAsync_ThrowsArgumentException_WhenStoryIdEmpty()
        {
            // Arrange
            var sut = CreateService(out var storyRepo, out _, out _, out _, out _, out _, out _);

            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.SuggestNextChapterAsync(new SuggestNextChapterRequest { StoryId = Guid.Empty }, AuthorId, CancellationToken.None));

            LogTestCase(
                utcId: "UTCID01",
                spec: "StoryId rỗng.",
                input: new { StoryId = Guid.Empty, AuthorId },
                output: null,
                ex: ex);

            // Assert
            Assert.Equal("StoryId là bắt buộc.", ex.Message);
            storyRepo.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// UTCID02 — <c>authorUserId</c> không xác định (UT: <see cref="Guid.Empty"/>): chặn trước budget/repo; <see cref="UnauthorizedAccessException"/>.
        /// </summary>
        [Fact]
        public async Task UTCID02_SuggestNextChapterAsync_ThrowsUnauthorized_WhenAuthorIdMissing()
        {
            // Arrange
            var sut = CreateService(out var storyRepo, out var chapterRepo, out _, out _, out _, out _, out var budgetMock);

            // Act
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                sut.SuggestNextChapterAsync(new SuggestNextChapterRequest { StoryId = StoryId }, Guid.Empty, CancellationToken.None));

            LogTestCase(
                utcId: "UTCID02",
                spec: "authorUserId không xác định (Guid.Empty): không gọi budget hay đọc truyện.",
                input: new { StoryId, authorUserId = (Guid?)null },
                output: null,
                ex: ex);

            // Assert
            Assert.Equal("Không xác định được người dùng. Vui lòng đăng nhập lại.", ex.Message);
            storyRepo.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
            budgetMock.Verify(x => x.EnsureWithinBudgetAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
            chapterRepo.Verify(x => x.GetByStoryId(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// UTCID03 — <see cref="IAuthorAiTokenBudgetService.EnsureWithinBudgetAsync"/> ném <see cref="AuthorAiTokenBudgetExceededException"/>; không đọc truyện.
        /// </summary>
        [Fact]
        public async Task UTCID03_SuggestNextChapterAsync_ThrowsWhenTokenBudgetExceeded()
        {
            // Arrange
            var sut = CreateService(out var storyRepo, out _, out _, out _, out _, out _, out var budgetMock);
            budgetMock
                .Setup(x => x.EnsureWithinBudgetAsync(AuthorId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new AuthorAiTokenBudgetExceededException(4500, 4000, AuthorAiTokenBudgetPeriodKind.PerDayUtc));

            // Act
            await Assert.ThrowsAsync<AuthorAiTokenBudgetExceededException>(() =>
                sut.SuggestNextChapterAsync(new SuggestNextChapterRequest { StoryId = StoryId }, AuthorId, CancellationToken.None));

            LogTestCase(
                utcId: "UTCID03",
                spec: "Vượt hạn mức token (EnsureWithinBudgetAsync → AuthorAiTokenBudgetExceededException).",
                input: new { StoryId, AuthorId },
                output: new { thrown = nameof(AuthorAiTokenBudgetExceededException) },
                ex: null);

            // Assert
            budgetMock.Verify(x => x.EnsureWithinBudgetAsync(AuthorId, It.IsAny<CancellationToken>()), Times.Once);
            storyRepo.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// UTCID04 — <c>tokensRemaining</c> &lt; <c>AI:SuggestMinRequiredTokens</c>: <see cref="AuthorAiEstimatedTokensInsufficientException"/>; chưa đọc truyện.
        /// </summary>
        [Fact]
        public async Task UTCID04_SuggestNextChapterAsync_ThrowsWhenEstimatedTokensInsufficient()
        {
            // Arrange
            var sut = CreateService(out var storyRepo, out _, out _, out _, out _, out _, out var budgetMock);
            budgetMock.Setup(x => x.EnsureWithinBudgetAsync(AuthorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            budgetMock
                .Setup(x => x.GetBudgetAsync(AuthorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AuthorAiTokenBudgetDto { TokensRemaining = 1000 });

            // Act
            var ex = await Assert.ThrowsAsync<AuthorAiEstimatedTokensInsufficientException>(() =>
                sut.SuggestNextChapterAsync(new SuggestNextChapterRequest { StoryId = StoryId }, AuthorId, CancellationToken.None));

            LogTestCase(
                utcId: "UTCID04",
                spec: "Không đủ hạn mức tối thiểu ước tính (tokensRemaining nhỏ hơn AI:SuggestMinRequiredTokens).",
                input: new { StoryId, AuthorId, Remaining = 1000 },
                output: null,
                ex: ex);

            // Assert
            Assert.Equal(3800, ex.MinRequiredTokens);
            budgetMock.Verify(x => x.EnsureWithinBudgetAsync(AuthorId, It.IsAny<CancellationToken>()), Times.Once);
            budgetMock.Verify(x => x.GetBudgetAsync(AuthorId, It.IsAny<CancellationToken>()), Times.Once);
            storyRepo.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// UTCID05 — happy path: các trường request hợp lệ; JWT có tác giả → <see cref="AIController.SuggestNextChapter"/> <c>200</c> và <see cref="SuggestNextChapterResponse"/> (mock <see cref="IAINextChapterService"/>, không chạy pipeline AI).
        /// </summary>
        [Fact]
        public async Task UTCID05_SuggestNextChapter_Returns200_WhenAllRequestFieldsValid()
        {
            // Arrange
            var (sut, nextMock) = CreateAiControllerForSuggest();
            var request = new SuggestNextChapterRequest { StoryId = StoryId, ChapterId = null, UpToChapterId = null };
            var expected = new SuggestNextChapterResponse
            {
                Suggestions =
                {
                    new NextChapterSuggestionItemDto
                    {
                        Title = "Hướng A",
                        Summary = "Tóm tắt",
                        Direction = "Chi tiết",
                        KeyEvents = "Sự kiện",
                        CharactersInvolved = "NV"
                    }
                },
                ContextUsed = new SuggestNextChapterContextDto { StoryTitle = "Truyện mẫu", ChaptersIncluded = 1 },
                ContextWarning = null
            };
            nextMock
                .Setup(x => x.SuggestNextChapterAsync(request, AuthorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var result = await sut.SuggestNextChapter(request, CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsType<SuggestNextChapterResponse>(ok.Value);

            LogTestCase(
                utcId: "UTCID05",
                spec: "Tất cả trường hợp lệ → HTTP 200 và SuggestNextChapterResponse (mock service; pipeline AI không chạy trong UT).",
                input: request,
                output: payload,
                ex: null);

            // Assert
            Assert.Single(payload.Suggestions);
            Assert.Equal("Hướng A", payload.Suggestions[0].Title);
            nextMock.Verify(x => x.SuggestNextChapterAsync(request, AuthorId, It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// UTCID06 — người gọi không phải tác giả truyện (<c>story.author_id</c> ≠ caller) → <see cref="UnauthorizedAccessException"/>; không <c>GetByStoryId</c>.
        /// </summary>
        [Fact]
        public async Task UTCID06_SuggestNextChapterAsync_ThrowsUnauthorized_WhenCallerIsNotStoryAuthor()
        {
            // Arrange
            var otherAuthor = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var sut = CreateService(out var storyRepo, out var chapterRepo, out _, out _, out _, out _, out var budgetMock);
            budgetMock.Setup(x => x.EnsureWithinBudgetAsync(AuthorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            budgetMock
                .Setup(x => x.GetBudgetAsync(AuthorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AuthorAiTokenBudgetDto { TokensRemaining = 9000 });
            storyRepo.Setup(x => x.GetById(StoryId)).Returns(new stories
            {
                id = StoryId,
                author_id = otherAuthor,
                title = "T",
                slug = "t",
                story_progress_status = "ongoing"
            });

            // Act
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                sut.SuggestNextChapterAsync(new SuggestNextChapterRequest { StoryId = StoryId }, AuthorId, CancellationToken.None));

            LogTestCase(
                utcId: "UTCID06",
                spec: "Caller không phải tác giả của truyện (author_id khác).",
                input: new { StoryId, storyAuthorId = otherAuthor, callerAuthorId = AuthorId },
                output: null,
                ex: ex);

            // Assert
            Assert.Contains("gợi ý chương", ex.Message, StringComparison.OrdinalIgnoreCase);
            budgetMock.Verify(x => x.EnsureWithinBudgetAsync(AuthorId, It.IsAny<CancellationToken>()), Times.Once);
            budgetMock.Verify(x => x.GetBudgetAsync(AuthorId, It.IsAny<CancellationToken>()), Times.Once);
            storyRepo.Verify(x => x.GetById(StoryId), Times.Once);
            chapterRepo.Verify(x => x.GetByStoryId(It.IsAny<Guid>()), Times.Never);
        }
    }
}


//
//dotnet test "f:\DA\SEP490-G59-AICommunityStoryWriting\backend\SEP490-G59-AICommunityStoryWriting\AIStory.Tests\AIStory.Tests.csproj" --filter "FullyQualifiedName~AIStory.Tests.UT_SuggestNextChapter" --logger "console;verbosity=detailed" -v:n