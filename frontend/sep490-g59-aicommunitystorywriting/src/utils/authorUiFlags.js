/**
 * Cờ tạm (window) để Header biết author đang ở màn "Quản lý danh sách chương"
 * — tránh toast realtime trùng khi moderator duyệt chương (list đã tự cập nhật).
 */
export function setAuthorChapterListActive(value) {
    if (typeof window === 'undefined') return;
    window.__authorChapterListActive = !!value;
}

export function isAuthorChapterListActive() {
    return typeof window !== 'undefined' && window.__authorChapterListActive === true;
}
