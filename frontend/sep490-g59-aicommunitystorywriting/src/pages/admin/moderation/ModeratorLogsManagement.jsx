import { useEffect, useState } from 'react';
import { RotateCcw } from 'lucide-react';
import { getModerationLogs } from '../../../api/admin/adminModerationApi';
import { Pagination } from '../../../components/pagination/Pagination';

const PAGE_SIZE = 10;

const ACTION_OPTIONS = [
    { value: '', label: 'Tất cả hành động' },
    { value: 'APPROVED', label: 'Đã duyệt' },
    { value: 'REJECTED', label: 'Đã từ chối' },
];

const TARGET_OPTIONS = [
    { value: '', label: 'Tất cả đối tượng' },
    { value: 'STORY', label: 'Truyện' },
    { value: 'CHAPTER', label: 'Chương' },
];

function formatDate(value) {
    if (!value) return '—';
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return '—';
    return d.toLocaleString('vi-VN');
}

function targetLabel(item) {
    const t = String(item?.targetType ?? '').toUpperCase();
    if (t === 'STORY') return 'Truyện';
    if (t === 'CHAPTER') return 'Chương';
    return item?.targetType || '—';
}

function actionLabel(item) {
    const a = String(item?.action ?? '').toUpperCase();
    if (a === 'APPROVED') return 'Duyệt';
    if (a === 'REJECTED') return 'Từ chối';
    return item?.action || '—';
}

export function ModeratorLogsManagement() {
    const [allRows, setAllRows] = useState([]);
    const [rows, setRows] = useState([]);
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(null);
    const [currentPage, setCurrentPage] = useState(1);
    const [totalCount, setTotalCount] = useState(0);
    const [totalPages, setTotalPages] = useState(1);

    const [filters, setFilters] = useState({
        search: '',
        action: '',
        targetType: '',
        dateFrom: '',
        dateTo: '',
        sortBy: 'created_at',
        sortOrder: 'desc',
    });
    const [appliedFilters, setAppliedFilters] = useState({
        search: '',
        action: '',
        targetType: '',
        dateFrom: '',
        dateTo: '',
        sortBy: 'created_at',
        sortOrder: 'desc',
    });

    const normalize = (v) => String(v ?? '').trim().toLowerCase();

    const applyLocalFilters = (items, activeFilters) => {
        const search = normalize(activeFilters.search);
        const action = normalize(activeFilters.action);
        const targetType = normalize(activeFilters.targetType);
        const from = activeFilters.dateFrom ? new Date(`${activeFilters.dateFrom}T00:00:00`).getTime() : null;
        const to = activeFilters.dateTo ? new Date(`${activeFilters.dateTo}T23:59:59`).getTime() : null;

        return (Array.isArray(items) ? items : []).filter((x) => {
            const itemAction = normalize(x.action);
            const itemTarget = normalize(x.targetType);
            const itemTime = x.createdAt ? new Date(x.createdAt).getTime() : NaN;

            if (action && itemAction !== action) return false;
            if (targetType && itemTarget !== targetType) return false;
            if (from != null && Number.isFinite(itemTime) && itemTime < from) return false;
            if (to != null && Number.isFinite(itemTime) && itemTime > to) return false;

            if (search) {
                const text = [
                    x.moderatorName,
                    x.rejectionReason,
                    x.targetTitle,
                    x.action,
                    x.targetType,
                ].map(normalize).join(' | ');
                if (!text.includes(search)) return false;
            }
            return true;
        });
    };

    const toUiRow = (x) => ({
        id: x?.id ?? x?.Id,
        targetId: x?.targetId ?? x?.TargetId ?? null,
        targetType: x?.targetType ?? x?.TargetType ?? '',
        targetTitle: x?.targetTitle ?? x?.TargetTitle ?? '',
        action: x?.action ?? x?.Action ?? '',
        moderatorId: x?.moderatorId ?? x?.ModeratorId ?? null,
        moderatorName: x?.moderatorName ?? x?.ModeratorName ?? '',
        createdAt: x?.createdAt ?? x?.CreatedAt ?? null,
        rejectionReason: x?.rejectionReason ?? x?.RejectionReason ?? '',
    });

    const computeFilteredPage = (sourceRows, page = 1, activeFilters = appliedFilters) => {
        const filtered = applyLocalFilters(sourceRows, activeFilters);
        const total = filtered.length;
        const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));
        const safePage = Math.min(Math.max(1, page), pages);
        const paged = filtered.slice((safePage - 1) * PAGE_SIZE, safePage * PAGE_SIZE);
        setRows(paged);
        setTotalCount(total);
        setTotalPages(pages);
        setCurrentPage(safePage);
    };

    const loadData = async () => {
        setLoading(true);
        setError(null);
        try {
            // Không dùng filter BE. Chỉ lấy raw data rồi filter hoàn toàn ở FE.
            let merged = [];
            let page = 1;
            const pageSize = 100;
            while (true) {
                const res = await getModerationLogs({ page, pageSize, sortBy: 'created_at', sortOrder: 'desc' });
                const chunk = res?.items ?? res?.Items ?? [];
                if (Array.isArray(chunk) && chunk.length) merged = merged.concat(chunk);
                if (!Array.isArray(chunk) || chunk.length < pageSize) break;
                page += 1;
            }

            const normalizedRows = merged.map(toUiRow);
            setAllRows(normalizedRows);
            computeFilteredPage(normalizedRows, 1, appliedFilters);
        } catch (e) {
            setError(e?.response?.data?.message ?? e?.message ?? 'Không tải được log kiểm duyệt.');
            setAllRows([]);
            setRows([]);
            setTotalCount(0);
            setTotalPages(1);
        } finally {
            setLoading(false);
        }
    };

    useEffect(() => {
        loadData();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    // Auto filter realtime: thay đổi filter là áp dụng ngay, không cần bấm "Lọc".
    useEffect(() => {
        setAppliedFilters(filters);
        computeFilteredPage(allRows, 1, filters);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [filters, allRows]);

    const onResetFilters = () => {
        const next = {
            search: '',
            action: '',
            targetType: '',
            dateFrom: '',
            dateTo: '',
            sortBy: 'created_at',
            sortOrder: 'desc',
        };
        setFilters(next);
        setAppliedFilters(next);
        computeFilteredPage(allRows, 1, next);
    };

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <div style={{ backgroundColor: '#fff', border: '1px solid #e2e8f0', borderRadius: '12px', padding: '1rem' }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.75rem' }}>
                    <h2 style={{ margin: 0, fontSize: '1.1rem', color: '#0f172a' }}>Nhật ký kiểm duyệt của kiểm duyệt viên</h2>
                    <span style={{ fontSize: '0.85rem', color: '#64748b' }}>Tổng: {totalCount}</span>
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr 1fr 1fr', gap: '0.6rem' }}>
                    <input
                        value={filters.search}
                        onChange={(e) => setFilters((p) => ({ ...p, search: e.target.value }))}
                        placeholder="Tìm theo moderator, lý do, tiêu đề truyện/chương..."
                        style={{ border: '1px solid #cbd5e1', borderRadius: '8px', padding: '0.55rem 0.7rem', fontSize: '0.875rem' }}
                    />
                    <select
                        value={filters.action}
                        onChange={(e) => setFilters((p) => ({ ...p, action: e.target.value }))}
                        style={{ border: '1px solid #cbd5e1', borderRadius: '8px', padding: '0.55rem 0.7rem', fontSize: '0.875rem' }}
                    >
                        {ACTION_OPTIONS.map((x) => <option key={x.value} value={x.value}>{x.label}</option>)}
                    </select>
                    <select
                        value={filters.targetType}
                        onChange={(e) => setFilters((p) => ({ ...p, targetType: e.target.value }))}
                        style={{ border: '1px solid #cbd5e1', borderRadius: '8px', padding: '0.55rem 0.7rem', fontSize: '0.875rem' }}
                    >
                        {TARGET_OPTIONS.map((x) => <option key={x.value} value={x.value}>{x.label}</option>)}
                    </select>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem' }}>
                        <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Từ ngày</label>
                        <input
                            type="date"
                            value={filters.dateFrom}
                            onChange={(e) => setFilters((p) => ({ ...p, dateFrom: e.target.value }))}
                            style={{ border: '1px solid #cbd5e1', borderRadius: '8px', padding: '0.55rem 0.7rem', fontSize: '0.875rem' }}
                        />
                    </div>
                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.25rem' }}>
                        <label style={{ fontSize: '0.75rem', color: '#64748b' }}>Tới ngày</label>
                        <input
                            type="date"
                            value={filters.dateTo}
                            onChange={(e) => setFilters((p) => ({ ...p, dateTo: e.target.value }))}
                            style={{ border: '1px solid #cbd5e1', borderRadius: '8px', padding: '0.55rem 0.7rem', fontSize: '0.875rem' }}
                        />
                    </div>
                </div>
                <div style={{ marginTop: '0.75rem', display: 'flex', gap: '0.5rem', justifyContent: 'flex-end' }}>
                    <button
                        onClick={onResetFilters}
                        style={{ display: 'inline-flex', alignItems: 'center', gap: '0.35rem', border: '1px solid #cbd5e1', background: '#fff', color: '#334155', borderRadius: '8px', padding: '0.45rem 0.75rem', cursor: 'pointer' }}
                    >
                        <RotateCcw style={{ width: 14, height: 14 }} />
                        Đặt lại
                    </button>
                </div>
            </div>

            <div style={{ backgroundColor: '#fff', border: '1px solid #e2e8f0', borderRadius: '12px', overflow: 'hidden' }}>
                {loading ? (
                    <div style={{ padding: '2rem', textAlign: 'center', color: '#64748b' }}>Đang tải log kiểm duyệt...</div>
                ) : error ? (
                    <div style={{ padding: '1rem', color: '#b91c1c' }}>{error}</div>
                ) : rows.length === 0 ? (
                    <div style={{ padding: '2rem', textAlign: 'center', color: '#64748b' }}>Không có dữ liệu.</div>
                ) : (
                    <div style={{ overflowX: 'auto' }}>
                        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                            <thead>
                                <tr style={{ backgroundColor: '#f8fafc' }}>
                                    <th style={{ textAlign: 'left', padding: '0.75rem', borderBottom: '1px solid #e2e8f0' }}>Thời điểm</th>
                                    <th style={{ textAlign: 'left', padding: '0.75rem', borderBottom: '1px solid #e2e8f0' }}>Moderator</th>
                                    <th style={{ textAlign: 'left', padding: '0.75rem', borderBottom: '1px solid #e2e8f0' }}>Đối tượng</th>
                                    <th style={{ textAlign: 'left', padding: '0.75rem', borderBottom: '1px solid #e2e8f0' }}>Tiêu đề</th>
                                    <th style={{ textAlign: 'left', padding: '0.75rem', borderBottom: '1px solid #e2e8f0' }}>Hành động</th>
                                    <th style={{ textAlign: 'left', padding: '0.75rem', borderBottom: '1px solid #e2e8f0' }}>Lý do từ chối</th>
                                </tr>
                            </thead>
                            <tbody>
                                {rows.map((r) => (
                                    <tr key={r.id ?? `${r.targetId}-${r.createdAt}`} style={{ borderBottom: '1px solid #f1f5f9' }}>
                                        <td style={{ padding: '0.75rem', color: '#334155', whiteSpace: 'nowrap' }}>{formatDate(r.createdAt)}</td>
                                        <td style={{ padding: '0.75rem', color: '#334155' }}>{r.moderatorName || '—'}</td>
                                        <td style={{ padding: '0.75rem', color: '#334155' }}>{targetLabel(r)}</td>
                                        <td style={{ padding: '0.75rem', color: '#334155' }}>{r.targetTitle || '—'}</td>
                                        <td style={{ padding: '0.75rem', color: String(r.action || '').toUpperCase() === 'REJECTED' ? '#b91c1c' : '#065f46', fontWeight: 600 }}>
                                            {actionLabel(r)}
                                        </td>
                                        <td style={{ padding: '0.75rem', color: '#475569', maxWidth: 420 }}>
                                            <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={r.rejectionReason || ''}>
                                                {r.rejectionReason || '—'}
                                            </div>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
                <Pagination
                    currentPage={currentPage}
                    totalPages={totalPages}
                    totalItems={totalCount}
                    itemsPerPage={PAGE_SIZE}
                    onPageChange={(p) => computeFilteredPage(allRows, p, appliedFilters)}
                    itemLabel="log"
                />
            </div>
        </div>
    );
}
