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
    public class UT_CoCreate
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

        public UT_CoCreate(ITestOutputHelper output) => _output = output;

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

        private static AICoCreationService CreateService(
            out Mock<IStoryRepository> storyRepo,
            out Mock<IChapterRepository> chapterRepo,
            out Mock<IAiGeneratedContentRepository> aiContentRepo,
            out Mock<IStoryMemoryEngine> memoryEngine,
            out Mock<IContentGuardrailService> guardrail,
            out Mock<IAIUsageLogRepository> usageLog,
            out Mock<IAuthorAiTokenBudgetService> budgetMock,
            out Mock<IStoryRagService> ragServiceMock,
            IConfiguration? configuration = null)
        {
            storyRepo = new Mock<IStoryRepository>(MockBehavior.Strict);
            chapterRepo = new Mock<IChapterRepository>(MockBehavior.Strict);
            aiContentRepo = new Mock<IAiGeneratedContentRepository>(MockBehavior.Strict);
            memoryEngine = new Mock<IStoryMemoryEngine>(MockBehavior.Strict);
            guardrail = new Mock<IContentGuardrailService>(MockBehavior.Strict);
            usageLog = new Mock<IAIUsageLogRepository>(MockBehavior.Strict);
            usageLog.Setup(x => x.SumCoCreatePipelineStepMaxTotals(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>())).Returns(0);
            budgetMock = new Mock<IAuthorAiTokenBudgetService>(MockBehavior.Strict);
            ragServiceMock = new Mock<IStoryRagService>(MockBehavior.Loose);
            ragServiceMock.Setup(x => x.GetRagStatus(It.IsAny<Guid>())).Returns(new RagStatusResponse { EmbeddingConfigured = true });
            ragServiceMock.Setup(x => x.TryEnsureIndexedAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            ragServiceMock.Setup(x => x.IsRagAvailableForStory(It.IsAny<Guid>())).Returns(true);
            configuration ??= new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AI:CoCreateMinRequiredTokens"] = "14000"
            }).Build();

            return new AICoCreationService(
                storyRepo.Object,
                chapterRepo.Object,
                aiContentRepo.Object,
                memoryEngine.Object,
                ragServiceMock.Object,
                guardrail.Object,
                usageLog.Object,
                configuration,
                budgetMock.Object,
                NullLogger<AICoCreationService>.Instance);
        }

        private static (AIController sut, Mock<IAICoCreationService> coCreateServiceMock) CreateAiControllerForCoCreate()
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
            return (sut, coCreateService);
        }

        /// <summary>
        /// UTCID01 — request StoryId rỗng: <see cref="AICoCreationService.CoCreateAsync"/> ném <see cref="ArgumentException"/>.
        /// </summary>
        [Fact]
        public async Task UTCID01_CoCreateAsync_ThrowsArgumentException_WhenStoryIdEmpty()
        {
            // Arrange
            var sut = CreateService(out var storyRepo, out _, out _, out _, out _, out _, out _, out _);

            // Act
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.CoCreateAsync(new CoCreationRequest { StoryId = Guid.Empty, AuthorIdea = "Ý tưởng mới" }, AuthorId, CancellationToken.None));

            LogTestCase(
                utcId: "UTCID01",
                spec: "StoryId rỗng.",
                input: new { StoryId = Guid.Empty, AuthorIdea = "Ý tưởng mới", AuthorId },
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
        public async Task UTCID02_CoCreateAsync_ThrowsUnauthorized_WhenAuthorIdMissing()
        {
            // Arrange
            var sut = CreateService(out var storyRepo, out var chapterRepo, out _, out _, out _, out _, out var budgetMock, out _);

            // Act
            var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                sut.CoCreateAsync(new CoCreationRequest { StoryId = StoryId, AuthorIdea = "Ý tưởng mới" }, Guid.Empty, CancellationToken.None));

            LogTestCase(
                utcId: "UTCID02",
                spec: "authorUserId không xác định (Guid.Empty): không gọi budget/token hay đọc truyện.",
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
        public async Task UTCID03_CoCreateAsync_ThrowsWhenTokenBudgetExceeded()
        {
            // Arrange
            var sut = CreateService(out var storyRepo, out _, out _, out _, out _, out _, out var budgetMock, out _);
            budgetMock
                .Setup(x => x.EnsureWithinBudgetAsync(AuthorId, It.IsAny<CancellationToken>()))
                .ThrowsAsync(new AuthorAiTokenBudgetExceededException(50000, 50000, AuthorAiTokenBudgetPeriodKind.PerMonthUtc));

            // Act
            await Assert.ThrowsAsync<AuthorAiTokenBudgetExceededException>(() =>
                sut.CoCreateAsync(new CoCreationRequest { StoryId = StoryId, AuthorIdea = "Ý tưởng mới" }, AuthorId, CancellationToken.None));

            LogTestCase(
                utcId: "UTCID03",
                spec: "Vượt hạn mức token (EnsureWithinBudgetAsync → AuthorAiTokenBudgetExceededException).",
                input: new { StoryId, AuthorId, tokensUsed = 50000, tokenLimit = 50000, period = nameof(AuthorAiTokenBudgetPeriodKind.PerMonthUtc) },
                output: new { thrown = nameof(AuthorAiTokenBudgetExceededException) },
                ex: null);

            // Assert
            budgetMock.Verify(x => x.EnsureWithinBudgetAsync(AuthorId, It.IsAny<CancellationToken>()), Times.Once);
            storyRepo.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// UTCID04 — <c>tokensRemaining</c> &lt; <c>AI:CoCreateMinRequiredTokens</c>: <see cref="AuthorAiEstimatedTokensInsufficientException"/>; chưa đọc truyện.
        /// </summary>
        [Fact]
        public async Task UTCID04_CoCreateAsync_ThrowsWhenRemainingTokenInsufficient()
        {
            // Arrange
            var sut = CreateService(out var storyRepo, out _, out _, out _, out _, out _, out var budgetMock, out _);
            budgetMock.Setup(x => x.EnsureWithinBudgetAsync(AuthorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            budgetMock
                .Setup(x => x.GetBudgetAsync(AuthorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AuthorAiTokenBudgetDto { TokensRemaining = 5000 });

            // Act
            var ex = await Assert.ThrowsAsync<AuthorAiEstimatedTokensInsufficientException>(() =>
                sut.CoCreateAsync(new CoCreationRequest { StoryId = StoryId, AuthorIdea = "Ý tưởng mới" }, AuthorId, CancellationToken.None));

            LogTestCase(
                utcId: "UTCID04",
                spec: "Không đủ hạn mức tối thiểu ước tính (tokensRemaining nhỏ hơn AI:CoCreateMinRequiredTokens).",
                input: new { StoryId, AuthorId, minRequiredTokens = 14000, tokensRemaining = 5000 },
                output: null,
                ex: ex);

            // Assert
            Assert.Equal(14000, ex.MinRequiredTokens);
            budgetMock.Verify(x => x.EnsureWithinBudgetAsync(AuthorId, It.IsAny<CancellationToken>()), Times.Once);
            budgetMock.Verify(x => x.GetBudgetAsync(AuthorId, It.IsAny<CancellationToken>()), Times.Once);
            storyRepo.Verify(x => x.GetById(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// UTCID05 — happy path: các trường request hợp lệ; JWT có tác giả → <see cref="AIController.CoCreate"/> <c>200</c> và <see cref="CoCreationResponse"/> (mock <see cref="IAICoCreationService"/>, không chạy pipeline AI).
        /// </summary>
        [Fact]
        public async Task UTCID05_CoCreate_Returns200_WhenAllRequestFieldsValid()
        {
            // Arrange
            var (sut, coCreateMock) = CreateAiControllerForCoCreate();
            var request = new CoCreationRequest
            {
                StoryId = StoryId,
                AuthorIdea = "Ý tưởng hợp lệ cho chương tiếp theo",
                ChapterOrderIndex = 0,
                ChapterId = null
            };
            var expected = new CoCreationResponse
            {
                Outline = "Dàn ý hợp lệ",
                FinalContent = "Nội dung chương sau co-create",
                Approved = true,
                RevisionCount = 0
            };
            coCreateMock
                .Setup(x => x.CoCreateAsync(request, AuthorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var result = await sut.CoCreate(request, CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsType<CoCreationResponse>(ok.Value);

            LogTestCase(
                utcId: "UTCID05",
                spec: "Tất cả trường hợp lệ → HTTP 200 và CoCreationResponse (mock service; pipeline AI không chạy trong UT).",
                input: request,
                output: payload,
                ex: null);

            // Assert
            Assert.Equal("Nội dung chương sau co-create", payload.FinalContent);
            coCreateMock.Verify(x => x.CoCreateAsync(request, AuthorId, It.IsAny<CancellationToken>()), Times.Once);
        }

        /// <summary>
        /// UTCID06 — người gọi không phải tác giả truyện (<c>story.author_id</c> ≠ caller) → <see cref="UnauthorizedAccessException"/>; không <c>GetByStoryId</c>.
        /// </summary>
        [Fact]
        public async Task UTCID06_CoCreateAsync_ThrowsUnauthorized_WhenCallerIsNotStoryAuthor()
        {
            // Arrange
            var otherAuthor = Guid.Parse("33333333-3333-3333-3333-333333333333");
            var sut = CreateService(out var storyRepo, out var chapterRepo, out _, out _, out _, out _, out var budgetMock, out _);
            budgetMock.Setup(x => x.EnsureWithinBudgetAsync(AuthorId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            budgetMock
                .Setup(x => x.GetBudgetAsync(AuthorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(new AuthorAiTokenBudgetDto { TokensRemaining = 20000 });
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
                sut.CoCreateAsync(new CoCreationRequest { StoryId = StoryId, AuthorIdea = "Ý tưởng" }, AuthorId, CancellationToken.None));

            LogTestCase(
                utcId: "UTCID06",
                spec: "Caller không phải tác giả của truyện (author_id khác).",
                input: new { StoryId, storyAuthorId = otherAuthor, callerAuthorId = AuthorId, AuthorIdea = "Ý tưởng" },
                output: null,
                ex: ex);

            // Assert
            Assert.Contains("đồng sáng tác", ex.Message, StringComparison.OrdinalIgnoreCase);
            budgetMock.Verify(x => x.EnsureWithinBudgetAsync(AuthorId, It.IsAny<CancellationToken>()), Times.Once);
            budgetMock.Verify(x => x.GetBudgetAsync(AuthorId, It.IsAny<CancellationToken>()), Times.Once);
            storyRepo.Verify(x => x.GetById(StoryId), Times.Once);
            chapterRepo.Verify(x => x.GetByStoryId(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// UTCID07 — <see cref="CoCreationRequest.AuthorIdea"/> tùy chọn; <c>null</c> vẫn là request hợp lệ: <see cref="AIController.CoCreate"/> trả <c>200</c> (giống ma trận UTCID05, không chạy pipeline AI trong UT).
        /// </summary>
        [Fact]
        public async Task UTCID07_CoCreate_Returns200_WhenAuthorIdeaNull()
        {
            // Arrange
            var (sut, coCreateMock) = CreateAiControllerForCoCreate();
            var request = new CoCreationRequest
            {
                StoryId = StoryId,
                AuthorIdea = null,
                ChapterOrderIndex = 0,
                ChapterId = null
            };
            var expected = new CoCreationResponse
            {
                Outline = "Dàn ý (không có gợi ý tác giả)",
                FinalContent = "Nội dung co-create khi AuthorIdea null",
                Approved = true,
                RevisionCount = 0
            };
            coCreateMock
                .Setup(x => x.CoCreateAsync(request, AuthorId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(expected);

            // Act
            var result = await sut.CoCreate(request, CancellationToken.None);
            var ok = Assert.IsType<OkObjectResult>(result);
            var payload = Assert.IsType<CoCreationResponse>(ok.Value);

            LogTestCase(
                utcId: "UTCID07",
                spec: "AuthorIdea = null (tùy chọn) → HTTP 200 và CoCreationResponse; controller không từ chối trước service.",
                input: request,
                output: payload,
                ex: null);

            // Assert
            Assert.Equal("Nội dung co-create khi AuthorIdea null", payload.FinalContent);
            coCreateMock.Verify(x => x.CoCreateAsync(request, AuthorId, It.IsAny<CancellationToken>()), Times.Once);
        }
    }
}