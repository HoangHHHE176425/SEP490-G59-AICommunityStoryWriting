using AIStory.Services.Implementations;
using BusinessObjects.Entities;
using Moq;
using Repositories.Interfaces;
using Services.DTOs.Auth;
using Services.Interfaces;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT04_FunctionVerifyOtpCustomer
    {
        private readonly ITestOutputHelper _output;

        public UT04_FunctionVerifyOtpCustomer(ITestOutputHelper output) => _output = output;

        private void LogUtcContext(string utcId, string oneLineGoal, params string[] details)
        {
            _output.WriteLine("");
            _output.WriteLine($"======== {utcId} | UT04 VerifyOtpCustomer ========");
            _output.WriteLine(oneLineGoal);
            foreach (var line in details)
                _output.WriteLine("  · " + line);
        }

        private void LogActualMessage(string message)
        {
            var line = "Actual log message: " + message;
            _output.WriteLine(line);
            Console.WriteLine(line);
        }

        private void LogOtpSuccess(string message)
        {
            var line = "Actual log message: " + message;
            _output.WriteLine(line);
            Console.WriteLine(line);
        }

        private static AuthService CreateSut(
            out Mock<IUserRepository> userRepoMock,
            out Mock<IOtpRepository> otpRepoMock,
            out Mock<IEmailService> emailServiceMock)
        {
            userRepoMock = new Mock<IUserRepository>(MockBehavior.Strict);
            otpRepoMock = new Mock<IOtpRepository>(MockBehavior.Strict);
            emailServiceMock = new Mock<IEmailService>(MockBehavior.Strict);
            var tokenServiceMock = new Mock<ITokenService>(MockBehavior.Strict);

            return new AuthService(
                userRepoMock.Object,
                otpRepoMock.Object,
                emailServiceMock.Object,
                tokenServiceMock.Object);
        }

        private static users PendingUser(string email)
        {
            return new users
            {
                id = Guid.NewGuid(),
                email = email,
                password_hash = BCrypt.Net.BCrypt.HashPassword("A12345678"),
                role = "USER",
                status = "PENDING"
            };
        }

        [Fact]
        public async Task UTCID17_VerifyOtp_Fails_WhenOtpIsWrong()
        {
            LogUtcContext("UTCID17",
                "Abnormal path: OTP sai -> verify thất bại.",
                "Precondition: user PENDING tồn tại, GetValidOtp trả về null.",
                "Input: valid@gmail.com / 000000.",
                "Kỳ vọng: throw exception OTP không đúng hoặc đã hết hạn; không UpdateUser, không MarkOtpAsUsed.");

            var email = "valid@gmail.com";
            var otpCode = "000000";
            var user = PendingUser(email);
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            userRepoMock.Setup(x => x.GetUserByEmail(email)).ReturnsAsync(user);
            otpRepoMock.Setup(x => x.GetValidOtp(user.id, otpCode, "EMAIL_VERIFICATION")).ReturnsAsync((otp_verifications?)null);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.VerifyAccountAsync(new VerifyOtpRequest
            {
                Email = email,
                OtpCode = otpCode
            }));

            LogActualMessage(ex.Message);
            Assert.Contains("OTP không đúng hoặc đã hết hạn", ex.Message, StringComparison.OrdinalIgnoreCase);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.UpdateUser(It.IsAny<users>()), Times.Never);
            otpRepoMock.Verify(x => x.GetValidOtp(user.id, otpCode, "EMAIL_VERIFICATION"), Times.Once);
            otpRepoMock.Verify(x => x.MarkOtpAsUsed(It.IsAny<Guid>()), Times.Never);
            emailServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID18_VerifyOtp_Fails_WhenOtpIsExpired()
        {
            LogUtcContext("UTCID18",
                "Abnormal path: OTP hết hạn -> verify thất bại.",
                "Precondition: user PENDING tồn tại, GetValidOtp trả về null vì đã expired.",
                "Input: valid@gmail.com / 111111.",
                "Kỳ vọng: throw exception OTP không đúng hoặc đã hết hạn; không UpdateUser.");

            var email = "valid@gmail.com";
            var otpCode = "111111";
            var user = PendingUser(email);
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            userRepoMock.Setup(x => x.GetUserByEmail(email)).ReturnsAsync(user);
            otpRepoMock.Setup(x => x.GetValidOtp(user.id, otpCode, "EMAIL_VERIFICATION")).ReturnsAsync((otp_verifications?)null);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.VerifyAccountAsync(new VerifyOtpRequest
            {
                Email = email,
                OtpCode = otpCode
            }));

            LogActualMessage(ex.Message);
            Assert.Contains("OTP không đúng hoặc đã hết hạn", ex.Message, StringComparison.OrdinalIgnoreCase);
            userRepoMock.Verify(x => x.UpdateUser(It.IsAny<users>()), Times.Never);
            otpRepoMock.Verify(x => x.MarkOtpAsUsed(It.IsAny<Guid>()), Times.Never);
            emailServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID19_VerifyOtp_Fails_WhenOtpIsNull()
        {
            LogUtcContext("UTCID19",
                "Abnormal path: OTP null ở tầng service.",
                "Precondition: user PENDING tồn tại, GetValidOtp với null trả về null.",
                "Input: valid@gmail.com / null.",
                "Kỳ vọng: throw exception OTP không đúng hoặc đã hết hạn; không UpdateUser.");

            var email = "valid@gmail.com";
            var user = PendingUser(email);
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            userRepoMock.Setup(x => x.GetUserByEmail(email)).ReturnsAsync(user);
            otpRepoMock.Setup(x => x.GetValidOtp(user.id, null!, "EMAIL_VERIFICATION")).ReturnsAsync((otp_verifications?)null);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.VerifyAccountAsync(new VerifyOtpRequest
            {
                Email = email,
                OtpCode = null!
            }));

            LogActualMessage(ex.Message);
            Assert.Contains("OTP không đúng hoặc đã hết hạn", ex.Message, StringComparison.OrdinalIgnoreCase);
            userRepoMock.Verify(x => x.UpdateUser(It.IsAny<users>()), Times.Never);
            otpRepoMock.Verify(x => x.MarkOtpAsUsed(It.IsAny<Guid>()), Times.Never);
            emailServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID20_ResendOtp_ThenVerify_Succeeds()
        {
            LogUtcContext("UTCID20",
                "Happy flow: resend OTP thành công, sau đó verify bằng OTP mới.",
                "Precondition: user PENDING tồn tại.",
                "Input: resend theo email rồi verify với otp vừa được tạo.",
                "Kỳ vọng: ResendOtpAsync trả TTL 900; VerifyAccountAsync update user ACTIVE và mark OTP used.");

            var email = "valid@gmail.com";
            var user = PendingUser(email);
            otp_verifications? resentOtp = null;
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            userRepoMock.Setup(x => x.GetUserByEmail(email)).ReturnsAsync(user);
            otpRepoMock.Setup(x => x.AddOtp(It.IsAny<otp_verifications>()))
                .Callback<otp_verifications>(o => resentOtp = o)
                .Returns(Task.CompletedTask);
            emailServiceMock.Setup(x => x.SendEmailAsync(email, "Xác thực tài khoản", It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            var resendRes = await sut.ResendOtpAsync(new ResendOtpRequest { Email = email });

            LogOtpSuccess($"ResendOtpAsync succeeded. Message={resendRes.Message}, ExpiresInSeconds={resendRes.ExpiresInSeconds}, OtpGenerated={(resentOtp != null)}");
            Assert.Equal(900, resendRes.ExpiresInSeconds);
            Assert.Equal("OTP mới đã được gửi. Vui lòng kiểm tra email.", resendRes.Message);
            Assert.NotNull(resentOtp);
            Assert.Equal(user.id, resentOtp!.user_id);
            Assert.Matches("^[0-9]{6}$", resentOtp.otp_code);

            otpRepoMock.Setup(x => x.GetValidOtp(user.id, resentOtp.otp_code, "EMAIL_VERIFICATION"))
                .ReturnsAsync(resentOtp);
            userRepoMock.Setup(x => x.UpdateUser(It.IsAny<users>())).Returns(Task.CompletedTask);
            otpRepoMock.Setup(x => x.MarkOtpAsUsed(resentOtp.id)).Returns(Task.CompletedTask);

            await sut.VerifyAccountAsync(new VerifyOtpRequest
            {
                Email = email,
                OtpCode = resentOtp.otp_code
            });

            LogOtpSuccess($"VerifyAccountAsync succeeded. UserStatus={user.status}, EmailVerifiedAtSet={(user.email_verified_at != null)}, OtpMarkedUsed={resentOtp.id != Guid.Empty}");
            Assert.Equal("ACTIVE", user.status);
            Assert.NotNull(user.email_verified_at);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Exactly(2));
            userRepoMock.Verify(x => x.UpdateUser(It.Is<users>(u => u.id == user.id && u.status == "ACTIVE")), Times.Once);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Once);
            otpRepoMock.Verify(x => x.GetValidOtp(user.id, resentOtp.otp_code, "EMAIL_VERIFICATION"), Times.Once);
            otpRepoMock.Verify(x => x.MarkOtpAsUsed(resentOtp.id), Times.Once);
            emailServiceMock.Verify(x => x.SendEmailAsync(email, "Xác thực tài khoản", It.Is<string>(body => body.Contains(resentOtp.otp_code))), Times.Once);
        }
    }
}
