// API Base URL
const API_BASE_URL = 'http://localhost:5000/api';

// API Service Class
class ApiService {
    static async request(url, options = {}) {
        try {
            const response = await fetch(`${API_BASE_URL}${url}`, {
                headers: {
                    'Content-Type': 'application/json',
                    ...options.headers
                },
                ...options
            });

            // Handle NoContent responses (204) first - these are successful
            if (response.status === 204) {
                return null;
            }

            if (!response.ok) {
                let errorMessage = response.statusText;
                try {
                    const errorBody = await response.json();
                    errorMessage = errorBody.message || errorBody.error || errorMessage;
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
        return fetch(`${API_BASE_URL}/categories`, {
            method: 'POST',
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
        return fetch(`${API_BASE_URL}/categories/${id}`, {
            method: 'PUT',
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
        return fetch(`${API_BASE_URL}/stories`, {
            method: 'POST',
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
        return fetch(`${API_BASE_URL}/stories/${id}`, {
            method: 'PUT',
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
