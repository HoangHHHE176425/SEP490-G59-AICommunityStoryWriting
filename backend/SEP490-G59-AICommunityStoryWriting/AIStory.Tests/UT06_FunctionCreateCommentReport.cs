using AIStory.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Services.DTOs.CommentReports;
using Services.Implementations;
using Services.Interfaces;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Security.Claims;
using System.Text.Json;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    /// <summary>
    /// UT06 — tạo báo cáo comment (endpoint <see cref="CommentReportsController.ReportStoryComment"/>; unit test + mock <see cref="ICommentReportService"/>).
    /// <b>Đối soát ma trận UTCID vs product (<see cref="Services.Implementations.CommentReportService.CreateCommentReportAsync"/>):</b>
    /// <list type="bullet">
    /// <item><description><b>Đã bắt — test pass:</b> UTCID01 (happy), UTCID02 (comment không tồn tại), UTCID04 (reporter không xác định → 401; JWT có id nhưng user không trong DB / <c>Guid.Empty</c> → <see cref="CommentReportService"/> + <see cref="IUserLookup"/>), UTCID05 (ReasonCode không có catalog), UTCID06 (ReasonCode thiếu/null — controller), UTCID07 (Description &gt; 200), UTCID08–09 (Description null/whitespace optional), UTCID10 (story không tìm thấy / mismatch URL), UTCID11 (trùng báo cáo), UTCID12 (story chưa PUBLISH), UTCID13 (tự báo cáo comment mình).</description></item>
    /// <item><description><b>Bug / thiếu — test fail cho đến khi sửa hoặc thống nhất spec:</b> UTCID03 (<c>commentId</c> null — REST <c>{{commentId:guid}}</c> không bind null; xử lý ở routing 404).</description></item>
    /// <item><description><b>Hành vi product có nhưng chưa có UTCID riêng trong file:</b> comment không thuộc chapter URL (<c>ReportChapterComment</c>), chủ comment role không phải AUTHOR/USER (<c>Bạn không thể báo cáo bình luận này.</c>), comment thiếu <c>story_id</c> (<c>Comment has no story_id.</c>) — cùng họ <see cref="InvalidOperationException"/> → 400 như các case khác.</description></item>
    /// </list>
    /// </summary>
    public class UT06_FunctionCreateCommentReport
    {
        private readonly ITestOutputHelper _output;

        public UT06_FunctionCreateCommentReport(ITestOutputHelper output) => _output = output;

        private void LogUtcContext(string utcId, string oneLineGoal, params string[] details)
        {
            _output.WriteLine("");
            _output.WriteLine($"======== {utcId} | UT06 CreateCommentReport ========");
            _output.WriteLine(oneLineGoal);
            foreach (var line in details)
                _output.WriteLine("  · " + line);
        }

        private static CommentReportsController CreateCommentReportsControllerSut(out Mock<ICommentReportService> serviceMock)
        {
            serviceMock = new Mock<ICommentReportService>(MockBehavior.Strict);
            return new CommentReportsController(serviceMock.Object);
        }

        /// <summary>
        /// UTCID01 — happy path: comment &amp; story hợp lệ, user chưa báo cáo comment này, truyện PUBLISHED, reporter không phải chủ comment (nghiệp vụ trong service); ReasonCode hợp lệ; mô tả &lt; 200 ký tự.
        /// Ma trận: Return True, log &quot;Tạo báo cáo thành công&quot; — API trả <c>200 OK</c> với <c>id</c> và &quot;Đã gửi báo cáo.&quot;; không assert đúng từng chữ.
        /// Product: <see cref="Services.Implementations.CommentReportService.CreateCommentReportAsync"/> (DAO/static) — unit test tầng API: <see cref="CommentReportsController.ReportStoryComment"/> + mock <see cref="ICommentReportService"/> trả <c>Guid</c> khác Empty → <c>Ok</c>; không DB.
        /// </summary>
        [Fact]
        public async Task UTCID01_CreateCommentReport_Succeeds_WhenPreconditionsMet_HappyPath()
        {
            LogUtcContext("UTCID01",
                "Spec: đủ điều kiện → tạo báo cáo comment thành công, persist (ma trận: Return True).",
                "API: POST stories/{storyId}/comments/{commentId}/reports — reporterId từ JWT.",
                "Kỳ vọng: 200 OK, body có id; CreateCommentReportAsync(..., expectedStoryId: storyId) gọi đúng 1 lần.",
                "Không assert log/message từng chữ so với ma trận.");

            var storyId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var returnedReportId = Guid.NewGuid();
            var controller = CreateCommentReportsControllerSut(out var serviceMock);

            var request = new CreateCommentReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = new string('a', 120)
            };

            serviceMock
                .Setup(s => s.CreateCommentReportAsync(
                    commentId,
                    reporterId,
                    request,
                    storyId,
                    null))
                .ReturnsAsync(returnedReportId);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStoryComment(storyId, commentId, request);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
            Assert.NotNull(ok.Value);
            var idProp = ok.Value.GetType().GetProperty("id", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(idProp);
            Assert.Equal(returnedReportId, idProp.GetValue(ok.Value));
            serviceMock.Verify(s => s.CreateCommentReportAsync(
                commentId,
                reporterId,
                request,
                storyId,
                null), Times.Once);
        }

        /// <summary>
        /// UTCID02 — spec: <c>commentId</c> không tồn tại → không tạo báo cáo; ma trận Return <c>false</c>, log &quot;Không tìm thấy comment&quot; (không assert đúng từng chữ).
        /// Product: <see cref="Services.Implementations.CommentReportService.CreateCommentReportAsync"/> — <c>CommentDAO.GetById</c> null → <see cref="InvalidOperationException"/> (message hiện tại tiếng Anh: <c>Comment not found.</c>).
        /// <see cref="CommentReportsController.ReportStoryComment"/> bắt <c>InvalidOperationException</c> → <c>400 BadRequest</c> (không trả bool <c>false</c>).
        /// Ghi chú: Ma trận của bạn có thể ghi &quot;UTCID01&quot; cho case này; trong file UT06 <see cref="UTCID01_CreateCommentReport_Succeeds_WhenPreconditionsMet_HappyPath"/> đã dùng cho happy path — case comment missing là <b>UTCID02</b>.
        /// </summary>
        [Fact]
        public async Task UTCID02_CreateCommentReport_ReturnsBadRequest_WhenCommentNotFound()
        {
            LogUtcContext("UTCID02",
                "Spec: commentId không có trong DB → dừng, không lưu (ma trận: false).",
                "Product service: InvalidOperationException; API: BadRequest. Mock tương đương.",
                "Không assert message từng chữ so với ma trận.");

            var storyId = Guid.NewGuid();
            var missingCommentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var controller = CreateCommentReportsControllerSut(out var serviceMock);

            var request = new CreateCommentReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = new string('b', 80)
            };

            serviceMock
                .Setup(s => s.CreateCommentReportAsync(
                    missingCommentId,
                    reporterId,
                    request,
                    storyId,
                    null))
                .ThrowsAsync(new InvalidOperationException("Comment not found."));

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStoryComment(storyId, missingCommentId, request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            serviceMock.Verify(s => s.CreateCommentReportAsync(
                missingCommentId,
                reporterId,
                request,
                storyId,
                null), Times.Once);
        }

        /// <summary>
        /// UTCID03 — ma trận: <c>commentId</c> null/không xác định → không tạo báo cáo (tương đương <c>Guid.Empty</c> trong unit test),
        /// Return <c>false</c> + log &quot;Không tìm thấy comment&quot;.
        /// </summary>
        [Fact]
        public async Task UTCID03_CreateCommentReport_ReturnsBadRequest_WhenCommentIdMissingOrEmpty()
        {
            LogUtcContext("UTCID03",
                "Spec: commentId null/không xác định → dừng, không lưu báo cáo.",
                "Test: mô phỏng bằng Guid.Empty; controller không gọi ICommentReportService.",
                "Kỳ vọng: BadRequest + message Không tìm thấy comment.");

            var storyId = Guid.NewGuid();
            var commentId = Guid.Empty;
            var reporterId = Guid.NewGuid();
            var controller = CreateCommentReportsControllerSut(out var serviceMock);

            var request = new CreateCommentReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = new string('c', 40)
            };

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStoryComment(storyId, commentId, request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
            Assert.Equal("Không tìm thấy comment.", doc.RootElement.GetProperty("message").GetString());

            serviceMock.Verify(s => s.CreateCommentReportAsync(
                It.IsAny<Guid>(),
                It.IsAny<Guid>(),
                It.IsAny<CreateCommentReportRequestDto>(),
                It.IsAny<Guid?>(),
                It.IsAny<Guid?>()), Times.Never);
        }

        /// <summary>
        /// UTCID04 — không xác định được người báo cáo <b>hoặc</b> user không còn trong CSDL → không tạo báo cáo; message kiểu &quot;USER không tồn tại&quot;.
        /// (A) API: <see cref="CommentReportsController.ReportStoryComment"/> — không parse được user từ JWT → <c>401</c>, không gọi service.
        /// (B) <see cref="CommentReportService.CreateCommentReportAsync"/>: <c>IUserLookup.Exists(reporterId)==false</c> hoặc <c>reporterId == Guid.Empty</c> → <see cref="InvalidOperationException"/> trước khi load comment / ghi DB.
        /// </summary>
        [Fact]
        public async Task UTCID04_CreateCommentReport_Rejects_WhenReporterInvalidOrUserNotInDatabase()
        {
            LogUtcContext("UTCID04 — reporter không hợp lệ / user không tồn tại",
                "Spec: không tạo báo cáo; API 401 khi thiếu identity; service từ chối khi Empty hoặc !Exists.",
                "Không assert message từng chữ so với ma trận.");

            var storyId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var controller = CreateCommentReportsControllerSut(out var serviceMock);

            var request = new CreateCommentReportRequestDto
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

            var result = await controller.ReportStoryComment(storyId, commentId, request);

            var unauthorized = Assert.IsType<UnauthorizedObjectResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);
            serviceMock.Verify(
                s => s.CreateCommentReportAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CreateCommentReportRequestDto>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>()),
                Times.Never);

            var reporterId = Guid.NewGuid();
            var userLookup = new Mock<IUserLookup>(MockBehavior.Strict);
            userLookup.Setup(x => x.Exists(reporterId)).Returns(false);
            var sut = new CommentReportService(userLookup.Object, notificationHubNotifier: null);

            var ex = await Record.ExceptionAsync(() =>
                sut.CreateCommentReportAsync(commentId, reporterId, request, expectedStoryId: storyId));
            var ioe = Assert.IsType<InvalidOperationException>(ex);
            Assert.Equal("USER không tồn tại.", ioe.Message);
            userLookup.Verify(x => x.Exists(reporterId), Times.Once);

            var userLookupEmpty = new Mock<IUserLookup>(MockBehavior.Strict);
            var sutEmpty = new CommentReportService(userLookupEmpty.Object, notificationHubNotifier: null);
            var exEmpty = await Record.ExceptionAsync(() =>
                sutEmpty.CreateCommentReportAsync(commentId, Guid.Empty, request, expectedStoryId: storyId));
            var ioeEmpty = Assert.IsType<InvalidOperationException>(exEmpty);
            Assert.Equal("USER không tồn tại.", ioeEmpty.Message);
            userLookupEmpty.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// UTCID05 — ma trận: <c>ReasonCode</c> không tồn tại trong hệ thống → không tạo báo cáo; Return <c>false</c>, log &quot;Không tồn tại lý do phù hợp&quot; (không assert đúng từng chữ).
        /// Product: <see cref="Services.Implementations.CommentReportService.CreateCommentReportAsync"/> — <c>CommentReportReasonCatalog.TryGet</c> false → <see cref="ArgumentException"/> (message hiện tại: <c>Invalid reason code.</c>).
        /// <see cref="CommentReportsController.ReportStoryComment"/> bắt <c>ArgumentException</c> → <c>400 BadRequest</c> với <c>ex.Message</c> (không trả bool <c>false</c>). Body phải có <c>ReasonCode</c> không rỗng mới vào service; rỗng thì controller trả BadRequest khác (&quot;ReasonCode is required.&quot;).
        /// Test: mock service ném <c>ArgumentException</c> tương đương — xác nhận mapping API.
        /// </summary>
        [Fact]
        public async Task UTCID05_CreateCommentReport_ReturnsBadRequest_WhenReasonCodeUnknown()
        {
            LogUtcContext("UTCID05",
                "Spec: ReasonCode không có trong catalog → dừng, không lưu (ma trận: false).",
                "Product service: ArgumentException; API: BadRequest. Mock tương đương.",
                "Không assert message từng chữ so với ma trận.");

            var storyId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var controller = CreateCommentReportsControllerSut(out var serviceMock);

            var request = new CreateCommentReportRequestDto
            {
                ReasonCode = "NOT_A_REGISTERED_COMMENT_REPORT_REASON",
                Description = new string('d', 40)
            };

            serviceMock
                .Setup(s => s.CreateCommentReportAsync(
                    commentId,
                    reporterId,
                    request,
                    storyId,
                    null))
                .ThrowsAsync(new ArgumentException("Invalid reason code."));

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStoryComment(storyId, commentId, request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            serviceMock.Verify(s => s.CreateCommentReportAsync(
                commentId,
                reporterId,
                request,
                storyId,
                null), Times.Once);
        }

        /// <summary>
        /// UTCID06 — ma trận: <c>ReasonCode</c> null → không tạo báo cáo; Return <c>false</c>, log &quot;Không tìm thấy lý do phù hợp&quot; (không assert đúng từng chữ).
        /// Product API: <see cref="CommentReportsController.ReportStoryComment"/> — <c>request == null || string.IsNullOrWhiteSpace(request.ReasonCode)</c> → <c>400 BadRequest</c> (&quot;ReasonCode is required.&quot;), <b>không</b> gọi <see cref="ICommentReportService.CreateCommentReportAsync"/>.
        /// DTO <see cref="CreateCommentReportRequestDto"/> có thể nhận <c>null</c> sau deserialize JSON; service không chạy nên không tới <c>CommentReportReasonCatalog.TryGet</c>.
        /// </summary>
        [Fact]
        public async Task UTCID06_CreateCommentReport_ReturnsBadRequest_WhenReasonCodeMissing()
        {
            LogUtcContext("UTCID06",
                "Spec: ReasonCode null → dừng, không lưu (ma trận: false).",
                "Product: controller chặn trước; BadRequest. Service không gọi.",
                "Không assert message từng chữ so với ma trận.");

            var storyId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var controller = CreateCommentReportsControllerSut(out var serviceMock);

#pragma warning disable CS8625 // null literal — mô phỏng body JSON thiếu / null reasonCode
            var request = new CreateCommentReportRequestDto
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

            var result = await controller.ReportStoryComment(storyId, commentId, request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            serviceMock.Verify(
                s => s.CreateCommentReportAsync(
                    It.IsAny<Guid>(),
                    It.IsAny<Guid>(),
                    It.IsAny<CreateCommentReportRequestDto>(),
                    It.IsAny<Guid?>(),
                    It.IsAny<Guid?>()),
                Times.Never);
        }

        /// <summary>
        /// UTCID07 — ma trận: <c>Description</c> &gt; <b>200</b> ký tự → không hợp lệ, không lưu; message kiểu &quot;ký tự quá dài&quot;.
        /// Product: <see cref="CreateCommentReportRequestDto.Description"/> <c>[MaxLength(200)]</c> + <see cref="CommentReportService.CreateCommentReportAsync"/> ném <see cref="ArgumentException"/> nếu vượt quá.
        /// </summary>
        [Fact]
        public async Task UTCID07_CreateCommentReport_Rejects_WhenDescriptionExceeds200Characters()
        {
            LogUtcContext("UTCID07 — Description > 200 ký tự",
                "Kỳ vọng: DataAnnotations + service đều từ chối > 200 ký tự.",
                "Không assert đúng từng chữ message so với ma trận.");

            static bool TryValidate(CreateCommentReportRequestDto dto, out List<ValidationResult> results)
            {
                results = new List<ValidationResult>();
                var ctx = new ValidationContext(dto);
                return Validator.TryValidateObject(dto, ctx, results, validateAllProperties: true);
            }

            var over201 = new CreateCommentReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = new string('g', 201)
            };
            Assert.False(TryValidate(over201, out var errors201));
            Assert.Contains(errors201, e => e.MemberNames.Contains(nameof(CreateCommentReportRequestDto.Description)));

            var wayOver = new CreateCommentReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = new string('f', 4001)
            };
            Assert.False(TryValidate(wayOver, out var errorsLong));
            Assert.Contains(errorsLong, e => e.MemberNames.Contains(nameof(CreateCommentReportRequestDto.Description)));

            var userLookup = new Mock<IUserLookup>(MockBehavior.Strict);
            var sut = new CommentReportService(userLookup.Object, notificationHubNotifier: null);
            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                sut.CreateCommentReportAsync(Guid.NewGuid(), Guid.NewGuid(), over201));
            Assert.Contains("quá dài", ex.Message, StringComparison.OrdinalIgnoreCase);
            userLookup.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
        }

        /// <summary>
        /// UTCID08 — spec: <c>Description</c> null — không bắt buộc → vẫn tạo báo cáo comment thành công; ma trận Return True, log &quot;Tạo báo cáo thành công&quot; (không assert đúng từng chữ).
        /// Product: <see cref="CreateCommentReportRequestDto.Description"/> là <c>string?</c>, không <c>[Required]</c>; <see cref="Services.Implementations.CommentReportService.CreateCommentReportAsync"/> dùng <c>string.IsNullOrWhiteSpace(request.Description)</c> → lưu <c>null</c> trong DB cho mô tả.
        /// <see cref="CommentReportsController.ReportStoryComment"/> chỉ bắt buộc <c>ReasonCode</c> không rỗng.
        /// API: mock <see cref="ICommentReportService.CreateCommentReportAsync"/> trả <c>Guid</c> khác Empty → <c>200 OK</c> (&quot;Đã gửi báo cáo.&quot;).
        /// </summary>
        [Fact]
        public async Task UTCID08_CreateCommentReport_Succeeds_WhenDescriptionIsNull_OptionalField()
        {
            LogUtcContext("UTCID08",
                "Spec: Description null — không bắt buộc → tạo báo cáo OK (ma trận: Return True).",
                "API: POST stories/{storyId}/comments/{commentId}/reports với ReasonCode, không mô tả.",
                "Kỳ vọng: 200 OK + id; CreateCommentReportAsync gọi đúng 1 lần. Không assert log từng chữ.");

            var storyId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var returnedReportId = Guid.NewGuid();
            var controller = CreateCommentReportsControllerSut(out var serviceMock);

            var request = new CreateCommentReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = null
            };

            serviceMock
                .Setup(s => s.CreateCommentReportAsync(
                    commentId,
                    reporterId,
                    request,
                    storyId,
                    null))
                .ReturnsAsync(returnedReportId);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStoryComment(storyId, commentId, request);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
            Assert.NotNull(ok.Value);
            var idProp = ok.Value.GetType().GetProperty("id", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(idProp);
            Assert.Equal(returnedReportId, idProp.GetValue(ok.Value));
            serviceMock.Verify(s => s.CreateCommentReportAsync(
                commentId,
                reporterId,
                request,
                storyId,
                null), Times.Once);
        }

        /// <summary>
        /// UTCID09 — spec: <c>Description</c> chỉ khoảng trắng — không bắt buộc nội dung có nghĩa → vẫn tạo báo cáo comment thành công; ma trận Return True, log &quot;Tạo báo cáo thành công&quot; (không assert đúng từng chữ).
        /// Product: <see cref="CommentReportsController.ReportStoryComment"/> không kiểm tra <c>IsNullOrWhiteSpace</c> trên <c>Description</c> (chỉ <c>ReasonCode</c>). <see cref="Services.Implementations.CommentReportService.CreateCommentReportAsync"/> dùng <c>string.IsNullOrWhiteSpace(request.Description)</c> khi lưu → coi như không có mô tả chi tiết, vẫn ghi báo cáo.
        /// API: mock <see cref="ICommentReportService.CreateCommentReportAsync"/> trả <c>Guid</c> khác Empty → <c>200 OK</c>.
        /// </summary>
        [Fact]
        public async Task UTCID09_CreateCommentReport_Succeeds_WhenDescriptionIsWhitespaceOnly()
        {
            LogUtcContext("UTCID09",
                "Spec: Description chỉ whitespace — vẫn tạo báo cáo OK (ma trận: Return True).",
                "Product: controller không chặn; service coalesce whitespace → null khi persist.",
                "Kỳ vọng: 200 OK + id; CreateCommentReportAsync gọi đúng 1 lần. Không assert log từng chữ.");

            var storyId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var returnedReportId = Guid.NewGuid();
            var controller = CreateCommentReportsControllerSut(out var serviceMock);

            var request = new CreateCommentReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = "   \t  \n  "
            };

            serviceMock
                .Setup(s => s.CreateCommentReportAsync(
                    commentId,
                    reporterId,
                    request,
                    storyId,
                    null))
                .ReturnsAsync(returnedReportId);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStoryComment(storyId, commentId, request);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
            Assert.NotNull(ok.Value);
            var idProp = ok.Value.GetType().GetProperty("id", BindingFlags.Public | BindingFlags.Instance);
            Assert.NotNull(idProp);
            Assert.Equal(returnedReportId, idProp.GetValue(ok.Value));
            serviceMock.Verify(s => s.CreateCommentReportAsync(
                commentId,
                reporterId,
                request,
                storyId,
                null), Times.Once);
        }

        /// <summary>
        /// UTCID10 — ma trận: không tìm thấy story tương ứng với comment → không tạo báo cáo; Return <c>false</c>, log &quot;Không tìm thấy truyện&quot; (không assert đúng từng chữ).
        /// Product: <see cref="Services.Implementations.CommentReportService.CreateCommentReportAsync"/> — sau khi load comment hợp lệ, <c>StoryDAO.GetById(storyId)</c> null → <see cref="InvalidOperationException"/> (message hiện tại: <c>Story not found.</c>).
        /// Khi <c>expectedStoryId</c> trên URL khác <c>comment.story_id</c>: <c>Comment not belong to this story.</c> (cùng nhóm &quot;story không khớp / không hợp lệ&quot;).
        /// <see cref="CommentReportsController.ReportStoryComment"/> bắt <c>InvalidOperationException</c> → <c>400 BadRequest</c> (không trả bool <c>false</c>).
        /// Test: mock <c>Story not found.</c> tương đương nhánh DB thiếu truyện.
        /// </summary>
        [Fact]
        public async Task UTCID10_CreateCommentReport_ReturnsBadRequest_WhenStoryNotFoundForComment()
        {
            LogUtcContext("UTCID10",
                "Spec: story không tồn tại / không khớp comment → dừng, không lưu (ma trận: false).",
                "Product service: InvalidOperationException (Story not found. hoặc Comment not belong...); API: BadRequest.",
                "Mock: Story not found. — không assert message từng chữ so với ma trận.");

            var storyIdFromRoute = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var controller = CreateCommentReportsControllerSut(out var serviceMock);

            var request = new CreateCommentReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = new string('h', 80)
            };

            serviceMock
                .Setup(s => s.CreateCommentReportAsync(
                    commentId,
                    reporterId,
                    request,
                    storyIdFromRoute,
                    null))
                .ThrowsAsync(new InvalidOperationException("Story not found."));

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStoryComment(storyIdFromRoute, commentId, request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            serviceMock.Verify(s => s.CreateCommentReportAsync(
                commentId,
                reporterId,
                request,
                storyIdFromRoute,
                null), Times.Once);
        }

        /// <summary>
        /// UTCID11 — ma trận: user đã báo cáo comment này trước đó → không tạo thêm; Return <c>false</c>, log &quot;Bạn đã báo cáo comment này rồi&quot; (không assert đúng từng chữ).
        /// Product: <see cref="Services.Implementations.CommentReportService.CreateCommentReportAsync"/> — trùng user/comment (evidence hoặc legacy <c>reports.reporter_id</c>) → <see cref="InvalidOperationException"/> (message hiện tại: <c>Bạn đã báo cáo bình luận này trước đó.</c>).
        /// <see cref="CommentReportsController.ReportStoryComment"/> bắt <c>InvalidOperationException</c> → <c>400 BadRequest</c> (không dùng <c>409</c> như <see cref="AIStory.API.Controllers.StoriesController.ReportStory"/> khi trùng report truyện).
        /// Test: mock ném exception cùng kiểu message — xác nhận mapping API.
        /// </summary>
        [Fact]
        public async Task UTCID11_CreateCommentReport_ReturnsBadRequest_WhenUserAlreadyReportedComment()
        {
            LogUtcContext("UTCID11",
                "Spec: đã báo cáo comment này rồi → không lưu thêm (ma trận: false).",
                "Product: InvalidOperationException; API: 400 BadRequest (không phải 409).",
                "Không assert message từng chữ so với ma trận.");

            var storyId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var controller = CreateCommentReportsControllerSut(out var serviceMock);

            var request = new CreateCommentReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = new string('i', 60)
            };

            serviceMock
                .Setup(s => s.CreateCommentReportAsync(
                    commentId,
                    reporterId,
                    request,
                    storyId,
                    null))
                .ThrowsAsync(new InvalidOperationException("Bạn đã báo cáo bình luận này trước đó."));

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStoryComment(storyId, commentId, request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            serviceMock.Verify(s => s.CreateCommentReportAsync(
                commentId,
                reporterId,
                request,
                storyId,
                null), Times.Once);
        }

        /// <summary>
        /// UTCID12 — ma trận (có thể ghi <b>UTCID122</b>): story chưa <c>PUBLISHED</c> → không tạo báo cáo comment; Return <c>false</c>, log &quot;Truyện chưa được PUBLISH&quot; (không assert đúng từng chữ).
        /// Product: <see cref="Services.Implementations.CommentReportService.CreateCommentReportAsync"/> — sau <c>StoryDAO.GetById</c>, <c>status</c> khác <c>PUBLISHED</c> → <see cref="InvalidOperationException"/> (message hiện tại: <c>Chỉ có thể báo cáo bình luận của truyện đã PUBLISHED.</c>).
        /// <see cref="CommentReportsController.ReportStoryComment"/> bắt <c>InvalidOperationException</c> → <c>400 BadRequest</c> (không trả bool <c>false</c>).
        /// Test: mock ném exception cùng kiểu — xác nhận mapping API.
        /// </summary>
        [Fact]
        public async Task UTCID12_CreateCommentReport_ReturnsBadRequest_WhenStoryNotPublished()
        {
            LogUtcContext("UTCID12 (ma trận có thể: UTCID122)",
                "Spec: story chưa PUBLISH → không lưu báo cáo comment (ma trận: false).",
                "Product service: InvalidOperationException; API: BadRequest.",
                "Không assert message từng chữ so với ma trận.");

            var storyId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var controller = CreateCommentReportsControllerSut(out var serviceMock);

            var request = new CreateCommentReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = new string('j', 55)
            };

            serviceMock
                .Setup(s => s.CreateCommentReportAsync(
                    commentId,
                    reporterId,
                    request,
                    storyId,
                    null))
                .ThrowsAsync(new InvalidOperationException("Chỉ có thể báo cáo bình luận của truyện đã PUBLISHED."));

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStoryComment(storyId, commentId, request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            serviceMock.Verify(s => s.CreateCommentReportAsync(
                commentId,
                reporterId,
                request,
                storyId,
                null), Times.Once);
        }

        /// <summary>
        /// UTCID13 — ma trận: reporter là chủ comment → không được tự báo cáo; Return <c>false</c>, log &quot;Không thể tự báo cáo chính mình&quot; (không assert đúng từng chữ).
        /// Product: <see cref="Services.Implementations.CommentReportService.CreateCommentReportAsync"/> — <c>comment.user_id == reporterId</c> → <see cref="InvalidOperationException"/> (message hiện tại: <c>Bạn không thể báo cáo bình luận của chính mình.</c>).
        /// <see cref="CommentReportsController.ReportStoryComment"/> bắt <c>InvalidOperationException</c> → <c>400 BadRequest</c>.
        /// Test: mock ném exception cùng kiểu — xác nhận mapping API.
        /// </summary>
        [Fact]
        public async Task UTCID13_CreateCommentReport_ReturnsBadRequest_WhenReporterIsCommentOwner()
        {
            LogUtcContext("UTCID13",
                "Spec: reporter trùng chủ comment → không lưu báo cáo (ma trận: false).",
                "Product service: InvalidOperationException; API: BadRequest.",
                "Không assert message từng chữ so với ma trận.");

            var storyId = Guid.NewGuid();
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var controller = CreateCommentReportsControllerSut(out var serviceMock);

            var request = new CreateCommentReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = new string('k', 45)
            };

            serviceMock
                .Setup(s => s.CreateCommentReportAsync(
                    commentId,
                    reporterId,
                    request,
                    storyId,
                    null))
                .ThrowsAsync(new InvalidOperationException("Bạn không thể báo cáo bình luận của chính mình."));

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, reporterId.ToString()) },
                authenticationType: "Test");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ReportStoryComment(storyId, commentId, request);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            serviceMock.Verify(s => s.CreateCommentReportAsync(
                commentId,
                reporterId,
                request,
                storyId,
                null), Times.Once);
        }
    }
}



//dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT06_FunctionCreateCommentReport"