import axiosInstance from "../axiosInstance";

/**
 * Tạo chapter mới.
 * @param {Object} data - {
 *   storyId (required, Guid),
 *   title (required),
 *   content?,
 *   orderIndex (required, int),
 *   status?: string (DRAFT, PUBLISHED),
 *   accessType?: string (FREE, PAID),
 *   coinPrice?: number,
 *   aiContributionRatio?: number,
 *   isAiClean?: boolean
 * }
 * @returns {Promise} - Created chapter từ server
 */
export async function createChapter(data) {
    const title = (data.title || "").trim();
    if (!title) {
        throw new Error("Tiêu đề chương không được để trống");
    }
    if (title.length > 255) {
        throw new Error("Tiêu đề chương không được vượt quá 255 ký tự");
    }
    if (!data.storyId) {
        throw new Error("StoryId không được để trống");
    }
    const orderIndex = parseInt(data.orderIndex, 10);
    if (isNaN(orderIndex) || orderIndex < 0) {
        throw new Error("OrderIndex phải là số nguyên không âm");
    }

    const body = {
        storyId: data.storyId,
        title: title,
        content: data.content ?? "",
        orderIndex: orderIndex,
        status: data.status || "DRAFT",
        accessType: data.accessType || "FREE",
        coinPrice: data.coinPrice ?? 0,
        aiContributionRatio: data.aiContributionRatio ?? 0,
        isAiClean: data.isAiClean ?? false,
    };

    const response = await axiosInstance.post("/chapters", body);
    return response.data;
}

/**
 * Lấy danh sách chapters có phân trang và lọc.
 * @param {Object} params - { storyId?, page?, pageSize?, status?, accessType?, sortBy?, sortOrder? }
 * @returns {Promise} - PagedResultDto
 */
export async function getChapters(params = {}) {
    const q = new URLSearchParams();
    if (params.storyId) q.append("storyId", params.storyId);
    if (params.page != null) q.append("page", params.page);
    if (params.pageSize != null) q.append("pageSize", params.pageSize);
    if (params.status) q.append("status", params.status);
    if (params.accessType) q.append("accessType", params.accessType);
    if (params.sortBy) q.append("sortBy", params.sortBy);
    if (params.sortOrder) q.append("sortOrder", params.sortOrder);

    const url = q.toString() ? `/chapters?${q}` : "/chapters";
    const response = await axiosInstance.get(url);
    return response.data;
}

/**
 * Lấy chapter theo ID.
 * @param {string} id - Guid
 * @returns {Promise}
 */
export async function getChapterById(id) {
    const response = await axiosInstance.get(`/chapters/${id}`);
    return response.data;
}

/**
 * Lấy tất cả chapters của một story.
 * @param {string} storyId - Guid
 * @returns {Promise} - Mảng chapters
 */
export async function getChaptersByStoryId(storyId) {
    const response = await axiosInstance.get(`/chapters/story/${storyId}`);
    return response.data;
}

/**
 * Lấy chapter theo storyId và order index.
 * @param {string} storyId - Guid
 * @param {number} orderIndex
 * @returns {Promise}
 */
export async function getChapterByStoryIdAndOrderIndex(storyId, orderIndex) {
    const response = await axiosInstance.get(`/chapters/story/${storyId}/order/${orderIndex}`);
    return response.data;
}

/**
 * Cập nhật chapter.
 * @param {string} id - Guid
 * @param {Object} data - { title?, content?, orderIndex?, status?, accessType?, coinPrice?, aiContributionRatio?, isAiClean? }
 * @returns {Promise} - NoContent khi thành công
 */
export async function updateChapter(id, data) {
    const title = (data.title || "").trim();
    if (!title) {
        throw new Error("Tiêu đề chương không được để trống");
    }
    if (title.length > 255) {
        throw new Error("Tiêu đề chương không được vượt quá 255 ký tự");
    }

    const body = {
        title: title,
        content: data.content ?? "",
        orderIndex: data.orderIndex != null ? parseInt(data.orderIndex, 10) : undefined,
        status: data.status,
        accessType: data.accessType,
        coinPrice: data.coinPrice,
        aiContributionRatio: data.aiContributionRatio,
        isAiClean: data.isAiClean,
    };

    const response = await axiosInstance.put(`/chapters/${id}`, body);
    return response.data;
}

/**
 * Xóa chapter.
 * @param {string} id - Guid
 * @returns {Promise}
 */
export async function deleteChapter(id) {
    const response = await axiosInstance.delete(`/chapters/${id}`);
    return response.data;
}

/**
 * Publish chapter.
 * @param {string} id - Guid
 * @returns {Promise}
 */
export async function publishChapter(id) {
    const response = await axiosInstance.post(`/chapters/${id}/publish`);
    return response.data;
}

/**
 * Unpublish chapter.
 * @param {string} id - Guid
 * @returns {Promise}
 */
export async function unpublishChapter(id) {
    const response = await axiosInstance.post(`/chapters/${id}/unpublish`);
    return response.data;
}

/**
 * Sắp xếp lại thứ tự chapter.
 * @param {string} id - Guid của chapter
 * @param {number} newOrderIndex - Thứ tự mới
 * @returns {Promise}
 */
export async function reorderChapter(id, newOrderIndex) {
    const response = await axiosInstance.post(`/chapters/${id}/reorder`, newOrderIndex, {
        headers: { "Content-Type": "application/json" },
    });
    return response.data;
}
