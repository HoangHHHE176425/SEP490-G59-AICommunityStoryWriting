using Microsoft.AspNetCore.SignalR;
using Services.Interfaces;

namespace AIStory.API.Hubs;

public class ModerationHubNotifier : IModerationHubNotifier
{
    private readonly IHubContext<ModeratorHub> _hubContext;

    public ModerationHubNotifier(IHubContext<ModeratorHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyPendingListChangedAsync()
    {
        await _hubContext.Clients.All.SendAsync(ModeratorHub.PendingListChanged);
    }
}
