using BusinessObjects.Entities;
using Moq;
using Services.DTOs.CommentReports;
using Services.Implementations;
using Services.Interfaces;
using Services.StoryReporting;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT_CreateCommentReport
    {
        private readonly ITestOutputHelper _output;

        public UT_CreateCommentReport(ITestOutputHelper output) => _output = output;

        private void LogTestCase(string utcId, object? input, object? output, Exception? ex = null)
        {
            _output.WriteLine("");
            _output.WriteLine($"========== {utcId} ==========");
            _output.WriteLine($"INPUT  : {JsonSerializer.Serialize(input)}");
            if (ex != null)
            {
                _output.WriteLine("OUTPUT : ERROR");
                _output.WriteLine($"TYPE   : {ex.GetType().Name}");
                _output.WriteLine($"MSG    : {ex.Message}");
                return;
            }

            _output.WriteLine("OUTPUT : SUCCESS");
            _output.WriteLine($"RESULT : {JsonSerializer.Serialize(output)}");
        }

        private static string ReportDescriptionWords(int count) =>
            string.Join(" ", Enumerable.Range(1, count).Select(i => $"w{i}"));

        private static CommentReportService CreateSut(
            List<reports> reportStore,
            List<report_evidences> evidenceStore,
            out Mock<IUserLookup> userLookupMock,
            out Mock<CommentReportService.ICreateCommentReportGateway> gatewayMock)
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
                enableCreateReportNotifications: false);
        }

        [Fact]
        public void UTCID01_CreateCommentReportAsync_Success_WhenValidInput()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var request = new CreateCommentReportRequestDto
            {
                ReasonCode = "OTHER",
                Description = ReportDescriptionWords(50)
            };

            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out var userLookupMock, out var gatewayMock);

            gatewayMock.Setup(x => x.GetCommentById(commentId)).Returns(new comments
            {
                id = commentId,
                story_id = storyId,
                user_id = Guid.NewGuid()
            });
            gatewayMock.Setup(x => x.GetUserRoleAsync(It.IsAny<Guid>())).ReturnsAsync("USER");
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED" });
            gatewayMock.Setup(x => x.FindOpenGroupedReportAsync(commentId, "OTHER")).ReturnsAsync((reports?)null);

            // Act
            var reportId = sut.CreateCommentReportAsync(commentId, reporterId, request, expectedStoryId: storyId).GetAwaiter().GetResult();
            LogTestCase("UTCID01", new { commentId, reporterId, storyId, request.ReasonCode }, new { reportId });

            // Assert
            Assert.NotEqual(Guid.Empty, reportId);
            Assert.Single(reportStore);
            Assert.Single(evidenceStore);
            Assert.Equal(reportId, reportStore[0].id);
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Once);
            gatewayMock.Verify(x => x.AddReportEvidence(It.IsAny<report_evidences>()), Times.Once);
            userLookupMock.Verify(x => x.Exists(reporterId), Times.Once);
        }

        [Fact]
        public void UTCID02_CreateCommentReportAsync_Fail_WhenCommentIdEmpty()
        {
            // Arrange
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ReportDescriptionWords(50) };

            // Act
            var ex = Record.Exception(() => sut.CreateCommentReportAsync(Guid.Empty, Guid.NewGuid(), request).GetAwaiter().GetResult());

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        [Fact]
        public void UTCID03_CreateCommentReportAsync_Fail_WhenReasonCodeInvalid()
        {
            // Arrange
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            var request = new CreateCommentReportRequestDto { ReasonCode = "INVALID", Description = ReportDescriptionWords(50) };

            // Act
            var ex = Record.Exception(() => sut.CreateCommentReportAsync(Guid.NewGuid(), Guid.NewGuid(), request).GetAwaiter().GetResult());

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        [Fact]
        public void UTCID04_CreateCommentReportAsync_Fail_WhenReporterIdEmpty()
        {
            // Arrange
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out var userLookupMock, out var gatewayMock);
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ReportDescriptionWords(50) };

            // Act
            var ex = Record.Exception(() => sut.CreateCommentReportAsync(Guid.NewGuid(), Guid.Empty, request).GetAwaiter().GetResult());

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            userLookupMock.Verify(x => x.Exists(It.IsAny<Guid>()), Times.Never);
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        [Fact]
        public void UTCID05_CreateCommentReportAsync_Fail_WhenReporterNotFound()
        {
            // Arrange
            var reporterId = Guid.NewGuid();
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out var userLookupMock, out var gatewayMock);
            userLookupMock.Reset();
            userLookupMock.Setup(x => x.Exists(reporterId)).Returns(false);
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ReportDescriptionWords(50) };

            // Act
            var ex = Record.Exception(() => sut.CreateCommentReportAsync(Guid.NewGuid(), reporterId, request).GetAwaiter().GetResult());

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        [Fact]
        public void UTCID06_CreateCommentReportAsync_Fail_WhenCommentNotFound()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            gatewayMock.Setup(x => x.GetCommentById(commentId)).Returns((comments?)null);
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ReportDescriptionWords(50) };

            // Act
            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, Guid.NewGuid(), request).GetAwaiter().GetResult());

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        [Fact]
        public void UTCID07_CreateCommentReportAsync_Fail_WhenCommentNotBelongToStory()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var expectedStoryId = Guid.NewGuid();
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            gatewayMock.Setup(x => x.GetCommentById(commentId)).Returns(new comments { id = commentId, story_id = Guid.NewGuid(), user_id = Guid.NewGuid() });
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ReportDescriptionWords(50) };

            // Act
            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, Guid.NewGuid(), request, expectedStoryId: expectedStoryId).GetAwaiter().GetResult());

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        [Fact]
        public void UTCID08_CreateCommentReportAsync_Fail_WhenReporterIsCommentOwner()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var reporterId = Guid.NewGuid();
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            gatewayMock.Setup(x => x.GetCommentById(commentId)).Returns(new comments { id = commentId, story_id = Guid.NewGuid(), user_id = reporterId });
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ReportDescriptionWords(50) };

            // Act
            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, reporterId, request).GetAwaiter().GetResult());

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        [Fact]
        public void UTCID09_CreateCommentReportAsync_Fail_WhenCommentOwnerRoleInvalid()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var commentOwnerId = Guid.NewGuid();
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            gatewayMock.Setup(x => x.GetCommentById(commentId)).Returns(new comments { id = commentId, story_id = storyId, user_id = commentOwnerId });
            gatewayMock.Setup(x => x.GetUserRoleAsync(commentOwnerId)).ReturnsAsync("ADMIN");
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ReportDescriptionWords(50) };

            // Act
            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, Guid.NewGuid(), request).GetAwaiter().GetResult());

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        [Fact]
        public void UTCID10_CreateCommentReportAsync_Fail_WhenStoryNotFound()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            gatewayMock.Setup(x => x.GetCommentById(commentId)).Returns(new comments { id = commentId, story_id = storyId, user_id = Guid.NewGuid() });
            gatewayMock.Setup(x => x.GetUserRoleAsync(It.IsAny<Guid>())).ReturnsAsync("USER");
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns((stories?)null);
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ReportDescriptionWords(50) };

            // Act
            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, Guid.NewGuid(), request).GetAwaiter().GetResult());

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        [Fact]
        public void UTCID11_CreateCommentReportAsync_Fail_WhenStoryNotPublished()
        {
            // Arrange
            var commentId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            gatewayMock.Setup(x => x.GetCommentById(commentId)).Returns(new comments { id = commentId, story_id = storyId, user_id = Guid.NewGuid() });
            gatewayMock.Setup(x => x.GetUserRoleAsync(It.IsAny<Guid>())).ReturnsAsync("USER");
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "DRAFT" });
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ReportDescriptionWords(50) };

            // Act
            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, Guid.NewGuid(), request).GetAwaiter().GetResult());

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        [Fact]
        public void UTCID12_CreateCommentReportAsync_Fail_WhenDuplicateReporterEvidenceExists()
        {
            // Arrange
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
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ReportDescriptionWords(50) };

            // Act
            var ex = Record.Exception(() => sut.CreateCommentReportAsync(commentId, reporterId, request).GetAwaiter().GetResult());

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }

        [Fact]
        public void UTCID13_CreateCommentReportAsync_Fail_WhenDescriptionTooShort()
        {
            // Arrange
            var reportStore = new List<reports>();
            var evidenceStore = new List<report_evidences>();
            var sut = CreateSut(reportStore, evidenceStore, out _, out var gatewayMock);
            var request = new CreateCommentReportRequestDto { ReasonCode = "OTHER", Description = ReportDescriptionWords(49) };

            // Act
            var ex = Record.Exception(() => sut.CreateCommentReportAsync(Guid.NewGuid(), Guid.NewGuid(), request).GetAwaiter().GetResult());

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.AddReport(It.IsAny<reports>()), Times.Never);
        }
    }
}

// dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT_CreateCommentReport" --logger "console;verbosity=detailed"