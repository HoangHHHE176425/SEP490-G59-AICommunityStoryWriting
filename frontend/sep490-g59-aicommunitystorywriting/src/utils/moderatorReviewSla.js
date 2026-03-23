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

/** Style badge theo TimeStatus (đồng bộ tone với admin). */
export function getSlaBadgeStyle(timeStatus) {
    const n = normalizeTimeStatus(timeStatus);
    const key = (n || '').toLowerCase();
    const map = {
        ontime: { bg: '#d1fae5', color: '#065f46', label: 'Trong hạn' },
        warning: { bg: '#fef3c7', color: '#92400e', label: 'Cần chú ý' },
        critical: { bg: '#ffedd5', color: '#9a3412', label: 'Gấp' },
        overdue: { bg: '#fee2e2', color: '#991b1b', label: 'Quá hạn chính sách' },
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
