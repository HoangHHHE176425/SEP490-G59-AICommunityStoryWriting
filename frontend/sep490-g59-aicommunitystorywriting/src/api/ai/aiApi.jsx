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
 * @returns {Promise<{ suggestions: Array<{ title, summary, direction }>, contextUsed?: { storyTitle?, chaptersIncluded } }>}
 */
export async function suggestNextChapter(storyId, afterChapterId = null) {
    if (!storyId) {
        throw new Error("StoryId là bắt buộc.");
    }
    const body = {
        storyId,
        afterChapterId: afterChapterId || null,
    };
    const response = await axiosInstance.post("ai/suggest-next-chapter", body);
    return response.data;
}
