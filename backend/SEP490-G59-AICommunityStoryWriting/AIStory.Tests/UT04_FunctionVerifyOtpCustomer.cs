using AIStory.Services.Implementations;
using BusinessObjects.Entities;
using Moq;
using Repositories.Interfaces;
using Services.DTOs.Auth;
using Services.Interfaces;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT04_FunctionVerifyOtpCustomer
    {
        private readonly ITestOutputHelper _output;

        public UT04_FunctionVerifyOtpCustomer(ITestOutputHelper output) => _output = output;

        private sealed record SentEmail(string To, string Subject, string Body);

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

        private void LogStores(string label, IReadOnlyList<users> userStore, IReadOnlyList<otp_verifications> otpStore, IReadOnlyList<SentEmail> sentEmails)
        {
            _output.WriteLine("");
            _output.WriteLine($"======== {label} - stores ========");
            _output.WriteLine($"Users: {userStore.Count}, Otp: {otpStore.Count}, SentEmails: {sentEmails.Count}");

            foreach (var user in userStore)
            {
                _output.WriteLine($"  user id={user.id}, email={user.email}, status={user.status}, email_verified_at={user.email_verified_at:O}");
            }

            foreach (var otp in otpStore)
            {
                _output.WriteLine($"  otp id={otp.id}, user_id={otp.user_id}, type={otp.type}, code={otp.otp_code}, is_used={otp.is_used}, expired_at={otp.expired_at:O}");
            }

            foreach (var email in sentEmails)
            {
                _output.WriteLine($"  email to={email.To}, subject={email.Subject}, bodyLen={email.Body.Length}");
            }
        }

        private static AuthService CreateSut(
            List<users> userStore,
            List<otp_verifications> otpStore,
            List<SentEmail> sentEmails,
            out Mock<IUserRepository> userRepoMock,
            out Mock<IOtpRepository> otpRepoMock,
            out Mock<IEmailService> emailServiceMock,
            out Mock<ITokenService> tokenServiceMock)
        {
            userRepoMock = new Mock<IUserRepository>(MockBehavior.Strict);
            otpRepoMock = new Mock<IOtpRepository>(MockBehavior.Strict);
            emailServiceMock = new Mock<IEmailService>(MockBehavior.Strict);
            tokenServiceMock = new Mock<ITokenService>(MockBehavior.Strict);

            userRepoMock.Setup(x => x.GetUserByEmail(It.IsAny<string>()))
                .ReturnsAsync((string email) => userStore.FirstOrDefault(u =>
                    string.Equals(u.email, email, StringComparison.OrdinalIgnoreCase)));
            userRepoMock.Setup(x => x.UpdateUser(It.IsAny<users>()))
                .Callback((users updatedUser) =>
                {
                    var index = userStore.FindIndex(u => u.id == updatedUser.id);
                    if (index >= 0)
                    {
                        userStore[index] = updatedUser;
                    }
                })
                .Returns(Task.CompletedTask);

            otpRepoMock.Setup(x => x.GetValidOtp(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((Guid userId, string otpCode, string type) => otpStore.FirstOrDefault(o =>
                    o.user_id == userId &&
                    o.otp_code == otpCode &&
                    string.Equals(o.type, type, StringComparison.OrdinalIgnoreCase) &&
                    o.is_used != true &&
                    o.expired_at > DateTime.UtcNow));
            otpRepoMock.Setup(x => x.AddOtp(It.IsAny<otp_verifications>()))
                .Callback((otp_verifications otp) => otpStore.Add(otp))
                .Returns(Task.CompletedTask);
            otpRepoMock.Setup(x => x.MarkOtpAsUsed(It.IsAny<Guid>()))
                .Callback((Guid otpId) =>
                {
                    var otp = otpStore.FirstOrDefault(o => o.id == otpId);
                    if (otp != null)
                    {
                        otp.is_used = true;
                    }
                })
                .Returns(Task.CompletedTask);

            emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback((string to, string subject, string body) => sentEmails.Add(new SentEmail(to, subject, body)))
                .Returns(Task.CompletedTask);

            return new AuthService(userRepoMock.Object, otpRepoMock.Object, emailServiceMock.Object, tokenServiceMock.Object);
        }

        private static users CreateUser(string email, string status = "PENDING")
        {
            return new users
            {
                id = Guid.NewGuid(),
                email = email,
                password_hash = BCrypt.Net.BCrypt.HashPassword("A12345678"),
                role = "USER",
                status = status,
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };
        }

        private static otp_verifications CreateOtp(Guid userId, string otpCode, DateTime? expiredAt = null, bool isUsed = false)
        {
            return new otp_verifications
            {
                id = Guid.NewGuid(),
                user_id = userId,
                otp_code = otpCode,
                type = "EMAIL_VERIFICATION",
                is_used = isUsed,
                expired_at = expiredAt ?? DateTime.UtcNow.AddMinutes(15),
                created_at = DateTime.UtcNow
            };
        }

        [Fact]
        public async Task UTCID17_VerifyOtp_Result_WhenOtpIsWrong()
        {
            // Arrange
            var email = "valid@gmail.com";
            var request = new VerifyOtpRequest { Email = email, OtpCode = "000000" };
            var user = CreateUser(email);
            var userStore = new List<users> { user };
            var otpStore = new List<otp_verifications> { CreateOtp(user.id, "123456") };
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            var ex = await Record.ExceptionAsync(() => sut.VerifyAccountAsync(request));
            LogTestCase("UTCID17", "OTP sai -> verify fail, khong update user va khong mark OTP used.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Equal("PENDING", user.status);
            Assert.Null(user.email_verified_at);
            Assert.DoesNotContain(otpStore, o => o.is_used == true);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.UpdateUser(It.IsAny<users>()), Times.Never);
            otpRepoMock.Verify(x => x.GetValidOtp(user.id, request.OtpCode, "EMAIL_VERIFICATION"), Times.Once);
            otpRepoMock.Verify(x => x.MarkOtpAsUsed(It.IsAny<Guid>()), Times.Never);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Never);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            tokenServiceMock.VerifyNoOtherCalls();
            LogStores("UTCID17 (sau verify)", userStore, otpStore, sentEmails);
        }

        [Fact]
        public async Task UTCID18_VerifyOtp_Result_WhenOtpIsExpired()
        {
            // Arrange
            var email = "valid@gmail.com";
            var request = new VerifyOtpRequest { Email = email, OtpCode = "111111" };
            var user = CreateUser(email);
            var userStore = new List<users> { user };
            var otpStore = new List<otp_verifications> { CreateOtp(user.id, request.OtpCode, DateTime.UtcNow.AddMinutes(-1)) };
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            var ex = await Record.ExceptionAsync(() => sut.VerifyAccountAsync(request));
            LogTestCase("UTCID18", "OTP het han -> verify fail, khong update user.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Equal("PENDING", user.status);
            Assert.Null(user.email_verified_at);
            Assert.DoesNotContain(otpStore, o => o.is_used == true);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.UpdateUser(It.IsAny<users>()), Times.Never);
            otpRepoMock.Verify(x => x.GetValidOtp(user.id, request.OtpCode, "EMAIL_VERIFICATION"), Times.Once);
            otpRepoMock.Verify(x => x.MarkOtpAsUsed(It.IsAny<Guid>()), Times.Never);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Never);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            tokenServiceMock.VerifyNoOtherCalls();
            LogStores("UTCID18 (sau verify)", userStore, otpStore, sentEmails);
        }

        [Fact]
        public async Task UTCID19_VerifyOtp_Result_WhenOtpIsNull()
        {
            // Arrange
            var email = "valid@gmail.com";
            var request = new VerifyOtpRequest { Email = email, OtpCode = null! };
            var user = CreateUser(email);
            var userStore = new List<users> { user };
            var otpStore = new List<otp_verifications> { CreateOtp(user.id, "222222") };
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            var ex = await Record.ExceptionAsync(() => sut.VerifyAccountAsync(request));
            LogTestCase("UTCID19", "OTP null -> verify fail, khong update user.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Equal("PENDING", user.status);
            Assert.Null(user.email_verified_at);
            Assert.DoesNotContain(otpStore, o => o.is_used == true);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.UpdateUser(It.IsAny<users>()), Times.Never);
            otpRepoMock.Verify(x => x.GetValidOtp(user.id, null!, "EMAIL_VERIFICATION"), Times.Once);
            otpRepoMock.Verify(x => x.MarkOtpAsUsed(It.IsAny<Guid>()), Times.Never);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Never);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            tokenServiceMock.VerifyNoOtherCalls();
            LogStores("UTCID19 (sau verify)", userStore, otpStore, sentEmails);
        }

        [Fact]
        public async Task UTCID20_ResendOtpAndVerify_Result_WhenPendingUserRequestsNewOtp()
        {
            // Arrange
            var email = "valid@gmail.com";
            var user = CreateUser(email);
            var userStore = new List<users> { user };
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);
            var resendRequest = new ResendOtpRequest { Email = email };

            // Act
            var resendDto = await sut.ResendOtpAsync(resendRequest);
            var resentOtp = Assert.Single(otpStore);
            var verifyRequest = new VerifyOtpRequest
            {
                Email = email,
                OtpCode = resentOtp.otp_code
            };
            await sut.VerifyAccountAsync(verifyRequest);
            LogTestCase(
                "UTCID20",
                "Resend OTP thanh cong roi verify bang OTP moi -> user ACTIVE va OTP duoc mark used.",
                new { Resend = resendRequest, Verify = verifyRequest },
                new { Resend = resendDto, User = user, Otp = resentOtp, Email = sentEmails.Single() });

            // Assert
            Assert.NotNull(resendDto);
            Assert.Equal(900, resendDto.ExpiresInSeconds);
            Assert.False(string.IsNullOrWhiteSpace(resendDto.Message));
            Assert.Equal(user.id, resentOtp.user_id);
            Assert.Matches("^[0-9]{6}$", resentOtp.otp_code);
            Assert.True(resentOtp.is_used);
            Assert.Equal("ACTIVE", user.status);
            Assert.NotNull(user.email_verified_at);
            Assert.Single(sentEmails);
            Assert.Equal(email, sentEmails[0].To);
            Assert.Contains(resentOtp.otp_code, sentEmails[0].Body);

            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Exactly(2));
            userRepoMock.Verify(x => x.UpdateUser(It.Is<users>(u => u.id == user.id && u.status == "ACTIVE")), Times.Once);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Once);
            otpRepoMock.Verify(x => x.GetValidOtp(user.id, resentOtp.otp_code, "EMAIL_VERIFICATION"), Times.Once);
            otpRepoMock.Verify(x => x.MarkOtpAsUsed(resentOtp.id), Times.Once);
            emailServiceMock.Verify(x => x.SendEmailAsync(email, "Xác thực tài khoản", It.Is<string>(body => body.Contains(resentOtp.otp_code))), Times.Once);
            tokenServiceMock.VerifyNoOtherCalls();
            LogStores("UTCID20 (sau verify)", userStore, otpStore, sentEmails);
        }
    }
}
