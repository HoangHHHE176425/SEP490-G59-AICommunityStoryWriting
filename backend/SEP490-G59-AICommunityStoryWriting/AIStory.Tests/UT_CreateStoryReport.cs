using BusinessObjects.Entities;
using Moq;
using Services.DTOs.StoryReports;
using Services.Implementations;
using Services.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT_CreateStoryReport
    {
        private readonly ITestOutputHelper _output;

        public UT_CreateStoryReport(ITestOutputHelper output) => _output = output;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            ReferenceHandler = ReferenceHandler.IgnoreCycles
        };

        private void LogTestCase(string utcId, string spec, object? input, object? output, Exception? ex = null)
        {
            _output.WriteLine("");
            _output.WriteLine($"========== {utcId} ==========");
            _output.WriteLine($"SPEC   : {spec}");
            _output.WriteLine($"INPUT  : {JsonSerializer.Serialize(input, _jsonOptions)}");

            if (ex != null)
            {
                _output.WriteLine($"Exception type: {ex.GetType().Name}");
                _output.WriteLine($"Message: {ex.Message}");
            }
            else
            {
                _output.WriteLine($"RESULT : {JsonSerializer.Serialize(output, _jsonOptions)}");
            }
        }

        private static StoryReportService CreateSut(
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
                enableAdminActionNotifications: false);
        }

        private static string Words(int count) => string.Join(" ", Enumerable.Range(1, count).Select(i => $"word{i}"));

        [Fact]
        public void UTCID01_CreateStoryReportAsync_Success_WhenAllInputsValid()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var reportId = Guid.NewGuid();
            var req = new CreateStoryReportRequestDto { ReasonCode = "OTHER", Description = Words(50) };
            var story = new stories { id = storyId, status = "PUBLISHED", author_id = Guid.NewGuid(), title = "Story A" };
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);

            userLookupMock.Setup(x => x.Exists(reporterId)).Returns(true);
            userActivityMock.Setup(x => x.HasReadAnyChapterOfStory(reporterId, storyId)).Returns(true);
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(story);
            gatewayMock.Setup(x => x.AppendStoryReportAggregated(storyId, reporterId, "OTHER", req.Description.Trim())).Returns(reportId);

            // Act
            var result = sut.CreateStoryReportAsync(storyId, reporterId, req).GetAwaiter().GetResult();
            LogTestCase("UTCID01", "Đủ điều kiện tạo báo cáo truyện thành công.", new { storyId, reporterId, req }, result);

            // Assert
            Assert.Equal(reportId, result);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(storyId, reporterId, "OTHER", req.Description.Trim()), Times.Once);
        }

        [Fact]
        public void UTCID02_CreateStoryReportAsync_Fail_WhenReasonCodeInvalid()
        {
            // Arrange
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);
            var req = new CreateStoryReportRequestDto { ReasonCode = "INVALID_CODE", Description = Words(50) };

            // Act
            var ex = Record.Exception(() => sut.CreateStoryReportAsync(Guid.NewGuid(), Guid.NewGuid(), req).GetAwaiter().GetResult());
            LogTestCase("UTCID02", "ReasonCode không hợp lệ phải fail.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
            userActivityMock.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public void UTCID03_CreateStoryReportAsync_Fail_WhenReporterIdEmpty()
        {
            // Arrange
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);
            var req = new CreateStoryReportRequestDto { ReasonCode = "OTHER", Description = Words(50) };

            // Act
            var ex = Record.Exception(() => sut.CreateStoryReportAsync(Guid.NewGuid(), Guid.Empty, req).GetAwaiter().GetResult());
            LogTestCase("UTCID03", "ReporterId rỗng phải fail.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            userLookupMock.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userActivityMock.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public void UTCID04_CreateStoryReportAsync_Fail_WhenReporterNotExists()
        {
            // Arrange
            var reporterId = Guid.NewGuid();
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);
            var req = new CreateStoryReportRequestDto { ReasonCode = "OTHER", Description = Words(50) };
            userLookupMock.Setup(x => x.Exists(reporterId)).Returns(false);

            // Act
            var ex = Record.Exception(() => sut.CreateStoryReportAsync(Guid.NewGuid(), reporterId, req).GetAwaiter().GetResult());
            LogTestCase("UTCID04", "Reporter không tồn tại phải fail.", new { reporterId, req }, null, ex);

            // Assert
            Assert.NotNull(ex);
            userLookupMock.Verify(x => x.Exists(reporterId), Times.Once);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userActivityMock.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public void UTCID05_CreateStoryReportAsync_Fail_WhenStoryNotFound()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var req = new CreateStoryReportRequestDto { ReasonCode = "OTHER", Description = Words(50) };
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);

            userLookupMock.Setup(x => x.Exists(reporterId)).Returns(true);
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns((stories?)null);

            // Act
            var ex = Record.Exception(() => sut.CreateStoryReportAsync(storyId, reporterId, req).GetAwaiter().GetResult());
            LogTestCase("UTCID05", "Story không tồn tại phải fail.", new { storyId, reporterId, req }, null, ex);

            // Assert
            Assert.NotNull(ex);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userActivityMock.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public void UTCID06_CreateStoryReportAsync_Fail_WhenStoryNotPublished()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var req = new CreateStoryReportRequestDto { ReasonCode = "OTHER", Description = Words(50) };
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);

            userLookupMock.Setup(x => x.Exists(reporterId)).Returns(true);
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "DRAFT", author_id = Guid.NewGuid() });

            // Act
            var ex = Record.Exception(() => sut.CreateStoryReportAsync(storyId, reporterId, req).GetAwaiter().GetResult());
            LogTestCase("UTCID06", "Story chưa PUBLISHED phải fail.", new { storyId, reporterId, req }, null, ex);

            // Assert
            Assert.NotNull(ex);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userActivityMock.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public void UTCID07_CreateStoryReportAsync_Fail_WhenReporterHasNotReadAnyChapter()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var req = new CreateStoryReportRequestDto { ReasonCode = "OTHER", Description = Words(50) };
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);

            userLookupMock.Setup(x => x.Exists(reporterId)).Returns(true);
            userActivityMock.Setup(x => x.HasReadAnyChapterOfStory(reporterId, storyId)).Returns(false);
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED", author_id = Guid.NewGuid() });

            // Act
            var ex = Record.Exception(() => sut.CreateStoryReportAsync(storyId, reporterId, req).GetAwaiter().GetResult());
            LogTestCase("UTCID07", "User chưa đọc chapter nào phải fail.", new { storyId, reporterId, req }, null, ex);

            // Assert
            Assert.NotNull(ex);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void UTCID08_CreateStoryReportAsync_Fail_WhenSelfReportingOwnStory()
        {
            // Arrange
            var storyId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var req = new CreateStoryReportRequestDto { ReasonCode = "OTHER", Description = Words(50) };
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);

            userLookupMock.Setup(x => x.Exists(reporterId)).Returns(true);
            userActivityMock.Setup(x => x.HasReadAnyChapterOfStory(reporterId, storyId)).Returns(true);
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED", author_id = reporterId });

            // Act
            var ex = Record.Exception(() => sut.CreateStoryReportAsync(storyId, reporterId, req).GetAwaiter().GetResult());
            LogTestCase("UTCID08", "Tác giả tự report truyện mình phải fail.", new { storyId, reporterId, req }, null, ex);

            // Assert
            Assert.NotNull(ex);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public void UTCID09_CreateStoryReportAsync_Fail_WhenDescriptionTooFewWords()
        {
            // Arrange
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);
            var req = new CreateStoryReportRequestDto { ReasonCode = "OTHER", Description = Words(49) };

            // Act
            var ex = Record.Exception(() => sut.CreateStoryReportAsync(Guid.NewGuid(), Guid.NewGuid(), req).GetAwaiter().GetResult());
            LogTestCase("UTCID09", "Description < 50 từ phải fail.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
            userActivityMock.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }

        [Fact]
        public void UTCID10_CreateStoryReportAsync_Fail_WhenDescriptionTooLong()
        {
            // Arrange
            var sut = CreateSut(out var userLookupMock, out var userActivityMock, out var gatewayMock);
            var req = new CreateStoryReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = string.Join(" ", Enumerable.Repeat(new string('z', 200), 51))
            };

            // Act
            var ex = Record.Exception(() => sut.CreateStoryReportAsync(Guid.NewGuid(), Guid.NewGuid(), req).GetAwaiter().GetResult());
            LogTestCase("UTCID10", "Description vượt giới hạn ký tự phải fail.", req, null, ex);

            // Assert
            Assert.NotNull(ex);
            gatewayMock.Verify(x => x.AppendStoryReportAggregated(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            userLookupMock.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
            userActivityMock.Verify(x => x.HasReadAnyChapterOfStory(It.IsAny<Guid>(), It.IsAny<Guid>()), Times.Never);
        }
    }
}

// dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT_CreateStoryReport" --logger "console;verbosity=detailed"
