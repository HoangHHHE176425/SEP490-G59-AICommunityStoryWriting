import axiosInstance from '../axiosInstance';

/**
 * Lấy danh sách thông báo của user đăng nhập.
 * @param {{ limit?: number, onlyUnread?: boolean }} params
 * @returns {Promise<Array<{ id: string, type: string, title: string, content: string, linkUrl: string, isRead: boolean, createdAt: string }>>}
 */
export async function getNotifications(params = {}) {
    const { limit = 50, onlyUnread = false } = params;
    const response = await axiosInstance.get('/notifications', { params: { limit, onlyUnread } });
    const data = response.data;
    if (!Array.isArray(data)) return [];
    return data.map((n) => ({
        id: n.id ?? n.Id,
        type: n.type ?? n.Type,
        title: n.title ?? n.Title,
        content: n.content ?? n.Content,
        linkUrl: n.linkUrl ?? n.LinkUrl,
        isRead: n.isRead ?? n.IsRead ?? false,
        createdAt: n.createdAt ?? n.CreatedAt,
    }));
}

/**
 * Số thông báo chưa đọc.
 * @returns {Promise<{ count: number }>}
 */
export async function getUnreadCount() {
    const response = await axiosInstance.get('/notifications/unread-count');
    const data = response.data;
    return { count: data?.count ?? data?.Count ?? 0 };
}

/**
 * Đánh dấu một thông báo đã đọc.
 * @param {string} id - Guid thông báo
 */
export async function markNotificationAsRead(id) {
    await axiosInstance.patch(`/notifications/${id}/read`);
}

/**
 * Đánh dấu tất cả thông báo đã đọc.
 */
export async function markAllNotificationsAsRead() {
    await axiosInstance.post('/notifications/mark-all-read');
}
