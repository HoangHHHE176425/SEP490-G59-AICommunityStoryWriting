import axiosInstance from "../axiosInstance";

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
