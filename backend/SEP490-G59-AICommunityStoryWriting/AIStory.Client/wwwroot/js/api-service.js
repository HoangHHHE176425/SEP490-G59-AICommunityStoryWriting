// API Base URL: từ server config (Layout) hoặc mặc định khi chạy API trên port 5000
const API_BASE_URL = (typeof window !== 'undefined' && window.__API_BASE_URL) ? window.__API_BASE_URL : 'http://localhost:5000/api';

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
                try {
                    const errorBody = await response.json();
                    // Prefer detailed error message if backend provides `error`.
                    errorMessage = errorBody.error || errorBody.message || errorMessage;
                } catch {
                    // If response body is not JSON, use statusText
                }
                throw new Error(errorMessage || `HTTP error! status: ${response.status}`);
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

    static async deleteChapter(id) {
        return this.request(`/chapters/${id}`, {
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
        return this.request(`/moderator/chapters/pending?${params.toString()}`);
    }

    static async moderatorClaimStory(id) {
        return this.request(`/moderator/stories/${id}/claim`, { method: 'POST' });
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

    static async moderatorClaimChapter(id) {
        return this.request(`/moderator/chapters/${id}/claim`, { method: 'POST' });
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
        params.append('page', options.page ?? 1);
        params.append('pageSize', options.pageSize ?? 20);
        if (options.moderatorId) params.append('moderatorId', options.moderatorId);
        if (options.dateFrom) params.append('dateFrom', options.dateFrom);
        if (options.dateTo) params.append('dateTo', options.dateTo);
        if (options.action) params.append('action', options.action);
        if (options.targetType) params.append('targetType', options.targetType);
        return this.request(`/admin/moderation/logs?${params.toString()}`);
    }

    static async adminGetModeratorPerformance(options = {}) {
        const params = new URLSearchParams();
        if (options.dateFrom) params.append('dateFrom', options.dateFrom);
        if (options.dateTo) params.append('dateTo', options.dateTo);
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
}

// Utility functions
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
        const date = new Date(dateString);
        return date.toLocaleString('vi-VN');
    },

    formatNumber: (num) => {
        if (num === null || num === undefined) return '0';
        return new Intl.NumberFormat('vi-VN').format(num);
    }
};