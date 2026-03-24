import { useCallback, useEffect, useMemo, useState } from 'react';
import {
    RotateCcw,
    X,
    Info,
    Globe,
    GlobeLock,
    Undo2,
    CheckCheck,
    Flag,
    FlagOff,
    MessageSquare,
    MessageSquareOff,
    ShieldAlert,
    LockOpen,
    History,
    ClipboardList,
} from 'lucide-react';
import { Pagination } from '../../../components/pagination/Pagination';
import { useAuth } from '../../../contexts/AuthContext';
import { getStoryById } from '../../../api/story/storyApi';
import { resolveBackendUrl } from '../../../utils/resolveBackendUrl';
import {
    adminReleaseComplianceCommentClaim,
    adminReleaseComplianceStoryClaim,
    claimComplianceCommentReports,
    claimComplianceStoryReports,
    getAdminComplianceLockRequests,
    getAdminComplianceLogs,
    getAdminComplianceOfficers,
    getAdminCompliancePerformance,
    getComplianceCommentReports,
    getComplianceStoryReports,
    getComplianceUserViolations,
    getMyComplianceAdminActionRequests,
    getMyComplianceLockRequests,
    requestComplianceStoryRelease,
    requestComplianceCommentAdminAction,
    requestComplianceStoryAdminAction,
    resolveAdminComplianceLockRequest,
    resolveAllOpenComplianceCommentReports,
    resolveAllOpenComplianceStoryReports,
    resolveComplianceCommentReport,
    setComplianceCommentThreadHidden,
    setComplianceStoryCommentsDisabled,
    setComplianceStoryFlag,
    setComplianceStoryHidden,
} from '../../../api/admin/adminComplianceApi';

const PAGE_SIZE = 10;

function formatDate(value) {
    if (!value) return '—';
    const raw = String(value).trim();
    // Backend sometimes returns UTC timestamps without timezone suffix.
    // If timezone is missing, force UTC parsing by appending "Z".
    const hasTimezone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(raw);
    const normalized = hasTimezone ? raw : `${raw}Z`;
    const d = new Date(normalized);
    if (Number.isNaN(d.getTime())) return '—';
    return d.toLocaleString('vi-VN', {
        timeZone: 'Asia/Ho_Chi_Minh',
        hour12: false,
    });
}

function readPaged(data) {
    if (Array.isArray(data)) {
        return {
            items: data,
            totalCount: data.length,
            page: 1,
        };
    }
    const queueItems = data?.queueItems ?? data?.QueueItems;
    const rows = data?.rows ?? data?.Rows;
    const items = data?.items ?? data?.Items;
    return {
        items: Array.isArray(items)
            ? items
            : Array.isArray(queueItems)
                ? queueItems
                : Array.isArray(rows)
                    ? rows
                    : [],
        totalCount: Number(data?.totalCount ?? data?.TotalCount ?? 0) || 0,
        page: Number(data?.page ?? data?.Page ?? 1) || 1,
    };
}

function pick(obj, a, b) {
    return obj?.[a] ?? obj?.[b];
}

/** Tránh truthy nhầm từ chuỗi "false" / giá trị lạ từ API. */
function coerceBool(v) {
    if (v === true || v === 1) return true;
    if (v === false || v === 0) return false;
    if (typeof v === 'string') {
        const s = v.trim().toLowerCase();
        if (s === 'true' || s === '1') return true;
        if (s === 'false' || s === '0' || s === '') return false;
    }
    return Boolean(v);
}

function normalizeStoryQueueItem(x) {
    const contributors = pick(x, 'contributors', 'Contributors');
    const openReportIds = pick(x, 'openReportIds', 'OpenReportIds');
    const distinctReasons = pick(x, 'distinctReasonCodes', 'DistinctReasonCodes');
    return {
        storyId: pick(x, 'storyId', 'StoryId'),
        storyTitle: pick(x, 'storyTitle', 'StoryTitle'),
        authorId: pick(x, 'authorId', 'AuthorId') ?? null,
        authorDisplayName: pick(x, 'authorDisplayName', 'AuthorDisplayName'),
        reportCount: Number(pick(x, 'reportCount', 'ReportCount') ?? 0) || 0,
        priorityScore: Number(pick(x, 'priorityScore', 'PriorityScore') ?? 0) || 0,
        maxSeverityScore: Number(pick(x, 'maxSeverityScore', 'MaxSeverityScore') ?? 0) || 0,
        timeWeight: Number(pick(x, 'timeWeight', 'TimeWeight') ?? 0) || 0,
        isComplianceLocked: coerceBool(pick(x, 'isComplianceLocked', 'IsComplianceLocked')),
        complianceClaimedByDisplayName: pick(x, 'complianceClaimedByDisplayName', 'ComplianceClaimedByDisplayName'),
        complianceHandlingSlaMessageVi: pick(x, 'complianceHandlingSlaMessageVi', 'ComplianceHandlingSlaMessageVi'),
        complianceFlagged: coerceBool(pick(x, 'complianceFlagged', 'ComplianceFlagged')),
        commentsDisabled: coerceBool(pick(x, 'commentsDisabled', 'CommentsDisabled')),
        complianceHidden: coerceBool(pick(x, 'complianceHidden', 'ComplianceHidden')),
        distinctReasonCodes: Array.isArray(distinctReasons) ? distinctReasons : [],
        contributors: Array.isArray(contributors) ? contributors : [],
        openReportIds: Array.isArray(openReportIds) ? openReportIds : [],
    };
}

function normalizeStoryReportRow(x) {
    return {
        storyId: pick(x, 'storyId', 'StoryId') ?? pick(x, 'targetId', 'TargetId'),
        storyTitle: pick(x, 'storyTitle', 'StoryTitle'),
        authorId: pick(x, 'authorId', 'AuthorId') ?? null,
        severityScore: Number(pick(x, 'severityScore', 'SeverityScore') ?? 0) || 0,
        status: pick(x, 'status', 'Status'),
        createdAtUtc: pick(x, 'createdAtUtc', 'CreatedAtUtc'),
        isComplianceLocked: coerceBool(pick(x, 'isComplianceLocked', 'IsComplianceLocked')),
        complianceClaimedByDisplayName: pick(x, 'complianceClaimedByDisplayName', 'ComplianceClaimedByDisplayName'),
        complianceHandlingSlaMessageVi: pick(x, 'complianceHandlingSlaMessageVi', 'ComplianceHandlingSlaMessageVi'),
        complianceFlagged: coerceBool(pick(x, 'complianceFlagged', 'ComplianceFlagged')),
        commentsDisabled: coerceBool(pick(x, 'commentsDisabled', 'CommentsDisabled')),
        complianceHidden: coerceBool(pick(x, 'complianceHidden', 'ComplianceHidden')),
    };
}

function groupStoryRows(rawRows) {
    const m = new Map();
    for (const row of rawRows) {
        const storyId = row.storyId;
        if (!storyId) continue;
        const prev = m.get(storyId) || {
            storyId,
            storyTitle: row.storyTitle || '—',
            authorId: row.authorId ?? null,
            authorDisplayName: row.authorDisplayName || null,
            reportCount: 0,
            priorityScore: 0,
            maxSeverityScore: 0,
            timeWeight: 0,
            isComplianceLocked: false,
            complianceClaimedByDisplayName: null,
            complianceHandlingSlaMessageVi: null,
            complianceFlagged: false,
            commentsDisabled: false,
            complianceHidden: false,
            distinctReasonCodes: [],
            contributors: [],
            openReportIds: [],
        };
        prev.reportCount += 1;
        prev.priorityScore = Math.max(prev.priorityScore, row.severityScore || 0);
        prev.maxSeverityScore = Math.max(prev.maxSeverityScore, row.severityScore || 0);
        prev.isComplianceLocked = prev.isComplianceLocked || row.isComplianceLocked;
        prev.complianceClaimedByDisplayName = prev.complianceClaimedByDisplayName || row.complianceClaimedByDisplayName;
        prev.complianceHandlingSlaMessageVi = prev.complianceHandlingSlaMessageVi || row.complianceHandlingSlaMessageVi;
        prev.complianceFlagged = prev.complianceFlagged || row.complianceFlagged;
        prev.commentsDisabled = prev.commentsDisabled || row.commentsDisabled;
        if (row.authorId) prev.authorId = row.authorId;
        // Cùng một truyện: mọi dòng có cùng snapshot DB; gán theo bản ghi cuối để tránh OR khiến cờ "ẩn" bị kẹt true.
        prev.complianceHidden = row.complianceHidden;
        m.set(storyId, prev);
    }
    return Array.from(m.values());
}

function normalizeCommentQueueItem(x) {
    return {
        reportId: pick(x, 'reportId', 'ReportId'),
        commentId: pick(x, 'commentId', 'CommentId'),
        storyTitle: pick(x, 'storyTitle', 'StoryTitle'),
        priorityScore: Number(pick(x, 'priorityScore', 'PriorityScore') ?? 0) || 0,
        status: pick(x, 'status', 'Status'),
        adminOrModeratorReplyWarningVi: pick(x, 'adminOrModeratorReplyWarningVi', 'AdminOrModeratorReplyWarningVi'),
        reasonCode: pick(x, 'reasonCode', 'ReasonCode'),
    };
}

function normalizeStoryTicketRow(x) {
    return {
        reportId: pick(x, 'reportId', 'ReportId') ?? pick(x, 'id', 'Id'),
        reasonCode: pick(x, 'reasonCode', 'ReasonCode'),
        status: pick(x, 'status', 'Status'),
        createdAtUtc: pick(x, 'createdAtUtc', 'CreatedAtUtc'),
        description: pick(x, 'description', 'Description'),
        contributors: pick(x, 'contributors', 'Contributors'),
    };
}

function reasonCodeToViLabel(code) {
    const key = String(code ?? '').trim().toUpperCase();
    const map = {
        HATE_SPEECH: 'Phát ngôn thù ghét / phân biệt',
        SEXUAL_CONTENT: 'Nội dung tình dục / 18+',
        VIOLENCE: 'Nội dung bạo lực',
        ILLEGAL_CONTENT: 'Nội dung vi phạm pháp luật',
        COPYRIGHT: 'Vi phạm bản quyền',
        SPAM: 'Spam / quảng cáo',
        HARASSMENT: 'Quấy rối / xúc phạm',
        MISINFORMATION: 'Thông tin sai lệch',
        OTHER: 'Khác',
    };
    return map[key] || key || 'Khác';
}

function getContributorLabel(c) {
    if (!c) return 'Ẩn danh';
    return c.userEmail || c.UserEmail || c.userName || c.UserName || c.userId || c.UserId || 'Ẩn danh';
}

function collectContributors(selectedStory, storyTickets) {
    const fromStory = Array.isArray(selectedStory?.contributors) ? selectedStory.contributors : [];
    const fromTickets = (Array.isArray(storyTickets) ? storyTickets : [])
        .flatMap((t) => (Array.isArray(t?.contributors ?? t?.Contributors) ? (t.contributors ?? t.Contributors) : []));
    const all = [...fromStory, ...fromTickets];
    const map = new Map();
    for (const c of all) {
        const key = String(c?.userId ?? c?.UserId ?? c?.userEmail ?? c?.UserEmail ?? Math.random());
        if (!map.has(key)) map.set(key, c);
        else {
            const prev = map.get(key);
            const prevDesc = prev?.description ?? prev?.Description;
            const nextDesc = c?.description ?? c?.Description;
            if (!prevDesc && nextDesc) map.set(key, { ...prev, description: nextDesc });
        }
    }

    // Fallback: nếu contributors không có description thì lấy description từ từng ticket
    // và gán cho contributor đầu tiên còn thiếu mô tả.
    if (map.size > 0 && Array.isArray(storyTickets) && storyTickets.length > 0) {
        const firstKey = map.keys().next().value;
        if (firstKey) {
            const cur = map.get(firstKey);
            const hasDesc = cur?.description ?? cur?.Description;
            if (!hasDesc) {
                const rowDesc = storyTickets
                    .map((t) => t?.description ?? t?.Description)
                    .find((d) => String(d ?? '').trim().length > 0);
                if (rowDesc) map.set(firstKey, { ...cur, description: rowDesc });
            }
        }
    }
    return Array.from(map.values());
}

function statusViLabel(status) {
    const s = String(status ?? '').trim().toUpperCase();
    if (s === 'NEW') return 'Mới';
    if (s === 'IN_REVIEW') return 'Đang xử lý';
    if (s === 'RESOLVED') return 'Đã xử lý';
    if (s === 'DISMISSED') return 'Bỏ qua';
    return status || '—';
}

function penaltyTypeVi(p) {
    const u = String(p ?? '').trim().toUpperCase();
    const map = {
        COMMENTS_DISABLED: 'Khóa bình luận',
        COMMENTS_ENABLED: 'Mở bình luận',
        STORY_HIDDEN_COMPLIANCE: 'Ẩn truyện (compliance)',
        STORY_UNHIDDEN_COMPLIANCE: 'Hiện lại truyện',
        BAN: 'Chặn tài khoản (admin)',
        SUSPEND_AUTHOR_WRITING: 'Tạm đình chỉ quyền viết',
    };
    return map[u] || (p || '—');
}

function complianceRequestStatusVi(s) {
    const u = String(s ?? '').trim().toUpperCase();
    if (u === 'PENDING') return 'Chờ xử lý';
    if (u === 'APPROVED') return 'Đã chấp nhận';
    if (u === 'REJECTED') return 'Từ chối';
    return s || '—';
}

function complianceAdminActionKindVi(k) {
    const u = String(k ?? '').trim().toUpperCase();
    if (u === 'BAN_USER') return 'Chặn tài khoản';
    if (u === 'SUSPEND_AUTHOR_WRITING') return 'Tạm đình chỉ quyền viết';
    return k || '—';
}

function Modal({ title, onClose, children, maxWidth = 1100 }) {
    return (
        <div className="fixed inset-0 z-[1200] bg-slate-900/45 flex items-center justify-center p-3">
            <div className="bg-white w-[96vw] max-h-[88vh] rounded-xl border border-slate-200 shadow-2xl flex flex-col" style={{ maxWidth }}>
                <div className="flex justify-between items-center border-b border-slate-200 px-4 py-3">
                    <h3 className="m-0 text-base font-bold text-slate-900">{title}</h3>
                    <button className="inline-flex items-center justify-center w-8 h-8 rounded-lg border border-slate-300 bg-white text-slate-700 hover:bg-slate-50" onClick={onClose}>
                        <X style={{ width: 16, height: 16 }} />
                    </button>
                </div>
                <div className="p-4 overflow-auto">{children}</div>
            </div>
        </div>
    );
}

export default function ViolationManagement() {
    const { role, user } = useAuth();
    const roleUpper = String(role ?? '').toUpperCase();
    const isAdmin = roleUpper === 'ADMIN';
    const tabs = useMemo(() => {
        const base = [
            { id: 'story-reports', label: 'Report truyện' },
            { id: 'comment-reports', label: 'Report bình luận' },
            { id: 'compliance-logs', label: 'Compliance log' },
            { id: 'compliance-performance', label: 'Hiệu suất compliance' },
        ];
        if (isAdmin) base.splice(2, 0, { id: 'lock-requests', label: 'Yêu cầu gỡ lock' });
        return base;
    }, [isAdmin]);

    const [activeTab, setActiveTab] = useState('story-reports');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(null);
    const [rows, setRows] = useState([]);
    const [totalCount, setTotalCount] = useState(0);
    const [currentPage, setCurrentPage] = useState(1);
    const [filters, setFilters] = useState({ search: '', statuses: 'NEW,IN_REVIEW', flaggedOnly: false });
    const [selectedStory, setSelectedStory] = useState(null);
    const [storyTickets, setStoryTickets] = useState([]);
    const [storyTicketLoading, setStoryTicketLoading] = useState(false);
    const [selectedComment, setSelectedComment] = useState(null);
    const [actionModal, setActionModal] = useState(null);
    const [adminActionForm, setAdminActionForm] = useState({ requestKind: 'BAN_USER', message: '', proposedSuspendUntilUtc: '' });
    const [adminActionError, setAdminActionError] = useState('');
    const [adminActionSubmitting, setAdminActionSubmitting] = useState(false);
    const [officers, setOfficers] = useState([]);
    const [lockResolveForm, setLockResolveForm] = useState({ decision: 'APPROVE_UNLOCK', newAssigneeId: '', adminNote: '' });
    const [showClaimedStoryList, setShowClaimedStoryList] = useState(false);
    const [isClaimPickerOpen, setIsClaimPickerOpen] = useState(false);
    const [claimPickerRows, setClaimPickerRows] = useState([]);
    const [claimPickerLoading, setClaimPickerLoading] = useState(false);
    const [claimPickerStoryMeta, setClaimPickerStoryMeta] = useState({});
    const [storyMetaMap, setStoryMetaMap] = useState({});
    const [releaseConfirmTarget, setReleaseConfirmTarget] = useState(null);
    const [releaseReason, setReleaseReason] = useState('');
    const [releaseFormError, setReleaseFormError] = useState('');
    const [releasingStoryId, setReleasingStoryId] = useState(null);
    const [pendingReleaseByStory, setPendingReleaseByStory] = useState({});
    const [storyActionConfirm, setStoryActionConfirm] = useState(null);
    const [storyActionBusy, setStoryActionBusy] = useState(false);
    const [accountViolationModal, setAccountViolationModal] = useState(null);
    const [accountViolationRows, setAccountViolationRows] = useState([]);
    const [accountViolationLoading, setAccountViolationLoading] = useState(false);
    const [myRequestsModalOpen, setMyRequestsModalOpen] = useState(false);
    const [myLockRequests, setMyLockRequests] = useState([]);
    const [myAdminRequests, setMyAdminRequests] = useState([]);
    const [myRequestsLoading, setMyRequestsLoading] = useState(false);

    const currentUserId = user?.id ?? user?.Id ?? null;
    const releaseStorageKey = useMemo(
        () => `compliance-pending-release-requests:${currentUserId ?? 'anon'}`,
        [currentUserId],
    );

    const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / PAGE_SIZE)), [totalCount]);

    const loadData = useCallback(async (page = 1) => {
        setLoading(true);
        setError(null);
        try {
            let data;
            if (activeTab === 'story-reports') {
                if (!showClaimedStoryList) {
                    // Auto-restore: nếu compliance đã nhận đơn trước đó thì tự hiển thị lại danh sách của tôi.
                    const mineProbe = await getComplianceStoryReports({
                        page: 1,
                        pageSize: 1,
                        groupByStory: true,
                        claimFilter: 'mine',
                        statuses: 'NEW,IN_REVIEW',
                    });
                    const mineProbeItems = readPaged(mineProbe).items;
                    if (Array.isArray(mineProbeItems) && mineProbeItems.length > 0) {
                        setShowClaimedStoryList(true);
                    } else {
                        setRows([]);
                        setTotalCount(0);
                        setCurrentPage(1);
                        setLoading(false);
                        return;
                    }
                }
                data = await getComplianceStoryReports({
                    page, pageSize: PAGE_SIZE, groupByStory: true, sortBy: 'priority_desc',
                    claimFilter: 'mine',
                    statuses: filters.statuses || undefined, search: filters.search || undefined, flaggedOnly: filters.flaggedOnly ? true : undefined,
                });
            } else if (activeTab === 'comment-reports') {
                data = await getComplianceCommentReports({ page, pageSize: PAGE_SIZE, status: filters.statuses || undefined, search: filters.search || undefined });
            } else if (activeTab === 'lock-requests') {
                data = await getAdminComplianceLockRequests({ status: 'PENDING' });
            } else if (activeTab === 'compliance-logs') {
                data = await getAdminComplianceLogs({ page, pageSize: PAGE_SIZE, search: filters.search || undefined, sortBy: 'created_at', sortOrder: 'desc' });
            } else {
                data = await getAdminCompliancePerformance({ page, pageSize: PAGE_SIZE, search: filters.search || undefined, sortBy: 'total', sortOrder: 'desc' });
            }
            const paged = readPaged(data);
            const sourceItems = Array.isArray(paged.items) ? paged.items : [];
            let normalizedItems = activeTab === 'story-reports'
                ? sourceItems.map(normalizeStoryQueueItem)
                : activeTab === 'comment-reports'
                    ? sourceItems.map(normalizeCommentQueueItem)
                    : sourceItems;

            // Fallback: một số runtime trả totalCount > 0 nhưng groupByStory rỗng.
            // Khi đó lấy raw report rows rồi gom nhóm tại FE để vẫn hiển thị queue và nút nhận duyệt.
            if (activeTab === 'story-reports' && normalizedItems.length === 0 && paged.totalCount > 0) {
                const fallback = await getComplianceStoryReports({
                    page: 1,
                    pageSize: 300,
                    groupByStory: false,
                    claimFilter: 'mine',
                    statuses: filters.statuses || undefined,
                    search: filters.search || undefined,
                    sortBy: 'newest',
                });
                const raw = (fallback?.items ?? fallback?.Items ?? []).map(normalizeStoryReportRow);
                normalizedItems = groupStoryRows(raw);
            }
            setRows(normalizedItems);
            setTotalCount(activeTab === 'lock-requests' ? (paged.items?.length ?? 0) : paged.totalCount);
            setCurrentPage(activeTab === 'lock-requests' ? 1 : paged.page);
        } catch (e) {
            setError(e?.response?.data?.message ?? e?.message ?? 'Không tải được dữ liệu vi phạm.');
            setRows([]);
            setTotalCount(0);
            setCurrentPage(1);
        } finally {
            setLoading(false);
        }
    }, [activeTab, filters.flaggedOnly, filters.search, filters.statuses, showClaimedStoryList]);

    useEffect(() => { loadData(1); }, [activeTab, loadData]);
    useEffect(() => {
        if (isAdmin) getAdminComplianceOfficers().then((x) => setOfficers(Array.isArray(x) ? x : x?.items ?? [])).catch(() => setOfficers([]));
    }, [isAdmin]);
    useEffect(() => {
        try {
            const raw = localStorage.getItem(releaseStorageKey);
            const parsed = raw ? JSON.parse(raw) : {};
            setPendingReleaseByStory(parsed && typeof parsed === 'object' ? parsed : {});
        } catch {
            setPendingReleaseByStory({});
        }
    }, [releaseStorageKey]);
    useEffect(() => {
        try {
            localStorage.setItem(releaseStorageKey, JSON.stringify(pendingReleaseByStory));
        } catch {
            // best effort
        }
    }, [pendingReleaseByStory, releaseStorageKey]);

    useEffect(() => {
        if (!isClaimPickerOpen || claimPickerRows.length === 0) return;
        let cancelled = false;
        const storyIds = Array.from(new Set(claimPickerRows.map((x) => x.storyId).filter(Boolean)));
        Promise.all(storyIds.map(async (id) => {
            try {
                const s = await getStoryById(id, { recordView: false });
                const cover = s?.coverImage ?? s?.CoverImage ?? s?.coverImageUrl ?? s?.cover_url ?? s?.coverUrl ?? s?.thumbnailUrl ?? s?.CoverImageUrl ?? null;
                const author = s?.authorName ?? s?.authorDisplayName ?? s?.AuthorName ?? s?.AuthorDisplayName ?? null;
                return [id, { coverUrl: cover ? resolveBackendUrl(cover) : null, authorName: author }];
            } catch {
                return [id, { coverUrl: null, authorName: null }];
            }
        })).then((entries) => {
            if (cancelled) return;
            setClaimPickerStoryMeta((prev) => ({ ...prev, ...Object.fromEntries(entries) }));
        });
        return () => { cancelled = true; };
    }, [isClaimPickerOpen, claimPickerRows]);

    const actionWithReload = async (fn) => {
        try {
            await fn();
            await loadData(currentPage);
        } catch (e) {
            alert(e?.response?.data?.message ?? e?.message ?? 'Thao tác thất bại.');
        }
    };

    const openAccountViolationHistory = async (row) => {
        const userId = row?.authorId;
        if (!userId) {
            alert('Chưa có định danh tác giả trên dòng này; không thể tải lịch sử vi phạm.');
            return;
        }
        setAccountViolationModal({
            userId,
            displayName: row.authorDisplayName || 'Tác giả',
        });
        setAccountViolationLoading(true);
        setAccountViolationRows([]);
        try {
            const data = await getComplianceUserViolations(userId, 80);
            setAccountViolationRows(Array.isArray(data) ? data : []);
        } catch (e) {
            alert(e?.response?.data?.message ?? e?.message ?? 'Không tải được lịch sử vi phạm.');
            setAccountViolationRows([]);
        } finally {
            setAccountViolationLoading(false);
        }
    };

    const openMyRequestsModal = async () => {
        setMyRequestsModalOpen(true);
        setMyRequestsLoading(true);
        try {
            const [locks, actions] = await Promise.all([
                getMyComplianceLockRequests(),
                getMyComplianceAdminActionRequests(),
            ]);
            setMyLockRequests(Array.isArray(locks) ? locks : []);
            setMyAdminRequests(Array.isArray(actions) ? actions : []);
        } catch (e) {
            alert(e?.response?.data?.message ?? e?.message ?? 'Không tải được đơn đã gửi.');
            setMyLockRequests([]);
            setMyAdminRequests([]);
        } finally {
            setMyRequestsLoading(false);
        }
    };

    const openStoryTicketsModal = async (story) => {
        setSelectedStory(story);
        setStoryTicketLoading(true);
        try {
            if (story?.storyId && !storyMetaMap[story.storyId]) {
                try {
                    const s = await getStoryById(story.storyId, { recordView: false });
                    const cover = s?.coverImage ?? s?.CoverImage ?? s?.coverImageUrl ?? s?.cover_url ?? s?.coverUrl ?? s?.thumbnailUrl ?? s?.CoverImageUrl ?? null;
                    const author = s?.authorName ?? s?.authorDisplayName ?? s?.AuthorName ?? s?.AuthorDisplayName ?? null;
                    setStoryMetaMap((prev) => ({
                        ...prev,
                        [story.storyId]: {
                            coverUrl: cover ? resolveBackendUrl(cover) : null,
                            authorName: author ?? null,
                        },
                    }));
                } catch {
                    // best effort
                }
            }
            const data = await getComplianceStoryReports({
                groupByStory: false,
                storyId: story.storyId,
                statuses: 'ALL',
                page: 1,
                pageSize: 200,
                sortBy: 'newest',
            });
            const list = data?.items ?? data?.Items ?? data?.rows ?? data?.Rows ?? [];
            setStoryTickets((Array.isArray(list) ? list : []).map(normalizeStoryTicketRow));
        } catch (e) {
            setStoryTickets([]);
            alert(e?.response?.data?.message ?? e?.message ?? 'Không tải được ticket chi tiết.');
        } finally {
            setStoryTicketLoading(false);
        }
    };

    const submitAdminAction = async () => {
        if (!actionModal) return;
        setAdminActionError('');
        const payload = {
            requestKind: adminActionForm.requestKind,
            message: adminActionForm.message || undefined,
            proposedSuspendUntilUtc: adminActionForm.requestKind === 'SUSPEND_AUTHOR_WRITING' && adminActionForm.proposedSuspendUntilUtc
                ? new Date(adminActionForm.proposedSuspendUntilUtc).toISOString()
                : undefined,
        };
        setAdminActionSubmitting(true);
        try {
            if (actionModal.type === 'story') {
                await requestComplianceStoryAdminAction(actionModal.targetId, payload);
            } else {
                await requestComplianceCommentAdminAction(actionModal.targetId, payload);
            }
            await loadData(currentPage);
            setActionModal(null);
        } catch (e) {
            setAdminActionError(e?.response?.data?.message ?? e?.message ?? 'Không thể gửi yêu cầu.');
        } finally {
            setAdminActionSubmitting(false);
        }
    };

    const openReleaseRequestModal = (story) => {
        if (!story?.storyId) return;
        setReleaseReason('');
        setReleaseFormError('');
        setReleaseConfirmTarget({
            storyId: story.storyId,
            storyTitle: story.storyTitle || '',
            reportCount: story.reportCount ?? 0,
        });
    };

    const confirmReleaseRequest = async () => {
        const target = releaseConfirmTarget;
        if (!target?.storyId) return;
        const reason = releaseReason.trim();
        if (reason.length < 10) {
            setReleaseFormError('Lý do cần ít nhất 10 ký tự.');
            return;
        }
        setReleaseFormError('');
        setReleasingStoryId(target.storyId);
        try {
            await requestComplianceStoryRelease(target.storyId, { message: reason });
            setPendingReleaseByStory((prev) => ({ ...prev, [target.storyId]: Date.now() }));
            setReleaseConfirmTarget(null);
            setReleaseReason('');
            await loadData(currentPage);
            alert('Đã gửi yêu cầu trả đơn về hàng đợi cho quản trị viên.');
        } catch (e) {
            setReleaseFormError(e?.response?.data?.message ?? e?.message ?? 'Không thể gửi yêu cầu.');
        } finally {
            setReleasingStoryId(null);
        }
    };

    const openStoryActionConfirm = (payload) => setStoryActionConfirm(payload);

    const submitStoryActionConfirm = async () => {
        if (!storyActionConfirm) return;
        const { run } = storyActionConfirm;
        setStoryActionBusy(true);
        try {
            await run();
            setStoryActionConfirm(null);
        } finally {
            setStoryActionBusy(false);
        }
    };

    const loadClaimableStories = async () => {
        try {
            setClaimPickerLoading(true);
            const queue = await getComplianceStoryReports({
                page: 1,
                pageSize: 50,
                groupByStory: true,
                claimFilter: 'unclaimed',
                sortBy: 'priority_desc',
                statuses: 'NEW,IN_REVIEW',
            });
            const grouped = (queue?.items ?? queue?.Items ?? queue?.queueItems ?? queue?.QueueItems ?? []).map(normalizeStoryQueueItem);
            if (grouped.length > 0) {
                setClaimPickerRows(grouped);
                setIsClaimPickerOpen(true);
                return;
            }
            // fallback if grouped empty
            const rawRes = await getComplianceStoryReports({
                page: 1,
                pageSize: 300,
                groupByStory: false,
                statuses: 'NEW,IN_REVIEW',
                sortBy: 'newest',
            });
            const rawItems = rawRes?.items ?? rawRes?.Items ?? rawRes?.rows ?? rawRes?.Rows ?? [];
            const groupedFromRows = groupStoryRows(rawItems.map(normalizeStoryReportRow));
            if (groupedFromRows.length > 0) {
                setClaimPickerRows(groupedFromRows);
                setIsClaimPickerOpen(true);
                return;
            }
            alert('Hiện không có đơn report truyện nào chờ nhận duyệt.');
        } catch (e) {
            alert(e?.response?.data?.message ?? e?.message ?? 'Không thể nhận duyệt đơn.');
        } finally {
            setClaimPickerLoading(false);
        }
    };

    const handleClaimStoryFromPicker = async (story) => {
        const storyId = story?.storyId;
        if (!storyId) return;
        try {
            await claimComplianceStoryReports(storyId);
            setIsClaimPickerOpen(false);
            setShowClaimedStoryList(true);
            await loadData(1);
        } catch (e) {
            alert(e?.response?.data?.message ?? e?.message ?? 'Không thể nhận duyệt đơn.');
        }
    };

    const renderStoryReports = () => (
        <div className="overflow-x-auto">
            <table className="w-full border-collapse" style={{ minWidth: 1550 }}>
                <thead><tr className="bg-slate-50">
                    <th style={th}>Ưu tiên</th>
                    <th style={th}>Mức độ</th>
                    <th style={th}>Số báo cáo</th>
                    <th style={th}>TW</th>
                    <th style={th}>Truyện</th>
                    <th style={th}>Tác giả</th>
                    <th style={th}>Vi phạm / mod</th>
                    <th style={th}>Người báo</th>
                    <th style={th}>Ticket</th>
                    <th style={th}>Lock</th>
                    <th style={th}>Thao tác</th>
                </tr></thead>
                <tbody>
                    {rows.length === 0 && (
                        <tr>
                            <td colSpan={11} className="p-6 text-center text-sm text-slate-500">Không có dữ liệu hiển thị theo bộ lọc hiện tại.</td>
                        </tr>
                    )}
                    {rows.map((r) => (
                        <tr key={r.storyId} className="border-t border-slate-200 hover:bg-slate-50/70">
                            {(() => {
                                const hasPendingReleaseRequest = !!pendingReleaseByStory[r.storyId];
                                return (
                                    <>
                                        <td style={td}>{(r.priorityScore ?? 0).toFixed?.(1) ?? r.priorityScore}</td>
                                        <td style={td}>{(r.maxSeverityScore ?? 0).toFixed?.(1) ?? r.maxSeverityScore ?? '—'}</td>
                                        <td style={td}>{r.reportCount ?? 0}</td>
                                        <td style={td}>{r.timeWeight ?? 0}</td>
                                        <td style={td}><div style={{ fontWeight: 600 }}>{r.storyTitle || '—'}</div><div style={{ color: '#64748b', fontSize: 12 }}>{r.storyId}</div></td>
                                        <td style={td}>{r.authorDisplayName || '—'}</td>
                                        <td style={td}>{(r.distinctReasonCodes ?? []).slice(0, 2).map(reasonCodeToViLabel).join(', ') || '—'}</td>
                                        <td style={td}>
                                            {(() => {
                                                const contributors = Array.isArray(r.contributors) ? r.contributors : [];
                                                if (contributors.length === 0) return '—';
                                                const first = getContributorLabel(contributors[0]);
                                                return contributors.length > 1 ? `${first} +${contributors.length - 1}` : first;
                                            })()}
                                        </td>
                                        <td style={td}>{(r.openReportIds ?? []).length || (r.reportCount ?? 0)}</td>
                                        <td style={td}>{r.isComplianceLocked ? `Đã lock - ${r.complianceClaimedByDisplayName || '—'}` : 'Chưa lock'}</td>
                                        <td style={td}>
                                            <div style={{ display: 'flex', gap: 4, alignItems: 'center' }}>
                                                <button style={iconBtn} title="Xem chi tiết báo cáo" onClick={() => openStoryTicketsModal(r)}><Info size={16} /></button>
                                                <button
                                                    type="button"
                                                    style={!r.authorId ? { ...iconBtn, opacity: 0.45, cursor: 'not-allowed' } : iconBtn}
                                                    title={r.authorId ? 'Lịch sử vi phạm tài khoản tác giả' : 'Chưa có id tác giả'}
                                                    onClick={() => openAccountViolationHistory(r)}
                                                    disabled={!r.authorId}
                                                >
                                                    <History size={16} />
                                                </button>
                                                <button
                                                    style={hasPendingReleaseRequest ? { ...iconBtn, opacity: 0.45, cursor: 'not-allowed' } : iconBtn}
                                                    title={hasPendingReleaseRequest ? 'Đang chờ admin xử lý yêu cầu hủy nhận duyệt' : 'Trả đơn về hàng đợi'}
                                                    onClick={() => openReleaseRequestModal(r)}
                                                    disabled={hasPendingReleaseRequest}
                                                >
                                                    <Undo2 size={16} />
                                                </button>
                                                {isAdmin && <button style={hasPendingReleaseRequest ? { ...iconBtn, opacity: 0.45, cursor: 'not-allowed' } : iconBtn} title={hasPendingReleaseRequest ? 'Đang chờ admin xử lý yêu cầu hủy nhận duyệt' : 'Admin gỡ khóa đơn'} onClick={() => openStoryActionConfirm({
                                                    title: 'Xác nhận gỡ khóa đơn',
                                                    message: 'Bạn có chắc muốn admin gỡ khóa đơn và trả các ticket về hàng đợi?',
                                                    run: () => actionWithReload(() => adminReleaseComplianceStoryClaim(r.storyId)),
                                                })} disabled={hasPendingReleaseRequest}><LockOpen size={16} /></button>}
                                                <button style={hasPendingReleaseRequest ? { ...iconBtn, opacity: 0.45, cursor: 'not-allowed' } : iconBtn} title={hasPendingReleaseRequest ? 'Đang chờ admin xử lý yêu cầu hủy nhận duyệt' : 'Xử lý toàn bộ ticket mở'} onClick={() => openStoryActionConfirm({
                                                    title: 'Xác nhận xử lý toàn bộ',
                                                    message: 'Bạn có chắc muốn đánh dấu xử lý toàn bộ ticket mở của truyện này?',
                                                    run: () => actionWithReload(() => resolveAllOpenComplianceStoryReports(r.storyId, { status: 'RESOLVED' })),
                                                })} disabled={hasPendingReleaseRequest}><CheckCheck size={16} /></button>
                                                <button style={hasPendingReleaseRequest ? { ...iconBtn, opacity: 0.45, cursor: 'not-allowed' } : iconBtn} title={hasPendingReleaseRequest ? 'Đang chờ admin xử lý yêu cầu hủy nhận duyệt' : (r.complianceFlagged ? 'Bỏ gắn cờ vi phạm' : 'Gắn cờ vi phạm')} onClick={() => openStoryActionConfirm({
                                                    title: r.complianceFlagged ? 'Xác nhận bỏ gắn cờ' : 'Xác nhận gắn cờ vi phạm',
                                                    message: r.complianceFlagged
                                                        ? 'Bạn có chắc muốn bỏ gắn cờ vi phạm cho truyện này?'
                                                        : 'Bạn có chắc muốn gắn cờ vi phạm cho truyện này?',
                                                    run: () => actionWithReload(() => setComplianceStoryFlag(r.storyId, { flagged: !r.complianceFlagged })),
                                                })} disabled={hasPendingReleaseRequest}>
                                                    {r.complianceFlagged ? <FlagOff size={16} /> : <Flag size={16} />}
                                                </button>
                                                <button style={hasPendingReleaseRequest ? { ...iconBtn, opacity: 0.45, cursor: 'not-allowed' } : iconBtn} title={hasPendingReleaseRequest ? 'Đang chờ admin xử lý yêu cầu hủy nhận duyệt' : (r.commentsDisabled ? 'Mở lại bình luận' : 'Khóa bình luận')} onClick={() => openStoryActionConfirm({
                                                    title: r.commentsDisabled ? 'Xác nhận mở lại bình luận' : 'Xác nhận khóa bình luận',
                                                    message: r.commentsDisabled
                                                        ? 'Bạn có chắc muốn mở lại bình luận cho truyện này?'
                                                        : 'Bạn có chắc muốn khóa bình luận của truyện này?',
                                                    run: () => actionWithReload(() => setComplianceStoryCommentsDisabled(r.storyId, { value: !r.commentsDisabled })),
                                                })} disabled={hasPendingReleaseRequest}>
                                                    {r.commentsDisabled ? <MessageSquareOff size={16} /> : <MessageSquare size={16} />}
                                                </button>
                                                <button style={hasPendingReleaseRequest ? { ...iconBtn, opacity: 0.45, cursor: 'not-allowed' } : iconBtn} title={hasPendingReleaseRequest ? 'Đang chờ admin xử lý yêu cầu hủy nhận duyệt' : (r.complianceHidden ? 'Hiển thị lại truyện' : 'Ẩn truyện khỏi người dùng thường')} onClick={() => openStoryActionConfirm({
                                                    title: r.complianceHidden ? 'Xác nhận hiển thị lại truyện' : 'Xác nhận ẩn truyện',
                                                    message: r.complianceHidden
                                                        ? 'Bạn có chắc muốn hiển thị lại truyện cho người dùng thường?'
                                                        : 'Bạn có chắc muốn ẩn truyện khỏi người dùng thường?',
                                                    run: () => actionWithReload(() => setComplianceStoryHidden(r.storyId, { value: !r.complianceHidden })),
                                                })} disabled={hasPendingReleaseRequest}>
                                                    {r.complianceHidden ? <GlobeLock size={16} /> : <Globe size={16} />}
                                                </button>
                                                <button
                                                    style={hasPendingReleaseRequest ? { ...iconBtn, opacity: 0.45, cursor: 'not-allowed' } : iconBtn}
                                                    title={hasPendingReleaseRequest ? 'Đang chờ admin xử lý yêu cầu hủy nhận duyệt' : 'Yêu cầu chặn tài khoản / tạm đình chỉ'}
                                                    onClick={() => { setAdminActionForm({ requestKind: 'BAN_USER', message: '', proposedSuspendUntilUtc: '' }); setAdminActionError(''); setActionModal({ type: 'story', targetId: r.storyId }); }}
                                                    disabled={hasPendingReleaseRequest}
                                                ><ShieldAlert size={16} /></button>
                                            </div>
                                            {hasPendingReleaseRequest ? (
                                                <div className="text-xs text-amber-700 mt-1">Đang chờ admin xử lý đơn hủy nhận duyệt.</div>
                                            ) : null}
                                        </td>
                                    </>
                                );
                            })()}
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );

    const renderCommentReports = () => (
        <div className="overflow-x-auto">
            <table className="w-full border-collapse">
                <thead><tr className="bg-slate-50">
                    <th style={th}>Ticket</th><th style={th}>Truyện</th><th style={th}>Ưu tiên</th><th style={th}>Trạng thái</th><th style={th}>Cảnh báo</th><th style={th}>Thao tác</th>
                </tr></thead>
                <tbody>
                    {rows.length === 0 && (
                        <tr>
                            <td colSpan={6} className="p-6 text-center text-sm text-slate-500">Không có dữ liệu hiển thị theo bộ lọc hiện tại.</td>
                        </tr>
                    )}
                    {rows.map((r) => (
                        <tr key={r.reportId} className="border-t border-slate-200 hover:bg-slate-50/70">
                            <td style={td}><div style={{ fontWeight: 600 }}>{r.reportId}</div><div style={{ color: '#64748b', fontSize: 12 }}>Comment: {r.commentId}</div></td>
                            <td style={td}>{r.storyTitle || '—'}</td>
                            <td style={td}>{(r.priorityScore ?? 0).toFixed?.(1) ?? r.priorityScore}</td>
                            <td style={td}>{r.status || '—'}</td>
                            <td style={td}>{r.adminOrModeratorReplyWarningVi || '—'}</td>
                            <td style={td}>
                                <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                                    <button style={btn} onClick={() => setSelectedComment(r)}>Chi tiết ticket</button>
                                    <button style={btn} onClick={() => actionWithReload(() => claimComplianceCommentReports(r.commentId))}>Claim</button>
                                    {isAdmin && <button style={btn} onClick={() => actionWithReload(() => adminReleaseComplianceCommentClaim(r.commentId))}>Admin release claim</button>}
                                    <button style={btn} onClick={() => actionWithReload(() => resolveAllOpenComplianceCommentReports(r.commentId, { status: 'RESOLVED', hideComment: true, includeReplies: true }))}>Resolve all</button>
                                    <button style={btn} onClick={() => actionWithReload(() => setComplianceCommentThreadHidden(r.commentId, { value: true, includeReplies: true }))}>Ẩn thread</button>
                                    <button style={btn} onClick={() => { setAdminActionForm({ requestKind: 'BAN_USER', message: '', proposedSuspendUntilUtc: '' }); setAdminActionError(''); setActionModal({ type: 'comment', targetId: r.commentId }); }}>Yêu cầu chặn / đình chỉ</button>
                                </div>
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );

    const renderLockRequests = () => (
        <div className="overflow-x-auto">
            <table className="w-full border-collapse">
                <thead><tr className="bg-slate-50">
                    <th style={th}>Truyện</th><th style={th}>Người gửi</th><th style={th}>Message</th><th style={th}>Thời điểm</th><th style={th}>Thao tác admin</th>
                </tr></thead>
                <tbody>{rows.map((r) => (
                    <tr key={r.id} className="border-t border-slate-200 hover:bg-slate-50/70">
                        <td style={td}><div style={{ fontWeight: 600 }}>{r.storyTitle || '—'}</div><div style={{ color: '#64748b', fontSize: 12 }}>{r.storyId}</div></td>
                        <td style={td}>{r.requesterDisplayName || r.requesterEmail || '—'}</td>
                        <td style={td}>{r.message || '—'}</td>
                        <td style={td}>{formatDate(r.createdAtUtc)}</td>
                        <td style={td}>
                            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                                <button style={btn} onClick={() => { setLockResolveForm({ decision: 'APPROVE_UNLOCK', newAssigneeId: '', adminNote: '' }); setActionModal({ type: 'lock', targetId: r.id }); }}>Resolve</button>
                                <button style={btn} onClick={() => actionWithReload(() => adminReleaseComplianceStoryClaim(r.storyId))}>Gỡ lock trực tiếp</button>
                            </div>
                        </td>
                    </tr>
                ))}</tbody>
            </table>
        </div>
    );

    return (
        <div className="p-8 space-y-6">
            <div>
                <h1 className="text-2xl font-bold text-slate-900 mb-1">Xử lý báo cáo vi phạm</h1>
                <p className="text-sm text-slate-500">
                    Quản lý hàng đợi report truyện/bình luận, lock claim và hành động compliance.
                </p>
            </div>

            <div className="bg-white rounded-xl border border-slate-200 p-4">
                <h2 className="text-lg font-bold text-slate-900 mb-3">Bộ lọc và điều hướng</h2>
                {activeTab === 'story-reports' && (
                    <div className="mb-3 flex flex-wrap gap-2 items-center">
                        <button
                            type="button"
                            onClick={loadClaimableStories}
                            className="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg bg-sky-500 text-white text-sm font-semibold hover:bg-sky-600"
                        >
                            {claimPickerLoading ? 'Đang tải...' : 'Nhận duyệt đơn'}
                        </button>
                        <button
                            type="button"
                            onClick={openMyRequestsModal}
                            className="inline-flex items-center gap-1.5 px-4 py-2 rounded-lg border border-slate-300 bg-white text-slate-800 text-sm font-semibold hover:bg-slate-50"
                        >
                            <ClipboardList size={16} />
                            Đơn đã gửi admin
                        </button>
                    </div>
                )}
                <div className="flex gap-2 flex-wrap mb-3">
                    {tabs.map((tab) => (
                        <button
                            key={tab.id}
                            onClick={() => setActiveTab(tab.id)}
                            className={`inline-flex items-center gap-1.5 px-3 py-2 rounded-full border text-sm font-semibold transition-colors ${activeTab === tab.id
                                ? 'bg-primary/15 border-primary/40 text-emerald-700'
                                : 'bg-white border-slate-300 text-slate-700 hover:bg-slate-50'
                                }`}
                        >
                            {tab.label}
                        </button>
                    ))}
                </div>
                <div className="grid grid-cols-1 lg:grid-cols-3 gap-2">
                    <input value={filters.search} onChange={(e) => setFilters((p) => ({ ...p, search: e.target.value }))} placeholder="Tìm kiếm..." style={input} />
                    <input value={filters.statuses} onChange={(e) => setFilters((p) => ({ ...p, statuses: e.target.value }))} placeholder="Statuses (VD: NEW,IN_REVIEW)" style={input} />
                    <button onClick={() => setFilters({ search: '', statuses: 'NEW,IN_REVIEW', flaggedOnly: false })} className="inline-flex items-center justify-center gap-1.5 px-3 py-2 rounded-lg border border-slate-300 bg-white text-slate-700 text-sm font-medium hover:bg-slate-50"><RotateCcw style={{ width: 14, height: 14 }} /> Đặt lại</button>
                </div>
                {activeTab === 'story-reports' && (
                    <label className="inline-flex gap-2 mt-2 text-sm text-slate-700">
                        <input type="checkbox" checked={filters.flaggedOnly} onChange={(e) => setFilters((p) => ({ ...p, flaggedOnly: e.target.checked }))} />
                        Chỉ hiển thị truyện đã gắn cờ
                    </label>
                )}
            </div>

            <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
                {loading ? <div className="p-8 text-center text-slate-500 text-sm">Đang tải...</div> : error ? <div className="m-4 rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-700">{error}</div> : (
                    activeTab === 'story-reports'
                        ? (showClaimedStoryList
                            ? renderStoryReports()
                            : <div className="p-8 text-center text-slate-500 text-sm">Bấm "Nhận duyệt đơn" để bắt đầu xử lý và hiển thị danh sách.</div>)
                        : activeTab === 'comment-reports' ? renderCommentReports()
                            : activeTab === 'lock-requests' ? renderLockRequests()
                                : activeTab === 'compliance-logs' ? (
                                    <div className="overflow-x-auto"><table className="w-full border-collapse"><thead><tr className="bg-slate-50"><th style={th}>Thời điểm</th><th style={th}>Compliance</th><th style={th}>Nguồn</th><th style={th}>Action</th><th style={th}>Status</th></tr></thead><tbody>{rows.map((r) => <tr key={r.rowId} className="border-t border-slate-200 hover:bg-slate-50/70"><td style={td}>{formatDate(r.createdAtUtc)}</td><td style={td}>{r.complianceUserName || '—'}</td><td style={td}>{r.source || '—'}</td><td style={td}>{r.action || '—'}</td><td style={td}>{r.status || '—'}</td></tr>)}</tbody></table></div>
                                ) : (
                                    <div className="overflow-x-auto"><table className="w-full border-collapse"><thead><tr className="bg-slate-50"><th style={th}>Compliance</th><th style={th}>Resolved</th><th style={th}>Dismissed</th><th style={th}>Story</th><th style={th}>Comment</th><th style={th}>Tổng</th></tr></thead><tbody>{rows.map((r) => <tr key={r.complianceUserId} className="border-t border-slate-200 hover:bg-slate-50/70"><td style={td}>{r.complianceUserName || r.complianceUserId}</td><td style={td}>{r.resolvedCount ?? 0}</td><td style={td}>{r.dismissedCount ?? 0}</td><td style={td}>{r.storyReportResolvedCount ?? 0}</td><td style={td}>{r.commentReportResolvedCount ?? 0}</td><td style={td}>{r.total ?? 0}</td></tr>)}</tbody></table></div>
                                )
                )}
                {activeTab !== 'lock-requests' && !(activeTab === 'story-reports' && !showClaimedStoryList) && (
                    <Pagination currentPage={currentPage} totalPages={totalPages} totalItems={totalCount} itemsPerPage={PAGE_SIZE} onPageChange={(p) => loadData(p)} itemLabel="bản ghi" />
                )}
            </div>

            {selectedStory && (
                <Modal title={`Ticket chi tiết - ${selectedStory.storyTitle || selectedStory.storyId}`} onClose={() => setSelectedStory(null)}>
                    {storyTicketLoading ? <div>Đang tải ticket...</div> : (
                        <div className="space-y-4">
                            <div className="rounded-xl border border-slate-200 bg-gradient-to-r from-slate-50 to-white p-4">
                                <div className="text-sm font-semibold text-slate-900 mb-1">Thông tin đơn</div>
                                <div className="flex items-center gap-3">
                                    {storyMetaMap[selectedStory.storyId]?.coverUrl ? (
                                        <img
                                            src={storyMetaMap[selectedStory.storyId].coverUrl}
                                            alt={selectedStory.storyTitle || 'story-cover'}
                                            className="w-14 h-20 rounded-lg object-cover border border-slate-200"
                                        />
                                    ) : (
                                        <div className="w-14 h-20 rounded-lg bg-slate-100 border border-slate-200 flex items-center justify-center text-slate-400 text-xs">No cover</div>
                                    )}
                                    <div className="min-w-0">
                                        <div className="text-lg font-bold text-slate-900">{selectedStory.storyTitle || '—'}</div>
                                        <div className="text-xs text-slate-500 mt-0.5">{storyMetaMap[selectedStory.storyId]?.authorName || selectedStory.authorDisplayName || 'Tác giả ẩn danh'}</div>
                                    </div>
                                </div>
                                <div className="flex flex-wrap gap-2 mt-3">
                                    <span className="px-2 py-0.5 rounded-full text-xs bg-emerald-50 text-emerald-700 border border-emerald-100">Ưu tiên: {(selectedStory.priorityScore ?? 0).toFixed?.(1) ?? selectedStory.priorityScore}</span>
                                    <span className="px-2 py-0.5 rounded-full text-xs bg-amber-50 text-amber-700 border border-amber-100">Mức độ: {(selectedStory.maxSeverityScore ?? 0).toFixed?.(1) ?? selectedStory.maxSeverityScore ?? '—'}</span>
                                    <span className="px-2 py-0.5 rounded-full text-xs bg-sky-50 text-sky-700 border border-sky-100">Số báo cáo: {selectedStory.reportCount ?? 0}</span>
                                </div>
                                <div className="grid grid-cols-1 md:grid-cols-3 gap-2 mt-3">
                                    <div className="text-sm text-slate-700"><span className="font-semibold">Mã báo cáo:</span> {storyTickets[0]?.reportId || '—'}</div>
                                    <div className="text-sm text-slate-700"><span className="font-semibold">Trạng thái:</span> {statusViLabel(storyTickets[0]?.status)}</div>
                                    <div className="text-sm text-slate-700"><span className="font-semibold">Thời điểm:</span> {formatDate(storyTickets[0]?.createdAtUtc)}</div>
                                </div>
                            </div>

                            <div className="rounded-lg border border-slate-200">
                                <div className="px-3 py-2 border-b border-slate-200 text-sm font-semibold text-slate-800">Danh sách người báo cáo</div>
                                <div className="p-3 grid gap-2">
                                    {(() => {
                                        const contributors = collectContributors(selectedStory, storyTickets);
                                        if (contributors.length === 0) return <div className="text-sm text-slate-500">Chưa có dữ liệu người báo cáo.</div>;
                                        return (
                                            <div className="grid gap-2">
                                                {contributors.map((c, idx) => (
                                                    <div key={`${getContributorLabel(c)}-${idx}`} className="rounded-lg border border-slate-200 p-2.5 bg-white flex items-center justify-between gap-3">
                                                        <div className="min-w-0 flex-1">
                                                            <div className="text-sm font-semibold text-slate-900 truncate">{idx + 1}. {getContributorLabel(c)}</div>
                                                            <div className="text-xs text-slate-500 mt-0.5">{formatDate(c?.reportedAtUtc ?? c?.ReportedAtUtc)}</div>
                                                            <div className="text-sm text-slate-700 mt-1 whitespace-pre-wrap">
                                                                {String(c?.description ?? c?.Description ?? '').trim() || 'Không có mô tả.'}
                                                            </div>
                                                        </div>
                                                        <span className="px-2 py-0.5 rounded-full text-xs bg-slate-50 text-slate-700 border border-slate-200">
                                                            {reasonCodeToViLabel(c?.reasonCode ?? c?.ReasonCode)}
                                                        </span>
                                                    </div>
                                                ))}
                                            </div>
                                        );
                                    })()}
                                </div>
                            </div>
                        </div>
                    )}
                </Modal>
            )}

            {isClaimPickerOpen && (
                <Modal title="Danh sách báo cáo vi phạm chờ nhận duyệt" onClose={() => setIsClaimPickerOpen(false)}>
                    <div className="grid gap-3">
                        {claimPickerRows.length === 0 && (
                            <div className="p-6 text-center text-sm text-slate-500">Không có đơn chờ nhận.</div>
                        )}
                        {claimPickerRows.map((r) => {
                            const meta = claimPickerStoryMeta[r.storyId] || {};
                            const cover = meta.coverUrl;
                            return (
                                <div key={r.storyId} className="border border-slate-200 rounded-xl p-3 bg-white hover:bg-slate-50/50 transition-colors">
                                    <div className="flex items-center gap-3">
                                        {cover ? (
                                            <img src={cover} alt={r.storyTitle || 'story-cover'} className="w-14 h-20 rounded-lg object-cover border border-slate-200" />
                                        ) : (
                                            <div className="w-14 h-20 rounded-lg bg-slate-100 border border-slate-200 flex items-center justify-center text-slate-400 text-xs">No cover</div>
                                        )}
                                        <div className="min-w-0 flex-1">
                                            <div className="font-semibold text-slate-900 truncate">{r.storyTitle || '—'}</div>
                                            <div className="text-xs text-slate-500 truncate">{meta.authorName || r.authorDisplayName || 'Tác giả ẩn danh'}</div>
                                            <div className="text-xs text-slate-400 truncate">{r.storyId}</div>
                                            <div className="flex items-center gap-2 mt-2 overflow-x-auto whitespace-nowrap">
                                                <span className="px-2 py-0.5 rounded-full text-xs bg-emerald-50 text-emerald-700 border border-emerald-100">Ưu tiên: {(r.priorityScore ?? 0).toFixed?.(1) ?? r.priorityScore}</span>
                                                <span className="px-2 py-0.5 rounded-full text-xs bg-amber-50 text-amber-700 border border-amber-100">Mức độ: {(r.maxSeverityScore ?? 0).toFixed?.(1) ?? r.maxSeverityScore ?? '—'}</span>
                                                <span className="px-2 py-0.5 rounded-full text-xs bg-sky-50 text-sky-700 border border-sky-100">Số báo cáo: {r.reportCount ?? 0}</span>
                                                <span className="px-2 py-0.5 rounded-full text-xs bg-slate-50 text-slate-700 border border-slate-200">Vi phạm: {(r.distinctReasonCodes ?? []).slice(0, 2).map(reasonCodeToViLabel).join(', ') || 'Khác'}</span>
                                            </div>
                                        </div>
                                        <button
                                            onClick={() => handleClaimStoryFromPicker(r)}
                                            className="px-4 py-2 rounded-lg bg-sky-500 text-white text-sm font-semibold hover:bg-sky-600"
                                        >
                                            Nhận
                                        </button>
                                    </div>
                                </div>
                            );
                        })}
                    </div>
                </Modal>
            )}

            {selectedComment && (
                <Modal title={`Ticket comment - ${selectedComment.reportId}`} onClose={() => setSelectedComment(null)}>
                    <div style={{ display: 'grid', gap: 8 }}>
                        <div><b>CommentId:</b> {selectedComment.commentId}</div>
                        <div><b>Story:</b> {selectedComment.storyTitle || '—'}</div>
                        <div><b>Reason:</b> {selectedComment.reasonCode || '—'}</div>
                        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                            <button style={btn} onClick={() => actionWithReload(() => resolveComplianceCommentReport(selectedComment.reportId, { status: 'RESOLVED', hideComment: true, includeReplies: true }))}>Resolve ticket</button>
                            <button style={btn} onClick={() => actionWithReload(() => resolveComplianceCommentReport(selectedComment.reportId, { status: 'DISMISSED' }))}>Dismiss ticket</button>
                            <button style={btn} onClick={() => actionWithReload(() => setComplianceCommentThreadHidden(selectedComment.commentId, { value: true, includeReplies: true }))}>Ẩn thread</button>
                        </div>
                    </div>
                </Modal>
            )}

            {actionModal?.type === 'story' || actionModal?.type === 'comment' ? (
                <Modal title="Gửi yêu cầu xử lý lên quản trị viên" onClose={() => setActionModal(null)} maxWidth={620}>
                    <div className="space-y-4">
                        <div className="rounded-lg border border-slate-200 bg-slate-50 p-3 text-sm text-slate-700">
                            Chọn hình thức xử lý phù hợp và mô tả rõ lý do để quản trị viên xem xét.
                        </div>
                        <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
                            <label className="text-sm font-semibold text-slate-700">
                                Hình thức xử lý
                                <select value={adminActionForm.requestKind} onChange={(e) => setAdminActionForm((p) => ({ ...p, requestKind: e.target.value }))} style={{ ...input, width: '100%', marginTop: 6 }}>
                                    <option value="BAN_USER">Chặn tài khoản</option>
                                    <option value="SUSPEND_AUTHOR_WRITING">Tạm đình chỉ quyền viết</option>
                                </select>
                            </label>
                            {adminActionForm.requestKind === 'SUSPEND_AUTHOR_WRITING' && (
                                <label className="text-sm font-semibold text-slate-700">
                                    Thời hạn đình chỉ
                                    <input type="datetime-local" value={adminActionForm.proposedSuspendUntilUtc} onChange={(e) => setAdminActionForm((p) => ({ ...p, proposedSuspendUntilUtc: e.target.value }))} style={{ ...input, width: '100%', marginTop: 6 }} />
                                </label>
                            )}
                        </div>
                        <label className="text-sm font-semibold text-slate-700">
                            Lý do đề xuất
                            <textarea value={adminActionForm.message} onChange={(e) => setAdminActionForm((p) => ({ ...p, message: e.target.value }))} placeholder="Mô tả ngắn gọn lý do đề xuất xử lý..." style={{ ...input, width: '100%', marginTop: 6, minHeight: 110, resize: 'vertical' }} />
                        </label>
                        {adminActionError ? (
                            <div className="text-sm text-red-600">{adminActionError}</div>
                        ) : null}
                        <div className="flex justify-end">
                            <button style={{ ...btn, background: '#0ea5e9', color: '#fff', borderColor: '#0ea5e9' }} onClick={submitAdminAction} disabled={adminActionSubmitting}>
                                {adminActionSubmitting ? 'Đang gửi...' : 'Gửi yêu cầu'}
                            </button>
                        </div>
                    </div>
                </Modal>
            ) : null}

            {actionModal?.type === 'lock' && (
                <Modal title="Admin xử lý yêu cầu gỡ lock" onClose={() => setActionModal(null)}>
                    <div style={{ display: 'grid', gap: 8 }}>
                        <select value={lockResolveForm.decision} onChange={(e) => setLockResolveForm((p) => ({ ...p, decision: e.target.value }))} style={input}>
                            <option value="APPROVE_UNLOCK">APPROVE_UNLOCK</option>
                            <option value="APPROVE_REASSIGN">APPROVE_REASSIGN</option>
                            <option value="REJECT">REJECT</option>
                        </select>
                        {lockResolveForm.decision === 'APPROVE_REASSIGN' && (
                            <select value={lockResolveForm.newAssigneeId} onChange={(e) => setLockResolveForm((p) => ({ ...p, newAssigneeId: e.target.value }))} style={input}>
                                <option value="">Chọn compliance mới</option>
                                {officers.map((o) => <option key={o.id} value={o.id}>{o.displayName || o.email || o.id}</option>)}
                            </select>
                        )}
                        <textarea value={lockResolveForm.adminNote} onChange={(e) => setLockResolveForm((p) => ({ ...p, adminNote: e.target.value }))} placeholder="Ghi chú admin..." style={{ ...input, minHeight: 90 }} />
                        <div>
                            <button style={btn} onClick={async () => {
                                await actionWithReload(() => resolveAdminComplianceLockRequest(actionModal.targetId, {
                                    decision: lockResolveForm.decision,
                                    newAssigneeId: lockResolveForm.decision === 'APPROVE_REASSIGN' ? (lockResolveForm.newAssigneeId || undefined) : undefined,
                                    adminNote: lockResolveForm.adminNote || undefined,
                                }));
                                setActionModal(null);
                            }}>Xác nhận</button>
                        </div>
                    </div>
                </Modal>
            )}

            {releaseConfirmTarget && (
                <Modal title="Trả truyện về hàng đợi xử lý vi phạm?" maxWidth={520} onClose={() => {
                    if (releasingStoryId) return;
                    setReleaseConfirmTarget(null);
                    setReleaseFormError('');
                }}>
                    <div style={{ display: 'grid', gap: 10 }}>
                        <p className="text-sm text-slate-600 m-0">
                            Yêu cầu này sẽ gửi lên quản trị viên. Sau khi được duyệt, truyện sẽ được trả về hàng đợi xử lý chung.
                        </p>
                        {releaseConfirmTarget.storyTitle ? (
                            <div className="rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-700">
                                <span className="text-slate-500">Truyện áp dụng:</span> <strong>&quot;{releaseConfirmTarget.storyTitle}&quot;</strong>
                                {releaseConfirmTarget.reportCount > 0 ? <> ({releaseConfirmTarget.reportCount} báo cáo)</> : null}
                            </div>
                        ) : null}
                        <label className="text-sm font-semibold text-slate-700">
                            Lý do gửi yêu cầu <span className="text-red-500">*</span> (tối thiểu 10 ký tự)
                            <textarea
                                value={releaseReason}
                                onChange={(e) => {
                                    setReleaseReason(e.target.value);
                                    if (releaseFormError) setReleaseFormError('');
                                }}
                                rows={4}
                                placeholder="Ví dụ: Tôi đang quá tải ticket, đề nghị trả truyện về hàng đợi để phân công lại."
                                disabled={!!releasingStoryId}
                                style={{ ...input, width: '100%', marginTop: 6, minHeight: 96, resize: 'vertical' }}
                            />
                        </label>
                        {releaseFormError ? <div className="text-sm text-red-600">{releaseFormError}</div> : null}
                        <div className="flex justify-end gap-2">
                            <button
                                style={btn}
                                disabled={!!releasingStoryId}
                                onClick={() => {
                                    setReleaseConfirmTarget(null);
                                    setReleaseFormError('');
                                }}
                            >
                                Hủy
                            </button>
                            <button
                                style={{ ...btn, background: releasingStoryId ? '#94a3b8' : '#dc2626', color: '#fff', borderColor: releasingStoryId ? '#94a3b8' : '#dc2626' }}
                                disabled={!!releasingStoryId}
                                onClick={confirmReleaseRequest}
                            >
                                {releasingStoryId ? 'Đang gửi...' : 'Gửi đơn lên quản trị'}
                            </button>
                        </div>
                    </div>
                </Modal>
            )}

            {storyActionConfirm && (
                <Modal
                    title={storyActionConfirm.title || 'Xác nhận thao tác'}
                    maxWidth={480}
                    onClose={() => {
                        if (storyActionBusy) return;
                        setStoryActionConfirm(null);
                    }}
                >
                    <div style={{ display: 'grid', gap: 10 }}>
                        <p className="text-sm text-slate-700 m-0">
                            {storyActionConfirm.message || 'Bạn có chắc muốn thực hiện thao tác này?'}
                        </p>
                        <div className="flex justify-end gap-2">
                            <button
                                style={btn}
                                disabled={storyActionBusy}
                                onClick={() => setStoryActionConfirm(null)}
                            >
                                Hủy
                            </button>
                            <button
                                style={{ ...btn, background: '#0ea5e9', color: '#fff', borderColor: '#0ea5e9' }}
                                disabled={storyActionBusy}
                                onClick={submitStoryActionConfirm}
                            >
                                {storyActionBusy ? 'Đang xử lý...' : 'Xác nhận'}
                            </button>
                        </div>
                    </div>
                </Modal>
            )}

            {accountViolationModal && (
                <Modal
                    title={`Lịch sử vi phạm — ${accountViolationModal.displayName}`}
                    maxWidth={720}
                    onClose={() => {
                        setAccountViolationModal(null);
                        setAccountViolationRows([]);
                    }}
                >
                    {accountViolationLoading ? (
                        <div className="text-sm text-slate-600">Đang tải...</div>
                    ) : (
                        <div className="overflow-x-auto">
                            <table className="w-full border-collapse text-sm">
                                <thead>
                                    <tr className="bg-slate-50">
                                        <th style={th}>Thời điểm</th>
                                        <th style={th}>Loại hình</th>
                                        <th style={th}>Mô tả</th>
                                        <th style={th}>Người thao tác</th>
                                    </tr>
                                </thead>
                                <tbody>
                                    {accountViolationRows.length === 0 ? (
                                        <tr>
                                            <td colSpan={4} className="p-4 text-center text-slate-500">Chưa có bản ghi violation_logs cho tài khoản này.</td>
                                        </tr>
                                    ) : (
                                        accountViolationRows.map((row) => (
                                            <tr key={String(row.id ?? row.Id)} className="border-t border-slate-200">
                                                <td style={td}>{formatDate(row.createdAtUtc ?? row.CreatedAtUtc)}</td>
                                                <td style={td}>{penaltyTypeVi(row.penaltyType ?? row.PenaltyType)}</td>
                                                <td style={td}>{row.reason ?? row.Reason ?? '—'}</td>
                                                <td style={td}>{row.complianceOfficerDisplayName ?? row.ComplianceOfficerDisplayName ?? '—'}</td>
                                            </tr>
                                        ))
                                    )}
                                </tbody>
                            </table>
                        </div>
                    )}
                </Modal>
            )}

            {myRequestsModalOpen && (
                <Modal title="Đơn đã gửi lên quản trị viên" maxWidth={900} onClose={() => setMyRequestsModalOpen(false)}>
                    {myRequestsLoading ? (
                        <div className="text-sm text-slate-600">Đang tải...</div>
                    ) : (
                        <div className="space-y-8">
                            <section>
                                <h4 className="text-base font-bold text-slate-900 mb-2">Yêu cầu gỡ lock / giao lại</h4>
                                <div className="overflow-x-auto">
                                    <table className="w-full border-collapse text-sm">
                                        <thead>
                                            <tr className="bg-slate-50">
                                                <th style={th}>Truyện</th>
                                                <th style={th}>Trạng thái</th>
                                                <th style={th}>Gửi lúc</th>
                                                <th style={th}>Ghi chú / lý do từ chối</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {myLockRequests.length === 0 ? (
                                                <tr>
                                                    <td colSpan={4} className="p-4 text-center text-slate-500">Chưa có đơn.</td>
                                                </tr>
                                            ) : (
                                                myLockRequests.map((row) => {
                                                    const st = String(row.status ?? row.Status ?? '').toUpperCase();
                                                    const rejected = st === 'REJECTED';
                                                    return (
                                                        <tr key={String(row.id ?? row.Id)} className="border-t border-slate-200">
                                                            <td style={td}>{row.storyTitle ?? row.StoryTitle ?? '—'}</td>
                                                            <td style={td}>
                                                                <span style={rejected ? { color: '#991b1b', fontWeight: 600 } : undefined}>
                                                                    {complianceRequestStatusVi(row.status ?? row.Status)}
                                                                </span>
                                                            </td>
                                                            <td style={td}>{formatDate(row.createdAtUtc ?? row.CreatedAtUtc)}</td>
                                                            <td style={{ ...td, ...(rejected ? { color: '#991b1b' } : {}) }}>
                                                                {row.resolutionNote ?? row.ResolutionNote ?? '—'}
                                                            </td>
                                                        </tr>
                                                    );
                                                })
                                            )}
                                        </tbody>
                                    </table>
                                </div>
                            </section>
                            <section>
                                <h4 className="text-base font-bold text-slate-900 mb-2">Yêu cầu chặn tài khoản / tạm đình chỉ viết</h4>
                                <div className="overflow-x-auto">
                                    <table className="w-full border-collapse text-sm">
                                        <thead>
                                            <tr className="bg-slate-50">
                                                <th style={th}>Truyện</th>
                                                <th style={th}>Loại</th>
                                                <th style={th}>Trạng thái</th>
                                                <th style={th}>Gửi lúc</th>
                                                <th style={th}>Ghi chú / lý do từ chối</th>
                                            </tr>
                                        </thead>
                                        <tbody>
                                            {myAdminRequests.length === 0 ? (
                                                <tr>
                                                    <td colSpan={5} className="p-4 text-center text-slate-500">Chưa có đơn.</td>
                                                </tr>
                                            ) : (
                                                myAdminRequests.map((row) => {
                                                    const st = String(row.status ?? row.Status ?? '').toUpperCase();
                                                    const rejected = st === 'REJECTED';
                                                    return (
                                                        <tr key={String(row.id ?? row.Id)} className="border-t border-slate-200">
                                                            <td style={td}>{row.storyTitle ?? row.StoryTitle ?? '—'}</td>
                                                            <td style={td}>{complianceAdminActionKindVi(row.requestKind ?? row.RequestKind)}</td>
                                                            <td style={td}>
                                                                <span style={rejected ? { color: '#991b1b', fontWeight: 600 } : undefined}>
                                                                    {complianceRequestStatusVi(row.status ?? row.Status)}
                                                                </span>
                                                            </td>
                                                            <td style={td}>{formatDate(row.createdAtUtc ?? row.CreatedAtUtc)}</td>
                                                            <td style={{ ...td, ...(rejected ? { color: '#991b1b' } : {}) }}>
                                                                {row.resolutionNote ?? row.ResolutionNote ?? '—'}
                                                            </td>
                                                        </tr>
                                                    );
                                                })
                                            )}
                                        </tbody>
                                    </table>
                                </div>
                            </section>
                        </div>
                    )}
                </Modal>
            )}
        </div>
    );
}

const th = {
    textAlign: 'left',
    padding: '0.75rem',
    borderBottom: '1px solid #e2e8f0',
    fontSize: '0.72rem',
    letterSpacing: '0.02em',
    color: '#64748b',
    fontWeight: 700,
    textTransform: 'uppercase',
};
const td = { padding: '0.75rem', color: '#334155', verticalAlign: 'top', fontSize: '0.875rem' };
const input = { border: '1px solid #e2e8f0', borderRadius: 8, padding: '0.6rem 0.75rem', fontSize: '0.875rem', color: '#0f172a', background: '#f8fafc' };
const btn = { display: 'inline-flex', alignItems: 'center', gap: 6, border: '1px solid #cbd5e1', background: '#fff', color: '#334155', borderRadius: 8, padding: '0.4rem 0.7rem', cursor: 'pointer' };
const iconBtn = { display: 'inline-flex', alignItems: 'center', justifyContent: 'center', width: 34, height: 34, border: '1px solid #dbe2ea', background: '#fff', color: '#64748b', borderRadius: 10, cursor: 'pointer' };
