using AIStory.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Services.DTOs.StoryReports;
using Services.Interfaces;
using System;
using System.Security.Claims;
using System.Text.Json;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT_AdminResolveComplianceAdminActionRequest
    {
        private readonly ITestOutputHelper _output;

        public UT_AdminResolveComplianceAdminActionRequest(ITestOutputHelper output) => _output = output;

        private void LogUtcContext(string utcId, string oneLineGoal, params string[] details)
        {
            _output.WriteLine("");
            _output.WriteLine($"======== {utcId} ========");
            _output.WriteLine(oneLineGoal);
            foreach (var line in details)
                _output.WriteLine("  · " + line);
        }

        private static AdminComplianceStoryReportsController CreateSut(out Mock<IStoryReportService> serviceMock)
        {
            serviceMock = new Mock<IStoryReportService>(MockBehavior.Strict);
            return new AdminComplianceStoryReportsController(
                serviceMock.Object,
                NullLogger<AdminComplianceStoryReportsController>.Instance);
        }

        /// <summary>
        /// UTCID01 — happy path: xử lý yêu cầu compliance hợp lệ và admin resolve thành công.
        /// Ma trận/spec: Return <c>true</c>, log &quot;Tạo báo cáo thành công&quot; (không assert đúng từng chữ).
        /// Product: <see cref="AdminComplianceStoryReportsController.ResolveAdminActionRequest"/> — controller kiểm tra Decision; nếu hợp lệ sẽ gọi
        /// <see cref="IStoryReportService.AdminResolveComplianceAdminActionRequestAsync"/> và trả <c>200 OK</c>.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UTCID01_AdminResolveComplianceAdminActionRequest_Succeeds_WhenValidInput()
        {
            LogUtcContext("UTCID01",
                "Spec: compliance request hợp lệ → resolve thành công.",
                "API: AdminComplianceStoryReportsController.ResolveAdminActionRequest.",
                "Kỳ vọng: 200 OK; service không ném exception.",
                "Không assert message/log theo từng chữ so với ma trận.");

            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var body = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "APPROVE",
                ReasonCode = "OTHER",
                AdminNote = "ok",
                SuspendUntilUtc = DateTime.UtcNow.AddDays(2)
            };

            var controller = CreateSut(out var serviceMock);

            serviceMock
                .Setup(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body))
                .Returns(System.Threading.Tasks.Task.CompletedTask);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, adminId.ToString()) },
                authenticationType: "Test");

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ResolveAdminActionRequest(requestId, body);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
            Assert.NotNull(ok.Value);

            serviceMock.Verify(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body), Times.Once);
        }

        /// <summary>
        /// UTCID02 — spec: requestId không tồn tại → không tìm thấy comment (ma trận: Return <c>false</c> + log).
        /// Product hiện tại (backend): nếu requestId không tồn tại, service/DAO ném <see cref="InvalidOperationException"/>
        /// (message hiện tại: &quot;Yêu cầu không tồn tại.&quot;). Controller bắt <see cref="InvalidOperationException"/> → <c>400 BadRequest</c>.
        /// Test: mock service ném exception tương đương — xác nhận mapping HTTP (không assert message từng chữ).
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UTCID02_AdminResolveComplianceAdminActionRequest_ReturnsBadRequest_WhenRequestIdNotFound()
        {
            LogUtcContext("UTCID02",
                "Spec: requestId not found → dừng xử lý.",
                "Product: service ném InvalidOperationException; controller trả 400.",
                "Không assert message từng chữ so với ma trận.");

            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();

            var body = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "REJECT",
                ReasonCode = "OTHER",
                AdminNote = "nope",
                SuspendUntilUtc = DateTime.UtcNow.AddDays(2)
            };

            var controller = CreateSut(out var serviceMock);

            serviceMock
                .Setup(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body))
                .ThrowsAsync(new InvalidOperationException("Yêu cầu không tồn tại."));

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, adminId.ToString()) },
                authenticationType: "Test");

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ResolveAdminActionRequest(requestId, body);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);

            serviceMock.Verify(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body), Times.Once);
        }

        /// <summary>
        /// UTCID03 — ma trận: requestId <b>null</b> → không xác định được comment/yêu cầu để xử lý (ma trận: Return <c>false</c> + log).
        /// REST: segment <c>{requestId:guid}</c> không tạo được tham số C# <c>null</c>; product xử lý tương đương bằng <see cref="Guid.Empty"/> (ID không hợp lệ / không xác định)
        /// — <see cref="AdminComplianceStoryReportsController.ResolveAdminActionRequest"/> trả <c>400</c>, log cảnh báo, <b>không</b> gọi service.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UTCID03_AdminResolveComplianceAdminActionRequest_ReturnsBadRequest_WhenRequestIdEffectivelyMissing()
        {
            LogUtcContext("UTCID03",
                "Spec: requestId null / không xác định → dừng, không xử lý (ma trận: false + log).",
                "Product: Guid.Empty tại controller → 400 + message Không tìm thấy comment.; không gọi IStoryReportService.");

            var adminId = Guid.NewGuid();
            var body = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "REJECT",
                ReasonCode = "OTHER",
                AdminNote = "nope",
                SuspendUntilUtc = DateTime.UtcNow.AddDays(2)
            };

            var controller = CreateSut(out var serviceMock);

            var identity = new ClaimsIdentity(
                new[] { new Claim(ClaimTypes.NameIdentifier, adminId.ToString()) },
                authenticationType: "Test");

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            var result = await controller.ResolveAdminActionRequest(Guid.Empty, body);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
            Assert.Equal("Không tìm thấy comment.", doc.RootElement.GetProperty("message").GetString());

            serviceMock.Verify(
                s => s.AdminResolveComplianceAdminActionRequestAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<AdminResolveComplianceAdminActionRequestDto>()),
                Times.Never);
        }

        /// <summary>
        /// UTCID04 — spec: reporterId <b>null</b> → không xác định được user thực hiện yêu cầu.
        /// Product hiện tại: controller lấy user qua JWT (sub/NameIdentifier). Nếu không parse được Guid (uid = null)
        /// thì trả <c>401 Unauthorized</c> ở tầng controller và <b>không</b> gọi service.
        /// Test: mô phỏng token không có claim định danh bằng <c>ClaimsIdentity()</c>.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UTCID04_AdminResolveComplianceAdminActionRequest_ReturnsUnauthorized_WhenReporterIdMissing()
        {
            LogUtcContext("UTCID04",
                "Spec: reporterId null → không xác định được user → dừng xử lý.",
                "Product: GetCurrentUserId() trả null → Unauthorized 401 ở controller.",
                "Không assert message message/log theo từng chữ so với ma trận.");

            var requestId = Guid.NewGuid();
            var body = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "APPROVE",
                ReasonCode = "OTHER",
                AdminNote = "ok",
                SuspendUntilUtc = DateTime.UtcNow.AddDays(2)
            };

            var controller = CreateSut(out var serviceMock);

            // Mô phỏng token không có claim định danh (uid sẽ null)
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity())
                }
            };

            var result = await controller.ResolveAdminActionRequest(requestId, body);

            var unauthorized = Assert.IsType<UnauthorizedResult>(result);
            Assert.Equal(StatusCodes.Status401Unauthorized, unauthorized.StatusCode);

            serviceMock.Verify(
                s => s.AdminResolveComplianceAdminActionRequestAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<AdminResolveComplianceAdminActionRequestDto>()),
                Times.Never);
        }

        /// <summary>
        /// UTCID05 — resolve đơn compliance admin-action không bắt buộc ReasonCode trong catalog.
        /// APPROVE với mã không tồn tại trong <see cref="Services.StoryReporting.StoryReportReasonCatalog"/> vẫn chuyển xuống service (UI có thể không gửi mã chuẩn).
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UTCID05_AdminResolveComplianceAdminActionRequest_Succeeds_WhenApproveWithUnknownReasonCode()
        {
            LogUtcContext("UTCID05",
                "Spec: APPROVE + reasonCode không có trong catalog.",
                "Product: controller không chặn; service xử lý BAN/SUSPEND theo đơn + AdminNote.");

            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var body = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "APPROVE",
                ReasonCode = "NOT_IN_CATALOG_XYZ",
                AdminNote = "ok",
                SuspendUntilUtc = DateTime.UtcNow.AddDays(2)
            };

            var controller = CreateSut(out var serviceMock);

            serviceMock
                .Setup(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body))
                .Returns(System.Threading.Tasks.Task.CompletedTask);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, adminId.ToString()) },
                            authenticationType: "Test"))
                }
            };

            var result = await controller.ResolveAdminActionRequest(requestId, body);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
            serviceMock.Verify(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body), Times.Once);
        }

        /// <summary>
        /// UTCID06 — APPROVE chặn tài khoản / đình chỉ viết: frontend có thể không gửi <c>ReasonCode</c>; vẫn 200 và gọi service.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UTCID06_AdminResolveComplianceAdminActionRequest_Succeeds_WhenApproveWithMissingReasonCode()
        {
            LogUtcContext("UTCID06",
                "Spec: APPROVE + ReasonCode null.",
                "Product: không yêu cầu ReasonCode cho resolve admin-action compliance.");

            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var body = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "APPROVE",
                ReasonCode = null,
                AdminNote = "tôi đồng ý xử lý",
                SuspendUntilUtc = DateTime.UtcNow.AddDays(2)
            };

            var controller = CreateSut(out var serviceMock);

            serviceMock
                .Setup(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body))
                .Returns(System.Threading.Tasks.Task.CompletedTask);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, adminId.ToString()) },
                            authenticationType: "Test"))
                }
            };

            var result = await controller.ResolveAdminActionRequest(requestId, body);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
            serviceMock.Verify(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body), Times.Once);
        }

        /// <summary>
        /// UTCID07 — ma trận: mô tả (<c>AdminNote</c>) &gt; 200 ký tự → không xử lý; message kiểu &quot;ký tự quá dài&quot;.
        /// Product: <see cref="AdminComplianceStoryReportsController.ResolveAdminActionRequest"/> trả <c>400</c> trước khi gọi service; DTO <see cref="AdminResolveComplianceAdminActionRequestDto"/> <c>[MaxLength(200)]</c>; service có guard tương tự.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UTCID07_AdminResolveComplianceAdminActionRequest_ReturnsBadRequest_WhenAdminNoteExceeds200Characters()
        {
            LogUtcContext("UTCID07 — AdminNote / description > 200 ký tự",
                "Spec: không resolve; không persist.",
                "Ánh xạ ma trận: description → AdminNote.",
                "Kỳ vọng: BadRequest; service không được gọi.");

            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var controller = CreateSut(out var serviceMock);

            var body = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "APPROVE",
                ReasonCode = "OTHER",
                AdminNote = new string('x', 201),
                SuspendUntilUtc = DateTime.UtcNow.AddDays(2)
            };

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, adminId.ToString()) },
                            authenticationType: "Test"))
                }
            };

            var result = await controller.ResolveAdminActionRequest(requestId, body);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            serviceMock.Verify(
                s => s.AdminResolveComplianceAdminActionRequestAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<AdminResolveComplianceAdminActionRequestDto>()),
                Times.Never);
        }

        /// <summary>
        /// UTCID08 — ma trận: <c>description</c> chỉ chứa khoảng trắng → vẫn xử lý thành công.
        /// <b>[BUG/Mapping note]</b> Endpoint/controller trong UT07 không có field <c>description</c> riêng,
        /// nên mình ánh xạ <c>description</c> sang <c>AdminNote</c> trong <see cref="AdminResolveComplianceAdminActionRequestDto"/>.
        /// Product hiện tại: controller không validate nội dung khoảng trắng trước khi gọi service.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UTCID08_Succeeds_WhenDescriptionIsWhitespaceOnly_OptionalField()
        {
            LogUtcContext("UTCID08",
                "Spec: description chỉ whitespace → vẫn xử lý thành công.",
                "UT07 mapping: description -> AdminNote (AdminNote = null).",
                "Kỳ vọng: 200 OK; service được gọi.");

            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var controller = CreateSut(out var serviceMock);

            var body = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "APPROVE",
                ReasonCode = "OTHER",
                AdminNote = "   \t  \n  ",
                SuspendUntilUtc = DateTime.UtcNow.AddDays(2)
            };

            serviceMock
                .Setup(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body))
                .Returns(System.Threading.Tasks.Task.CompletedTask);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, adminId.ToString()) },
                            authenticationType: "Test"))
                }
            };

            var result = await controller.ResolveAdminActionRequest(requestId, body);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
            Assert.NotNull(ok.Value);

            serviceMock.Verify(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body), Times.Once);
        }

        /// <summary>
        /// UTCID09 — ma trận: <c>description</c> chỉ chứa khoảng trắng → vẫn xử lý thành công.
        /// <b>[Mapping note]</b> Endpoint/controller trong UT07 không có field <c>description</c> riêng,
        /// nên ánh xạ <c>description</c> sang <see cref="AdminResolveComplianceAdminActionRequestDto.AdminNote"/>.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UTCID09_Succeeds_WhenDescriptionIsWhitespaceOnly_OptionalField()
        {
            LogUtcContext("UTCID09",
                "Spec: description chỉ whitespace → vẫn xử lý thành công.",
                "UT07 mapping: description -> AdminNote (whitespace).",
                "Kỳ vọng: 200 OK; service được gọi.");

            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var controller = CreateSut(out var serviceMock);

            var body = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "APPROVE",
                ReasonCode = "OTHER",
                AdminNote = " \t  ",
                SuspendUntilUtc = DateTime.UtcNow.AddDays(2)
            };

            serviceMock
                .Setup(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body))
                .Returns(System.Threading.Tasks.Task.CompletedTask);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, adminId.ToString()) },
                            authenticationType: "Test"))
                }
            };

            var result = await controller.ResolveAdminActionRequest(requestId, body);

            var ok = Assert.IsType<OkObjectResult>(result);
            Assert.Equal(StatusCodes.Status200OK, ok.StatusCode);
            Assert.NotNull(ok.Value);

            serviceMock.Verify(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body), Times.Once);
        }

        /// <summary>
        /// UTCID10 — ma trận: không tìm thấy story (ánh xạ ngữ cảnh: story tương ứng comment/báo cáo) → không xử lý request compliance
        /// (Return <c>false</c> + log &quot;Không tìm thấy truyện&quot;).
        /// Product: <see cref="Services.Implementations.StoryReportService.AdminResolveComplianceAdminActionRequestAsync"/> kiểm tra
        /// <c>StoryDAO.GetById(row.story_id)</c>; ném <see cref="InvalidOperationException"/> (&quot;Không tìm thấy truyện.&quot;); controller trả <c>400</c>.
        /// Test: mock service ném exception tương đương — xác nhận mapping HTTP (message theo product).
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UTCID10_AdminResolveComplianceAdminActionRequest_ReturnsBadRequest_WhenStoryNotFoundForComplianceRequest()
        {
            LogUtcContext("UTCID10",
                "Spec: không tìm thấy truyện gắn với yêu cầu → không resolve.",
                "Product: InvalidOperationException Không tìm thấy truyện.; controller 400 + log cảnh báo.");

            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var controller = CreateSut(out var serviceMock);

            var body = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "APPROVE",
                ReasonCode = "OTHER",
                AdminNote = "note",
                SuspendUntilUtc = DateTime.UtcNow.AddDays(2)
            };

            serviceMock
                .Setup(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body))
                .ThrowsAsync(new InvalidOperationException("Không tìm thấy truyện."));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, adminId.ToString()) },
                            authenticationType: "Test"))
                }
            };

            var result = await controller.ResolveAdminActionRequest(requestId, body);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
            Assert.Equal("Không tìm thấy truyện.", doc.RootElement.GetProperty("message").GetString());

            serviceMock.Verify(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body), Times.Once);
        }

        /// <summary>
        /// UTCID11 — ma trận: user đã từng báo cáo comment này trước đó → không xử lý yêu cầu compliance để tạo report mới (ma trận: Return <c>false</c> + log).
        /// <b>[Mapping]</b> Endpoint UT07 hiện tại là resolve admin action request: nếu request không còn trạng thái <c>PENDING</c> thì service
        /// sẽ ném <see cref="InvalidOperationException"/> (&quot;Yêu cầu đã xử lý.&quot;).
        /// Mình dùng case này để đại diện cho “đã từng xử lý trước đó / trùng request” trong phạm vi contract UT07.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UTCID11_AdminResolveComplianceAdminActionRequest_ReturnsBadRequest_WhenAlreadyResolved()
        {
            LogUtcContext("UTCID11",
                "Spec: user đã từng báo cáo comment → không tạo/xử lý thêm.",
                "UT07 mapping: resolve lại cùng requestId thì service ném InvalidOperationException (request đã xử lý).",
                "Kỳ vọng: controller trả 400 BadRequest; không còn Ok 200.");

            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var controller = CreateSut(out var serviceMock);

            var body = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "REJECT",
                ReasonCode = "OTHER",
                AdminNote = "note",
                SuspendUntilUtc = DateTime.UtcNow.AddDays(2)
            };

            serviceMock
                .Setup(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body))
                .ThrowsAsync(new InvalidOperationException("Yêu cầu đã xử lý."));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, adminId.ToString()) },
                            authenticationType: "Test"))
                }
            };

            var result = await controller.ResolveAdminActionRequest(requestId, body);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);

            serviceMock.Verify(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body), Times.Once);
        }

        /// <summary>
        /// UTCID12 — ma trận: story chưa <c>PUBLISH</c> → không xử lý yêu cầu compliance để tạo report (ma trận: Return <c>false</c>).
        /// <b>[BUG — thiếu validate status]</b> Endpoint UT07 resolve dựa trên <c>requestId</c> và gọi
        /// <see cref="Services.Implementations.StoryReportService.AdminResolveComplianceAdminActionRequestAsync(Guid, Guid, AdminResolveComplianceAdminActionRequestDto)"/>.
        /// Trong service hiện tại không có bước load/validate <c>Story.status</c> trước khi resolve.
        /// Vì vậy với spec yêu cầu reject, backend hiện có thể vẫn xử lý thành công → unit test dưới đây đánh dấu bug.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UTCID12_Bug_AdminResolveComplianceAdminActionRequest_StoryNotPublished_PerSpec()
        {
            LogUtcContext("UTCID12 [BUG — story not published]",
                "Spec: story chưa PUBLISH → không xử lý yêu cầu.",
                "UT07 product code: AdminResolveComplianceAdminActionRequestAsync không validate Story.status.",
                "Kỳ vọng: reject; hiện tại (mock) trả OK nên fail để đánh dấu bug.");

            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var controller = CreateSut(out var serviceMock);

            var body = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "APPROVE",
                ReasonCode = "OTHER",
                AdminNote = "note",
                SuspendUntilUtc = DateTime.UtcNow.AddDays(2)
            };

            // Mô phỏng backend validate story.status != PUBLISHED.
            serviceMock
                .Setup(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body))
                .ThrowsAsync(new InvalidOperationException("Truyện chưa được PUBLISH"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, adminId.ToString()) },
                            authenticationType: "Test"))
                }
            };

            var result = await controller.ResolveAdminActionRequest(requestId, body);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
            Assert.Equal("Truyện chưa được PUBLISH", doc.RootElement.GetProperty("message").GetString());

            serviceMock.Verify(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body), Times.Once);
        }

        /// <summary>
        /// UTCID13 — reporter là chủ comment → không được tự báo cáo → không xử lý yêu cầu compliance.
        /// Product/service: nếu <c>requester_id == target_user_id</c> thì ném <see cref="InvalidOperationException"/> (&quot;Không thể tự báo cáo chính mình&quot;);
        /// controller trả <c>400</c> + log cảnh báo; không gọi resolve.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UTCID13_Bug_AdminResolveComplianceAdminActionRequest_SelfReportCommentOwner_PerSpec()
        {
            LogUtcContext("UTCID13 [BUG — self report comment owner]",
                "Spec: reporter là chủ comment → không được phép xử lý (Return false + log).",
                "Test: mô phỏng service ném InvalidOperationException 'Không thể tự báo cáo chính mình'.",
                "Kỳ vọng: 400 BadRequest; không còn Ok 200.");

            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var controller = CreateSut(out var serviceMock);

            var body = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "APPROVE",
                ReasonCode = "OTHER",
                AdminNote = "note",
                SuspendUntilUtc = DateTime.UtcNow.AddDays(2)
            };

            serviceMock
                .Setup(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body))
                .ThrowsAsync(new InvalidOperationException("Không thể tự báo cáo chính mình"));

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(
                        new ClaimsIdentity(
                            new[] { new Claim(ClaimTypes.NameIdentifier, adminId.ToString()) },
                            authenticationType: "Test"))
                }
            };

            var result = await controller.ResolveAdminActionRequest(requestId, body);

            var badRequest = Assert.IsType<BadRequestObjectResult>(result);
            Assert.Equal(StatusCodes.Status400BadRequest, badRequest.StatusCode);
            Assert.NotNull(badRequest.Value);
            using var doc = JsonDocument.Parse(JsonSerializer.Serialize(badRequest.Value));
            Assert.Equal("Không thể tự báo cáo chính mình", doc.RootElement.GetProperty("message").GetString());

            serviceMock.Verify(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body), Times.Once);
        }
    }
}


//dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT07_FunctionAdminResolveComplianceAdminActionRequest"