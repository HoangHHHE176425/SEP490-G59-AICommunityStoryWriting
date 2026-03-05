import axiosInstance from '../axiosInstance';

/**
 * Danh sách truyện chờ duyệt (PENDING_REVIEW).
 * Moderator chỉ thấy truyện thuộc thể loại được gán (moderator_category_assignments); ADMIN thấy tất cả.
 * @param {Object} params - { page, pageSize, search, sortBy, sortOrder, claimFilter: 'all'|'UNCLAIMED'|'CLAIMED' }
 */
export async function getPendingStories(params = {}) {
    const q = new URLSearchParams();
    if (params.page != null) q.append('page', params.page);
    if (params.pageSize != null) q.append('pageSize', params.pageSize);
    if (params.search) q.append('search', params.search);
    if (params.sortBy) q.append('sortBy', params.sortBy);
    if (params.sortOrder) q.append('sortOrder', params.sortOrder);
    if (params.claimFilter) q.append('claimFilter', params.claimFilter);
    const url = q.toString() ? `/moderator/stories/pending?${q}` : '/moderator/stories/pending';
    const res = await axiosInstance.get(url);
    return res.data;
}

/**
 * Danh sách chương chờ duyệt (PENDING_REVIEW).
 * Moderator chỉ thấy chương thuộc truyện có thể loại được gán; ADMIN thấy tất cả.
 * @param {Object} params - { page, pageSize, storyId, search, sortBy, sortOrder, claimFilter }
 */
export async function getPendingChapters(params = {}) {
    const q = new URLSearchParams();
    if (params.page != null) q.append('page', params.page);
    if (params.pageSize != null) q.append('pageSize', params.pageSize);
    if (params.storyId) q.append('storyId', params.storyId);
    if (params.search) q.append('search', params.search);
    if (params.sortBy) q.append('sortBy', params.sortBy);
    if (params.sortOrder) q.append('sortOrder', params.sortOrder);
    if (params.claimFilter) q.append('claimFilter', params.claimFilter);
    const url = q.toString() ? `/moderator/chapters/pending?${q}` : '/moderator/chapters/pending';
    const res = await axiosInstance.get(url);
    return res.data;
}

/**
 * Moderator nhận duyệt truyện (claim). 1 item chỉ 1 moderator; đã nhận thì người khác không thấy trong queue unclaimed.
 */
export async function claimStory(storyId) {
    await axiosInstance.post(`/moderator/stories/${storyId}/claim`);
}

/**
 * Moderator nhận duyệt chương (claim).
 */
export async function claimChapter(chapterId) {
    await axiosInstance.post(`/moderator/chapters/${chapterId}/claim`);
}

/**
 * Duyệt truyện (approve) → status PUBLISHED.
 */
export async function approveStory(storyId) {
    await axiosInstance.post(`/moderator/stories/${storyId}/approve`);
}

/**
 * Từ chối truyện (bắt buộc lý do).
 */
export async function rejectStory(storyId, reason) {
    await axiosInstance.post(`/moderator/stories/${storyId}/reject`, { reason });
}

/**
 * Duyệt chương (approve) → status PUBLISHED.
 */
export async function approveChapter(chapterId) {
    await axiosInstance.post(`/moderator/chapters/${chapterId}/approve`);
}

/**
 * Từ chối chương (bắt buộc lý do).
 */
export async function rejectChapter(chapterId, reason) {
    await axiosInstance.post(`/moderator/chapters/${chapterId}/reject`, { reason });
}
