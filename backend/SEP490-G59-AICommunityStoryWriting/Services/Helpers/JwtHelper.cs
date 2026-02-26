using BusinessObjects.Entities; // Đảm bảo namespace này chứa class 'user'
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace AIStory.Services.Helpers
{
    public class JwtHelper
    {
        private readonly IConfiguration _config;
        public JwtHelper(IConfiguration config) { _config = config; }

        public string GenerateToken(users user)
        {
            var key = _config["Jwt:Key"];
            var issuer = _config["Jwt:Issuer"];
            var audience = _config["Jwt:Audience"];
            var expireMinutesRaw = _config["Jwt:ExpireMinutes"];
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new InvalidOperationException("Missing Jwt:Key configuration.");
            }
            if (string.IsNullOrWhiteSpace(issuer))
            {
                issuer = "AIStory.API";
            }
            if (string.IsNullOrWhiteSpace(audience))
            {
                audience = "AIStory.Client";
            }
            var expireMinutes = 120;
            if (!string.IsNullOrWhiteSpace(expireMinutesRaw) && int.TryParse(expireMinutesRaw, out var m) && m > 0)
            {
                expireMinutes = m;
            }

            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var role = (user.role ?? "USER").Trim().ToUpperInvariant();
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.id.ToString()), // Guid -> String (JWT standard)
                new Claim(ClaimTypes.NameIdentifier, user.id.ToString()), // ASP.NET Core standard
                new Claim(JwtRegisteredClaimNames.Email, user.email),
                new Claim(ClaimTypes.Role, role), // ASP.NET Core role claim
                new Claim("role", role) // Mapped by JwtBearer RoleClaimType="role"
            };

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expireMinutes),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}