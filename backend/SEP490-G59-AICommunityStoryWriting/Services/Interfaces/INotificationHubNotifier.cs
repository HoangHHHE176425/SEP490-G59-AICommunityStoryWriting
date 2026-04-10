using Services.DTOs.Notifications;

namespace Services.Interfaces;

/// Notifier for real-time push notifications to users (e.g. author khi moderator duyệt/từ chối truyện/chương).
public interface INotificationHubNotifier
{
    /// Gửi thông báo real-time tới user (author). Client đăng ký hub sẽ nhận event NewNotification.
    Task NotifyUserAsync(Guid userId, NotificationDto notification);

    /// Thu hồi phiên user theo thời gian thực (vd. admin vừa ban tài khoản đang online).
    Task RevokeUserSessionAsync(Guid userId, string message);
}
