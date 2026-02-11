import axiosInstance from "../axiosInstance";

/** Chuyển base64 dataURL sang File (dùng cho ảnh bìa từ form). */
function dataURLtoFile(dataUrl, filename = "cover.png") {
    if (!dataUrl || typeof dataUrl !== "string") return null;
    try {
        const arr = dataUrl.split(",");
        const mimeMatch = arr[0].match(/:(.*?);/);
        const mime = mimeMatch ? mimeMatch[1] : "image/png";
        const bstr = atob(arr[1]);
        let n = bstr.length;
        const u8arr = new Uint8Array(n);
        while (n--) u8arr[n] = bstr.charCodeAt(n);
        return new File([u8arr], filename, { type: mime });
    } catch {
        return null;
    }
}

/** Map độ tuổi UI -> API. */
const AGE_RATING_MAP = {
    "Phù hợp mọi lứa tuổi": "ALL",
    "Từ 13 tuổi": "13+",
    "Từ 16 tuổi": "16+",
    "Từ 18 tuổi": "18+",
};

/** Map trạng thái tiến độ UI -> API. */
const STORY_PROGRESS_MAP = {
    "Đang ra": "ONGOING",
    "Hoàn thành": "COMPLETED",
    "Tạm dừng": "HIATUS",
};

/**
 * Tạo truyện mới (multipart/form-data).
 * @param {Object} data - {
 *   title (required),
 *   summary?,
 *   categoryIds?: string[] (Guid),
 *   ageRating?: string (ALL, 13+, 16+, 18+),
 *   storyProgressStatus?: string (ONGOING, COMPLETED, HIATUS),
 *   authorId?: string (Guid - dev mode khi chưa có auth),
 *   coverImage?: File | string (base64 dataURL)
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

    const ageRating = AGE_RATING_MAP[data.ageRating] || data.ageRating || "ALL";
    const rawProgress = data.storyProgressStatus || data.status || "";
    const storyProgress =
        STORY_PROGRESS_MAP[rawProgress] ||
        (["ONGOING", "COMPLETED", "HIATUS"].includes(String(rawProgress).toUpperCase()) ? String(rawProgress).toUpperCase() : "ONGOING");
    formData.append("AgeRating", ageRating);
    formData.append("StoryProgressStatus", storyProgress);

    if (data.authorId) {
        formData.append("AuthorId", data.authorId);
    }

    let coverFile = data.coverImage;
    if (typeof coverFile === "string" && coverFile.startsWith("data:")) {
        coverFile = dataURLtoFile(coverFile, "cover.png");
    }
    if (coverFile instanceof File) {
        const allowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
        const ext = coverFile.name.toLowerCase().substring(coverFile.name.lastIndexOf("."));
        if (!allowedExtensions.includes(ext) && !coverFile.type?.startsWith("image/")) {
            throw new Error(`Ảnh bìa: chỉ chấp nhận ${allowedExtensions.join(", ").toUpperCase()}`);
        }
        if (coverFile.size > 5 * 1024 * 1024) {
            throw new Error("Kích thước ảnh bìa không được vượt quá 5MB");
        }
        formData.append("CoverImage", coverFile);
    }

    try {
        const response = await axiosInstance.post("/stories", formData, {
            headers: { "Content-Type": "multipart/form-data" },
        });
        return response.data;
    } catch (err) {
        const msg = err?.response?.data?.message ?? err?.response?.data?.error ?? err?.message;
        throw new Error(typeof msg === "string" ? msg : "Không thể tạo truyện. Vui lòng thử lại.");
    }
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
    formData.append("Status", (data.status || "DRAFT").toUpperCase());
    const ageRating = AGE_RATING_MAP[data.ageRating] || data.ageRating || "ALL";
    const rawProgress = data.storyProgressStatus || data.publishStatus || data.status || "";
    const storyProgress = STORY_PROGRESS_MAP[rawProgress] || (["ONGOING", "COMPLETED", "HIATUS"].includes(String(rawProgress).toUpperCase()) ? String(rawProgress).toUpperCase() : "ONGOING");
    formData.append("AgeRating", ageRating);
    formData.append("StoryProgressStatus", storyProgress);

    if (Array.isArray(data.categoryIds)) {
        const validGuids = data.categoryIds
            .map((cid) => (typeof cid === "string" ? cid : String(cid || "")))
            .filter((s) => /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/.test(s));
        validGuids.forEach((cid) => formData.append("CategoryIds", cid));
    }

    let coverFile = data.coverImage;
    if (typeof coverFile === "string" && coverFile.startsWith("data:")) {
        coverFile = dataURLtoFile(coverFile, "cover.png");
    }
    if (coverFile instanceof File) {
        const allowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp"];
        const ext = coverFile.name.toLowerCase().substring(coverFile.name.lastIndexOf("."));
        if (!allowedExtensions.includes(ext) && !coverFile.type?.startsWith("image/")) {
            throw new Error(`Ảnh bìa: chỉ chấp nhận ${allowedExtensions.join(", ").toUpperCase()}`);
        }
        if (coverFile.size > 5 * 1024 * 1024) {
            throw new Error("Kích thước ảnh bìa không được vượt quá 5MB");
        }
        formData.append("CoverImage", coverFile);
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
