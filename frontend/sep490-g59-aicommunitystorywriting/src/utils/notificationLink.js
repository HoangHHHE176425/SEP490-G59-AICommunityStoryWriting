const GUID_RE = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/i;

const VIOLATION_OR_REPORT_TYPES_OPEN_MODAL_ON_CLICK = new Set([
    'STORY_REPORTED_TO_AUTHOR',
    'COMMENT_REPORTED_TO_OWNER',
    'COMPLIANCE_STORY_MODERATION_ACTION',
    'COMPLIANCE_COMMENT_MODERATION_ACTION',
    'COMPLIANCE_AUTHOR_WRITING_MODERATION',
    'COMPLIANCE_STORY_REPORT_BULK_RESOLVED',
    'COMPLIANCE_COMMENT_REPORT_BULK_RESOLVED',
    'COMPLIANCE_ADMIN_ACTION_APPROVED',
]);

/**
 * Chuẩn hóa link thông báo từ BE (đường dẫn Razor cũ) sang route React Router.
 * @param {string|null|undefined} linkUrl
 * @returns {string} path + hash dùng cho <Link to="...">
 */
export function normalizeNotificationTo(linkUrl) {
    if (!linkUrl || typeof linkUrl !== 'string') return '/home';
    const trimmed = linkUrl.trim();
    if (!trimmed.startsWith('/')) return '/home';

    const hashIdx = trimmed.indexOf('#');
    const hash = hashIdx >= 0 ? trimmed.slice(hashIdx) : '';
    const pathAndQuery = hashIdx >= 0 ? trimmed.slice(0, hashIdx) : trimmed;

    const qIdx = pathAndQuery.indexOf('?');
    const pathname = (qIdx >= 0 ? pathAndQuery.slice(0, qIdx) : pathAndQuery).replace(/\/$/, '');
    const query = qIdx >= 0 ? pathAndQuery.slice(qIdx + 1) : '';

    if (pathname.toLowerCase() === '/home/story' && query) {
        const id = new URLSearchParams(query).get('id');
        if (id && GUID_RE.test(id)) {
            return `/story/${id}${hash}`;
        }
    }

    const storiesDetails = pathname.match(/^\/Stories\/Details\/([0-9a-fA-F-]{36})$/i);
    if (storiesDetails) {
        return `/story/${storiesDetails[1]}${hash}`;
    }

    if (pathname.replace(/\/$/, '').toLowerCase() === '/chapters/index' && query) {
        const sid = new URLSearchParams(query).get('storyId');
        if (sid && GUID_RE.test(sid)) {
            return `/story/${sid}${hash}`;
        }
    }

    const chaptersRead = pathname.match(/^\/Chapters\/Read\/([0-9a-fA-F-]{36})$/i);
    if (chaptersRead) {
        return `/chapter?chapterId=${encodeURIComponent(chaptersRead[1])}${hash}`;
    }

    return `${pathAndQuery}${hash}`;
}

/**
 * Đích điều hướng cuối (kèm override cho thông báo báo cáo → màn tác giả).
 * @param {object} notification
 * @returns {string}
 */
export function resolveNotificationTarget(notification) {
    const linkUrl = notification?.linkUrl ?? notification?.LinkUrl;
    let target = normalizeNotificationTo(linkUrl);
    const typeUpper = String(notification?.type ?? notification?.Type ?? '').toUpperCase();

    if (typeUpper === 'STORY_REPORTED_TO_AUTHOR' || typeUpper === 'COMMENT_REPORTED_TO_OWNER') {
        const raw = String(linkUrl ?? '');
        const storyMatch = raw.match(/\/story\/([0-9a-fA-F-]{36})/i);
        const legacyMatch = raw.match(/\/Stories\/Details\/([0-9a-fA-F-]{36})/i);
        const storyId = storyMatch?.[1] ?? legacyMatch?.[1];
        target = storyId ? `/author?view=reports&storyId=${encodeURIComponent(storyId)}` : '/author?view=reports';
    }
    return target;
}

/**
 * Ẩn nút "Mở trang liên quan" (vd. ủng hộ — chỉ xem nội dung trong popup).
 */
export function shouldShowOpenRelatedPageButton(notification) {
    const typeUpper = String(notification?.type ?? notification?.Type ?? '').toUpperCase();
    if (typeUpper === 'DONATION') return false;
    const rawLink = String(notification?.linkUrl ?? notification?.LinkUrl ?? '').trim();
    const target = resolveNotificationTarget(notification);
    if (!rawLink && target === '/home') return false;
    return true;
}

/**
 * Bấm một dòng thông báo: điều hướng ngay (duyệt truyện/chương, chương mới, mở khóa...) thay vì mở popup.
 * Thông báo vi phạm / báo cáo: vẫn mở popup để đọc chi tiết.
 */
export function shouldNavigateNotificationOnRowClick(notification) {
    if (!shouldShowOpenRelatedPageButton(notification)) return false;
    const t = String(notification?.type ?? notification?.Type ?? '').toUpperCase();
    if (t.startsWith('COMPLIANCE_') || VIOLATION_OR_REPORT_TYPES_OPEN_MODAL_ON_CLICK.has(t)) {
        return false;
    }
    return true;
}
