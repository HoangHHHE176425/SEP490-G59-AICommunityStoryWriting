using System.IdentityModel.Tokens.Jwt;
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
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests;

public class UT_SuggestNextChapter
{
    private readonly ITestOutputHelper _output;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
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

    private static AIController CreateSut(
        IConfiguration? configuration = null,
        bool isDevelopment = false,
        ClaimsPrincipal? user = null,
        Mock<IAINextChapterService>? aiNextChapterServiceMock = null,
        Mock<IAuthorAiTokenBudgetService>? authorBudgetMock = null)
    {
        aiNextChapterServiceMock ??= new Mock<IAINextChapterService>(MockBehavior.Strict);
        authorBudgetMock ??= new Mock<IAuthorAiTokenBudgetService>(MockBehavior.Strict);

        var coCreateMock = new Mock<IAICoCreationService>(MockBehavior.Strict);
        var chapterCheckMock = new Mock<IChapterCheckService>(MockBehavior.Strict);
        var chapterCompareMock = new Mock<IChapterCompareService>(MockBehavior.Strict);
        var chapterVersionCompareMock = new Mock<IChapterVersionAiCompareService>(MockBehavior.Strict);
        var ragServiceMock = new Mock<IStoryRagService>(MockBehavior.Strict);
        var storyRepoMock = new Mock<IStoryRepository>(MockBehavior.Strict);
        var rateLimitMock = new Mock<IAISuggestRateLimitService>(MockBehavior.Strict);

        var envMock = new Mock<IWebHostEnvironment>(MockBehavior.Strict);
        envMock.SetupGet(x => x.EnvironmentName).Returns(isDevelopment ? "Development" : "Production");

        configuration ??= new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:SuggestMinRequiredTokens"] = "3800"
        }).Build();

        var controller = new AIController(
            aiNextChapterServiceMock.Object,
            coCreateMock.Object,
            chapterCheckMock.Object,
            chapterCompareMock.Object,
            chapterVersionCompareMock.Object,
            ragServiceMock.Object,
            storyRepoMock.Object,
            rateLimitMock.Object,
            authorBudgetMock.Object,
            configuration,
            envMock.Object,
            NullLogger<AIController>.Instance);

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
            }
        };

        return controller;
    }

    private static ClaimsPrincipal BuildUser(Guid userId)
    {
        var identity = new ClaimsIdentity(new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString())
        }, "Test");
        return new ClaimsPrincipal(identity);
    }

    private static T? GetProp<T>(object? obj, string name)
    {
        if (obj == null) return default;
        var p = obj.GetType().GetProperty(name);
        if (p == null) return default;
        var val = p.GetValue(obj);
        if (val is T t) return t;
        return default;
    }

    [Fact]
    // Case 01: StoryId rỗng thì API phải trả về 400 BadRequest.
    public async Task UTCID01_SuggestNextChapter_ReturnsBadRequest_WhenStoryIdEmpty()
    {
        var authorBudgetMock = new Mock<IAuthorAiTokenBudgetService>(MockBehavior.Strict);
        var aiMock = new Mock<IAINextChapterService>(MockBehavior.Strict);
        var user = BuildUser(Guid.NewGuid());
        var sut = CreateSut(user: user, aiNextChapterServiceMock: aiMock, authorBudgetMock: authorBudgetMock);

        var result = await sut.SuggestNextChapter(new SuggestNextChapterRequest { StoryId = Guid.Empty }, CancellationToken.None);

        var bad = Assert.IsType<BadRequestObjectResult>(result);
        LogTestCase("UTCID01", "StoryId rỗng trả 400.", new { StoryId = Guid.Empty, UserId = user.FindFirstValue(JwtRegisteredClaimNames.Sub) }, bad.Value);
        Assert.Equal("StoryId là bắt buộc.", GetProp<string>(bad.Value, "message"));
        aiMock.Verify(x => x.SuggestNextChapterAsync(It.IsAny<SuggestNextChapterRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        authorBudgetMock.VerifyNoOtherCalls();
    }

    [Fact]
    // Case 02: Không có claim user hợp lệ thì API phải trả về 401 Unauthorized.
    public async Task UTCID02_SuggestNextChapter_ReturnsUnauthorized_WhenNoValidUserClaim()
    {
        var authorBudgetMock = new Mock<IAuthorAiTokenBudgetService>(MockBehavior.Strict);
        var aiMock = new Mock<IAINextChapterService>(MockBehavior.Strict);
        var sut = CreateSut(user: new ClaimsPrincipal(new ClaimsIdentity()), aiNextChapterServiceMock: aiMock, authorBudgetMock: authorBudgetMock);

        var result = await sut.SuggestNextChapter(new SuggestNextChapterRequest { StoryId = Guid.NewGuid() }, CancellationToken.None);

        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
        LogTestCase("UTCID02", "Thiếu claim user trả 401.", new { StoryId = "valid", User = "anonymous" }, unauthorized.Value);
        Assert.Equal("Không xác định được người dùng. Vui lòng đăng nhập lại.", GetProp<string>(unauthorized.Value, "message"));
        aiMock.Verify(x => x.SuggestNextChapterAsync(It.IsAny<SuggestNextChapterRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        authorBudgetMock.VerifyNoOtherCalls();
    }

    [Fact]
    // Case 03: Vượt token budget của tác giả thì API phải trả về 403.
    public async Task UTCID03_SuggestNextChapter_Returns403_WhenOverTokenBudget()
    {
        var userId = Guid.NewGuid();
        var authorBudgetMock = new Mock<IAuthorAiTokenBudgetService>(MockBehavior.Strict);
        var aiMock = new Mock<IAINextChapterService>(MockBehavior.Strict);
        authorBudgetMock
            .Setup(x => x.EnsureWithinBudgetAsync(userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new AuthorAiTokenBudgetExceededException(4500, 4000, AuthorAiTokenBudgetPeriodKind.PerDayUtc));

        var sut = CreateSut(user: BuildUser(userId), aiNextChapterServiceMock: aiMock, authorBudgetMock: authorBudgetMock);

        var result = await sut.SuggestNextChapter(new SuggestNextChapterRequest { StoryId = Guid.NewGuid() }, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        LogTestCase("UTCID03", "Vượt token budget trả 403.", new { UserId = userId, StoryId = "valid" }, obj.Value);
        Assert.Equal(403, obj.StatusCode);
        Assert.Contains("giới hạn token", GetProp<string>(obj.Value, "message") ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        aiMock.Verify(x => x.SuggestNextChapterAsync(It.IsAny<SuggestNextChapterRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        authorBudgetMock.VerifyAll();
    }

    [Fact]
    // Case 04: Token còn lại thấp hơn mức tối thiểu ước tính thì API phải trả về 403.
    public async Task UTCID04_SuggestNextChapter_Returns403_WhenEstimatedTokenInsufficient()
    {
        var userId = Guid.NewGuid();
        var authorBudgetMock = new Mock<IAuthorAiTokenBudgetService>(MockBehavior.Strict);
        var aiMock = new Mock<IAINextChapterService>(MockBehavior.Strict);
        authorBudgetMock
            .Setup(x => x.EnsureWithinBudgetAsync(userId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        authorBudgetMock
            .Setup(x => x.GetBudgetAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorAiTokenBudgetDto { TokensRemaining = 1000 });

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AI:SuggestMinRequiredTokens"] = "3800"
        }).Build();
        var sut = CreateSut(configuration: config, user: BuildUser(userId), aiNextChapterServiceMock: aiMock, authorBudgetMock: authorBudgetMock);

        var result = await sut.SuggestNextChapter(new SuggestNextChapterRequest { StoryId = Guid.NewGuid() }, CancellationToken.None);

        var obj = Assert.IsType<ObjectResult>(result);
        LogTestCase("UTCID04", "Token còn lại không đủ trả 403.", new { UserId = userId, StoryId = "valid", Remaining = 1000 }, obj.Value);
        Assert.Equal(403, obj.StatusCode);
        Assert.Contains("không đủ", GetProp<string>(obj.Value, "message") ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        aiMock.Verify(x => x.SuggestNextChapterAsync(It.IsAny<SuggestNextChapterRequest>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        authorBudgetMock.VerifyAll();
    }

    [Fact]
    // Case 05: Luồng hợp lệ, service trả dữ liệu gợi ý thì API phải trả về 200 Ok.
    public async Task UTCID05_SuggestNextChapter_ReturnsOk_WhenServiceSucceeds()
    {
        var userId = Guid.NewGuid();
        var req = new SuggestNextChapterRequest { StoryId = Guid.NewGuid() };
        var expected = new SuggestNextChapterResponse
        {
            Suggestions = new List<NextChapterSuggestionItemDto>
            {
                new() { Title = "Hướng 1", Summary = "Tóm tắt", Direction = "Diễn biến" }
            }
        };

        var authorBudgetMock = new Mock<IAuthorAiTokenBudgetService>(MockBehavior.Strict);
        var aiMock = new Mock<IAINextChapterService>(MockBehavior.Strict);
        authorBudgetMock.Setup(x => x.EnsureWithinBudgetAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        authorBudgetMock.Setup(x => x.GetBudgetAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorAiTokenBudgetDto { TokensRemaining = 9000 });
        aiMock.Setup(x => x.SuggestNextChapterAsync(req, userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var sut = CreateSut(user: BuildUser(userId), aiNextChapterServiceMock: aiMock, authorBudgetMock: authorBudgetMock);

        var result = await sut.SuggestNextChapter(req, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var payload = Assert.IsType<SuggestNextChapterResponse>(ok.Value);
        LogTestCase("UTCID05", "Input hợp lệ trả 200.", new { req.StoryId, UserId = userId }, payload);
        Assert.Single(payload.Suggestions);
        Assert.Equal("Hướng 1", payload.Suggestions[0].Title);
        aiMock.VerifyAll();
        authorBudgetMock.VerifyAll();
    }

    [Fact]
    // Case 06: Service báo không phải tác giả truyện thì API phải trả về 403.
    public async Task UTCID06_SuggestNextChapter_Returns403_WhenCallerIsNotStoryAuthor()
    {
        var userId = Guid.NewGuid();
        var req = new SuggestNextChapterRequest { StoryId = Guid.NewGuid() };
        var authorBudgetMock = new Mock<IAuthorAiTokenBudgetService>(MockBehavior.Strict);
        var aiMock = new Mock<IAINextChapterService>(MockBehavior.Strict);
        authorBudgetMock.Setup(x => x.EnsureWithinBudgetAsync(userId, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        authorBudgetMock.Setup(x => x.GetBudgetAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthorAiTokenBudgetDto { TokensRemaining = 9000 });
        aiMock.Setup(x => x.SuggestNextChapterAsync(req, userId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException("Bạn không phải là tác giả của truyện này."));

        var sut = CreateSut(user: BuildUser(userId), aiNextChapterServiceMock: aiMock, authorBudgetMock: authorBudgetMock);
        var result = await sut.SuggestNextChapter(req, CancellationToken.None);

        var forbidden = Assert.IsType<ObjectResult>(result);
        LogTestCase("UTCID06", "Không phải tác giả trả 403.", new { req.StoryId, UserId = userId }, forbidden.Value);
        Assert.Equal(403, forbidden.StatusCode);
        Assert.Equal("Bạn không phải là tác giả của truyện này.", GetProp<string>(forbidden.Value, "message"));
    }
}
