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
    public class UT03_FunctionRegisterCustomer
    {
        private readonly ITestOutputHelper _output;
        private const string ValidPassword = "A12345678";

        public UT03_FunctionRegisterCustomer(ITestOutputHelper output) => _output = output;

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
                _output.WriteLine($"  user id={user.id}, email={user.email}, status={user.status}, nickname={user.user_profiles?.nickname}");
            }

            foreach (var otp in otpStore)
            {
                _output.WriteLine($"  otp id={otp.id}, user_id={otp.user_id}, type={otp.type}, code={otp.otp_code}, is_used={otp.is_used}");
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

            userRepoMock.Setup(x => x.IsEmailExist(It.IsAny<string>()))
                .ReturnsAsync((string email) => userStore.Any(u => string.Equals(u.email, email, StringComparison.OrdinalIgnoreCase)));
            userRepoMock.Setup(x => x.IsNicknameExist(It.IsAny<string>(), It.IsAny<Guid>()))
                .ReturnsAsync((string nickname, Guid currentUserId) => userStore.Any(u =>
                    u.id != currentUserId &&
                    string.Equals(u.user_profiles?.nickname, nickname, StringComparison.OrdinalIgnoreCase)));
            userRepoMock.Setup(x => x.AddUser(It.IsAny<users>()))
                .Callback((users user) => userStore.Add(user))
                .Returns(Task.CompletedTask);

            otpRepoMock.Setup(x => x.AddOtp(It.IsAny<otp_verifications>()))
                .Callback((otp_verifications otp) => otpStore.Add(otp))
                .Returns(Task.CompletedTask);

            emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback((string to, string subject, string body) => sentEmails.Add(new SentEmail(to, subject, body)))
                .Returns(Task.CompletedTask);

            return new AuthService(userRepoMock.Object, otpRepoMock.Object, emailServiceMock.Object, tokenServiceMock.Object);
        }

        private static RegisterRequest CreateRequest(
            string? email = "valid@gmail.com",
            string? password = ValidPassword,
            string? confirmPassword = null,
            string? fullName = "Test User")
        {
            return new RegisterRequest
            {
                Email = email!,
                Password = password!,
                ConfirmPassword = confirmPassword ?? password!,
                FullName = fullName
            };
        }

        private static users ExistingUser(string email, string nickname)
        {
            var userId = Guid.NewGuid();
            return new users
            {
                id = userId,
                email = email,
                password_hash = BCrypt.Net.BCrypt.HashPassword(ValidPassword),
                role = "USER",
                status = "ACTIVE",
                user_profiles = new user_profiles
                {
                    user_id = userId,
                    nickname = nickname,
                    settings = "{\"allow_notif\":true}",
                    updated_at = DateTime.UtcNow
                },
                created_at = DateTime.UtcNow,
                updated_at = DateTime.UtcNow
            };
        }

        private static void AssertNoSaveCalls(Mock<IUserRepository> userRepoMock, Mock<IOtpRepository> otpRepoMock, Mock<IEmailService> emailServiceMock)
        {
            userRepoMock.Verify(x => x.AddUser(It.IsAny<users>()), Times.Never);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Never);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        private static void AssertRegisterSuccess(RegisterRequest request, List<users> userStore, List<otp_verifications> otpStore, List<SentEmail> sentEmails)
        {
            Assert.Single(userStore);
            Assert.Single(otpStore);
            Assert.Single(sentEmails);

            var user = userStore[0];
            var otp = otpStore[0];
            var sentEmail = sentEmails[0];

            Assert.Equal(request.Email, user.email);
            Assert.Equal("USER", user.role);
            Assert.Equal("PENDING", user.status);
            Assert.True(BCrypt.Net.BCrypt.Verify(request.Password, user.password_hash));
            Assert.Equal(user.id, user.user_profiles!.user_id);
            Assert.Equal(user.id, otp.user_id);
            Assert.Equal("EMAIL_VERIFICATION", otp.type);
            Assert.False(otp.is_used ?? true);
            Assert.Matches("^[0-9]{6}$", otp.otp_code);
            Assert.Equal(request.Email, sentEmail.To);
            Assert.Equal("Xác thực tài khoản", sentEmail.Subject);
            Assert.Contains(otp.otp_code, sentEmail.Body);
        }

        [Fact]
        public async Task UTCID01_Register_Result_WhenEmailAlreadyExists()
        {
            // Arrange
            var request = CreateRequest(email: "existing@gmail.com", fullName: "Hung Nguyen");
            var userStore = new List<users> { ExistingUser(request.Email, "Existing User") };
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            var ex = await Record.ExceptionAsync(() => sut.RegisterAsync(request));
            LogTestCase("UTCID01", "Email da ton tai -> register fail, khong AddUser/AddOtp/gui mail.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Single(userStore);
            Assert.Empty(otpStore);
            Assert.Empty(sentEmails);
            userRepoMock.Verify(x => x.IsEmailExist(request.Email), Times.Once);
            userRepoMock.Verify(x => x.IsNicknameExist(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
            AssertNoSaveCalls(userRepoMock, otpRepoMock, emailServiceMock);
            tokenServiceMock.VerifyNoOtherCalls();
            LogStores("UTCID01 (sau verify)", userStore, otpStore, sentEmails);
        }

        [Fact]
        public async Task UTCID02_Register_Result_WhenAllInputsValid()
        {
            // Arrange
            var request = CreateRequest(email: "newuser@gmail.com", fullName: "Hung Nguyen");
            var userStore = new List<users>();
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            await sut.RegisterAsync(request);
            LogTestCase("UTCID02", "Input hop le -> tao user PENDING, OTP va email xac thuc.", request, new { User = userStore.Single(), Otp = otpStore.Single(), Email = sentEmails.Single() });

            // Assert
            AssertRegisterSuccess(request, userStore, otpStore, sentEmails);
            Assert.Equal("Hung Nguyen", userStore[0].user_profiles!.nickname);
            userRepoMock.Verify(x => x.IsEmailExist(request.Email), Times.Once);
            userRepoMock.Verify(x => x.IsNicknameExist("Hung Nguyen", It.IsAny<Guid>()), Times.Once);
            userRepoMock.Verify(x => x.AddUser(It.IsAny<users>()), Times.Once);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Once);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            tokenServiceMock.VerifyNoOtherCalls();
            LogStores("UTCID02 (sau verify)", userStore, otpStore, sentEmails);
        }

        [Fact]
        public async Task UTCID03_Register_Result_WhenEmailIsNull()
        {
            // Arrange
            var request = CreateRequest(email: null, fullName: "Test User");
            var userStore = new List<users>();
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            var ex = await Record.ExceptionAsync(() => sut.RegisterAsync(request));
            LogTestCase("UTCID03", "Email null -> fail truoc khi repository duoc goi.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(userStore);
            Assert.Empty(otpStore);
            Assert.Empty(sentEmails);
            userRepoMock.Verify(x => x.AddUser(It.IsAny<users>()), Times.Never);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Never);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID04_Register_Result_WhenEmailFormatIsInvalid()
        {
            // Arrange
            var request = CreateRequest(email: "invalid.email.com", fullName: "Test User");
            var userStore = new List<users>();
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            var ex = await Record.ExceptionAsync(() => sut.RegisterAsync(request));
            LogTestCase("UTCID04", "Email sai format -> fail truoc khi luu du lieu.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(userStore);
            Assert.Empty(otpStore);
            Assert.Empty(sentEmails);
            AssertNoSaveCalls(userRepoMock, otpRepoMock, emailServiceMock);
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID05_Register_Result_WhenPasswordMeetsBoundaryRule()
        {
            // Arrange
            var request = CreateRequest(password: "Abcd1234");
            var userStore = new List<users>();
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            await sut.RegisterAsync(request);
            LogTestCase("UTCID05", "Password hop le theo boundary -> register thanh cong.", request, new { User = userStore.Single(), Otp = otpStore.Single(), Email = sentEmails.Single() });

            // Assert
            AssertRegisterSuccess(request, userStore, otpStore, sentEmails);
            Assert.True(BCrypt.Net.BCrypt.Verify("Abcd1234", userStore[0].password_hash));
            userRepoMock.Verify(x => x.AddUser(It.IsAny<users>()), Times.Once);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Once);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            tokenServiceMock.VerifyNoOtherCalls();
            LogStores("UTCID05 (sau verify)", userStore, otpStore, sentEmails);
        }

        [Fact]
        public async Task UTCID06_Register_Result_WhenPasswordIsTooShort()
        {
            // Arrange
            var request = CreateRequest(password: "12345", confirmPassword: "12345");
            var userStore = new List<users>();
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            var ex = await Record.ExceptionAsync(() => sut.RegisterAsync(request));
            LogTestCase("UTCID06", "Password qua ngan -> fail, khong luu du lieu.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(userStore);
            Assert.Empty(otpStore);
            Assert.Empty(sentEmails);
            AssertNoSaveCalls(userRepoMock, otpRepoMock, emailServiceMock);
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID07_Register_Result_WhenPasswordIsNull()
        {
            // Arrange
            var request = CreateRequest(password: null, confirmPassword: null);
            var userStore = new List<users>();
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            var ex = await Record.ExceptionAsync(() => sut.RegisterAsync(request));
            LogTestCase("UTCID07", "Password null -> fail, khong luu du lieu.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(userStore);
            Assert.Empty(otpStore);
            Assert.Empty(sentEmails);
            AssertNoSaveCalls(userRepoMock, otpRepoMock, emailServiceMock);
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID08_Register_Result_WhenPasswordMustBeHashedWithBCrypt()
        {
            // Arrange
            var request = CreateRequest();
            var userStore = new List<users>();
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            await sut.RegisterAsync(request);
            LogTestCase("UTCID08", "Password raw khong duoc luu truc tiep, phai hash BCrypt.", request, new { User = userStore.Single(), Otp = otpStore.Single(), Email = sentEmails.Single() });

            // Assert
            AssertRegisterSuccess(request, userStore, otpStore, sentEmails);
            Assert.NotEqual(request.Password, userStore[0].password_hash);
            Assert.True(BCrypt.Net.BCrypt.Verify(request.Password, userStore[0].password_hash));
            userRepoMock.Verify(x => x.AddUser(It.IsAny<users>()), Times.Once);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Once);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID09_Register_Result_WhenFullNameHasLeadingAndTrailingSpaces()
        {
            // Arrange
            var request = CreateRequest(fullName: "  Test User  ");
            var userStore = new List<users>();
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            await sut.RegisterAsync(request);
            LogTestCase("UTCID09", "FullName co space dau/cuoi -> nickname duoc trim.", request, new { User = userStore.Single(), Otp = otpStore.Single(), Email = sentEmails.Single() });

            // Assert
            AssertRegisterSuccess(request, userStore, otpStore, sentEmails);
            Assert.Equal("Test User", userStore[0].user_profiles!.nickname);
            userRepoMock.Verify(x => x.IsNicknameExist("Test User", It.IsAny<Guid>()), Times.Once);
            userRepoMock.Verify(x => x.AddUser(It.IsAny<users>()), Times.Once);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Once);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID10_Register_Result_WhenFullNameIsNull()
        {
            // Arrange
            var request = CreateRequest(email: "prefix.name@gmail.com", fullName: null);
            var userStore = new List<users>();
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            await sut.RegisterAsync(request);
            LogTestCase("UTCID10", "FullName null -> dung email prefix lam nickname.", request, new { User = userStore.Single(), Otp = otpStore.Single(), Email = sentEmails.Single() });

            // Assert
            AssertRegisterSuccess(request, userStore, otpStore, sentEmails);
            Assert.Equal("prefix.name", userStore[0].user_profiles!.nickname);
            userRepoMock.Verify(x => x.IsNicknameExist("prefix.name", It.IsAny<Guid>()), Times.Once);
            userRepoMock.Verify(x => x.AddUser(It.IsAny<users>()), Times.Once);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Once);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID11_Register_Result_WhenFullNameIsTooLong()
        {
            // Arrange
            var longName = new string('N', 120);
            var trimmedName = new string('N', 100);
            var request = CreateRequest(fullName: longName);
            var userStore = new List<users>();
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            await sut.RegisterAsync(request);
            LogTestCase("UTCID11", "FullName > 100 ky tu -> nickname bi trim ve 100 ky tu.", new { FullNameLength = longName.Length, request.Email }, new { User = userStore.Single(), Otp = otpStore.Single(), Email = sentEmails.Single() });

            // Assert
            AssertRegisterSuccess(request, userStore, otpStore, sentEmails);
            Assert.Equal(100, userStore[0].user_profiles!.nickname!.Length);
            Assert.Equal(trimmedName, userStore[0].user_profiles!.nickname);
            userRepoMock.Verify(x => x.IsNicknameExist(trimmedName, It.IsAny<Guid>()), Times.Once);
            userRepoMock.Verify(x => x.AddUser(It.IsAny<users>()), Times.Once);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Once);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID12_Register_Result_WhenBaseNicknameIsUnique()
        {
            // Arrange
            var request = CreateRequest(fullName: "BaseUnique");
            var userStore = new List<users>();
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            await sut.RegisterAsync(request);
            LogTestCase("UTCID12", "Base nickname unique -> dung truc tiep nickname goc.", request, new { User = userStore.Single(), Otp = otpStore.Single(), Email = sentEmails.Single() });

            // Assert
            AssertRegisterSuccess(request, userStore, otpStore, sentEmails);
            Assert.Equal("BaseUnique", userStore[0].user_profiles!.nickname);
            userRepoMock.Verify(x => x.IsNicknameExist("BaseUnique", It.IsAny<Guid>()), Times.Once);
            userRepoMock.Verify(x => x.AddUser(It.IsAny<users>()), Times.Once);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Once);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID13_Register_Result_WhenBaseNicknameExists()
        {
            // Arrange
            var request = CreateRequest(email: "another@gmail.com", fullName: "duplicate-name");
            var userStore = new List<users> { ExistingUser("existing-nick@gmail.com", "duplicate-name") };
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            await sut.RegisterAsync(request);
            var addedUser = Assert.Single(userStore, u => u.email == request.Email);
            LogTestCase("UTCID13", "Base nickname da ton tai -> tao nickname co suffix va van register thanh cong.", request, new { User = addedUser, Otp = otpStore.Single(), Email = sentEmails.Single() });

            // Assert
            Assert.Equal(2, userStore.Count);
            Assert.Single(otpStore);
            Assert.Single(sentEmails);
            Assert.StartsWith("duplicate-name_", addedUser.user_profiles!.nickname);
            Assert.True(addedUser.user_profiles.nickname!.Length <= 100);
            userRepoMock.Verify(x => x.IsNicknameExist("duplicate-name", It.IsAny<Guid>()), Times.Once);
            userRepoMock.Verify(x => x.AddUser(It.IsAny<users>()), Times.Once);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Once);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID14_Register_Result_WhenNicknameKeepsColliding()
        {
            // Arrange
            var request = CreateRequest(email: "guid@gmail.com", fullName: "guid-fallback");
            var userStore = new List<users>();
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);
            userRepoMock.Setup(x => x.IsNicknameExist(It.Is<string>(n => n == "guid-fallback" || n.StartsWith("guid-fallback_")), It.IsAny<Guid>()))
                .ReturnsAsync(true);

            // Act
            await sut.RegisterAsync(request);
            LogTestCase("UTCID14", "Nickname goc va 5 suffix deu bi trung -> fallback GUID suffix.", request, new { User = userStore.Single(), Otp = otpStore.Single(), Email = sentEmails.Single() });

            // Assert
            AssertRegisterSuccess(request, userStore, otpStore, sentEmails);
            Assert.Matches("^guid-fallback_[a-f0-9]{8}$", userStore[0].user_profiles!.nickname!);
            userRepoMock.Verify(x => x.AddUser(It.IsAny<users>()), Times.Once);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Once);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID15_Register_Result_WhenConfirmPasswordDoesNotMatch()
        {
            // Arrange
            var request = CreateRequest(password: "A12345678", confirmPassword: "A12345679");
            var userStore = new List<users>();
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            var ex = await Record.ExceptionAsync(() => sut.RegisterAsync(request));
            LogTestCase("UTCID15", "Confirm password khong khop -> fail, khong luu du lieu.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(userStore);
            Assert.Empty(otpStore);
            Assert.Empty(sentEmails);
            AssertNoSaveCalls(userRepoMock, otpRepoMock, emailServiceMock);
            tokenServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID16_Register_Result_WhenConfirmPasswordIsNull()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "valid@gmail.com",
                Password = "A12345678",
                ConfirmPassword = null!,
                FullName = "Test User"
            };
            var userStore = new List<users>();
            var otpStore = new List<otp_verifications>();
            var sentEmails = new List<SentEmail>();
            var sut = CreateSut(userStore, otpStore, sentEmails, out var userRepoMock, out var otpRepoMock, out var emailServiceMock, out var tokenServiceMock);

            // Act
            var ex = await Record.ExceptionAsync(() => sut.RegisterAsync(request));
            LogTestCase("UTCID16", "Confirm password null -> fail, khong luu du lieu.", request, null, ex);

            // Assert
            Assert.NotNull(ex);
            Assert.Empty(userStore);
            Assert.Empty(otpStore);
            Assert.Empty(sentEmails);
            AssertNoSaveCalls(userRepoMock, otpRepoMock, emailServiceMock);
            tokenServiceMock.VerifyNoOtherCalls();
        }
    }
}
