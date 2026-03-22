using System.Security.Claims;
using AIStory.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Services.DTOs.AI;
using Services.DTOs.Stories;
using Services.Interfaces;
using Xunit;

namespace AIStory.Tests;

public class UC13_RateLikeStoryTests
{
    private static StoriesController CreateController(Guid? userId, IStoryService? storyService = null)
    {
        var ctrl = new StoriesController(
            storyService ?? new DelegateStoryService(),
            new FakeContentGuardrailService(),
            new StubStoryReportService(),
            new NoOpNotificationHubNotifier(),
            NullLogger<StoriesController>.Instance);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = BuildUser(userId)
            }
        };
        return ctrl;
    }

    private static ClaimsPrincipal BuildUser(Guid? userId)
    {
        if (!userId.HasValue)
            return new ClaimsPrincipal(new ClaimsIdentity());
        return new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.Value.ToString())
        }, authenticationType: "TestAuth"));
    }

    [Fact]
    public void RateStory_Unauthorized_WhenNoUserIdInToken()
    {
        var ctrl = CreateController(userId: null);
        var result = ctrl.RateStory(Guid.NewGuid(), new RateStoryRequestDto { StarValue = 5, ReviewText = "ok" });
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public void RateStory_InvalidOperationException_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        var ctrl = CreateController(userId, new DelegateStoryService(rate: (_, _, _, _) => throw new InvalidOperationException("bad")));
        var result = ctrl.RateStory(Guid.NewGuid(), new RateStoryRequestDto { StarValue = 5 });
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("bad", bad.Value?.ToString() ?? "");
    }

    [Fact]
    public void RateStory_ArgumentException_ReturnsBadRequest()
    {
        var userId = Guid.NewGuid();
        var ctrl = CreateController(userId, new DelegateStoryService(rate: (_, _, _, _) => throw new ArgumentException("arg")));
        var result = ctrl.RateStory(Guid.NewGuid(), new RateStoryRequestDto { StarValue = 0 });
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("arg", bad.Value?.ToString() ?? "");
    }

    [Fact]
    public void RateStory_Success_ReturnsOkWithAvgAndCount()
    {
        var userId = Guid.NewGuid();
        var storyId = Guid.NewGuid();
        var ctrl = CreateController(userId, new DelegateStoryService(rate: (_, _, _, _) => (4.5m, 12)));

        var result = ctrl.RateStory(storyId, new RateStoryRequestDto { StarValue = 5, ReviewText = "nice" });

        var ok = Assert.IsType<OkObjectResult>(result);
        var text = ok.Value?.ToString() ?? "";
        Assert.Contains("avgRating", text);
        Assert.Contains("ratingCount", text);
    }

    [Fact]
    public void ToggleCommentLike_Unauthorized_WhenNoUserIdInToken()
    {
        var ctrl = CreateController(userId: null);
        var result = ctrl.ToggleCommentLike(Guid.NewGuid(), Guid.NewGuid());
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    private sealed class DelegateStoryService : IStoryService
    {
        private readonly Func<Guid, Guid, int, string?, (decimal avg, int count)> _rate;

        public DelegateStoryService(Func<Guid, Guid, int, string?, (decimal avg, int count)>? rate = null)
        {
            _rate = rate ?? ((_, _, _, _) => (0m, 0));
        }

        public StoryResponseDto Create(CreateStoryRequestDto request, Guid authorId, string? coverImageUrl) => throw new NotImplementedException();
        public PagedResultDto<StoryListItemDto> GetAll(StoryQueryDto query) => throw new NotImplementedException();
        public StoryResponseDto? GetById(Guid id, Guid? userId = null) => null;
        public StoryResponseDto? GetBySlug(string slug, Guid? userId = null) => null;
        public void SaveReadingProgress(Guid storyId, Guid userId, Guid chapterId) { }
        public PagedResultDto<StoryListItemDto> GetByAuthor(Guid authorId, StoryQueryDto query) => throw new NotImplementedException();
        public bool Update(Guid id, UpdateStoryRequestDto request) => throw new NotImplementedException();
        public bool Delete(Guid id) => throw new NotImplementedException();
        public bool Publish(Guid id) => throw new NotImplementedException();
        public bool Unpublish(Guid id) => throw new NotImplementedException();
        public void RecordViewIfAllowed(Guid storyId, string viewerKey) { }
        public void RecordReadStory(Guid storyId, Guid userId, string? ipAddress = null, string? deviceInfo = null) { }
        public void RecordReadChapter(Guid storyId, Guid chapterId, Guid userId, string? ipAddress = null, string? deviceInfo = null) { }
        public (decimal avgRating, int ratingCount) RateStory(Guid storyId, Guid userId, int starValue, string? reviewText)
        {
            var (avg, count) = _rate(storyId, userId, starValue, reviewText);
            return (avg, count);
        }
        public (string? reason, DateTime? rejectedAt) GetLatestRejectionForStory(Guid storyId) => (null, null);
    }

    private sealed class FakeContentGuardrailService : IContentGuardrailService
    {
        public Task<GuardrailResult> CheckAsync(Guid storyId, string draftContent, System.Threading.CancellationToken cancellationToken = default)
            => Task.FromResult(new GuardrailResult { Passed = true, Violations = new() });

        public Task<GuardrailResult> CheckCommentBannedWordsAsync(string content, System.Threading.CancellationToken cancellationToken = default)
            => Task.FromResult(new GuardrailResult { Passed = true, Violations = new() });
    }
}

