import axiosInstance from "../axiosInstance";

/**
 * Tạo truyện mới (multipart/form-data).
 * @param {Object} data - {
 *   title (required),
 *   summary?,
 *   categoryIds?: string[] (Guid),
 *   ageRating?: string (ALL, 13+, 16+, 18+),
 *   storyProgressStatus?: string (ONGOING, COMPLETED, HIATUS),
 *   authorId?: string (Guid - dev mode khi chưa có auth),
 *   coverImage?: File
 * }
 * @returns {Promise} - Created story từ server
 */
export async function createStory(data) {
    const title = (data.title || "").trim();
    if (!title) {
        throw new Error("Tiêu đề truyện không được để trống");
    }
    if (title.length > 255) {
        throw new Error("Tiêu đề truyện không được vượt quá 255 ký tự");
    }

    const formData = new FormData();
    formData.append("Title", title);

    if (data.summary != null && data.summary !== "") {
        formData.append("Summary", String(data.summary).trim());
    }

    if (Array.isArray(data.categoryIds) && data.categoryIds.length > 0) {
        data.categoryIds.forEach((id) => {
            if (id) formData.append("CategoryIds", id);
        });
    }

    formData.append("AgeRating", data.ageRating || "ALL");
    formData.append("StoryProgressStatus", data.storyProgressStatus || "ONGOING");

    if (data.authorId) {
        formData.append("AuthorId", data.authorId);
    }

    if (data.coverImage instanceof File) {
        const allowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
        const ext = data.coverImage.name.toLowerCase().substring(data.coverImage.name.lastIndexOf("."));
        if (!allowedExtensions.includes(ext)) {
            throw new Error(`Ảnh bìa: chỉ chấp nhận ${allowedExtensions.join(", ").toUpperCase()}`);
        }
        if (data.coverImage.size > 5 * 1024 * 1024) {
            throw new Error("Kích thước ảnh bìa không được vượt quá 5MB");
        }
        formData.append("CoverImage", data.coverImage);
    }

    const response = await axiosInstance.post("/stories", formData, {
        headers: { "Content-Type": "multipart/form-data" },
    });
    return response.data;
}

/**
 * Lấy danh sách truyện có phân trang và lọc.
 * @param {Object} params - { page?, pageSize?, search?, categoryId?, authorId?, status?, sortBy?, sortOrder? }
 * @returns {Promise} - PagedResultDto
 */
export async function getStories(params = {}) {
    const q = new URLSearchParams();
    if (params.page != null) q.append("page", params.page);
    if (params.pageSize != null) q.append("pageSize", params.pageSize);
    if (params.search) q.append("search", params.search);
    if (params.categoryId) q.append("categoryId", params.categoryId);
    if (params.authorId) q.append("authorId", params.authorId);
    if (params.status) q.append("status", params.status);
    if (params.sortBy) q.append("sortBy", params.sortBy);
    if (params.sortOrder) q.append("sortOrder", params.sortOrder);

    const url = q.toString() ? `/stories?${q}` : "/stories";
    const response = await axiosInstance.get(url);
    return response.data;
}

/**
 * Lấy truyện theo ID.
 * @param {string} id - Guid
 * @returns {Promise}
 */
export async function getStoryById(id) {
    const response = await axiosInstance.get(`/stories/${id}`);
    return response.data;
}

/**
 * Lấy truyện theo slug.
 * @param {string} slug
 * @returns {Promise}
 */
export async function getStoryBySlug(slug) {
    const response = await axiosInstance.get(`/stories/slug/${encodeURIComponent(slug)}`);
    return response.data;
}

/**
 * Lấy truyện theo author.
 * @param {string} authorId - Guid
 * @param {Object} params - { page?, pageSize?, search?, status?, sortBy?, sortOrder? }
 * @returns {Promise}
 */
export async function getStoriesByAuthor(authorId, params = {}) {
    const q = new URLSearchParams();
    if (params.page != null) q.append("page", params.page);
    if (params.pageSize != null) q.append("pageSize", params.pageSize);
    if (params.search) q.append("search", params.search);
    if (params.status) q.append("status", params.status);
    if (params.sortBy) q.append("sortBy", params.sortBy);
    if (params.sortOrder) q.append("sortOrder", params.sortOrder);

    const url = q.toString() ? `/stories/author/${authorId}?${q}` : `/stories/author/${authorId}`;
    const response = await axiosInstance.get(url);
    return response.data;
}

/**
 * Cập nhật truyện (multipart/form-data).
 * @param {string} id - Guid
 * @param {Object} data - { title?, summary?, categoryIds?, status?, ageRating?, storyProgressStatus?, coverImage? (File) }
 * @returns {Promise} - NoContent khi thành công
 */
export async function updateStory(id, data) {
    const title = (data.title || "").trim();
    if (!title) {
        throw new Error("Tiêu đề truyện không được để trống");
    }
    if (title.length > 255) {
        throw new Error("Tiêu đề truyện không được vượt quá 255 ký tự");
    }

    const formData = new FormData();
    formData.append("Title", title);
    formData.append("Summary", data.summary != null ? String(data.summary).trim() : "");
    formData.append("Status", data.status || "DRAFT");
    formData.append("AgeRating", data.ageRating || "ALL");
    formData.append("StoryProgressStatus", data.storyProgressStatus || "ONGOING");

    if (Array.isArray(data.categoryIds)) {
        data.categoryIds.forEach((cid) => {
            if (cid) formData.append("CategoryIds", cid);
        });
    }

    if (data.coverImage instanceof File) {
        const allowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
        const ext = data.coverImage.name.toLowerCase().substring(data.coverImage.name.lastIndexOf("."));
        if (!allowedExtensions.includes(ext)) {
            throw new Error(`Ảnh bìa: chỉ chấp nhận ${allowedExtensions.join(", ").toUpperCase()}`);
        }
        if (data.coverImage.size > 5 * 1024 * 1024) {
            throw new Error("Kích thước ảnh bìa không được vượt quá 5MB");
        }
        formData.append("CoverImage", data.coverImage);
    }

    const response = await axiosInstance.put(`/stories/${id}`, formData, {
        headers: { "Content-Type": "multipart/form-data" },
    });
    return response.data;
}

/**
 * Xóa truyện.
 * @param {string} id - Guid
 * @returns {Promise}
 */
export async function deleteStory(id) {
    const response = await axiosInstance.delete(`/stories/${id}`);
    return response.data;
}

/**
 * Publish truyện.
 * @param {string} id - Guid
 * @returns {Promise}
 */
export async function publishStory(id) {
    const response = await axiosInstance.post(`/stories/${id}/publish`);
    return response.data;
}

/**
 * Unpublish truyện.
 * @param {string} id - Guid
 * @returns {Promise}
 */
export async function unpublishStory(id) {
    const response = await axiosInstance.post(`/stories/${id}/unpublish`);
    return response.data;
}
