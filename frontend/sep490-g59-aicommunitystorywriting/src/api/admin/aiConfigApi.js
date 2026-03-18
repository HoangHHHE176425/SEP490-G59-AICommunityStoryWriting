import axiosInstance from "../axiosInstance";

export async function getAdminAiUsageLimit() {
    const res = await axiosInstance.get("admin/ai-usage-limit");
    return res.data; // { maxRequestsPerDay }
}

export async function setAdminAiUsageLimit(maxRequestsPerDay) {
    const res = await axiosInstance.put("admin/ai-usage-limit", { maxRequestsPerDay });
    return res.data; // { maxRequestsPerDay, message }
}

export async function getAdminBannedWords(category = "BannedWord") {
    const res = await axiosInstance.get("admin/banned-words", {
        params: category ? { category } : undefined,
    });
    return res.data; // Array<{ id, word, category, createdAt }>
}

export async function addAdminBannedWord(word, category = "BannedWord") {
    const res = await axiosInstance.post("admin/banned-words", { word, category });
    return res.data; // BannedWordItemDto
}

export async function deleteAdminBannedWord(id) {
    await axiosInstance.delete(`admin/banned-words/${id}`);
}

