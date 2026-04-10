// API Base URL: từ server config (Layout) hoặc mặc định khi chạy API trên port 5000
const API_BASE_URL = (typeof window !== 'undefined' && window.__API_BASE_URL) ? window.__API_BASE_URL : 'http://localhost:5000/api';

/**
 * URL ảnh/media từ API: nếu đã là http(s):// (vd. Cloudinary) thì dùng nguyên;
 * nếu là đường dẫn tương đối (/uploads/...) thì ghép gốc backend (API_BASE_URL bỏ /api).
 */
function resolveMediaSrc(pathOrUrl) {
    if (pathOrUrl == null || pathOrUrl === '') return '';
    const s = String(pathOrUrl).trim();
    if (/^https?:\/\//i.test(s)) return s;
    const base = String(typeof API_BASE_URL !== 'undefined' ? API_BASE_URL : 'http://localhost:5000/api').replace(/\/api\/?$/i, '');
    const root = base || 'http://localhost:5000';
    return s.startsWith('/') ? root + s : root + '/' + s;
}
if (typeof window !== 'undefined') window.resolveMediaSrc = resolveMediaSrc;

// API Service Class
class ApiService {
    /**
     * Gửi request tới API. Khi gặp 401 Unauthorized, thử refresh token rồi gửi lại request một lần.
     * @param {string} url - Đường dẫn API (vd: /chapters/xxx)
     * @param {RequestInit} options - fetch options
     * @param {boolean} skipRetry - Không thử refresh khi 401 (dùng cho request refresh tránh lặp vô hạn)
     */
    static async request(url, options = {}, skipRetry = false) {
        try {
            // Tự động thêm Authorization header nếu có token
            const headers = {
                'Content-Type': 'application/json',
                ...options.headers
            };

            // Lấy token từ auth helper nếu có
            if (typeof AuthHelper !== 'undefined' && AuthHelper.getToken()) {
                headers['Authorization'] = `Bearer ${AuthHelper.getToken()}`;
            }

            const response = await fetch(`${API_BASE_URL}${url}`, {
                headers: headers,
                credentials: 'include', // Gửi cookie (refresh token) khi gọi API
                ...options
            });

            // Handle NoContent responses (204) first - these are successful
            if (response.status === 204) {
                return null;
            }

            if (!response.ok) {
                // 401: thử refresh token rồi gửi lại (trừ khi đang gọi refresh hoặc đã skipRetry)
                if (response.status === 401 && !skipRetry && url.indexOf('/auth/refresh') === -1 &&
                    typeof AuthHelper !== 'undefined' && AuthHelper.getToken()) {
                    const refreshed = await ApiService._tryRefreshToken();
                    if (refreshed) {
                        return ApiService.request(url, options, true);
                    }
                    // Refresh thất bại: xóa token và báo đăng nhập lại
                    AuthHelper.removeToken();
                    throw new Error('Phiên đăng nhập hết hạn. Vui lòng đăng nhập lại.');
                }

                let errorMessage = response.statusText;
                let errorBody = null;
                try {
                    errorBody = await response.json();
                    // Prefer detailed error message if backend provides `error`.
                    errorMessage = errorBody.error || errorBody.message || errorMessage;
                } catch {
                    // If response body is not JSON, use statusText
                }
                const err = new Error(errorMessage || `HTTP error! status: ${response.status}`);
                err.status = response.status;
                err.body = errorBody;
                throw err;
            }

            // Try to parse JSON, but handle empty responses gracefully
            const contentType = response.headers.get('content-type');
            if (contentType && contentType.includes('application/json')) {
                const text = await response.text();
                return text ? JSON.parse(text) : null;
            }

            return null;
        } catch (error) {
            console.error('API Request Error:', error);
            // Re-throw with a more user-friendly message if it's a network error
            if (error instanceof TypeError && error.message.includes('fetch')) {
                throw new Error('Không thể kết nối đến server. Vui lòng kiểm tra kết nối mạng.');
            }
            throw error;
        }
    }

    /** Gọi API refresh token (dùng cookie), cập nhật token mới vào AuthHelper. Trả về true nếu thành công. */
    static async _tryRefreshToken() {
        try {
            const res = await fetch(`${API_BASE_URL}/auth/refresh`, {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                credentials: 'include'
            });
            if (!res.ok) return false;
            const data = await res.json().catch(() => null);
            const token = data?.accessToken ?? data?.AccessToken;
            if (token && typeof AuthHelper !== 'undefined') {
                AuthHelper.setToken(token);
                return true;
            }
            return false;
        } catch {
            return false;
        }
    }

    // Categories API
    static async getCategories(includeInactive = false, parentId = null, rootsOnly = false) {
        const params = new URLSearchParams();
        params.append('includeInactive', includeInactive);
        if (rootsOnly) params.append('rootsOnly', 'true');
        if (parentId != null && parentId !== '') params.append('parentId', parentId);
        return this.request(`/categories?${params.toString()}`);
    }

    static async getCategoriesWithPagination(query = {}) {
        const params = new URLSearchParams();
        Object.keys(query).forEach(key => {
            if (query[key] !== null && query[key] !== undefined && query[key] !== '') {
                params.append(key, query[key]);
            }
        });
        const queryString = params.toString();
        return this.request(`/categories${queryString ? '?' + queryString : ''}`);
    }

    static async getCategoryById(id) {
        return this.request(`/categories/${id}`);
    }

    static async getCategoryBySlug(slug) {
        return this.request(`/categories/slug/${slug}`);
    }

    static async createCategory(formData) {
        const headers = {};
        // Thêm Authorization header nếu có token
        if (typeof AuthHelper !== 'undefined' && AuthHelper.getToken()) {
            headers['Authorization'] = `Bearer ${AuthHelper.getToken()}`;
        }

        return fetch(`${API_BASE_URL}/categories`, {
            method: 'POST',
            headers: headers,
            body: formData
        }).then(async (response) => {
            if (!response.ok) {
                const error = await response.json().catch(() => ({ message: response.statusText }));
                throw new Error(error.message || `HTTP error! status: ${response.status}`);
            }
            return await response.json();
        });
    }

    static async updateCategory(id, formData) {
        const headers = {};
        // Thêm Authorization header nếu có token
        if (typeof AuthHelper !== 'undefined' && AuthHelper.getToken()) {
            headers['Authorization'] = `Bearer ${AuthHelper.getToken()}`;
        }

        return fetch(`${API_BASE_URL}/categories/${id}`, {
            method: 'PUT',
            headers: headers,
            body: formData
        }).then(async (response) => {
            if (!response.ok) {
                const error = await response.json().catch(() => ({ message: response.statusText }));
                throw new Error(error.message || `HTTP error! status: ${response.status}`);
            }
            return response.status === 204 ? null : await response.json();
        });
    }

    static async deleteCategory(id) {
        return this.request(`/categories/${id}`, {
            method: 'DELETE'
        });
    }

    static async toggleCategoryActive(id) {
        return this.request(`/categories/${id}/toggle-active`, {
            method: 'PATCH'
        });
    }

    /**
     * Thống kê trang chủ (không cần đăng nhập): publishedStoriesCount, authorsCount, totalViews.
     * GET /api/community/stats
     */
    static async getCommunityHomeStats() {
        return this.request('/community/stats');
    }

    // Stories API
    static async getStories(query = {}) {
        const params = new URLSearchParams();
        Object.keys(query).forEach(key => {
            if (query[key] !== null && query[key] !== undefined && query[key] !== '') {
                params.append(key, query[key]);
            }
        });
        const queryString = params.toString();
        return this.request(`/stories${queryString ? '?' + queryString : ''}`);
    }

    static async getStoryById(id) {
        return this.request(`/stories/${id}`);
    }

    static async getStoryBySlug(slug) {
        return this.request(`/stories/slug/${slug}`);
    }

    static async getStoriesByAuthor(authorId, query = {}) {
        const params = new URLSearchParams();
        Object.keys(query).forEach(key => {
            if (query[key] !== null && query[key] !== undefined && query[key] !== '') {
                params.append(key, query[key]);
            }
        });
        const queryString = params.toString();
        return this.request(`/stories/author/${authorId}${queryString ? '?' + queryString : ''}`);
    }

    static async createStory(formData) {
        const headers = {};
        // Thêm Authorization header nếu có token
        if (typeof AuthHelper !== 'undefined' && AuthHelper.getToken()) {
            headers['Authorization'] = `Bearer ${AuthHelper.getToken()}`;
        }

        return fetch(`${API_BASE_URL}/stories`, {
            method: 'POST',
            headers: headers,
            body: formData
        }).then(async (response) => {
            if (!response.ok) {
                const errBody = await response.json().catch(() => ({ message: response.statusText }));
                const msg = errBody.error || errBody.message || `HTTP error! status: ${response.status}`;
                throw new Error(msg);
            }
            return await response.json();
        });
    }

    static async updateStory(id, formData) {
        const headers = {};
        // Thêm Authorization header nếu có token
        if (typeof AuthHelper !== 'undefined' && AuthHelper.getToken()) {
            headers['Authorization'] = `Bearer ${AuthHelper.getToken()}`;
        }

        return fetch(`${API_BASE_URL}/stories/${id}`, {
            method: 'PUT',
            headers: headers,
            body: formData
        }).then(async (response) => {
            if (!response.ok) {
                const errBody = await response.json().catch(() => ({ message: response.statusText }));
                const msg = errBody.error || errBody.message || `HTTP error! status: ${response.status}`;
                throw new Error(msg);
            }
            return response.status === 204 ? null : await response.json();
        });
    }

    static async deleteStory(id) {
        return this.request(`/stories/${id}`, {
            method: 'DELETE'
        });
    }

    static async publishStory(id) {
        return this.request(`/stories/${id}/publish`, {
            method: 'POST'
        });
    }

    static async unpublishStory(id) {
        return this.request(`/stories/${id}/unpublish`, {
            method: 'POST'
        });
    }

    static async getStoryRejectionReason(id) {
        return this.request(`/stories/${id}/rejection-reason`);
    }

    static async rateStory(storyId, data) {
        return this.request(`/stories/${storyId}/ratings`, {
            method: 'POST',
            body: JSON.stringify(data)
        });
    }

    static async followStory(storyId) {
        return this.request(`/stories/${storyId}/follow`, { method: 'POST' });
    }

    static async unfollowStory(storyId) {
        return this.request(`/stories/${storyId}/follow`, { method: 'DELETE' });
    }

    /** Lưu tiến độ đọc: đang đọc đến chapter nào (để hiển thị "Đọc tiếp" trên trang truyện). Cần đăng nhập. */
    static async saveReadingProgress(storyId, chapterId) {
        return this.request(`/stories/${storyId}/reading-progress`, {
            method: 'POST',
            body: JSON.stringify({ chapterId: chapterId })
        });
    }

    // Authors (follow author) API
    static async getAuthorFollowing(authorId) {
        const res = await this.request(`/authors/${authorId}/following`);
        return res && res.following === true;
    }

    static async getAuthorFollowerCount(authorId) {
        const res = await this.request(`/authors/${authorId}/followers-count`);
        return res && typeof res.followersCount !== 'undefined' ? res.followersCount : 0;
    }

    static async followAuthor(authorId) {
        return this.request(`/authors/${authorId}/follow`, { method: 'POST' });
    }

    static async unfollowAuthor(authorId) {
        return this.request(`/authors/${authorId}/follow`, { method: 'DELETE' });
    }

    /** Thư viện của tôi: truyện theo dõi, tác giả theo dõi, lịch sử đọc. Cần đăng nhập. */
    static async getMyLibrary() {
        return this.request('/library');
    }

    static async getStoryComments(storyId) {
        return this.request(`/stories/${storyId}/comments`);
    }

    static async addStoryComment(storyId, data) {
        return this.request(`/stories/${storyId}/comments`, {
            method: 'POST',
            body: JSON.stringify(data)
        });
    }

    static async toggleCommentLike(storyId, commentId) {
        return this.request(`/stories/${storyId}/comments/${commentId}/like`, {
            method: 'POST'
        });
    }

    /** reactionType: 'LIKE'|'DISLIKE'|'FUNNY'|'SAD'|'ANGRY'|'LOVE'|'WOW' hoặc null để bỏ reaction */
    static async setCommentReaction(storyId, commentId, reactionType) {
        return this.request(`/stories/${storyId}/comments/${commentId}/reaction`, {
            method: 'POST',
            body: JSON.stringify({ reactionType: reactionType || null })
        });
    }

    /** Danh sách người đã reaction comment (để hiển thị modal). */
    static async getCommentReactions(storyId, commentId) {
        return this.request(`/stories/${storyId}/comments/${commentId}/reactions`);
    }

    static async getChapterRejectionReason(id) {
        return this.request(`/chapters/${id}/rejection-reason`);
    }

    // Chapters API
    static async getChapters(query = {}) {
        const params = new URLSearchParams();
        Object.keys(query).forEach(key => {
            if (query[key] !== null && query[key] !== undefined && query[key] !== '') {
                params.append(key, query[key]);
            }
        });
        const queryString = params.toString();
        return this.request(`/chapters${queryString ? '?' + queryString : ''}`);
    }

    static async getChapterById(id) {
        return this.request(`/chapters/${id}`);
    }

    static async unlockChapter(chapterId) {
        return this.request(`/chapters/${chapterId}/unlock`, {
            method: 'POST'
        });
    }

    static async getChapterComments(chapterId) {
        return this.request(`/chapters/${chapterId}/comments`);
    }

    static async addChapterComment(chapterId, data) {
        return this.request(`/chapters/${chapterId}/comments`, {
            method: 'POST',
            body: JSON.stringify(data)
        });
    }

    static async getChapterCommentReactions(chapterId, commentId) {
        return this.request(`/chapters/${chapterId}/comments/${commentId}/reactions`);
    }

    static async setChapterCommentReaction(chapterId, commentId, reactionType) {
        return this.request(`/chapters/${chapterId}/comments/${commentId}/reaction`, {
            method: 'POST',
            body: JSON.stringify({ reactionType: reactionType || null })
        });
    }

    static async getChaptersByStoryId(storyId) {
        return this.request(`/chapters/story/${storyId}`);
    }

    static async getChapterByStoryIdAndOrder(storyId, orderIndex) {
        return this.request(`/chapters/story/${storyId}/order/${orderIndex}`);
    }

    static async createChapter(data) {
        return this.request('/chapters', {
            method: 'POST',
            body: JSON.stringify(data)
        });
    }

    static async updateChapter(id, data) {
        return this.request(`/chapters/${id}`, {
            method: 'PUT',
            body: JSON.stringify(data)
        });
    }

    /**
     * Xóa chapter (DRAFT). Lần đầu gọi với deleteIncludingVersions=false.
     * Nếu API trả 409 (code CHAPTER_DELETE_VERSIONS_CONFIRM_REQUIRED), hỏi user rồi gọi lại với true.
     */
    static async deleteChapter(id, deleteIncludingVersions = false) {
        const q = deleteIncludingVersions ? '?deleteIncludingVersions=true' : '';
        return this.request(`/chapters/${id}${q}`, {
            method: 'DELETE'
        });
    }

    // Chapter versions (AUTHOR)
    static async getChapterVersions(chapterId) {
        return this.request(`/chapters/${chapterId}/versions`);
    }

    static async getChapterVersion(chapterId, versionId) {
        return this.request(`/chapters/${chapterId}/versions/${versionId}`);
    }

    static async createChapterVersion(chapterId, data) {
        return this.request(`/chapters/${chapterId}/versions`, {
            method: 'POST',
            body: JSON.stringify(data || {})
        });
    }

    static async updateChapterVersion(chapterId, versionId, data) {
        return this.request(`/chapters/${chapterId}/versions/${versionId}`, {
            method: 'PUT',
            body: JSON.stringify(data || {})
        });
    }

    static async deleteChapterVersion(chapterId, versionId) {
        return this.request(`/chapters/${chapterId}/versions/${versionId}`, {
            method: 'DELETE'
        });
    }

    static async submitChapterVersion(chapterId, versionId) {
        return this.request(`/chapters/${chapterId}/versions/${versionId}/submit`, {
            method: 'POST'
        });
    }

    static async publishChapter(id) {
        return this.request(`/chapters/${id}/publish`, {
            method: 'POST'
        });
    }

    static async unpublishChapter(id) {
        return this.request(`/chapters/${id}/unpublish`, {
            method: 'POST'
        });
    }

    static async reorderChapter(id, newOrderIndex) {
        return this.request(`/chapters/${id}/reorder`, {
            method: 'POST',
            body: JSON.stringify(newOrderIndex)
        });
    }

    // Moderator API (kiểm duyệt - cần role MODERATOR hoặc ADMIN)
    static async getPendingStories(options = {}) {
        const params = new URLSearchParams();
        params.append('page', options.page ?? 1);
        params.append('pageSize', options.pageSize ?? 20);
        if (options.search) params.append('search', options.search);
        if (options.sortBy) params.append('sortBy', options.sortBy);
        if (options.sortOrder) params.append('sortOrder', options.sortOrder);
        if (options.claimFilter) params.append('claimFilter', options.claimFilter);
        if (options.timeStatus) params.append('timeStatus', options.timeStatus);
        return this.request(`/moderator/stories/pending?${params.toString()}`);
    }

    static async getPendingChapters(options = {}) {
        const params = new URLSearchParams();
        params.append('page', options.page ?? 1);
        params.append('pageSize', options.pageSize ?? 20);
        if (options.storyId) params.append('storyId', options.storyId);
        if (options.search) params.append('search', options.search);
        if (options.sortBy) params.append('sortBy', options.sortBy);
        if (options.sortOrder) params.append('sortOrder', options.sortOrder);
        if (options.claimFilter) params.append('claimFilter', options.claimFilter);
        if (options.timeStatus) params.append('timeStatus', options.timeStatus);
        return this.request(`/moderator/chapters/pending?${params.toString()}`);
    }

    static async moderatorClaimStory(id, reviewDeadlineAtIso) {
        return this.request(`/moderator/stories/${id}/claim`, {
            method: 'POST',
            body: JSON.stringify({ reviewDeadlineAt: reviewDeadlineAtIso })
        });
    }

    static async moderatorApproveStory(id) {
        return this.request(`/moderator/stories/${id}/approve`, { method: 'POST' });
    }

    static async moderatorRejectStory(id, reason) {
        return this.request(`/moderator/stories/${id}/reject`, {
            method: 'POST',
            body: JSON.stringify({ reason: reason })
        });
    }

    static async moderatorClaimChapter(id, reviewDeadlineAtIso) {
        return this.request(`/moderator/chapters/${id}/claim`, {
            method: 'POST',
            body: JSON.stringify({ reviewDeadlineAt: reviewDeadlineAtIso })
        });
    }

    static async moderatorApproveChapter(id) {
        return this.request(`/moderator/chapters/${id}/approve`, { method: 'POST' });
    }

    static async moderatorRejectChapter(id, reason) {
        return this.request(`/moderator/chapters/${id}/reject`, {
            method: 'POST',
            body: JSON.stringify({ reason: reason })
        });
    }

    static async moderatorGetChapterVersions(chapterId) {
        return this.request(`/moderator/chapters/${chapterId}/versions`);
    }

    static async moderatorGetChapterVersion(chapterId, versionId) {
        return this.request(`/moderator/chapters/${chapterId}/versions/${versionId}`);
    }

    static async moderatorGetReviewAssignmentSelf(targetType, targetId) {
        const params = new URLSearchParams();
        params.append('targetType', targetType);
        params.append('targetId', targetId);
        return this.request(`/moderator/review-assignment/self?${params.toString()}`);
    }

    static async moderatorSubmitReviewEscalation(body) {
        return this.request('/moderator/review-escalations', {
            method: 'POST',
            body: JSON.stringify(body)
        });
    }

    // Admin Moderation API (chỉ role ADMIN)
    static async adminGetPendingStories(options = {}) {
        const params = new URLSearchParams();
        params.append('page', options.page ?? 1);
        params.append('pageSize', options.pageSize ?? 20);
        if (options.search) params.append('search', options.search);
        if (options.sortBy) params.append('sortBy', options.sortBy);
        if (options.sortOrder) params.append('sortOrder', options.sortOrder);
        if (options.claimFilter) params.append('claimFilter', options.claimFilter);
        return this.request(`/admin/moderation/pending-stories?${params.toString()}`);
    }

    static async adminGetPendingChapters(options = {}) {
        const params = new URLSearchParams();
        params.append('page', options.page ?? 1);
        params.append('pageSize', options.pageSize ?? 20);
        if (options.storyId) params.append('storyId', options.storyId);
        if (options.search) params.append('search', options.search);
        if (options.sortBy) params.append('sortBy', options.sortBy);
        if (options.sortOrder) params.append('sortOrder', options.sortOrder);
        if (options.claimFilter) params.append('claimFilter', options.claimFilter);
        return this.request(`/admin/moderation/pending-chapters?${params.toString()}`);
    }

    static async adminGetPendingReviewEscalations(urgencyTier) {
        const params = new URLSearchParams();
        if (urgencyTier) params.append('urgencyTier', urgencyTier);
        const q = params.toString();
        return this.request(`/admin/moderation/review-escalations/pending${q ? '?' + q : ''}`);
    }

    /** Moderator + compliance — một danh sách đơn chờ admin (có counts). */
    static async adminGetPendingUnifiedEscalations(urgencyTier) {
        const params = new URLSearchParams();
        if (urgencyTier) params.append('urgencyTier', urgencyTier);
        const q = params.toString();
        return this.request(`/admin/moderation/review-escalations/pending-unified${q ? '?' + q : ''}`);
    }

    static async adminGetReviewEscalationHistory(skip, take) {
        const params = new URLSearchParams();
        if (skip != null && skip !== undefined) params.append('skip', skip);
        if (take != null && take !== undefined) params.append('take', take);
        const q = params.toString();
        return this.request(`/admin/moderation/review-escalations/history${q ? '?' + q : ''}`);
    }

    /** Log thống nhất đơn gửi admin: moderator escalation + compliance lock + compliance hành động (UnifiedEscalationLogQueryDto). */
    static async adminGetUnifiedEscalationLog(options = {}) {
        const params = new URLSearchParams();
        const n = (k, v) => { if (v !== undefined && v !== null && v !== '') params.append(k, v); };
        n('page', options.page);
        n('pageSize', options.pageSize);
        n('source', options.source);
        n('search', options.search);
        n('status', options.status);
        n('requestKind', options.requestKind);
        n('targetType', options.targetType);
        n('senderId', options.senderId);
        n('resolverId', options.resolverId);
        n('createdFrom', options.createdFrom);
        n('createdTo', options.createdTo);
        n('resolvedFrom', options.resolvedFrom);
        n('resolvedTo', options.resolvedTo);
        n('sortBy', options.sortBy);
        n('sortOrder', options.sortOrder);
        return this.request(`/admin/moderation/review-escalations/unified-log?${params.toString()}`);
    }

    static async adminGetModeratorsForAssignment() {
        return this.request('/admin/moderation/moderators-for-assignment');
    }

    static async adminResolveReviewEscalation(id, body) {
        return this.request(`/admin/moderation/review-escalations/${id}/resolve`, {
            method: 'POST',
            body: JSON.stringify(body)
        });
    }

    static async adminGetApprovedStories(options = {}) {
        const params = new URLSearchParams();
        params.append('page', options.page ?? 1);
        params.append('pageSize', options.pageSize ?? 20);
        if (options.search) params.append('search', options.search);
        if (options.sortBy) params.append('sortBy', options.sortBy);
        if (options.sortOrder) params.append('sortOrder', options.sortOrder);
        if (options.moderatorId) params.append('moderatorId', options.moderatorId);
        if (options.dateFrom) params.append('dateFrom', options.dateFrom);
        if (options.dateTo) params.append('dateTo', options.dateTo);
        return this.request(`/admin/moderation/approved-stories?${params.toString()}`);
    }

    static async adminGetRejectedStories(options = {}) {
        const params = new URLSearchParams();
        params.append('page', options.page ?? 1);
        params.append('pageSize', options.pageSize ?? 20);
        if (options.search) params.append('search', options.search);
        if (options.sortBy) params.append('sortBy', options.sortBy);
        if (options.sortOrder) params.append('sortOrder', options.sortOrder);
        if (options.moderatorId) params.append('moderatorId', options.moderatorId);
        if (options.dateFrom) params.append('dateFrom', options.dateFrom);
        if (options.dateTo) params.append('dateTo', options.dateTo);
        return this.request(`/admin/moderation/rejected-stories?${params.toString()}`);
    }

    static async adminGetApprovedChapters(options = {}) {
        const params = new URLSearchParams();
        params.append('page', options.page ?? 1);
        params.append('pageSize', options.pageSize ?? 20);
        if (options.search) params.append('search', options.search);
        if (options.sortBy) params.append('sortBy', options.sortBy);
        if (options.sortOrder) params.append('sortOrder', options.sortOrder);
        if (options.moderatorId) params.append('moderatorId', options.moderatorId);
        if (options.dateFrom) params.append('dateFrom', options.dateFrom);
        if (options.dateTo) params.append('dateTo', options.dateTo);
        return this.request(`/admin/moderation/approved-chapters?${params.toString()}`);
    }

    static async adminGetRejectedChapters(options = {}) {
        const params = new URLSearchParams();
        params.append('page', options.page ?? 1);
        params.append('pageSize', options.pageSize ?? 20);
        if (options.search) params.append('search', options.search);
        if (options.sortBy) params.append('sortBy', options.sortBy);
        if (options.sortOrder) params.append('sortOrder', options.sortOrder);
        if (options.moderatorId) params.append('moderatorId', options.moderatorId);
        if (options.dateFrom) params.append('dateFrom', options.dateFrom);
        if (options.dateTo) params.append('dateTo', options.dateTo);
        return this.request(`/admin/moderation/rejected-chapters?${params.toString()}`);
    }

    static async adminGetModerationLogs(options = {}) {
        const params = new URLSearchParams();
        const n = (k, v) => { if (v !== undefined && v !== null && v !== '') params.append(k, v); };
        n('page', options.page ?? 1);
        n('pageSize', options.pageSize ?? 20);
        n('search', options.search);
        n('moderatorId', options.moderatorId);
        n('dateFrom', options.dateFrom);
        n('dateTo', options.dateTo);
        n('action', options.action);
        n('targetType', options.targetType);
        n('targetId', options.targetId);
        n('processingTimeMinMs', options.processingTimeMinMs);
        n('processingTimeMaxMs', options.processingTimeMaxMs);
        n('sortBy', options.sortBy);
        n('sortOrder', options.sortOrder);
        return this.request(`/admin/moderation/logs?${params.toString()}`);
    }

    static async adminGetModeratorPerformance(options = {}) {
        const params = new URLSearchParams();
        const n = (k, v) => { if (v !== undefined && v !== null && v !== '') params.append(k, v); };
        n('page', options.page ?? 1);
        n('pageSize', options.pageSize ?? 20);
        n('dateFrom', options.dateFrom);
        n('dateTo', options.dateTo);
        n('targetType', options.targetType);
        n('search', options.search);
        n('minTotalActions', options.minTotalActions);
        n('sortBy', options.sortBy);
        n('sortOrder', options.sortOrder);
        n('moderatorId', options.moderatorId);
        return this.request(`/admin/moderation/moderator-performance?${params.toString()}`);
    }

    // Admin Wallet API (ví hệ thống platform_wallet)
    static async adminGetPlatformWalletBalance() {
        return this.request(`/admin/wallet/balance`, {
            method: 'GET'
        });
    }

    static async adminGetPlatformWalletSummary() {
        return this.request(`/admin/wallet/summary`, {
            method: 'GET'
        });
    }

    static async adminAdjustPlatformWallet(deltaCoins, note = null) {
        return this.request(`/admin/wallet/adjust`, {
            method: 'POST',
            body: JSON.stringify({ deltaCoins, note })
        });
    }

    static async adminGetPlatformWalletAdjustments(page = 1, pageSize = 20, filters = {}) {
        const params = new URLSearchParams({
            page: String(page),
            pageSize: String(pageSize)
        });

        if (filters) {
            if (filters.dateFrom) params.set('dateFrom', filters.dateFrom);
            if (filters.dateTo) params.set('dateTo', filters.dateTo);
            if (filters.type) params.set('type', filters.type);
            if (filters.q) params.set('q', filters.q);
        }

        return this.request(`/admin/wallet/adjustments?${params.toString()}`, {
            method: 'GET'
        });
    }

    // Admin User Wallet API (adjust specific user wallet balance_coin)
    static async adminAdjustUserWallet(targetUser, deltaCoins, note = null) {
        return this.request(`/admin/wallet/adjust-user-wallet`, {
            method: 'POST',
            body: JSON.stringify({ targetUser, deltaCoins, note })
        });
    }

    static async adminGetUserWalletAdjustments(page = 1, pageSize = 20, filters = {}) {
        const params = new URLSearchParams({
            page: String(page),
            pageSize: String(pageSize)
        });

        if (filters) {
            if (filters.dateFrom) params.set('dateFrom', filters.dateFrom);
            if (filters.dateTo) params.set('dateTo', filters.dateTo);
            if (filters.type) params.set('type', filters.type);
            if (filters.q) params.set('q', filters.q);
        }

        return this.request(`/admin/wallet/user-adjustments?${params.toString()}`, {
            method: 'GET'
        });
    }

    // Admin System Coin Ledger API (unlock / platform adj / withdraw)
    static async adminGetSystemCoinLedger(page = 1, pageSize = 20, filters = {}) {
        const params = new URLSearchParams({
            page: String(page),
            pageSize: String(pageSize)
        });

        if (filters) {
            if (filters.dateFrom) params.set('dateFrom', filters.dateFrom);
            if (filters.dateTo) params.set('dateTo', filters.dateTo);
            if (filters.type) params.set('type', filters.type);
        }

        return this.request(`/admin/wallet/system-coin-ledger?${params.toString()}`, {
            method: 'GET'
        });
    }

    // Notifications API
    static async getNotifications(options = {}) {
        const params = new URLSearchParams();
        if (options.limit != null) params.append('limit', options.limit);
        if (options.onlyUnread) params.append('onlyUnread', 'true');
        return this.request(`/notifications?${params.toString()}`);
    }

    static async getUnreadNotificationCount() {
        return this.request('/notifications/unread-count');
    }

    static async markNotificationRead(id) {
        return this.request(`/notifications/${id}/read`, { method: 'PATCH' });
    }

    static async markAllNotificationsRead() {
        return this.request('/notifications/mark-all-read', { method: 'POST' });
    }

    // Authentication API
    static async register(email, password, fullName = 'New User') {
        return this.request('/auth/register', {
            method: 'POST',
            body: JSON.stringify({ email, password, fullName })
        });
    }

    static async verifyOtp(email, otpCode) {
        return this.request('/auth/verify-otp', {
            method: 'POST',
            body: JSON.stringify({ email, otpCode })
        });
    }

    static async login(email, password) {
        return this.request('/auth/login', {
            method: 'POST',
            body: JSON.stringify({ email, password })
        });
    }

    static async logout() {
        return this.request('/auth/logout', {
            method: 'POST'
        });
    }

    static async refreshToken() {
        return this.request('/auth/refresh', {
            method: 'POST'
        });
    }

    static async forgotPassword(email) {
        return this.request('/auth/forgot-password', {
            method: 'POST',
            body: JSON.stringify({ email })
        });
    }

    static async resetPassword(token, newPassword) {
        return this.request('/auth/reset-password', {
            method: 'POST',
            body: JSON.stringify({ token, newPassword })
        });
    }

    // Coins / Wallet API
    static async getMyWallet() {
        return this.request('/coins/wallet', {
            method: 'GET'
        });
    }

    static async getMyChapterUnlockHistory(page = 1, pageSize = 20, filters = {}) {
        const params = new URLSearchParams({
            page: String(page),
            pageSize: String(pageSize),
        });

        if (filters) {
            if (filters.search) params.set('search', filters.search);
            if (filters.dateFrom) params.set('dateFrom', filters.dateFrom);
            if (filters.dateTo) params.set('dateTo', filters.dateTo);

            if (filters.minCoins !== null && filters.minCoins !== undefined && filters.minCoins !== '') {
                params.set('minCoins', String(filters.minCoins));
            }
            if (filters.maxCoins !== null && filters.maxCoins !== undefined && filters.maxCoins !== '') {
                params.set('maxCoins', String(filters.maxCoins));
            }
        }

        return this.request(`/coins/wallet/unlock-history?${params.toString()}`, {
            method: 'GET'
        });
    }

    static async getAuthorChapterUnlockIncomeHistory(page = 1, pageSize = 20) {
        return this.request(`/coins/author/unlock-chapter-income-history?page=${encodeURIComponent(page)}&pageSize=${encodeURIComponent(pageSize)}`, {
            method: 'GET'
        });
    }

    static async getAuthorChapterUnlockIncomeHistoryByStory(page = 1, pageSize = 20, filters = {}) {
        const params = new URLSearchParams({
            page: String(page),
            pageSize: String(pageSize)
        });

        if (filters) {
            if (filters.search) params.set('search', filters.search);
            if (filters.monthFrom) params.set('monthFrom', filters.monthFrom);
            if (filters.monthTo) params.set('monthTo', filters.monthTo);
            if (filters.status) params.set('status', filters.status);
        }

        return this.request(`/coins/author/unlock-chapter-income-history/by-story?${params.toString()}`, {
            method: 'GET'
        });
    }

    static async getStoryReportReasons() {
        return this.request('/story-reporting/reasons');
    }

    static async getCommentReportReasons() {
        return this.request('/comment-reporting/reasons');
    }

    static async reportStory(storyId, payload) {
        return this.request(`/stories/${encodeURIComponent(storyId)}/reports`, {
            method: 'POST',
            body: JSON.stringify(payload)
        });
    }

    static async reportStoryComment(storyId, commentId, payload) {
        return this.request(`/stories/${encodeURIComponent(storyId)}/comments/${encodeURIComponent(commentId)}/reports`, {
            method: 'POST',
            body: JSON.stringify(payload)
        });
    }

    static async reportChapterComment(chapterId, commentId, payload) {
        return this.request(`/chapters/${encodeURIComponent(chapterId)}/comments/${encodeURIComponent(commentId)}/reports`, {
            method: 'POST',
            body: JSON.stringify(payload)
        });
    }

    static async complianceGetStoryReports(query = {}) {
        const params = new URLSearchParams();
        Object.keys(query).forEach((key) => {
            const v = query[key];
            if (v !== null && v !== undefined && v !== '') params.append(key, v);
        });
        const qs = params.toString();
        return this.request(`/compliance/story-reports${qs ? '?' + qs : ''}`);
    }

    /** COMPLIANCE (đang lock truyện): đóng một báo cáo — status RESOLVED | DISMISSED */
    static async complianceResolveReport(reportId, body = {}) {
        return this.request(`/compliance/story-reports/${encodeURIComponent(reportId)}/resolve`, {
            method: 'POST',
            body: JSON.stringify({ status: body.status || 'RESOLVED' })
        });
    }

    /** COMPLIANCE: đóng mọi báo cáo mở của truyện */
    static async complianceResolveAllOpenReports(storyId, body = {}) {
        return this.request(`/compliance/story-reports/stories/${encodeURIComponent(storyId)}/resolve-all-open`, {
            method: 'POST',
            body: JSON.stringify({ status: body.status || 'RESOLVED' })
        });
    }

    /** COMPLIANCE: đánh dấu / gỡ đánh dấu xác minh cho từng người báo truyện (story_report_contributors) */
    static async complianceSetStoryContributorVerification(storyId, body = {}) {
        return this.request(`/compliance/story-reports/stories/${encodeURIComponent(storyId)}/contributor-verification`, {
            method: 'POST',
            body: JSON.stringify({
                verifyUserIds: body.verifyUserIds || body.VerifyUserIds || [],
                unverifyUserIds: body.unverifyUserIds || body.UnverifyUserIds || []
            })
        });
    }

    /** COMPLIANCE: đóng mọi report comment mở của comment thread */
    static async complianceResolveAllOpenCommentReports(commentId, body = {}) {
        // body: { status: 'RESOLVED'|'DISMISSED', HideComment: bool, IncludeReplies: bool }
        return this.request(`/compliance/comment-reports/comments/${encodeURIComponent(commentId)}/resolve-all-open`, {
            method: 'POST',
            body: JSON.stringify({
                status: body.status || 'RESOLVED',
                HideComment: body.HideComment ?? true,
                IncludeReplies: body.IncludeReplies ?? true
            })
        });
    }

    /** COMPLIANCE: đánh dấu / gỡ đánh dấu xác minh cho từng report_evidences (request người báo cáo) */
    static async complianceSetCommentReportEvidenceVerification(commentId, body = {}) {
        return this.request(`/compliance/comment-reports/comments/${encodeURIComponent(commentId)}/evidence-verification`, {
            method: 'POST',
            body: JSON.stringify({
                verifyEvidenceIds: body.verifyEvidenceIds || body.VerifyEvidenceIds || [],
                unverifyEvidenceIds: body.unverifyEvidenceIds || body.UnverifyEvidenceIds || []
            })
        });
    }

    static async complianceGetMyResolvedHistory(query = {}) {
        const params = new URLSearchParams();
        if (query.page) params.append('page', query.page);
        if (query.pageSize) params.append('pageSize', query.pageSize);
        if (query.search) params.append('search', query.search);
        const qs = params.toString();
        return this.request(`/compliance/story-reports/my-resolved-history${qs ? '?' + qs : ''}`);
    }

    static async complianceUpdateStoryReport(reportId, status) {
        return this.request(`/admin/compliance-story-reports/${encodeURIComponent(reportId)}/status`, {
            method: 'PATCH',
            body: JSON.stringify({ status })
        });
    }

    static async complianceRequestLockRelease(storyId, body) {
        return this.request(`/compliance/story-reports/stories/${encodeURIComponent(storyId)}/request-release`, {
            method: 'POST',
            body: JSON.stringify(body || {})
        });
    }

    static async complianceGetCommentReports(query = {}) {
        const params = new URLSearchParams();
        Object.keys(query).forEach((key) => {
            const v = query[key];
            if (v !== null && v !== undefined && v !== '') params.append(key, v);
        });
        const qs = params.toString();
        return this.request(`/compliance/comment-reports${qs ? '?' + qs : ''}`);
    }

    static async complianceResolveCommentReport(reportId, body = {}) {
        return this.request(`/compliance/comment-reports/${encodeURIComponent(reportId)}/resolve`, {
            method: 'POST',
            body: JSON.stringify(body || {})
        });
    }

    /** COMPLIANCE: nhận lock / claim xử lý report comment */
    static async complianceClaimCommentReports(commentId) {
        return this.request(`/compliance/comment-reports/comments/${encodeURIComponent(commentId)}/claim`, {
            method: 'POST',
            body: JSON.stringify({})
        });
    }

    static async complianceRequestCommentLockRelease(commentId, body) {
        return this.request(`/compliance/comment-reports/comments/${encodeURIComponent(commentId)}/request-release`, {
            method: 'POST',
            body: JSON.stringify(body || {})
        });
    }

    static async complianceRequestAdminActionOnComment(commentId, body) {
        return this.request(`/compliance/comment-reports/comments/${encodeURIComponent(commentId)}/admin-action-requests`, {
            method: 'POST',
            body: JSON.stringify(body || {})
        });
    }

    /** ADMIN: gỡ lock claim report comment */
    static async adminReleaseComplianceCommentClaim(commentId) {
        return this.request(`/admin/compliance-comment-reports/comments/${encodeURIComponent(commentId)}/release-claim`, {
            method: 'POST'
        });
    }

    static async adminListComplianceLockRequests(status = 'PENDING') {
        const q = status ? `?status=${encodeURIComponent(status)}` : '';
        return this.request(`/admin/compliance-story-reports/lock-requests${q}`);
    }

    static async adminListComplianceOfficersForReports() {
        return this.request('/admin/compliance-story-reports/compliance-officers');
    }

    static async adminResolveComplianceLockRequest(requestId, body) {
        return this.request(`/admin/compliance-story-reports/lock-requests/${encodeURIComponent(requestId)}/resolve`, {
            method: 'POST',
            body: JSON.stringify(body)
        });
    }

    static async adminGetComplianceLogs(query = {}) {
        const params = new URLSearchParams();
        Object.keys(query).forEach((key) => {
            const v = query[key];
            if (v !== null && v !== undefined && v !== '') params.append(key, v);
        });
        const qs = params.toString();
        return this.request(`/admin/compliance-story-reports/compliance-logs${qs ? '?' + qs : ''}`);
    }

    static async adminGetCompliancePerformance(query = {}) {
        const params = new URLSearchParams();
        Object.keys(query).forEach((key) => {
            const v = query[key];
            if (v !== null && v !== undefined && v !== '') params.append(key, v);
        });
        const qs = params.toString();
        return this.request(`/admin/compliance-story-reports/compliance-performance${qs ? '?' + qs : ''}`);
    }

    static async adminComplianceReleaseStoryReportClaim(storyId) {
        return this.request(`/admin/compliance-story-reports/stories/${encodeURIComponent(storyId)}/release-claim`, {
            method: 'POST'
        });
    }

    static async complianceClaimStoryReports(storyId) {
        return this.request(`/compliance/story-reports/stories/${encodeURIComponent(storyId)}/claim`, {
            method: 'POST',
            body: '{}'
        });
    }

    static async complianceReleaseStoryClaim(storyId) {
        return this.request(`/admin/compliance-story-reports/stories/${encodeURIComponent(storyId)}/release-claim`, {
            method: 'POST'
        });
    }

    static async complianceSetStoryFlag(storyId, body) {
        return this.request(`/compliance/story-reports/stories/${encodeURIComponent(storyId)}/flag`, {
            method: 'POST',
            body: JSON.stringify(body || {})
        });
    }

    static async complianceSetStoryCommentsDisabled(storyId, value) {
        return this.request(`/compliance/story-reports/stories/${encodeURIComponent(storyId)}/comments-disabled`, {
            method: 'POST',
            body: JSON.stringify({ value: !!value })
        });
    }

    static async complianceSetStoryHidden(storyId, value) {
        return this.request(`/compliance/story-reports/stories/${encodeURIComponent(storyId)}/compliance-hidden`, {
            method: 'POST',
            body: JSON.stringify({ value: !!value })
        });
    }

    /** COMPLIANCE: bật/tắt tạm khóa quyền viết tác giả truyện (không qua admin). */
    static async complianceSetStoryAuthorWritingSuspended(storyId, value) {
        return this.request(`/compliance/story-reports/stories/${encodeURIComponent(storyId)}/author-writing-suspended`, {
            method: 'POST',
            body: JSON.stringify({ value: !!value })
        });
    }

    /** COMPLIANCE: bật/tắt tạm khóa quyền viết (thread comment; body.value + tuỳ chọn targetUserId). */
    static async complianceSetCommentAuthorWritingSuspended(commentId, body) {
        return this.request(`/compliance/comment-reports/comments/${encodeURIComponent(commentId)}/author-writing-suspended`, {
            method: 'POST',
            body: JSON.stringify(body || {})
        });
    }

    static async complianceRequestAdminAction(storyId, body) {
        return this.request(`/compliance/story-reports/stories/${encodeURIComponent(storyId)}/admin-action-requests`, {
            method: 'POST',
            body: JSON.stringify(body || {})
        });
    }

    static async complianceListUserViolations(userId, take = 80) {
        return this.request(`/compliance/story-reports/users/${encodeURIComponent(userId)}/violations?take=${encodeURIComponent(take)}`, {
            method: 'GET'
        });
    }

    static async adminListComplianceActionRequests(status = 'PENDING') {
        const q = status ? `?status=${encodeURIComponent(status)}` : '';
        return this.request(`/admin/compliance-story-reports/admin-action-requests${q}`);
    }

    static async adminResolveComplianceActionRequest(requestId, body) {
        return this.request(`/admin/compliance-story-reports/admin-action-requests/${encodeURIComponent(requestId)}/resolve`, {
            method: 'POST',
            body: JSON.stringify(body)
        });
    }
}

// Utility functions
/** Chuỗi ISO từ API không có offset → coi là UTC (tránh browser parse như giờ địa phương). */
function normalizeApiDateForParse(dateString) {
    if (dateString == null || dateString === '') return dateString;
    let s = String(dateString).trim();
    // .NET / SQL đôi khi: "2024-03-22 14:30:00" — chuẩn hóa rồi gắn Z (đồng bộ với ApiDateTime.AsUtcForJson trên server)
    if (/^\d{4}-\d{2}-\d{2} \d{2}:\d{2}/.test(s) && !/[zZ]$/.test(s) && !/[+-]\d{2}/.test(s)) {
        s = s.replace(' ', 'T');
        if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}$/.test(s)) s += ':00';
    }
    if (/^\d{4}-\d{2}-\d{2}T/.test(s) && !/[zZ]$/.test(s) && !/[+-][0-9]{2}/.test(s))
        return s + 'Z';
    return s;
}

/**
 * Giá trị `<input type="datetime-local">` (không kèm múi giờ) → ISO UTC gửi API.
 * Dùng thành phần lịch theo giờ địa phương máy người dùng, không dùng Date.parse(chuỗi) (tránh khác nhau giữa trình duyệt/OS).
 */
function datetimeLocalValueToIsoUtc(localVal) {
    if (localVal == null || localVal === '') return '';
    const s = String(localVal).trim();
    const m = s.match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?/);
    if (!m) return '';
    const y = +m[1];
    const mo = +m[2] - 1;
    const d = +m[3];
    const h = +m[4];
    const mi = +m[5];
    const sec = m[6] != null ? +m[6] : 0;
    const dt = new Date(y, mo, d, h, mi, sec, 0);
    if (isNaN(dt.getTime())) return '';
    return dt.toISOString();
}

const Utils = {
    showAlert: (message, type = 'info') => {
        const alertDiv = document.createElement('div');
        alertDiv.className = `alert alert-${type} alert-dismissible fade show`;
        alertDiv.innerHTML = `
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;
        document.body.insertBefore(alertDiv, document.body.firstChild);
        setTimeout(() => alertDiv.remove(), 5000);
    },

    formatDate: (dateString) => {
        if (!dateString) return 'N/A';
        const s = normalizeApiDateForParse(dateString);
        const date = new Date(s);
        if (isNaN(date.getTime())) return String(dateString);
        return date.toLocaleString('vi-VN');
    },

    /** datetime-local → ISO UTC (gửi claim / hạn duyệt / lọc ngày). */
    datetimeLocalToIsoUtc: datetimeLocalValueToIsoUtc,

    /** Hiển thị theo múi UTC (có hậu tố UTC). */
    formatDateUtc: (dateString) => {
        if (!dateString) return 'N/A';
        const s = normalizeApiDateForParse(dateString);
        const date = new Date(s);
        if (isNaN(date.getTime())) return String(dateString);
        return date.toLocaleString('vi-VN', { timeZone: 'UTC' }) + ' UTC';
    },

    /** Thời điểm trong quá khứ (UTC) → "đã gửi x giây trước" / "đã gửi x tháng trước" (tiếng Việt). */
    formatRelativePastVi: (iso) => {
        if (!iso) return '';
        const t = new Date(normalizeApiDateForParse(iso)).getTime();
        if (isNaN(t)) return '';
        const sec = Math.floor((Date.now() - t) / 1000);
        if (sec < 0) return 'vừa mới';
        if (sec < 10) return 'vừa xong';
        if (sec < 60) return `đã gửi ${sec} giây trước`;
        const min = Math.floor(sec / 60);
        if (min < 60) return `đã gửi ${min} phút trước`;
        const hr = Math.floor(min / 60);
        if (hr < 24) return `đã gửi ${hr} giờ trước`;
        const day = Math.floor(hr / 24);
        if (day < 30) return `đã gửi ${day} ngày trước`;
        const approxMonth = Math.floor(day / 30);
        if (approxMonth < 12) return `đã gửi ${approxMonth} tháng trước`;
        const yr = Math.floor(day / 365);
        return `đã gửi ${yr} năm trước`;
    },

    /** Đếm ngược tới hạn SLA (ISO UTC/local) — "còn X ngày …" / "đã quá hạn …". */
    formatCountdownRemainingVi: (isoDeadline) => {
        if (!isoDeadline) return '';
        const end = new Date(normalizeApiDateForParse(isoDeadline)).getTime();
        if (isNaN(end)) return '';
        const sec = Math.floor((end - Date.now()) / 1000);
        if (sec < 0) {
            const over = Math.abs(sec);
            const d = Math.floor(over / 86400);
            const h = Math.floor((over % 86400) / 3600);
            if (d >= 1) return `đã quá hạn ${d} ngày`;
            if (h >= 1) return `đã quá hạn ${h} giờ`;
            const m = Math.floor(over / 60);
            return m >= 1 ? `đã quá hạn ${m} phút` : 'đã quá hạn';
        }
        const d = Math.floor(sec / 86400);
        const h = Math.floor((sec % 86400) / 3600);
        const m = Math.floor((sec % 3600) / 60);
        if (d >= 1) return `còn ${d} ngày ${h} giờ`;
        if (h >= 1) return `còn ${h} giờ ${m} phút`;
        if (m >= 1) return `còn ${m} phút`;
        return 'sắp hết hạn';
    },

    /** Badge theo thời gian đã chờ kể từ lúc tác giả gửi (OnTime | Warning | Critical | Overdue). */
    moderatorDeadlineBadgeHtml: (timeStatus) => {
        if (timeStatus === 'Overdue') return '<span class="badge bg-danger badge-status">Chờ quá lâu</span>';
        if (timeStatus === 'Critical') return '<span class="badge bg-danger badge-status">Chờ lâu</span>';
        if (timeStatus === 'Warning') return '<span class="badge bg-warning text-dark badge-status">Chờ khá lâu</span>';
        return '<span class="badge bg-success badge-status">Mới gửi</span>';
    },

    formatNumber: (num) => {
        if (num === null || num === undefined) return '0';
        return new Intl.NumberFormat('vi-VN').format(num);
    }
};