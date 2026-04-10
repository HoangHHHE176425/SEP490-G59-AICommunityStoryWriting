using Microsoft.AspNetCore.SignalR;
using Services.DTOs.Notifications;
using Services.Interfaces;

namespace AIStory.API.Hubs;

public class NotificationHubNotifier : INotificationHubNotifier
{
    private readonly IHubContext<NotificationHub> _hubContext;

    public NotificationHubNotifier(IHubContext<NotificationHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyUserAsync(Guid userId, NotificationDto notification)
    {
        await _hubContext.Clients.User(userId.ToString()).SendAsync(NotificationHub.NewNotification, notification);
    }

    public async Task RevokeUserSessionAsync(Guid userId, string message)
    {
        await _hubContext.Clients.User(userId.ToString()).SendAsync(NotificationHub.SessionRevoked, new
        {
            Message = message
        });
    }
}
