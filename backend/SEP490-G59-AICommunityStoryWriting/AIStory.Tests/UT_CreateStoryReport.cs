using BusinessObjects.Entities;
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
    public class UT_CreateStoryReport
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

        public UT_CreateStoryReport(ITestOutputHelper output) => _output = output;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        private void LogTestCase(string utcId, string spec, object? input, object? output, Exception? ex = null)
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
                _output.WriteLine($"RESULT : {JsonSerializer.Serialize(output, _jsonOptions)}");
            }
            else
            {
                _output.WriteLine("OUTPUT : SUCCESS");
                _output.WriteLine($"RESULT : {JsonSerializer.Serialize(output, _jsonOptions)}");
            }
        }

        private StoryReportService CreateSut(
            out Mock<IUserLookup> userLookupMock,
            out Mock<IUserActivityLookup> userActivityMock,
            out Mock<StoryReportService.ICreateStoryReportGateway> gatewayMock)
        {
            userLookupMock = new Mock<IUserLookup>(MockBehavior.Strict);
            userActivityMock = new Mock<IUserActivityLookup>(MockBehavior.Strict);
            gatewayMock = new Mock<StoryReportService.ICreateStoryReportGateway>(MockBehavior.Strict);

            return new StoryReportService(
                userLookupMock.Object,
                userActivityMock.Object,
                notificationHubNotifier: null,
                emailService: null,
                createStoryReportGateway: gatewayMock.Object,
                enableCreateStoryReportNotifications: false,
                adminComplianceGateway: null,
                enableAdminActionNotifications: false,
                logger: new TestLogger<StoryReportService>(_output));
        }

        private static string VietnameseDescription50Words() =>
            // 50 từ, tổng 199 ký tự để vừa điều kiện min 50 words và max 200 chars.
            string.Join(" ", Enumerable.Repeat("bao", 50));

        private static string VietnameseDescriptionTooLong() =>
            string.Join(" ", Enumerable.Repeat(
                "Nội dung báo cáo rất dài nhằm mô phỏng trường hợp người dùng nhập quá nhiều ký tự vượt giới hạn cho phép của hệ thống.",
                3));

        [Fact]
        public async Task UTCID01_CreateStoryReportAsync_Success_WhenAllInputsValid()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var reportId = Guid.NewGuid();
            var req = new CreateStoryReportRequestDto
            {
                ReasonCode = "SPAM_AD",
                Description = VietnameseDescription50Words()
            };
            var story = new stories
            {
                id = storyId,
                status = "PUBLISHED",
                author_id = Guid.NewGuid(),
                title = "Bóng Trăng Trên Thành Cổ"
            };
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);

            userLookupMock.Setup(x => x.Exists(reporterId)).Returns(true);
            userActivityMock.Setup(x => x.HasReadAnyChapterOfStory(reporterId, storyId)).Returns(true);
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(story);
            // Precondition UTCID01: reporter chưa từng report story này trước đó -> gateway trả id mới khác Guid.Empty.
            gatewayMock
                .Setup(x => x.AppendStoryReportAggregated(storyId, reporterId, "SPAM_AD", req.Description.Trim()))
                .Returns(reportId);

            // Act
            var result = await sut.CreateStoryReportAsync(storyId, reporterId, req);
            LogTestCase(
                "UTCID01",
                "Story tồn tại, user hợp lệ, chưa report trước đó -> tạo báo cáo truyện thành công.",
                new { storyId, reporterId, req, storyTitle = story.title },
                result);

            // Assert
            Assert.Equal(reportId, result.ReportId);
            Assert.NotEqual(Guid.Empty, result.ReportId);
            userLookupMock.Verify(x => x.Exists(reporterId), Times.Once);
            gatewayMock.Verify(x => x.GetStoryById(storyId), Times.Once);
            userActivityMock.Verify(x => x.HasReadAnyChapterOfStory(reporterId, storyId), Times.Once);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(storyId, reporterId, "SPAM_AD", req.Description.Trim()), Times.Once);
        }

        [Fact]
        public async Task UTCID02_CreateStoryReportAsync_Fail_WhenStoryNotFound()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);
            var req = new CreateStoryReportRequestDto { ReasonCode = "OTHER", Description = VietnameseDescription50Words() };
            userLookupMock.Setup(x => x.Exists(reporterId)).Returns(true);
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns((stories?)null);

            // Act
            var ex = await Record.ExceptionAsync(() => sut.CreateStoryReportAsync(storyId, reporterId, req));
            LogTestCase("UTCID02", "Story không tồn tại.", new { storyId, reporterId, req }, null, ex);

            // Assert
            Assert.NotNull(ex);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(reporterId), Times.Once);
            userActivityMock.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task UTCID03_CreateStoryReportAsync_Fail_WhenReporterNotExists()
        {
            // Arrange
            var reporterId = Guid.NewGuid();
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);
            var req = new CreateStoryReportRequestDto { ReasonCode = "OTHER", Description = VietnameseDescription50Words() };
            userLookupMock.Setup(x => x.Exists(reporterId)).Returns(false);

            // Act
            var ex = await Record.ExceptionAsync(() => sut.CreateStoryReportAsync(Guid.NewGuid(), reporterId, req));
            LogTestCase("UTCID03", "User không tồn tại.", new { reporterId, req }, null, ex);

            // Assert
            Assert.NotNull(ex);
            userLookupMock.Verify(x => x.Exists(reporterId), Times.Once);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userActivityMock.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task UTCID04_CreateStoryReportAsync_Fail_WhenReasonCodeInvalid()
        {
            // Arrange
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);
            var req = new CreateStoryReportRequestDto { ReasonCode = "SAI_MA_LY_DO", Description = VietnameseDescription50Words() };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.CreateStoryReportAsync(Guid.NewGuid(), Guid.NewGuid(), req));
            LogTestCase("UTCID04", "Không tồn tại lý do báo cáo phù hợp.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            userLookupMock.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userActivityMock.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task UTCID05_CreateStoryReportAsync_Fail_WhenDescriptionInvalid()
        {
            // Arrange
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);
            var req = new CreateStoryReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = "Tôi báo cáo truyện này vì có nhiều đoạn nội dung kích động bạo lực và ngôn từ thù ghét, ảnh hưởng tiêu cực đến cộng đồng người đọc trẻ tuổi trên nền tảng."
            };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.CreateStoryReportAsync(Guid.NewGuid(), Guid.NewGuid(), req));
            LogTestCase("UTCID05", "Description không hợp lệ (dưới 50 từ).", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
            userActivityMock.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task UTCID06_CreateStoryReportAsync_Fail_WhenDescriptionTooLong()
        {
            // Arrange
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);
            var req = new CreateStoryReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = VietnameseDescriptionTooLong()
            };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.CreateStoryReportAsync(Guid.NewGuid(), Guid.NewGuid(), req));
            LogTestCase("UTCID06", "Description vượt giới hạn ký tự.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
            userActivityMock.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task UTCID07_CreateStoryReportAsync_Fail_WhenUserNotLoggedIn()
        {
            // Arrange
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);
            var req = new CreateStoryReportRequestDto { ReasonCode = "OTHER", Description = VietnameseDescription50Words() };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.CreateStoryReportAsync(Guid.NewGuid(), Guid.Empty, req));
            LogTestCase("UTCID07", "User chưa đăng nhập không tạo được báo cáo.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
            userActivityMock.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task UTCID08_CreateStoryReportAsync_Fail_WhenDescriptionWhitespaceOnly()
        {
            // Arrange
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);
            var req = new CreateStoryReportRequestDto { ReasonCode = "OTHER", Description = "   \t   " };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.CreateStoryReportAsync(Guid.NewGuid(), Guid.NewGuid(), req));
            LogTestCase("UTCID08", "Mô tả toàn khoảng trắng.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
            userActivityMock.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public async Task UTCID09_CreateStoryReportAsync_Fail_WhenReporterAlreadyReportedStory()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var req = new CreateStoryReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = VietnameseDescription50Words()
            };
            var story = new stories { id = storyId, status = "PUBLISHED", author_id = Guid.NewGuid(), title = "Đêm Mưa Thành Cũ" };
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);

            userLookupMock.Setup(x => x.Exists(reporterId)).Returns(true);
            userActivityMock.Setup(x => x.HasReadAnyChapterOfStory(reporterId, storyId)).Returns(true);
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(story);
            // DAO trả Guid.Empty khi user đã report story này trước đó.
            gatewayMock.Setup(x => x.AppendStoryReportAggregated(storyId, reporterId, "OTHER", req.Description.Trim()))
                .Returns(Guid.Empty);

            // Act
            var ex = await Record.ExceptionAsync(() => sut.CreateStoryReportAsync(storyId, reporterId, req));
            LogTestCase("UTCID09", "Đã báo cáo truyện này rồi.", new { storyId, reporterId, req }, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.IsType<InvalidOperationException>(ex);
            Assert.Contains("đã báo cáo truyện này trước đó", ex.Message, StringComparison.OrdinalIgnoreCase);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(storyId, reporterId, "OTHER", req.Description.Trim()), Times.Once);
        }

        [Fact]
        public async Task UTCID10_CreateStoryReportAsync_Fail_WhenStoryNotPublic()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var req = new CreateStoryReportRequestDto { ReasonCode = "OTHER", Description = VietnameseDescription50Words() };
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);

            userLookupMock.Setup(x => x.Exists(reporterId)).Returns(true);
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "DRAFT", author_id = Guid.NewGuid() });

            // Act
            var ex = await Record.ExceptionAsync(() => sut.CreateStoryReportAsync(storyId, reporterId, req));
            LogTestCase("UTCID10", "Truyện chưa được public (PUBLISHED) phải fail.", new { storyId, reporterId, req }, null, ex);

            // Assert
            Assert.NotNull(ex);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(reporterId), Times.Once);
            userActivityMock.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

    }
}

// dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT_CreateStoryReport" --logger "console;verbosity=detailed"
