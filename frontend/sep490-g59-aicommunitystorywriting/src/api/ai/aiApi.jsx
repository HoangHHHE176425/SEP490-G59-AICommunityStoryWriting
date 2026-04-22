import axiosInstance from "../axiosInstance";

/** Chuỗi cảnh báo ngữ cảnh (chương trước nháp có nội dung) từ BE suggest-next-chapter / co-create. */
export function pickAiContextWarning(payload) {
    if (payload == null || typeof payload !== "object") return "";
    const w = payload.contextWarning ?? payload.ContextWarning;
    if (typeof w !== "string") return "";
    const t = w.trim();
    return t || "";
}

/**
 * Index RAG cho truyện (embedding các chương) để gợi ý chương chính xác hơn. Chỉ tác giả.
 * Gọi trước suggest-next-chapter nếu muốn dùng ngữ cảnh RAG; không bắt buộc (BE có fallback Story Context).
 * @param {string} storyId - ID truyện (Guid)
 */
export async function indexRag(storyId) {
    return indexRagWithOptions(storyId, { throwOnError: false });
}

/**
 * Index RAG cho truyện (embedding các chương) với tùy chọn kiểm soát lỗi.
 * @param {string} storyId - ID truyện (Guid)
 * @param {{ throwOnError?: boolean }} options
 */
export async function indexRagWithOptions(storyId, options = {}) {
    if (!storyId) return;
    const throwOnError = Boolean(options?.throwOnError);
    try {
        const response = await axiosInstance.post("ai/index-rag", { storyId });
        return response?.data ?? null;
    } catch (err) {
        if (throwOnError) throw err;
        // Legacy behavior: bỏ qua lỗi để luồng AI có thể fallback context nếu BE hỗ trợ.
        return null;
    }
}

/**
 * Lấy trạng thái RAG hiện tại của truyện.
 * @param {string} storyId - ID truyện (Guid)
 */
export async function getRagStatus(storyId) {
    if (!storyId) throw new Error("storyId là bắt buộc.");
    const response = await axiosInstance.get(`ai/rag-status?storyId=${encodeURIComponent(storyId)}`);
    return response?.data ?? null;
}

/**
 * Gợi ý 3 hướng đi cho chương tiếp theo (chỉ tác giả, check token từ BE).
 * @param {string} storyId - ID truyện (Guid)
 * @param {string|null} afterChapterId - ID chương sau đó muốn gợi ý; null = sau chương mới nhất
 * @param {string|null} prompt - Prompt tùy chọn do tác giả nhập
 * @param {string|null} chapterId - ID chương đang soạn (FE tạo trước); BE lưu gợi ý vào ai_generated_content
 * @returns {Promise<{ suggestions: Array<{ title, summary, direction }>, contextUsed?: { storyTitle?, chaptersIncluded } }>}
 */
export async function suggestNextChapter(storyId, afterChapterId = null, prompt = null, chapterId = null) {
    if (!storyId) {
        throw new Error("StoryId là bắt buộc.");
    }
    const trimmedPrompt = (prompt ?? "").toString().trim();
    const body = {
        storyId,
        afterChapterId: afterChapterId || null,
        chapterId: chapterId || null,
        // Hỗ trợ BE theo nhiều naming convention (BE có thể chọn 1 trong các field này)
        prompt: trimmedPrompt || null,
        authorPrompt: trimmedPrompt || null,
    };
    const response = await axiosInstance.post("ai/suggest-next-chapter", body);
    return response.data;
}

/**
 * Đồng sáng tác: ý tưởng tác giả → Agent 1 (dàn ý) → Agent 2 (nội dung) → Guardrail → Agent 3 (kiểm duyệt). Check token từ BE.
 * BE trả về JSON thường (không dùng SSE).
 * @param {string} storyId - ID truyện (Guid)
 * @param {string|null|undefined} authorIdea - Ý tưởng của tác giả (có thể null khi BE cho phép auto)
 * @param {{ chapterOrderIndex?: number, chapterId?: string }} [options] - order_index/chapterId chương đang soạn
 * @returns {Promise<{ outline: string, suggestedChapterTitle?: string, finalContent: string, approved: boolean, revisionCount: number, reviewFeedback?: string, contextWarning?: string }>}
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
    const rawChapterId = options?.chapterId ?? options?.ChapterId;
    if (rawChapterId != null && String(rawChapterId).trim() !== "") {
        payload.chapterId = String(rawChapterId).trim();
    }
    const response = await axiosInstance.post("ai/co-create", payload);
    return response.data;
}

/**
 * So sánh nội dung chương với các bản AI (cùng chapter_index = order_index của chương). Chỉ cần chapterId — BE tự lấy story/order_index.
 * @param {{ chapterId: string }} payload
 */
export async function compareChapter(payload) {
    const chapterId = payload?.chapterId ?? payload?.ChapterId;
    const content = (payload?.content ?? "").toString();
    if (!chapterId || String(chapterId).trim() === "") throw new Error("chapterId là bắt buộc.");
    const response = await axiosInstance.post("ai/compare-chapter", {
        chapterId,
        content,
    });
    return response.data;
}

/**
 * So sánh nội dung đang soạn với bản AI trước khi lưu (không ghi DB).
 * @param {{ storyId: string, orderIndex: number, content: string }} payload
 */
export async function compareChapterPreview(payload) {
    const chapterId = payload?.chapterId ?? payload?.ChapterId;
    const content = (payload?.content ?? "").toString();
    if (chapterId != null && String(chapterId).trim() !== "") {
        return compareChapter({ chapterId, content });
    }
    const storyId = payload?.storyId ?? payload?.StoryId;
    const orderIndex = payload?.orderIndex ?? payload?.OrderIndex;
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

const authHeader = () => {
    const token = localStorage.getItem("accessToken");
    return token ? { Authorization: `Bearer ${token}` } : undefined;
};

/**
 * Legacy: gộp chính tả + từ cấm (nếu BE còn route cũ).
 * @param {Object} payload - { content: string, storyId?: string|null, chapterTitle?: string|null }
 */
export async function checkChapter(payload) {
    const content = (payload?.content ?? '').toString();
    if (!content.trim()) throw new Error("Content là bắt buộc.");
    const body = {
        content,
        storyId: payload?.storyId ?? null,
        chapterTitle: payload?.chapterTitle ?? null,
    };
    const response = await axiosInstance.post("ai/check-chapter", body, {
        headers: authHeader(),
    });
    return response.data;
}

/**
 * Kiểm tra chính tả (POST /api/ai/check-chapter-spelling).
 * @param {Object} payload - { content: string, storyId?: string|null, chapterTitle?: string|null }
 */
export async function checkChapterSpelling(payload) {
    const content = (payload?.content ?? "").toString();
    if (!content.trim()) throw new Error("Content là bắt buộc.");
    const body = {
        content,
        storyId: payload?.storyId ?? null,
        chapterTitle: payload?.chapterTitle ?? null,
    };
    try {
        const response = await axiosInstance.post("ai/check-chapter-spelling", body, {
            headers: authHeader(),
        });
        return response.data;
    } catch (err) {
        if (err?.response?.status !== 404) throw err;
        return checkChapter(payload);
    }
}

/**
 * Kiểm tra từ cấm / chính sách (POST /api/ai/check-chapter-banned-words).
 * @param {Object} payload - { content: string, storyId?: string|null }
 */
export async function checkBannedWords(payload) {
    const content = (payload?.content ?? "").toString();
    if (!content.trim()) throw new Error("Content là bắt buộc.");
    const body = {
        content,
        storyId: payload?.storyId ?? null,
    };
    try {
        const response = await axiosInstance.post("ai/check-chapter-banned-words", body, {
            headers: authHeader(),
        });
        return response.data;
    } catch (err) {
        if (err?.response?.status !== 404) throw err;
    }
    try {
        const response = await axiosInstance.post("ai/check-banned-words", { ...body, chapterTitle: payload?.chapterTitle ?? null }, {
            headers: authHeader(),
        });
        return response.data;
    } catch (err2) {
        if (err2?.response?.status !== 404) throw err2;
        const legacy = await checkChapter({
            content,
            storyId: payload?.storyId ?? null,
            chapterTitle: payload?.chapterTitle ?? null,
        });
        const policyViolations = legacy?.policyViolations ?? legacy?.PolicyViolations ?? [];
        const hasInappropriateContent = Boolean(legacy?.hasInappropriateContent ?? legacy?.HasInappropriateContent);
        return {
            passed: Array.isArray(policyViolations) && policyViolations.length === 0 && !hasInappropriateContent,
            policyViolations,
            hasInappropriateContent,
            summary: legacy?.summary ?? legacy?.Summary ?? null,
        };
    }
}

/**
 * Xem giới hạn sử dụng AI của user hiện tại.
 * Hỗ trợ cả payload cũ (rate-limit theo ngày) và payload mới (token budget author).
 */
export async function getAiUsageLimit() {
    const response = await axiosInstance.get("ai/usage-limit");
    const raw = response?.data ?? {};
    const payload = raw?.data ?? raw?.Data ?? raw;
    const suggestRaw =
        payload?.suggestNextChapter ??
        payload?.SuggestNextChapter ??
        payload?.suggest_next_chapter ??
        payload?.suggest ??
        {};
    const coCreateRaw =
        payload?.coCreate ??
        payload?.CoCreate ??
        payload?.co_create ??
        payload?.coCreateLimit ??
        payload?.coCreateUsage ??
        {};

    const pickNum = (obj, keys, fallback = 0) => {
        for (const k of keys) {
            const v = obj?.[k];
            if (v != null && Number.isFinite(Number(v))) return Number(v);
        }
        return Number.isFinite(Number(fallback)) ? Number(fallback) : 0;
    };
    const pickVal = (obj, keys, fallback = null) => {
        for (const k of keys) {
            const v = obj?.[k];
            if (v != null) return v;
        }
        return fallback;
    };

    // Legacy root vẫn dùng cho suggest.
    const rootLimit = pickNum(payload, ["limitPerDay", "LimitPerDay", "limit_per_day"], 0);
    const rootUsed = pickNum(payload, ["usedInWindow", "UsedInWindow", "used_in_window"], 0);
    const rootRemaining = pickNum(payload, ["remaining", "Remaining"], 0);
    const rootResets = pickVal(payload, ["resetsAtUtc", "ResetsAtUtc", "resets_at_utc"], null);

    const suggest = {
        limitPerDay: pickNum(suggestRaw, ["limitPerDay", "LimitPerDay", "limit_per_day"], rootLimit),
        usedInWindow: pickNum(suggestRaw, ["usedInWindow", "UsedInWindow", "used_in_window"], rootUsed),
        remaining: pickNum(suggestRaw, ["remaining", "Remaining"], rootRemaining),
        resetsAtUtc: pickVal(suggestRaw, ["resetsAtUtc", "ResetsAtUtc", "resets_at_utc"], rootResets),
    };

    // Co-create: chỉ dùng object riêng. Nếu BE runtime chưa trả object này thì đánh dấu unavailable.
    const hasCoCreateObject = Object.keys(coCreateRaw || {}).length > 0;
    const coCreate = {
        limitPerDay: hasCoCreateObject ? pickNum(coCreateRaw, ["limitPerDay", "LimitPerDay", "limit_per_day"], 0) : null,
        usedInWindow: hasCoCreateObject ? pickNum(coCreateRaw, ["usedInWindow", "UsedInWindow", "used_in_window"], 0) : null,
        remaining: hasCoCreateObject ? pickNum(coCreateRaw, ["remaining", "Remaining"], 0) : null,
        resetsAtUtc: hasCoCreateObject ? pickVal(coCreateRaw, ["resetsAtUtc", "ResetsAtUtc", "resets_at_utc"], null) : null,
    };

    const tokenBudgetRaw =
        payload?.authorTokenBudget ??
        payload?.AuthorTokenBudget ??
        null;
    const tokenBudgetBlocked = Boolean(
        payload?.authorTokenBudgetBlocked ??
        payload?.AuthorTokenBudgetBlocked ??
        false
    );

    const tokenLimit = pickVal(tokenBudgetRaw, ["tokenLimit", "TokenLimit"], null);
    const grantAmountRaw =
        pickVal(
            tokenBudgetRaw,
            ["grantAmount", "GrantAmount", "monthlyGrantAmount", "MonthlyGrantAmount"],
            null
        ) ??
        pickVal(
            payload,
            ["grantAmount", "GrantAmount", "monthlyGrantAmount", "MonthlyGrantAmount"],
            null
        );
    const grantAmount = grantAmountRaw != null && Number.isFinite(Number(grantAmountRaw))
        ? Number(grantAmountRaw)
        : null;
    const tokensUsed = pickNum(tokenBudgetRaw, ["tokensUsed", "TokensUsed"], 0);
    const tokensRemainingLifetimeRaw = pickVal(
        tokenBudgetRaw,
        ["tokensRemainingLifetime", "TokensRemainingLifetime", "tokensRemaining", "TokensRemaining"],
        null
    );
    const unlimitedLifetime = Boolean(
        pickVal(tokenBudgetRaw, ["unlimitedLifetime", "UnlimitedLifetime"], tokenLimit == null)
    );
    const tokensRemainingLifetime = unlimitedLifetime
        ? null
        : (tokensRemainingLifetimeRaw != null && Number.isFinite(Number(tokensRemainingLifetimeRaw))
            ? Number(tokensRemainingLifetimeRaw)
            : (tokenLimit != null && Number.isFinite(Number(tokenLimit))
                ? Math.max(0, Number(tokenLimit) - Number(tokensUsed))
                : null));

    return {
        suggestNextChapter: suggest,
        coCreate,
        coCreateAvailable: hasCoCreateObject,
        limitPerDay: suggest.limitPerDay,
        usedInWindow: suggest.usedInWindow,
        remaining: suggest.remaining,
        resetsAtUtc: suggest.resetsAtUtc,
        authorTokenBudget: tokenBudgetRaw
            ? {
                tokenLimit: tokenLimit != null && Number.isFinite(Number(tokenLimit)) ? Number(tokenLimit) : null,
                grantAmount,
                tokensUsed,
                tokensRemainingLifetime,
                unlimitedLifetime,
            }
            : null,
        authorTokenBudgetBlocked: tokenBudgetBlocked,
    };
}
