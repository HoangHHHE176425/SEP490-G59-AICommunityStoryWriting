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
 * Lấy lý do từ chối chương (cho tác giả). GET /chapters/{id}/rejection-reason.
 * @param {string} id - Guid chương
 * @returns {Promise<{ reason: string|null, rejectedAt: string|null }>}
 */
export async function getChapterRejectionReason(id) {
    const response = await axiosInstance.get(`/chapters/${id}/rejection-reason`);
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
 * Cập nhật chapter (tạo version tự động trên BE khi có thay đổi nội dung).
 * @param {string} id - Guid
 * @param {Object} data - { title?, content?, orderIndex?, status?, accessType?, coinPrice?, aiContributionRatio?, isAiClean?, changeSummary? }
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
    if (data.changeSummary != null && String(data.changeSummary).trim() !== '') {
        body.changeSummary = String(data.changeSummary).trim();
    }

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

/**
 * Duyệt chương (phê duyệt / approve) – gọi POST /chapters/{id}/publish, chuyển status sang PUBLISHED.
 * @param {string} id - Guid chương
 * @returns {Promise}
 */
export async function approveChapter(id) {
    const response = await axiosInstance.post(`/chapters/${id}/publish`);
    return response.data;
}

/**
 * Từ chối duyệt chương – cập nhật status sang REJECTED qua PUT /chapters/{id}.
 * Cần title và content (backend Update chapter yêu cầu); nếu không truyền sẽ gọi getChapterById rồi cập nhật.
 * @param {string} id - Guid chương
 * @param {Object} [chapterData] - { title?, content?, orderIndex? } (nếu không truyền sẽ tự fetch)
 * @returns {Promise}
 */
export async function rejectChapter(id, chapterData) {
    let title = chapterData?.title ?? chapterData?.Title;
    let content = chapterData?.content ?? chapterData?.Content;
    let orderIndex = chapterData?.orderIndex ?? chapterData?.OrderIndex;
    if (title == null || content == null) {
        const chapter = await getChapterById(id);
        title = title ?? chapter?.title ?? chapter?.Title ?? '';
        content = content ?? chapter?.content ?? chapter?.Content ?? '';
        if (orderIndex == null) orderIndex = chapter?.orderIndex ?? chapter?.OrderIndex;
    }
    const payload = {
        title: title || 'Chương',
        content: content ?? '',
        status: 'REJECTED',
    };
    if (orderIndex != null) payload.orderIndex = orderIndex;
    return updateChapter(id, payload);
}

// ---------- Chapter Versions (AUTHOR) ----------
/** GET /chapters/{chapterId}/versions - Lấy danh sách version của chapter. */
export async function getChapterVersions(chapterId) {
    const response = await axiosInstance.get(`/chapters/${chapterId}/versions`);
    return response.data;
}

/** GET /chapters/{chapterId}/versions/{versionId} - Lấy chi tiết một version. */
export async function getChapterVersionById(chapterId, versionId) {
    const response = await axiosInstance.get(`/chapters/${chapterId}/versions/${versionId}`);
    const raw = response?.data;
    if (raw == null) return raw;
    return raw?.data ?? raw;
}

/** POST /chapters/{chapterId}/versions - Tạo version mới. Body: { titleSnapshot?, contentSnapshot? } */
export async function createChapterVersion(chapterId, data = {}) {
    const body = {
        titleSnapshot: data.titleSnapshot ?? data.title ?? '',
        contentSnapshot: data.contentSnapshot ?? data.content ?? '',
    };
    const response = await axiosInstance.post(`/chapters/${chapterId}/versions`, body);
    return response.data;
}

/** PUT /chapters/{chapterId}/versions/{versionId} - Cập nhật version (chỉ DRAFT). */
export async function updateChapterVersion(chapterId, versionId, data = {}) {
    const body = {};
    if (data.titleSnapshot != null || data.title != null) body.titleSnapshot = data.titleSnapshot ?? data.title ?? '';
    if (data.contentSnapshot != null || data.content != null) body.contentSnapshot = data.contentSnapshot ?? data.content ?? '';
    const response = await axiosInstance.put(`/chapters/${chapterId}/versions/${versionId}`, body);
    return response.data;
}

/** DELETE /chapters/{chapterId}/versions/{versionId} - Xóa version (chỉ DRAFT). */
export async function deleteChapterVersion(chapterId, versionId) {
    const response = await axiosInstance.delete(`/chapters/${chapterId}/versions/${versionId}`);
    return response.data;
}

/** POST /chapters/{chapterId}/versions/{versionId}/submit - Gửi duyệt version (xuất bản). */
export async function submitChapterVersion(chapterId, versionId) {
    const response = await axiosInstance.post(`/chapters/${chapterId}/versions/${versionId}/submit`);
    return response.data;
}

/** POST /chapters/{chapterId}/versions/{versionId}/unsubmit - Hủy gửi duyệt version (đưa version và chapter về DRAFT). Chỉ version PENDING_REVIEW. */
export async function unsubmitChapterVersion(chapterId, versionId) {
    const response = await axiosInstance.post(`/chapters/${chapterId}/versions/${versionId}/unsubmit`);
    return response.data;
}

// ---------- Chapter Comments (AllowAnonymous list; POST cần đăng nhập + đã đọc ít nhất 1 chương) ----------

/** GET api/chapters/{id}/comments - Danh sách comment của chapter (AllowAnonymous). */
export async function getChapterComments(chapterId) {
    const response = await axiosInstance.get(`/chapters/${chapterId}/comments`);
    return response.data;
}

/** POST api/chapters/{id}/comments - Thêm comment (body: content, parentId?). Yêu cầu đăng nhập và đã đọc ít nhất 1 chapter của truyện. */
export async function addChapterComment(chapterId, body) {
    const payload = {
        content: (body?.content ?? '').trim(),
        parentId: body?.parentId ?? null,
    };
    const response = await axiosInstance.post(`/chapters/${chapterId}/comments`, payload);
    return response.data;
}

/** GET api/chapters/{chapterId}/comments/{commentId}/reactions - Danh sách người đã reaction (cho modal). */
export async function getChapterCommentReactions(chapterId, commentId) {
    const response = await axiosInstance.get(`/chapters/${chapterId}/comments/${commentId}/reactions`);
    return response.data;
}

/** POST api/chapters/{chapterId}/comments/{commentId}/reaction - Đặt/bỏ reaction (LIKE, DISLIKE, FUNNY, SAD, ANGRY, LOVE, WOW). reactionType = null/'' để bỏ. */
export async function setChapterCommentReaction(chapterId, commentId, reactionType) {
    const response = await axiosInstance.post(`/chapters/${chapterId}/comments/${commentId}/reaction`, {
        reactionType: reactionType ?? null,
    });
    return response.data;
}
