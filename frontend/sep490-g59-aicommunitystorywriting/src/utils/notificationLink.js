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

    // Legacy: /Home/Story?id={guid}
    if (pathname.toLowerCase() === '/home/story' && query) {
        const id = new URLSearchParams(query).get('id');
        if (id && /^[0-9a-fA-F-]{36}$/.test(id)) {
            return `/story/${id}${hash}`;
        }
    }

    return `${pathAndQuery}${hash}`;
}
