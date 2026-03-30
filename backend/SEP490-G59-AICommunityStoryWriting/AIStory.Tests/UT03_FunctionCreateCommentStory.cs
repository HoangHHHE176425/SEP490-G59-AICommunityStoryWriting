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

        /// <summary>
        /// UTCID03 — spec: truyện/user/đã đọc chapter là tiền đề nghiệp vụ; input <c>content</c> null, <c>ParentId</c> null.
        /// Product: <see cref="StoriesController.AddStoryComment"/> từ chối sớm (<c>string.IsNullOrWhiteSpace(request.Content)</c>) → BadRequest,
        /// không tạo comment (không gọi <c>IStoryCommentPostService.AddAsync</c>), không gọi guardrail.
        /// Không assert đúng từng chữ message (spec có thể khác text thực tế).
        /// </summary>
        [Fact]
        public async Task UTCID03_CreateStoryComment_Rejects_WhenContentIsNull()
        {
            LogUtcContext("UTCID03",
                "Spec: nội dung bình luận null → hệ thống từ chối, không lưu comment.",
                "Precondition (nghiệp vụ): story/user/đã đọc chapter — API kiểm tra content trước khi vào service.",
                "Input: StoryId bất kỳ (route); Content null; ParentId null.",
                "Kỳ vọng: HTTP 400 BadRequest; AddAsync không được gọi.");

            var userId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                authenticationType: "Test");
            var controller = CreateControllerSut(out var guardrailMock, out var commentPostMock);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.AddStoryComment(
                storyId,
                new CreateStoryCommentRequestDto { Content = null!, ParentId = null });

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            commentPostMock.Verify(
                x => x.AddAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
                Times.Never);
            guardrailMock.Verify(
                x => x.CheckCommentBannedWordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// UTCID04 — spec: StoryId hợp lệ, content hợp lệ, ParentId null; UserId không tồn tại trong hệ thống → Fail, không lưu;
        /// log kiểu &quot;User không tồn tại&quot; (không assert đúng từng chữ message).
        /// Mô phỏng &quot;user không tồn tại&quot;: <c>userId</c> là Guid bất kỳ; preconditions nghiệp vụ (đã đọc chapter) vẫn <c>true</c> qua mock —
        /// tương tự <see cref="UT01_FunctionCreateStory.UTCID14_CreateStory_Fails_WhenCallerIsNotRegisteredAuthor"/> (Exists = false).
        /// Product hiện tại: <see cref="StoryCommentPostService"/> không gọi <see cref="IUserLookup.Exists"/> trước <c>AddStoryComment</c>.
        /// Test assert theo spec (<see cref="StoryCommentPostStatus.Rejected"/>, <c>Dto</c> null, không persist); <b>hiện FAIL</b> (bug) cho đến khi product
        /// kiểm tra user tồn tại (ví dụ inject <see cref="IUserLookup"/>, <c>Exists(userId)==false</c> → <see cref="StoryCommentPostOutcome.BadRequest"/>).
        /// Sau khi fix, cập nhật <see cref="CreatePostServiceSut"/> để truyền mock <c>IUserLookup</c> nếu constructor service thêm dependency.
        /// </summary>
        [Fact]
        public async Task UTCID04_CreateStoryComment_Fails_WhenUserIdDoesNotExist()
        {
            LogUtcContext("UTCID04",
                "Spec: UserId không tồn tại → từ chối, không lưu (Fail / Data null).",
                "Precondition (mock): truyện PUBLISHED; HasReadAnyChapterOfStory(userId, storyId) = true.",
                "Input: StoryId hợp lệ; content hợp lệ; ParentId null; userId = GUID mô phỏng không có trong users.",
                "Kỳ vọng spec: Rejected; Dto null; AddStoryComment không được gọi.",
                "BUG nếu test FAIL: product chưa validate user tồn tại trước khi tạo comment (giống cách UT01 ghi bug UTCID05/09/12…).");

            var storyId = Guid.NewGuid();
            var unknownUserId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var slugSuffix = Guid.NewGuid().ToString("N")[..8];
            const string content = "abc spec root comment";

            var story = new stories
            {
                id = storyId,
                author_id = Guid.NewGuid(),
                title = "UT04 Story",
                slug = $"ut04-{slugSuffix}",
                status = "PUBLISHED",
                story_progress_status = "ONGOING",
                comments_disabled = false
            };

            var savedEntity = new comments
            {
                id = commentId,
                user_id = unknownUserId,
                story_id = storyId,
                chapter_id = null,
                parent_id = null,
                content = content,
                likes_count = 0,
                status = "APPROVED",
                created_at = DateTime.UtcNow,
                userNavigation = null
            };

            var sut = CreatePostServiceSut(
                out var storyLookup,
                out var userActivity,
                out var commentCmd,
                out var reactionReader,
                out _);

            storyLookup.Setup(x => x.GetById(storyId)).Returns(story);
            userActivity.Setup(x => x.HasReadAnyChapterOfStory(unknownUserId, storyId)).Returns(true);
            commentCmd
                .Setup(x => x.AddStoryComment(storyId, unknownUserId, content, null))
                .Returns(savedEntity);
            reactionReader
                .Setup(x => x.GetSummary(commentId, unknownUserId))
                .Returns((false, (string?)null, new Dictionary<string, int>()));

            var outcome = await sut.AddAsync(storyId, unknownUserId, content, null, default);

            Assert.Equal(StoryCommentPostStatus.Rejected, outcome.Status);
            Assert.Null(outcome.Dto);
            commentCmd.Verify(x => x.AddStoryComment(storyId, unknownUserId, content, null), Times.Never);
        }

        /// <summary>
        /// UTCID05 — spec: tạo comment khi StoryId không trỏ tới truyện có trong hệ thống (ma trận có thể ghi StoryId null — ý &quot;không xác định truyện&quot;);
        /// pre: user hợp lệ, đã đọc chapter (mock không cần gọi vì dừng sớm); input content hợp lệ, ParentId null.
        /// API <see cref="StoriesController.AddStoryComment"/> dùng <c>{id:guid}</c> — không có <c>Guid?</c> null trên route; mô phỏng &quot;không tồn tại&quot; bằng
        /// <c>IStoryLookup.GetById(storyId) == null</c>.
        /// Product: <see cref="StoryCommentPostService.AddAsync"/> → <see cref="StoryCommentPostStatus.StoryNotFound"/>, không gọi <c>AddStoryComment</c>.
        /// Không assert đúng từng chữ message (spec có thể ghi &quot;Truyện không tồn tại&quot;).
        /// </summary>
        [Fact]
        public async Task UTCID05_CreateStoryComment_Fails_WhenStoryDoesNotExist()
        {
            LogUtcContext("UTCID05",
                "Spec: Story không tồn tại (hoặc không xác định) → dừng, không lưu comment.",
                "Precondition: userId hợp lệ; đã đọc chapter — service không tới bước đó khi story null.",
                "Input: storyId bất kỳ; GetById = null; content \"abc...\"; ParentId null.",
                "Kỳ vọng: StoryNotFound; Dto null; không AddStoryComment / không HasReadAnyChapter.");

            var missingStoryId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            const string content = "abc spec story missing";

            var sut = CreatePostServiceSut(
                out var storyLookup,
                out var userActivity,
                out var commentCmd,
                out var reactionReader,
                out _);

            storyLookup.Setup(x => x.GetById(missingStoryId)).Returns((stories?)null);

            var outcome = await sut.AddAsync(missingStoryId, userId, content, null, default);

            Assert.Equal(StoryCommentPostStatus.StoryNotFound, outcome.Status);
            Assert.Null(outcome.Dto);
            Assert.NotNull(outcome.Message);

            storyLookup.Verify(x => x.GetById(missingStoryId), Times.Once);
            userActivity.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
            commentCmd.Verify(x => x.AddStoryComment(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
            reactionReader.Verify(x => x.GetSummary(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
        }

        /// <summary>
        /// UTCID06 — spec: nội dung bình luận dài hơn 2000 ký tự → dừng, không lưu; log kiểu &quot;Nội dung quá dài&quot; (không assert đúng từng chữ).
        /// Precondition (nghiệp vụ): story/user/đã đọc chapter — API kiểm tra độ dài sau <c>Trim()</c> trước service/guardrail.
        /// Product: <see cref="StoriesController.AddStoryComment"/> — <c>content.Length &gt; 2000</c> → <c>BadRequest</c>, không gọi <c>AddAsync</c>, không gọi guardrail.
        /// </summary>
        [Fact]
        public async Task UTCID06_CreateStoryComment_Rejects_WhenContentExceeds2000Characters()
        {
            LogUtcContext("UTCID06",
                "Spec: content &gt; 2000 ký tự → từ chối, không lưu comment.",
                "Precondition: user đăng nhập (claim); story/chapter hợp lệ — không tới service vì chặn ở controller.",
                "Input: Content 2001 ký tự (không khoảng đầu/cuối để Trim không rút độ dài); ParentId null.",
                "Kỳ vọng: HTTP 400; AddAsync không được gọi; CheckCommentBannedWordsAsync không được gọi.");

            var userId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var longContent = new string('z', 2001);
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                authenticationType: "Test");
            var controller = CreateControllerSut(out var guardrailMock, out var commentPostMock);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.AddStoryComment(
                storyId,
                new CreateStoryCommentRequestDto { Content = longContent, ParentId = null });

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            commentPostMock.Verify(
                x => x.AddAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
                Times.Never);
            guardrailMock.Verify(
                x => x.CheckCommentBannedWordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// UTCID07 — spec: content chỉ gồm khoảng trắng → sau trim rỗng, không hợp lệ, không lưu; log kiểu &quot;Nội dung không hợp lệ&quot; (không assert đúng từng chữ).
        /// Precondition (nghiệp vụ): story/user/đã đọc chapter — API chặn trước service.
        /// Product: <see cref="StoriesController.AddStoryComment"/> — <c>string.IsNullOrWhiteSpace(request.Content)</c> bắt cả chuỗi chỉ whitespace (trước bước <c>Trim()</c>),
        /// → <c>BadRequest</c>, không gọi <c>AddAsync</c>, không gọi guardrail.
        /// </summary>
        [Fact]
        public async Task UTCID07_CreateStoryComment_Rejects_WhenContentIsWhitespaceOnly()
        {
            LogUtcContext("UTCID07",
                "Spec: content chỉ khoảng trắng → coi như không có nội dung, không lưu comment.",
                "Precondition: user đăng nhập; story/chapter hợp lệ — không tới service.",
                "Input: Content = spaces/tabs/newlines; ParentId null.",
                "Kỳ vọng: HTTP 400; AddAsync không được gọi; CheckCommentBannedWordsAsync không được gọi.");

            var userId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            const string whitespaceOnly = "   \t  \r\n  ";
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                authenticationType: "Test");
            var controller = CreateControllerSut(out var guardrailMock, out var commentPostMock);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.AddStoryComment(
                storyId,
                new CreateStoryCommentRequestDto { Content = whitespaceOnly, ParentId = null });

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            commentPostMock.Verify(
                x => x.AddAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
                Times.Never);
            guardrailMock.Verify(
                x => x.CheckCommentBannedWordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// UTCID08 — ma trận: cùng nghiệp vụ &quot;content chỉ khoảng trắng → sau trim rỗng, không hợp lệ&quot; như <see cref="UTCID07_CreateStoryComment_Rejects_WhenContentIsWhitespaceOnly"/>;
        /// dùng mẫu whitespace khác để ghi nhận đúng mã UTCID08. Fail / Data null; log kiểu &quot;Nội dung không hợp lệ&quot; (không assert đúng từng chữ).
        /// Product: <see cref="StoriesController.AddStoryComment"/> — <c>string.IsNullOrWhiteSpace(request.Content)</c> → <c>BadRequest</c>.
        /// </summary>
        [Fact]
        public async Task UTCID08_CreateStoryComment_Rejects_WhenContentIsWhitespaceOnly_Matrix08()
        {
            LogUtcContext("UTCID08",
                "Spec (ma trận UTCID08): content chỉ whitespace → không lưu comment; Data null.",
                "Cùng rule UTCID07; input: toàn space thường + NBSP (\\u00A0) — vẫn IsNullOrWhiteSpace.",
                "Precondition: user đăng nhập; ParentId null.",
                "Kỳ vọng: HTTP 400; AddAsync / guardrail không gọi.");

            var userId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var whitespaceOnly = new string(' ', 12) + "\u00A0" + "\t";
            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, userId.ToString()) },
                authenticationType: "Test");
            var controller = CreateControllerSut(out var guardrailMock, out var commentPostMock);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.AddStoryComment(
                storyId,
                new CreateStoryCommentRequestDto { Content = whitespaceOnly, ParentId = null });

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            commentPostMock.Verify(
                x => x.AddAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()),
                Times.Never);
            guardrailMock.Verify(
                x => x.CheckCommentBannedWordsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        /// <summary>
        /// UTCID09 — spec: user chưa đọc chapter nào của truyện → không cho comment, không lưu; log kiểu &quot;Chưa đọc nội dung nào&quot; (không assert đúng từng chữ).
        /// Precondition: story tồn tại, user tồn tại — mô phỏng qua story hợp lệ + <c>HasReadAnyChapterOfStory = false</c>.
        /// Product: <see cref="StoryCommentPostService.AddAsync"/> — <c>!HasReadAnyChapterOfStory</c> → <see cref="StoryCommentPostStatus.Rejected"/>, không <c>AddStoryComment</c>.
        /// </summary>
        [Fact]
        public async Task UTCID09_CreateStoryComment_Fails_WhenUserHasNotReadAnyChapter()
        {
            LogUtcContext("UTCID09",
                "Spec: chưa đọc chapter nào của truyện → dừng, không lưu comment.",
                "Precondition: story PUBLISHED, không comments_disabled; HasReadAnyChapterOfStory = false.",
                "Input: content hợp lệ; ParentId null.",
                "Kỳ vọng: Rejected; Dto null; không AddStoryComment.");

            var storyId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var slugSuffix = Guid.NewGuid().ToString("N")[..8];
            const string content = "abc spec no chapter read";

            var story = new stories
            {
                id = storyId,
                author_id = Guid.NewGuid(),
                title = "UT09 Story",
                slug = $"ut09-{slugSuffix}",
                status = "PUBLISHED",
                story_progress_status = "ONGOING",
                comments_disabled = false
            };

            var sut = CreatePostServiceSut(
                out var storyLookup,
                out var userActivity,
                out var commentCmd,
                out var reactionReader,
                out _);

            storyLookup.Setup(x => x.GetById(storyId)).Returns(story);
            userActivity.Setup(x => x.HasReadAnyChapterOfStory(userId, storyId)).Returns(false);

            var outcome = await sut.AddAsync(storyId, userId, content, null, default);

            Assert.Equal(StoryCommentPostStatus.Rejected, outcome.Status);
            Assert.Null(outcome.Dto);
            Assert.NotNull(outcome.Message);

            storyLookup.Verify(x => x.GetById(storyId), Times.Once);
            userActivity.Verify(x => x.HasReadAnyChapterOfStory(userId, storyId), Times.Once);
            commentCmd.Verify(x => x.AddStoryComment(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Never);
            reactionReader.Verify(x => x.GetSummary(It.IsAny<Guid>(), It.IsAny<Guid?>()), Times.Never);
        }
    }
}


//dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT03_FunctionCreateCommentStory"
