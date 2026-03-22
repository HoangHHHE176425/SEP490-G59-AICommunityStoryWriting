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
