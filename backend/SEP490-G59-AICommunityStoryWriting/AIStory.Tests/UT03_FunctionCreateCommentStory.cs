using AIStory.API.Controllers;
using BusinessObjects.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Services.DTOs.AI;
using Services.DTOs.Comments;
using Services.Implementations;
using Services.Interfaces;
using System.Security.Claims;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT03_FunctionCreateCommentStory
    {
        private readonly ITestOutputHelper _output;

        public UT03_FunctionCreateCommentStory(ITestOutputHelper output) => _output = output;

        private void LogUtcContext(string utcId, string oneLineGoal, params string[] details)
        {
            _output.WriteLine("");
            _output.WriteLine($"======== {utcId} | UT03 CreateCommentStory ========");
            _output.WriteLine(oneLineGoal);
            foreach (var line in details)
                _output.WriteLine("  · " + line);
        }

        /// <summary>Giống <see cref="UT01_FunctionCreateStory.CreateSut"/> — unit test nghiệp vụ qua service + Moq lookup/command.</summary>
        private static StoryCommentPostService CreatePostServiceSut(
            out Mock<IStoryLookup> storyLookupMock,
            out Mock<IUserActivityLookup> userActivityMock,
            out Mock<IStoryCommentCommand> commentCommandMock,
            out Mock<ICommentReactionReader> reactionReaderMock,
            out Mock<INotificationHubNotifier> notifierMock)
        {
            storyLookupMock = new Mock<IStoryLookup>(MockBehavior.Strict);
            userActivityMock = new Mock<IUserActivityLookup>(MockBehavior.Strict);
            commentCommandMock = new Mock<IStoryCommentCommand>(MockBehavior.Strict);
            reactionReaderMock = new Mock<ICommentReactionReader>(MockBehavior.Strict);
            notifierMock = new Mock<INotificationHubNotifier>(MockBehavior.Loose);

            return new StoryCommentPostService(
                storyLookupMock.Object,
                userActivityMock.Object,
                commentCommandMock.Object,
                reactionReaderMock.Object,
                notifierMock.Object,
                NullLogger<StoryCommentPostService>.Instance);
        }

        private static StoriesController CreateControllerSut(
            out Mock<IContentGuardrailService> guardrailMock,
            out Mock<IStoryCommentPostService> commentPostMock)
        {
            var storyServiceMock = new Mock<IStoryService>(MockBehavior.Loose);
            var reportMock = new Mock<IStoryReportService>(MockBehavior.Loose);
            var notifMock = new Mock<INotificationHubNotifier>(MockBehavior.Loose);
            guardrailMock = new Mock<IContentGuardrailService>(MockBehavior.Strict);
            commentPostMock = new Mock<IStoryCommentPostService>(MockBehavior.Strict);

            guardrailMock
                .Setup(x => x.CheckCommentBannedWordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new GuardrailResult { Passed = true, Violations = new List<GuardrailViolation>() });

            return new StoriesController(
                storyServiceMock.Object,
                guardrailMock.Object,
                reportMock.Object,
                notifMock.Object,
                commentPostMock.Object,
                NullLogger<StoriesController>.Instance);
        }

        /// <summary>
        /// UTCID01 — happy path comment gốc: truyện PUBLISHED, đã đọc chapter, ParentId null, nội dung đã trim (service nhận chuỗi đã trim).
        /// Ma trận có thể ghi "Đã đăng bình luận" — không assert message. Test <see cref="StoryCommentPostService"/> + Moq (không DB).
        /// </summary>
        [Fact]
        public async Task UTCID01_CreateStoryComment_Succeeds_ForRootComment_WhenPreconditionsMet()
        {
            LogUtcContext("UTCID01",
                "Happy path: IStoryLookup + IUserActivityLookup + IStoryCommentCommand (mock) → Success + DTO.",
                "Precondition: story PUBLISHED, không comments_disabled; HasReadAnyChapterOfStory = true.",
                "Input: content đã trim; ParentId null.",
                "Kỳ vọng: StoryCommentPostStatus.Success; DTO.Content đúng; ParentId null. Không assert log từng chữ.");

            var storyId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var slugSuffix = Guid.NewGuid().ToString("N")[..8];

            var story = new stories
            {
                id = storyId,
                author_id = userId,
                title = "UT03 Story",
                slug = $"ut03-{slugSuffix}",
                status = "PUBLISHED",
                story_progress_status = "ONGOING",
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
                out var userActivity,
                out var commentCmd,
                out var reactionReader,
                out _);

            storyLookup.Setup(x => x.GetById(storyId)).Returns(story);
            userActivity.Setup(x => x.HasReadAnyChapterOfStory(userId, storyId)).Returns(true);
            commentCmd
                .Setup(x => x.AddStoryComment(storyId, userId, "abc happy path comment", null))
                .Returns(savedEntity);
            reactionReader
                .Setup(x => x.GetSummary(commentId, userId))
                .Returns((false, (string?)null, new Dictionary<string, int>()));

            var outcome = await sut.AddAsync(storyId, userId, "abc happy path comment", null, default);

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

        /// <summary>
        /// UTCID02 — spec: thao tác comment yêu cầu người gọi đã xác định được user id từ ngữ cảnh đăng nhập;
        /// nếu không lấy được id hợp lệ thì từ chối ngay, không chạy nghiệp vụ thêm comment.
        /// Product: <see cref="StoriesController.AddStoryComment"/> — <c>GetCurrentUserId()</c> null → <c>Unauthorized</c> (401), không gọi <c>AddAsync</c>.
        /// Không assert đúng từng chữ message.
        /// </summary>
        [Fact]
        public async Task UTCID02_CreateStoryComment_Rejects_WhenCallerUserIdCannotBeResolved()
        {
            LogUtcContext("UTCID02",
                "Spec: không resolve được user id từ context đăng nhập → từ chối trước khi post comment.",
                "Product: User không có claim Sub/NameIdentifier hợp lệ → Unauthorized; IStoryCommentPostService.AddAsync không được gọi.");

            var controller = CreateControllerSut(out _, out var commentPostMock);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            var result = await controller.AddStoryComment(Guid.NewGuid(), new CreateStoryCommentRequestDto { Content = "hello" });

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
            commentPostMock.Verify(
                x => x.AddAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }
    }
}


//dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT03_FunctionCreateCommentStory"
