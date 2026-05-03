using BusinessObjects.Entities;
using Microsoft.Extensions.Logging;
using Moq;
using Services.DTOs.CommentReports;
using Services.Implementations;
using Services.Interfaces;
using Services.StoryReporting;
using System.Collections.Generic;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT_CreateCommentReport
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

        public UT_CreateCommentReport(ITestOutputHelper output) => _output = output;

        /// <summary>JSON indent cho log test (dễ đọc trong Standard Output).</summary>
        private static readonly JsonSerializerOptions _jsonOptionsLog = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
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
            _output.WriteLine("INPUT  :");
            _output.WriteLine(JsonSerializer.Serialize(input, _jsonOptionsLog));

            if (ex != null)
            {
                _output.WriteLine("OUTPUT : ERROR");
                _output.WriteLine($"         Type    : {ex.GetType().Name}");
                _output.WriteLine($"         Message : {ex.Message}");
            }
            else
            {
                _output.WriteLine("OUTPUT : SUCCESS");
                _output.WriteLine("RESULT :");
                _output.WriteLine(JsonSerializer.Serialize(output, _jsonOptionsLog));
            }
        }

        /// <summary>
        /// 50 từ (tiếng Việt + từ viết tắt hay gặp trong báo cáo thật), vừa đủ <see cref="UserReportDescriptionRules.MinWords"/> từ và tối đa <see cref="UserReportDescriptionRules.MaxLength"/> ký tự.
        /// </summary>
        private static readonly string[] CommentReportDescriptionTokens50 =
        {
            "Tôi", "báo", "cáo", "bình", "luận", "spam", "quấy", "rối", "xúc", "phạm", "sai", "lệ", "link", "QC", "sai",
            "nhờ", "ban", "xử", "lẹ", "độc", "giả", "lời", "thô", "kích", "ác", "tin", "sai", "hại", "gửi", "URL", "lạ", "chèn", "chữ", "sao", "chép", "QP", "quy", "tắc", "CD", "nhờ", "sớm", "chặn", "xử", "phạt", "gấp", "để", "răn", "đe", "trừ", "khác"
        };

        private static readonly string ValidCommentReportDescription =
            string.Join(" ", CommentReportDescriptionTokens50);

        /// <summary>Mô tả chỉ 49 từ — dùng cho case fail độ dài.</summary>
        private static readonly string TooShortCommentReportDescription =
            string.Join(" ", CommentReportDescriptionTokens50.Take(49));

        /// <summary>Vượt <see cref="UserReportDescriptionRules.MaxLength"/> nhưng vẫn đủ <see cref="UserReportDescriptionRules.MinWords"/> từ (thêm ký tự vào cuối không tách thêm từ).</summary>
        private static readonly string TooLongCommentReportDescription =
            ValidCommentReportDescription + "x";

        static UT_CreateCommentReport()
        {
            var t = ValidCommentReportDescription.Trim();
            if (UserReportDescriptionRules.CountWords(t) < UserReportDescriptionRules.MinWords
                || t.Length > UserReportDescriptionRules.MaxLength)
                throw new InvalidOperationException(
                    $"Fixture mô tả hợp lệ sai quy tắc: độ dài={t.Length}, số từ={UserReportDescriptionRules.CountWords(t)}.");

            var shortText = TooShortCommentReportDescription.Trim();
            if (UserReportDescriptionRules.CountWords(shortText) >= UserReportDescriptionRules.MinWords)
                throw new InvalidOperationException("Fixture mô tả quá ngắn phải dưới 50 từ.");

            var longText = TooLongCommentReportDescription.Trim();
            if (longText.Length <= UserReportDescriptionRules.MaxLength
                || UserReportDescriptionRules.CountWords(longText) < UserReportDescriptionRules.MinWords)
                throw new InvalidOperationException(
                    $"Fixture mô tả quá dài sai quy tắc: độ dài={longText.Length}, số từ={UserReportDescriptionRules.CountWords(longText)}.");
        }

        private CommentReportService CreateSut(
            List<reports> reportStore,
            List<report_evidences> evidenceStore,
            out Mock<IUserLookup> userLookupMock,
            out Mock<CommentReportService.ICreateCommentReportGateway> gatewayMock,
            ILogger<CommentReportService>? logger = null)
        {
            userLookupMock = new Mock<IUserLookup>(MockBehavior.Strict);
            gatewayMock = new Mock<CommentReportService.ICreateCommentReportGateway>(MockBehavior.Strict);

            userLookupMock.Setup(x => x.Exists(It.IsAny<Guid>())).Returns(true);
            gatewayMock.Setup(x => x.DisposeAsync()).Returns(ValueTask.CompletedTask);
            gatewayMock.Setup(x => x.HasReporterEvidenceAsync(It.IsAny<Guid>(), It.IsAny<string>())).ReturnsAsync(false);
            gatewayMock.Setup(x => x.HasLegacyReporterAsync(It.IsAny<Guid>(), It.IsAny<Guid>())).ReturnsAsync(false);
            gatewayMock.Setup(x => x.AddReport(It.IsAny<reports>()))
                .Callback((reports r) => reportStore.Add(r));
            gatewayMock.Setup(x => x.AddReportEvidence(It.IsAny<report_evidences>()))
                .Callback((report_evidences e) => evidenceStore.Add(e));
            gatewayMock.Setup(x => x.SaveChangesAsync()).Returns(Task.CompletedTask);
            var gateway = gatewayMock.Object;

            return new CommentReportService(
                userLookupMock.Object,
                notificationHubNotifier: null,
                createCommentReportGatewayFactory: () => gateway,
                enableCreateReportNotifications: false,
                logger: logger ?? new TestLogger<CommentReportService>(_output));
        }

        /// <summary>
        /// UTCID01 – luồng thành công: gọi trực tiếp <see cref="CommentReportService.CreateCommentReportAsync"/>.
        /// Điều kiện: bình luận và truyện tồn tại; truyện đã xuất bản (PUBLISHED); người báo cáo đăng nhập hợp lệ;
        /// chưa từng báo cáo bình luận này; người báo cáo không phải chủ bình luận; mã lý do và mô tả hợp lệ (≥ 50 từ, ≤ 200 ký tự sau trim).
        /// Kết quả: <see cref="Guid"/> báo cáo khác Empty; nhật ký service ghi Tạo báo cáo thành công kèm ReportId và ngữ cảnh (xem Standard Output khi chạy test).
        /// </summary>
        [Fact]
        public void UTCID01_CreateCommentReportAsync_Success_WhenValidInput()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var commentOwnerId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            Assert.NotEqual(commentOwnerId, reporterId);

            var description = ValidCommentReportDescription;
            var trimmedDescription = description.Trim();
            Assert.True(trimmedDescription.Length <= UserReportDescriptionRules.MaxLength);

            var request = new CreateCommentReportRequestDto
            {
                ReasonCode = "SPAM_AD",
                Description = description
            };

            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out var userLookupMock, out var gatewayMock);

            gatewayMock.Setup(x => x.GetCommentById(commentId)).Returns(new comments
            {
                id = commentId,
                story_id = storyId,
                user_id = commentOwnerId
            });
            gatewayMock.Setup(x => x.GetUserRoleAsync(commentOwnerId)).ReturnsAsync("USER");
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED" });
            gatewayMock.Setup(x => x.FindOpenGroupedReportAsync(commentId, "SPAM_AD")).ReturnsAsync((reports?)null);

            // Act
            var reportId = sut.CreateCommentReportAsync(commentId, reporterId, request, expectedStoryId: storyId).GetAwaiter().GetResult();
            LogTestCase(
                utcId: "UTCID01",
                spec: "Tạo báo cáo bình luận thành công: bình luận và truyện tồn tại, truyện đã xuất bản (PUBLISHED), người báo cáo hợp lệ, chưa từng báo cáo bình luận này, người báo cáo không phải chủ bình luận. Service ghi nhật ký: Tạo báo cáo thành công kèm ReportId, CommentId, StoryId, ReporterId, ReasonCode.",
                input: new
                {
                    commentId,
                    reporterId,
                    storyId,
                    request.ReasonCode,
                    request.Description,
                    descriptionWordCount = UserReportDescriptionRules.CountWords(trimmedDescription),
                    descriptionLength = trimmedDescription.Length
                },
                output: new { ReportId = reportId },
                ex: null);

            // Assert
            Assert.NotEqual(Guid.Empty, reportId);
            Assert.Single(reportStore);
            Assert.Single(evidenceStore);
            Assert.Equal(reportId, reportStore[0].id);
            gatewayMock.Verify(x => x.GetCommentById(commentId), Times.Once);
            gatewayMock.Verify(x => x.GetStoryById(storyId), Times.Once);
            gatewayMock.Verify(x => x.HasReporterEvidenceAsync(commentId, reporterId.ToString()), Times.Once);
            gatewayMock.Verify(x => x.HasLegacyReporterAsync(commentId, reporterId), Times.Once);
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Once);
            gatewayMock.Verify(x => x.AddReportEvidence(It.IsAny<report_evidences>()), Times.Once);
            userLookupMock.Verify(x => x.Exists(reporterId), Times.Once);
        }

        /// <summary>
        /// UTCID02 – Id có dạng hợp lệ nhưng bình luận không tồn tại (<c>GetCommentById</c> trả về null).
        /// </summary>
        [Fact]
        public void UTCID02_CreateCommentReportAsync_Fail_WhenCommentNotFound()
        {
            var commentId = Guid.NewGuid();
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            gatewayMock.Setup(x => x.GetCommentById(commentId)).Returns((comments?)null);
            var reporterId = Guid.NewGuid();
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ValidCommentReportDescription };

            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, reporterId, request).GetAwaiter().GetResult());
            LogTestCase(
                utcId: "UTCID02",
                spec: "Không tìm thấy bình luận trong hệ thống (GetCommentById = null) → không tạo báo cáo.",
                input: new { commentId, reporterId, request.ReasonCode, request.Description },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ioe = Assert.IsType<InvalidOperationException>(ex);
            gatewayMock.Verify(x => x.GetCommentById(commentId), Times.Once);
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        /// <summary>
        /// UTCID03 – <c>commentId</c> null / rỗng: service nhận <see cref="Guid.Empty"/> → từ chối trước khi truy vấn DB.
        /// </summary>
        [Fact]
        public void UTCID03_CreateCommentReportAsync_Fail_WhenCommentIdNull()
        {
            Guid? commentId = null;
            var commentIdForService = commentId.GetValueOrDefault();
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            var reporterId = Guid.NewGuid();
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ValidCommentReportDescription };

            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentIdForService, reporterId, request).GetAwaiter().GetResult());
            LogTestCase(
                utcId: "UTCID03",
                spec: "commentId null (tương đương Guid.Empty) → Không tìm thấy comment, không tạo báo cáo.",
                input: new { commentId, reporterId, request.ReasonCode, request.Description },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ioe = Assert.IsType<InvalidOperationException>(ex);
            gatewayMock.Verify(x => x.GetCommentById(It.IsAny<Guid>()), Times.Never);
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        /// <summary>UTCID04 – User (người báo cáo) không tồn tại trong hệ thống.</summary>
        [Fact]
        public void UTCID04_CreateCommentReportAsync_Fail_WhenReporterUserNotFound()
        {
            var reporterId = Guid.NewGuid();
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out var userLookupMock, out var gatewayMock);
            userLookupMock.Reset();
            userLookupMock.Setup(x => x.Exists(reporterId)).Returns(false);
            var commentId = Guid.NewGuid();
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ValidCommentReportDescription };

            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, reporterId, request).GetAwaiter().GetResult());
            LogTestCase(
                utcId: "UTCID04",
                spec: "User không tồn tại — người báo cáo không có trong hệ thống (IUserLookup.Exists = false) → USER không tồn tại, không tạo báo cáo.",
                input: new { commentId, reporterId, request.ReasonCode, request.Description },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ioe = Assert.IsType<InvalidOperationException>(ex);
            userLookupMock.Verify(x => x.Exists(reporterId), Times.Once);
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        /// <summary>
        /// UTCID05 – Mã lý do không tồn tại trong <see cref="StoryReporting.CommentReportReasonCatalog"/> (không có lý do báo cáo phù hợp).
        /// </summary>
        [Fact]
        public void UTCID05_CreateCommentReportAsync_Fail_WhenReportReasonCodeNotInCatalog()
        {
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var unknownReasonCode = "NOT_IN_COMMENT_REPORT_CATALOG";
            var request = new CreateCommentReportRequestDto { ReasonCode = unknownReasonCode, Description = ValidCommentReportDescription };

            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, reporterId, request).GetAwaiter().GetResult());
            LogTestCase(
                utcId: "UTCID05",
                spec: "Không tồn tại lý do báo cáo phù hợp — ReasonCode không có trong CommentReportReasonCatalog (chỉ COPYRIGHT, SEXUAL_EXPLICIT, … OTHER, v.v.) → ArgumentException Invalid reason code., không tạo báo cáo.",
                input: new { commentId, reporterId, reasonCode = unknownReasonCode, request.Description },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ae = Assert.IsType<ArgumentException>(ex);
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        /// <summary>UTCID06 – <c>ReasonCode</c> null → không tìm thấy lý do phù hợp trong catalog.</summary>
        [Fact]
        public void UTCID06_CreateCommentReportAsync_Fail_WhenReasonCodeNull()
        {
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var request = new CreateCommentReportRequestDto { ReasonCode = null!, Description = ValidCommentReportDescription };

            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, reporterId, request).GetAwaiter().GetResult());
            LogTestCase(
                utcId: "UTCID06",
                spec: "ReasonCode = null — không tìm thấy lý do báo cáo phù hợp (CommentReportReasonCatalog.TryGet) → ArgumentException Invalid reason code., không tạo báo cáo.",
                input: new { commentId, reporterId, reasonCode = (string?)null, request.Description },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ae = Assert.IsType<ArgumentException>(ex);
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        /// <summary>UTCID07 – Mô tả sau trim vượt <see cref="UserReportDescriptionRules.MaxLength"/> ký tự (đã đủ số từ tối thiểu).</summary>
        [Fact]
        public void UTCID07_CreateCommentReportAsync_Fail_WhenDescriptionTooLong()
        {
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var desc = TooLongCommentReportDescription;
            var trimmed = desc.Trim();
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = desc };

            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, reporterId, request).GetAwaiter().GetResult());
            LogTestCase(
                utcId: "UTCID07",
                spec: $"Mô tả quá dài (sau trim > {UserReportDescriptionRules.MaxLength} ký tự; đủ {UserReportDescriptionRules.MinWords} từ) → ArgumentException, không tạo báo cáo.",
                input: new
                {
                    commentId,
                    reporterId,
                    request.ReasonCode,
                    request.Description,
                    descriptionCharCount = trimmed.Length,
                    descriptionWordCount = UserReportDescriptionRules.CountWords(trimmed)
                },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ae = Assert.IsType<ArgumentException>(ex);
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        /// <summary>UTCID08 – Tạo báo cáo với <c>Description</c> null (thiếu mô tả).</summary>
        [Fact]
        public void UTCID08_CreateCommentReportAsync_Fail_WhenDescriptionNull()
        {
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = null };

            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, reporterId, request).GetAwaiter().GetResult());
            LogTestCase(
                utcId: "UTCID08",
                spec: "Tạo báo cáo nhưng Description = null → ValidateDescription: vui lòng nhập mô tả, không tạo báo cáo.",
                input: new { commentId, reporterId, request.ReasonCode, description = (string?)null },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ae = Assert.IsType<ArgumentException>(ex);
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        /// <summary>UTCID09 – <c>Description</c> chỉ gồm khoảng trắng (sau trim rỗng).</summary>
        [Fact]
        public void UTCID09_CreateCommentReportAsync_Fail_WhenDescriptionWhitespaceOnly()
        {
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            var whitespaceOnly = " \t\r\n \u00A0 "; // NBSP + spaces/tab/newline — trim về rỗng
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = whitespaceOnly };

            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, reporterId, request).GetAwaiter().GetResult());
            LogTestCase(
                utcId: "UTCID09",
                spec: "Description = chỉ khoảng trắng → ValidateDescription coi như thiếu mô tả, không tạo báo cáo.",
                input: new { commentId, reporterId, request.ReasonCode, description = whitespaceOnly, trimmedLength = whitespaceOnly.Trim().Length },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ae = Assert.IsType<ArgumentException>(ex);
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        /// <summary>UTCID10 – <c>story_id</c> trên bình luận trỏ tới truyện không có trong DB (<see cref="CommentReportService.ICreateCommentReportGateway.GetStoryById"/> trả null).</summary>
        [Fact]
        public void UTCID10_CreateCommentReportAsync_Fail_WhenStoryIdDoesNotExist()
        {
            var commentId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            gatewayMock.Setup(x => x.GetCommentById(commentId)).Returns(new comments { id = commentId, story_id = storyId, user_id = Guid.NewGuid() });
            gatewayMock.Setup(x => x.GetUserRoleAsync(It.IsAny<Guid>())).ReturnsAsync("USER");
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns((stories?)null);
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ValidCommentReportDescription };

            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, reporterId, request).GetAwaiter().GetResult());
            LogTestCase(
                utcId: "UTCID10",
                spec: "storyId không tồn tại — bình luận gắn story_id nhưng GetStoryById(storyId) = null → Story not found., không tạo báo cáo.",
                input: new { commentId, reporterId, storyId, request.ReasonCode, request.Description },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ioe = Assert.IsType<InvalidOperationException>(ex);
            gatewayMock.Verify(x => x.GetStoryById(storyId), Times.Once);
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        /// <summary>UTCID11 – Đã báo cáo bình luận/trường hợp này trước đó (trùng người báo cáo).</summary>
        [Fact]
        public void UTCID11_CreateCommentReportAsync_Fail_WhenAlreadyReportedThisComment()
        {
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            gatewayMock.Setup(x => x.GetCommentById(commentId)).Returns(new comments { id = commentId, story_id = storyId, user_id = Guid.NewGuid() });
            gatewayMock.Setup(x => x.GetUserRoleAsync(It.IsAny<Guid>())).ReturnsAsync("USER");
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED" });
            gatewayMock.Setup(x => x.HasReporterEvidenceAsync(commentId, reporterId.ToString())).ReturnsAsync(true);
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ValidCommentReportDescription };

            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, reporterId, request).GetAwaiter().GetResult());
            LogTestCase(
                utcId: "UTCID11",
                spec: "Bạn đã báo cáo truyện này rồi (cùng bình luận, cùng người báo cáo; HasReporterEvidenceAsync = true)",
                input: new { commentId, reporterId, storyId, request.ReasonCode, request.Description },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ioe = Assert.IsType<InvalidOperationException>(ex);
            gatewayMock.Verify(x => x.HasReporterEvidenceAsync(commentId, reporterId.ToString()), Times.Once);
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        /// <summary>UTCID12 – Truyện chưa public (không PUBLISHED): không được báo cáo bình luận.</summary>
        [Fact]
        public void UTCID12_CreateCommentReportAsync_Fail_WhenStoryNotPublished()
        {
            var commentId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            gatewayMock.Setup(x => x.GetCommentById(commentId)).Returns(new comments { id = commentId, story_id = storyId, user_id = Guid.NewGuid() });
            gatewayMock.Setup(x => x.GetUserRoleAsync(It.IsAny<Guid>())).ReturnsAsync("USER");
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "DRAFT" });
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ValidCommentReportDescription };

            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, reporterId, request).GetAwaiter().GetResult());
            LogTestCase(
                utcId: "UTCID12",
                spec: "Truyện chưa được public (status ≠ PUBLISHED, ví dụ DRAFT)",
                input: new { commentId, reporterId, storyId, storyStatus = "DRAFT", request.ReasonCode, request.Description },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ioe = Assert.IsType<InvalidOperationException>(ex);
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        /// <summary>UTCID13 – Báo cáo bình luận của chính mình: service từ chối (reporter = chủ bình luận).</summary>
        [Fact]
        public void UTCID13_CreateCommentReportAsync_Fail_WhenReporterIsCommentOwner()
        {
            var commentId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            gatewayMock.Setup(x => x.GetCommentById(commentId)).Returns(new comments
            {
                id = commentId,
                story_id = storyId,
                user_id = reporterId
            });
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ValidCommentReportDescription };

            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, reporterId, request).GetAwaiter().GetResult());
            LogTestCase(
                utcId: "UTCID13",
                spec: "Không thể báo cáo bình luận của chính mình (reporterId = user_id của bình luận) → message service: Bạn không thể báo cáo bình luận của chính mình.",
                input: new { commentId, reporterId, storyId, request.ReasonCode, request.Description },
                output: null,
                ex);

            Assert.NotNull(ex);
            var ioe = Assert.IsType<InvalidOperationException>(ex);
            Assert.Equal("Bạn không thể báo cáo bình luận của chính mình.", ioe.Message);
            gatewayMock.Verify(x => x.GetCommentById(commentId), Times.Once);
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

    }
}

// dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT_CreateCommentReport" --logger "console;verbosity=detailed"