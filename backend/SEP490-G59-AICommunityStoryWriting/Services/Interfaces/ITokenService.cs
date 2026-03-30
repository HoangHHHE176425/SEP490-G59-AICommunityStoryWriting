using BusinessObjects.Entities;

namespace Services.Interfaces
{
    public interface ITokenService
    {
        string? GenerateAccessToken(users user);
        string? GenerateRefreshToken();
    }
}
