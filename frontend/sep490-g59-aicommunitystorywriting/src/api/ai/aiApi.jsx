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
 * Đồng sáng tác: ý tưởng tác giả → Agent 1 (dàn ý) → Agent 2 (nội dung) → Guardrail → Agent 3 (kiểm duyệt). Có rate limit.
 * @param {string} storyId - ID truyện (Guid)
 * @param {string} authorIdea - Ý tưởng của tác giả (1–2 câu hoặc đoạn ngắn)
 * @returns {Promise<{ ideaContradictionFeedback?: string, outline: string, finalContent: string, approved: boolean, revisionCount: number, reviewFeedback?: string }>}
 */
export async function coCreate(storyId, authorIdea) {
    if (!storyId) throw new Error("StoryId là bắt buộc.");
    const trimmed = (authorIdea || "").trim();
    // BE hỗ trợ 2 luồng: có nhập định hướng và không nhập định hướng.
    const response = await axiosInstance.post("ai/co-create", {
        storyId,
        authorIdea: trimmed || null,
        saveAsDraft: false,
    });
    return response.data;
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
