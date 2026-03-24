import * as signalR from '@microsoft/signalr';

const API_URL = import.meta.env.VITE_API_URL || 'https://localhost:7117/api';

/** Base URL không có /api (ví dụ https://localhost:7117). */
function getHubBaseUrl() {
    const base = API_URL.replace(/\/api\/?$/i, '');
    return base || API_URL;
}

/** Tên event backend gửi khi có thông báo mới (NotificationHub.NewNotification). */
export const NEW_NOTIFICATION = 'NewNotification';

/**
 * Tạo và khởi động kết nối SignalR tới hub notification (cho tác giả nhận real-time khi moderator duyệt/từ chối).
 * JWT đưa vào query vì WebSocket không gửi được header Authorization.
 * User đăng nhập (AUTHOR, USER, ADMIN) đều có thể kết nối; backend gửi NewNotification tới đúng user_id.
 *
 * @param {(payload: { id: string, type: string, title: string, content: string, linkUrl?: string, isRead: boolean, createdAt?: string }) => void} onNewNotification - Gọi khi nhận thông báo mới (vd: moderator vừa duyệt/từ chối truyện hoặc chương của tác giả).
 * @returns {{ connection: signalR.HubConnection, startPromise: Promise<void>, stop: () => Promise<void> }}
 */
export function createNotificationHubConnection(onNewNotification) {
    const token = typeof localStorage !== 'undefined' ? localStorage.getItem('accessToken') : null;
    const hubBase = getHubBaseUrl();
    const url = token
        ? `${hubBase}/hubs/notifications?access_token=${encodeURIComponent(token)}`
        : `${hubBase}/hubs/notifications`;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(url)
        .withAutomaticReconnect()
        .build();

    connection.on(NEW_NOTIFICATION, (payload) => {
        if (payload && typeof onNewNotification === 'function') {
            const n = payload;
            onNewNotification({
                id: n.id ?? n.Id,
                type: n.type ?? n.Type,
                title: n.title ?? n.Title,
                content: n.content ?? n.Content,
                linkUrl: n.linkUrl ?? n.LinkUrl,
                isRead: n.isRead ?? n.IsRead ?? false,
                createdAt: n.createdAt ?? n.CreatedAt,
            });
        }
    });

    const startPromise = connection
        .start()
        .then(() => {
            if (typeof console !== 'undefined' && console.debug) {
                console.debug('[NotificationHub] Connected');
            }
        })
        .catch((err) => {
            console.warn('[NotificationHub] Connection failed:', err?.message || err);
        });

    return {
        connection,
        startPromise,
        async stop() {
            try {
                connection.off(NEW_NOTIFICATION);
                // Tránh stop() trong lúc negotiation → lỗi "The connection was stopped during negotiation".
                // Đợi start() xong (thành công hoặc thất bại) rồi mới stop.
                await startPromise.catch(() => {});
                await connection.stop();
            } catch {
                // ignore when already stopped
            }
        },
    };
}
