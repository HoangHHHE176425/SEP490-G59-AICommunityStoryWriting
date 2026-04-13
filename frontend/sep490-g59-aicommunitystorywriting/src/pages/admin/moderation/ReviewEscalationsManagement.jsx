import { useState, useEffect, useCallback } from 'react';
import { Link } from 'react-router-dom';
import { ShieldCheck, ListFilter } from 'lucide-react';
import { Pagination } from '../../../components/pagination/Pagination';
import {
    getPendingReviewEscalations,
    resolveReviewEscalation,
    getReviewEscalationHistory,
    getReviewEscalationLog,
} from '../../../api/admin/adminModerationApi';
import {
    getAdminComplianceLockRequests,
    resolveAdminComplianceLockRequest,
    getAdminComplianceAdminActionRequests,
    resolveAdminComplianceAdminActionRequest,
} from '../../../api/admin/adminComplianceApi';
import { getChapterReviewContent } from '../../../api/moderator/moderatorApi';
import { getChapters } from '../../../api/chapter/chapterApi';
import { localDateTimeInputToIsoUtc } from '../../../utils/moderatorReviewSla';
import { formatApiDateTimeVietnamVi } from '../../../utils/apiDateTime';

/** Khi từ chối đơn escalation/compliance: ghi chú admin tối thiểu số từ. */
const ADMIN_REJECT_NOTE_MIN_WORDS = 50;
const ADMIN_REJECT_NOTE_MAX_LENGTH = 2000;

function countWords(text) {
    const t = String(text ?? '').trim();
    if (!t) return 0;
    return t.split(/\s+/).filter(Boolean).length;
}

/** Bỏ URL khỏi chuỗi lỗi (tránh hiện localhost trong UI). */
function sanitizeApiErrorText(s) {
    if (typeof s !== 'string' || !s.trim()) return '';
    return s
        .replace(/https?:\/\/[^\s]+/gi, '')
        .replace(/\s{2,}/g, ' ')
        .trim();
}

/**
 * Thông báo lỗi thân thiện từ axios/API — không dùng raw message chứa URL.
 * @param {unknown} err
 * @returns {string}
 */
function getEscalationResolveErrorMessage(err) {
    const data = err?.response?.data;
    if (data && typeof data === 'object') {
        const msg = data.message ?? data.Message;
        if (typeof msg === 'string' && msg.trim()) {
            const clean = sanitizeApiErrorText(msg);
            if (clean) return clean;
        }
        const er = data.error ?? data.Error;
        if (typeof er === 'string' && er.trim()) {
            const clean = sanitizeApiErrorText(er);
            if (clean) return clean;
        }
    }
    const code = err?.response?.status;
    if (code === 401) return 'Phiên đăng nhập hết hạn hoặc không xác định được người dùng.';
    if (code === 403) return 'Bạn không có quyền thực hiện thao tác này.';
    if (code === 404) return 'Không tìm thấy đơn hoặc đơn đã được xử lý.';
    if (code === 400) return 'Dữ liệu không hợp lệ. Vui lòng kiểm tra lại hoặc tải lại trang.';
    if (code >= 500) return 'Máy chủ đang bận hoặc gặp lỗi. Vui lòng thử lại sau.';
    const raw = typeof err?.message === 'string' ? err.message : '';
    if (raw && !/localhost|127\.0\.0\.1|:\/\/\/?/i.test(raw)) {
        if (/^Request failed with status code\s+\d+/i.test(raw)) {
            return code ? `Yêu cầu thất bại (mã ${code}).` : 'Yêu cầu thất bại.';
        }
        const clean = sanitizeApiErrorText(raw);
        if (clean) return clean;
    }
    if (err?.code === 'ERR_NETWORK' || err?.code === 'ECONNABORTED' || /network error/i.test(raw)) {
        return 'Không kết nối được máy chủ. Kiểm tra mạng và thử lại.';
    }
    return 'Đã xảy ra lỗi. Vui lòng thử lại.';
}

/** Đồng bộ token với các màn admin (PublicationManagement, CategoryManagement, …). */
const T = {
    // Prefer AdminLayout theme tokens when present.
    green: 'var(--admin-primary, #13ec5b)',
    greenHover: 'var(--admin-primary, #13ec5b)',
    // Previously used as "active tab" color; map to admin primary for consistency.
    sky: 'var(--admin-primary, #13ec5b)',
    skySoft: 'var(--admin-primary-soft, rgba(19, 236, 91, 0.12))',
    slate: '#64748b',
    slateDark: '#334155',
    title: '#1e293b',
    border: '#e2e8f0',
    bg: '#f8fafc',
    card: '#ffffff',
    critical: { bg: '#fee2e2', fg: '#991b1b', border: '#fecaca' },
    standard: { bg: '#f1f5f9', fg: '#475569', border: '#e2e8f0' },
};

function pubTabStyle(active, activeBg) {
    return {
        padding: '0.46rem 0.95rem',
        fontSize: '0.78rem',
        fontWeight: 600,
        backgroundColor: active ? activeBg : 'transparent',
        color: active ? '#ffffff' : T.slate,
        border: active ? 'none' : `1px solid ${T.border}`,
        borderRadius: '9999px',
        cursor: 'pointer',
        transition: 'all 0.2s',
    };
}

function pillHoverHandlers(active) {
    return {
        onMouseEnter: (e) => {
            if (!active) e.currentTarget.style.backgroundColor = T.bg;
        },
        onMouseLeave: (e) => {
            if (!active) e.currentTarget.style.backgroundColor = 'transparent';
        },
    };
}

function kindShort(k) {
    const s = String(k || '').toUpperCase();
    if (s.includes('EXTEND')) return 'Gia hạn';
    if (s.includes('RELEASE')) return 'Hủy nhận';
    return k || '—';
}

function kindLong(k) {
    const s = String(k || '').toUpperCase();
    if (s.includes('EXTEND')) return 'Gia hạn hạn duyệt';
    if (s.includes('RELEASE')) return 'Hủy nhận duyệt';
    return kindShort(k);
}

/** Đích đơn escalation (STORY/CHAPTER) — nhãn tiếng Việt cho cột Loại. */
function targetTypeVi(row) {
    const tt = String(row?.targetType ?? row?.TargetType ?? '').trim().toUpperCase();
    const map = {
        STORY: 'Truyện',
        CHAPTER: 'Chương',
        MODERATOR: 'Kiểm duyệt viên',
    };
    return map[tt] || (tt || '—');
}

/** Nickname/email từ API — chuẩn hóa nhãn tiếng Việt khi hệ thống dùng từ tiếng Anh. */
function senderDisplayNameVi(row) {
    const raw = row?.senderName
        ?? row?.SenderName
        ?? row?.senderDisplayName
        ?? row?.SenderDisplayName
        ?? row?.requesterDisplayName
        ?? row?.RequesterDisplayName
        ?? row?.requesterEmail
        ?? row?.RequesterEmail
        ?? row?.senderEmail
        ?? row?.SenderEmail;
    if (raw == null || String(raw).trim() === '') return '—';
    return String(raw).trim();
}

function urgencyBadge(tier) {
    const t = String(tier || '').toUpperCase();
    const map = {
        CRITICAL: { ...T.critical, label: 'Nghiêm trọng' },
        STANDARD: { ...T.standard, label: 'Thông thường' },
    };
    const norm = t === 'CRITICAL' ? 'CRITICAL' : 'STANDARD';
    const c = map[norm] ?? { ...T.standard, label: tier || '—' };
    return (
        <span
            style={{
                display: 'inline-block',
                padding: '0.2rem 0.5rem',
                borderRadius: '6px',
                fontWeight: 700,
                fontSize: '0.7rem',
                backgroundColor: c.bg,
                color: c.fg,
                border: `1px solid ${c.border}`,
            }}
        >
            {c.label}
        </span>
    );
}

function historyResultBadge(st) {
    const s = String(st || '').toUpperCase();
    if (s === 'APPROVED') {
        return (
            <span style={{ padding: '0.2rem 0.45rem', borderRadius: '6px', fontSize: '0.7rem', fontWeight: 700, background: '#d1fae5', color: '#065f46' }}>
                Đã chấp nhận
            </span>
        );
    }
    if (s === 'REJECTED') {
        return (
            <span style={{ padding: '0.2rem 0.45rem', borderRadius: '6px', fontSize: '0.7rem', fontWeight: 700, background: '#fee2e2', color: '#991b1b' }}>
                Từ chối
            </span>
        );
    }
    return <span style={{ fontSize: '0.75rem', color: T.slate }}>{st || '—'}</span>;
}

function logStatusBadge(st) {
    const s = String(st || '').toUpperCase();
    if (s === 'PENDING') {
        return <span style={{ padding: '0.2rem 0.45rem', borderRadius: '6px', fontSize: '0.7rem', fontWeight: 700, background: '#fef3c7', color: '#92400e' }}>Chờ xử lý</span>;
    }
    if (s === 'APPROVED') {
        return <span style={{ padding: '0.2rem 0.45rem', borderRadius: '6px', fontSize: '0.7rem', fontWeight: 700, background: '#d1fae5', color: '#065f46' }}>Đã chấp nhận</span>;
    }
    if (s === 'REJECTED') {
        return <span style={{ padding: '0.2rem 0.45rem', borderRadius: '6px', fontSize: '0.7rem', fontWeight: 700, background: '#e2e8f0', color: '#475569' }}>Từ chối</span>;
    }
    return <span style={{ fontSize: '0.75rem' }}>{st || '—'}</span>;
}

function truncate(str, max) {
    const t = (str ?? '').toString();
    if (t.length <= max) return t;
    return `${t.slice(0, max)}…`;
}

/** Ẩn tag nội bộ nguồn comment report trong message hiển thị cho admin. */
function sanitizeComplianceReasonText(raw) {
    const s = String(raw ?? '').trim();
    if (!s) return '—';
    return s.replace(/\[COMMENT_REPORT:[0-9a-fA-F-]{36}\]\s*/g, '').trim() || '—';
}

/** Đồng bộ BE: moderator dùng UrgencyTier (từ sender_urgency_tier); compliance lock/action dùng urgency_tier trên bảng tương ứng — chỉ CRITICAL | STANDARD. */
function urgencyKeyFromRow(row) {
    const raw = String(
        row?.urgencyTier
        ?? row?.UrgencyTier
        ?? row?.senderUrgencyTier
        ?? row?.SenderUrgencyTier
        ?? row?.sender_urgency_tier
        ?? row?.urgency_tier
        ?? 'STANDARD',
    ).toUpperCase();
    return raw === 'CRITICAL' ? 'CRITICAL' : 'STANDARD';
}

function toDateTimeLocalInput(value) {
    if (!value) return '';
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return '';
    const pad = (n) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** Nhãn trạng thái chương cho bảng tóm tắt (không tải nội dung). */
function chapterStatusVi(status) {
    const s = String(status ?? '').toUpperCase();
    const map = {
        PENDING_REVIEW: 'Chờ duyệt',
        PUBLISHED: 'Đã xuất bản',
        REJECTED: 'Từ chối',
        DRAFT: 'Bản nháp',
    };
    return map[s] || (status ? String(status) : '—');
}

function TargetTitleCell({ row }) {
    const tt = String(row.targetType ?? row.TargetType ?? '').toUpperCase();
    const id = row.targetId ?? row.TargetId;
    const title = row.targetTitle ?? row.TargetTitle ?? id ?? '—';
    if (tt === 'STORY' && id) {
        return (
            <Link to={`/story/${id}`} target="_blank" rel="noopener noreferrer" style={{ color: T.sky, fontWeight: 600, textDecoration: 'none' }}>
                {title}
            </Link>
        );
    }
    return (
        <span
            style={{ fontWeight: 600 }}
            title="CHAPTER: API chỉ trả targetId (chapter). Đọc nội dung công khai cần storyId+chapterId; mở qua Quản lý xuất bản (moderator) nếu cần."
        >
            {title}
        </span>
    );
}

const tableBase = {
    width: '100%',
    borderCollapse: 'collapse',
    fontSize: '0.8125rem',
    background: '#ffffff',
};
/** Header / hàng — cùng palette bảng Compliance (mint) */
const thBase = {
    padding: '0.65rem 0.75rem',
    borderBottom: '1px solid #c9f0d8',
    textAlign: 'left',
    background: '#f0faf5',
    color: '#047857',
    fontWeight: 700,
    fontSize: '0.72rem',
    letterSpacing: '0.02em',
    textTransform: 'uppercase',
};
const tdBase = { padding: '0.65rem 0.75rem', borderBottom: '1px solid #c9f0d8', verticalAlign: 'middle', background: '#ffffff' };

const inputBase = {
    display: 'block',
    width: '100%',
    marginTop: 4,
    padding: '0.42rem 0.58rem',
    borderRadius: 7,
    border: `1px solid ${T.border}`,
    fontSize: '0.78rem',
    color: T.title,
    background: T.card,
    boxSizing: 'border-box',
};

export function ReviewEscalationsManagement() {
    /** Hai vùng chính (cùng cấu trúc màn Admin Client): đơn moderator vs log escalation. */
    const [mainTab, setMainTab] = useState('orders');
    const [orderSourceTab, setOrderSourceTab] = useState('moderator'); // moderator | compliance
    const [listMode, setListMode] = useState('pending');
    const [tier, setTier] = useState('');
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [items, setItems] = useState([]);
    const [counts, setCounts] = useState({ critical: 0, standard: 0 });
    const [historyTotal, setHistoryTotal] = useState(0);

    const [resolveRow, setResolveRow] = useState(null);
    const [adminNote, setAdminNote] = useState('');
    const [adminNoteRejectError, setAdminNoteRejectError] = useState('');
    /** Lỗi API khi chấp nhận/từ chối — hiển thị trong modal (không dùng alert). */
    const [resolveApiError, setResolveApiError] = useState(null);
    /** { loading, error, data } | null — nội dung chapter khi target CHAPTER */
    const [resolveChapterPreview, setResolveChapterPreview] = useState(null);
    /** { loading, error, items } | null — metadata các chương khi target STORY (không nội dung) */
    const [resolveStoryChaptersPreview, setResolveStoryChaptersPreview] = useState(null);
    const [resolving, setResolving] = useState(false);
    const [complianceResolveRow, setComplianceResolveRow] = useState(null);
    const [complianceAdminNote, setComplianceAdminNote] = useState('');
    const [complianceAdminNoteRejectError, setComplianceAdminNoteRejectError] = useState('');
    const [complianceSuspendUntilUtc, setComplianceSuspendUntilUtc] = useState('');
    const [complianceResolving, setComplianceResolving] = useState(false);

    /** Log tab */
    const [logLoading, setLogLoading] = useState(false);
    const [logError, setLogError] = useState(null);
    const [logItems, setLogItems] = useState([]);
    const [logPage, setLogPage] = useState(1);
    const [logPageInfo, setLogPageInfo] = useState({ page: 1, totalPages: 1, total: 0, hasPrev: false, hasNext: false });
    const [logSearch, setLogSearch] = useState('');
    const [logStatus, setLogStatus] = useState('');
    const [logRequestKind, setLogRequestKind] = useState('');
    const [logTargetType, setLogTargetType] = useState('');
    const [logPageSize, setLogPageSize] = useState(20);
    const [logSenderId, setLogSenderId] = useState('');
    const [logResolverId, setLogResolverId] = useState('');
    const [logCreatedFrom, setLogCreatedFrom] = useState('');
    const [logCreatedTo, setLogCreatedTo] = useState('');
    const [logResolvedFrom, setLogResolvedFrom] = useState('');
    const [logResolvedTo, setLogResolvedTo] = useState('');
    const [logSortBy, setLogSortBy] = useState('created_at');
    const [logSortOrder, setLogSortOrder] = useState('desc');
    const [showLogAdvancedFilters, setShowLogAdvancedFilters] = useState(false);
    const [historySearch, setHistorySearch] = useState('');
    const [historyStatus, setHistoryStatus] = useState('');
    const [historyRequestType, setHistoryRequestType] = useState('');
    const [historyPage, setHistoryPage] = useState(1);
    const [historyPageSize] = useState(10);
    const loadOrders = useCallback(async () => {
        setLoading(true);
        setError(null);
        try {
            if (orderSourceTab === 'compliance') {
                if (listMode === 'history') {
                    const [approvedLockRes, rejectedLockRes, approvedActionRes, rejectedActionRes] = await Promise.all([
                        getAdminComplianceLockRequests({ status: 'APPROVED' }),
                        getAdminComplianceLockRequests({ status: 'REJECTED' }),
                        getAdminComplianceAdminActionRequests({ status: 'APPROVED' }),
                        getAdminComplianceAdminActionRequests({ status: 'REJECTED' }),
                    ]);
                    const approvedLocks = (Array.isArray(approvedLockRes) ? approvedLockRes : []).map((x) => ({ ...x, __reqType: 'LOCK' }));
                    const rejectedLocks = (Array.isArray(rejectedLockRes) ? rejectedLockRes : []).map((x) => ({ ...x, __reqType: 'LOCK' }));
                    const approvedActions = (Array.isArray(approvedActionRes) ? approvedActionRes : []).map((x) => ({ ...x, __reqType: 'ADMIN_ACTION' }));
                    const rejectedActions = (Array.isArray(rejectedActionRes) ? rejectedActionRes : []).map((x) => ({ ...x, __reqType: 'ADMIN_ACTION' }));
                    const merged = [...approvedLocks, ...rejectedLocks, ...approvedActions, ...rejectedActions].sort((a, b) => {
                        const ta = new Date(a?.resolvedAtUtc ?? a?.resolved_at ?? a?.createdAtUtc ?? a?.created_at ?? 0).getTime();
                        const tb = new Date(b?.resolvedAtUtc ?? b?.resolved_at ?? b?.createdAtUtc ?? b?.created_at ?? 0).getTime();
                        return tb - ta;
                    });
                    setItems(merged);
                    setHistoryTotal(merged.length);
                    setCounts({ critical: 0, standard: 0 });
                    return;
                }
                const [pendingLockRes, pendingActionRes] = await Promise.all([
                    getAdminComplianceLockRequests({ status: 'PENDING' }),
                    getAdminComplianceAdminActionRequests({ status: 'PENDING' }),
                ]);
                const pendingLocks = (Array.isArray(pendingLockRes) ? pendingLockRes : []).map((x) => ({ ...x, __reqType: 'LOCK' }));
                const pendingActions = (Array.isArray(pendingActionRes) ? pendingActionRes : []).map((x) => ({ ...x, __reqType: 'ADMIN_ACTION' }));
                const pendingAll = [...pendingLocks, ...pendingActions].sort((a, b) => {
                    const ta = new Date(a?.createdAtUtc ?? a?.created_at ?? 0).getTime();
                    const tb = new Date(b?.createdAtUtc ?? b?.created_at ?? 0).getTime();
                    return tb - ta;
                });
                const complianceCounts = pendingAll.reduce((acc, row) => {
                    const k = urgencyKeyFromRow(row);
                    if (k === 'CRITICAL') acc.critical += 1;
                    else acc.standard += 1;
                    return acc;
                }, { critical: 0, standard: 0 });
                const pending = !tier
                    ? pendingAll
                    : pendingAll.filter((x) => urgencyKeyFromRow(x) === tier);
                setItems(pending);
                setCounts(complianceCounts);
                return;
            }
            if (listMode === 'history') {
                const rh = await getReviewEscalationHistory(0, 200);
                const hist = rh?.items ?? rh?.Items ?? [];
                const total = rh?.totalCount ?? rh?.TotalCount ?? hist.length;
                setItems(Array.isArray(hist) ? hist : []);
                setHistoryTotal(total);
                setCounts({ critical: 0, standard: 0 });
                return;
            }
            const r = await getPendingReviewEscalations(tier || undefined);
            setItems(r?.items ?? r?.Items ?? []);
            const c = r?.counts ?? r?.Counts ?? {};
            setCounts({
                critical: c.critical ?? c.Critical ?? 0,
                standard: c.standard ?? c.Standard ?? 0,
            });
        } catch (e) {
            setError(e?.response?.data?.message ?? e?.message ?? 'Không tải được danh sách.');
            setItems([]);
        } finally {
            setLoading(false);
        }
    }, [listMode, tier, orderSourceTab]);

    const loadLog = useCallback(async (pageOverride) => {
        const page = typeof pageOverride === 'number' && pageOverride >= 1 ? pageOverride : logPage;
        setLogLoading(true);
        setLogError(null);
        try {
            const params = {
                page,
                pageSize: logPageSize,
                sortBy: logSortBy,
                sortOrder: logSortOrder,
            };
            if (logSearch.trim()) params.search = logSearch.trim();
            if (logStatus) params.status = logStatus;
            if (logRequestKind) params.requestKind = logRequestKind;
            if (logTargetType) params.targetType = logTargetType;
            if (logSenderId.trim()) params.senderId = logSenderId.trim();
            if (logResolverId.trim()) params.resolverId = logResolverId.trim();
            if (logCreatedFrom) {
                const iso = localDateTimeInputToIsoUtc(logCreatedFrom);
                if (iso) params.createdFrom = iso;
            }
            if (logCreatedTo) {
                const iso = localDateTimeInputToIsoUtc(logCreatedTo);
                if (iso) params.createdTo = iso;
            }
            if (logResolvedFrom) {
                const iso = localDateTimeInputToIsoUtc(logResolvedFrom);
                if (iso) params.resolvedFrom = iso;
            }
            if (logResolvedTo) {
                const iso = localDateTimeInputToIsoUtc(logResolvedTo);
                if (iso) params.resolvedTo = iso;
            }
            const r = await getReviewEscalationLog(params);
            const list = r?.items ?? r?.Items ?? [];
            const total = r?.totalCount ?? r?.TotalCount ?? 0;
            const p = r?.page ?? r?.Page ?? page;
            const ps = r?.pageSize ?? r?.PageSize ?? logPageSize;
            const tp = r?.totalPages ?? r?.TotalPages ?? Math.max(1, Math.ceil(total / (ps || 1)));
            const hasPrev = r?.hasPreviousPage ?? r?.HasPreviousPage ?? p > 1;
            const hasNext = r?.hasNextPage ?? r?.HasNextPage ?? p < tp;
            setLogItems(Array.isArray(list) ? list : []);
            setLogPage(p);
            setLogPageInfo({ page: p, totalPages: tp, total, hasPrev, hasNext });
        } catch (e) {
            setLogError(e?.response?.data?.message ?? e?.message ?? 'Lỗi tải log.');
            setLogItems([]);
        } finally {
            setLogLoading(false);
        }
    }, [
        logPage,
        logPageSize,
        logSearch,
        logStatus,
        logRequestKind,
        logTargetType,
        logSenderId,
        logResolverId,
        logCreatedFrom,
        logCreatedTo,
        logResolvedFrom,
        logResolvedTo,
        logSortBy,
        logSortOrder,
    ]);

    useEffect(() => {
        if (mainTab !== 'orders') return;
        loadOrders();
    }, [mainTab, loadOrders]);

    useEffect(() => {
        setTier('');
    }, [orderSourceTab]);

    useEffect(() => {
        if (mainTab !== 'log') return;
        loadLog(1);
        // Chỉ khi chuyển sang tab Log; không gắn loadLog vào deps (tránh refetch khi đổi trang/filter làm đổi identity).
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [mainTab]);

    const openResolve = (row) => {
        setResolveRow(row);
        setAdminNote('');
        setAdminNoteRejectError('');
        setResolveApiError(null);
    };

    const closeResolveModal = () => {
        if (resolving) return;
        setResolveRow(null);
        setResolveApiError(null);
    };

    useEffect(() => {
        if (!resolveRow) {
            setResolveChapterPreview(null);
            setResolveStoryChaptersPreview(null);
            return undefined;
        }
        const tt = String(resolveRow.targetType ?? resolveRow.TargetType ?? '').toUpperCase();
        const tid = resolveRow.targetId ?? resolveRow.TargetId;
        let cancelled = false;

        if (tt === 'CHAPTER' && tid) {
            setResolveChapterPreview({ loading: true, error: null, data: null });
            getChapterReviewContent(tid)
                .then((data) => {
                    if (!cancelled) setResolveChapterPreview({ loading: false, error: null, data });
                })
                .catch((e) => {
                    if (!cancelled) {
                        setResolveChapterPreview({
                            loading: false,
                            error: e?.response?.data?.message ?? e?.message ?? 'Không tải được nội dung chương.',
                            data: null,
                        });
                    }
                });
        } else {
            setResolveChapterPreview(null);
        }

        if (tt === 'STORY' && tid) {
            const kind = String(resolveRow.requestKind ?? resolveRow.RequestKind ?? '').toUpperCase();
            const isRelease = kind === 'RELEASE_ASSIGNMENT';
            const releaseIds = resolveRow.releaseAffectedChapterIds ?? resolveRow.ReleaseAffectedChapterIds;
            const idList = Array.isArray(releaseIds) ? releaseIds.map((x) => String(x)) : [];

            if (isRelease && idList.length > 0) {
                setResolveStoryChaptersPreview({ loading: true, error: null, items: [] });
                getChapters({
                    storyId: String(tid),
                    includeChapterIds: idList,
                    page: 1,
                    pageSize: Math.max(idList.length, 20),
                    sortBy: 'order_index',
                    sortOrder: 'asc',
                })
                    .then((res) => {
                        const list = res?.items ?? res?.Items ?? [];
                        const arr = Array.isArray(list) ? list : [];
                        const sorted = [...arr].sort(
                            (a, b) => (Number(a.orderIndex ?? a.OrderIndex ?? 0)) - (Number(b.orderIndex ?? b.OrderIndex ?? 0)),
                        );
                        if (!cancelled) setResolveStoryChaptersPreview({ loading: false, error: null, items: sorted });
                    })
                    .catch((e) => {
                        if (!cancelled) {
                            setResolveStoryChaptersPreview({
                                loading: false,
                                error: e?.response?.data?.message ?? e?.message ?? 'Không tải được danh sách chương.',
                                items: [],
                            });
                        }
                    });
            } else if (isRelease) {
                setResolveStoryChaptersPreview({ loading: false, error: null, items: [] });
            } else {
                setResolveStoryChaptersPreview(null);
            }
        } else {
            setResolveStoryChaptersPreview(null);
        }

        return () => {
            cancelled = true;
        };
    }, [resolveRow]);

    const submitApprove = async () => {
        if (!resolveRow) return;
        const id = resolveRow.id ?? resolveRow.Id;
        const body = { approve: true, adminNote: adminNote.trim() || null, confirmedDeadlineAt: null, reassignToUserId: null };
        /* EXTEND: BE dùng proposed_deadline_at. RELEASE: luôn trả về hàng đợi (không reassign). */

        setResolveApiError(null);
        setResolving(true);
        try {
            await resolveReviewEscalation(id, body);
            setResolveApiError(null);
            setResolveRow(null);
            await loadOrders();
        } catch (e) {
            setResolveApiError({
                title: 'Không thể chấp nhận đơn',
                message: getEscalationResolveErrorMessage(e),
            });
        } finally {
            setResolving(false);
        }
    };

    const submitReject = async () => {
        if (!resolveRow) return;
        const trimmed = adminNote.trim();
        const wc = countWords(trimmed);
        if (wc < ADMIN_REJECT_NOTE_MIN_WORDS) {
            setAdminNoteRejectError(
                `Khi từ chối, bắt buộc nhập lý do (ghi chú quản trị viên). Tối thiểu ${ADMIN_REJECT_NOTE_MIN_WORDS} từ (hiện tại: ${wc} từ).`,
            );
            return;
        }
        if (trimmed.length > ADMIN_REJECT_NOTE_MAX_LENGTH) {
            setAdminNoteRejectError(`Ghi chú quản trị viên không được vượt quá ${ADMIN_REJECT_NOTE_MAX_LENGTH} ký tự.`);
            return;
        }
        setAdminNoteRejectError('');
        setResolveApiError(null);
        const id = resolveRow.id ?? resolveRow.Id;
        setResolving(true);
        try {
            await resolveReviewEscalation(id, { approve: false, adminNote: trimmed });
            setResolveApiError(null);
            setResolveRow(null);
            await loadOrders();
        } catch (e) {
            setResolveApiError({
                title: 'Không thể từ chối đơn',
                message: getEscalationResolveErrorMessage(e),
            });
        } finally {
            setResolving(false);
        }
    };

    const openComplianceResolve = (row) => {
        setComplianceResolveRow(row);
        setComplianceAdminNote('');
        setComplianceAdminNoteRejectError('');
        setComplianceSuspendUntilUtc(toDateTimeLocalInput(row?.proposedSuspendUntilUtc ?? row?.ProposedSuspendUntilUtc));
        setResolveApiError(null);
    };

    const closeComplianceResolve = () => {
        if (complianceResolving) return;
        setComplianceResolveRow(null);
        setComplianceAdminNoteRejectError('');
        setResolveApiError(null);
    };

    const submitComplianceResolve = async (decisionOverride) => {
        if (!complianceResolveRow) return;
        const id = complianceResolveRow.id ?? complianceResolveRow.Id;
        const reqType = complianceResolveRow.__reqType;
        const decision = decisionOverride ?? (reqType === 'ADMIN_ACTION' ? 'APPROVE' : 'APPROVE_UNLOCK');
        const trimmedNote = (complianceAdminNote || '').trim();
        if (decision === 'REJECT') {
            const wc = countWords(trimmedNote);
            if (wc < ADMIN_REJECT_NOTE_MIN_WORDS) {
                setComplianceAdminNoteRejectError(
                    `Khi từ chối, bắt buộc nhập lý do (ghi chú quản trị viên). Tối thiểu ${ADMIN_REJECT_NOTE_MIN_WORDS} từ (hiện tại: ${wc} từ).`,
                );
                return;
            }
            if (trimmedNote.length > ADMIN_REJECT_NOTE_MAX_LENGTH) {
                setComplianceAdminNoteRejectError(`Ghi chú quản trị viên không được vượt quá ${ADMIN_REJECT_NOTE_MAX_LENGTH} ký tự.`);
                return;
            }
        }
        setComplianceAdminNoteRejectError('');
        setComplianceResolving(true);
        setResolveApiError(null);
        try {
            if (reqType === 'ADMIN_ACTION') {
                await resolveAdminComplianceAdminActionRequest(id, {
                    decision,
                    adminNote: trimmedNote || undefined,
                    suspendUntilUtc: complianceSuspendUntilUtc ? new Date(complianceSuspendUntilUtc).toISOString() : undefined,
                });
            } else {
                await resolveAdminComplianceLockRequest(id, {
                    decision,
                    adminNote: trimmedNote || undefined,
                });
            }
            setComplianceResolveRow(null);
            await loadOrders();
        } catch (e) {
            setResolveApiError({
                title: 'Không thể xử lý đơn compliance',
                message: getEscalationResolveErrorMessage(e),
            });
        } finally {
            setComplianceResolving(false);
        }
    };

    const applyLogFilters = () => {
        setLogPage(1);
        loadLog(1);
    };

    useEffect(() => {
        setHistoryPage(1);
    }, [historySearch, historyStatus, historyRequestType, historyPageSize, listMode, orderSourceTab]);

    const historyFilteredItems = useCallback(() => {
        if (listMode !== 'history') return [];
        const q = historySearch.trim().toLowerCase();
        return (Array.isArray(items) ? items : []).filter((row) => {
            const statusRaw = String(row.status ?? row.Status ?? '').toUpperCase();
            const reqKindRaw = String(row.requestKind ?? row.RequestKind ?? '').toUpperCase();
            const reqTypeRaw = String(row.__reqType ?? '').toUpperCase();
            if (historyStatus && statusRaw !== historyStatus) return false;
            if (historyRequestType) {
                if (orderSourceTab === 'moderator') {
                    if (reqKindRaw !== historyRequestType) return false;
                } else if (reqTypeRaw !== historyRequestType) return false;
            }
            if (!q) return true;
            const title = String(row.storyTitle ?? row.targetTitle ?? '').toLowerCase();
            const sender = String(row.requesterDisplayName ?? row.requesterEmail ?? row.senderName ?? '').toLowerCase();
            const message = String(row.message ?? row.reason ?? '').toLowerCase();
            const reqLabel = reqKindRaw === 'BAN_USER' ? 'chan tai khoan' : reqKindRaw === 'SUSPEND_AUTHOR_WRITING' ? 'tam dinh chi quyen viet' : reqKindRaw.toLowerCase();
            return title.includes(q) || sender.includes(q) || message.includes(q) || reqLabel.includes(q);
        });
    }, [historySearch, historyStatus, historyRequestType, items, listMode, orderSourceTab]);

    const historyItems = historyFilteredItems();
    const historyTotalPages = Math.max(1, Math.ceil(historyItems.length / historyPageSize));
    const safeHistoryPage = Math.min(historyPage, historyTotalPages);
    const historyPageItems = historyItems.slice((safeHistoryPage - 1) * historyPageSize, safeHistoryPage * historyPageSize);

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1.05rem' }}>
            <style>{`
                .re-compliance-table tbody tr { transition: background-color 0.15s ease; }
                .re-compliance-table tbody tr:hover { background-color: #f7fcf9 !important; }
            `}</style>
            {/* Header — cùng format Quản lý xuất bản / Category */}
            <div>
                <h1 style={{ fontSize: '1.5rem', fontWeight: 700, color: T.title, margin: 0, marginBottom: '0.35rem' }}>Quản lý đơn</h1>
                <p style={{ fontSize: '0.8rem', color: T.slate, margin: 0, maxWidth: '38rem', lineHeight: 1.45 }}>
                    Quản lý đơn của kiểm duyệt viên và xử lý vi phạm viên, bao gồm đơn chờ xử lý, lịch sử đã xử lý và nhật ký tra cứu.
                </p>
            </div>

            {/* Tab cấp cao — card + pill như bộ lọc PublicationManagement */}
            <div
                style={{
                    backgroundColor: T.card,
                    borderRadius: '10px',
                    padding: '0.75rem',
                    border: `1px solid ${T.border}`,
                    display: 'flex',
                    gap: '0.4rem',
                    flexWrap: 'wrap',
                    alignItems: 'center',
                }}
            >
                <button
                    type="button"
                    onClick={() => { setMainTab('orders'); setOrderSourceTab('moderator'); }}
                    style={pubTabStyle(mainTab === 'orders' && orderSourceTab === 'moderator', T.sky)}
                    {...pillHoverHandlers(mainTab === 'orders' && orderSourceTab === 'moderator')}
                >
                    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
                        Đơn của kiểm duyệt viên
                        {counts.critical > 0 && mainTab === 'orders' && listMode === 'pending' && orderSourceTab === 'moderator' && (
                            <span
                                style={{
                                    minWidth: 20,
                                    height: 20,
                                    padding: '0 6px',
                                    borderRadius: 999,
                                    background: '#ffffff',
                                    color: T.green,
                                    fontSize: '0.7rem',
                                    fontWeight: 800,
                                    display: 'inline-flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                }}
                            >
                                {counts.critical > 99 ? '99+' : counts.critical}
                            </span>
                        )}
                    </span>
                </button>
                <button
                    type="button"
                    onClick={() => { setMainTab('orders'); setOrderSourceTab('compliance'); }}
                    style={pubTabStyle(mainTab === 'orders' && orderSourceTab === 'compliance', T.sky)}
                    {...pillHoverHandlers(mainTab === 'orders' && orderSourceTab === 'compliance')}
                >
                    <span style={{ display: 'inline-flex', alignItems: 'center', gap: 8 }}>
                        Đơn của xử lý vi phạm viên
                        {mainTab === 'orders' && listMode === 'pending' && orderSourceTab === 'compliance' && items.length > 0 && (
                            <span
                                style={{
                                    minWidth: 20,
                                    height: 20,
                                    padding: '0 6px',
                                    borderRadius: 999,
                                    background: '#ffffff',
                                    color: T.green,
                                    fontSize: '0.7rem',
                                    fontWeight: 800,
                                    display: 'inline-flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                }}
                            >
                                {items.length > 99 ? '99+' : items.length}
                            </span>
                        )}
                    </span>
                </button>
            </div>

            {mainTab === 'orders' && (
                <div style={{ backgroundColor: T.card, borderRadius: '10px', border: `1px solid ${T.border}`, overflow: 'hidden' }}>
                    <div style={{ padding: '1rem 1.1rem' }}>
                        <h2 style={{ margin: '0 0 0.8rem', fontSize: '1rem', fontWeight: 600, color: T.title }}>
                            {orderSourceTab === 'compliance'
                                ? 'Đơn của xử lý vi phạm viên'
                                : 'Đơn của kiểm duyệt viên'}
                        </h2>

                        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.4rem', marginBottom: '0.9rem' }}>
                            <button
                                type="button"
                                style={pubTabStyle(listMode === 'pending', T.green)}
                                {...pillHoverHandlers(listMode === 'pending')}
                                onClick={() => setListMode('pending')}
                            >
                                Chờ xử lý
                            </button>
                            <button
                                type="button"
                                style={pubTabStyle(listMode === 'history', T.green)}
                                {...pillHoverHandlers(listMode === 'history')}
                                onClick={() => setListMode('history')}
                            >
                                Đã xử lý (lịch sử)
                            </button>
                        </div>

                        {listMode === 'pending' && (
                            <div style={{ marginBottom: '0.9rem' }}>
                                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: '0.7rem', marginBottom: '0.7rem' }}>
                                    <div style={{ borderRadius: '10px', border: `1px solid ${T.critical.border}`, background: T.critical.bg, padding: '0.75rem' }}>
                                        <p style={{ fontSize: '0.75rem', color: T.critical.fg, margin: 0, fontWeight: 600 }}>Nghiêm trọng</p>
                                        <p style={{ fontSize: '1.25rem', fontWeight: 700, color: T.critical.fg, margin: '0.25rem 0 0' }}>{counts.critical}</p>
                                    </div>
                                    <div style={{ borderRadius: '10px', border: `1px solid ${T.standard.border}`, background: T.standard.bg, padding: '0.75rem' }}>
                                        <p style={{ fontSize: '0.75rem', color: T.standard.fg, margin: 0, fontWeight: 600 }}>Thông thường</p>
                                        <p style={{ fontSize: '1.25rem', fontWeight: 700, color: T.standard.fg, margin: '0.25rem 0 0' }}>{counts.standard}</p>
                                    </div>
                                </div>
                                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.4rem' }}>
                                    {[
                                        { key: '', label: 'Tất cả', color: T.sky },
                                        { key: 'CRITICAL', label: 'Chỉ nghiêm trọng', color: '#ef4444' },
                                        { key: 'STANDARD', label: 'Chỉ thông thường', color: '#64748b' },
                                    ].map((b) => {
                                        const active = tier === b.key;
                                        return (
                                            <button
                                                key={b.key || 'all'}
                                                type="button"
                                                onClick={() => setTier(b.key)}
                                                style={pubTabStyle(active, b.color)}
                                                {...pillHoverHandlers(active)}
                                            >
                                                {b.label}
                                            </button>
                                        );
                                    })}
                                </div>
                            </div>
                        )}

                        {listMode === 'history' && (
                            <div style={{ marginBottom: '1rem', display: 'grid', gap: '0.75rem' }}>
                                <p style={{ fontSize: '0.875rem', color: T.slate, margin: 0, lineHeight: 1.5 }}>
                                    {orderSourceTab === 'compliance'
                                        ? 'Lịch sử đơn đã xử lý của xử lý vi phạm viên.'
                                        : 'Lịch sử đơn đã xử lý của kiểm duyệt viên.'}
                                </p>
                                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))', gap: '0.75rem' }}>
                                    <label style={{ fontSize: '0.8125rem', fontWeight: 600, color: T.title }}>
                                        Tìm kiếm
                                        <input value={historySearch} onChange={(e) => setHistorySearch(e.target.value)} placeholder="Truyện, người gửi, lý do..." style={inputBase} />
                                    </label>
                                    <label style={{ fontSize: '0.8125rem', fontWeight: 600, color: T.title }}>
                                        Trạng thái
                                        <select value={historyStatus} onChange={(e) => setHistoryStatus(e.target.value)} style={inputBase}>
                                            <option value="">Tất cả</option>
                                            <option value="APPROVED">Đã chấp nhận</option>
                                            <option value="REJECTED">Từ chối</option>
                                        </select>
                                    </label>
                                    <label style={{ fontSize: '0.8125rem', fontWeight: 600, color: T.title }}>
                                        Loại đơn
                                        <select value={historyRequestType} onChange={(e) => setHistoryRequestType(e.target.value)} style={inputBase}>
                                            <option value="">Tất cả</option>
                                            {orderSourceTab === 'moderator' ? (
                                                <>
                                                    <option value="RELEASE_ASSIGNMENT">Hủy nhận duyệt</option>
                                                    <option value="EXTEND_DEADLINE">Gia hạn</option>
                                                </>
                                            ) : (
                                                <>
                                                    <option value="LOCK">Yêu cầu trả đơn về hàng đợi</option>
                                                    <option value="ADMIN_ACTION">Yêu cầu xử lý tài khoản</option>
                                                </>
                                            )}
                                        </select>
                                    </label>
                                </div>
                                <div style={{ fontSize: '0.8125rem', color: T.slate }}>
                                    Tổng lọc: <strong>{historyItems.length}</strong> · Trang <strong>{safeHistoryPage}</strong>/{historyTotalPages}
                                </div>
                            </div>
                        )}

                        {error && (
                            <div
                                style={{
                                    padding: '1rem 1.25rem',
                                    background: '#fee2e2',
                                    color: '#991b1b',
                                    borderRadius: '12px',
                                    marginBottom: '1rem',
                                    fontSize: '0.875rem',
                                    border: '1px solid #fecaca',
                                }}
                            >
                                {error}
                            </div>
                        )}

                        {loading ? (
                            <div
                                style={{
                                    backgroundColor: '#fafafa',
                                    borderRadius: '12px',
                                    padding: '3rem 2rem',
                                    textAlign: 'center',
                                    border: `1px solid ${T.border}`,
                                }}
                            >
                                <p style={{ fontSize: '0.875rem', color: T.slate, margin: 0 }}>Đang tải danh sách...</p>
                            </div>
                        ) : listMode === 'history' ? (
                            historyItems.length === 0 ? (
                                <p style={{ color: T.slate }}>
                                    Chưa có đơn nào đã xử lý. Tổng trong hệ thống: <strong>{historyTotal}</strong>
                                </p>
                            ) : (
                                <>
                                    {orderSourceTab === 'compliance' ? (
                                        <div style={{ overflowX: 'auto', border: '1px solid #c9f0d8', borderRadius: '12px' }}>
                                            <table style={tableBase} className="re-compliance-table">
                                                <thead>
                                                    <tr>
                                                        {['Kết quả', 'Loại đơn', 'Truyện', 'Người gửi', 'Yêu cầu', 'Lý do', 'Xử lý lúc', 'Ghi chú quản trị viên'].map((h) => (
                                                            <th key={h} style={thBase}>{h}</th>
                                                        ))}
                                                    </tr>
                                                </thead>
                                                <tbody>
                                                    {historyPageItems.map((row) => {
                                                        const id = row.id ?? row.Id;
                                                        const status = row.status ?? row.Status;
                                                        const note = row.resolutionNote ?? row.resolution_note ?? '—';
                                                        return (
                                                            <tr key={id}>
                                                                <td style={tdBase}>{historyResultBadge(status)}</td>
                                                                <td style={tdBase}>{row.__reqType === 'ADMIN_ACTION' ? 'Yêu cầu xử lý tài khoản' : 'Yêu cầu trả đơn về hàng đợi'}</td>
                                                                <td style={tdBase}>{row.storyTitle ?? row.story?.title ?? '—'}</td>
                                                                <td style={tdBase}>{row.requesterDisplayName ?? row.requesterEmail ?? '—'}</td>
                                                                <td style={{ ...tdBase, fontSize: '0.75rem' }}>
                                                                    {row.__reqType === 'ADMIN_ACTION'
                                                                        ? (String(row.requestKind ?? '').toUpperCase() === 'BAN_USER' ? 'Chặn tài khoản' : 'Tạm đình chỉ quyền viết')
                                                                        : 'Trả đơn truyện về hàng đợi'}
                                                                </td>
                                                                <td style={{ ...tdBase, maxWidth: 260, fontSize: '0.75rem' }}>{truncate(row.message ?? '', 180) || '—'}</td>
                                                                <td style={{ ...tdBase, fontSize: '0.75rem', whiteSpace: 'nowrap' }}>{formatApiDateTimeVietnamVi(row.resolvedAtUtc ?? row.resolved_at)}</td>
                                                                <td style={{ ...tdBase, maxWidth: 240, fontSize: '0.75rem' }}>{truncate(note, 180) || '—'}</td>
                                                            </tr>
                                                        );
                                                    })}
                                                </tbody>
                                            </table>
                                        </div>
                                    ) : (
                                        <>
                                            <div style={{ overflowX: 'auto', border: '1px solid #c9f0d8', borderRadius: '12px' }}>
                                                <table style={tableBase} className="re-compliance-table">
                                                    <thead>
                                                        <tr>
                                                            {['Kết quả', 'Loại', 'Tiêu đề', 'Người gửi', 'Loại đơn', 'Người xử lý', 'Xử lý lúc', 'Hạn xác nhận', 'Ghi chú quản trị viên', 'Lý do'].map((h) => (
                                                                <th key={h} style={thBase}>{h}</th>
                                                            ))}
                                                        </tr>
                                                    </thead>
                                                    <tbody>
                                                        {historyPageItems.map((row) => {
                                                            const id = row.id ?? row.Id;
                                                            const note = truncate(row.resolverNote ?? row.ResolverNote ?? '—', 160);
                                                            const reason = truncate(row.reason ?? row.Reason ?? '', 160);
                                                            return (
                                                                <tr key={id}>
                                                                    <td style={tdBase}>{historyResultBadge(row.status ?? row.Status)}</td>
                                                                    <td style={tdBase}>{targetTypeVi(row)}</td>
                                                                    <td style={tdBase}><TargetTitleCell row={row} /></td>
                                                                    <td style={tdBase}>{senderDisplayNameVi(row)}</td>
                                                                    <td style={tdBase}>{kindShort(row.requestKind ?? row.RequestKind)}</td>
                                                                    <td style={tdBase}>{row.resolverName ?? row.ResolverName ?? '—'}</td>
                                                                    <td style={{ ...tdBase, fontSize: '0.75rem' }}>{formatApiDateTimeVietnamVi(row.resolvedAt ?? row.ResolvedAt)}</td>
                                                                    <td style={{ ...tdBase, fontSize: '0.75rem' }}>{formatApiDateTimeVietnamVi(row.confirmedDeadlineAt ?? row.ConfirmedDeadlineAt)}</td>
                                                                    <td style={{ ...tdBase, fontSize: '0.75rem', maxWidth: 200 }}>{note}</td>
                                                                    <td style={{ ...tdBase, fontSize: '0.75rem', maxWidth: 200 }}>{reason || '—'}</td>
                                                                </tr>
                                                            );
                                                        })}
                                                    </tbody>
                                                </table>
                                            </div>
                                        </>
                                    )}
                                    {historyTotalPages > 1 && (
                                        <Pagination
                                            currentPage={safeHistoryPage}
                                            totalPages={historyTotalPages}
                                            totalItems={historyItems.length}
                                            itemsPerPage={historyPageSize}
                                            onPageChange={setHistoryPage}
                                            itemLabel="đơn"
                                        />
                                    )}
                                </>
                            )
                        ) : items.length === 0 ? (
                            <p style={{ color: T.slate }}>Không có đơn chờ xử lý.</p>
                        ) : (
                            <div style={{ overflowX: 'auto', border: '1px solid #c9f0d8', borderRadius: '12px' }}>
                                <table style={tableBase} className="re-compliance-table">
                                    <thead>
                                        <tr>
                                            {(orderSourceTab === 'compliance'
                                                ? ['Mức độ', 'Loại đơn', 'Truyện', 'Người gửi', 'Yêu cầu', 'Gửi lúc', 'Lý do', '']
                                                : ['Mức độ', 'Loại', 'Tiêu đề', 'Người gửi', 'Yêu cầu', 'Gửi lúc', 'Lý do', 'Hạn đề xuất', '']
                                            ).map((h) => (
                                                <th key={h || 'a'} style={thBase}>{h}</th>
                                            ))}
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {items.map((row) => {
                                            const id = row.id ?? row.Id;
                                            if (orderSourceTab === 'compliance') {
                                                return (
                                                    <tr key={id}>
                                                        <td style={tdBase}>{urgencyBadge(urgencyKeyFromRow(row))}</td>
                                                        <td style={tdBase}>{row.__reqType === 'ADMIN_ACTION' ? 'Yêu cầu xử lý tài khoản' : 'Yêu cầu trả đơn về hàng đợi'}</td>
                                                        <td style={tdBase}>{row.storyTitle ?? row.story?.title ?? '—'}</td>
                                                        <td style={tdBase}>{row.requesterDisplayName ?? row.requesterEmail ?? '—'}</td>
                                                        <td style={{ ...tdBase, fontSize: '0.75rem' }}>
                                                            {row.__reqType === 'ADMIN_ACTION'
                                                                ? (String(row.requestKind ?? '').toUpperCase() === 'BAN_USER' ? 'Chặn tài khoản' : 'Tạm đình chỉ quyền viết')
                                                                : 'Trả đơn truyện về hàng đợi'}
                                                        </td>
                                                        <td style={{ ...tdBase, fontSize: '0.75rem', whiteSpace: 'nowrap' }}>
                                                            {formatApiDateTimeVietnamVi(row.createdAtUtc ?? row.created_at)}
                                                        </td>
                                                        <td style={{ ...tdBase, maxWidth: 280, wordBreak: 'break-word', fontSize: '0.75rem' }}>
                                                            {row.message || '—'}
                                                        </td>
                                                        <td style={tdBase}>
                                                            <button
                                                                type="button"
                                                                onClick={() => openComplianceResolve(row)}
                                                                style={{
                                                                    padding: '0.5rem 0.875rem',
                                                                    fontSize: '0.8125rem',
                                                                    fontWeight: 600,
                                                                    background: '#7c3aed',
                                                                    color: '#fff',
                                                                    border: 'none',
                                                                    borderRadius: 8,
                                                                    cursor: 'pointer',
                                                                }}
                                                            >
                                                                Xử lý
                                                            </button>
                                                        </td>
                                                    </tr>
                                                );
                                            }
                                            return (
                                                <tr key={id}>
                                                    <td style={tdBase}>{urgencyBadge(urgencyKeyFromRow(row))}</td>
                                                    <td style={tdBase}>{targetTypeVi(row)}</td>
                                                    <td style={tdBase}><TargetTitleCell row={row} /></td>
                                                    <td style={tdBase}>{senderDisplayNameVi(row)}</td>
                                                    <td style={tdBase}>{kindShort(row.requestKind ?? row.RequestKind)}</td>
                                                    <td style={{ ...tdBase, fontSize: '0.75rem', whiteSpace: 'nowrap' }}>
                                                        {formatApiDateTimeVietnamVi(
                                                            row.createdAtUtc ??
                                                            row.CreatedAtUtc ??
                                                            row.created_at ??
                                                            row.createdAt ??
                                                            row.CreatedAt
                                                        )}
                                                    </td>
                                                    <td style={{ ...tdBase, maxWidth: 220, wordBreak: 'break-word', fontSize: '0.75rem' }}>{row.reason ?? row.Reason ?? '—'}</td>
                                                    <td style={{ ...tdBase, fontSize: '0.75rem', whiteSpace: 'nowrap' }}>{formatApiDateTimeVietnamVi(row.proposedDeadlineAt ?? row.ProposedDeadlineAt)}</td>
                                                    <td style={tdBase}>
                                                        <button
                                                            type="button"
                                                            onClick={() => openResolve(row)}
                                                            style={{
                                                                padding: '0.5rem 0.875rem',
                                                                fontSize: '0.8125rem',
                                                                fontWeight: 600,
                                                                background: T.green,
                                                                color: '#fff',
                                                                border: 'none',
                                                                borderRadius: 8,
                                                                cursor: 'pointer',
                                                            }}
                                                            onMouseEnter={(e) => { e.currentTarget.style.background = T.greenHover; }}
                                                            onMouseLeave={(e) => { e.currentTarget.style.background = T.green; }}
                                                        >
                                                            Xử lý
                                                        </button>
                                                    </td>
                                                </tr>
                                            );
                                        })}
                                    </tbody>
                                </table>
                            </div>
                        )}
                    </div>
                </div>
            )}

            {mainTab === 'log' && (
                <div style={{ backgroundColor: T.card, borderRadius: '12px', border: `1px solid ${T.border}`, overflow: 'hidden' }}>
                    <div style={{ padding: '1.25rem 1.5rem' }}>
                        <h2 style={{ margin: '0 0 0.35rem', fontSize: '1.125rem', fontWeight: 600, color: T.title }}>Nhật ký yêu cầu</h2>
                        <p style={{ fontSize: '0.875rem', color: T.slate, margin: '0 0 1.25rem', lineHeight: 1.55 }}>
                            Tra cứu các yêu cầu đã tạo. Có thể lọc theo trạng thái, loại yêu cầu, đích (STORY/CHAPTER), người gửi/xử lý và thời gian tạo/xử lý.
                        </p>

                        <div
                            style={{
                                background: T.bg,
                                borderRadius: '12px',
                                border: `1px solid ${T.border}`,
                                padding: '1rem',
                                marginBottom: '1rem',
                            }}
                        >
                            <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '0.75rem', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
                                <p style={{ fontSize: '0.75rem', fontWeight: 700, color: T.slate, textTransform: 'uppercase', letterSpacing: '0.06em', margin: 0 }}>Bộ lọc</p>
                                <button
                                    type="button"
                                    onClick={() => setShowLogAdvancedFilters((v) => !v)}
                                    style={{
                                        padding: '0.35rem 0.75rem',
                                        fontSize: '0.8125rem',
                                        fontWeight: 600,
                                        borderRadius: 9999,
                                        border: `1px solid ${T.border}`,
                                        background: T.card,
                                        color: T.title,
                                        cursor: 'pointer',
                                    }}
                                >
                                    {showLogAdvancedFilters ? 'Thu gọn' : 'Mở rộng'}
                                </button>
                            </div>

                            {/* Quick filters */}
                            <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr 1fr 0.8fr', gap: '0.75rem', marginBottom: '0.75rem' }}>
                                <label style={{ fontSize: '0.75rem', fontWeight: 700, color: T.slateDark }}>
                                    Tìm kiếm
                                    <input value={logSearch} onChange={(e) => setLogSearch(e.target.value)} placeholder="Lý do, tiêu đề, GUID" style={{ ...inputBase, marginTop: 6 }} />
                                </label>
                                <label style={{ fontSize: '0.75rem', fontWeight: 700, color: T.slateDark }}>
                                    Trạng thái
                                    <select value={logStatus} onChange={(e) => setLogStatus(e.target.value)} style={{ ...inputBase, marginTop: 6 }}>
                                        <option value="">Tất cả</option>
                                        <option value="PENDING">PENDING</option>
                                        <option value="APPROVED">APPROVED</option>
                                        <option value="REJECTED">REJECTED</option>
                                    </select>
                                </label>
                                <label style={{ fontSize: '0.75rem', fontWeight: 700, color: T.slateDark }}>
                                    Loại
                                    <select value={logRequestKind} onChange={(e) => setLogRequestKind(e.target.value)} style={{ ...inputBase, marginTop: 6 }}>
                                        <option value="">Tất cả</option>
                                        <option value="EXTEND_DEADLINE">Gia hạn</option>
                                        <option value="RELEASE_ASSIGNMENT">Hủy nhận</option>
                                    </select>
                                </label>
                                <label style={{ fontSize: '0.75rem', fontWeight: 700, color: T.slateDark }}>
                                    Đích
                                    <select value={logTargetType} onChange={(e) => setLogTargetType(e.target.value)} style={{ ...inputBase, marginTop: 6 }}>
                                        <option value="">Tất cả</option>
                                        <option value="STORY">STORY</option>
                                        <option value="CHAPTER">CHAPTER</option>
                                    </select>
                                </label>
                                <label style={{ fontSize: '0.75rem', fontWeight: 700, color: T.slateDark }}>
                                    / trang
                                    <select value={logPageSize} onChange={(e) => setLogPageSize(Number(e.target.value))} style={{ ...inputBase, marginTop: 6 }}>
                                        <option value={20}>20</option>
                                        <option value={50}>50</option>
                                        <option value={100}>100</option>
                                    </select>
                                </label>
                            </div>

                            <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.75rem', alignItems: 'flex-end' }}>
                                <label style={{ fontSize: '0.75rem', fontWeight: 700, color: T.slateDark }}>
                                    Sắp xếp
                                    <select value={logSortBy} onChange={(e) => setLogSortBy(e.target.value)} style={{ ...inputBase, marginTop: 6, minWidth: 180 }}>
                                        <option value="created_at">Thời gian tạo</option>
                                        <option value="resolved_at">Thời gian xử lý</option>
                                    </select>
                                </label>
                                <label style={{ fontSize: '0.75rem', fontWeight: 700, color: T.slateDark }}>
                                    Thứ tự
                                    <select value={logSortOrder} onChange={(e) => setLogSortOrder(e.target.value)} style={{ ...inputBase, marginTop: 6, minWidth: 180 }}>
                                        <option value="desc">Mới nhất trước</option>
                                        <option value="asc">Cũ nhất trước</option>
                                    </select>
                                </label>
                                <button
                                    type="button"
                                    onClick={applyLogFilters}
                                    style={{
                                        display: 'inline-flex',
                                        alignItems: 'center',
                                        gap: 8,
                                        padding: '0.55rem 1rem',
                                        fontWeight: 700,
                                        fontSize: '0.875rem',
                                        borderRadius: 10,
                                        border: 'none',
                                        background: T.green,
                                        color: '#fff',
                                        cursor: 'pointer',
                                    }}
                                >
                                    <ListFilter style={{ width: 16, height: 16 }} />
                                    Áp dụng
                                </button>
                            </div>

                            {/* Advanced filters (still full functionality) */}
                            {showLogAdvancedFilters && (
                                <div style={{ marginTop: '0.9rem', paddingTop: '0.9rem', borderTop: `1px dashed ${T.border}` }}>
                                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(180px, 1fr))', gap: '0.75rem' }}>
                                        <label style={{ fontSize: '0.75rem', fontWeight: 700, color: T.slateDark }}>
                                            Người gửi (GUID)
                                            <input value={logSenderId} onChange={(e) => setLogSenderId(e.target.value)} placeholder="senderId" style={{ ...inputBase, marginTop: 6 }} />
                                        </label>
                                        <label style={{ fontSize: '0.75rem', fontWeight: 700, color: T.slateDark }}>
                                            Người xử lý (GUID)
                                            <input value={logResolverId} onChange={(e) => setLogResolverId(e.target.value)} placeholder="resolverId" style={{ ...inputBase, marginTop: 6 }} />
                                        </label>
                                        <label style={{ fontSize: '0.75rem', fontWeight: 700, color: T.slateDark }}>
                                            Tạo từ
                                            <input type="datetime-local" value={logCreatedFrom} onChange={(e) => setLogCreatedFrom(e.target.value)} style={{ ...inputBase, marginTop: 6 }} />
                                        </label>
                                        <label style={{ fontSize: '0.75rem', fontWeight: 700, color: T.slateDark }}>
                                            Tạo đến
                                            <input type="datetime-local" value={logCreatedTo} onChange={(e) => setLogCreatedTo(e.target.value)} style={{ ...inputBase, marginTop: 6 }} />
                                        </label>
                                        <label style={{ fontSize: '0.75rem', fontWeight: 700, color: T.slateDark }}>
                                            Xử lý từ
                                            <input type="datetime-local" value={logResolvedFrom} onChange={(e) => setLogResolvedFrom(e.target.value)} style={{ ...inputBase, marginTop: 6 }} />
                                        </label>
                                        <label style={{ fontSize: '0.75rem', fontWeight: 700, color: T.slateDark }}>
                                            Xử lý đến
                                            <input type="datetime-local" value={logResolvedTo} onChange={(e) => setLogResolvedTo(e.target.value)} style={{ ...inputBase, marginTop: 6 }} />
                                        </label>
                                    </div>
                                </div>
                            )}
                        </div>

                        <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '0.5rem', marginBottom: '1rem' }}>
                            <button
                                type="button"
                                disabled={!logPageInfo.hasPrev || logLoading}
                                onClick={() => loadLog(logPageInfo.page - 1)}
                                style={{
                                    padding: '0.5rem 0.875rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    borderRadius: 8,
                                    border: `1px solid ${T.border}`,
                                    background: T.card,
                                    cursor: logPageInfo.hasPrev ? 'pointer' : 'not-allowed',
                                    opacity: logPageInfo.hasPrev ? 1 : 0.5,
                                    color: T.title,
                                }}
                            >
                                « Trước
                            </button>
                            <span style={{ fontSize: '0.875rem', color: T.slate }}>
                                Trang {logPageInfo.page} / {logPageInfo.totalPages} · Tổng {logPageInfo.total} bản ghi
                            </span>
                            <button
                                type="button"
                                disabled={!logPageInfo.hasNext || logLoading}
                                onClick={() => loadLog(logPageInfo.page + 1)}
                                style={{
                                    padding: '0.5rem 0.875rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    borderRadius: 8,
                                    border: `1px solid ${T.border}`,
                                    background: T.card,
                                    cursor: logPageInfo.hasNext ? 'pointer' : 'not-allowed',
                                    opacity: logPageInfo.hasNext ? 1 : 0.5,
                                    color: T.title,
                                }}
                            >
                                Sau »
                            </button>
                        </div>
                        {logError && (
                            <div
                                style={{
                                    padding: '1rem 1.25rem',
                                    background: '#fee2e2',
                                    color: '#991b1b',
                                    borderRadius: '12px',
                                    marginBottom: '1rem',
                                    fontSize: '0.875rem',
                                    border: '1px solid #fecaca',
                                }}
                            >
                                {logError}
                            </div>
                        )}
                        {logLoading ? (
                            <div
                                style={{
                                    backgroundColor: '#fafafa',
                                    borderRadius: '12px',
                                    padding: '3rem 2rem',
                                    textAlign: 'center',
                                    border: `1px solid ${T.border}`,
                                }}
                            >
                                <p style={{ fontSize: '0.875rem', color: T.slate, margin: 0 }}>Đang tải log...</p>
                            </div>
                        ) : logItems.length === 0 ? (
                            <p style={{ fontSize: '0.875rem', color: T.slate, margin: 0 }}>Không có bản ghi khớp bộ lọc.</p>
                        ) : (
                            <div style={{ overflowX: 'auto', border: '1px solid #c9f0d8', borderRadius: '12px' }}>
                                <table style={tableBase} className="re-compliance-table">
                                    <thead>
                                        <tr>
                                            {['Id', 'Trạng thái', 'Mức độ', 'Loại', 'Tiêu đề', 'Yêu cầu', 'Người gửi', 'Tạo lúc', 'Người xử lý', 'Xử lý lúc', 'Lý do'].map((h) => (
                                                <th key={h} style={thBase}>{h}</th>
                                            ))}
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {logItems.map((x) => {
                                            const id = x.id ?? x.Id;
                                            const idShort = String(id).length > 12 ? `${String(id).slice(0, 8)}…` : String(id);
                                            const reason = truncate(x.reason ?? x.Reason ?? '', 120);
                                            return (
                                                <tr key={id}>
                                                    <td style={{ ...tdBase, fontSize: '0.75rem', color: T.slate }} title={String(id)}>{idShort}</td>
                                                    <td style={tdBase}>{logStatusBadge(x.status ?? x.Status)}</td>
                                                    <td style={tdBase}>{urgencyBadge(urgencyKeyFromRow(x))}</td>
                                                    <td style={tdBase}>{targetTypeVi(x)}</td>
                                                    <td style={tdBase}><TargetTitleCell row={x} /></td>
                                                    <td style={tdBase}>{kindShort(x.requestKind ?? x.RequestKind)}</td>
                                                    <td style={{ ...tdBase, fontSize: '0.75rem' }}>{senderDisplayNameVi(x)}</td>
                                                    <td style={{ ...tdBase, fontSize: '0.75rem', whiteSpace: 'nowrap' }}>{formatApiDateTimeVietnamVi(x.createdAt ?? x.CreatedAt)}</td>
                                                    <td style={{ ...tdBase, fontSize: '0.75rem' }}>{x.resolverName ?? x.ResolverName ?? '—'}</td>
                                                    <td style={{ ...tdBase, fontSize: '0.75rem', whiteSpace: 'nowrap' }}>{formatApiDateTimeVietnamVi(x.resolvedAt ?? x.ResolvedAt)}</td>
                                                    <td style={{ ...tdBase, fontSize: '0.75rem', maxWidth: 180 }}>{reason || '—'}</td>
                                                </tr>
                                            );
                                        })}
                                    </tbody>
                                </table>
                            </div>
                        )}
                    </div>
                </div>
            )}

            {resolveRow && (
                <div
                    style={{
                        position: 'fixed',
                        inset: 0,
                        backgroundColor: 'rgba(0, 0, 0, 0.5)',
                        zIndex: 10050,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        padding: '1rem',
                    }}
                    onClick={() => !resolving && closeResolveModal()}
                >
                    <div
                        style={{
                            background: T.card,
                            borderRadius: '12px',
                            maxWidth: 720,
                            width: '100%',
                            maxHeight: '85vh',
                            overflow: 'auto',
                            display: 'flex',
                            flexDirection: 'column',
                            boxShadow: '0 20px 40px rgba(0,0,0,0.15)',
                            border: `1px solid ${T.border}`,
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div style={{ padding: '1rem 1.25rem', borderBottom: `1px solid ${T.border}`, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                            <h3 style={{ margin: 0, fontSize: '1.125rem', fontWeight: 600, color: T.title, display: 'flex', alignItems: 'center', gap: 8 }}>
                                <ShieldCheck style={{ width: 22, height: 22, color: T.green }} />
                                Xác nhận xử lý đơn
                            </h3>
                            <button
                                type="button"
                                onClick={() => !resolving && closeResolveModal()}
                                style={{ background: 'none', border: 'none', fontSize: '1.5rem', cursor: 'pointer', color: T.slate, lineHeight: 1 }}
                                aria-label="Đóng"
                            >
                                ×
                            </button>
                        </div>
                        <div style={{ padding: '1rem 1.25rem' }}>
                            <div style={{ fontSize: '0.875rem', marginBottom: 16, color: T.slateDark, lineHeight: 1.5 }}>
                                <p style={{ margin: '0 0 6px' }}>
                                    <strong>{kindLong(resolveRow.requestKind ?? resolveRow.RequestKind)}</strong>
                                    {' — '}
                                    {targetTypeVi(resolveRow)}
                                    {' — '}
                                    <strong>{resolveRow.targetTitle ?? resolveRow.TargetTitle ?? resolveRow.targetId ?? resolveRow.TargetId}</strong>
                                </p>
                                <p style={{ margin: 0, color: T.slate }}>
                                    Người gửi: {senderDisplayNameVi(resolveRow)} · Thời gian gửi: {formatApiDateTimeVietnamVi(
                                        resolveRow.createdAtUtc ??
                                        resolveRow.CreatedAtUtc ??
                                        resolveRow.created_at ??
                                        resolveRow.createdAt ??
                                        resolveRow.CreatedAt
                                    )}
                                </p>
                                <p style={{ margin: '6px 0 0', color: T.slate }}>
                                    Lý do: {resolveRow.reason ?? resolveRow.Reason ?? '—'}
                                </p>
                            </div>

                            {resolveApiError ? (
                                <div
                                    role="alert"
                                    style={{
                                        marginBottom: 16,
                                        padding: '0.75rem 1rem',
                                        borderRadius: 8,
                                        border: '1px solid #fecaca',
                                        background: '#fef2f2',
                                    }}
                                >
                                    <div style={{ fontSize: '0.9375rem', fontWeight: 700, color: '#991b1b', marginBottom: 4 }}>
                                        {resolveApiError.title}
                                    </div>
                                    <div style={{ fontSize: '0.8125rem', color: '#7f1d1d', lineHeight: 1.45 }}>
                                        {resolveApiError.message}
                                    </div>
                                </div>
                            ) : null}

                            {String(resolveRow.targetType ?? resolveRow.TargetType ?? '').toUpperCase() === 'STORY' &&
                                String(resolveRow.requestKind ?? resolveRow.RequestKind ?? '').toUpperCase() === 'RELEASE_ASSIGNMENT' && (
                                    <div style={{ marginBottom: 16 }}>
                                        <div style={{ fontSize: '0.8125rem', fontWeight: 600, color: T.title, marginBottom: 8 }}>
                                            Các chương đang nhận duyệt (phạm vi hủy nhận duyệt)
                                        </div>
                                        {(() => {
                                            const rids = resolveRow.releaseAffectedChapterIds ?? resolveRow.ReleaseAffectedChapterIds;
                                            const needFetch = Array.isArray(rids) && rids.length > 0;
                                            const loading = needFetch && (!resolveStoryChaptersPreview || resolveStoryChaptersPreview.loading);
                                            return loading ? (
                                                <p style={{ fontSize: '0.8125rem', color: T.slate, margin: 0 }}>Đang tải danh sách chương…</p>
                                            ) : null;
                                        })()}
                                        {resolveStoryChaptersPreview?.error && (
                                            <p style={{ fontSize: '0.8125rem', color: '#b91c1c', margin: 0 }}>{resolveStoryChaptersPreview.error}</p>
                                        )}
                                        {resolveStoryChaptersPreview &&
                                            !resolveStoryChaptersPreview.loading &&
                                            !resolveStoryChaptersPreview.error &&
                                            (resolveStoryChaptersPreview.items?.length ?? 0) === 0 && (
                                                <p style={{ fontSize: '0.8125rem', color: T.slate, margin: 0 }}>
                                                    Không có chương nào đang được moderator này nhận duyệt trên truyện này (có thể chỉ còn đơn nhận ở cấp truyện).
                                                </p>
                                            )}
                                        {resolveStoryChaptersPreview &&
                                            !resolveStoryChaptersPreview.loading &&
                                            !resolveStoryChaptersPreview.error &&
                                            (resolveStoryChaptersPreview.items?.length ?? 0) > 0 && (
                                                <div
                                                    style={{
                                                        maxHeight: 280,
                                                        overflow: 'auto',
                                                        border: `1px solid ${T.border}`,
                                                        borderRadius: 8,
                                                    }}
                                                >
                                                    <table style={{ ...tableBase, margin: 0, width: '100%' }} className="re-compliance-table">
                                                        <thead>
                                                            <tr>
                                                                {['#', 'Tiêu đề', 'Trạng thái', 'Số từ', 'Gửi duyệt', 'Hạn (SLA)', 'Người nhận duyệt'].map((h) => (
                                                                    <th key={h} style={{ ...thBase, fontSize: '0.7rem', whiteSpace: 'nowrap', padding: '8px 6px' }}>
                                                                        {h}
                                                                    </th>
                                                                ))}
                                                            </tr>
                                                        </thead>
                                                        <tbody>
                                                            {resolveStoryChaptersPreview.items.map((ch, rowIdx) => {
                                                                const oid = ch.id ?? ch.Id;
                                                                const oiRaw = ch.orderIndex ?? ch.OrderIndex ?? ch.order_index;
                                                                const oiNum = oiRaw != null && oiRaw !== '' ? Number(oiRaw) : NaN;
                                                                const ord = Number.isFinite(oiNum) && oiNum > 0 ? oiNum : rowIdx + 1;
                                                                const fullTitle = (ch.title ?? ch.Title ?? '—').toString();
                                                                const st = chapterStatusVi(ch.status ?? ch.Status);
                                                                const wc = ch.wordCount ?? ch.WordCount;
                                                                const ps = ch.pendingSince ?? ch.PendingSince;
                                                                const dl = ch.deadlineAt ?? ch.DeadlineAt;
                                                                const claim = ch.claimedByDisplayName ?? ch.ClaimedByDisplayName;
                                                                return (
                                                                    <tr key={oid}>
                                                                        <td style={{ ...tdBase, fontSize: '0.72rem', whiteSpace: 'nowrap' }}>{ord}</td>
                                                                        <td
                                                                            style={{
                                                                                ...tdBase,
                                                                                fontSize: '0.72rem',
                                                                                maxWidth: 200,
                                                                                overflow: 'hidden',
                                                                                textOverflow: 'ellipsis',
                                                                                whiteSpace: 'nowrap',
                                                                            }}
                                                                            title={fullTitle}
                                                                        >
                                                                            {fullTitle}
                                                                        </td>
                                                                        <td style={{ ...tdBase, fontSize: '0.72rem', whiteSpace: 'nowrap' }}>{st}</td>
                                                                        <td style={{ ...tdBase, fontSize: '0.72rem', whiteSpace: 'nowrap' }}>
                                                                            {wc != null && wc !== '' ? Number(wc).toLocaleString('vi-VN') : '—'}
                                                                        </td>
                                                                        <td style={{ ...tdBase, fontSize: '0.7rem', whiteSpace: 'nowrap' }}>
                                                                            {formatApiDateTimeVietnamVi(ps)}
                                                                        </td>
                                                                        <td style={{ ...tdBase, fontSize: '0.7rem', whiteSpace: 'nowrap' }}>
                                                                            {formatApiDateTimeVietnamVi(dl)}
                                                                        </td>
                                                                        <td style={{ ...tdBase, fontSize: '0.72rem', maxWidth: 140, wordBreak: 'break-word' }}>
                                                                            {claim || '—'}
                                                                        </td>
                                                                    </tr>
                                                                );
                                                            })}
                                                        </tbody>
                                                    </table>
                                                </div>
                                            )}
                                    </div>
                                )}

                            {String(resolveRow.targetType ?? resolveRow.TargetType ?? '').toUpperCase() === 'CHAPTER' && (
                                <div style={{ marginBottom: 16 }}>
                                    {(() => {
                                        const kindExt = String(resolveRow.requestKind ?? resolveRow.RequestKind ?? '').toUpperCase().includes('EXTEND');
                                        const d = resolveChapterPreview?.data;
                                        const pending = d ? (d.pendingVersions ?? d.PendingVersions ?? []) : [];
                                        const hasPv = Boolean(d && (d.hasPendingVersion ?? d.HasPendingVersion)) && pending.length > 0;
                                        const extendVersionOnly = kindExt && hasPv;
                                        return (
                                            <>
                                                <div style={{ fontSize: '0.8125rem', fontWeight: 600, color: T.title, marginBottom: 8 }}>
                                                    {extendVersionOnly
                                                        ? 'Phiên bản chờ duyệt (đơn gia hạn)'
                                                        : 'Nội dung chương'}
                                                </div>
                                                {extendVersionOnly ? (
                                                    <p style={{ fontSize: '0.72rem', color: T.slate, margin: '0 0 8px', lineHeight: 1.45 }}>
                                                        Chỉ hiển thị nội dung snapshot của phiên bản chỉnh sửa — không hiển thị bản gốc đã xuất bản.
                                                    </p>
                                                ) : null}
                                            </>
                                        );
                                    })()}
                                    {resolveChapterPreview?.loading && (
                                        <p style={{ fontSize: '0.8125rem', color: T.slate, margin: 0 }}>Đang tải nội dung…</p>
                                    )}
                                    {resolveChapterPreview?.error && (
                                        <p style={{ fontSize: '0.8125rem', color: '#b91c1c', margin: 0 }}>{resolveChapterPreview.error}</p>
                                    )}
                                    {resolveChapterPreview?.data && !resolveChapterPreview.loading && (
                                        <div
                                            style={{
                                                maxHeight: 280,
                                                overflow: 'auto',
                                                padding: 12,
                                                background: T.card,
                                                borderRadius: 8,
                                                border: `1px solid ${T.border}`,
                                                fontSize: '0.8125rem',
                                                lineHeight: 1.55,
                                            }}
                                        >
                                            {(() => {
                                                const d = resolveChapterPreview.data;
                                                const origTitle = d.originalTitle ?? d.OriginalTitle ?? '—';
                                                const origContent = (d.originalContent ?? d.OriginalContent ?? '').trim() || '—';
                                                const pending = d.pendingVersions ?? d.PendingVersions ?? [];
                                                const hasPv = Boolean(d.hasPendingVersion ?? d.HasPendingVersion) && pending.length > 0;
                                                const kindExt = String(resolveRow.requestKind ?? resolveRow.RequestKind ?? '').toUpperCase().includes('EXTEND');
                                                const extendVersionOnly = kindExt && hasPv;

                                                const versionBlocks = pending.map((v, vIdx) => (
                                                    <div
                                                        key={v.id ?? v.Id}
                                                        style={{
                                                            marginTop: extendVersionOnly ? (vIdx === 0 ? 0 : 12) : 12,
                                                            paddingTop: extendVersionOnly ? (vIdx === 0 ? 0 : 12) : 12,
                                                            borderTop: extendVersionOnly
                                                                ? vIdx === 0
                                                                    ? 'none'
                                                                    : `1px dashed ${T.border}`
                                                                : `1px dashed ${T.border}`,
                                                        }}
                                                    >
                                                        <div style={{ fontWeight: 600, color: T.slate, marginBottom: 4 }}>
                                                            Phiên bản chỉnh sửa #{v.versionNumber ?? v.VersionNumber}
                                                        </div>
                                                        <div style={{ fontWeight: 600, color: T.title, marginBottom: 6 }}>
                                                            {v.titleSnapshot ?? v.TitleSnapshot ?? '—'}
                                                        </div>
                                                        <div style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word', color: T.title }}>
                                                            {(v.contentSnapshot ?? v.ContentSnapshot ?? '').trim() || '—'}
                                                        </div>
                                                    </div>
                                                ));

                                                if (extendVersionOnly) {
                                                    return <>{versionBlocks}</>;
                                                }

                                                return (
                                                    <>
                                                        <div style={{ marginBottom: hasPv ? 12 : 0 }}>
                                                            <div style={{ fontWeight: 600, color: T.slate, marginBottom: 4 }}>Bản đang duyệt</div>
                                                            <div style={{ fontWeight: 600, color: T.title, marginBottom: 6 }}>{origTitle}</div>
                                                            <div style={{ whiteSpace: 'pre-wrap', wordBreak: 'break-word', color: T.title }}>{origContent}</div>
                                                        </div>
                                                        {hasPv ? versionBlocks : null}
                                                    </>
                                                );
                                            })()}
                                        </div>
                                    )}
                                </div>
                            )}

                            {String(resolveRow.requestKind ?? resolveRow.RequestKind ?? '').toUpperCase().includes('EXTEND') && (
                                <div style={{ marginBottom: 16 }}>
                                    {(() => {
                                        const extendReadonlyBox = {
                                            padding: '0.5rem 0.75rem',
                                            borderRadius: 8,
                                            border: `1px solid ${T.border}`,
                                            background: '#f1f5f9',
                                            fontSize: '0.875rem',
                                            color: T.title,
                                            cursor: 'default',
                                        };
                                        const currentDl =
                                            resolveRow.currentAssignmentDeadlineAt
                                            ?? resolveRow.CurrentAssignmentDeadlineAt
                                            ?? resolveRow.assignmentDeadlineAt
                                            ?? resolveRow.AssignmentDeadlineAt
                                            ?? resolveRow.reviewDeadlineAt
                                            ?? resolveRow.ReviewDeadlineAt;
                                        const currentDlText = formatApiDateTimeVietnamVi(currentDl);
                                        return (
                                            <>
                                                <div style={{ fontSize: '0.8125rem', fontWeight: 600, color: T.title, marginBottom: 6 }}>
                                                    Hạn duyệt hiện tại của kiểm duyệt viên
                                                </div>
                                                <div aria-readonly="true" style={extendReadonlyBox}>
                                                    {currentDlText && String(currentDlText).trim() !== '' ? currentDlText : '—'}
                                                </div>
                                                <p style={{ fontSize: '0.75rem', color: T.slate, margin: '6px 0 12px' }}>
                                                    Theo hạn kiểm duyệt viên đang giữ — không chỉnh sửa.
                                                </p>
                                                <div style={{ fontSize: '0.8125rem', fontWeight: 600, color: T.title, marginBottom: 6 }}>Hạn duyệt sau khi chấp nhận</div>
                                                <div aria-readonly="true" style={extendReadonlyBox}>
                                                    {formatApiDateTimeVietnamVi(resolveRow.proposedDeadlineAt ?? resolveRow.ProposedDeadlineAt)}
                                                </div>
                                                <p style={{ fontSize: '0.75rem', color: T.slate, margin: '6px 0 0' }}>Theo hạn kiểm duyệt viên đề xuất — không chỉnh sửa.</p>
                                            </>
                                        );
                                    })()}
                                </div>
                            )}

                            <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, color: T.title }}>
                                Ghi chú quản trị viên
                                <span style={{ fontWeight: 500, color: T.slate, marginLeft: 6 }}>
                                    (tùy chọn khi chấp nhận; bắt buộc khi từ chối — tối thiểu {ADMIN_REJECT_NOTE_MIN_WORDS} từ)
                                </span>
                                <textarea
                                    value={adminNote}
                                    onChange={(e) => {
                                        setAdminNote(e.target.value);
                                        if (adminNoteRejectError) setAdminNoteRejectError('');
                                        if (resolveApiError) setResolveApiError(null);
                                    }}
                                    rows={3}
                                    placeholder="Nhập ghi chú xử lý..."
                                    style={{
                                        ...inputBase,
                                        marginTop: 6,
                                        fontFamily: 'inherit',
                                        minHeight: '4.5rem',
                                        borderColor: adminNoteRejectError ? '#ef4444' : T.border,
                                    }}
                                />
                                {adminNoteRejectError ? (
                                    <p style={{ fontSize: '0.75rem', color: '#b91c1c', margin: '6px 0 0', fontWeight: 600 }} role="alert">
                                        {adminNoteRejectError}
                                    </p>
                                ) : null}
                            </label>
                        </div>
                        <div style={{ padding: '0.75rem 1.25rem 1.25rem', display: 'flex', flexWrap: 'wrap', gap: 10, justifyContent: 'flex-end', borderTop: `1px solid ${T.border}`, background: T.bg }}>
                            <button type="button" disabled={resolving} onClick={closeResolveModal} style={{ padding: '0.5rem 1rem', borderRadius: 8, border: `1px solid ${T.border}`, background: T.card, fontWeight: 600, cursor: 'pointer', color: T.title }}>
                                Đóng
                            </button>
                            <button
                                type="button"
                                disabled={resolving}
                                onClick={submitReject}
                                style={{ padding: '0.5rem 1rem', borderRadius: 8, border: 'none', background: '#ef4444', color: '#fff', fontWeight: 700, cursor: 'pointer' }}
                            >
                                Từ chối
                            </button>
                            <button
                                type="button"
                                disabled={resolving}
                                onClick={submitApprove}
                                style={{ padding: '0.5rem 1rem', borderRadius: 8, border: 'none', background: T.green, color: '#fff', fontWeight: 700, cursor: 'pointer' }}
                                onMouseEnter={(e) => { if (!resolving) e.currentTarget.style.background = T.greenHover; }}
                                onMouseLeave={(e) => { e.currentTarget.style.background = T.green; }}
                            >
                                Chấp nhận
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {complianceResolveRow && (
                <div
                    style={{
                        position: 'fixed', inset: 0, backgroundColor: 'rgba(0, 0, 0, 0.5)', zIndex: 10050,
                        display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '1rem',
                    }}
                    onClick={() => !complianceResolving && closeComplianceResolve()}
                >
                    <div
                        style={{
                            background: T.card, borderRadius: '12px', maxWidth: 620, width: '100%', maxHeight: '85vh',
                            overflow: 'auto', display: 'flex', flexDirection: 'column', boxShadow: '0 20px 40px rgba(0,0,0,0.15)', border: `1px solid ${T.border}`,
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div style={{ padding: '1rem 1.25rem', borderBottom: `1px solid ${T.border}`, display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                            <h3 style={{ margin: 0, fontSize: '1.125rem', fontWeight: 600, color: T.title, display: 'flex', alignItems: 'center', gap: 8 }}>
                                <ShieldCheck style={{ width: 22, height: 22, color: T.green }} />
                                {complianceResolveRow.__reqType === 'ADMIN_ACTION'
                                    ? (String(complianceResolveRow.requestKind ?? '').toUpperCase() === 'BAN_USER'
                                        ? 'Xử lý đơn chặn tài khoản'
                                        : 'Xử lý đơn tạm đình chỉ quyền viết')
                                    : 'Xử lý đơn trả về hàng đợi từ xử lý vi phạm viên'}
                            </h3>
                            <button type="button" onClick={closeComplianceResolve} style={{ background: 'none', border: 'none', fontSize: '1.5rem', cursor: 'pointer', color: T.slate, lineHeight: 1 }}>×</button>
                        </div>
                        <div style={{ padding: '1rem 1.25rem', display: 'grid', gap: 10 }}>
                            <div
                                style={{
                                    fontSize: '0.875rem',
                                    color: T.slateDark,
                                    lineHeight: 1.55,
                                    display: 'grid',
                                    gap: 8,
                                    padding: '0.75rem 0.875rem',
                                    border: `1px solid ${T.border}`,
                                    borderRadius: 10,
                                    background: '#f8fafc',
                                }}
                            >
                                <div>
                                    <span style={{ color: T.slate, fontWeight: 600 }}>Loại yêu cầu:</span>{' '}
                                    <strong>
                                        {complianceResolveRow.__reqType === 'ADMIN_ACTION'
                                            ? 'Yêu cầu xử lý tài khoản'
                                            : 'Yêu cầu trả đơn về hàng đợi'}
                                    </strong>
                                </div>
                                <div>
                                    <span style={{ color: T.slate, fontWeight: 600 }}>Người gửi:</span>{' '}
                                    {complianceResolveRow.requesterDisplayName ?? complianceResolveRow.requesterEmail ?? '—'}
                                    {' · '}
                                    <span style={{ color: T.slate, fontWeight: 600 }}>Thời gian gửi:</span>{' '}
                                    {formatApiDateTimeVietnamVi(
                                        complianceResolveRow.createdAtUtc ??
                                        complianceResolveRow.CreatedAtUtc ??
                                        complianceResolveRow.created_at ??
                                        complianceResolveRow.createdAt ??
                                        complianceResolveRow.CreatedAt
                                    )}
                                </div>
                                <div>
                                    <span style={{ color: T.slate, fontWeight: 600 }}>Tài khoản mục tiêu:</span>{' '}
                                    <strong style={{ color: T.slateDark }}>
                                        {complianceResolveRow.targetUserDisplayName
                                            ?? complianceResolveRow.TargetUserDisplayName
                                            ?? '—'}
                                    </strong>
                                    {' · '}
                                    <span style={{ color: T.slate, fontWeight: 600 }}>Email:</span>{' '}
                                    {complianceResolveRow.targetUserEmail
                                        ?? complianceResolveRow.TargetUserEmail
                                        ?? '—'}
                                </div>
                                <div>
                                    <span style={{ color: T.slate, fontWeight: 600 }}>Lý do:</span>{' '}
                                    {sanitizeComplianceReasonText(complianceResolveRow.message ?? complianceResolveRow.Message)}
                                </div>
                            </div>
                            {resolveApiError ? (
                                <div role="alert" style={{ padding: '0.75rem 1rem', borderRadius: 8, border: '1px solid #fecaca', background: '#fef2f2' }}>
                                    <div style={{ fontSize: '0.9375rem', fontWeight: 700, color: '#991b1b', marginBottom: 4 }}>{resolveApiError.title}</div>
                                    <div style={{ fontSize: '0.8125rem', color: '#7f1d1d', lineHeight: 1.45 }}>{resolveApiError.message}</div>
                                </div>
                            ) : null}
                            {complianceResolveRow.__reqType === 'ADMIN_ACTION'
                                && String(complianceResolveRow.requestKind ?? '').toUpperCase() === 'SUSPEND_AUTHOR_WRITING'
                                && (
                                    <label style={{ fontSize: '0.8125rem', fontWeight: 600, color: T.title }}>
                                        Thời điểm kết thúc đình chỉ
                                        <input
                                            type="datetime-local"
                                            value={complianceSuspendUntilUtc}
                                            readOnly
                                            style={inputBase}
                                        />
                                    </label>
                                )}
                            <label style={{ fontSize: '0.8125rem', fontWeight: 600, color: T.title }}>
                                Ghi chú quản trị viên
                                <span style={{ marginLeft: 6, color: T.slate, fontWeight: 500 }}>
                                    (tùy chọn khi chấp nhận; bắt buộc khi từ chối — tối thiểu {ADMIN_REJECT_NOTE_MIN_WORDS} từ)
                                </span>
                                <textarea
                                    value={complianceAdminNote}
                                    onChange={(e) => {
                                        setComplianceAdminNote(e.target.value);
                                        if (complianceAdminNoteRejectError) setComplianceAdminNoteRejectError('');
                                        if (resolveApiError) setResolveApiError(null);
                                    }}
                                    rows={3}
                                    placeholder="Nhập ghi chú xử lý..."
                                    style={{
                                        ...inputBase,
                                        marginTop: 6,
                                        fontFamily: 'inherit',
                                        minHeight: '4.5rem',
                                        borderColor: complianceAdminNoteRejectError ? '#ef4444' : T.border,
                                    }}
                                />
                                {complianceAdminNoteRejectError ? (
                                    <p style={{ fontSize: '0.75rem', color: '#b91c1c', margin: '6px 0 0', fontWeight: 600 }} role="alert">
                                        {complianceAdminNoteRejectError}
                                    </p>
                                ) : null}
                            </label>
                        </div>
                        <div style={{ padding: '0.75rem 1.25rem 1.25rem', display: 'flex', justifyContent: 'flex-end', gap: 10, borderTop: `1px solid ${T.border}`, background: T.bg }}>
                            <button type="button" disabled={complianceResolving} onClick={closeComplianceResolve} style={{ padding: '0.5rem 1rem', borderRadius: 8, border: `1px solid ${T.border}`, background: T.card, fontWeight: 600, cursor: 'pointer', color: T.title }}>
                                Đóng
                            </button>
                            <button
                                type="button"
                                disabled={complianceResolving}
                                onClick={() => submitComplianceResolve('REJECT')}
                                style={{ padding: '0.5rem 1rem', borderRadius: 8, border: 'none', background: '#ef4444', color: '#fff', fontWeight: 700, cursor: 'pointer' }}
                            >
                                Từ chối
                            </button>
                            <button
                                type="button"
                                disabled={complianceResolving}
                                onClick={() => submitComplianceResolve(complianceResolveRow.__reqType === 'ADMIN_ACTION' ? 'APPROVE' : 'APPROVE_UNLOCK')}
                                style={{ padding: '0.5rem 1rem', borderRadius: 8, border: 'none', background: T.green, color: '#fff', fontWeight: 700, cursor: 'pointer' }}
                            >
                                {complianceResolving ? 'Đang xử lý...' : 'Chấp nhận'}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
