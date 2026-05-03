using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.Extensions.Logging;
using Moq;
using Services.DTOs.StoryReports;
using Services.Implementations;
using Services.Interfaces;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT_AdminResolveComplianceAdminActionRequest
    {
        public class TestLogger<T> : ILogger<T>
        {
            private readonly ITestOutputHelper _output;
            public TestLogger(ITestOutputHelper output) => _output = output;
            public IDisposable BeginScope<TState>(TState state) => null!;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => _output.WriteLine(formatter(state, exception));
        }

        private readonly ITestOutputHelper _output;

        public UT_AdminResolveComplianceAdminActionRequest(ITestOutputHelper output) => _output = output;

        /// <summary>Ghi lại nội dung log (để assert message thành công trong UTCID01).</summary>
        private sealed class CollectingLogger : ILogger<StoryReportService>
        {
            public List<string> Messages { get; } = new();
            public IDisposable BeginScope<TState>(TState state) => NullScope.Instance;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Messages.Add(formatter(state, exception));

            private sealed class NullScope : IDisposable
            {
                public static readonly NullScope Instance = new();
                public void Dispose() { }
            }
        }

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

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
                _output.WriteLine($"Exception type: {ex.GetType().Name}");
                _output.WriteLine($"Message: {ex.Message}");
            }
            else
            {
                _output.WriteLine("OUTPUT : SUCCESS");
                _output.WriteLine($"RESULT : {JsonSerializer.Serialize(output, _jsonOptions)}");
            }
        }

        private StoryReportService CreateSut(
            List<(Guid RequestId, Guid AdminId, string FinalStatus, string? Note, string Action)> resolveStore,
            out Mock<StoryReportService.IAdminComplianceAdminActionGateway> gatewayMock,
            ILogger<StoryReportService>? logger = null)
        {
            var userLookupMock = new Mock<IUserLookup>(MockBehavior.Strict);
            var userActivityLookupMock = new Mock<IUserActivityLookup>(MockBehavior.Strict);
            gatewayMock = new Mock<StoryReportService.IAdminComplianceAdminActionGateway>(MockBehavior.Strict);

            gatewayMock.Setup(x => x.CanUserResolveComplianceAdminAction(It.IsAny<Guid>())).Returns(true);
            gatewayMock
                .Setup(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()))
                .Callback((Guid requestId, Guid adminId, string finalStatus, string? note, string action) =>
                    resolveStore.Add((requestId, adminId, finalStatus, note, action)));

            var log = logger ?? new TestLogger<StoryReportService>(_output);
            return new StoryReportService(
                userLookupMock.Object,
                userActivityLookupMock.Object,
                notificationHubNotifier: null,
                emailService: null,
                adminComplianceGateway: gatewayMock.Object,
                enableAdminActionNotifications: false,
                logger: log);
        }

        /// <summary>
        /// UTCID01 – Admin resolve thành công: request tồn tại, PENDING, actor là ADMIN; ghi log &quot;Xử lý yêu cầu thành công&quot;.
        /// Kết quả nghiệp vụ: đơn được đánh dấu xử lý (MarkResolved); REJECT → DB status REJECTED (đơn không còn PENDING).
        /// </summary>
        [Fact]
        public async Task UTCID01_AdminResolveComplianceAdminActionRequest_Success_WhenRejectWithPendingRequest()
        {
            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var resolveStore = new List<(Guid RequestId, Guid AdminId, string FinalStatus, string? Note, string Action)>();
            var collectingLogger = new CollectingLogger();
            var sut = CreateSut(resolveStore, out var gatewayMock, collectingLogger);
            var resolutionNote = "reject by admin";
            var dto = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "REJECT",
                ReasonCode = "OTHER",
                AdminNote = resolutionNote
            };
            gatewayMock.Setup(x => x.GetTrackedById(requestId)).Returns(new compliance_admin_action_requests
            {
                id = requestId,
                story_id = storyId,
                target_user_id = Guid.NewGuid(),
                requester_id = Guid.NewGuid(),
                request_kind = ComplianceAdminActionRequestDAO.KindBanUser,
                status = ComplianceAdminActionRequestDAO.StatusPending
            });
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED", title = "T1", summary = "S1" });

            await sut.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, dto);

            var resolved = resolveStore[0];
            LogTestCase(
                utcId: "UTCID01",
                spec:
                "Precondition: requestId tồn tại; request PENDING; user thực hiện là ADMIN. " +
                "Input: requestId, adminId hợp lệ; resolutionNote (AdminNote) hợp lệ. " +
                "Service kiểm tra request tồn tại, status PENDING, quyền ADMIN, cập nhật trạng thái. " +
                "Expected: thành công; persistence final status REJECTED cho luồng REJECT; log: \"Xử lý yêu cầu thành công\".",
                input: new
                {
                    requestId,
                    adminId,
                    storyId,
                    dto.Decision,
                    dto.ReasonCode,
                    resolutionNote
                },
                output: new
                {
                    status = "Success",
                    resolvedStatus = "RESOLVED",
                    persistenceFinalStatus = resolved.FinalStatus,
                    resolutionAction = resolved.Action,
                    logMessageExpected = "Xử lý yêu cầu thành công",
                    markResolved = new
                    {
                        resolved.RequestId,
                        resolved.AdminId,
                        resolved.Note,
                    }
                },
                ex: null);

            Assert.Single(resolveStore);
            gatewayMock.Verify(x => x.CanUserResolveComplianceAdminAction(adminId), Times.Once);
            gatewayMock.Verify(x => x.MarkResolved(requestId, adminId, ComplianceAdminActionRequestDAO.StatusRejected, dto.AdminNote, "REJECT"), Times.Once);
        }

        /// <summary>UTCID02 – requestId hợp lệ nhưng không tồn tại (GetTrackedById = null): &quot;Không tìm thấy comment.&quot;</summary>
        [Fact]
        public async Task UTCID02_AdminResolveComplianceAdminActionRequest_Fail_WhenRequestNotFound()
        {
            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var resolveStore = new List<(Guid RequestId, Guid AdminId, string FinalStatus, string? Note, string Action)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            gatewayMock.Setup(x => x.GetTrackedById(requestId)).Returns((compliance_admin_action_requests?)null);
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "REJECT", ReasonCode = "OTHER", AdminNote = "note" };

            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, dto));
            LogTestCase(
                utcId: "UTCID02",
                spec: "requestId not found (không có bản ghi yêu cầu) → InvalidOperationException message: Không tìm thấy comment.; không MarkResolved.",
                input: new { requestId, adminId, dto.Decision, dto.AdminNote },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ioe = Assert.IsType<InvalidOperationException>(ex);
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        /// <summary>UTCID03 – API nhận requestId null → map Guid.Empty; message &quot;Không tìm thấy comment.&quot;</summary>
        [Fact]
        public async Task UTCID03_AdminResolveComplianceAdminActionRequest_Fail_WhenRequestIdNull()
        {
            Guid? requestId = null;
            var requestIdForService = requestId.GetValueOrDefault();
            var adminId = Guid.NewGuid();
            var resolveStore = new List<(Guid RequestId, Guid AdminId, string FinalStatus, string? Note, string Action)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "REJECT", AdminNote = "x" };

            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(requestIdForService, adminId, dto));
            LogTestCase(
                utcId: "UTCID03",
                spec: "requestId = null (service nhận Guid.Empty) → Không tìm thấy comment.; không GetTrackedById / không MarkResolved.",
                input: new { requestId = (Guid?)null, requestIdForService, adminId, dto.Decision, dto.AdminNote },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ioe = Assert.IsType<InvalidOperationException>(ex);
            Assert.Equal("Không tìm thấy comment.", ioe.Message);
            gatewayMock.Verify(x => x.GetTrackedById(It.IsAny<Guid>()), Times.Never);
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        /// <summary>UTCID04 – Admin/reporter (actor) null → Guid.Empty; message USER không tồn tại. (API C# dùng tham số adminId.)</summary>
        [Fact]
        public async Task UTCID04_AdminResolveComplianceAdminActionRequest_Fail_WhenActorUserIdNull()
        {
            var requestId = Guid.NewGuid();
            Guid? adminId = null;
            var adminIdForService = adminId.GetValueOrDefault();
            var resolveStore = new List<(Guid RequestId, Guid AdminId, string FinalStatus, string? Note, string Action)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "REJECT", ReasonCode = "OTHER", AdminNote = "note" };

            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(requestId, adminIdForService, dto));
            LogTestCase(
                utcId: "UTCID04",
                spec: "reporterId/adminId = null (service nhận Guid.Empty) → USER không tồn tại.; không CanUserResolve / không MarkResolved.",
                input: new { requestId, adminId = (Guid?)null, adminIdForService, dto.Decision, dto.ReasonCode, dto.AdminNote },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ioe = Assert.IsType<InvalidOperationException>(ex);
            Assert.Equal("USER không tồn tại.", ioe.Message);
            gatewayMock.Verify(x => x.CanUserResolveComplianceAdminAction(It.IsAny<Guid>()), Times.Never);
            gatewayMock.Verify(x => x.GetTrackedById(It.IsAny<Guid>()), Times.Never);
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        /// <summary>UTCID05 – ReasonCode không có trong catalog (not found).</summary>
        [Fact]
        public async Task UTCID05_AdminResolveComplianceAdminActionRequest_Fail_WhenReasonCodeNotFound()
        {
            var resolveStore = new List<(Guid RequestId, Guid AdminId, string FinalStatus, string? Note, string Action)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var dto = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "REJECT",
                ReasonCode = "NOT_A_VALID_REASON_CODE_XYZ",
                AdminNote = "note"
            };

            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, dto));
            LogTestCase(
                utcId: "UTCID05",
                spec: "ReasonCode không tồn tại trong StoryReportReasonCatalog → ArgumentException Invalid reason code.; không CanUserResolve / không MarkResolved.",
                input: new { requestId, adminId, dto.Decision, dto.ReasonCode, dto.AdminNote },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ax = Assert.IsType<ArgumentException>(ex);
            Assert.Equal("Invalid reason code.", ax.Message);
            gatewayMock.Verify(x => x.CanUserResolveComplianceAdminAction(It.IsAny<Guid>()), Times.Never);
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        /// <summary>UTCID06 – ReasonCode = null → Invalid reason code. (cùng nhánh với mã không có trong catalog).</summary>
        [Fact]
        public async Task UTCID06_AdminResolveComplianceAdminActionRequest_Fail_WhenReasonCodeNull()
        {
            var resolveStore = new List<(Guid RequestId, Guid AdminId, string FinalStatus, string? Note, string Action)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "REJECT", ReasonCode = null, AdminNote = "note" };

            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, dto));
            LogTestCase(
                utcId: "UTCID06",
                spec: "ReasonCode = null → ArgumentException Invalid reason code.; không CanUserResolve / không MarkResolved.",
                input: new { requestId, adminId, dto.Decision, reasonCode = (string?)null, dto.AdminNote },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ax = Assert.IsType<ArgumentException>(ex);
            Assert.Equal("Invalid reason code.", ax.Message);
            gatewayMock.Verify(x => x.CanUserResolveComplianceAdminAction(It.IsAny<Guid>()), Times.Never);
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        /// <summary>UTCID07 – Description vượt 200 ký tự (sau trim).</summary>
        [Fact]
        public async Task UTCID07_AdminResolveComplianceAdminActionRequest_Fail_WhenDescriptionTooLong()
        {
            var resolveStore = new List<(Guid RequestId, Guid AdminId, string FinalStatus, string? Note, string Action)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var dto = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "REJECT",
                ReasonCode = "OTHER",
                AdminNote = "note",
                Description = new string('x', 201)
            };

            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, dto));
            LogTestCase(
                utcId: "UTCID07",
                spec: "Description > 200 ký tự → ArgumentException Mô tả tối đa 200 ký tự.; không CanUserResolve / không MarkResolved.",
                input: new { requestId, adminId, dto.Decision, dto.ReasonCode, descriptionLength = dto.Description?.Length },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ax = Assert.IsType<ArgumentException>(ex);
            Assert.Equal("Mô tả tối đa 200 ký tự.", ax.Message);
            gatewayMock.Verify(x => x.CanUserResolveComplianceAdminAction(It.IsAny<Guid>()), Times.Never);
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        /// <summary>UTCID08 – Description = null (tùy chọn; không gây lỗi validation).</summary>
        [Fact]
        public async Task UTCID08_AdminResolveComplianceAdminActionRequest_Success_WhenDescriptionNull()
        {
            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var resolveStore = new List<(Guid RequestId, Guid AdminId, string FinalStatus, string? Note, string Action)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            var dto = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "REJECT",
                ReasonCode = "OTHER",
                AdminNote = "note",
                Description = null
            };
            gatewayMock.Setup(x => x.GetTrackedById(requestId)).Returns(new compliance_admin_action_requests
            {
                id = requestId,
                story_id = storyId,
                target_user_id = Guid.NewGuid(),
                requester_id = Guid.NewGuid(),
                request_kind = ComplianceAdminActionRequestDAO.KindBanUser,
                status = ComplianceAdminActionRequestDAO.StatusPending
            });
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED", title = "T", summary = "S" });

            await sut.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, dto);

            var resolved = resolveStore[0];
            LogTestCase(
                utcId: "UTCID08",
                spec: "Description = null (tùy chọn) → không lỗi độ dài; REJECT → MarkResolved REJECTED.",
                input: new { requestId, adminId, storyId, dto.Decision, dto.ReasonCode, description = (string?)null, dto.AdminNote },
                output: new { resolved.RequestId, resolved.FinalStatus, resolved.Action },
                ex: null);

            Assert.Single(resolveStore);
            gatewayMock.Verify(x => x.CanUserResolveComplianceAdminAction(adminId), Times.Once);
            gatewayMock.Verify(x => x.MarkResolved(requestId, adminId, ComplianceAdminActionRequestDAO.StatusRejected, dto.AdminNote, "REJECT"), Times.Once);
        }

        /// <summary>UTCID09 – Description chỉ gồm khoảng trắng (trim → rỗng; không vi phạm giới hạn độ dài).</summary>
        [Fact]
        public async Task UTCID09_AdminResolveComplianceAdminActionRequest_Success_WhenDescriptionWhitespaceOnly()
        {
            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var resolveStore = new List<(Guid RequestId, Guid AdminId, string FinalStatus, string? Note, string Action)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            const string whitespaceDescription = "   \t  ";
            var dto = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "REJECT",
                ReasonCode = "OTHER",
                AdminNote = "note",
                Description = whitespaceDescription
            };
            gatewayMock.Setup(x => x.GetTrackedById(requestId)).Returns(new compliance_admin_action_requests
            {
                id = requestId,
                story_id = storyId,
                target_user_id = Guid.NewGuid(),
                requester_id = Guid.NewGuid(),
                request_kind = ComplianceAdminActionRequestDAO.KindBanUser,
                status = ComplianceAdminActionRequestDAO.StatusPending
            });
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED", title = "T", summary = "S" });

            await sut.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, dto);

            var resolved = resolveStore[0];
            LogTestCase(
                utcId: "UTCID09",
                spec: "Description = khoảng trắng → sau Trim không tính độ dài > 200; REJECT → MarkResolved REJECTED.",
                input: new { requestId, adminId, storyId, dto.Decision, dto.ReasonCode, descriptionRaw = whitespaceDescription, dto.AdminNote },
                output: new { resolved.RequestId, resolved.FinalStatus, resolved.Action },
                ex: null);

            Assert.Single(resolveStore);
            gatewayMock.Verify(x => x.CanUserResolveComplianceAdminAction(adminId), Times.Once);
            gatewayMock.Verify(x => x.MarkResolved(requestId, adminId, ComplianceAdminActionRequestDAO.StatusRejected, dto.AdminNote, "REJECT"), Times.Once);
        }

        /// <summary>UTCID10 – Truyện chưa được PUBLISH (không cho resolve).</summary>
        [Fact]
        public async Task UTCID10_AdminResolveComplianceAdminActionRequest_Fail_WhenStoryNotPublished()
        {
            var requestId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var resolveStore = new List<(Guid RequestId, Guid AdminId, string FinalStatus, string? Note, string Action)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            gatewayMock.Setup(x => x.GetTrackedById(requestId)).Returns(new compliance_admin_action_requests
            {
                id = requestId,
                requester_id = Guid.NewGuid(),
                target_user_id = Guid.NewGuid(),
                story_id = storyId,
                request_kind = ComplianceAdminActionRequestDAO.KindBanUser,
                status = ComplianceAdminActionRequestDAO.StatusPending
            });
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "DRAFT" });
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "REJECT", ReasonCode = "OTHER", AdminNote = "note" };

            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(requestId, Guid.NewGuid(), dto));
            LogTestCase(
                utcId: "UTCID10",
                spec: "Truyện không ở trạng thái PUBLISHED → không MarkResolved.",
                input: new { requestId, storyId, storyStatus = "DRAFT", dto.Decision, dto.AdminNote },
                output: null,
                ex);

            Assert.NotNull(ex);
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        /// <summary>UTCID11 – Thời gian không hợp lệ (APPROVE suspend: mốc suspend không phải tương lai so với UTC hiện tại).</summary>
        [Fact]
        public async Task UTCID11_AdminResolveComplianceAdminActionRequest_Fail_WhenSuspendTimeInvalid()
        {
            var requestId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var resolveStore = new List<(Guid RequestId, Guid AdminId, string FinalStatus, string? Note, string Action)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            gatewayMock.Setup(x => x.GetTrackedById(requestId)).Returns(new compliance_admin_action_requests
            {
                id = requestId,
                story_id = storyId,
                target_user_id = Guid.NewGuid(),
                requester_id = Guid.NewGuid(),
                request_kind = ComplianceAdminActionRequestDAO.KindSuspendAuthorWriting,
                status = ComplianceAdminActionRequestDAO.StatusPending,
                proposed_suspend_until_utc = DateTime.UtcNow.AddHours(-2)
            });
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED" });
            var invalidUntilUtc = DateTime.UtcNow.AddHours(-1);
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "APPROVE", ReasonCode = "OTHER", AdminNote = "note", SuspendUntilUtc = invalidUntilUtc };

            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(requestId, Guid.NewGuid(), dto));
            LogTestCase(
                utcId: "UTCID11",
                spec: "Thời gian không hợp lệ: SuspendUntilUtc ≤ UtcNow → ArgumentException Cần SuspendUntilUtc hoặc đề xuất hợp lệ trong tương lai.; không MarkResolved.",
                input: new { requestId, storyId, dto.Decision, dto.AdminNote, SuspendUntilUtc = invalidUntilUtc },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ax = Assert.IsType<ArgumentException>(ex);
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        /// <summary>UTCID12 – Truyện chưa được public (không PUBLISHED): không cho resolve.</summary>
        [Fact]
        public async Task UTCID12_AdminResolveComplianceAdminActionRequest_Fail_WhenStoryNotPublic()
        {
            var requestId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var resolveStore = new List<(Guid RequestId, Guid AdminId, string FinalStatus, string? Note, string Action)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            gatewayMock.Setup(x => x.GetTrackedById(requestId)).Returns(new compliance_admin_action_requests
            {
                id = requestId,
                requester_id = Guid.NewGuid(),
                target_user_id = Guid.NewGuid(),
                story_id = storyId,
                request_kind = ComplianceAdminActionRequestDAO.KindBanUser,
                status = ComplianceAdminActionRequestDAO.StatusPending
            });
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "DRAFT" });
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "APPROVE", ReasonCode = "OTHER", AdminNote = "note" };

            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(requestId, Guid.NewGuid(), dto));
            LogTestCase(
                utcId: "UTCID12",
                spec: "Truyện chưa public (status ≠ PUBLISHED) → InvalidOperationException Truyện chưa được PUBLISH; không MarkResolved.",
                input: new { requestId, storyId, storyStatus = "DRAFT", dto.Decision, dto.ReasonCode, dto.AdminNote },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ioe = Assert.IsType<InvalidOperationException>(ex);
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        /// <summary>UTCID13 – APPROVE ban user: khóa tài khoản, sweep moderation, MarkResolved APPROVED / BAN_USER.</summary>
        [Fact]
        public async Task UTCID13_AdminResolveComplianceAdminActionRequest_Success_WhenApproveBanUser()
        {
            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();
            var resolveStore = new List<(Guid RequestId, Guid AdminId, string FinalStatus, string? Note, string Action)>();
            var bannedUsers = new List<Guid>();
            var sweepRunCount = 0;
            var sut = CreateSut(resolveStore, out var gatewayMock);
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "APPROVE", ReasonCode = "OTHER", AdminNote = "ban approved" };
            gatewayMock.Setup(x => x.GetTrackedById(requestId)).Returns(new compliance_admin_action_requests
            {
                id = requestId,
                story_id = storyId,
                target_user_id = targetUserId,
                requester_id = Guid.NewGuid(),
                request_kind = ComplianceAdminActionRequestDAO.KindBanUser,
                status = ComplianceAdminActionRequestDAO.StatusPending,
                message = "m"
            });
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED", title = "T", summary = "S" });
            gatewayMock.Setup(x => x.SetUserAccountStatus(targetUserId, "BANNED")).Callback(() => bannedUsers.Add(targetUserId));
            gatewayMock.Setup(x => x.RunBannedAuthorModerationSweep()).Callback(() => sweepRunCount++);
            gatewayMock.Setup(x => x.InsertViolationLog(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()));

            await sut.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, dto);

            var resolved = resolveStore[0];
            LogTestCase(
                utcId: "UTCID13",
                spec: "APPROVE ban user → BANNED, chạy sweep, MarkResolved APPROVED + BAN_USER.",
                input: new { requestId, adminId, storyId, targetUserId, dto.Decision, dto.AdminNote },
                output: new
                {
                    Resolve = new { resolved.RequestId, resolved.AdminId, resolved.FinalStatus, resolved.Note, resolved.Action },
                    BannedUserId = bannedUsers.Count > 0 ? bannedUsers[0] : (Guid?)null,
                    sweepRunCount
                },
                ex: null);

            Assert.Single(resolveStore);
            Assert.Single(bannedUsers);
            Assert.Equal(1, sweepRunCount);
            gatewayMock.Verify(x => x.MarkResolved(requestId, adminId, ComplianceAdminActionRequestDAO.StatusApproved, dto.AdminNote, "BAN_USER"), Times.Once);
        }

    }
}

// dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT_AdminResolveComplianceAdminActionRequest" --logger "console;verbosity=detailed"
