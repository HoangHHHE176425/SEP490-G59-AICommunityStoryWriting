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
 * Lịch sử phiên bản chương bị từ chối (version). Moderator chỉ thấy bản do mình từ chối; ADMIN thấy tất cả.
 * @returns {Promise<Array<{ id, chapterId?, storyId?, storyTitle?, chapterTitle?, versionNumber, titleSnapshot?, status?, rejectionReason?, rejectedAt? }>>}
 */
export async function getRejectedChapterVersionsHistory() {
    const res = await axiosInstance.get('/moderator/chapter-versions/rejected-history');
    return Array.isArray(res.data) ? res.data : [];
}

/**
 * Moderator xem chi tiết một version (nội dung snapshot).
 * @param {string} chapterId - Guid chapter
 * @param {string} versionId - Guid version
 */
export async function getModeratorChapterVersion(chapterId, versionId) {
    const res = await axiosInstance.get(`/moderator/chapters/${chapterId}/versions/${versionId}`);
    return res.data;
}

/**
 * Moderator nhận duyệt truyện (claim). Body bắt buộc: reviewDeadlineAt (UTC ISO).
 */
export async function claimStory(storyId, reviewDeadlineAt) {
    await axiosInstance.post(`/moderator/stories/${storyId}/claim`, { reviewDeadlineAt });
}

/**
 * Moderator nhận duyệt chương (claim). Body bắt buộc: reviewDeadlineAt (UTC ISO).
 */
export async function claimChapter(chapterId, reviewDeadlineAt) {
    await axiosInstance.post(`/moderator/chapters/${chapterId}/claim`, { reviewDeadlineAt });
}

/**
 * Thông tin assignment hiện tại của moderator cho mục (deadline, đơn escalation, SLA).
 * @param {'STORY'|'CHAPTER'} targetType
 * @param {string} targetId - Guid
 */
export async function getReviewAssignmentSelf(targetType, targetId) {
    const q = new URLSearchParams({ targetType, targetId: String(targetId) });
    const res = await axiosInstance.get(`/moderator/review-assignment/self?${q}`);
    return res.data;
}

/**
 * Gửi đơn báo cáo lên admin (gia hạn / hủy nhận duyệt).
 * @param {{ targetType: string, targetId: string, requestKind: string, reason: string, proposedDeadlineAt?: string|null }} dto
 */
export async function submitReviewEscalation(dto) {
    const res = await axiosInstance.post('/moderator/review-escalations', dto);
    return res.data;
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

/**
 * Nội dung chapter cho màn duyệt: bản gốc đã xuất bản + bản version chờ duyệt (khi chapter đã PUBLISHED và có version gửi chỉnh sửa).
 * Dùng để moderator xem 2 phiên bản (chỉnh sửa sau báo cáo vi phạm).
 * @param {string} chapterId - Guid chapter
 * @returns {Promise<{ chapterId, chapterStatus, originalTitle, originalContent, hasPendingVersion, pendingVersions }>}
 */
export async function getChapterReviewContent(chapterId) {
    const res = await axiosInstance.get(`/moderator/chapters/${chapterId}/review-content`);
    return res.data;
}
