using System.Security.Claims;
using AIStory.API.Controllers;
using AIStory.API.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Repositories;
using Services;
using Services.DTOs.AI;
using Services.DTOs.Admin;
using Services.Interfaces;
using Xunit;

namespace AIStory.Tests;

public class UT_CoCreate
{
    private static readonly Guid StoryId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid AuthorId = Guid.Parse("22222222-2222-2222-2222-222222222222");

    private static (AIController sut, Mock<IAICoCreationService> coCreateServiceMock, Mock<IAuthorAiTokenBudgetService> budgetServiceMock)
        CreateControllerSut(
            bool withValidUser = true,
            bool tokenBudgetAllowed = true,
            bool enoughTokens = true)
    {
        var nextChapterService = new Mock<IAINextChapterService>(MockBehavior.Strict);
        var coCreateService = new Mock<IAICoCreationService>(MockBehavior.Strict);
        var chapterCheckService = new Mock<IChapterCheckService>(MockBehavior.Strict);
        var chapterCompareService = new Mock<IChapterCompareService>(MockBehavior.Strict);
        var chapterVersionCompareService = new Mock<IChapterVersionAiCompareService>(MockBehavior.Strict);
        var ragService = new Mock<IStoryRagService>(MockBehavior.Strict);
        var storyRepository = new Mock<IStoryRepository>(MockBehavior.Strict);
        var rateLimitService = new Mock<IAISuggestRateLimitService>(MockBehavior.Strict);
        var budgetService = new Mock<IAuthorAiTokenBudgetService>(MockBehavior.Strict);
        var env = new Mock<IWebHostEnvironment>(MockBehavior.Strict);

        env.Setup(x => x.EnvironmentName).Returns("Production");

        budgetService
            .Setup(x => x.EnsureWithinBudgetAsync(AuthorId, It.IsAny<CancellationToken>()))
            .Returns(tokenBudgetAllowed
                ? Task.CompletedTask
                : Task.FromException(new AuthorAiTokenBudgetExceededException(50000, 50000, AuthorAiTokenBudgetPeriodKind.PerMonthUtc)));

        budgetService
            .Setup(x => x.GetBudgetAsync(AuthorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorAiTokenBudgetDto
            {
                TokensRemaining = enoughTokens ? 20000 : 5000
            });

        var configData = new Dictionary<string, string?>
        {
            ["AI:CoCreateMinRequiredTokens"] = "14000"
        };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(configData)
            .Build();

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

        var identity = withValidUser
            ? new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, AuthorId.ToString()) }, "test-auth")
            : new ClaimsIdentity();

        sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(identity)
            }
        };

        return (sut, coCreateService, budgetService);
    }

    [Fact]
    // Case 01: StoryId rỗng thì API phải trả về 400 BadRequest.
    public async Task UTCID01_CoCreate_Returns400_WhenStoryIdIsEmpty()
    {
        var (sut, coCreateServiceMock, _) = CreateControllerSut();
        var request = new CoCreationRequest { StoryId = Guid.Empty, AuthorIdea = "Ý tưởng mới" };

        var result = await sut.CoCreate(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("StoryId là bắt buộc", badRequest.Value?.ToString());
        coCreateServiceMock.Verify(
            x => x.CoCreateAsync(It.IsAny<CoCreationRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    // Case 02: Không có claim user hợp lệ thì API phải trả về 401 Unauthorized.
    public async Task UTCID02_CoCreate_Returns401_WhenAuthorIdIsInvalid()
    {
        var (sut, coCreateServiceMock, _) = CreateControllerSut(withValidUser: false);
        var request = new CoCreationRequest { StoryId = StoryId, AuthorIdea = "Ý tưởng mới" };

        var result = await sut.CoCreate(request, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        Assert.Contains("Không xác định được người dùng", unauthorized.Value?.ToString());
        coCreateServiceMock.Verify(
            x => x.CoCreateAsync(It.IsAny<CoCreationRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    // Case 03: Vượt token budget của tác giả thì API phải trả về 403.
    public async Task UTCID03_CoCreate_Returns403_WhenTokenBudgetExceeded()
    {
        var (sut, coCreateServiceMock, _) = CreateControllerSut(tokenBudgetAllowed: false);
        var request = new CoCreationRequest { StoryId = StoryId, AuthorIdea = "Ý tưởng mới" };

        var result = await sut.CoCreate(request, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Contains("giới hạn token", forbidden.Value?.ToString());
        coCreateServiceMock.Verify(
            x => x.CoCreateAsync(It.IsAny<CoCreationRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    // Case 04: Token còn lại thấp hơn mức tối thiểu ước tính thì API phải trả về 403.
    public async Task UTCID04_CoCreate_Returns403_WhenRemainingTokenIsInsufficient()
    {
        var (sut, coCreateServiceMock, _) = CreateControllerSut(enoughTokens: false);
        var request = new CoCreationRequest { StoryId = StoryId, AuthorIdea = "Ý tưởng mới" };

        var result = await sut.CoCreate(request, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Contains("không đủ", forbidden.Value?.ToString());
        coCreateServiceMock.Verify(
            x => x.CoCreateAsync(It.IsAny<CoCreationRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    // Case 05: Luồng hợp lệ, service trả dữ liệu co-create thì API phải trả về 200 Ok.
    public async Task UTCID05_CoCreate_Returns200_WhenInputIsValid()
    {
        var (sut, coCreateServiceMock, _) = CreateControllerSut();
        var request = new CoCreationRequest { StoryId = StoryId, AuthorIdea = "Ý tưởng hợp lệ" };
        var expected = new CoCreationResponse
        {
            Outline = "Dàn ý test",
            FinalContent = "Nội dung test",
            Approved = true
        };

        coCreateServiceMock
            .Setup(x => x.CoCreateAsync(request, AuthorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await sut.CoCreate(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<CoCreationResponse>(ok.Value);
        Assert.Equal("Nội dung test", payload.FinalContent);
        coCreateServiceMock.Verify(
            x => x.CoCreateAsync(request, AuthorId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    // Case 06: Service báo không phải tác giả truyện thì API phải trả về 403.
    public async Task UTCID06_CoCreate_Returns403_WhenCallerIsNotStoryAuthor()
    {
        var (sut, coCreateServiceMock, _) = CreateControllerSut();
        var request = new CoCreationRequest { StoryId = StoryId, AuthorIdea = null };

        coCreateServiceMock
            .Setup(x => x.CoCreateAsync(request, AuthorId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Không phải là tác giả của truyện."));

        var result = await sut.CoCreate(request, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Contains("Không phải là tác giả", forbidden.Value?.ToString());
        coCreateServiceMock.Verify(
            x => x.CoCreateAsync(request, AuthorId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    // Case 07: AuthorIdea để null vẫn hợp lệ, API vẫn phải trả về 200 Ok.
    public async Task UTCID07_CoCreate_Returns200_WhenAuthorIdeaIsNull()
    {
        var (sut, coCreateServiceMock, _) = CreateControllerSut();
        var request = new CoCreationRequest { StoryId = StoryId, AuthorIdea = null };
        var expected = new CoCreationResponse
        {
            Outline = "Dàn ý khi không nhập ý tưởng",
            FinalContent = "Nội dung AI tự viết theo mạch truyện",
            Approved = true
        };

        coCreateServiceMock
            .Setup(x => x.CoCreateAsync(request, AuthorId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await sut.CoCreate(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<CoCreationResponse>(ok.Value);
        Assert.Equal("Nội dung AI tự viết theo mạch truyện", payload.FinalContent);
        coCreateServiceMock.Verify(
            x => x.CoCreateAsync(request, AuthorId, It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
