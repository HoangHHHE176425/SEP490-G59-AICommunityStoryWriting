using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AIStory.API.Hubs;

/// SignalR hub for real-time notifications to users (e.g. author khi moderator duyệt/từ chối).
/// Client subscribe để nhận NewNotification khi có thông báo mới.
[Authorize]
public class NotificationHub : Hub
{
    public const string NewNotification = "NewNotification";
}
