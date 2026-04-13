/**
 * Logic SLA duyệt dùng chung (FE): mốc thời gian (pendingSince từ BE) + chính sách +7 ngày, badge theo TimeStatus.
 * BE: ModeratorReviewSlaHelper — OnTime / Warning / Critical / Overdue.
 */

export const POLICY_REVIEW_DAYS = 7;

/** Đồng bộ với ReviewEscalationService.ValidateNewDeadline (BE). */
export const MIN_HOURS_UNTIL_NEW_DEADLINE = 24;
export const MAX_DEADLINE_DAYS_AHEAD = 366;

/**
 * Kiểm tra hạn đề xuất khi moderator gửi đơn EXTEND_DEADLINE (trước khi gọi API).
 * @param {string} proposedIsoUtc - ISO UTC từ localDateTimeInputToIsoUtc
 * @param {string|null|undefined} currentReviewDeadlineIso - ReviewDeadlineAt từ GET review-assignment/self (ISO)
 * @returns {{ ok: true } | { ok: false, message: string }}
 */
export function validateModeratorExtendProposedDeadline(proposedIsoUtc, currentReviewDeadlineIso) {
    const proposedMs = new Date(proposedIsoUtc).getTime();
    if (!Number.isFinite(proposedMs)) {
        return { ok: false, message: 'Ngày giờ đề xuất không hợp lệ.' };
    }
    const nowMs = Date.now();
    const minMs = nowMs + MIN_HOURS_UNTIL_NEW_DEADLINE * 3600000;
    if (proposedMs <= minMs) {
        return {
            ok: false,
            message: 'Hạn đề xuất phải sau ít nhất 24 giờ kể từ hiện tại (theo quy định hệ thống).',
        };
    }
    const maxMs = nowMs + MAX_DEADLINE_DAYS_AHEAD * 86400000;
    if (proposedMs > maxMs) {
        return { ok: false, message: `Hạn không được vượt quá ${MAX_DEADLINE_DAYS_AHEAD} ngày so với hiện tại.` };
    }
    if (currentReviewDeadlineIso != null && String(currentReviewDeadlineIso).trim() !== '') {
        const curMs = new Date(currentReviewDeadlineIso).getTime();
        if (Number.isFinite(curMs) && proposedMs <= curMs) {
            const fmt = new Date(curMs).toLocaleString('vi-VN', {
                day: '2-digit',
                month: '2-digit',
                year: 'numeric',
                hour: '2-digit',
                minute: '2-digit',
            });
            return {
                ok: false,
                message: `Gia hạn nghĩa là hạn mới phải muộn hơn hạn duyệt hiện tại của bạn (${fmt}). Bạn đang chọn một mốc sớm hơn hoặc trùng hạn hiện tại.`,
            };
        }
    }
    return { ok: true };
}

/** Gia hạn duyệt: chỉ +3 / +5 / +7 ngày so với hạn hiện tại (khớp ReviewEscalationService). */
export const EXTEND_DEADLINE_DAY_CHOICES = Object.freeze([3, 5, 7]);

/**
 * Cộng N ngày vào mốc deadline UTC (khớp DateTime.Utc.AddDays với N nguyên).
 * @param {string|null|undefined} currentIsoUtc
 * @param {number} days
 * @returns {string|null}
 */
export function addUtcDaysToDeadlineIso(currentIsoUtc, days) {
    if (currentIsoUtc == null || String(currentIsoUtc).trim() === '') return null;
    const cur = new Date(currentIsoUtc);
    if (!Number.isFinite(cur.getTime())) return null;
    const n = Number(days);
    if (!Number.isFinite(n)) return null;
    return new Date(cur.getTime() + n * 86400000).toISOString();
}

/**
 * @param {string} proposedIsoUtc
 * @param {string|null|undefined} currentReviewDeadlineIso
 * @param {number} days
 */
export function validateModeratorExtendPresetDays(proposedIsoUtc, currentReviewDeadlineIso, days) {
    if (!EXTEND_DEADLINE_DAY_CHOICES.includes(Number(days))) {
        return { ok: false, message: 'Chọn thời gian gia hạn 3, 5 hoặc 7 ngày.' };
    }
    const expected = addUtcDaysToDeadlineIso(currentReviewDeadlineIso, days);
    if (!expected) {
        return { ok: false, message: 'Không tải được hạn duyệt hiện tại — không thể tính gia hạn.' };
    }
    const base = validateModeratorExtendProposedDeadline(proposedIsoUtc, currentReviewDeadlineIso);
    if (!base.ok) return base;
    const proposedMs = new Date(proposedIsoUtc).getTime();
    const expectedMs = new Date(expected).getTime();
    const tolMs = 3600000;
    if (Math.abs(proposedMs - expectedMs) > tolMs) {
        return { ok: false, message: 'Hạn đề xuất không khớp lựa chọn gia hạn.' };
    }
    return { ok: true };
}

const TIME_STATUS_RANK = {
    ontime: 0,
    warning: 1,
    critical: 2,
    overdue: 3,
};

/** Chuẩn hóa chuỗi TimeStatus (Pascal/camel). */
export function normalizeTimeStatus(raw) {
    if (raw == null || raw === '') return null;
    const s = String(raw).trim();
    if (!s) return null;
    const lower = s.toLowerCase();
    if (lower === 'ontime') return 'OnTime';
    if (lower === 'warning') return 'Warning';
    if (lower === 'critical') return 'Critical';
    if (lower === 'overdue') return 'Overdue';
    return s;
}

/** Chọn mức nghiêm trọng nhất trong danh sách TimeStatus. */
export function worstTimeStatus(statuses) {
    const list = (statuses || []).map(normalizeTimeStatus).filter(Boolean);
    if (list.length === 0) return null;
    return list.reduce((best, cur) => {
        const kb = TIME_STATUS_RANK[String(best).toLowerCase()] ?? 0;
        const kc = TIME_STATUS_RANK[String(cur).toLowerCase()] ?? 0;
        return kc > kb ? cur : best;
    }, list[0]);
}

/** Mốc kết thúc chính sách (+7 ngày từ mốc moderator nhận duyệt). */
export function policySuggestedEndUtc(claimedAtIso) {
    if (!claimedAtIso) return null;
    const t = new Date(claimedAtIso).getTime();
    if (!Number.isFinite(t)) return null;
    return new Date(t + POLICY_REVIEW_DAYS * 86400000);
}

const MS_PER_DAY = 86400000;

/**
 * Lấy chuỗi deadline từ item API (camelCase / PascalCase / snake_case).
 * @param {Record<string, unknown>|null|undefined} obj
 * @returns {string|null}
 */
export function pickReviewDeadlineIso(obj) {
    if (obj == null || typeof obj !== 'object') return null;
    const v = obj.deadlineAt ?? obj.DeadlineAt ?? obj.deadline_at;
    if (v == null || v === '') return null;
    const s = typeof v === 'string' ? v.trim() : v instanceof Date ? v.toISOString() : String(v);
    const t = new Date(s).getTime();
    return Number.isFinite(t) ? s : null;
}

/**
 * Đếm ngược tới mốc deadline (ISO UTC, khớp review_deadline_at từ BE).
 * @param {string|null|undefined} deadlineIsoUtc
 * @param {Date} [now]
 * @returns {string|null} Ví dụ "5 ngày 3 giờ 12 phút", "3 giờ 12 phút", "45 phút", "quá 1 ngày 2 giờ 5 phút".
 */
export function formatRemainingUntilUtcDeadline(deadlineIsoUtc, now = new Date()) {
    if (deadlineIsoUtc == null || String(deadlineIsoUtc).trim() === '') return null;
    const end = new Date(deadlineIsoUtc).getTime();
    if (!Number.isFinite(end)) return null;
    const ms = end - now.getTime();
    if (ms <= 0) {
        const lateMin = Math.max(0, Math.floor(-ms / 60000));
        const overD = Math.floor(lateMin / (60 * 24));
        const overH = Math.floor((lateMin % (60 * 24)) / 60);
        const overM = lateMin % 60;
        if (overD >= 1) return `${overD} ngày ${overH} giờ ${overM} phút`;
        if (overH >= 1) return `${overH} giờ ${overM} phút`;
        return `${Math.max(1, lateMin)} phút`;
    }
    const totalMin = Math.floor(ms / 60000);
    const d = Math.floor(totalMin / (60 * 24));
    const h = Math.floor((totalMin % (60 * 24)) / 60);
    const m = totalMin % 60;
    if (d >= 1) return `${d} ngày ${h} giờ ${m} phút`;
    if (h >= 1) return `${h} giờ ${m} phút`;
    if (totalMin >= 1) return `${m} phút`;
    return 'dưới 1 phút';
}

/**
 * Badge theo review_deadline_at: &lt; 1 ngày = đỏ; &lt; 3 ngày = cảnh báo nhẹ; còn lại xanh; quá hạn = đỏ đậm.
 * @param {string|null|undefined} deadlineIsoUtc
 * @param {Date} [now]
 * @returns {{ bg: string, color: string, label: string } | null}
 */
export function getReviewDeadlineBadge(deadlineIsoUtc, now = new Date()) {
    if (deadlineIsoUtc == null || String(deadlineIsoUtc).trim() === '') return null;
    const end = new Date(deadlineIsoUtc).getTime();
    if (!Number.isFinite(end)) return null;
    const ms = end - now.getTime();
    const remain = formatRemainingUntilUtcDeadline(deadlineIsoUtc, now);
    if (remain == null) return null;

    if (ms <= 0) {
        return {
            bg: '#fee2e2',
            color: '#991b1b',
            label: `Đã quá hạn (${remain})`,
        };
    }
    if (ms < MS_PER_DAY) {
        return {
            bg: '#fee2e2',
            color: '#991b1b',
            label: `Còn ${remain}`,
        };
    }
    if (ms < 3 * MS_PER_DAY) {
        return {
            bg: '#fef3c7',
            color: '#92400e',
            label: `Còn ${remain}`,
        };
    }
    return {
        bg: '#d1fae5',
        color: '#065f46',
        label: `Còn ${remain}`,
    };
}

/** Style badge theo TimeStatus (đồng bộ tone với admin). */
export function getSlaBadgeStyle(timeStatus) {
    const n = normalizeTimeStatus(timeStatus);
    const key = (n || '').toLowerCase();
    const map = {
        ontime: { bg: '#d1fae5', color: '#065f46', label: 'Trong hạn' },
        warning: { bg: '#fef3c7', color: '#92400e', label: 'Cần chú ý' },
        critical: { bg: '#fee2e2', color: '#991b1b', label: 'Sắp hết hạn' },
        overdue: { bg: '#fee2e2', color: '#991b1b', label: 'Quá hạn' },
    };
    return map[key] ?? { bg: '#f1f5f9', color: '#475569', label: n || '—' };
}

/**
 * Copy đếm ngược tới hạn chính sách (+7 ngày; label hiển thị: từ lúc nhận duyệt đơn).
 * @param {string|null} claimedAtIso - mốc thời gian moderator nhận duyệt (UTC ISO)
 * @param {Date} [now]
 */
export function formatPolicySlaCountdown(claimedAtIso, now = new Date()) {
    const end = policySuggestedEndUtc(claimedAtIso);
    if (!end) return { line: null, short: null };
    const ms = end.getTime() - now.getTime();
    if (ms <= 0) {
        const overMin = Math.ceil(-ms / 60000);
        const overH = Math.floor(overMin / 60);
        const overD = Math.floor(overH / 24);
        let overTxt = '';
        if (overD >= 1) overTxt = `${overD} ngày`;
        else if (overH >= 1) overTxt = `${overH} giờ`;
        else overTxt = `${overMin} phút`;
        return {
            line: `Đã quá hạn chính sách (+7 ngày từ lúc nhận duyệt đơn) khoảng ${overTxt}.`,
            short: `Quá hạn ~${overTxt}`,
        };
    }
    const totalMin = Math.floor(ms / 60000);
    const d = Math.floor(totalMin / (60 * 24));
    const h = Math.floor((totalMin % (60 * 24)) / 60);
    const m = totalMin % 60;
    let remain = '';
    if (d >= 1) remain = `${d} ngày ${h} giờ`;
    else if (h >= 1) remain = `${h} giờ ${m} phút`;
    else remain = `${m} phút`;
    return {
        line: `Hạn chính sách (+7 ngày từ lúc nhận duyệt đơn): còn khoảng ${remain}.`,
        short: `Còn ~${remain}`,
    };
}

/** Hạn moderator chọn khi nhận duyệt — ISO UTC. */
export function reviewDeadlineAfterDaysUtc(days) {
    const d = Number(days);
    if (!Number.isFinite(d) || d < 1) return new Date(Date.now() + 7 * 86400000).toISOString();
    return new Date(Date.now() + d * 86400000).toISOString();
}

/** Từ input datetime-local (local) → ISO UTC */
export function localDateTimeInputToIsoUtc(value) {
    if (!value) return null;
    const dt = new Date(value);
    if (Number.isNaN(dt.getTime())) return null;
    return dt.toISOString();
}

/** Đồng bộ với ReviewEscalationService (BE): lý do gửi đơn escalation (gia hạn / trả về hàng đợi). */
export const MODERATOR_ESCALATION_REASON_MIN_WORDS = 50;

export function countModeratorEscalationReasonWords(text) {
    const t = String(text ?? '').trim();
    if (!t) return 0;
    return t.split(/\s+/).filter(Boolean).length;
}
