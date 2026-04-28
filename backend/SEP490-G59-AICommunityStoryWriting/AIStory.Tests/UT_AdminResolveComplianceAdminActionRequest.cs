using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Moq;
using Services.DTOs.StoryReports;
using Services.Implementations;
using Services.Interfaces;
using System;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT_AdminResolveComplianceAdminActionRequest
    {
        private readonly ITestOutputHelper _output;

        public UT_AdminResolveComplianceAdminActionRequest(ITestOutputHelper output) => _output = output;

        private void LogSuccess(string utcId, object payload)
        {
            _output.WriteLine("");
            _output.WriteLine($"========== {utcId} ==========");
            _output.WriteLine("RESULT : SUCCESS");
            _output.WriteLine(JsonSerializer.Serialize(payload));
        }

        private StoryReportService CreateSut(
            List<(Guid RequestId, Guid AdminId, string FinalStatus, string? Note, string Action)> resolveStore,
            out Mock<StoryReportService.IAdminComplianceAdminActionGateway> gatewayMock)
        {
            var userLookupMock = new Mock<IUserLookup>(MockBehavior.Strict);
            var userActivityLookupMock = new Mock<IUserActivityLookup>(MockBehavior.Strict);
            gatewayMock = new Mock<StoryReportService.IAdminComplianceAdminActionGateway>(MockBehavior.Strict);

            gatewayMock
                .Setup(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()))
                .Callback((Guid requestId, Guid adminId, string finalStatus, string? note, string action) =>
                    resolveStore.Add((requestId, adminId, finalStatus, note, action)));

            return new StoryReportService(
                userLookupMock.Object,
                userActivityLookupMock.Object,
                notificationHubNotifier: null,
                emailService: null,
                adminComplianceGateway: gatewayMock.Object,
                enableAdminActionNotifications: false);
        }

        [Fact]
        public async Task UTCID01_AdminResolveComplianceAdminActionRequest_Success_WhenRejectWithPendingRequest()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var resolveStore = new List<(Guid, Guid, string, string?, string)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            var dto = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "REJECT",
                ReasonCode = "OTHER",
                AdminNote = "reject by admin"
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

            // Act
            await sut.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, dto);
            LogSuccess("UTCID01", new { RequestId = requestId, dto.Decision, dto.AdminNote });

            // Assert
            Assert.Single(resolveStore);
            gatewayMock.Verify(x => x.MarkResolved(requestId, adminId, ComplianceAdminActionRequestDAO.StatusRejected, dto.AdminNote, "REJECT"), Times.Once);
        }

        [Fact]
        public async Task UTCID02_AdminResolveComplianceAdminActionRequest_Fail_WhenRequestIdIsEmpty()
        {
            // Arrange
            var adminId = Guid.NewGuid();
            var resolveStore = new List<(Guid, Guid, string, string?, string)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "REJECT", AdminNote = "x" };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(Guid.Empty, adminId, dto));

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }
        
        [Fact]
        public async Task UTCID03_AdminResolveComplianceAdminActionRequest_Fail_WhenAdminNoteTooLong()
        {
            // Arrange
            var resolveStore = new List<(Guid, Guid, string, string?, string)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "REJECT", AdminNote = new string('a', 2001) };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(Guid.NewGuid(), Guid.NewGuid(), dto));

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        [Fact]
        public async Task UTCID04_AdminResolveComplianceAdminActionRequest_Fail_WhenDecisionInvalid()
        {
            // Arrange
            var resolveStore = new List<(Guid, Guid, string, string?, string)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "INVALID_DECISION", AdminNote = "note" };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(Guid.NewGuid(), Guid.NewGuid(), dto));

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        [Fact]
        public async Task UTCID05_AdminResolveComplianceAdminActionRequest_Fail_WhenRequestNotFound()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var resolveStore = new List<(Guid, Guid, string, string?, string)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            gatewayMock.Setup(x => x.GetTrackedById(requestId)).Returns((compliance_admin_action_requests?)null);
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "REJECT", AdminNote = "note" };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(requestId, Guid.NewGuid(), dto));

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        [Fact]
        public async Task UTCID06_AdminResolveComplianceAdminActionRequest_Fail_WhenRequestAlreadyResolved()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var resolveStore = new List<(Guid, Guid, string, string?, string)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            gatewayMock.Setup(x => x.GetTrackedById(requestId)).Returns(new compliance_admin_action_requests
            {
                id = requestId,
                status = ComplianceAdminActionRequestDAO.StatusApproved,
                request_kind = ComplianceAdminActionRequestDAO.KindBanUser
            });
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "REJECT", AdminNote = "note" };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(requestId, Guid.NewGuid(), dto));

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        [Fact]
        public async Task UTCID07_AdminResolveComplianceAdminActionRequest_Fail_WhenRequesterEqualsTargetUser()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var userId = Guid.NewGuid();
            var resolveStore = new List<(Guid, Guid, string, string?, string)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            gatewayMock.Setup(x => x.GetTrackedById(requestId)).Returns(new compliance_admin_action_requests
            {
                id = requestId,
                requester_id = userId,
                target_user_id = userId,
                story_id = Guid.NewGuid(),
                request_kind = ComplianceAdminActionRequestDAO.KindBanUser,
                status = ComplianceAdminActionRequestDAO.StatusPending
            });
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "REJECT", AdminNote = "note" };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(requestId, Guid.NewGuid(), dto));

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        [Fact]
        public async Task UTCID08_AdminResolveComplianceAdminActionRequest_Fail_WhenStoryNotFound()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var resolveStore = new List<(Guid, Guid, string, string?, string)>();
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
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns((stories?)null);
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "REJECT", AdminNote = "note" };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(requestId, Guid.NewGuid(), dto));

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        [Fact]
        public async Task UTCID09_AdminResolveComplianceAdminActionRequest_Fail_WhenStoryNotPublished()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var resolveStore = new List<(Guid, Guid, string, string?, string)>();
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
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "REJECT", AdminNote = "note" };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(requestId, Guid.NewGuid(), dto));

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        [Fact]
        public async Task UTCID10_AdminResolveComplianceAdminActionRequest_Success_WhenApproveSuspendWithValidUntil()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();
            var resolveStore = new List<(Guid, Guid, string, string?, string)>();
            var suspendStore = new List<(Guid UserId, DateTime? UntilUtc)>();
            var violationStore = new List<(Guid ActorId, Guid ViolatorId, string PenaltyType)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            var dto = new AdminResolveComplianceAdminActionRequestDto
            {
                Decision = "APPROVE",
                AdminNote = "suspend approved",
                SuspendUntilUtc = DateTime.UtcNow.AddDays(3)
            };
            gatewayMock.Setup(x => x.GetTrackedById(requestId)).Returns(new compliance_admin_action_requests
            {
                id = requestId,
                story_id = storyId,
                target_user_id = targetUserId,
                requester_id = Guid.NewGuid(),
                request_kind = ComplianceAdminActionRequestDAO.KindSuspendAuthorWriting,
                status = ComplianceAdminActionRequestDAO.StatusPending,
                message = "m"
            });
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED", title = "T", summary = "S" });
            gatewayMock.Setup(x => x.SetAuthorWritingSuspendedUntil(targetUserId, It.IsAny<DateTime?>()))
                .Callback((Guid u, DateTime? until) => suspendStore.Add((u, until)));
            gatewayMock.Setup(x => x.InsertViolationLog(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>()))
                .Callback((Guid actor, Guid violator, string _, Guid _, string penalty, string? _, string? _) => violationStore.Add((actor, violator, penalty)));

            // Act
            await sut.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, dto);
            LogSuccess("UTCID10", new { RequestId = requestId, dto.Decision, dto.SuspendUntilUtc });

            // Assert
            Assert.Single(resolveStore);
            Assert.Single(suspendStore);
            Assert.Single(violationStore);
            gatewayMock.Verify(x => x.MarkResolved(requestId, adminId, ComplianceAdminActionRequestDAO.StatusApproved, dto.AdminNote, "SUSPEND_WRITING"), Times.Once);
        }

        [Fact]
        public async Task UTCID11_AdminResolveComplianceAdminActionRequest_Fail_WhenApproveSuspendWithoutValidFutureTime()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var resolveStore = new List<(Guid, Guid, string, string?, string)>();
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
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "APPROVE", AdminNote = "note", SuspendUntilUtc = DateTime.UtcNow.AddHours(-1) };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(requestId, Guid.NewGuid(), dto));

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        [Fact]
        public async Task UTCID12_AdminResolveComplianceAdminActionRequest_Fail_WhenRequestKindUnsupported()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var resolveStore = new List<(Guid, Guid, string, string?, string)>();
            var sut = CreateSut(resolveStore, out var gatewayMock);
            gatewayMock.Setup(x => x.GetTrackedById(requestId)).Returns(new compliance_admin_action_requests
            {
                id = requestId,
                story_id = storyId,
                target_user_id = Guid.NewGuid(),
                requester_id = Guid.NewGuid(),
                request_kind = "UNKNOWN_KIND",
                status = ComplianceAdminActionRequestDAO.StatusPending
            });
            gatewayMock.Setup(x => x.GetStoryById(storyId)).Returns(new stories { id = storyId, status = "PUBLISHED" });
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "APPROVE", AdminNote = "note", SuspendUntilUtc = DateTime.UtcNow.AddDays(1) };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.AdminResolveComplianceAdminActionRequestAsync(requestId, Guid.NewGuid(), dto));

            // Assert
            Assert.NotNull(ex);
            _output.WriteLine($"Exception type: {ex.GetType().Name}");
            _output.WriteLine($"Message: {ex.Message}");
            gatewayMock.Verify(x => x.MarkResolved(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>()), Times.Never);
            Assert.Empty(resolveStore);
        }

        [Fact]
        public async Task UTCID13_AdminResolveComplianceAdminActionRequest_Success_WhenApproveBanUser()
        {
            // Arrange
            var requestId = Guid.NewGuid();
            var adminId = Guid.NewGuid();
            var storyId = Guid.NewGuid();
            var targetUserId = Guid.NewGuid();
            var resolveStore = new List<(Guid, Guid, string, string?, string)>();
            var bannedUsers = new List<Guid>();
            var sweepRunCount = 0;
            var sut = CreateSut(resolveStore, out var gatewayMock);
            var dto = new AdminResolveComplianceAdminActionRequestDto { Decision = "APPROVE", AdminNote = "ban approved" };
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

            // Act
            await sut.AdminResolveComplianceAdminActionRequestAsync(requestId, adminId, dto);
            LogSuccess("UTCID13", new { RequestId = requestId, dto.Decision, dto.AdminNote, BannedUserId = targetUserId });

            // Assert
            Assert.Single(resolveStore);
            Assert.Single(bannedUsers);
            Assert.Equal(1, sweepRunCount);
            gatewayMock.Verify(x => x.MarkResolved(requestId, adminId, ComplianceAdminActionRequestDAO.StatusApproved, dto.AdminNote, "BAN_USER"), Times.Once);
        }
    }
}

// dotnet test ".\AIStory.Tests.csproj" --no-restore --filter "FullyQualifiedName~AIStory.Tests.UT_AdminResolveComplianceAdminActionRequest" --logger "console;verbosity=detailed"