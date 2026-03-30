using AIStory.Services.Implementations;
using BusinessObjects.Entities;
using Moq;
using Repositories.Interfaces;
using Services.DTOs.Auth;
using Services.Interfaces;
using Xunit.Abstractions;

namespace AIStory.Tests
{
    public class UT03_FunctionRegisterCustomer
    {
        private readonly ITestOutputHelper _output;
        private const string ValidPassword = "A12345678";

        public UT03_FunctionRegisterCustomer(ITestOutputHelper output) => _output = output;

        private void LogUtcContext(string utcId, string oneLineGoal, params string[] details)
        {
            _output.WriteLine("");
            _output.WriteLine($"======== {utcId} | UT03 RegisterCustomer ========");
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

        private void LogRegisterSuccess(users user, otp_verifications? otp = null, string? email = null)
        {
            var line =
                $"Actual log message: Register succeeded. UserStatus={user.status}, Nickname={user.user_profiles?.nickname}, OtpGenerated={(otp != null)}, EmailTarget={email ?? user.email}";
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

        private static RegisterRequest CreateRequest(
            string email = "valid@gmail.com",
            string password = ValidPassword,
            string? confirmPassword = null,
            string? fullName = "Test User")
        {
            return new RegisterRequest
            {
                Email = email,
                Password = password,
                ConfirmPassword = confirmPassword ?? password,
                FullName = fullName
            };
        }

        [Fact]
        public async Task UTCID01_Register_Fails_WhenEmailAlreadyExists()
        {
            LogUtcContext("UTCID01",
                "Abnormal path: email đã tồn tại -> register fail ngay từ đầu.",
                "Precondition: IsEmailExist = true.",
                "Input: existing@gmail.com / A12345678 / bất kỳ fullName.",
                "Kỳ vọng: throw Exception Email already exists.; không AddUser, không AddOtp, không gửi mail.");

            var request = CreateRequest(email: "existing@gmail.com", fullName: "Hung Nguyen");

            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            userRepoMock.Setup(x => x.IsEmailExist(request.Email)).ReturnsAsync(true);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.RegisterAsync(request));

            LogActualMessage(ex.Message);
            Assert.Equal("Email already exists.", ex.Message);
            userRepoMock.Verify(x => x.IsEmailExist(request.Email), Times.Once);
            userRepoMock.Verify(x => x.IsNicknameExist(It.IsAny<string>(), It.IsAny<Guid>()), Times.Never);
            userRepoMock.Verify(x => x.AddUser(It.IsAny<users>()), Times.Never);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Never);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task UTCID02_Register_Succeeds_WhenAllInputsValid()
        {
            LogUtcContext("UTCID02",
                "Happy path: email mới, nickname chưa trùng -> tạo user PENDING, tạo OTP và gửi email.",
                "Precondition: IsEmailExist = false; IsNicknameExist(fullName) = false.",
                "Input: fullName = Hung Nguyen, email = newuser@gmail.com, password hợp lệ.",
                "Kỳ vọng: AddUser/AddOtp/SendEmailAsync đều được gọi đúng 1 lần.");

            var request = CreateRequest(email: "newuser@gmail.com", fullName: "Hung Nguyen");
            users? addedUser = null;
            otp_verifications? addedOtp = null;
            string? sentTo = null;
            string? sentSubject = null;
            string? sentBody = null;
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            userRepoMock.Setup(x => x.IsEmailExist(request.Email)).ReturnsAsync(false);
            userRepoMock.Setup(x => x.IsNicknameExist("Hung Nguyen", It.IsAny<Guid>())).ReturnsAsync(false);
            userRepoMock.Setup(x => x.AddUser(It.IsAny<users>()))
                .Callback<users>(u => addedUser = u)
                .Returns(Task.CompletedTask);
            otpRepoMock.Setup(x => x.AddOtp(It.IsAny<otp_verifications>()))
                .Callback<otp_verifications>(o => addedOtp = o)
                .Returns(Task.CompletedTask);
            emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<string, string, string>((to, subject, body) =>
                {
                    sentTo = to;
                    sentSubject = subject;
                    sentBody = body;
                })
                .Returns(Task.CompletedTask);

            await sut.RegisterAsync(request);

            LogRegisterSuccess(addedUser!, addedOtp, sentTo);
            Assert.NotNull(addedUser);
            Assert.Equal(request.Email, addedUser!.email);
            Assert.Equal("USER", addedUser.role);
            Assert.Equal("PENDING", addedUser.status);
            Assert.Equal("Hung Nguyen", addedUser.user_profiles!.nickname);
            Assert.True(BCrypt.Net.BCrypt.Verify(request.Password, addedUser.password_hash));
            Assert.NotNull(addedOtp);
            Assert.Equal(addedUser.id, addedOtp!.user_id);
            Assert.Equal("EMAIL_VERIFICATION", addedOtp.type);
            Assert.Matches("^[0-9]{6}$", addedOtp.otp_code);
            Assert.Equal(request.Email, sentTo);
            Assert.Equal("Xác thực tài khoản", sentSubject);
            Assert.NotNull(sentBody);
            Assert.Contains(addedOtp.otp_code, sentBody);

            userRepoMock.Verify(x => x.IsNicknameExist("Hung Nguyen", It.IsAny<Guid>()), Times.Once);
            userRepoMock.Verify(x => x.AddUser(It.IsAny<users>()), Times.Once);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Once);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task UTCID03_Register_Fails_WhenEmailIsNull()
        {
            LogUtcContext("UTCID03",
                "Abnormal path: email null phải bị BE chặn.",
                "Precondition: request.Email = null.",
                "Input: null / A12345678 / confirm đúng.",
                "Kỳ vọng: throw Exception Email is required.; không AddUser, không AddOtp, không gửi mail.");

            var request = CreateRequest(email: null!, fullName: "Test User");
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.RegisterAsync(request));

            LogActualMessage(ex.Message);
            Assert.Equal("Email is required.", ex.Message);
            userRepoMock.VerifyNoOtherCalls();
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID04_Register_Fails_WhenEmailFormatIsInvalid()
        {
            LogUtcContext("UTCID04",
                "Abnormal path: email sai format phải bị BE chặn.",
                "Precondition: request.Email = invalid.email.com.",
                "Input: invalid.email.com / A12345678 / confirm đúng.",
                "Kỳ vọng: throw Exception Invalid Email format; không AddUser, không AddOtp, không gửi mail.");

            var request = CreateRequest(email: "invalid.email.com", fullName: "Test User");
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.RegisterAsync(request));

            LogActualMessage(ex.Message);
            Assert.Equal("Invalid Email format", ex.Message);
            userRepoMock.VerifyNoOtherCalls();
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID05_Register_Succeeds_WhenPasswordMeetsBoundaryRule()
        {
            LogUtcContext("UTCID05",
                "Boundary happy path: password hợp lệ theo rule tối thiểu vẫn cho đăng ký thành công.",
                "Precondition: email chưa tồn tại; nickname unique.",
                "Input: valid@gmail.com / Abcd1234 / Test User.",
                "Kỳ vọng: đăng ký thành công, có AddUser/AddOtp/SendEmail.");

            var request = CreateRequest(password: "Abcd1234");
            users? addedUser = null;
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            userRepoMock.Setup(x => x.IsEmailExist(request.Email)).ReturnsAsync(false);
            userRepoMock.Setup(x => x.IsNicknameExist("Test User", It.IsAny<Guid>())).ReturnsAsync(false);
            userRepoMock.Setup(x => x.AddUser(It.IsAny<users>()))
                .Callback<users>(u => addedUser = u)
                .Returns(Task.CompletedTask);
            otpRepoMock.Setup(x => x.AddOtp(It.IsAny<otp_verifications>())).Returns(Task.CompletedTask);
            emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            await sut.RegisterAsync(request);

            LogRegisterSuccess(addedUser!);
            Assert.NotNull(addedUser);
            Assert.True(BCrypt.Net.BCrypt.Verify("Abcd1234", addedUser!.password_hash));
            userRepoMock.Verify(x => x.IsNicknameExist("Test User", It.IsAny<Guid>()), Times.Once);
            userRepoMock.Verify(x => x.AddUser(It.IsAny<users>()), Times.Once);
            otpRepoMock.Verify(x => x.AddOtp(It.IsAny<otp_verifications>()), Times.Once);
            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
        }

        [Fact]
        public async Task UTCID06_Register_Fails_WhenPasswordIsTooShort()
        {
            LogUtcContext("UTCID06",
                "Abnormal path: password quá ngắn phải bị BE chặn.",
                "Precondition: request.Password = 12345, confirm đúng.",
                "Input: valid@gmail.com / 12345 / Test User.",
                "Kỳ vọng: throw Exception Password too short; không AddUser, không AddOtp, không gửi mail.");

            var request = CreateRequest(password: "12345", confirmPassword: "12345");
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.RegisterAsync(request));

            LogActualMessage(ex.Message);
            Assert.Equal("Password too short", ex.Message);
            userRepoMock.VerifyNoOtherCalls();
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID07_Register_Fails_WhenPasswordIsNull()
        {
            LogUtcContext("UTCID07",
                "Abnormal path: password null phải bị BE chặn.",
                "Precondition: request.Password = null.",
                "Input: valid@gmail.com / null / confirm null.",
                "Kỳ vọng: throw Exception Password is required.; không AddUser, không AddOtp, không gửi mail.");

            var request = CreateRequest(password: null!, confirmPassword: null!);
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.RegisterAsync(request));

            LogActualMessage(ex.Message);
            Assert.Equal("Password is required.", ex.Message);
            userRepoMock.VerifyNoOtherCalls();
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID08_Register_HashesPasswordWithBCrypt()
        {
            LogUtcContext("UTCID08",
                "Security path: password thô không được lưu trực tiếp, phải được hash bằng BCrypt.",
                "Precondition: email chưa tồn tại; nickname unique.",
                "Input: valid@gmail.com / A12345678 / Test User.",
                "Kỳ vọng: password_hash khác raw password và BCrypt.Verify trả true.");

            var request = CreateRequest();
            users? addedUser = null;
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            userRepoMock.Setup(x => x.IsEmailExist(request.Email)).ReturnsAsync(false);
            userRepoMock.Setup(x => x.IsNicknameExist("Test User", It.IsAny<Guid>())).ReturnsAsync(false);
            userRepoMock.Setup(x => x.AddUser(It.IsAny<users>()))
                .Callback<users>(u => addedUser = u)
                .Returns(Task.CompletedTask);
            otpRepoMock.Setup(x => x.AddOtp(It.IsAny<otp_verifications>())).Returns(Task.CompletedTask);
            emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            await sut.RegisterAsync(request);

            LogRegisterSuccess(addedUser!);
            Assert.NotNull(addedUser);
            Assert.NotEqual(request.Password, addedUser!.password_hash);
            Assert.True(BCrypt.Net.BCrypt.Verify(request.Password, addedUser.password_hash));
        }

        [Fact]
        public async Task UTCID09_Register_TrimsFullName_WhenItHasLeadingAndTrailingSpaces()
        {
            LogUtcContext("UTCID09",
                "Current service behavior: FullName có space đầu/cuối sẽ được trim trước khi tạo nickname.",
                "Precondition: email chưa tồn tại; nickname sau trim chưa trùng.",
                "Input: fullName = '  Test User  '.",
                "Kỳ vọng hiện tại: đăng ký thành công với nickname = 'Test User'.");

            var request = CreateRequest(fullName: "  Test User  ");
            users? addedUser = null;
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            userRepoMock.Setup(x => x.IsEmailExist(request.Email)).ReturnsAsync(false);
            userRepoMock.Setup(x => x.IsNicknameExist("Test User", It.IsAny<Guid>())).ReturnsAsync(false);
            userRepoMock.Setup(x => x.AddUser(It.IsAny<users>()))
                .Callback<users>(u => addedUser = u)
                .Returns(Task.CompletedTask);
            otpRepoMock.Setup(x => x.AddOtp(It.IsAny<otp_verifications>())).Returns(Task.CompletedTask);
            emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            await sut.RegisterAsync(request);

            LogRegisterSuccess(addedUser!);
            Assert.NotNull(addedUser);
            Assert.Equal("Test User", addedUser!.user_profiles!.nickname);
        }

        [Fact]
        public async Task UTCID10_Register_UsesEmailPrefix_WhenFullNameIsNull()
        {
            LogUtcContext("UTCID10",
                "Current service behavior: FullName null không fail mà dùng prefix của email làm nickname.",
                "Precondition: email chưa tồn tại; nickname email prefix chưa trùng.",
                "Input: email = prefix.name@gmail.com, fullName = null.",
                "Kỳ vọng hiện tại: user được tạo với nickname = prefix.name.");

            var request = CreateRequest(email: "prefix.name@gmail.com", fullName: null);
            users? addedUser = null;
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            userRepoMock.Setup(x => x.IsEmailExist(request.Email)).ReturnsAsync(false);
            userRepoMock.Setup(x => x.IsNicknameExist("prefix.name", It.IsAny<Guid>())).ReturnsAsync(false);
            userRepoMock.Setup(x => x.AddUser(It.IsAny<users>()))
                .Callback<users>(u => addedUser = u)
                .Returns(Task.CompletedTask);
            otpRepoMock.Setup(x => x.AddOtp(It.IsAny<otp_verifications>())).Returns(Task.CompletedTask);
            emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            await sut.RegisterAsync(request);

            LogRegisterSuccess(addedUser!);
            Assert.NotNull(addedUser);
            Assert.Equal("prefix.name", addedUser!.user_profiles!.nickname);
        }

        [Fact]
        public async Task UTCID11_Register_TrimsNicknameToMaxLength100_WhenFullNameIsTooLong()
        {
            LogUtcContext("UTCID11",
                "Boundary path: FullName quá dài được truncate nickname về tối đa 100 ký tự.",
                "Precondition: email chưa tồn tại; nickname sau truncate chưa trùng.",
                "Input: FullName dài hơn 100 ký tự.",
                "Kỳ vọng hiện tại: đăng ký thành công, nickname dài 100 ký tự.");

            var longName = new string('N', 120);
            var trimmedName = new string('N', 100);
            var request = CreateRequest(fullName: longName);
            users? addedUser = null;
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            userRepoMock.Setup(x => x.IsEmailExist(request.Email)).ReturnsAsync(false);
            userRepoMock.Setup(x => x.IsNicknameExist(trimmedName, It.IsAny<Guid>())).ReturnsAsync(false);
            userRepoMock.Setup(x => x.AddUser(It.IsAny<users>()))
                .Callback<users>(u => addedUser = u)
                .Returns(Task.CompletedTask);
            otpRepoMock.Setup(x => x.AddOtp(It.IsAny<otp_verifications>())).Returns(Task.CompletedTask);
            emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            await sut.RegisterAsync(request);

            LogRegisterSuccess(addedUser!);
            Assert.NotNull(addedUser);
            Assert.Equal(100, addedUser!.user_profiles!.nickname!.Length);
            Assert.Equal(trimmedName, addedUser.user_profiles.nickname);
        }

        [Fact]
        public async Task UTCID12_Register_UsesBaseNicknameWhenItIsUnique()
        {
            LogUtcContext("UTCID12",
                "Happy path: base nickname unique thì dùng trực tiếp, không thêm suffix.",
                "Precondition: email chưa tồn tại; IsNicknameExist(base) = false.",
                "Input: fullName = BaseUnique.",
                "Kỳ vọng: nickname đúng bằng BaseUnique.");

            var request = CreateRequest(fullName: "BaseUnique");
            users? addedUser = null;
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            userRepoMock.Setup(x => x.IsEmailExist(request.Email)).ReturnsAsync(false);
            userRepoMock.Setup(x => x.IsNicknameExist("BaseUnique", It.IsAny<Guid>())).ReturnsAsync(false);
            userRepoMock.Setup(x => x.AddUser(It.IsAny<users>()))
                .Callback<users>(u => addedUser = u)
                .Returns(Task.CompletedTask);
            otpRepoMock.Setup(x => x.AddOtp(It.IsAny<otp_verifications>())).Returns(Task.CompletedTask);
            emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            await sut.RegisterAsync(request);

            LogRegisterSuccess(addedUser!);
            Assert.Equal("BaseUnique", addedUser!.user_profiles!.nickname);
        }

        [Fact]
        public async Task UTCID13_Register_GeneratesAlternativeNickname_WhenBaseNicknameExists()
        {
            LogUtcContext("UTCID13",
                "Abnormal-but-recoverable path: nickname gốc đã tồn tại -> service tạo nickname khác và vẫn register thành công.",
                "Precondition: IsEmailExist = false; IsNicknameExist(base) = true; nickname có suffix là false.",
                "Input: fullName = duplicate-name.",
                "Kỳ vọng: AddUser với nickname bắt đầu bằng duplicate-name_ và không vượt quá 100 ký tự.");

            var request = CreateRequest(email: "another@gmail.com", fullName: "duplicate-name");
            users? addedUser = null;
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            userRepoMock.Setup(x => x.IsEmailExist(request.Email)).ReturnsAsync(false);
            userRepoMock.Setup(x => x.IsNicknameExist("duplicate-name", It.IsAny<Guid>())).ReturnsAsync(true);
            userRepoMock.Setup(x => x.IsNicknameExist(It.Is<string>(n => n.StartsWith("duplicate-name_")), It.IsAny<Guid>()))
                .ReturnsAsync(false);
            userRepoMock.Setup(x => x.AddUser(It.IsAny<users>()))
                .Callback<users>(u => addedUser = u)
                .Returns(Task.CompletedTask);
            otpRepoMock.Setup(x => x.AddOtp(It.IsAny<otp_verifications>())).Returns(Task.CompletedTask);
            emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            await sut.RegisterAsync(request);

            LogRegisterSuccess(addedUser!);
            Assert.NotNull(addedUser);
            Assert.StartsWith("duplicate-name_", addedUser!.user_profiles!.nickname);
            Assert.True(addedUser.user_profiles.nickname!.Length <= 100);
        }

        [Fact]
        public async Task UTCID14_Register_UsesGuidSuffixWhenNicknameKeepsColliding()
        {
            LogUtcContext("UTCID14",
                "Boundary path: nickname gốc và 5 lần thử suffix đều bị trùng -> fallback sang GUID suffix.",
                "Precondition: IsEmailExist = false; mọi nickname bắt đầu bằng guid-fallback_ đều bị xem là đã tồn tại trong 5 lần random.",
                "Input: fullName = guid-fallback.",
                "Kỳ vọng: nickname cuối cùng kết thúc bằng _{8 ký tự hex}.");

            var request = CreateRequest(email: "guid@gmail.com", fullName: "guid-fallback");
            users? addedUser = null;
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            userRepoMock.Setup(x => x.IsEmailExist(request.Email)).ReturnsAsync(false);
            userRepoMock.Setup(x => x.IsNicknameExist("guid-fallback", It.IsAny<Guid>())).ReturnsAsync(true);
            userRepoMock.Setup(x => x.IsNicknameExist(It.Is<string>(n => n.StartsWith("guid-fallback_")), It.IsAny<Guid>()))
                .ReturnsAsync(true);
            userRepoMock.Setup(x => x.AddUser(It.IsAny<users>()))
                .Callback<users>(u => addedUser = u)
                .Returns(Task.CompletedTask);
            otpRepoMock.Setup(x => x.AddOtp(It.IsAny<otp_verifications>())).Returns(Task.CompletedTask);
            emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.CompletedTask);

            await sut.RegisterAsync(request);

            LogRegisterSuccess(addedUser!);
            Assert.NotNull(addedUser);
            var nickname = addedUser!.user_profiles!.nickname;
            Assert.NotNull(nickname);
            Assert.Matches("^guid-fallback_[a-f0-9]{8}$", nickname!);
        }

        [Fact]
        public async Task UTCID15_Register_Fails_WhenConfirmPasswordDoesNotMatch()
        {
            LogUtcContext("UTCID15",
                "Abnormal path: confirm password không khớp phải bị BE chặn.",
                "Precondition: password và confirmPassword khác nhau.",
                "Input: A12345678 / A12345679.",
                "Kỳ vọng: throw Exception Password not match; không AddUser, không AddOtp, không gửi mail.");

            var request = CreateRequest(password: "A12345678", confirmPassword: "A12345679");
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.RegisterAsync(request));

            LogActualMessage(ex.Message);
            Assert.Equal("Password not match", ex.Message);
            userRepoMock.VerifyNoOtherCalls();
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
        }

        [Fact]
        public async Task UTCID16_Register_Fails_WhenConfirmPasswordIsNull()
        {
            LogUtcContext("UTCID16",
                "Abnormal path: confirm password null phải bị BE chặn.",
                "Precondition: request.ConfirmPassword = null.",
                "Input: A12345678 / null.",
                "Kỳ vọng: throw Exception Confirm password is required.; không AddUser, không AddOtp, không gửi mail.");

            var request = new RegisterRequest
            {
                Email = "valid@gmail.com",
                Password = "A12345678",
                ConfirmPassword = null!,
                FullName = "Test User"
            };
            var sut = CreateSut(out var userRepoMock, out var otpRepoMock, out var emailServiceMock);

            var ex = await Assert.ThrowsAsync<Exception>(() => sut.RegisterAsync(request));

            LogActualMessage(ex.Message);
            Assert.Equal("Confirm password is required.", ex.Message);
            userRepoMock.VerifyNoOtherCalls();
            otpRepoMock.VerifyNoOtherCalls();
            emailServiceMock.VerifyNoOtherCalls();
        }
    }
}
