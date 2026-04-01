using AIStory.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Services.DTOs.StoryReports;
using Services.Implementations;
using Services.Interfaces;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    /// <summary>
    /// Đối chiếu ma trận / product (CreateStoryReport):
    /// <list type="bullet">
    /// <item><description>UTCID01, 02, 04, 05, 07, 08, 09, 10 — product có logic tương ứng (hoặc test mock mapping HTTP/exception).</description></item>
    /// <item><description>UTCID03 — reporter không xác định (JWT) → 401; <c>Guid.Empty</c> hoặc user không trong DB → <see cref="StoryReportService.CreateStoryReportAsync"/> ném <see cref="InvalidOperationException"/> (<c>USER không tồn tại.</c>), không lưu — xem <see cref="UT05_FunctionCreateStoryReport.UTCID03_CreateStoryReport_Rejects_WhenReporterInvalidOrUserNotInDatabase"/>.</description></item>
    /// <item><description>UTCID06 — Description &gt; 200 ký tự → không hợp lệ: <see cref="CreateStoryReportRequestDto"/> + <see cref="StoryReportService.CreateStoryReportAsync"/>; test <see cref="UT05_FunctionCreateStoryReport.UTCID06_CreateStoryReport_Rejects_WhenDescriptionExceeds200Characters"/>.</description></item>
    /// </list>
    /// </summary>
    public class UT05_FunctionCreateStoryReport
    {
        private readonly ITestOutputHelper _output;

        public UT05_FunctionCreateStoryReport(ITestOutputHelper output) => _output = output;

        private void LogUtcContext(string utcId, string oneLineGoal, params string[] details)
        {
            _output.WriteLine("");
            _output.WriteLine($"======== {utcId} | UT05 CreateStoryReport ========");
            _output.WriteLine(oneLineGoal);
            foreach (var line in details)
                _output.WriteLine("  · " + line);
        }

        /// <summary>Chỉ ép chặt <see cref="IStoryReportService"/>; các dependency khác không dùng trong <c>ReportStory</c>.</summary>
        private static StoriesController CreateStoriesControllerSut(out Mock<IStoryReportService> reportMock)
        {
            var storyServiceMock = new Mock<IStoryService>(MockBehavior.Loose);
            var guardrailMock = new Mock<IContentGuardrailService>(MockBehavior.Loose);
            reportMock = new Mock<IStoryReportService>(MockBehavior.Strict);
            var notifMock = new Mock<INotificationHubNotifier>(MockBehavior.Loose);
            var commentPostMock = new Mock<IStoryCommentPostService>(MockBehavior.Loose);

            return new StoriesController(
                storyServiceMock.Object,
                guardrailMock.Object,
                reportMock.Object,
                notifMock.Object,
                commentPostMock.Object,
                NullLogger<StoriesController>.Instance);
        }

        /// <summary>
        /// UTCID01 — happy path: story tồn tại, user/reporter hợp lệ, chưa từng báo cáo (service trả id khác Empty), ReasonCode hợp lệ, mô tả &lt; 200 ký tự.
        /// Ma trận: Return True, log &quot;Tạo báo cáo thành công&quot; — product API trả <c>200 OK</c> với <c>id</c> và message &quot;Đã gửi báo cáo.&quot;; không assert đúng từng chữ log/message.
        /// Product nghiệp vụ: <see cref="Services.Implementations.StoryReportService.CreateStoryReportAsync"/> — <c>StoryReportReasonCatalog.TryGet</c>, <c>IUserLookup.Exists(reporterId)</c>, <c>StoryDAO.GetById</c>, trạng thái PUBLISHED, không tự báo cáo chính mình, <c>StoryReportDAO.AppendStoryReportAggregated</c> (trùng user+story → <c>Guid.Empty</c> → controller <c>409</c>).
        /// Unit test tầng API: <see cref="StoriesController.ReportStory"/> + mock service trả <c>Guid</c> khác Empty → <c>Ok</c>; không DB.
        /// </summary>
        [Fact]
        public async Task UTCID01_CreateStoryReport_Succeeds_WhenPreconditionsMet_HappyPath()
        {
            LogUtcContext("UTCID01",
                "Spec: đủ điều kiện → tạo báo cáo thành công, persist (ma trận: Return True).",
                "API: POST reports — reporterId từ JWT; body ReasonCode + Description.",
                "Kỳ vọng: 200 OK, body có id báo cáo; CreateStoryReportAsync gọi đúng 1 lần với cùng request.",
                "Không assert log/message từng chữ so với ma trận.");

            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var returnedReportId = Guid.NewGuid();
            var controller = CreateStoriesControllerSut(out var reportMock);

            var request = new CreateStoryReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = new string('a', 120)
            };

            reportMock
                .Setup(s => s.CreateStoryReportAsync(storyId, reporterId, request))
                .ReturnsAsync(returnedReportId);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStory(storyId, request);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
            Assert.NotNull(ok.Value);
            var idProp = ok.Value.GetType().GetProperty("id", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(idProp);
            Assert.Equal(returnedReportId, idProp.GetValue(ok.Value));
            reportMock.Verify(s => s.CreateStoryReportAsync(storyId, reporterId, request), Times.Once);
        }

        /// <summary>
        /// UTCID02 — spec: <c>storyId</c> không tồn tại → không tạo báo cáo, không persist; ma trận Return <c>false</c>, log &quot;Không tìm thấy truyện&quot; (không assert đúng từng chữ).
        /// Product: <see cref="Services.Implementations.StoryReportService.CreateStoryReportAsync"/> — <c>StoryDAO.GetById</c> null → <see cref="InvalidOperationException"/> (message hiện tại: <c>Story not found.</c>).
        /// <see cref="StoriesController.ReportStory"/> bắt <c>InvalidOperationException</c> → <c>400 BadRequest</c> với <c>ex.Message</c> (không trả bool <c>false</c>).
        /// Test: mock service ném exception cùng kiểu — xác nhận mapping API; không DB.
        /// </summary>
        [Fact]
        public async Task UTCID02_CreateStoryReport_ReturnsBadRequest_WhenStoryNotFound()
        {
            LogUtcContext("UTCID02",
                "Spec: storyId không có trong DB → dừng, không lưu báo cáo (ma trận: false).",
                "Product service: InvalidOperationException khi story null; API: BadRequest.",
                "Mock tương đương — không assert message từng chữ so với ma trận.");

            var missingStoryId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var controller = CreateStoriesControllerSut(out var reportMock);

            var request = new CreateStoryReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = new string('b', 80)
            };

            reportMock
                .Setup(s => s.CreateStoryReportAsync(missingStoryId, reporterId, request))
                .ThrowsAsync(new InvalidOperationException("Story not found."));

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStory(missingStoryId, request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            reportMock.Verify(s => s.CreateStoryReportAsync(missingStoryId, reporterId, request), Times.Once);
        }

        /// <summary>
        /// UTCID03 — ma trận: không xác định được người báo cáo <b>hoặc</b> user không tồn tại → không tạo / không lưu báo cáo; message kiểu &quot;USER không tồn tại&quot;.
        /// (A) API: không parse được user từ JWT → <c>401</c>, không gọi <see cref="IStoryReportService.CreateStoryReportAsync"/>.
        /// (B) Service: <see cref="StoryReportService.CreateStoryReportAsync"/> với <c>IUserLookup.Exists(reporterId)==false</c> → <see cref="InvalidOperationException"/> trước <c>StoryDAO.GetById</c> (không persist).
        /// (C) Service: <c>reporterId == Guid.Empty</c> → cùng exception; không gọi <c>Exists</c>.
        /// </summary>
        [Fact]
        public async Task UTCID03_CreateStoryReport_Rejects_WhenReporterInvalidOrUserNotInDatabase()
        {
            LogUtcContext("UTCID03 — reporter không hợp lệ / user không tồn tại",
                "Spec: không tạo báo cáo; API 401 khi thiếu identity; service từ chối khi Empty hoặc !Exists.",
                "Không assert log từng chữ so với ma trận.");

            var storyId = Guid.NewGuid();
            var controller = CreateStoriesControllerSut(out var reportMock);

            var request = new CreateStoryReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = new string('c', 50)
            };

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            var result = await controller.ReportStory(storyId, request);

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
            reportMock.Verify(
                s => s.CreateStoryReportAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateStoryReportRequestDto>()),
                Times.Never);

            var reporterId = Guid.NewGuid();
            var userLookup = new Mock<IUserLookup>(MockBehavior.Strict);
            userLookup.Setup(x => x.Exists(reporterId)).Returns(false);
            var sut = new StoryReportService(userLookup.Object, notificationHubNotifier: null);

            var ex = await Record.ExceptionAsync(() => sut.CreateStoryReportAsync(storyId, reporterId, request));
            var ioe = Assert.IsType<InvalidOperationException>(ex);
            Assert.Equal("USER không tồn tại.", ioe.Message);
            userLookup.Verify(x => x.Exists(reporterId), Times.Once);

            var userLookupEmpty = new Mock<IUserLookup>(MockBehavior.Strict);
            var sutEmpty = new StoryReportService(userLookupEmpty.Object, notificationHubNotifier: null);
            var exEmpty = await Record.ExceptionAsync(() => sutEmpty.CreateStoryReportAsync(storyId, Guid.Empty, request));
            var ioeEmpty = Assert.IsType<InvalidOperationException>(exEmpty);
            Assert.Equal("USER không tồn tại.", ioeEmpty.Message);
            userLookupEmpty.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// UTCID04 — spec: <c>ReasonCode</c> không tồn tại trong hệ thống → không tạo báo cáo; ma trận Return <c>false</c>, log &quot;Không tồn tại lý do phù hợp&quot; (không assert đúng từng chữ).
        /// Product: <see cref="Services.Implementations.StoryReportService.CreateStoryReportAsync"/> — <c>StoryReportReasonCatalog.TryGet</c> false → <see cref="ArgumentException"/> (message hiện tại: <c>Invalid reason code.</c>).
        /// <see cref="StoriesController.ReportStory"/> bắt <c>ArgumentException</c> → <c>400 BadRequest</c> với <c>ex.Message</c> (không trả bool <c>false</c>). Body có <c>ReasonCode</c> không rỗng mới vào service; rỗng thì controller trả BadRequest khác (&quot;ReasonCode is required.&quot;).
        /// Test: mock service ném <c>ArgumentException</c> tương đương — xác nhận mapping API.
        /// </summary>
        [Fact]
        public async Task UTCID04_CreateStoryReport_ReturnsBadRequest_WhenReasonCodeUnknown()
        {
            LogUtcContext("UTCID04",
                "Spec: ReasonCode không có trong catalog → dừng, không lưu (ma trận: false).",
                "Product service: ArgumentException; API: BadRequest. Mock tương đương.",
                "Không assert message từng chữ so với ma trận.");

            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var controller = CreateStoriesControllerSut(out var reportMock);

            var request = new CreateStoryReportRequestDto
            {
                ReasonCode = "NOT_A_REGISTERED_REASON_CODE",
                Description = new string('d', 40)
            };

            reportMock
                .Setup(s => s.CreateStoryReportAsync(storyId, reporterId, request))
                .ThrowsAsync(new ArgumentException("Invalid reason code."));

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStory(storyId, request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            reportMock.Verify(s => s.CreateStoryReportAsync(storyId, reporterId, request), Times.Once);
        }

        /// <summary>
        /// UTCID05 — spec: <c>ReasonCode</c> null → không tạo báo cáo; ma trận Return <c>false</c>, log &quot;Không tìm thấy lý do phù hợp&quot; (không assert đúng từng chữ).
        /// Product API: <see cref="StoriesController.ReportStory"/> — <c>request == null || string.IsNullOrWhiteSpace(request.ReasonCode)</c> → <c>400 BadRequest</c> (&quot;ReasonCode is required.&quot;), <b>không</b> gọi <see cref="IStoryReportService.CreateStoryReportAsync"/>.
        /// DTO <see cref="CreateStoryReportRequestDto"/> có thể nhận <c>null</c> sau deserialize JSON; service không chạy nên không tới <c>TryGet</c>.
        /// </summary>
        [Fact]
        public async Task UTCID05_CreateStoryReport_ReturnsBadRequest_WhenReasonCodeMissing()
        {
            LogUtcContext("UTCID05",
                "Spec: ReasonCode null → dừng, không lưu (ma trận: false).",
                "Product: controller chặn trước; BadRequest. Service không gọi.",
                "Không assert message từng chữ so với ma trận.");

            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var controller = CreateStoriesControllerSut(out var reportMock);

#pragma warning disable CS8625 // null literal — mô phỏng body JSON thiếu / null reasonCode
            var request = new CreateStoryReportRequestDto
            {
                ReasonCode = null!,
                Description = new string('e', 40)
            };
#pragma warning restore CS8625

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStory(storyId, request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            reportMock.Verify(
                s => s.CreateStoryReportAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CreateStoryReportRequestDto>()),
                Times.Never);
        }

        /// <summary>
        /// UTCID06 — ma trận: <c>Description</c> &gt; <b>200</b> ký tự → không hợp lệ, không lưu; message kiểu &quot;ký tự quá dài&quot;.
        /// Product: <see cref="CreateStoryReportRequestDto.Description"/> <c>[MaxLength(200)]</c> + <see cref="StoryReportService.CreateStoryReportAsync"/> ném <see cref="ArgumentException"/> nếu vượt quá.
        /// </summary>
        [Fact]
        public async Task UTCID06_CreateStoryReport_Rejects_WhenDescriptionExceeds200Characters()
        {
            LogUtcContext("UTCID06 — Description > 200 ký tự",
                "Kỳ vọng: DataAnnotations + service đều từ chối > 200 ký tự.",
                "Không assert đúng từng chữ message so với ma trận.");

            static bool TryValidate(CreateStoryReportRequestDto dto, out List<ValidationResult> results)
            {
                results = new List<ValidationResult>();
                var ctx = new ValidationContext(dto);
                return Validator.TryValidateObject(dto, ctx, results, validateAllProperties: true);
            }

            var over201 = new CreateStoryReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = new string('g', 201)
            };
            Assert.False(TryValidate(over201, out var errors201));
            Assert.Contains(errors201, e => e.MemberNames.Contains(nameof(CreateStoryReportRequestDto.Description)));

            var wayOver = new CreateStoryReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = new string('f', 4001)
            };
            Assert.False(TryValidate(wayOver, out var errorsLong));
            Assert.Contains(errorsLong, e => e.MemberNames.Contains(nameof(CreateStoryReportRequestDto.Description)));

            var userLookup = new Mock<IUserLookup>(MockBehavior.Strict);
            var sut = new StoryReportService(userLookup.Object, notificationHubNotifier: null);
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.CreateStoryReportAsync(Guid.NewGuid(), Guid.NewGuid(), over201));
            Assert.Contains("quá dài", ex.Message, StringComparison.OrdinalIgnoreCase);
            userLookup.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// UTCID07 — spec: <c>Description</c> null (không nhập mô tả) — không bắt buộc → vẫn tạo báo cáo thành công; ma trận Return True, log &quot;Tạo báo cáo thành công&quot; (không assert đúng từng chữ).
        /// Product: <see cref="CreateStoryReportRequestDto.Description"/> là <c>string?</c>, không <c>[Required]</c>; <see cref="Services.Implementations.StoryReportService.CreateStoryReportAsync"/> truyền <c>null</c> xuống DAO (trim/optional trong DAO).
        /// API: <see cref="StoriesController.ReportStory"/> + mock <see cref="IStoryReportService.CreateStoryReportAsync"/> trả id khác Empty → <c>200 OK</c> (&quot;Đã gửi báo cáo.&quot;).
        /// </summary>
        [Fact]
        public async Task UTCID07_CreateStoryReport_Succeeds_WhenDescriptionIsNull_OptionalField()
        {
            LogUtcContext("UTCID07",
                "Spec: Description null — không bắt buộc → tạo báo cáo OK (ma trận: Return True).",
                "API: POST reports với ReasonCode, không mô tả.",
                "Kỳ vọng: 200 OK + id; CreateStoryReportAsync gọi đúng 1 lần. Không assert log từng chữ.");

            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var returnedReportId = Guid.NewGuid();
            var controller = CreateStoriesControllerSut(out var reportMock);

            var request = new CreateStoryReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = null
            };

            reportMock
                .Setup(s => s.CreateStoryReportAsync(storyId, reporterId, request))
                .ReturnsAsync(returnedReportId);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStory(storyId, request);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
            Assert.NotNull(ok.Value);
            var idProp = ok.Value.GetType().GetProperty("id", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(idProp);
            Assert.Equal(returnedReportId, idProp.GetValue(ok.Value));
            reportMock.Verify(s => s.CreateStoryReportAsync(storyId, reporterId, request), Times.Once);
        }

        /// <summary>
        /// UTCID08 — spec: <c>Description</c> chỉ khoảng trắng — không bắt buộc → vẫn tạo báo cáo thành công; ma trận Return True, log &quot;Tạo báo cáo thành công&quot; (không assert đúng từng chữ).
        /// Product: <see cref="StoriesController.ReportStory"/> không kiểm tra <c>IsNullOrWhiteSpace</c> trên <c>Description</c> (chỉ <c>ReasonCode</c>). <see cref="Services.Implementations.StoryReportService.CreateStoryReportAsync"/> / DAO dùng <c>string.IsNullOrWhiteSpace</c> khi lưu → coi như không có mô tả chi tiết, vẫn ghi báo cáo.
        /// API: mock <see cref="IStoryReportService.CreateStoryReportAsync"/> trả id khác Empty → <c>200 OK</c>.
        /// </summary>
        [Fact]
        public async Task UTCID08_CreateStoryReport_Succeeds_WhenDescriptionIsWhitespaceOnly()
        {
            LogUtcContext("UTCID08",
                "Spec: Description chỉ whitespace — không bắt buộc → tạo báo cáo OK (ma trận: Return True).",
                "Product: controller không chặn; service/DAO trim/coalesce whitespace.",
                "Kỳ vọng: 200 OK + id; CreateStoryReportAsync gọi đúng 1 lần. Không assert log từng chữ.");

            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var returnedReportId = Guid.NewGuid();
            var controller = CreateStoriesControllerSut(out var reportMock);

            var request = new CreateStoryReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = "   \t  \n  "
            };

            reportMock
                .Setup(s => s.CreateStoryReportAsync(storyId, reporterId, request))
                .ReturnsAsync(returnedReportId);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStory(storyId, request);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
            Assert.NotNull(ok.Value);
            var idProp = ok.Value.GetType().GetProperty("id", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(idProp);
            Assert.Equal(returnedReportId, idProp.GetValue(ok.Value));
            reportMock.Verify(s => s.CreateStoryReportAsync(storyId, reporterId, request), Times.Once);
        }

        /// <summary>
        /// UTCID09 — spec: user đã báo cáo truyện này trước đó → không tạo thêm; ma trận Return <c>false</c>, log &quot;Bạn đã báo cáo truyện này rồi&quot; (không assert đúng từng chữ).
        /// Product: <see cref="DataAccessObjects.DAOs.StoryReportDAO.AppendStoryReportAggregated"/> — đã có dòng <c>story_report_contributors</c> cho (story, user) → <c>Guid.Empty</c>; <see cref="Services.Implementations.StoryReportService.CreateStoryReportAsync"/> trả <c>Empty</c>.
        /// <see cref="StoriesController.ReportStory"/>: <c>reportId == Guid.Empty</c> → <c>409 Conflict</c> (&quot;Bạn đã báo cáo truyện này trước đó.&quot;) — không trả bool <c>false</c>, không phải <c>400</c>.
        /// Test: mock <c>ReturnsAsync(Guid.Empty)</c> — xác nhận mapping API; không DB.
        /// </summary>
        [Fact]
        public async Task UTCID09_CreateStoryReport_ReturnsConflict_WhenUserAlreadyReportedStory()
        {
            LogUtcContext("UTCID09",
                "Spec: đã báo cáo truyện này rồi → không lưu thêm (ma trận: false).",
                "Product: service trả Guid.Empty → HTTP 409 Conflict.",
                "Không assert message từng chữ so với ma trận.");

            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var controller = CreateStoriesControllerSut(out var reportMock);

            var request = new CreateStoryReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = new string('h', 50)
            };

            reportMock
                .Setup(s => s.CreateStoryReportAsync(storyId, reporterId, request))
                .ReturnsAsync(Guid.Empty);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStory(storyId, request);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(StatusCodes.Status409Conflict, conflict.StatusCode);
            Assert.NotNull(conflict.Value);
            reportMock.Verify(s => s.CreateStoryReportAsync(storyId, reporterId, request), Times.Once);
        }

        /// <summary>
        /// UTCID10 — spec: truyện chưa <c>PUBLISHED</c> → không tạo báo cáo; ma trận Return <c>false</c>, log &quot;Truyện chưa được PUBLISH&quot; (không assert đúng từng chữ).
        /// Product: <see cref="Services.Implementations.StoryReportService.CreateStoryReportAsync"/> — sau <c>StoryDAO.GetById</c>, <c>status</c> khác <c>PUBLISHED</c> → <see cref="InvalidOperationException"/> (message hiện tại: <c>Chỉ có thể báo cáo truyện đã PUBLISHED.</c>).
        /// <see cref="StoriesController.ReportStory"/> bắt <c>InvalidOperationException</c> → <c>400 BadRequest</c> (không trả bool <c>false</c>).
        /// Test: mock service ném exception cùng kiểu — xác nhận mapping API; không DB.
        /// </summary>
        [Fact]
        public async Task UTCID10_CreateStoryReport_ReturnsBadRequest_WhenStoryNotPublished()
        {
            LogUtcContext("UTCID10",
                "Spec: story không PUBLISHED → dừng, không lưu báo cáo (ma trận: false).",
                "Product service: InvalidOperationException; API: BadRequest. Mock tương đương.",
                "Không assert message từng chữ so với ma trận.");

            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var controller = CreateStoriesControllerSut(out var reportMock);

            var request = new CreateStoryReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = new string('i', 40)
            };

            reportMock
                .Setup(s => s.CreateStoryReportAsync(storyId, reporterId, request))
                .ThrowsAsync(new InvalidOperationException("Chỉ có thể báo cáo truyện đã PUBLISHED."));

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStory(storyId, request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            reportMock.Verify(s => s.CreateStoryReportAsync(storyId, reporterId, request), Times.Once);
        }
    }
}



//dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT05_FunctionCreateStoryReport"