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

        Task<AuthResponse> LoginAsync(DTOs.Auth.LoginRequest request);
        Task<AuthResponse> RefreshAsync(string refreshToken);
        Task LogoutAsync(string refreshToken);
        Task ForgotPasswordAsync(DTOs.Auth.ForgotPasswordRequest request);
        Task ResetPasswordAsync(DTOs.Auth.ResetPasswordRequest request);
    }
}
