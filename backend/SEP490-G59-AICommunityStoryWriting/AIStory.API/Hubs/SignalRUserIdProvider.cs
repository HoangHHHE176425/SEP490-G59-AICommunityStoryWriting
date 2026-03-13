using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;
using System.IdentityModel.Tokens.Jwt;

namespace AIStory.API.Hubs;

/// <summary>
/// SignalR user id từ JWT: dùng claim "sub" (hoặc NameIdentifier) để Clients.User(userId) khớp với author_id khi push thông báo duyệt/từ chối.
/// </summary>
public class SignalRUserIdProvider : IUserIdProvider
{
    public string? GetUserId(HubConnectionContext connection)
    {
        var sub = connection.User?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                  ?? connection.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrEmpty(sub) ? null : sub;
    }
}
