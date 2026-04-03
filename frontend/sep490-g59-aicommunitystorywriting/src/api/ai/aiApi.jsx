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
 * @param {{ chapterOrderIndex?: number, chapterId?: string }} [options] - order_index/chapterId chương đang soạn
 * @returns {Promise<{ ideaContradictionFeedback?: string, outline: string, suggestedChapterTitle?: string, finalContent: string, approved: boolean, revisionCount: number, reviewFeedback?: string }>}
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
 * Xem giới hạn sử dụng AI của user hiện tại (số lần/24h).
 * @returns {Promise<{
 *   suggestNextChapter: { limitPerDay: number, usedInWindow: number, remaining: number, resetsAtUtc: string|null },
 *   coCreate: { limitPerDay: number, usedInWindow: number, remaining: number, resetsAtUtc: string|null },
 *   // legacy root (mirror suggest)
 *   limitPerDay: number, usedInWindow: number, remaining: number, resetsAtUtc: string|null
 * }>}
 */
export async function getAiUsageLimit() {
    const response = await axiosInstance.get("ai/usage-limit");
    const raw = response?.data ?? {};
    // DEBUG tạm: in payload usage-limit đúng 1 lần để đối chiếu BE runtime.
    if (typeof window !== "undefined" && !window.__aiUsageLimitLoggedOnce) {
        window.__aiUsageLimitLoggedOnce = true;
        console.log("[AI usage-limit raw payload]", raw);
    }
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

    return {
        suggestNextChapter: suggest,
        coCreate,
        coCreateAvailable: hasCoCreateObject,
        limitPerDay: suggest.limitPerDay,
        usedInWindow: suggest.usedInWindow,
        remaining: suggest.remaining,
        resetsAtUtc: suggest.resetsAtUtc,
    };
}
