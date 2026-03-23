import { useEffect, useMemo, useState } from 'react';
import {
    Wallet,
    ArrowDownCircle,
    ArrowUpCircle,
    Lock,
    Users,
    Book,
    LineChart,
    History,
    RefreshCw,
    Search,
    ChevronLeft,
    ChevronRight,
    Info,
    Copy,
    FileDown,
} from 'lucide-react';
import { getAdminWalletSummary, getSystemCoinLedger, getTopAuthorsByIncome, getTopSpenders } from '../../../api/admin/walletApi';
import { AdminTransactions } from '../transactions/AdminTransactions';

// FE mock: Ví hệ thống (admin) - sau này nối API:
// - GET /api/admin/wallet/summary
// - GET /api/admin/wallet/top-authors, top-spenders
// - GET /api/admin/wallet/transactions (phân trang, lọc)

/** Tab lịch sử ví hệ thống: chỉ donate + mở khóa chương (phí nền tảng 30%). API: type=UNLOCK_AND_DONATE | UNLOCK | DONATE */
const LEDGER_EVENT_TYPES = [
    { value: 'UNLOCK_AND_DONATE', label: 'Tất cả (donate + mở khóa)' },
    { value: 'UNLOCK', label: 'Mở khóa chương' },
    { value: 'DONATE', label: 'Ủng hộ (donate)' },
];

/** Hiển thị UUID dài gọn, vẫn copy full qua clipboard. */
function shortenGuidLike(id) {
    if (id == null || id === '') return '';
    const s = String(id).trim();
    if (s.length <= 16) return s;
    return `${s.slice(0, 8)}…${s.slice(-4)}`;
}

function CopyIdButton({ value, label }) {
    if (!value) return null;
    const full = String(value);
    return (
        <button
            type="button"
            title={`Sao chép ${label}`}
            onClick={async () => {
                try {
                    await navigator.clipboard.writeText(full);
                } catch {
                    /* ignore */
                }
            }}
            className="inline-flex shrink-0 rounded-md p-1 text-slate-400 transition hover:bg-slate-100 hover:text-slate-700"
            aria-label={`Sao chép ${label}`}
        >
            <Copy className="h-3.5 w-3.5" />
        </button>
    );
}

function IdLine({ roleLabel, id }) {
    if (!id) return null;
    const full = String(id);
    return (
        <div className="flex min-w-0 items-center gap-1">
            <span className="w-[4.5rem] shrink-0 text-[11px] font-medium uppercase tracking-wide text-slate-500">{roleLabel}</span>
            <code
                className="min-w-0 flex-1 truncate rounded border-l-2 border-slate-300 bg-slate-50 px-1.5 py-0.5 text-[11px] text-slate-800"
                title={full}
            >
                {shortenGuidLike(full)}
            </code>
            <CopyIdButton value={full} label={roleLabel} />
        </div>
    );
}

// Mock summary/top lists (fallback when API unavailable)
const MOCK_SUMMARY = {
    totalCoinsInSystem: 12_450_000,
    totalIncomeBalance: 3_250_000,
    totalFrozenBalance: 120_000,
    totalPendingEscrow: 80_000,
    totalRechargeVnd: 985_000_000,
    totalWithdrawVnd: 420_000_000,
    totalWithdrawCoins: 0,
    platformFeeCoins: 1_250_000,
    platformRevenueVnd: 565_000_000,
    coinRateVnd: 100,
    activeAuthors: 126,
    activeReaders: 8_540,
    systemWalletBalanceCoins: 1_250_000,
};

const MOCK_TOP_AUTHORS = [
    { id: 'mock-a1', name: 'Thiên Tằm Thổ Đậu', incomeCoins: 42_500_000, stories: 5 },
    { id: 'mock-a2', name: 'Ngã Cật Tây Hồng Thị', incomeCoins: 30_200_000, stories: 3 },
    { id: 'mock-a3', name: 'Đường Gia Tam Thiếu', incomeCoins: 18_750_000, stories: 4 },
    { id: 'mock-a4', name: 'Cổ Long', incomeCoins: 12_300_000, stories: 2 },
];

const MOCK_TOP_SPENDERS = [
    { id: 'mock-u1', name: 'user_001', coins: 180_000 },
    { id: 'mock-u2', name: 'user_029', coins: 125_000 },
    { id: 'mock-u3', name: 'user_312', coins: 90_000 },
    { id: 'mock-u4', name: 'user_777', coins: 75_500 },
];

export function AdminWalletDashboard({ initialActiveTab } = {}) {
    const [activeTab, setActiveTab] = useState(initialActiveTab || 'overview');
    const [chartRange, setChartRange] = useState('7'); // 7 | 30 ngày
    const [overviewLoading, setOverviewLoading] = useState(false);
    const [historyTypeFilter, setHistoryTypeFilter] = useState('UNLOCK_AND_DONATE');
    const [historySearch, setHistorySearch] = useState('');
    const [historyDateFrom, setHistoryDateFrom] = useState('');
    const [historyDateTo, setHistoryDateTo] = useState('');
    const [historyPage, setHistoryPage] = useState(1);
    const historyPageSize = 20;
    const [historyLoading, setHistoryLoading] = useState(false);
    const [transactions, setTransactions] = useState(() => []);
    const [historyTotalCount, setHistoryTotalCount] = useState(0);
    const [historyTotalPages, setHistoryTotalPages] = useState(1);
    const [systemWalletBalanceCoins, setSystemWalletBalanceCoins] = useState(MOCK_SUMMARY.systemWalletBalanceCoins);
    const [summary, setSummary] = useState(MOCK_SUMMARY);
    const [topAuthors, setTopAuthors] = useState(MOCK_TOP_AUTHORS);
    const [topSpenders, setTopSpendersState] = useState(MOCK_TOP_SPENDERS);
    const [loadError, setLoadError] = useState('');

    const summarySafe = summary ?? MOCK_SUMMARY;

    const dailyIncome = useMemo(() => {
        const base = [
            { day: 'T2', income: 12_000_000 },
            { day: 'T3', income: 9_500_000 },
            { day: 'T4', income: 15_200_000 },
            { day: 'T5', income: 11_800_000 },
            { day: 'T6', income: 18_400_000 },
            { day: 'T7', income: 22_100_000 },
            { day: 'CN', income: 17_600_000 },
        ];
        return chartRange === '30' ? [...base, ...base.slice(0, 3).map((d, i) => ({ day: `T${i + 8}`, income: d.income }))] : base;
    }, [chartRange]);

    const topAuthorsView = topAuthors.map((a) => ({
        id: a.id,
        name: a.name,
        income: a.incomeCoins ?? a.income ?? 0,
        stories: a.stories ?? 0,
    }));

    const topSpendersView = topSpenders.map((u) => ({
        id: u.id,
        name: u.name,
        coins: u.coins ?? 0,
    }));

    const maxIncome = useMemo(() => Math.max(...dailyIncome.map((d) => d.income), 1), [dailyIncome]);

    const filteredTransactions = useMemo(() => {
        let list = [...transactions];
        if (historySearch.trim()) {
            const q = historySearch.trim().toLowerCase();
            list = list.filter(
                (t) =>
                    (t.eventType && t.eventType.toLowerCase().includes(q)) ||
                    (t.note && t.note.toLowerCase().includes(q)) ||
                    (t.storyTitle && t.storyTitle.toLowerCase().includes(q)) ||
                    (t.chapterTitle && t.chapterTitle.toLowerCase().includes(q)) ||
                    String(t.authorUserId || '').toLowerCase().includes(q) ||
                    String(t.buyerUserId || '').toLowerCase().includes(q) ||
                    String(t.adminId || '').toLowerCase().includes(q)
            );
        }
        return list;
    }, [historySearch, transactions]);

    const hasAnyNote = useMemo(
        () => filteredTransactions.some((tx) => tx.note && String(tx.note).trim()),
        [filteredTransactions]
    );

    const totalHistoryPages = Math.max(1, historyTotalPages);
    const paginatedTransactions = filteredTransactions;

    const formatVnd = (value) => `${Number(value).toLocaleString('vi-VN')} đ`;
    const formatCoins = (value) =>
        `${Number(value).toLocaleString('vi-VN', { maximumFractionDigits: 0 })} Coins`;

    const getTypeLabel = (tx) => {
        const map = {
            UNLOCK: 'Mở khóa chương',
            DONATE: 'Ủng hộ (donate)',
        };
        return map[tx?.eventType] || tx?.eventType || '-';
    };
    const getTypeBadgeClass = (type) => {
        const map = {
            UNLOCK: 'bg-emerald-50 text-emerald-700 ring-1 ring-emerald-200/80',
            DONATE: 'bg-fuchsia-100 text-fuchsia-800 ring-1 ring-fuchsia-200/80',
        };
        return map[type] || 'bg-slate-100 text-slate-600 ring-1 ring-slate-200/80';
    };

    const exportHistoryCsv = () => {
        if (!filteredTransactions.length) return;

        const csvEscape = (value) => {
            const s = String(value ?? '');
            return `"${s.replace(/"/g, '""')}"`;
        };

        const headers = ['Thời gian', 'Loại sự kiện', 'Delta ví hệ thống', 'Delta độc giả', 'Delta thu nhập tác giả', 'Delta coin khóa tác giả', 'Story', 'Chapter', 'AdminId', 'BuyerId', 'AuthorId', 'Ghi chú'];
        const csvRows = filteredTransactions.map((tx) => [
            csvEscape(formatDate(tx.eventTime)),
            csvEscape(getTypeLabel(tx)),
            csvEscape(tx.platformDeltaCoins ?? ''),
            csvEscape(tx.buyerDeltaCoins ?? ''),
            csvEscape(tx.authorIncomeDeltaCoins ?? ''),
            csvEscape(tx.authorFrozenDeltaCoins ?? ''),
            csvEscape(tx.storyTitle || ''),
            csvEscape(tx.chapterTitle || ''),
            csvEscape(tx.adminId || ''),
            csvEscape(tx.buyerUserId || ''),
            csvEscape(tx.authorUserId || ''),
            csvEscape(tx.note || ''),
        ]);

        const csv = [headers.map(csvEscape).join(','), ...csvRows.map((r) => r.join(','))].join('\n');
        const blob = new Blob([csv], { type: 'text/csv;charset=utf-8;' });
        const url = URL.createObjectURL(blob);

        const a = document.createElement('a');
        a.href = url;
        a.download = `admin-wallet-history-${new Date().toISOString().slice(0, 10)}.csv`;
        document.body.appendChild(a);
        a.click();
        a.remove();
        URL.revokeObjectURL(url);
    };
    const formatDate = (iso) => {
        const d = new Date(iso);
        return d.toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' });
    };

    const formatSignedCoins = (value) => {
        const n = Number(value ?? 0);
        if (!Number.isFinite(n) || n === 0) return '0';
        return `${n > 0 ? '+' : ''}${n.toLocaleString('vi-VN', { maximumFractionDigits: 2 })}`;
    };

    const deltaToneClass = (value) => {
        const n = Number(value ?? 0);
        if (!Number.isFinite(n) || n === 0) return 'text-slate-600 tabular-nums';
        return n > 0 ? 'text-emerald-600 font-semibold tabular-nums' : 'text-rose-600 font-semibold tabular-nums';
    };

    const handleRefreshOverview = async () => {
        setOverviewLoading(true);
        try {
            const [summaryRes, topAuthorsRes, topSpendersRes] = await Promise.all([
                getAdminWalletSummary(),
                getTopAuthorsByIncome({ take: 10 }),
                getTopSpenders({ take: 10 }),
            ]);

            setLoadError('');
            if (summaryRes) setSummary(summaryRes);
            if (topAuthorsRes?.items) setTopAuthors(topAuthorsRes.items);
            if (topSpendersRes?.items) setTopSpendersState(topSpendersRes.items);

            const bal = summaryRes?.systemWalletBalanceCoins;
            if (typeof bal === 'number') {
                setSystemWalletBalanceCoins(bal);
                window.dispatchEvent(new CustomEvent('system-wallet:balance', { detail: { balance: bal } }));
            }
        } catch (err) {
            // Keep last known data (including mock) if API fails
            setLoadError(err?.response?.status === 401 ? 'Chưa đăng nhập Admin hoặc token hết hạn.' : 'Không tải được dữ liệu từ API. Đang hiển thị dữ liệu gần nhất.');
        } finally {
            setOverviewLoading(false);
        }
    };

    // Load once on mount
    useEffect(() => {
        handleRefreshOverview();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    useEffect(() => {
        if (activeTab !== 'history') return;
        let cancelled = false;
        const loadHistory = async () => {
            try {
                setHistoryLoading(true);
                const res = await getSystemCoinLedger({
                    page: historyPage,
                    pageSize: historyPageSize,
                    dateFrom: historyDateFrom || undefined,
                    dateTo: historyDateTo || undefined,
                    type: historyTypeFilter || 'UNLOCK_AND_DONATE',
                });
                if (cancelled) return;
                setTransactions(Array.isArray(res?.items) ? res.items : []);
                setHistoryTotalCount(Number(res?.totalCount ?? 0) || 0);
                setHistoryTotalPages(Math.max(1, Math.ceil((Number(res?.totalCount ?? 0) || 0) / historyPageSize)));
            } catch {
                if (cancelled) return;
                setTransactions([]);
                setHistoryTotalCount(0);
                setHistoryTotalPages(1);
            } finally {
                if (!cancelled) setHistoryLoading(false);
            }
        };
        loadHistory();
        return () => {
            cancelled = true;
        };
    }, [activeTab, historyPage, historyPageSize, historyDateFrom, historyDateTo, historyTypeFilter]);

    return (
        <div className="space-y-6 rounded-2xl">
            {loadError ? (
                <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-[12px] font-semibold text-amber-800">
                    {loadError}
                </div>
            ) : null}
            {/* Header + Tabs */}
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
                <div className="border-l-4 border-slate-300 pl-4">
                    <h1 className="text-2xl font-bold text-slate-900">
                        Ví hệ thống
                    </h1>
                    <p className="mt-1 text-sm text-slate-600">
                        Tổng quan dòng tiền: <span className="font-semibold text-slate-800">coin</span>, thu nhập tác giả, doanh thu nền tảng.
                    </p>
                </div>
                <div className="inline-flex shrink-0 items-center gap-1 rounded-xl border border-slate-200/90 bg-white/90 p-1 shadow-sm">
                    <button
                        type="button"
                        onClick={() => setActiveTab('overview')}
                        className={`rounded-lg px-4 py-2 text-sm font-semibold transition-all ${
                            activeTab === 'overview'
                                ? 'bg-slate-900 text-white shadow-sm'
                                : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
                        }`}
                    >
                        Tổng quan
                    </button>
                    <button
                        type="button"
                        onClick={() => setActiveTab('history')}
                        className={`flex items-center gap-2 rounded-lg px-4 py-2 text-sm font-semibold transition-all ${
                            activeTab === 'history'
                                ? 'bg-slate-900 text-white shadow-sm'
                                : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
                        }`}
                    >
                        <History className="h-4 w-4" />
                        Lịch sử giao dịch ví
                    </button>
                </div>
            </div>

            {activeTab === 'overview' && (
                <>
                    {/* Nút làm mới */}
                    <div className="flex justify-end">
                        <button
                            onClick={handleRefreshOverview}
                            disabled={overviewLoading}
                            className="flex items-center gap-2 rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm font-semibold text-slate-700 transition hover:bg-slate-50 disabled:opacity-60"
                        >
                            <RefreshCw className={`w-4 h-4 ${overviewLoading ? 'animate-spin' : ''}`} />
                            {overviewLoading ? 'Đang tải...' : 'Làm mới'}
                        </button>
                    </div>

                    {/* Summary cards */}
                    <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
                        <div className="flex items-start gap-3 rounded-xl border border-slate-200 bg-white p-4">
                            <div className="flex h-10 w-10 items-center justify-center rounded-full bg-emerald-50">
                                <Wallet className="h-5 w-5 text-emerald-600" />
                            </div>
                            <div className="flex-1">
                                <p className="text-xs font-medium text-slate-500">Tổng coin trong hệ thống</p>
                                <p className="mt-1 text-lg font-bold text-slate-900">
                                    {overviewLoading ? '...' : formatCoins(summarySafe.totalCoinsInSystem)}
                                </p>
                                <p className="mt-1 text-xs text-slate-400">
                                    Bao gồm số dư ví người dùng, ví tác giả, coin đang chờ đối soát.
                                </p>
                            </div>
                        </div>

                        <div className="bg-white rounded-xl border border-slate-200 p-4 flex items-start gap-3">
                            <div className="w-10 h-10 rounded-full bg-blue-50 flex items-center justify-center">
                                <ArrowDownCircle className="w-5 h-5 text-blue-500" />
                            </div>
                            <div className="flex-1">
                                <p className="text-xs font-medium text-slate-500">Tổng tiền người dùng đã nạp</p>
                                <p className="mt-1 text-lg font-bold text-slate-900">
                                    {overviewLoading ? '...' : formatVnd(summarySafe.totalRechargeVnd)}
                                </p>
                                <p className="mt-1 text-xs text-slate-400">
                                    Cộng dồn tất cả order PayOS/VNPAY đã thanh toán.
                                </p>
                            </div>
                        </div>

                        <div className="bg-white rounded-xl border border-slate-200 p-4 flex items-start gap-3">
                            <div className="w-10 h-10 rounded-full bg-amber-50 flex items-center justify-center">
                                <ArrowUpCircle className="w-5 h-5 text-amber-500" />
                            </div>
                            <div className="flex-1">
                                <p className="text-xs font-medium text-slate-500">Đã chi trả cho tác giả</p>
                                <p className="mt-1 text-lg font-bold text-slate-900">
                                    {overviewLoading
                                        ? '...'
                                        : summarySafe.totalWithdrawVnd != null
                                            ? formatVnd(summarySafe.totalWithdrawVnd)
                                            : formatCoins(summarySafe.totalWithdrawCoins)}
                                </p>
                                <p className="mt-1 text-xs text-slate-400">
                                    Tổng số tiền đã rút ra ngân hàng cho tác giả.
                                </p>
                            </div>
                        </div>

                        <div className="bg-white rounded-xl border border-slate-200 p-4 flex items-start gap-3">
                            <div className="w-10 h-10 rounded-full bg-purple-50 flex items-center justify-center">
                                <LineChart className="w-5 h-5 text-purple-500" />
                            </div>
                            <div className="flex-1">
                                <p className="text-xs font-medium text-slate-500">Doanh thu nền tảng (ước tính)</p>
                                <p className="mt-1 text-lg font-bold text-slate-900">
                                    {overviewLoading
                                        ? '...'
                                        : summarySafe.platformRevenueVnd != null
                                            ? formatVnd(summarySafe.platformRevenueVnd)
                                            : '—'}
                                </p>
                                <p className="mt-1 text-xs text-slate-400">
                                    Tổng phí nền tảng thu được (30%) quy đổi theo tỷ giá cố định.
                                </p>
                            </div>
                        </div>
                    </div>

                    {/* Balances + Chart */}
                    <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                        <div className="lg:col-span-2 rounded-xl border border-slate-200 bg-white p-5">
                            <div className="mb-4 flex flex-wrap items-center justify-between gap-2">
                                <h2 className="flex items-center gap-2 text-sm font-semibold text-slate-900">
                                    <span className="h-2 w-2 shrink-0 rounded-full bg-emerald-500" aria-hidden />
                                    Phân bổ số dư ví
                                </h2>
                                <div className="flex items-center gap-2">
                                    <span className="text-xs text-slate-400">Thu nhập theo:</span>
                                    <select
                                        value={chartRange}
                                        onChange={(e) => setChartRange(e.target.value)}
                                        className="text-xs border border-slate-200 rounded px-2 py-1 text-slate-700"
                                    >
                                        <option value="7">7 ngày</option>
                                        <option value="30">30 ngày</option>
                                    </select>
                                </div>
                            </div>

                            <div className="grid grid-cols-1 sm:grid-cols-3 gap-4">
                                <div className="flex items-center gap-3 rounded-lg border border-emerald-100 bg-emerald-50/60 px-3 py-2.5">
                                    <div className="w-9 h-9 rounded-full bg-emerald-500/10 flex items-center justify-center">
                                        <Wallet className="w-5 h-5 text-emerald-500" />
                                    </div>
                                    <div>
                                        <p className="text-xs text-slate-500">Thu nhập khả dụng của tác giả</p>
                                        <p className="text-sm font-semibold text-slate-900">
                                            {formatCoins(summarySafe.totalIncomeBalance)}
                                        </p>
                                    </div>
                                </div>
                                <div className="flex items-center gap-3 rounded-lg border border-slate-200 bg-slate-50/80 px-3 py-2.5">
                                    <div className="w-9 h-9 rounded-full bg-slate-500/10 flex items-center justify-center">
                                        <Lock className="w-5 h-5 text-slate-500" />
                                    </div>
                                    <div>
                                        <p className="text-xs text-slate-500">Số dư bị khóa</p>
                                        <p className="text-sm font-semibold text-slate-900">
                                            {formatCoins(summarySafe.totalFrozenBalance)}
                                        </p>
                                    </div>
                                </div>
                                <div className="flex items-center gap-3 rounded-lg border border-amber-100 bg-amber-50/60 px-3 py-2.5">
                                    <div className="w-9 h-9 rounded-full bg-amber-500/10 flex items-center justify-center">
                                        <ArrowDownCircle className="w-5 h-5 text-amber-500" />
                                    </div>
                                    <div>
                                        <p className="text-xs text-slate-500">Coin đang treo (escrow)</p>
                                        <p className="text-sm font-semibold text-slate-900">
                                            {formatCoins(summarySafe.totalPendingEscrow)}
                                        </p>
                                    </div>
                                </div>
                            </div>

                            <div className="mt-6">
                                <p className="text-xs font-medium text-slate-500 mb-2">
                                    Thu nhập theo ngày {chartRange === '30' ? '(30 ngày gần nhất)' : '(7 ngày)'}
                                </p>
                                <div className="h-52 flex items-end justify-between gap-2">
                                    {dailyIncome.map((d) => {
                                        const height = Math.round((d.income / maxIncome) * 100);
                                        return (
                                            <div
                                                key={d.day}
                                                className="flex-1 flex flex-col items-center gap-1"
                                            >
                                                <div className="relative w-full h-full flex items-end">
                                                    <div
                                                        className="w-full bg-primary/30 hover:bg-primary/60 rounded-t-md transition-colors cursor-pointer relative group"
                                                        style={{ height: `${Math.max(height, 10)}%` }}
                                                    >
                                                        <div className="absolute -top-7 left-1/2 -translate-x-1/2 bg-slate-900 text-white text-[10px] px-2 py-1 rounded opacity-0 group-hover:opacity-100 whitespace-nowrap z-10">
                                                            {formatVnd(d.income)}
                                                        </div>
                                                    </div>
                                                </div>
                                                <span className="text-[11px] text-slate-500">{d.day}</span>
                                            </div>
                                        );
                                    })}
                                </div>
                            </div>
                        </div>

                        <div className="bg-white rounded-xl border border-slate-200 p-5 space-y-4">
                            <h2 className="text-sm font-semibold text-slate-900">Hoạt động ví</h2>
                            <div className="grid grid-cols-2 gap-3">
                                <div className="rounded-lg border border-slate-200 bg-slate-50/70 p-3 flex flex-col gap-1">
                                    <div className="flex items-center gap-2">
                                        <Users className="w-4 h-4 text-primary" />
                                        <span className="text-xs font-medium text-slate-600">Độc giả hoạt động</span>
                                    </div>
                                    <p className="text-lg font-bold text-slate-900 mt-1">
                                        {Number(summarySafe.activeReaders).toLocaleString('vi-VN')}
                                    </p>
                                </div>
                                <div className="rounded-lg border border-slate-200 bg-slate-50/70 p-3 flex flex-col gap-1">
                                    <div className="flex items-center gap-2">
                                        <Book className="w-4 h-4 text-emerald-500" />
                                        <span className="text-xs font-medium text-slate-600">Tác giả có thu nhập</span>
                                    </div>
                                    <p className="text-lg font-bold text-slate-900 mt-1">
                                        {Number(summarySafe.activeAuthors).toLocaleString('vi-VN')}
                                    </p>
                                </div>
                            </div>
                            <div className="border-t border-slate-200 pt-4">
                                <p className="text-xs font-medium text-slate-500 mb-2">Gợi ý luồng dữ liệu</p>
                                <ul className="text-xs text-slate-500 space-y-1 list-disc pl-4">
                                    <li>Nguồn tiền vào: PayOS/VNPAY → coin_orders (PAID) → cập nhật ví.</li>
                                    <li>Chi tiêu: mua chương VIP → chuyển coin từ ví độc giả sang thu nhập tác giả.</li>
                                    <li>Rút tiền: trừ income_balance, tăng frozen cho tới khi admin thanh toán.</li>
                                </ul>
                            </div>
                        </div>
                    </div>

                    {/* Top authors & top spenders */}
                    <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                        <div className="bg-white rounded-xl border border-slate-200 p-5">
                            <h2 className="text-sm font-semibold text-slate-900 mb-3">Top tác giả theo thu nhập</h2>
                            <div className="space-y-2">
                                {topAuthorsView.map((a, idx) => (
                                    <div
                                        key={a.id}
                                        className="flex items-center gap-3 px-2 py-2 rounded-lg hover:bg-slate-50 transition-colors"
                                    >
                                        <div className="w-6 h-6 rounded-full bg-primary/10 text-primary text-xs flex items-center justify-center font-semibold">
                                            {idx + 1}
                                        </div>
                                        <div className="flex-1 min-w-0">
                                            <p className="text-sm font-medium text-slate-900 truncate">{a.name}</p>
                                            <p className="text-[11px] text-slate-500">{a.stories} truyện đang bật chương VIP</p>
                                        </div>
                                        <div className="text-right">
                                            <p className="text-sm font-semibold text-emerald-600">{formatVnd(a.income)}</p>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        </div>
                        <div className="bg-white rounded-xl border border-slate-200 p-5">
                            <h2 className="text-sm font-semibold text-slate-900 mb-3">Top độc giả chi tiêu coin</h2>
                            <div className="space-y-2">
                                {topSpendersView.map((u, idx) => (
                                    <div
                                        key={u.id}
                                        className="flex items-center gap-3 px-2 py-2 rounded-lg hover:bg-slate-50 transition-colors"
                                    >
                                        <div className="w-6 h-6 rounded-full bg-slate-100 text-slate-700 text-xs flex items-center justify-center font-semibold">
                                            {idx + 1}
                                        </div>
                                        <div className="flex-1 min-w-0">
                                            <p className="text-sm font-medium text-slate-900 truncate">{u.name}</p>
                                        </div>
                                        <div className="text-right">
                                            <p className="text-sm font-semibold text-slate-700">{formatCoins(u.coins)}</p>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        </div>
                    </div>
                </>
            )}

            {activeTab === 'history' && (
                <div className="overflow-hidden rounded-2xl border border-slate-200 bg-white shadow-sm">
                    {/* Phạm vi dữ liệu */}
                    <div className="border-b border-slate-200 bg-slate-50/70 px-5 py-4 sm:px-6">
                        <div className="flex gap-3">
                            <div className="mt-0.5 flex h-10 w-10 shrink-0 items-center justify-center rounded-xl bg-slate-200 text-slate-700">
                                <Info className="h-4 w-4" aria-hidden />
                            </div>
                            <div className="min-w-0 space-y-1">
                                <p className="text-sm font-semibold text-slate-900">Phạm vi bảng này</p>
                                <p className="text-sm leading-relaxed text-slate-600">
                                    Chỉ <span className="font-semibold text-slate-800">ủng hộ (donate)</span> và{' '}
                                    <span className="font-semibold text-slate-800">mở khóa chương</span> (phí nền tảng 30% / thu nhập tác giả).
                                    Lịch <span className="font-medium text-slate-800">nạp coin / rút tiền</span> nằm ở khối{' '}
                                    <span className="rounded-md bg-white px-1.5 py-0.5 font-semibold text-slate-700 shadow-sm ring-1 ring-slate-200">
                                        Lịch sử giao dịch (Nạp / Rút)
                                    </span>{' '}
                                    bên dưới.
                                </p>
                            </div>
                        </div>
                    </div>

                    {/* Filters */}
                    <div className="border-b border-slate-200 bg-slate-50/40 px-4 py-4 sm:px-6">
                        <div className="grid grid-cols-1 gap-4 lg:grid-cols-12 lg:items-end">
                            <label className="block min-w-0 lg:col-span-4">
                                <span className="mb-1.5 block text-xs font-semibold text-slate-500">Tìm kiếm</span>
                                <div className="relative">
                                    <Search className="pointer-events-none absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-slate-400" />
                                    <input
                                        type="text"
                                        placeholder="User, truyện, chương, mã tham chiếu..."
                                        value={historySearch}
                                        onChange={(e) => {
                                            setHistorySearch(e.target.value);
                                            setHistoryPage(1);
                                        }}
                                        className="w-full rounded-xl border border-slate-200 bg-white py-2.5 pl-10 pr-3 text-sm text-slate-800 shadow-sm transition placeholder:text-slate-400 focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                                    />
                                </div>
                            </label>
                            <label className="block lg:col-span-3">
                                <span className="mb-1.5 block text-xs font-semibold text-slate-500">Loại sự kiện</span>
                                <select
                                    value={historyTypeFilter}
                                    onChange={(e) => {
                                        setHistoryTypeFilter(e.target.value);
                                        setHistoryPage(1);
                                    }}
                                    className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm text-slate-800 shadow-sm focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                                >
                                    {LEDGER_EVENT_TYPES.map((x) => (
                                        <option key={x.value} value={x.value}>
                                            {x.label}
                                        </option>
                                    ))}
                                </select>
                            </label>
                            <div className="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:col-span-3">
                                <label className="block min-w-0">
                                    <span className="mb-1.5 block text-xs font-semibold text-slate-500">Từ ngày</span>
                                    <input
                                        type="date"
                                        value={historyDateFrom}
                                        onChange={(e) => {
                                            setHistoryDateFrom(e.target.value);
                                            setHistoryPage(1);
                                        }}
                                        className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm text-slate-800 shadow-sm focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                                    />
                                </label>
                                <label className="block min-w-0">
                                    <span className="mb-1.5 block text-xs font-semibold text-slate-500">Đến ngày</span>
                                    <input
                                        type="date"
                                        value={historyDateTo}
                                        onChange={(e) => {
                                            setHistoryDateTo(e.target.value);
                                            setHistoryPage(1);
                                        }}
                                        className="w-full rounded-xl border border-slate-200 bg-white px-3 py-2.5 text-sm text-slate-800 shadow-sm focus:border-primary focus:outline-none focus:ring-2 focus:ring-primary/20"
                                    />
                                </label>
                            </div>
                            <div className="flex lg:col-span-2 lg:justify-end">
                                <button
                                    type="button"
                                    onClick={exportHistoryCsv}
                                    disabled={filteredTransactions.length === 0}
                                    className={`inline-flex w-full items-center justify-center gap-2 rounded-xl px-4 py-2.5 text-sm font-semibold shadow-sm transition sm:w-auto ${
                                        filteredTransactions.length === 0
                                            ? 'cursor-not-allowed border border-slate-200 bg-slate-100 text-slate-400'
                                            : 'border border-slate-300 bg-white text-slate-700 hover:bg-slate-50'
                                    }`}
                                >
                                    <FileDown className="h-4 w-4 shrink-0" />
                                    Xuất CSV
                                </button>
                            </div>
                        </div>
                    </div>

                    {/* Table */}
                    <div className="overflow-x-auto">
                        <table className="w-full min-w-[920px] table-fixed border-collapse text-left text-sm text-slate-700">
                            <thead>
                                <tr className="border-b border-slate-200 bg-slate-50/90">
                                    <th className="w-[11%] px-4 py-3.5 text-xs font-semibold text-slate-600" title="Thời điểm ghi nhận">
                                        Thời gian
                                    </th>
                                    <th className="w-[12%] px-4 py-3.5 text-xs font-semibold text-slate-600">Loại</th>
                                    <th className="w-[9%] px-0 py-3.5 text-center text-xs font-semibold text-slate-600" title="Thay đổi ví nền tảng">
                                        Ví HT
                                    </th>
                                    <th className="w-[9%] px-0 py-3.5 text-center text-xs font-semibold text-slate-600" title="Thay đổi ví độc giả">
                                        Độc giả
                                    </th>
                                    <th className="w-[9%] px-0 py-3.5 text-center text-xs font-semibold text-slate-600" title="Thu nhập tác giả">
                                        Tác giả
                                    </th>
                                    <th className="w-[8%] px-0 py-3.5 text-center text-xs font-semibold text-slate-600" title="Coin khóa">
                                        Khóa
                                    </th>
                                    <th className="w-[32%] min-w-[240px] px-4 py-3.5 text-xs font-semibold text-slate-600">Chi tiết</th>
                                    {hasAnyNote ? (
                                        <th className="w-[10%] px-4 py-3.5 text-xs font-semibold text-slate-600">Ghi chú</th>
                                    ) : null}
                                </tr>
                            </thead>
                            <tbody className="divide-y divide-slate-100">
                                {paginatedTransactions.length === 0 ? (
                                    <tr>
                                        <td
                                            colSpan={hasAnyNote ? 8 : 7}
                                            className="px-4 py-16 text-center"
                                        >
                                            <div className="mx-auto max-w-sm">
                                                <p className="text-sm font-medium text-slate-700">
                                                    {historyLoading ? 'Đang tải lịch sử…' : 'Không có giao dịch nào'}
                                                </p>
                                                <p className="mt-1 text-xs text-slate-500">
                                                    {historyLoading
                                                        ? 'Vui lòng chờ trong giây lát.'
                                                        : 'Thử đổi khoảng thời gian hoặc bộ lọc loại sự kiện.'}
                                                </p>
                                            </div>
                                        </td>
                                    </tr>
                                ) : (
                                    paginatedTransactions.map((tx) => (
                                        <tr
                                            key={`${tx.eventType}-${tx.eventTime}-${tx.adminId || ''}-${tx.authorUserId || ''}-${tx.buyerUserId || ''}`}
                                            className="align-top transition-colors hover:bg-slate-50/80"
                                        >
                                            <td className="px-4 py-3.5">
                                                <span className="font-medium text-slate-800">{formatDate(tx.eventTime)}</span>
                                            </td>
                                            <td className="px-4 py-3.5">
                                                <span
                                                    className={`inline-flex max-w-full items-center rounded-full px-2.5 py-1 text-xs font-semibold ${getTypeBadgeClass(tx.eventType)}`}
                                                >
                                                    {getTypeLabel(tx)}
                                                </span>
                                            </td>
                                            <td className={`px-1 py-3.5 text-center text-sm ${deltaToneClass(tx.platformDeltaCoins)}`}>
                                                {formatSignedCoins(tx.platformDeltaCoins)}
                                            </td>
                                            <td className={`px-1 py-3.5 text-center text-sm ${deltaToneClass(tx.buyerDeltaCoins)}`}>
                                                {formatSignedCoins(tx.buyerDeltaCoins)}
                                            </td>
                                            <td className={`px-1 py-3.5 text-center text-sm ${deltaToneClass(tx.authorIncomeDeltaCoins)}`}>
                                                {formatSignedCoins(tx.authorIncomeDeltaCoins)}
                                            </td>
                                            <td className={`px-1 py-3.5 text-center text-sm ${deltaToneClass(tx.authorFrozenDeltaCoins)}`}>
                                                {formatSignedCoins(tx.authorFrozenDeltaCoins)}
                                            </td>
                                            <td className="px-4 py-3.5">
                                                <div className="space-y-2">
                                                    {(tx.storyTitle || tx.chapterTitle) && (
                                                        <div className="flex flex-wrap items-baseline gap-x-2 gap-y-0.5 text-sm">
                                                            {tx.storyTitle ? (
                                                                <span className="font-semibold text-slate-900">{tx.storyTitle}</span>
                                                            ) : null}
                                                            {tx.chapterTitle ? (
                                                                <span className="text-slate-600">
                                                                    {tx.storyTitle ? '·' : ''} {tx.chapterTitle}
                                                                </span>
                                                            ) : null}
                                                        </div>
                                                    )}
                                                    <div className="space-y-1.5">
                                                        <IdLine roleLabel="Độc giả" id={tx.buyerUserId} />
                                                        <IdLine roleLabel="Tác giả" id={tx.authorUserId} />
                                                        <IdLine roleLabel="Admin" id={tx.adminId} />
                                                    </div>
                                                    {!tx.storyTitle &&
                                                    !tx.chapterTitle &&
                                                    !tx.adminId &&
                                                    !tx.buyerUserId &&
                                                    !tx.authorUserId ? (
                                                        <span className="text-xs text-slate-400">—</span>
                                                    ) : null}
                                                </div>
                                            </td>
                                            {hasAnyNote ? (
                                                <td className="px-4 py-3.5 text-sm text-slate-600">
                                                    <span className="line-clamp-2" title={tx.note || ''}>
                                                        {tx.note && String(tx.note).trim() ? tx.note : '—'}
                                                    </span>
                                                </td>
                                            ) : null}
                                        </tr>
                                    ))
                                )}
                            </tbody>
                        </table>
                    </div>

                    {/* Pagination */}
                    {filteredTransactions.length > 0 && (
                        <div className="flex flex-col gap-3 border-t border-slate-200 bg-slate-50/30 px-4 py-3.5 sm:flex-row sm:items-center sm:justify-between sm:px-6">
                            <p className="text-xs text-slate-600">
                                Hiển thị{' '}
                                <span className="font-semibold text-slate-700">
                                    {(historyPage - 1) * historyPageSize + 1}–{Math.min(historyPage * historyPageSize, historyTotalCount)}
                                </span>{' '}
                                / <span className="font-semibold text-slate-700">{historyTotalCount}</span> bản ghi
                            </p>
                            <div className="flex items-center justify-end gap-1">
                                <button
                                    type="button"
                                    onClick={() => setHistoryPage((p) => Math.max(1, p - 1))}
                                    disabled={historyPage <= 1}
                                    className="rounded-lg border border-slate-200 bg-white p-2 text-slate-700 shadow-sm transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
                                    aria-label="Trang trước"
                                >
                                    <ChevronLeft className="h-4 w-4" />
                                </button>
                                <span className="min-w-[7rem] px-2 text-center text-sm font-semibold text-slate-700">
                                    Trang {historyPage} / {totalHistoryPages}
                                </span>
                                <button
                                    type="button"
                                    onClick={() => setHistoryPage((p) => Math.min(totalHistoryPages, p + 1))}
                                    disabled={historyPage >= totalHistoryPages}
                                    className="rounded-lg border border-slate-200 bg-white p-2 text-slate-700 shadow-sm transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-40"
                                    aria-label="Trang sau"
                                >
                                    <ChevronRight className="h-4 w-4" />
                                </button>
                            </div>
                        </div>
                    )}

                    <div className="border-t border-slate-200 bg-slate-50/20 p-4 sm:p-6">
                        <AdminTransactions />
                    </div>
                </div>
            )}
        </div>
    );
}
