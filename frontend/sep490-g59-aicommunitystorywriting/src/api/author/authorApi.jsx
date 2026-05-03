import axiosInstance from '../axiosInstance';

/** BE dùng `{authorId:guid}` — id như "1","2" không match route → 404. */
export function isAuthorGuid(authorId) {
    if (authorId == null || authorId === '') return false;
    const s = String(authorId).trim();
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(s);
}

/**
 * Kiểm tra user hiện tại có đang follow author không.
 * GET api/authors/{authorId}/following
 * @param {string} authorId - Guid
 * @returns {Promise<{ following: boolean }>}
 */
export async function getAuthorFollowing(authorId) {
    if (!isAuthorGuid(authorId)) {
        return { following: false };
    }
    const response = await axiosInstance.get(`/authors/${authorId}/following`);
    return response.data;
}

/**
 * Số người đang theo dõi tác giả (public, không cần đăng nhập).
 * GET api/authors/{authorId}/followers-count
 * @param {string} authorId - Guid
 * @returns {Promise<number>}
 */
export async function getAuthorFollowersCount(authorId) {
    if (!isAuthorGuid(authorId)) {
        return 0;
    }
    const response = await axiosInstance.get(`/authors/${authorId}/followers-count`);
    const d = response.data ?? {};
    const n = d.followersCount ?? d.FollowersCount;
    const num = Number(n);
    return Number.isFinite(num) ? num : 0;
}

/**
 * Số follower mới từ đầu tuần (Thứ Hai 00:00, giờ máy chủ).
 * GET api/authors/{authorId}/followers-this-week
 * @param {string} authorId - Guid
 * @returns {Promise<number>}
 */
export async function getAuthorNewFollowersThisWeek(authorId) {
    if (!isAuthorGuid(authorId)) {
        return 0;
    }
    const response = await axiosInstance.get(`/authors/${authorId}/followers-this-week`);
    const d = response.data ?? {};
    const n = d.newFollowersThisWeek ?? d.NewFollowersThisWeek;
    const num = Number(n);
    return Number.isFinite(num) ? Math.max(0, Math.floor(num)) : 0;
}

/**
 * Theo dõi tác giả.
 * POST api/authors/{authorId}/follow
 * @param {string} authorId - Guid
 * @returns {Promise<{ following: true, message: string }>}
 */
export async function followAuthor(authorId) {
    if (!isAuthorGuid(authorId)) {
        return Promise.reject(new Error('Mã tác giả không hợp lệ.'));
    }
    const response = await axiosInstance.post(`/authors/${authorId}/follow`);
    return response.data;
}

/**
 * Bỏ theo dõi tác giả.
 * DELETE api/authors/{authorId}/follow
 * @param {string} authorId - Guid
 * @returns {Promise<{ following: false, message: string }>}
 */
export async function unfollowAuthor(authorId) {
    if (!isAuthorGuid(authorId)) {
        return Promise.reject(new Error('Mã tác giả không hợp lệ.'));
    }
    const response = await axiosInstance.delete(`/authors/${authorId}/follow`);
    return response.data;
}

/**
 * Danh sách followers của tác giả (phân trang).
 * GET api/authors/{authorId}/followers
 * @param {string} authorId - Guid
 * @param {{ page?: number, pageSize?: number, search?: string }} options
 */
export async function getAuthorFollowers(authorId, options = {}) {
    if (!isAuthorGuid(authorId)) {
        return { items: [], totalCount: 0, page: 1, pageSize: 20 };
    }
    const page = Number(options?.page) > 0 ? Number(options.page) : 1;
    const pageSize = Number(options?.pageSize) > 0 ? Number(options.pageSize) : 20;
    const search = typeof options?.search === 'string' ? options.search.trim() : '';
    const response = await axiosInstance.get(`/authors/${authorId}/followers`, {
        params: {
            page,
            pageSize,
            ...(search ? { search } : {}),
        },
    });
    return response.data ?? { items: [], totalCount: 0, page, pageSize };
}
