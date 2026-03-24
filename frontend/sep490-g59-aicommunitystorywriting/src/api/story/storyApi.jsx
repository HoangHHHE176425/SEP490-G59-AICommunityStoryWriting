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
 * @param {Object} params - { page?, pageSize?, search?, categoryId?, categoryIds?, authorId?, status?, sortBy?, sortOrder?, includeStoryIds?, storyProgressStatus?, ageRating?, minTotalChapters?, maxTotalChapters? }
 * @returns {Promise} - PagedResultDto
 */
export async function getStories(params = {}) {
    const q = new URLSearchParams();
    if (params.page != null) q.append("page", params.page);
    if (params.pageSize != null) q.append("pageSize", params.pageSize);
    if (params.search) q.append("search", params.search);
    if (params.categoryId) q.append("categoryId", params.categoryId);
    const catIds = params.categoryIds;
    if (Array.isArray(catIds) && catIds.length > 0) {
        catIds.forEach((id) => {
            if (id != null && String(id).trim() !== "") q.append("categoryIds", String(id).trim());
        });
    }
    if (params.authorId) q.append("authorId", params.authorId);
    if (params.status) q.append("status", params.status);
    if (params.sortBy) q.append("sortBy", params.sortBy);
    if (params.sortOrder) q.append("sortOrder", params.sortOrder);
    if (params.storyProgressStatus) q.append("storyProgressStatus", String(params.storyProgressStatus).trim());
    if (params.ageRating) q.append("ageRating", String(params.ageRating).trim());
    if (params.minTotalChapters != null) q.append("minTotalChapters", String(params.minTotalChapters));
    if (params.maxTotalChapters != null) q.append("maxTotalChapters", String(params.maxTotalChapters));
    const inc = params.includeStoryIds;
    if (Array.isArray(inc) && inc.length > 0) {
        inc.forEach((id) => {
            if (id != null && String(id).trim() !== "") q.append("includeStoryIds", String(id).trim());
        });
    }

    const url = q.toString() ? `/stories?${q}` : "/stories";
    const response = await axiosInstance.get(url);
    return response.data;
}

/**
 * Lấy truyện theo ID.
 * @param {string} id - Guid
 * @param {Object} [options] - { recordView?: boolean } mặc định true. Khi false chỉ lấy dữ liệu, không ghi nhận lượt xem (BE vẫn chống spam 1/viewer/24h khi recordView=true).
 * @returns {Promise}
 */
export async function getStoryById(id, options = {}) {
    const recordView = options.recordView !== false;
    const url = recordView ? `/stories/${id}` : `/stories/${id}?recordView=false`;
    const response = await axiosInstance.get(url);
    return response.data;
}

const STORY_VIEW_CACHE_KEY = "story_view";
const STORY_VIEW_COOLDOWN_MS = 24 * 60 * 60 * 1000; // 24h

/**
 * Lấy viewer key cho cache lượt xem (FE): user id nếu đăng nhập, 'anon' nếu không.
 * @param {string|null} userId - từ useAuth().user?.id
 * @returns {string}
 */
export function getViewerKeyForViewCache(userId) {
    return userId ? `u:${userId}` : "anon";
}

/**
 * Kiểm tra đã ghi nhận lượt xem cho story trong 24h (cache FE).
 * @param {string} storyId - Guid
 * @param {string} viewerKey - từ getViewerKeyForViewCache(user?.id)
 * @returns {boolean} true nếu đã xem trong 24h
 */
export function hasViewedStoryInCooldown(storyId, viewerKey) {
    try {
        const raw = localStorage.getItem(STORY_VIEW_CACHE_KEY);
        if (!raw) return false;
        const data = JSON.parse(raw);
        const key = `${storyId}_${viewerKey}`;
        const ts = data[key];
        if (ts == null) return false;
        return Date.now() - ts < STORY_VIEW_COOLDOWN_MS;
    } catch {
        return false;
    }
}

/**
 * Đánh dấu đã ghi nhận lượt xem cho story (cache FE 24h).
 * @param {string} storyId - Guid
 * @param {string} viewerKey - từ getViewerKeyForViewCache(user?.id)
 */
export function setStoryViewCache(storyId, viewerKey) {
    try {
        const raw = localStorage.getItem(STORY_VIEW_CACHE_KEY);
        const data = raw ? JSON.parse(raw) : {};
        data[`${storyId}_${viewerKey}`] = Date.now();
        localStorage.setItem(STORY_VIEW_CACHE_KEY, JSON.stringify(data));
    } catch {
        // ignore
    }
}

/**
 * Gọi API chỉ ghi nhận 1 lượt xem (BE chống spam: 1 lượt/viewer/24h). Nên gọi khi FE cache báo chưa xem trong 24h.
 * @param {string} storyId - Guid
 * @returns {Promise<void>}
 */
export async function recordStoryView(storyId) {
    await axiosInstance.post(`/stories/${storyId}/record-view`);
}

/**
 * Lưu tiến độ đọc: đang đọc đến chapter nào. Cần đăng nhập.
 * POST api/stories/{id}/reading-progress, body { chapterId: "guid" }.
 * @param {string} storyId - Guid truyện
 * @param {string} chapterId - Guid chương
 * @returns {Promise<void>}
 */
export async function saveReadingProgress(storyId, chapterId) {
    await axiosInstance.post(`/stories/${storyId}/reading-progress`, { chapterId });
}

/**
 * Lấy lý do từ chối truyện (cho tác giả). GET /stories/{id}/rejection-reason.
 * @param {string} id - Guid truyện
 * @returns {Promise<{ reason: string|null, rejectedAt: string|null }>}
 */
export async function getStoryRejectionReason(id) {
    const response = await axiosInstance.get(`/stories/${id}/rejection-reason`);
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

/**
 * Theo dõi truyện (chỉ story PUBLISHED). Khi có chương mới sẽ nhận thông báo. [Authorize]
 * @param {string} storyId - Guid
 * @returns {Promise<{ following: boolean, message: string }>}
 */
export async function followStory(storyId) {
    const response = await axiosInstance.post(`/stories/${storyId}/follow`);
    return response.data;
}

/**
 * Bỏ theo dõi truyện. [Authorize]
 * @param {string} storyId - Guid
 * @returns {Promise<{ following: boolean, message: string }>}
 */
export async function unfollowStory(storyId) {
    const response = await axiosInstance.delete(`/stories/${storyId}/follow`);
    return response.data;
}

/**
 * Duyệt truyện (phê duyệt / approve) – gọi POST /stories/{id}/publish, chuyển status sang PUBLISHED.
 * @param {string} id - Guid truyện
 * @returns {Promise}
 */
export async function approveStory(id) {
    const response = await axiosInstance.post(`/stories/${id}/publish`);
    return response.data;
}

/**
 * Từ chối duyệt truyện – cập nhật status sang REJECTED qua PUT /stories/{id}.
 * Cần truyền đủ dữ liệu truyện (title, summary, categoryIds, ageRating, storyProgressStatus) theo format updateStory.
 * @param {string} id - Guid truyện
 * @param {Object} storyData - { title, summary?, categoryIds?, ageRating?, storyProgressStatus? }
 * @returns {Promise}
 */
export async function rejectStory(id, storyData) {
    return updateStory(id, {
        title: storyData.title ?? storyData.Title ?? 'Untitled',
        summary: storyData.summary ?? storyData.Summary ?? '',
        categoryIds: storyData.categoryIds ?? storyData.CategoryIds ?? [],
        ageRating: storyData.ageRating ?? storyData.AgeRating ?? 'ALL',
        storyProgressStatus: storyData.storyProgressStatus ?? storyData.StoryProgressStatus ?? 'ONGOING',
        status: 'REJECTED',
    });
}

/**
 * Đánh giá truyện (1–5 sao). Bắt buộc đăng nhập. BE chặn nếu chưa đọc (chưa có log đọc chapter/story).
 * @param {string} storyId - Guid truyện
 * @param {Object} payload - { starValue: number (1..5), reviewText?: string }
 * @returns {Promise<{ avgRating: number, ratingCount: number }>}
 * @throws Nếu 400: message thường là "Bạn cần đọc truyện trước khi đánh giá."
 */
export async function rateStory(storyId, payload) {
    const starValue = Number(payload.starValue);
    if (starValue < 1 || starValue > 5) {
        throw new Error('Số sao phải từ 1 đến 5.');
    }
    const response = await axiosInstance.post(`/stories/${storyId}/ratings`, {
        starValue,
        reviewText: payload.reviewText != null ? String(payload.reviewText).trim() || null : null,
    });
    return response.data;
}

/**
 * Lấy lịch sử đánh giá của story (AllowAnonymous).
 * @param {string} storyId - Guid
 * @returns {Promise<Array<{ id, userId?, userDisplayName, starValue, reviewText?, createdAt? }>>}
 */
export async function getStoryRatings(storyId) {
    const response = await axiosInstance.get(`/stories/${storyId}/ratings`);
    return Array.isArray(response.data) ? response.data : [];
}

// --- Comments (GET/POST /api/stories/{id}/comments, POST like) ---

/**
 * Lấy danh sách comment của story (AllowAnonymous). Có đăng nhập thì mỗi comment có userHasLiked.
 * @param {string} storyId - Guid
 * @returns {Promise<Array>} StoryCommentDto[]
 */
export async function getStoryComments(storyId) {
    const response = await axiosInstance.get(`/stories/${storyId}/comments`);
    return Array.isArray(response.data) ? response.data : [];
}

/**
 * Tạo comment hoặc reply (parentId). Bắt buộc login + đã đọc ít nhất 1 chapter.
 * @param {string} storyId - Guid
 * @param {Object} payload - { content: string, parentId?: string (Guid) }
 * @returns {Promise<object>} Created comment DTO
 */
export async function addStoryComment(storyId, payload) {
    const content = (payload.content ?? '').trim();
    if (!content) throw new Error('Nội dung comment không được để trống.');
    const body = { content };
    if (payload.parentId) body.parentId = payload.parentId;
    const response = await axiosInstance.post(`/stories/${storyId}/comments`, body);
    return response.data;
}

/**
 * Bật/tắt like comment. 1 user chỉ 1 lần/comment. [Authorize]
 * @param {string} storyId - Guid
 * @param {string} commentId - Guid
 * @returns {Promise<{ liked: boolean, likesCount: number }>}
 */
export async function toggleCommentLike(storyId, commentId) {
    const response = await axiosInstance.post(`/stories/${storyId}/comments/${commentId}/like`);
    return response.data;
}
