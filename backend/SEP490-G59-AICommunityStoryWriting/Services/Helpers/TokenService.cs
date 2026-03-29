using BusinessObjects.Entities;
using Services.Interfaces;
using System.Security.Cryptography;

namespace AIStory.Services.Helpers
{
    public class TokenService : ITokenService
    {
        private readonly JwtHelper _jwtHelper;

        public TokenService(JwtHelper jwtHelper)
        {
            _jwtHelper = jwtHelper;
        }

        public string? GenerateAccessToken(users user)
        {
            return _jwtHelper.GenerateToken(user);
        }

        public string? GenerateRefreshToken()
        {
            var bytes = RandomNumberGenerator.GetBytes(64);
            return Convert.ToBase64String(bytes)
                .Replace("+", "-")
                .Replace("/", "_")
                .Replace("=", "");
        }
    }
}
