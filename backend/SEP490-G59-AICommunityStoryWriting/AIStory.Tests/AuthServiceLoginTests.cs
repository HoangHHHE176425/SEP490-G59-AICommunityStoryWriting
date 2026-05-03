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
    public class UT02_FunctionLoginCustomer
    {
        private readonly ITestOutputHelper _output;
        private const string ValidPassword = "12345678aa";

        public UT02_FunctionLoginCustomer(ITestOutputHelper output) => _output = output;

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

        private void LogTokenStore(string label, IReadOnlyList<auth_tokens> tokenStore)
        {
            _output.WriteLine("");
            _output.WriteLine($"======== {label} - authTokenStore ({tokenStore.Count} phan tu) ========");
            if (tokenStore.Count == 0)
            {
                _output.WriteLine("  (rong)");
                return;
            }

            for (var i = 0; i < tokenStore.Count; i++)
            {
                var token = tokenStore[i];
                _output.WriteLine(
                    $"  [{i}] id={token.id}, user_id={token.user_id}, refresh_token={token.refresh_token}, device={token.device_info}, expires_at={token.expires_at:O}");
            }
        }

        private static AuthService CreateSut(
            List<users> userStore,
            List<auth_tokens> tokenStore,
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
            userRepoMock.Setup(x => x.GetUserById(It.IsAny<Guid>()))
                .ReturnsAsync((Guid id) => userStore.FirstOrDefault(u => u.id == id));
            userRepoMock.Setup(x => x.AddUser(It.IsAny<users>()))
                .Callback((users user) => userStore.Add(user))
                .Returns(Task.CompletedTask);
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
            userRepoMock.Setup(x => x.AddRefreshToken(It.IsAny<auth_tokens>()))
                .Callback((auth_tokens token) => tokenStore.Add(token))
                .Returns(Task.CompletedTask);
            userRepoMock.Setup(x => x.GetRefreshToken(It.IsAny<string>()))
                .ReturnsAsync((string refreshToken) => tokenStore.FirstOrDefault(t => t.refresh_token == refreshToken));
            userRepoMock.Setup(x => x.DeleteRefreshToken(It.IsAny<string>()))
                .Callback((string refreshToken) => tokenStore.RemoveAll(t => t.refresh_token == refreshToken))
                .Returns(Task.CompletedTask);
            userRepoMock.Setup(x => x.IsNicknameExist(It.IsAny<string>(), It.IsAny<Guid>()))
                .ReturnsAsync((string nickname, Guid currentUserId) => userStore.Any(u =>
                    u.id != currentUserId &&
                    string.Equals(u.user_profiles?.nickname, nickname, StringComparison.OrdinalIgnoreCase)));

            return new AuthService(userRepoMock.Object, otpRepoMock.Object, emailServiceMock.Object, tokenServiceMock.Object);
        }

        private static users CreateUser(string email, string password, string? status = "ACTIVE")
        {
            return new users
            {
                id = Guid.NewGuid(),
                email = email,
                password_hash = BCrypt.Net.BCrypt.HashPassword(password),
                role = "USER",
                status = status,
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };
        }

        [Fact]
        public async Task UTCID01_Login_Result_WhenEmailDoesNotExist()
        {
            // Arrange
            var email = "notfound@gmail.com";
            var userStore = new List<users>();
            var tokenStore = new List<auth_tokens>();
            var sut = CreateSut(userStore, tokenStore, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);
            var request = new LoginRequest { Email = email, Password = ValidPassword };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.LoginAsync(request));
            LogTestCase("UTCID01", "Email khong ton tai -> login fail, khong luu refresh token.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(tokenStore);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
            LogTokenStore("UTCID01 (sau verify)", tokenStore);
        }

        [Fact]
        public async Task UTCID02_Login_Result_WhenEmailPasswordAndStatusAreValid()
        {
            // Arrange
            var email = "existing@gmail.com";
            var user = CreateUser(email, ValidPassword, "ACTIVE");
            var userStore = new List<users> { user };
            var tokenStore = new List<auth_tokens>();
            var sut = CreateSut(userStore, tokenStore, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);
            var request = new LoginRequest { Email = email, Password = ValidPassword };
            tokenServiceMock.Setup(x => x.GenerateAccessToken(user)).Returns("access-token");
            tokenServiceMock.Setup(x => x.GenerateRefreshToken()).Returns("refresh-token");

            // Act
            var dto = await sut.LoginAsync(request);
            LogTestCase("UTCID02", "Email, password va status ACTIVE hop le -> login thanh cong.", request, dto);

            // Assert
            Assert.NotNull(dto);
            Assert.Equal("access-token", dto.AccessToken);
            Assert.Equal("refresh-token", dto.RefreshToken);
            Assert.Single(tokenStore);
            Assert.Equal(user.id, tokenStore[0].user_id);
            Assert.Equal(dto.RefreshToken, tokenStore[0].refresh_token);
            Assert.Equal("Unknown", tokenStore[0].device_info);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Once);
            tokenServiceMock.Verify(x => x.GenerateAccessToken(user), Times.Once);
            tokenServiceMock.Verify(x => x.GenerateRefreshToken(), Times.Once);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            LogTokenStore("UTCID02 (sau verify)", tokenStore);
        }

        [Fact]
        public async Task UTCID03_Login_Result_WhenEmailIsNull()
        {
            // Arrange
            var userStore = new List<users>();
            var tokenStore = new List<auth_tokens>();
            var sut = CreateSut(userStore, tokenStore, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);
            var request = new LoginRequest { Email = null!, Password = ValidPassword };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.LoginAsync(request));
            LogTestCase("UTCID03", "Email null -> login fail, khong luu refresh token.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(tokenStore);
            userRepoMock.Verify(x => x.GetUserByEmail(null!), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
            LogTokenStore("UTCID03 (sau verify)", tokenStore);
        }

        [Fact]
        public async Task UTCID04_Login_Result_WhenEmailFormatIsInvalid()
        {
            // Arrange
            var email = "invalid-email";
            var userStore = new List<users>();
            var tokenStore = new List<auth_tokens>();
            var sut = CreateSut(userStore, tokenStore, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);
            var request = new LoginRequest { Email = email, Password = ValidPassword };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.LoginAsync(request));
            LogTestCase("UTCID04", "Email sai format va khong tim thay user -> login fail, khong luu refresh token.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(tokenStore);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
            LogTokenStore("UTCID04 (sau verify)", tokenStore);
        }

        [Fact]
        public async Task UTCID05_Login_Result_WhenPasswordIsWrong()
        {
            // Arrange
            var email = "existing@gmail.com";
            var user = CreateUser(email, ValidPassword, "ACTIVE");
            var userStore = new List<users> { user };
            var tokenStore = new List<auth_tokens>();
            var sut = CreateSut(userStore, tokenStore, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);
            var request = new LoginRequest { Email = email, Password = "wrong-pass" };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.LoginAsync(request));
            LogTestCase("UTCID05", "Password sai -> login fail, khong luu refresh token.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(tokenStore);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
            LogTokenStore("UTCID05 (sau verify)", tokenStore);
        }

        [Fact]
        public async Task UTCID06_Login_Result_WhenPasswordIsNull()
        {
            // Arrange
            var email = "existing@gmail.com";
            var user = CreateUser(email, ValidPassword, "ACTIVE");
            var userStore = new List<users> { user };
            var tokenStore = new List<auth_tokens>();
            var sut = CreateSut(userStore, tokenStore, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);
            var request = new LoginRequest { Email = email, Password = null! };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.LoginAsync(request));
            LogTestCase("UTCID06", "Password null -> BCrypt verify fail, khong luu refresh token.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(tokenStore);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
            LogTokenStore("UTCID06 (sau verify)", tokenStore);
        }

        [Fact]
        public async Task UTCID07_Login_Result_WhenAccountIsPending()
        {
            // Arrange
            var email = "existing@gmail.com";
            var user = CreateUser(email, ValidPassword, "PENDING");
            var userStore = new List<users> { user };
            var tokenStore = new List<auth_tokens>();
            var sut = CreateSut(userStore, tokenStore, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);
            var request = new LoginRequest { Email = email, Password = ValidPassword };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.LoginAsync(request));
            LogTestCase("UTCID07", "Account PENDING -> login fail, khong luu refresh token.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(tokenStore);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
            LogTokenStore("UTCID07 (sau verify)", tokenStore);
        }

        [Fact]
        public async Task UTCID08_Login_Result_WhenStatusIsInvalid()
        {
            // Arrange
            var email = "existing@gmail.com";
            var user = CreateUser(email, ValidPassword, "INVALID");
            var userStore = new List<users> { user };
            var tokenStore = new List<auth_tokens>();
            var sut = CreateSut(userStore, tokenStore, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);
            var request = new LoginRequest { Email = email, Password = ValidPassword };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.LoginAsync(request));
            LogTestCase("UTCID08", "Status khac ACTIVE -> login fail, khong luu refresh token.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(tokenStore);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
            LogTokenStore("UTCID08 (sau verify)", tokenStore);
        }

        [Fact]
        public async Task UTCID09_Login_Result_WhenStatusIsNull()
        {
            // Arrange
            var email = "existing@gmail.com";
            var user = CreateUser(email, ValidPassword, status: null);
            var userStore = new List<users> { user };
            var tokenStore = new List<auth_tokens>();
            var sut = CreateSut(userStore, tokenStore, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);
            var request = new LoginRequest { Email = email, Password = ValidPassword };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.LoginAsync(request));
            LogTestCase("UTCID09", "Status null -> login fail, khong luu refresh token.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(tokenStore);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
            LogTokenStore("UTCID09 (sau verify)", tokenStore);
        }

        [Fact]
        public async Task UTCID10_Login_Result_WhenAccountIsBanned()
        {
            // Arrange
            var email = "existing@gmail.com";
            var user = CreateUser(email, ValidPassword, "BANNED");
            var userStore = new List<users> { user };
            var tokenStore = new List<auth_tokens>();
            var sut = CreateSut(userStore, tokenStore, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);
            var request = new LoginRequest { Email = email, Password = ValidPassword };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.LoginAsync(request));
            LogTestCase("UTCID10", "Account BANNED -> login fail, khong luu refresh token.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(tokenStore);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
            LogTokenStore("UTCID10 (sau verify)", tokenStore);
        }

        [Fact]
        public async Task UTCID11_Login_Result_WhenAccessTokenIsNull()
        {
            // Arrange
            var email = "existing@gmail.com";
            var user = CreateUser(email, ValidPassword, "ACTIVE");
            var userStore = new List<users> { user };
            var tokenStore = new List<auth_tokens>();
            var sut = CreateSut(userStore, tokenStore, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);
            var request = new LoginRequest { Email = email, Password = ValidPassword };
            tokenServiceMock.Setup(x => x.GenerateAccessToken(user)).Returns((string?)null);

            // Act
            var ex = await Record.ExceptionAsync(() => sut.LoginAsync(request));
            LogTestCase("UTCID11", "Token service tra access token null -> login fail, khong luu refresh token.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(tokenStore);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            tokenServiceMock.Verify(x => x.GenerateAccessToken(user), Times.Once);
            tokenServiceMock.Verify(x => x.GenerateRefreshToken(), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            LogTokenStore("UTCID11 (sau verify)", tokenStore);
        }

        [Fact]
        public async Task UTCID12_GoogleLogin_Result_WhenExistingAccountIsBanned()
        {
            // Arrange
            var email = "banned@gmail.com";
            var user = CreateUser(email, ValidPassword, "BANNED");
            var userStore = new List<users> { user };
            var tokenStore = new List<auth_tokens>();
            var sut = CreateSut(userStore, tokenStore, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);
            var input = new { Email = email, FullName = "Banned User", GoogleSubject = "google-subject" };

            // Act
            var ex = await Record.ExceptionAsync(() => sut.LoginWithGoogleAsync(input.Email, input.FullName, input.GoogleSubject));
            LogTestCase("UTCID12", "Google login voi account da BANNED -> fail, khong update user, khong luu refresh token.", input, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(tokenStore);
            userRepoMock.Verify(x => x.GetUserByEmail(email), Times.Once);
            userRepoMock.Verify(x => x.UpdateUser(It.IsAny<users>()), Times.Never);
            userRepoMock.Verify(x => x.AddUser(It.IsAny<users>()), Times.Never);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
            LogTokenStore("UTCID12 (sau verify)", tokenStore);
        }

        [Fact]
        public async Task UTCID13_Refresh_Result_WhenAccountIsBanned()
        {
            // Arrange
            var email = "banned@gmail.com";
            var user = CreateUser(email, ValidPassword, "BANNED");
            var tokenRow = new auth_tokens
            {
                id = Guid.NewGuid(),
                user_id = user.id,
                refresh_token = "refresh-token",
                device_info = "Browser",
                expires_at = DateTime.UtcNow.AddDays(30),
                created_at = DateTime.UtcNow
            };
            var userStore = new List<users> { user };
            var tokenStore = new List<auth_tokens> { tokenRow };
            var sut = CreateSut(userStore, tokenStore, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            var ex = await Record.ExceptionAsync(() => sut.RefreshAsync(tokenRow.refresh_token));
            LogTestCase("UTCID13", "Refresh token cua account BANNED -> fail, revoke token cu, khong phat hanh token moi.", new { tokenRow.refresh_token }, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(tokenStore);
            userRepoMock.Verify(x => x.GetRefreshToken(tokenRow.refresh_token), Times.Once);
            userRepoMock.Verify(x => x.GetUserById(user.id), Times.Once);
            userRepoMock.Verify(x => x.DeleteRefreshToken(tokenRow.refresh_token), Times.Once);
            userRepoMock.Verify(x => x.AddRefreshToken(It.IsAny<auth_tokens>()), Times.Never);
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
            tokenServiceMock.VerifyNoOtherCalls();
            LogTokenStore("UTCID13 (sau verify)", tokenStore);
        }
    }
}
