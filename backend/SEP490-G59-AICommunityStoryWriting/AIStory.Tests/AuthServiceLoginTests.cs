using AIStory.Services.Implementations;
using BusinessObjects.Entities;
using Moq;
using Repositories.Interfaces;
using Services.DTOs.Auth;
using Services.Interfaces;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT02_FunctionLoginCustomer
    {
        private readonly ITestOutputHelper _output;
        private const string ValidPassword = "12345678aa";

        public UT02_FunctionLoginCustomer(ITestOutputHelper output) => _output = output;

        private void LogUtcContext(string utcId, string oneLineGoal, params string[] details)
        {
            _output.WriteLine("");
            _output.WriteLine($"======== {utcId} | UT02 LoginCustomer ========");
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

        private void LogSuccessResult(AuthResponse result)
        {
            var line =
                $"Actual log message: Login succeeded. AccessToken generated = {!string.IsNullOrWhiteSpace(result.AccessToken)}, RefreshToken generated = {!string.IsNullOrWhiteSpace(result.RefreshToken)}";
            _output.WriteLine(line);
            Console.WriteLine(line);
        }

        private static AuthService CreateSut(
            out Mock<IUserRepository> userRepoMock,
            out Mock<IOtpRepository> otpRepoMock,
            out Mock<IEmailService> emailServiceMock,
            out Mock<ITokenService> tokenServiceMock)
        {
            userRepoMock = new Mock<IUserRepository>(MockBehavior.Strict);
            otpRepoMock = new Mock<IOtpRepository>(MockBehavior.Strict);
            emailServiceMock = new Mock<IEmailService>(MockBehavior.Strict);
            tokenServiceMock = new Mock<ITokenService>(MockBehavior.Strict);

            return new AuthService(
                userRepoMock.Object,
                otpRepoMock.Object,
                emailServiceMock.Object,
                tokenServiceMock.Object);
        }

        private static users CreateUser(string email, string password, string status = "ACTIVE")
        {
            return new users
            {
                id = Guid.NewGuid(),
                email = email,
                password_hash = BCrypt.Net.BCrypt.HashPassword(password),
                role = "USER",
                status = status
            };
        }

        [Fact]
        public async Task UTCID02_Login_Succeeds_WhenEmailPasswordAndStatusAreValid()
        {
            LogUtcContext("UTCID02",
                "Happy path: email tồn tại, password đúng, status ACTIVE -> login thành công.",
                "Precondition: repo trả về user ACTIVE có password hash hợp lệ.",
                "Input: existing@gmail.com / 12345678aa.",
                "Kỳ vọng: có AccessToken, RefreshToken và AddRefreshToken được gọi đúng 1 lần.");

            var email = "existing@gmail.com";
            var user = CreateUser(email, ValidPassword, "ACTIVE");
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            userRepoMock.Setup(x => x.GetUserByEmail(email)).ReturnsAsync(user);
            userRepoMock.Setup(x => x.AddRefreshToken(It.IsAny<auth_tokens>())).Returns(Task.CompletedTask);
            tokenServiceMock.Setup(x => x.GenerateAccessToken(user)).Returns("access-token");
            tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("refresh-token");

            var result = await sut.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = ValidPassword
            });

            LogSuccessResult(result);
            Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
            Assert.False(string.IsNullOrWhiteSpace(result.RefreshToken));
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.Is<auth_tokens>(t =>
                t.user_id == user.id &&
                t.refresh_token == result.RefreshToken &&
                t.device_info == "Unknown")), Times.Once);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.Verify(x => x.GenerateAccessToken(user), Times.Once);
            tokenServiceMock.Verify(x => x.GenerateRefreshToken(), Times.Once);
        }

        [Fact]
        public async Task UTCID01_Login_Fails_WhenEmailDoesNotExist()
        {
            LogUtcContext("UTCID01",
                "Abnormal path: email không tồn tại -> login fail.",
                "Precondition: repo lookup email trả về null.",
                "Input: notfound@gmail.com / 12345678aa.",
                "Kỳ vọng: throw Exception với message Invalid email or password.; không lưu refresh token.");

            var email = "notfound@gmail.com";
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            userRepoMock.Setup(x => x.GetUserByEmail(email)).ReturnsAsync((users?)null);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = ValidPassword
            }));

            LogActualMessage(ex.Message);
            Assert.Equal("Invalid email or password.", ex.Message);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID05_Login_Fails_WhenPasswordIsWrong()
        {
            LogUtcContext("UTCID05",
                "Abnormal path: email đúng nhưng password sai -> login fail.",
                "Precondition: user ACTIVE tồn tại trong repo.",
                "Input: existing@gmail.com / wrong-pass.",
                "Kỳ vọng: throw Exception Invalid email or password.; không lưu refresh token.");

            var email = "existing@gmail.com";
            var user = CreateUser(email, ValidPassword, "ACTIVE");
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            userRepoMock.Setup(x => x.GetUserByEmail(email)).ReturnsAsync(user);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = "wrong-pass"
            }));

            LogActualMessage(ex.Message);
            Assert.Equal("Invalid email or password.", ex.Message);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID07_Login_Fails_WhenAccountIsPending()
        {
            LogUtcContext("UTCID07",
                "Abnormal path: tài khoản đúng nhưng status PENDING -> chưa được phép login.",
                "Precondition: user tồn tại, password đúng, status = PENDING.",
                "Input: existing@gmail.com / 12345678aa.",
                "Kỳ vọng: throw Exception thông báo chưa xác thực; không lưu refresh token.");

            var email = "existing@gmail.com";
            var user = CreateUser(email, ValidPassword, "PENDING");
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            userRepoMock.Setup(x => x.GetUserByEmail(email)).ReturnsAsync(user);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = ValidPassword
            }));

            LogActualMessage(ex.Message);
            Assert.Contains("chưa được xác thực", ex.Message, StringComparison.OrdinalIgnoreCase);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID09_Login_Fails_WhenStatusIsNull()
        {
            LogUtcContext("UTCID09",
                "Boundary path: user hợp lệ nhưng status null -> bị xem là chưa active.",
                "Precondition: repo trả về user có password đúng, status = null.",
                "Input: existing@gmail.com / 12345678aa.",
                "Kỳ vọng: throw Exception thông báo chưa xác thực; không lưu refresh token.");

            var email = "existing@gmail.com";
            var user = CreateUser(email, ValidPassword, status: null!);
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            userRepoMock.Setup(x => x.GetUserByEmail(email)).ReturnsAsync(user);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = ValidPassword
            }));

            LogActualMessage(ex.Message);
            Assert.Contains("chưa được xác thực", ex.Message, StringComparison.OrdinalIgnoreCase);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID04_Login_Fails_WhenEmailFormatIsInvalid()
        {
            LogUtcContext("UTCID04",
                "Abnormal path: email sai format ở tầng service được xem như không tìm thấy user.",
                "Precondition: repo lookup với invalid-email trả về null.",
                "Input: invalid-email / 12345678aa.",
                "Kỳ vọng: throw Exception Invalid email or password.; không lưu refresh token.");

            var email = "invalid-email";
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            userRepoMock.Setup(x => x.GetUserByEmail(email)).ReturnsAsync((users?)null);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = ValidPassword
            }));

            LogActualMessage(ex.Message);
            Assert.Equal("Invalid email or password.", ex.Message);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID03_Login_Fails_WhenEmailIsNull()
        {
            LogUtcContext("UTCID03",
                "Abnormal path: email null ở tầng service được truyền thẳng vào repo lookup.",
                "Precondition: repo lookup với email null trả về null.",
                "Input: null / 12345678aa.",
                "Kỳ vọng: throw Exception Invalid email or password.; không lưu refresh token.");

            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            userRepoMock.Setup(x => x.GetUserByEmail(null!)).ReturnsAsync((users?)null);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.LoginAsync(new LoginRequest
            {
                Email = null!,
                Password = ValidPassword
            }));

            LogActualMessage(ex.Message);
            Assert.Equal("Invalid email or password.", ex.Message);
            userRepoMock.Verify(x => x.GetUserByEmail(null!), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID06_Login_Fails_WhenPasswordIsNull()
        {
            LogUtcContext("UTCID06",
                "Abnormal path: password null khi verify BCrypt.",
                "Precondition: user ACTIVE tồn tại trong repo.",
                "Input: existing@gmail.com / null.",
                "Kỳ vọng: throw exception; không lưu refresh token.");

            var email = "existing@gmail.com";
            var user = CreateUser(email, ValidPassword, "ACTIVE");
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            userRepoMock.Setup(x => x.GetUserByEmail(email)).ReturnsAsync(user);

            var ex = await Assert.ThrowsAnyAsync<Exception>(() => sut.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = null!
            }));

            LogActualMessage($"{ex.GetType().Name}: {ex.Message}");
            Assert.NotNull(ex);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID08_Login_Fails_WhenStatusIsInvalid()
        {
            LogUtcContext("UTCID08",
                "Abnormal path: status khác ACTIVE (ví dụ INVALID) vẫn bị chặn login.",
                "Precondition: user tồn tại, password đúng, status = INVALID.",
                "Input: existing@gmail.com / 12345678aa.",
                "Kỳ vọng hiện tại của product: throw Exception thông báo chưa xác thực; không lưu refresh token.");

            var email = "existing@gmail.com";
            var user = CreateUser(email, ValidPassword, "INVALID");
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            userRepoMock.Setup(x => x.GetUserByEmail(email)).ReturnsAsync(user);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = ValidPassword
            }));

            LogActualMessage(ex.Message);
            Assert.Contains("chưa được xác thực", ex.Message, StringComparison.OrdinalIgnoreCase);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID10_Login_Fails_WhenAccountIsBanned()
        {
            LogUtcContext("UTCID10",
                "Abnormal path: account bị banned nhưng service hiện tại chưa có branch riêng.",
                "Precondition: user tồn tại, password đúng, status = BANNED.",
                "Input: existing@gmail.com / 12345678aa.",
                "Kỳ vọng hiện tại của product: mọi status khác ACTIVE đều trả thông báo chưa xác thực; không lưu refresh token.");

            var email = "existing@gmail.com";
            var user = CreateUser(email, ValidPassword, "BANNED");
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            userRepoMock.Setup(x => x.GetUserByEmail(email)).ReturnsAsync(user);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = ValidPassword
            }));

            LogActualMessage(ex.Message);
            Assert.Contains("chưa được xác thực", ex.Message, StringComparison.OrdinalIgnoreCase);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID11_Login_Fails_WhenAccessTokenIsNull()
        {
            LogUtcContext("UTCID11",
                "Abnormal path: token service trả access token null.",
                "Precondition: user ACTIVE tồn tại, password đúng, nhưng GenerateAccessToken trả null.",
                "Input: existing@gmail.com / 12345678aa.",
                "Kỳ vọng: throw NullReferenceException Access token generation failed.; không lưu refresh token.");

            var email = "existing@gmail.com";
            var user = CreateUser(email, ValidPassword, "ACTIVE");
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            userRepoMock.Setup(x => x.GetUserByEmail(email)).ReturnsAsync(user);
            tokenServiceMock.Setup(x => x.GenerateAccessToken(user)).Returns((string?)null);

            var ex = await Assert.ThrowsAsync<NullReferenceException>(() => sut.LoginAsync(new LoginRequest
            {
                Email = email,
                Password = ValidPassword
            }));

            LogActualMessage(ex.Message);
            Assert.Equal("Access token generation failed.", ex.Message);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.Verify(x => x.GenerateAccessToken(user), Times.Once);
            tokenServiceMock.Verify(x => x.GenerateRefreshToken(), Times.Never);
        }
    }
}
