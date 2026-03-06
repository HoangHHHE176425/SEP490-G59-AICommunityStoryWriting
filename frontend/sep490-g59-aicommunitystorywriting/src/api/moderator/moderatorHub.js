import * as signalR from '@microsoft/signalr';

const API_URL = import.meta.env.VITE_API_URL || 'https://localhost:7117/api';

/** Base URL không có /api (ví dụ https://localhost:7117). */
function getHubBaseUrl() {
    const base = API_URL.replace(/\/api\/?$/i, '');
    return base || API_URL;
}

/** Tên event backend gửi khi danh sách chờ duyệt thay đổi (ModeratorHub.PendingListChanged). */
export const PENDING_LIST_CHANGED = 'PendingListChanged';

/**
 * Tạo và khởi động kết nối SignalR tới hub moderator.
 * JWT đưa vào query vì WebSocket không gửi được header Authorization.
 * Chỉ MODERATOR/ADMIN mới được kết nối (backend [Authorize(Roles = "MODERATOR,ADMIN")]).
 *
 * @param {() => void} onPendingListChanged - Gọi khi nhận PendingListChanged (client nên refetch GET stories/pending & chapters/pending).
 * @returns {{ connection: signalR.HubConnection, stop: () => Promise<void> }}
 */
export function createModeratorHubConnection(onPendingListChanged) {
    const token = typeof localStorage !== 'undefined' ? localStorage.getItem('accessToken') : null;
    const hubBase = getHubBaseUrl();
    const url = token
        ? `${hubBase}/hubs/moderator?access_token=${encodeURIComponent(token)}`
        : `${hubBase}/hubs/moderator`;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl(url)
        .withAutomaticReconnect()
        .build();

    connection.on(PENDING_LIST_CHANGED, () => {
        onPendingListChanged?.();
    });

    const startPromise = connection.start().catch((err) => {
        console.warn('[ModeratorHub] Connection failed:', err?.message || err);
    });

    return {
        connection,
        startPromise,
        async stop() {
            try {
                connection.off(PENDING_LIST_CHANGED);
                await connection.stop();
            } catch {
                // ignore when already stopped
            }
        },
    };
}
