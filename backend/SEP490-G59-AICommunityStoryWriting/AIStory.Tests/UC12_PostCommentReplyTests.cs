using System.Security.Claims;
using AIStory.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Services.DTOs.Comments;
using Services.DTOs.Chapters;
using Services.DTOs.Stories;
using Services.Interfaces;
using Xunit;

namespace AIStory.Tests;

public class UC12_PostCommentReplyTests
{
    private static StoriesController CreateStoriesController(Guid? userId)
    {
        var ctrl = new StoriesController(new FakeStoryService(), NullLogger<StoriesController>.Instance);
        ctrl.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = BuildUser(userId)
            }
        };
        return ctrl;
    }

    private static ChaptersController CreateChaptersController(Guid? userId, ChapterResponseDto? chapter = null)
    {
        var ctrl = new ChaptersController(
            chapterService: new FakeChapterService(chapter),
            chapterVersionService: new FakeChapterVersionService(),
            scopeFactory: new FakeServiceScopeFactory(),
            storyService: new FakeStoryService());

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
    public void PostStoryComment_Unauthorized_WhenNoUserIdInToken()
    {
        var ctrl = CreateStoriesController(userId: null);
        var result = ctrl.AddStoryComment(Guid.NewGuid(), new CreateStoryCommentRequestDto { Content = "hi" });
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public void PostStoryComment_EmptyContent_ReturnsBadRequest()
    {
        var ctrl = CreateStoriesController(userId: Guid.NewGuid());
        var result = ctrl.AddStoryComment(Guid.NewGuid(), new CreateStoryCommentRequestDto { Content = "   " });
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("không được để trống", bad.Value?.ToString() ?? "");
    }

    [Fact]
    public void PostStoryComment_ContentTooLong_ReturnsBadRequest()
    {
        var ctrl = CreateStoriesController(userId: Guid.NewGuid());
        var longContent = new string('a', 2001);
        var result = ctrl.AddStoryComment(Guid.NewGuid(), new CreateStoryCommentRequestDto { Content = longContent });
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("2000", bad.Value?.ToString() ?? "");
    }

    [Fact]
    public void PostChapterComment_Unauthorized_WhenNoUserIdInToken()
    {
        var ctrl = CreateChaptersController(userId: null, chapter: null);
        var result = ctrl.AddChapterComment(Guid.NewGuid(), new CreateStoryCommentRequestDto { Content = "hi" });
        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public void PostChapterComment_EmptyContent_ReturnsBadRequest()
    {
        var ctrl = CreateChaptersController(userId: Guid.NewGuid(), chapter: null);
        var result = ctrl.AddChapterComment(Guid.NewGuid(), new CreateStoryCommentRequestDto { Content = "  " });
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("không được để trống", bad.Value?.ToString() ?? "");
    }

    [Fact]
    public void PostChapterComment_ContentTooLong_ReturnsBadRequest()
    {
        var ctrl = CreateChaptersController(userId: Guid.NewGuid(), chapter: null);
        var longContent = new string('a', 2001);
        var result = ctrl.AddChapterComment(Guid.NewGuid(), new CreateStoryCommentRequestDto { Content = longContent });
        var bad = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Contains("2000", bad.Value?.ToString() ?? "");
    }

    [Fact]
    public void PostChapterComment_ChapterNotFound_ReturnsNotFound()
    {
        var ctrl = CreateChaptersController(userId: Guid.NewGuid(), chapter: null);
        var result = ctrl.AddChapterComment(Guid.NewGuid(), new CreateStoryCommentRequestDto { Content = "hello" });
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public void GetChapterComments_ChapterNotFound_ReturnsNotFound()
    {
        var ctrl = CreateChaptersController(userId: null, chapter: null);
        var result = ctrl.GetChapterComments(Guid.NewGuid());
        Assert.IsType<NotFoundObjectResult>(result);
    }

    private sealed class FakeStoryService : IStoryService
    {
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
        public (decimal avgRating, int ratingCount) RateStory(Guid storyId, Guid userId, int starValue, string? reviewText) => throw new NotImplementedException();
        public (string? reason, DateTime? rejectedAt) GetLatestRejectionForStory(Guid storyId) => (null, null);
    }

    private sealed class FakeChapterService : IChapterService
    {
        private readonly ChapterResponseDto? _chapter;
        public FakeChapterService(ChapterResponseDto? chapter) => _chapter = chapter;

        public ChapterResponseDto Create(CreateChapterRequestDto request) => throw new NotImplementedException();
        public PagedResultDto<ChapterListItemDto> GetAll(ChapterQueryDto query) => throw new NotImplementedException();
        public ChapterResponseDto? GetById(Guid id) => _chapter;
        public IEnumerable<ChapterListItemDto> GetByStoryId(Guid storyId) => throw new NotImplementedException();
        public ChapterResponseDto? GetByStoryIdAndOrderIndex(Guid storyId, int orderIndex) => throw new NotImplementedException();
        public bool Update(Guid id, UpdateChapterRequestDto request) => throw new NotImplementedException();
        public bool Delete(Guid id) => throw new NotImplementedException();
        public bool Publish(Guid id) => throw new NotImplementedException();
        public bool Unpublish(Guid id) => throw new NotImplementedException();
        public bool Reorder(Guid id, int newOrderIndex) => throw new NotImplementedException();
        public (string? reason, DateTime? rejectedAt) GetLatestRejectionForChapter(Guid chapterId) => (null, null);
    }

    private sealed class FakeChapterVersionService : IChapterVersionService
    {
        public IReadOnlyList<ChapterVersionListItemDto> GetByChapterId(Guid chapterId) => Array.Empty<ChapterVersionListItemDto>();
        public ChapterVersionDetailDto? GetById(Guid id) => null;
        public ChapterVersionDetailDto? Create(Guid chapterId, Guid authorId, CreateChapterVersionRequestDto request) => null;
        public bool Update(Guid id, Guid authorId, UpdateChapterVersionRequestDto request) => false;
        public bool Delete(Guid id, Guid authorId) => false;
        public bool SubmitForReview(Guid versionId, Guid authorId) => false;
        public bool CancelSubmit(Guid versionId, Guid authorId) => false;
    }

    private sealed class FakeServiceScopeFactory : IServiceScopeFactory
    {
        public IServiceScope CreateScope() => new FakeServiceScope();

        private sealed class FakeServiceScope : IServiceScope
        {
            public IServiceProvider ServiceProvider { get; } = new FakeServiceProvider();
            public void Dispose() { }
        }

        private sealed class FakeServiceProvider : IServiceProvider
        {
            public object? GetService(Type serviceType) => null;
        }
    }
}

