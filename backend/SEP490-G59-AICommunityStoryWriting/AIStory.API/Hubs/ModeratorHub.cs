using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace AIStory.API.Hubs;

/// SignalR hub for moderator dashboard real-time updates.
/// Clients subscribe to receive PendingListChanged when stories/chapters are submitted or moderated.
[Authorize(Roles = "MODERATOR,ADMIN")]
public class ModeratorHub : Hub
{
    public const string PendingListChanged = "PendingListChanged";
}
