namespace Services.Interfaces;

/// Notifier for real-time moderator dashboard updates (e.g. SignalR).
/// When pending stories/chapters change, backend calls this so connected clients refresh immediately.
public interface IModerationHubNotifier
{
    /// Notify all connected moderator clients to refresh their pending lists.
    Task NotifyPendingListChangedAsync();
}
