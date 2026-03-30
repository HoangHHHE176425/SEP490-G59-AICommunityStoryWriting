using AIStory.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Repositories;
using Services.Implementations;
using Services.Interfaces;
using System.Security.Claims;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT04_FunctionApproveChapter
    {
        private readonly ITestOutputHelper _output;

        public UT04_FunctionApproveChapter(ITestOutputHelper output) => _output = output;

        private void LogUtcContext(string utcId, string oneLineGoal, params string[] details)
        {
            _output.WriteLine("");
            _output.WriteLine($"======== {utcId} | UT04 ApproveChapter ========");
            _output.WriteLine(oneLineGoal);
            foreach (var line in details)
                _output.WriteLine("  · " + line);
        }

        private static ModeratorController CreateModeratorControllerSut(
            out Mock<IModerationService> moderationMock,
            out Mock<IChapterVersionService> chapterVersionMock,
            out Mock<IReviewEscalationService> escalationMock)
        {
            moderationMock = new Mock<IModerationService>(MockBehavior.Strict);
            chapterVersionMock = new Mock<IChapterVersionService>(MockBehavior.Loose);
            escalationMock = new Mock<IReviewEscalationService>(MockBehavior.Loose);

            return new ModeratorController(
                moderationMock.Object,
                chapterVersionMock.Object,
                escalationMock.Object,
                NullLogger<ModeratorController>.Instance);
        }

        /// <summary>Tạo <see cref="ModerationService"/> với dependency Moq Loose — nhánh category gate gọi thêm StoryDAO (static).</summary>
        private static ModerationService CreateModerationServiceSut(
            out Mock<IChapterRepository> chapterRepoMock,
            out Mock<IChapterVersionRepository> versionRepoMock)
        {
            var storyRepoMock = new Mock<IStoryRepository>(MockBehavior.Loose);
            chapterRepoMock = new Mock<IChapterRepository>(MockBehavior.Loose);
            versionRepoMock = new Mock<IChapterVersionRepository>(MockBehavior.Loose);
            var storyServiceMock = new Mock<IStoryService>(MockBehavior.Loose);
            var chapterServiceMock = new Mock<IChapterService>(MockBehavior.Loose);
            var scopeFactoryMock = new Mock<IServiceScopeFactory>(MockBehavior.Loose);

            return new ModerationService(
                storyRepoMock.Object,
                chapterRepoMock.Object,
                versionRepoMock.Object,
                storyServiceMock.Object,
                chapterServiceMock.Object,
                scopeFactoryMock.Object,
                NullLogger<ModerationService>.Instance);
        }

        /// <summary>
        /// UTCID01 — happy path: moderator duyệt chapter hợp lệ (chapter tồn tại, PENDING_REVIEW, đã claim), <c>allowedCategoryIds</c> null (ADMIN / full category).
        /// Ma trận: Return True, log &quot;Đã duyệt chương&quot; — không assert message.
        /// Product nghiệp vụ: <see cref="IModerationService.ApproveChapter"/> trong <c>ModerationService</c> (kiểm tra chapter, trạng thái, claim qua DAO, cập nhật PUBLISHED…).
        /// Unit test tầng API: <see cref="ModeratorController.ApproveChapter"/> gọi service với <c>allowedCategoryIds: null</c>; khi service trả <c>true</c> → <c>204 NoContent</c>.
        /// Lý do mock service: <c>ApproveChapter</c> gọi <c>ReviewAssignmentDAO</c> / <c>StoryDAO</c> tĩnh — không tách được trong unit test không DB.
        /// </summary>
        [Fact]
        public async Task UTCID01_ApproveChapter_Succeeds_WhenServiceReturnsTrue_HappyPath()
        {
            LogUtcContext("UTCID01",
                "Spec: điều kiện đủ (chapter PENDING_REVIEW, moderator đã claim) → duyệt thành công.",
                "API: POST approve — moderatorId từ JWT; allowedCategoryIds null như ModeratorController.",
                "Kỳ vọng: NoContent (204); IModerationService.ApproveChapter(id, modId, null) được gọi đúng 1 lần.",
                "Không assert log từng chữ.");

            var chapterId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var controller = CreateModeratorControllerSut(out var moderationMock, out _, out _);

            moderationMock
                .Setup(s => s.ApproveChapter(chapterId, moderatorId, null))
                .Returns(true);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, moderatorId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ApproveChapter(chapterId);

            Assert.IsType<NoContentResult>(result);
            moderationMock.Verify(s => s.ApproveChapter(chapterId, moderatorId, null), Times.Once);
        }

        /// <summary>
        /// UTCID02 — spec: không có moderator thực hiện duyệt (<c>moderatorId</c> null) → dừng, chapter không được duyệt; ma trận có thể ghi <c>false</c> / <see cref="InvalidOperationException"/> / log &quot;Không có người duyệt&quot;.
        /// Product API: <see cref="ModeratorController.ApproveChapter"/> — <see cref="ModeratorController.GetCurrentUserId"/> null → <c>Unauthorized</c> (401), <b>không</b> gọi <see cref="IModerationService.ApproveChapter"/>.
        /// Service nhận <c>Guid moderatorId</c> (không nullable) — &quot;null&quot; chỉ xảy ra ở tầng HTTP/JWT. Không assert đúng từng chữ message.
        /// </summary>
        [Fact]
        public async Task UTCID02_ApproveChapter_Rejects_WhenModeratorIdCannotBeResolved()
        {
            LogUtcContext("UTCID02",
                "Spec: không có moderator (moderatorId null) → không duyệt.",
                "Product: User không có Sub/NameIdentifier hợp lệ → Unauthorized; service không chạy.",
                "Không assert message từng chữ.");

            var chapterId = Guid.NewGuid();
            var controller = CreateModeratorControllerSut(out var moderationMock, out _, out _);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            var result = await controller.ApproveChapter(chapterId);

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
            moderationMock.Verify(
                s => s.ApproveChapter(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<IReadOnlyList<Guid>?>()),
                Times.Never);
        }

        /// <summary>
        /// UTCID03 — spec: moderator hợp lệ nhưng không phải người đã claim chapter → không duyệt; ma trận: <c>false</c> / <see cref="InvalidOperationException"/> / log &quot;Bạn không có quyền…&quot;.
        /// Product: <c>ModerationService.ApproveChapter</c> → <c>EnsureModeratorHasClaimedForReview</c>: nếu <c>!ReviewAssignmentDAO.IsAssignedTo(…, moderatorId)</c> →
        /// <see cref="InvalidOperationException"/> (message hiện tại: chỉ moderator đã nhận duyệt mới duyệt/từ chối được).
        /// <see cref="ModeratorController.ApproveChapter"/> bắt exception → <c>400 BadRequest</c> với <c>ex.Message</c> — không trả <c>false</c> qua HTTP body dạng bool.
        /// Test mô phỏng throw từ <see cref="IModerationService"/> (cùng kiểu exception product) để xác nhận mapping API; không assert đúng từng chữ so với ma trận.
        /// </summary>
        [Fact]
        public async Task UTCID03_ApproveChapter_ReturnsBadRequest_WhenServiceThrowsNotAssignee()
        {
            LogUtcContext("UTCID03",
                "Spec: moderator không phải người claim → dừng, không duyệt.",
                "Product service: InvalidOperationException khi không IsAssignedTo; API: BadRequest.",
                "Mock service ném exception tương đương — không assert message từng chữ.");

            var chapterId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var controller = CreateModeratorControllerSut(out var moderationMock, out _, out _);

            moderationMock
                .Setup(s => s.ApproveChapter(chapterId, moderatorId, null))
                .Throws(new InvalidOperationException(
                    "Chỉ moderator đã nhận duyệt mới có thể duyệt hoặc từ chối mục này."));

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, moderatorId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ApproveChapter(chapterId);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            moderationMock.Verify(s => s.ApproveChapter(chapterId, moderatorId, null), Times.Once);
        }

        /// <summary>
        /// UTCID04 — spec: chapter không ở trạng thái chờ duyệt (không PENDING_REVIEW và không có version PENDING_REVIEW) → không duyệt; Return <c>false</c>; log &quot;Chương không trong trạng thái chờ duyệt&quot; (không assert đúng từng chữ).
        /// Product: <c>ModerationService.ApproveChapter</c> khi <c>!canApprove</c> → <c>return false</c> (không đổi DB).
        /// <see cref="ModeratorController.ApproveChapter"/>: <c>!ok</c> → <c>404 NotFound</c> với message gộp &quot;không tồn tại hoặc không ở trạng thái chờ duyệt&quot; — không phân tách riêng chỉ &quot;trạng thái&quot;.
        /// Test: mock service trả <c>false</c> (mô phỏng nhánh từ chối vì trạng thái) → xác nhận mapping HTTP.
        /// </summary>
        [Fact]
        public async Task UTCID04_ApproveChapter_ReturnsNotFound_WhenServiceReturnsFalse_NotPendingReview()
        {
            LogUtcContext("UTCID04",
                "Spec: chapter không PENDING_REVIEW (và không có version chờ duyệt) → không duyệt, false.",
                "Product API: service false → NotFound (404). Không assert message từng chữ.");

            var chapterId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var controller = CreateModeratorControllerSut(out var moderationMock, out _, out _);

            moderationMock
                .Setup(s => s.ApproveChapter(chapterId, moderatorId, null))
                .Returns(false);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, moderatorId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ApproveChapter(chapterId);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
            Assert.NotNull(notFound.Value);
            moderationMock.Verify(s => s.ApproveChapter(chapterId, moderatorId, null), Times.Once);
        }

        /// <summary>
        /// UTCID05 — spec: <c>chapterId</c> không tồn tại → không duyệt, không đổi DB; Return <c>false</c>; log &quot;Chương không tồn tại&quot; (không assert đúng từng chữ).
        /// Product: <c>ModerationService.ApproveChapter</c> — <c>_chapterRepository.GetById(chapterId)</c> null → <c>return false</c>.
        /// <see cref="ModeratorController.ApproveChapter"/>: cùng nhánh <c>!ok</c> với UTCID04 → <c>404 NotFound</c>, message API gộp &quot;không tồn tại hoặc không ở trạng thái chờ duyệt&quot; (không tách riêng chỉ &quot;không tồn tại&quot;).
        /// Test: mock service <c>false</c> (mô phỏng chapter missing) → xác nhận mapping HTTP.
        /// </summary>
        [Fact]
        public async Task UTCID05_ApproveChapter_ReturnsNotFound_WhenServiceReturnsFalse_ChapterNotFound()
        {
            LogUtcContext("UTCID05",
                "Spec: chapterId không có trong DB → dừng, false, không persist.",
                "Product API: service false → NotFound (404). Không assert message từng chữ.");

            var missingChapterId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var controller = CreateModeratorControllerSut(out var moderationMock, out _, out _);

            moderationMock
                .Setup(s => s.ApproveChapter(missingChapterId, moderatorId, null))
                .Returns(false);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, moderatorId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ApproveChapter(missingChapterId);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
            Assert.NotNull(notFound.Value);
            moderationMock.Verify(s => s.ApproveChapter(missingChapterId, moderatorId, null), Times.Once);
        }

        /// <summary>
        /// UTCID06 — spec: <c>allowedCategoryIds</c> rỗng <c>{}</c> → cấu hình không hợp lệ, không duyệt; Return <c>false</c>; log &quot;cấu hình không hợp lệ&quot; (không assert đúng từng chữ).
        /// Product: <see cref="ModerationService.ApproveChapter"/> đầu hàm — <c>allowedCategoryIds != null &amp;&amp; Count == 0</c> → <c>return false</c> (không gọi repository).
        /// <see cref="ModeratorController.ApproveChapter"/> luôn truyền <c>allowedCategoryIds: null</c> — endpoint HTTP hiện không gửi list rỗng; test gọi trực tiếp service để xác nhận validate.
        /// </summary>
        [Fact]
        public void UTCID06_ApproveChapter_ReturnsFalse_WhenAllowedCategoryIdsIsEmpty()
        {
            LogUtcContext("UTCID06",
                "Spec: allowedCategoryIds rỗng → không duyệt, false.",
                "Product: ModerationService chặn trước GetById. Không assert message từng chữ.");

            var sut = CreateModerationServiceSut(out var chapterRepoMock, out _);
            var chapterId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            IReadOnlyList<Guid> emptyAllowed = Array.Empty<Guid>();

            var ok = sut.ApproveChapter(chapterId, moderatorId, emptyAllowed);

            Assert.False(ok);
            chapterRepoMock.Verify(r => r.GetById(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// UTCID07 — spec: <c>allowedCategoryIds</c> chứa category không hợp lệ / không gắn với truyện → không duyệt; Return <c>false</c>; log &quot;Category không tồn tại&quot; (không assert đúng từng chữ).
        /// Product: <see cref="ModerationService.ApproveChapter"/> khi <c>allowedCategoryIds</c> non-empty và <c>chapter.story_id</c> có giá trị → <c>StoryDAO.GetById</c>;
        /// nếu <c>story == null</c> hoặc <c>!story.category.Any(c =&gt; allowedCategoryIds.Contains(c.id))</c> → <c>return false</c> (trước claim).
        /// <see cref="ModeratorController.ApproveChapter"/> luôn truyền <c>allowedCategoryIds: null</c> — không gọi được gate category qua HTTP hiện tại.
        /// Test (không DB): mock <see cref="IModerationService"/> trả <c>false</c> như kết quả sau gate — xác nhận API vẫn <c>404 NotFound</c> (cùng mapping với mọi lý do <c>false</c> khác).
        /// </summary>
        [Fact]
        public async Task UTCID07_ApproveChapter_ReturnsNotFound_WhenServiceReturnsFalse_CategoryGateFailure()
        {
            LogUtcContext("UTCID07",
                "Spec: category không hợp lệ / không thuộc truyện → service false, không duyệt.",
                "Product nghiệp vụ: ModerationService + StoryDAO (cần DB). Test: mock false → 404. Không assert message từng chữ.");

            var chapterId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var controller = CreateModeratorControllerSut(out var moderationMock, out _, out _);

            moderationMock
                .Setup(s => s.ApproveChapter(chapterId, moderatorId, null))
                .Returns(false);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, moderatorId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ApproveChapter(chapterId);

            var notFound = Assert.IsType<NotFoundObjectResult>(result);
            Assert.Equal(StatusCodes.Status404NotFound, notFound.StatusCode);
            Assert.NotNull(notFound.Value);
            moderationMock.Verify(s => s.ApproveChapter(chapterId, moderatorId, null), Times.Once);
        }

        /// <summary>
        /// UTCID08 — spec: chương trước chưa PUBLISHED → không duyệt chương hiện tại (đúng thứ tự 0→1→2…); ma trận: <c>false</c> / <see cref="InvalidOperationException"/> / log &quot;Duyệt phải đúng thứ tự&quot; (không assert đúng từng chữ).
        /// Product: <see cref="ModerationService.ApproveChapter"/> — lần publish đầu (<c>!published_at</c>) và <c>order_index &gt; 0</c>: chương <c>order_index - 1</c> phải tồn tại và <c>PUBLISHED</c>, nếu không → <see cref="InvalidOperationException"/>.
        /// <see cref="ModeratorController.ApproveChapter"/> bắt → <c>400 BadRequest</c> với <c>ex.Message</c> (không trả <c>false</c> bool).
        /// Test: mock service ném exception cùng dạng message product — xác nhận mapping HTTP; không gọi DB.
        /// </summary>
        [Fact]
        public async Task UTCID08_ApproveChapter_ReturnsBadRequest_WhenServiceThrowsOutOfOrderPublish()
        {
            LogUtcContext("UTCID08",
                "Spec: chương trước chưa published → không duyệt (đúng thứ tự).",
                "Product: InvalidOperationException; API BadRequest. Mock tương đương — không assert message từng chữ.");

            var chapterId = Guid.NewGuid();
            var moderatorId = Guid.NewGuid();
            var controller = CreateModeratorControllerSut(out var moderationMock, out _, out _);

            moderationMock
                .Setup(s => s.ApproveChapter(chapterId, moderatorId, null))
                .Throws(new InvalidOperationException(
                    "Phải duyệt chương theo thứ tự. Cần duyệt chương có thứ tự 1 trước khi duyệt chương 2."));

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, moderatorId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ApproveChapter(chapterId);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            moderationMock.Verify(s => s.ApproveChapter(chapterId, moderatorId, null), Times.Once);
        }
    }
}

//dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT04_FunctionApproveChapter"
