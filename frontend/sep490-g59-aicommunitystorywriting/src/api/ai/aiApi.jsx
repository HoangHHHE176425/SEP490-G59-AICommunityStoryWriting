import axiosInstance from "../axiosInstance";

/**
 * Index RAG cho truyện (embedding các chương) để gợi ý chương chính xác hơn. Chỉ tác giả.
 * Gọi trước suggest-next-chapter nếu muốn dùng ngữ cảnh RAG; không bắt buộc (BE có fallback Story Context).
 * @param {string} storyId - ID truyện (Guid)
 */
export async function indexRag(storyId) {
    if (!storyId) return;
    try {
        await axiosInstance.post("ai/index-rag", { storyId });
    } catch {
        // Bỏ qua lỗi (429, 500): suggest-next-chapter vẫn chạy với Story Context
    }
}

/**
 * Gợi ý 3 hướng đi cho chương tiếp theo (chỉ tác giả, có rate limit).
 * @param {string} storyId - ID truyện (Guid)
 * @param {string|null} afterChapterId - ID chương sau đó muốn gợi ý; null = sau chương mới nhất
 * @param {string|null} prompt - Prompt tùy chọn do tác giả nhập
 * @returns {Promise<{ suggestions: Array<{ title, summary, direction }>, contextUsed?: { storyTitle?, chaptersIncluded } }>}
 */
export async function suggestNextChapter(storyId, afterChapterId = null, prompt = null) {
    if (!storyId) {
        throw new Error("StoryId là bắt buộc.");
    }
    const trimmedPrompt = (prompt ?? "").toString().trim();
    const body = {
        storyId,
        afterChapterId: afterChapterId || null,
        // Hỗ trợ BE theo nhiều naming convention (BE có thể chọn 1 trong các field này)
        prompt: trimmedPrompt || null,
        authorPrompt: trimmedPrompt || null,
    };
    const response = await axiosInstance.post("ai/suggest-next-chapter", body);
    return response.data;
}

/**
 * Parse body Server-Sent Events từ POST /ai/co-create (BE trả text/event-stream: progress → result | error).
 */
function parseCoCreateSseResponse(rawText) {
    if (rawText == null || typeof rawText !== "string") {
        throw new Error("Phản hồi AI không hợp lệ (rỗng).");
    }
    const blocks = rawText
        .split(/\n\n+/)
        .map((b) => b.trim())
        .filter(Boolean);
    let lastError = null;
    let lastResult = null;
    for (const block of blocks) {
        let eventType = "message";
        const dataLines = [];
        for (const line of block.split("\n")) {
            if (line.startsWith("event:")) {
                eventType = line.slice(6).trim();
            } else if (line.startsWith("data:")) {
                dataLines.push(line.slice(5).trim());
            }
        }
        const dataStr = dataLines.join("");
        if (!dataStr) continue;
        try {
            const data = JSON.parse(dataStr);
            if (eventType === "error") lastError = data;
            if (eventType === "result") lastResult = data;
        } catch {
            /* bỏ qua chunk không parse được */
        }
    }
    if (lastError) {
        const msg = lastError.message ?? lastError.Message ?? "Lỗi đồng sáng tác với AI.";
        const err = new Error(typeof msg === "string" ? msg : JSON.stringify(lastError));
        err.response = { status: 400, data: lastError };
        throw err;
    }
    if (!lastResult) {
        throw new Error(
            "Không nhận được kết quả từ AI (thiếu sự kiện result). Kiểm tra cấu hình AI hoặc thử lại sau."
        );
    }
    return lastResult;
}

/**
 * Đồng sáng tác: ý tưởng tác giả → Agent 1 (dàn ý) → Agent 2 (nội dung) → Guardrail → Agent 3 (kiểm duyệt). Có rate limit.
 * BE trả về SSE (không phải JSON thuần) — axios phải đọc text rồi parse sự kiện `result`.
 * @param {string} storyId - ID truyện (Guid)
 * @param {string|null|undefined} authorIdea - Ý tưởng của tác giả (có thể null khi BE cho phép auto)
 * @param {{ chapterOrderIndex?: number }} [options] - order_index chương đang soạn (0-based), để lưu ai_generated_content đúng slot và so % khi copy–paste
 * @returns {Promise<{ ideaContradictionFeedback?: string, outline: string, finalContent: string, approved: boolean, revisionCount: number, reviewFeedback?: string }>}
 */
export async function coCreate(storyId, authorIdea, options = {}) {
    if (!storyId) throw new Error("StoryId là bắt buộc.");
    const trimmed = (authorIdea || "").trim();
    const rawIdx = options?.chapterOrderIndex ?? options?.ChapterOrderIndex;
    const payload = {
        storyId,
        authorIdea: trimmed || null,
        saveAsDraft: false,
    };
    if (rawIdx !== undefined && rawIdx !== null && Number.isFinite(Number(rawIdx))) {
        payload.chapterOrderIndex = Math.max(0, Math.floor(Number(rawIdx)));
    }
    const response = await axiosInstance.post(
        "ai/co-create",
        payload,
        {
            responseType: "text",
            validateStatus: (status) => status < 600,
        }
    );

    const body = response.data;
    const tryJsonMessage = () => {
        try {
            const j = typeof body === "string" ? JSON.parse(body) : body;
            return j?.message ?? j?.Message ?? null;
        } catch {
            return null;
        }
    };

    if (response.status === 401) {
        const msg = tryJsonMessage() || "Không xác định được người dùng. Vui lòng đăng nhập lại.";
        const err = new Error(msg);
        err.response = { status: 401, data: { message: msg } };
        throw err;
    }
    if (response.status === 429) {
        const msg = tryJsonMessage() || "Bạn đã đạt giới hạn sử dụng AI trong ngày.";
        const err = new Error(msg);
        err.response = { status: 429, data: { message: msg } };
        throw err;
    }
    if (response.status === 400) {
        const msg = tryJsonMessage() || "Yêu cầu không hợp lệ.";
        const err = new Error(msg);
        err.response = { status: 400, data: { message: msg } };
        throw err;
    }
    if (response.status >= 400) {
        const msg =
            tryJsonMessage() ||
            (typeof body === "string" && body.length < 500 ? body : `Lỗi ${response.status}`);
        const err = new Error(msg);
        err.response = { status: response.status, data: { message: msg } };
        throw err;
    }

    return parseCoCreateSseResponse(typeof body === "string" ? body : String(body ?? ""));
}

/**
 * So sánh nội dung chương với các bản AI (cùng chapter_index = order_index của chương). Chỉ cần chapterId — BE tự lấy story/order_index.
 * @param {{ chapterId: string }} payload
 */
export async function compareChapter(payload) {
    const chapterId = payload?.chapterId ?? payload?.ChapterId;
    if (!chapterId || String(chapterId).trim() === "") throw new Error("chapterId là bắt buộc.");
    const response = await axiosInstance.post("ai/compare-chapter", {
        chapterId,
    });
    return response.data;
}

/**
 * So sánh nội dung đang soạn với bản AI trước khi lưu (không ghi DB).
 * @param {{ storyId: string, orderIndex: number, content: string }} payload
 */
export async function compareChapterPreview(payload) {
    const storyId = payload?.storyId ?? payload?.StoryId;
    const orderIndex = payload?.orderIndex ?? payload?.OrderIndex;
    const content = (payload?.content ?? "").toString();
    if (!storyId || String(storyId).trim() === "") throw new Error("storyId là bắt buộc.");
    if (orderIndex == null || Number.isNaN(Number(orderIndex))) throw new Error("orderIndex là bắt buộc.");
    try {
        const response = await axiosInstance.post("ai/compare-chapter-preview", {
            storyId,
            orderIndex: Number(orderIndex),
            content,
        });
        return response.data;
    } catch (err) {
        const status = err?.response?.status;
        if (status === 404) {
            const hint =
                "Không tìm thấy API so sánh (404). Hãy build và khởi động lại backend AIStory.API bản mới (có route POST /api/ai/compare-chapter-preview). Kiểm tra VITE_API_URL trỏ đúng cổng API (vd. http://localhost:5000/api).";
            const wrapped = new Error(hint);
            wrapped.response = err.response;
            wrapped.cause = err;
            throw wrapped;
        }
        throw err;
    }
}

/**
 * Check chapter content: chính tả + từ cấm/chính sách.
 * @param {Object} payload - { content: string, storyId?: string|null, chapterTitle?: string|null }
 * @returns {Promise<{ passed: boolean, spellingIssues: Array<{ wordOrPhrase, suggestion, context? }>, policyViolations: Array<{ type, description, quote? }>, hasInappropriateContent: boolean, summary?: string|null }>}
 */

export async function checkChapter(payload) {
    const content = (payload?.content ?? '').toString();
    if (!content.trim()) throw new Error("Content là bắt buộc.");
    const body = {
        content,
        storyId: payload?.storyId ?? null,
        chapterTitle: payload?.chapterTitle ?? null,
    };
    const token = localStorage.getItem("accessToken");
    const response = await axiosInstance.post("ai/check-chapter", body, {
        headers: token ? { Authorization: `Bearer ${token}` } : undefined,
    });
    return response.data;
}

/**
 * Xem giới hạn sử dụng AI của user hiện tại (số lần/24h).
 * @returns {Promise<{ limitPerDay: number, usedInWindow: number, remaining: number, resetsAtUtc: string }>}
 */
export async function getAiUsageLimit() {
    const response = await axiosInstance.get("ai/usage-limit");
    return response.data;
}
