using AIStory.API.Controllers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Services.DTOs.StoryReports;
using Services.Interfaces;
using System;
using System.Security.Claims;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT07_FunctionAdminResolveComplianceAdminActionRequest
    {
        private readonly ITestOutputHelper _output;

        public UT07_FunctionAdminResolveComplianceAdminActionRequest(ITestOutputHelper output) => _output = output;

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
        /// Product hiện: controller <see cref="AdminComplianceStoryReportsController.ResolveAdminActionRequest(Guid, AdminResolveComplianceAdminActionRequestDto)"/>
        /// nhận <c>requestId</c> dạng <c>Guid</c> (từ route param), nên ở tầng C# không thể truyền <c>null</c>.
        /// Routing thường sẽ trả <c>404</c>/không gọi action nếu segment không phải GUID hợp lệ.
        /// Test: hiện không thể biểu diễn trực tiếp case “requestId null” theo contract; viết dưới dạng <b>[BUG]</b> để chờ product đổi contract/validate (ví dụ chuyển về <c>Guid?</c> và kiểm tra null).
        /// </summary>
        [Fact]
        public void UTCID03_Bug_AdminResolveComplianceAdminActionRequest_RequestIdNull_PerSpec()
        {
            LogUtcContext("UTCID03 [BUG — requestId null]",
                "Spec: requestId null → dừng, không xử lý.",
                "Product contract: ResolveAdminActionRequest nhận requestId kiểu Guid (route {requestId:guid}) → không thể null ở C#.",
                "Hệ quả: test không thể mô phỏng đúng input null; chỉ có thể dùng Guid.Empty tương đương 'not found' (không đúng ma trận).");

            Assert.Fail(
                "BUG UT07/UTCID03: Ma trận yêu cầu requestId null, nhưng API/controller/service đang nhận requestId là Guid (không Nullable) từ route constraint {requestId:guid} nên không thể biểu diễn 'null' trong unit test theo contract hiện tại. " +
                "Cần thống nhất spec/REST (404 routing) hoặc đổi contract (Guid? + check null) rồi cập nhật test để pass.");
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
        /// UTCID05 — ma trận: <c>reasonCode</c> không tồn tại → không xử lý yêu cầu compliance (ma trận: Return <c>false</c>).
        /// <b>[BUG — mismatch contract]</b> Endpoint/controller này hiện tại <c>không nhận</c> tham số <c>reasonCode</c> từ request:
        /// <see cref="AdminComplianceStoryReportsController.ResolveAdminActionRequest(Guid, AdminResolveComplianceAdminActionRequestDto)"/>
        /// chỉ nhận <c>requestId</c> (route) và DTO <see cref="AdminResolveComplianceAdminActionRequestDto"/> (có <c>Decision</c>/<c>AdminNote</c>/<c>SuspendUntilUtc</c>).
        /// Vì vậy không thể mô phỏng input “reasonCode không tồn tại” đúng theo contract hiện tại.
        /// </summary>
        [Fact]
        public void UTCID05_Bug_AdminResolveComplianceAdminActionRequest_ReasonCodeNotFound_PerSpec()
        {
            LogUtcContext("UTCID05 [BUG — reasonCode mismatch]",
                "Spec: reasonCode không tồn tại → không xử lý.",
                "Product contract mismatch: ResolveAdminActionRequest không có field reasonCode trong DTO.",
                "Không thể assert nhánh “không tồn tại lý do phù hợp” cho case này trong unit test tầng controller.");

            Assert.Fail(
                "BUG UT07/UTCID05: Ma trận yêu cầu validate reasonCode, nhưng endpoint ResolveAdminActionRequest hiện không nhận reasonCode trong request body/parameters. " +
                "Nếu product muốn hỗ trợ case này, cần bổ sung contract (vd: thêm reasonCode vào DTO hoặc có endpoint khác) và validate ở service/controller; " +
                "sau đó mới cập nhật test để pass theo đúng nghiệp vụ.");
        }

        /// <summary>
        /// UTCID06 — ma trận: <c>reasonCode</c> null → không xử lý yêu cầu compliance (ma trận: Return <c>false</c>).
        /// <b>[BUG — mismatch contract]</b> Endpoint/controller này không nhận <c>reasonCode</c> từ request:
        /// <see cref="AdminComplianceStoryReportsController.ResolveAdminActionRequest(Guid, AdminResolveComplianceAdminActionRequestDto)"/>
        /// chỉ nhận <c>requestId</c> và DTO <see cref="AdminResolveComplianceAdminActionRequestDto"/> (có <c>Decision</c>/<c>AdminNote</c>/<c>SuspendUntilUtc</c>).
        /// Vì vậy không thể mô phỏng/verify nghiệp vụ “không tìm thấy lý do phù hợp” cho case <c>reasonCode = null</c> trong unit test hiện tại.
        /// </summary>
        [Fact]
        public void UTCID06_Bug_AdminResolveComplianceAdminActionRequest_ReasonCodeNull_PerSpec()
        {
            LogUtcContext("UTCID06 [BUG — reasonCode null mismatch]",
                "Spec: reasonCode null → không xử lý.",
                "Product contract mismatch: ResolveAdminActionRequest không có reasonCode trong DTO.",
                "Không thể assert nhánh 'Không tìm thấy lý do phù hợp' cho case này theo contract unit test hiện tại.");

            Assert.Fail(
                "BUG UT07/UTCID06: Ma trận yêu cầu validate reasonCode, nhưng endpoint ResolveAdminActionRequest hiện không nhận reasonCode trong request body/parameters. " +
                "Nếu product muốn hỗ trợ case này, cần bổ sung contract (vd: thêm reasonCode vào DTO hoặc cung cấp endpoint riêng) + validate ở service/controller; " +
                "sau đó cập nhật test để pass.");
        }

        /// <summary>
        /// UTCID07 — ma trận: <c>description</c> &gt; 200 ký tự → không xử lý yêu cầu compliance (ma trận: Return <c>false</c> + log &quot;kí tự quá dài&quot;).
        /// <b>[BUG — ánh xạ field]</b> Endpoint/controller trong UT07 đang resolve compliance theo
        /// <see cref="AdminResolveComplianceAdminActionRequestDto"/> (fields gồm <c>Decision</c>, <c>AdminNote</c>, <c>SuspendUntilUtc</c>),
        /// không có field <c>description</c> riêng. Mình ánh xạ <c>description</c> trong ma trận sang <c>AdminNote</c> để thử validate độ dài.
        /// Product hiện tại: controller/service chưa validate độ dài <c>AdminNote</c> (không thấy rule MaxLength &lt;= 200 ở DTO/service).
        /// Test: kỳ vọng reject trước khi gọi service; nếu vẫn gọi service và trả OK thì coi là bug.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UTCID07_Bug_AdminResolveComplianceAdminActionRequest_DescriptionTooLong_PerSpec()
        {
            LogUtcContext("UTCID07 [BUG — description/length]",
                "Spec: description > 200 → không xử lý.",
                "UT07 mapping: dùng AdminNote làm description.",
                "Kỳ vọng: controller reject trước khi gọi service (không cần assert message từng chữ).");

            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var controller = CreateSut(out var serviceMock);

            var body = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "APPROVE",
                // mô phỏng description > 200
                AdminNote = new string('x', 201),
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

            if (result is OkObjectResult ok && ok.StatusCode == StatusCodes.Status200OK)
            {
                _output.WriteLine("[BUG UT07/UTCID07] Backend chưa validate độ dài AdminNote/description > 200. Cần bổ sung check MaxLength(200) ở DTO/service/controller để trả BadRequest trước khi gọi service.");
                Assert.Fail("BUG UT07/UTCID07: Expected reject for description/AdminNote > 200, but API returned 200 OK.");
            }

            // Nếu future product reject đúng thì không gọi service.
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
        /// UTCID10 — ma trận: không tìm thấy story tương ứng với comment → không xử lý yêu cầu compliance (ma trận: Return false).
        /// <b>[BUG — spec gap / mismatch]</b> Endpoint UT07 đang resolve <i>story compliance admin action request</i> bằng
        /// <see cref="Services.Implementations.StoryReportService.AdminResolveComplianceAdminActionRequestAsync(Guid, Guid, AdminResolveComplianceAdminActionRequestDto)"/>
        /// và chỉ kiểm tra tồn tại requestId + trạng thái pending/approved/rejected + kind.
        /// Product hiện tại <b>không</b> validate “story_id của yêu cầu có tồn tại” (hoặc mapping “comment -> story”) ở tầng này.
        /// Vì vậy unit test hiện tại không thể đạt được nhánh “Không tìm thấy truyện” đúng theo spec bằng contract UT07.
        /// Test dưới đây cố tình mock service trả OK và <see cref="Assert.Fail(string)"/> để đánh dấu bug/thiếu validate.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UTCID10_Bug_AdminResolveComplianceAdminActionRequest_StoryNotFoundForComment_PerSpec()
        {
            LogUtcContext("UTCID10 [BUG — missing story for comment]",
                "Spec: không tìm thấy truyện tương ứng với comment → không xử lý.",
                "UT07 mismatch: ResolveAdminActionRequest không có commentId/storyId riêng để kiểm tra mapping.",
                "Product code check: AdminResolveComplianceAdminActionRequestAsync chỉ GetTrackedById(requestId), không load/validate story_id tồn tại.");

            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var controller = CreateSut(out var serviceMock);

            var body = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "APPROVE",
                AdminNote = "note",
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

            if (result is OkObjectResult ok && ok.StatusCode == StatusCodes.Status200OK)
            {
                _output.WriteLine(
                    "[BUG UT07/UTCID10] Backend hiện chưa validate 'story không tồn tại' theo spec. " +
                    "Cần bổ sung lookup/validate mapping story_id trước khi resolve và trả BadRequest/exception tương ứng.");
                Assert.Fail("BUG UT07/UTCID10: Expected reject when story for comment not found, but API returned 200 OK.");
            }
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
                AdminNote = "note",
                SuspendUntilUtc = DateTime.UtcNow.AddDays(2)
            };

            // Mô phỏng backend hiện không reject theo story.status (thiếu validate).
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

            if (result is OkObjectResult ok && ok.StatusCode == StatusCodes.Status200OK)
            {
                _output.WriteLine("[BUG UT07/UTCID12] Backend chưa validate story.status != PUBLISHED để reject theo spec. " +
                                 "Cần bổ sung lookup/validate story trước khi resolve và trả BadRequest/exception phù hợp.");
                Assert.Fail("BUG UT07/UTCID12: Expected reject when story not published, but API returned 200 OK.");
            }
        }

        /// <summary>
        /// UTCID13 — ma trận: reporter là chủ comment → không được tự báo cáo → không xử lý yêu cầu compliance (ma trận: Return <c>false</c> + log).
        /// <b>[BUG — contract mismatch]</b> Endpoint UT07 hiện tại là resolve <i>admin action request</i> (route theo <c>requestId</c> và DTO gồm
        /// <c>Decision/AdminNote/SuspendUntilUtc</c>) nên <b>không có</b> tham số để biểu diễn “comment owner vs reporterId”.
        /// Vì vậy unit test theo contract hiện không mô phỏng được self-report case đúng nghĩa; mình tạo bug placeholder:
        /// nếu controller vẫn trả <c>200 OK</c> (mock service OK) thì đánh dấu thiếu validate theo spec.
        /// </summary>
        [Fact]
        public async System.Threading.Tasks.Task UTCID13_Bug_AdminResolveComplianceAdminActionRequest_SelfReportCommentOwner_PerSpec()
        {
            LogUtcContext("UTCID13 [BUG — self report comment owner]",
                "Spec: reporter là chủ comment → không được phép xử lý.",
                "UT07 contract mismatch: ResolveAdminActionRequest không nhận commentId/reporterId để kiểm tra self-report.",
                "Kỳ vọng: reject; hiện tại (với mock service OK) controller có thể vẫn trả 200 → fail để đánh dấu bug.");

            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var controller = CreateSut(out var serviceMock);

            var body = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "APPROVE",
                AdminNote = "note",
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

            if (result is OkObjectResult ok && ok.StatusCode == StatusCodes.Status200OK)
            {
                _output.WriteLine("[BUG UT07/UTCID13] Endpoint UT07 không có contract để kiểm tra self-report theo spec. " +
                                 "Cần validate ở endpoint tương ứng khi tạo compliance request (request compliance) hoặc mở rộng contract cho resolve nếu thật sự cần.");
                Assert.Fail("BUG UT07/UTCID13: Expected reject for self-report comment owner, but API returned 200 OK.");
            }

            serviceMock.Verify(s => s.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, body), Times.Once);
        }
    }
}


//dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT07_FunctionAdminResolveComplianceAdminActionRequest"