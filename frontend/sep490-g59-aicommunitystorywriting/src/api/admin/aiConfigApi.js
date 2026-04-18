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

export async function getAdminAiOpenRouterKeys() {
    const res = await axiosInstance.get("admin/ai-usage-stats/openrouter-keys");
    return res.data;
}

export async function getAdminAiOpenRouterActivity(params = {}) {
    const clean = Object.fromEntries(
        Object.entries(params).filter(([, v]) => v !== undefined && v !== null && String(v).trim() !== "")
    );
    const res = await axiosInstance.get("admin/ai-usage-stats/openrouter-activity", { params: clean });
    return res.data;
}

export async function getAdminAiOpenRouterCredits() {
    const res = await axiosInstance.get("admin/ai-usage-stats/openrouter-credits");
    return res.data;
}

export async function getAdminAiRequestLogs(params = {}) {
    const clean = Object.fromEntries(
        Object.entries(params).filter(([, v]) => v !== undefined && v !== null && String(v) !== "")
    );
    const res = await axiosInstance.get("admin/ai-usage-stats/requests", {
        params: clean,
        headers: {
            "Cache-Control": "no-cache",
            Pragma: "no-cache",
        },
    });
    return res.data;
}

export async function getAdminAiGenerationsDaily(params = {}) {
    const clean = Object.fromEntries(
        Object.entries(params).filter(([, v]) => v !== undefined && v !== null && String(v) !== "")
    );
    const res = await axiosInstance.get("admin/ai-usage-stats/generations-daily", {
        params: clean,
        headers: {
            "Cache-Control": "no-cache",
            Pragma: "no-cache",
        },
    });
    return res.data;
}

export async function getAdminAiOpenRouterGeneration(generationId) {
    if (!generationId) throw new Error("generationId là bắt buộc.");
    const res = await axiosInstance.get(`admin/ai-usage-stats/openrouter-generation/${generationId}`);
    return res.data;
}

export async function getAdminAuthorAiTokenBudget(userId) {
    if (!userId) throw new Error("userId là bắt buộc.");
    const res = await axiosInstance.get(`admin/users/${userId}/author-ai-token-budget`);
    return res.data;
}

export async function setAdminAuthorAiTokenBudget(userId, tokenLimit) {
    if (!userId) throw new Error("userId là bắt buộc.");
    const res = await axiosInstance.put(`admin/users/${userId}/author-ai-token-budget`, { tokenLimit });
    return res.data;
}

export async function getAdminAuthorAiTokenAutoGrantRules() {
    const res = await axiosInstance.get("admin/author-ai-token-auto-grants");
    return res.data;
}

export async function createAdminAuthorAiTokenAutoGrantRule(payload) {
    const res = await axiosInstance.post("admin/author-ai-token-auto-grants", payload ?? {});
    return res.data;
}

export async function updateAdminAuthorAiTokenAutoGrantRule(ruleId, payload) {
    if (!ruleId) throw new Error("ruleId là bắt buộc.");
    const res = await axiosInstance.put(`admin/author-ai-token-auto-grants/${ruleId}`, payload ?? {});
    return res.data;
}

export async function deleteAdminAuthorAiTokenAutoGrantRule(ruleId) {
    if (!ruleId) throw new Error("ruleId là bắt buộc.");
    const res = await axiosInstance.delete(`admin/author-ai-token-auto-grants/${ruleId}`);
    return res.data;
}

export async function runNowAdminAuthorAiTokenAutoGrantRule(ruleId) {
    if (!ruleId) throw new Error("ruleId là bắt buộc.");
    const res = await axiosInstance.post(`admin/author-ai-token-auto-grants/${ruleId}/run-now`);
    return res.data;
}

