using Services.DTOs.Notifications;

namespace Services.Interfaces;

/// Notifier for real-time push notifications to users (e.g. author khi moderator duyệt/từ chối truyện/chương).
public interface INotificationHubNotifier
{
    /// Gửi thông báo real-time tới user (author). Client đăng ký hub sẽ nhận event NewNotification.
    Task NotifyUserAsync(Guid userId, NotificationDto notification);
}
