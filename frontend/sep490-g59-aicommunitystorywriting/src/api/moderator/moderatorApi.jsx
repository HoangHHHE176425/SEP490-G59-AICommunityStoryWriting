import axiosInstance from '../axiosInstance';

/**
 * Danh sách truyện chờ duyệt (PENDING_REVIEW).
 * Moderator chỉ thấy truyện thuộc thể loại được gán; ADMIN thấy tất cả.
 * claimFilter: all (mặc định) | UNCLAIMED | CLAIMED.
 * @param {Object} params - { page, pageSize, search, sortBy, sortOrder, claimFilter }
 */
export async function getPendingStories(params = {}) {
    const q = new URLSearchParams();
    if (params.page != null) q.append('page', params.page);
    if (params.pageSize != null) q.append('pageSize', params.pageSize);
    if (params.search) q.append('search', params.search);
    q.append('sortBy', params.sortBy ?? 'updated_at');
    q.append('sortOrder', params.sortOrder ?? 'asc');
    q.append('claimFilter', params.claimFilter ?? 'all');
    const url = `/moderator/stories/pending?${q}`;
    const res = await axiosInstance.get(url);
    return res.data;
}

/**
 * Danh sách chương chờ duyệt (PENDING_REVIEW).
 * Moderator chỉ thấy chương thuộc truyện có thể loại được gán; ADMIN thấy tất cả.
 * claimFilter: all | UNCLAIMED | CLAIMED.
 * @param {Object} params - { page, pageSize, storyId, search, sortBy, sortOrder, claimFilter }
 */
export async function getPendingChapters(params = {}) {
    const q = new URLSearchParams();
    if (params.page != null) q.append('page', params.page);
    if (params.pageSize != null) q.append('pageSize', params.pageSize);
    if (params.storyId) q.append('storyId', params.storyId);
    if (params.search) q.append('search', params.search);
    q.append('sortBy', params.sortBy ?? 'created_at');
    q.append('sortOrder', params.sortOrder ?? 'asc');
    q.append('claimFilter', params.claimFilter ?? 'all');
    const url = `/moderator/chapters/pending?${q}`;
    const res = await axiosInstance.get(url);
    return res.data;
}

/**
 * Lịch sử chương đã duyệt/từ chối (PUBLISHED | REJECTED), lọc theo category moderator. Dùng cho tab Từ chối (chương bị từ chối).
 * @param {Object} params - { status: 'PUBLISHED' | 'REJECTED', page, pageSize, search?, sortBy?, sortOrder? }
 */
export async function getModeratorReviewedChapters(params = {}) {
    const q = new URLSearchParams();
    if (params.status) q.append('status', params.status);
    if (params.page != null) q.append('page', params.page);
    if (params.pageSize != null) q.append('pageSize', params.pageSize);
    if (params.search) q.append('search', params.search);
    if (params.sortBy) q.append('sortBy', params.sortBy);
    if (params.sortOrder) q.append('sortOrder', params.sortOrder);
    const url = `/moderator/chapters/reviewed?${q}`;
    const res = await axiosInstance.get(url);
    return res.data;
}

/**
 * Lịch sử đã duyệt / từ chối cho moderator: truyện theo status (PUBLISHED | REJECTED), lọc theo category moderator được gán.
 * @param {Object} params - { status: 'PUBLISHED' | 'REJECTED', page, pageSize, search?, sortBy?, sortOrder? }
 */
export async function getModeratorReviewedStories(params = {}) {
    const q = new URLSearchParams();
    if (params.status) q.append('status', params.status);
    if (params.page != null) q.append('page', params.page);
    if (params.pageSize != null) q.append('pageSize', params.pageSize);
    if (params.search) q.append('search', params.search);
    if (params.sortBy) q.append('sortBy', params.sortBy);
    if (params.sortOrder) q.append('sortOrder', params.sortOrder);
    const url = `/moderator/stories/reviewed?${q}`;
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
