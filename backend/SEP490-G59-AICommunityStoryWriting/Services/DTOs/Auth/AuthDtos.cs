using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Services.DTOs.Auth
{
    public class RegisterRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;

        [Required, MinLength(6)]
        public string Password { get; set; } = null!;

        [Required]
        public string ConfirmPassword { get; set; } = null!;

        // Optional display name. If empty/null, server will default to email prefix.
        public string? FullName { get; set; }
    }

    public class LoginRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;
        [Required]
        public string Password { get; set; } = null!;
    }

    public class VerifyOtpRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;
        [Required]
        public string OtpCode { get; set; } = null!;
    }

    public class ResendOtpRequest
    {
        [Required, EmailAddress]
        public string Email { get; set; } = null!;
    }

    public class ResendOtpResponse
    {
        public string Message { get; set; } = string.Empty;
        public int ExpiresInSeconds { get; set; }
    }

    public class AuthResponse
    {
        public string AccessToken { get; set; } = null!;
        public string? RefreshToken { get; set; }
    }

    public class AccessTokenResponse
    {
        public string AccessToken { get; set; } = null!;
    }
}
