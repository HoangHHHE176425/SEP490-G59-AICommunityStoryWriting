import axiosInstance from '../axiosInstance';

/**
 * Kiểm tra user hiện tại có đang follow author không.
 * GET api/authors/{authorId}/following
 * @param {string} authorId - Guid
 * @returns {Promise<{ following: boolean }>}
 */
export async function getAuthorFollowing(authorId) {
    const response = await axiosInstance.get(`/authors/${authorId}/following`);
    return response.data;
}

/**
 * Theo dõi tác giả.
 * POST api/authors/{authorId}/follow
 * @param {string} authorId - Guid
 * @returns {Promise<{ following: true, message: string }>}
 */
export async function followAuthor(authorId) {
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
    const response = await axiosInstance.delete(`/authors/${authorId}/follow`);
    return response.data;
}
