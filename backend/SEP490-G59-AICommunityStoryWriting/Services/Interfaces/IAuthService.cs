using Microsoft.AspNetCore.Identity.Data;
using Services.DTOs.Auth;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.Interfaces
{
    public interface IAuthService
    {
        Task RegisterAsync(DTOs.Auth.RegisterRequest request);

        Task VerifyAccountAsync(VerifyOtpRequest request);

        /// <summary>
        /// Gửi lại OTP xác thực email (EMAIL_VERIFICATION) và trả về TTL còn lại.
        /// </summary>
        Task<ResendOtpResponse> ResendOtpAsync(ResendOtpRequest request);

        Task<AuthResponse> LoginAsync(DTOs.Auth.LoginRequest request);
        /// <summary>
        /// Login hoặc tạo user khi Google OAuth redirect code flow trả về email.
        /// </summary>
        Task<AuthResponse> LoginWithGoogleAsync(string email, string? fullName, string googleSubject);
        Task<AuthResponse> RefreshAsync(string refreshToken);
        Task LogoutAsync(string refreshToken);
        Task ForgotPasswordAsync(DTOs.Auth.ForgotPasswordRequest request);
        Task ResetPasswordAsync(DTOs.Auth.ResetPasswordRequest request);
    }
}
