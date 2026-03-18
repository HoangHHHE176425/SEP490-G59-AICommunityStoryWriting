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
    Filter,
} from 'lucide-react';
import { getAdminWalletSummary, getTopAuthorsByIncome, getTopSpenders } from '../../../api/admin/walletApi';

// FE mock: Ví hệ thống (admin) - sau này nối API:
// - GET /api/admin/wallet/summary
// - GET /api/admin/wallet/top-authors, top-spenders
// - GET /api/admin/wallet/transactions (phân trang, lọc)

const TRANSACTION_TYPES = [
    { value: '', label: 'Tất cả' },
    { value: 'DEPOSIT', label: 'Nạp coin' },
    { value: 'PURCHASE_CHAPTER', label: 'Mua chương VIP' },
    { value: 'AUTHOR_INCOME', label: 'Thu nhập tác giả' },
    { value: 'PLATFORM_FEE', label: 'Phí nền tảng (30%)' },
    { value: 'WITHDRAW', label: 'Rút tiền' },
    { value: 'REFUND', label: 'Hoàn coin' },
    { value: 'PROMOTION', label: 'Khuyến mãi' },
];

// Mock danh sách giao dịch ví hệ thống
const MOCK_WALLET_TRANSACTIONS = [
    { id: 'tx1', type: 'DEPOSIT', userId: 'user_001', userDisplay: 'user_001', amountCoins: 50000, amountVnd: 50000, status: 'SUCCESS', refId: 'ORD-2024-001', createdAt: '2025-03-17T10:30:00Z' },
    { id: 'tx2', type: 'PURCHASE_CHAPTER', userId: 'user_029', userDisplay: 'user_029', amountCoins: -500, amountVnd: 0, status: 'SUCCESS', refId: 'CH-abc-12', storyTitle: 'Tu Tiên Chi Lộ', createdAt: '2025-03-17T09:15:00Z' },
    { id: 'tx3', type: 'AUTHOR_INCOME', userId: 'author_tt', userDisplay: 'Thiên Tằm Thổ Đậu', amountCoins: 350, amountVnd: 0, status: 'SUCCESS', refId: 'CH-abc-12', storyTitle: 'Tu Tiên Chi Lộ', createdAt: '2025-03-17T09:15:01Z' },
    { id: 'tx3-fee', type: 'PLATFORM_FEE', userId: 'SYSTEM', userDisplay: 'Nền tảng', amountCoins: 150, amountVnd: 0, status: 'SUCCESS', refId: 'CH-abc-12', storyTitle: 'Tu Tiên Chi Lộ', createdAt: '2025-03-17T09:15:02Z' },
    { id: 'tx4', type: 'WITHDRAW', userId: 'author_tt', userDisplay: 'Thiên Tằm Thổ Đậu', amountCoins: 0, amountVnd: 5_000_000, status: 'SUCCESS', refId: 'WD-2024-089', createdAt: '2025-03-16T14:00:00Z' },
    { id: 'tx5', type: 'DEPOSIT', userId: 'user_312', userDisplay: 'user_312', amountCoins: 100000, amountVnd: 100000, status: 'SUCCESS', refId: 'ORD-2024-002', createdAt: '2025-03-16T11:20:00Z' },
    { id: 'tx6', type: 'REFUND', userId: 'user_777', userDisplay: 'user_777', amountCoins: 200, amountVnd: 0, status: 'SUCCESS', refId: 'REF-003', createdAt: '2025-03-15T16:45:00Z' },
    { id: 'tx7', type: 'PROMOTION', userId: 'user_new', userDisplay: 'user_new', amountCoins: 1000, amountVnd: 0, status: 'SUCCESS', refId: 'PROMO-SIGNUP', createdAt: '2025-03-15T08:00:00Z' },
    { id: 'tx8', type: 'WITHDRAW', userId: 'author_nga', userDisplay: 'Ngã Cật Tây Hồng Thị', amountCoins: 0, amountVnd: 2_500_000, status: 'PENDING', refId: 'WD-2024-090', createdAt: '2025-03-17T08:00:00Z' },
];

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

export function AdminWalletDashboard() {
    const [activeTab, setActiveTab] = useState('overview');
    const [chartRange, setChartRange] = useState('7'); // 7 | 30 ngày
    const [overviewLoading, setOverviewLoading] = useState(false);
    const [historyTypeFilter, setHistoryTypeFilter] = useState('');
    const [historySearch, setHistorySearch] = useState('');
    const [historyDateFrom, setHistoryDateFrom] = useState('');
    const [historyDateTo, setHistoryDateTo] = useState('');
    const [historyPage, setHistoryPage] = useState(1);
    const historyPageSize = 5;
    const [transactions, setTransactions] = useState(() => MOCK_WALLET_TRANSACTIONS);
    const [refundLoadingRef, setRefundLoadingRef] = useState('');
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

    // Lọc lịch sử giao dịch (mock)
    const filteredTransactions = useMemo(() => {
        let list = [...transactions];
        if (historyTypeFilter) {
            list = list.filter((t) => t.type === historyTypeFilter);
        }
        if (historySearch.trim()) {
            const q = historySearch.trim().toLowerCase();
            list = list.filter(
                (t) =>
                    (t.userDisplay && t.userDisplay.toLowerCase().includes(q)) ||
                    (t.refId && t.refId.toLowerCase().includes(q)) ||
                    (t.storyTitle && t.storyTitle.toLowerCase().includes(q))
            );
        }
        if (historyDateFrom) {
            list = list.filter((t) => t.createdAt >= historyDateFrom + 'T00:00:00Z');
        }
        if (historyDateTo) {
            list = list.filter((t) => t.createdAt <= historyDateTo + 'T23:59:59Z');
        }
        return list.sort((a, b) => String(b.createdAt).localeCompare(String(a.createdAt)));
    }, [historyTypeFilter, historySearch, historyDateFrom, historyDateTo, transactions]);

    const totalHistoryPages = Math.max(1, Math.ceil(filteredTransactions.length / historyPageSize));
    const paginatedTransactions = useMemo(() => {
        const start = (historyPage - 1) * historyPageSize;
        return filteredTransactions.slice(start, start + historyPageSize);
    }, [filteredTransactions, historyPage, historyPageSize]);

    const formatVnd = (value) => `${Number(value).toLocaleString('vi-VN')} đ`;
    const formatCoins = (value) =>
        `${Number(value).toLocaleString('vi-VN', { maximumFractionDigits: 0 })} Coins`;

    const getTypeLabel = (type) => TRANSACTION_TYPES.find((t) => t.value === type)?.label || type;
    const getTypeBadgeClass = (type) => {
        const map = {
            DEPOSIT: 'bg-emerald-100 text-emerald-700',
            PURCHASE_CHAPTER: 'bg-blue-100 text-blue-700',
            AUTHOR_INCOME: 'bg-amber-100 text-amber-700',
            PLATFORM_FEE: 'bg-emerald-100 text-emerald-800',
            WITHDRAW: 'bg-purple-100 text-purple-700',
            REFUND: 'bg-slate-100 text-slate-700',
            PROMOTION: 'bg-pink-100 text-pink-700',
        };
        return map[type] || 'bg-slate-100 text-slate-600';
    };
    const formatDate = (iso) => {
        const d = new Date(iso);
        return d.toLocaleString('vi-VN', { dateStyle: 'short', timeStyle: 'short' });
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

    const isRefundable = (tx) => {
        if (!tx) return false;
        if (tx.type !== 'PURCHASE_CHAPTER') return false;
        if (tx.status !== 'SUCCESS') return false;
        const hasRefund = transactions.some((t) => t.type === 'REFUND' && t.refId === tx.refId);
        return !hasRefund;
    };

    const handleRefundCoinsForPurchase = async (purchaseTx) => {
        if (!isRefundable(purchaseTx)) return;
        const ok = window.confirm(
            `Hoàn lại coin cho giao dịch mở khóa chương?\n\nUser: ${purchaseTx.userDisplay}\nSố coin hoàn: ${Math.abs(purchaseTx.amountCoins)}`
        );
        if (!ok) return;

        try {
            setRefundLoadingRef(purchaseTx.refId);
            // FE mock delay
            await new Promise((r) => setTimeout(r, 450));

            const now = new Date().toISOString();
            const purchaseCoins = Math.abs(Number(purchaseTx.amountCoins || 0));
            const authorIncomeTx = transactions.find((t) => t.type === 'AUTHOR_INCOME' && t.refId === purchaseTx.refId);
            const feeTx = transactions.find((t) => t.type === 'PLATFORM_FEE' && t.refId === purchaseTx.refId);

            const next = [
                // Hoàn coin cho user
                {
                    id: `rf-${purchaseTx.refId}-${Date.now()}`,
                    type: 'REFUND',
                    userId: purchaseTx.userId,
                    userDisplay: purchaseTx.userDisplay,
                    amountCoins: purchaseCoins,
                    amountVnd: 0,
                    status: 'SUCCESS',
                    refId: purchaseTx.refId,
                    storyTitle: purchaseTx.storyTitle,
                    createdAt: now,
                },
                // Đảo thu nhập tác giả (nếu có)
                ...(authorIncomeTx
                    ? [
                          {
                              id: `rv-ai-${purchaseTx.refId}-${Date.now()}`,
                              type: 'AUTHOR_INCOME',
                              userId: authorIncomeTx.userId,
                              userDisplay: authorIncomeTx.userDisplay,
                              amountCoins: -Math.abs(Number(authorIncomeTx.amountCoins || 0)),
                              amountVnd: 0,
                              status: 'SUCCESS',
                              refId: purchaseTx.refId,
                              storyTitle: purchaseTx.storyTitle,
                              createdAt: now,
                          },
                      ]
                    : []),
                // Đảo phí nền tảng 30% (nếu có)
                ...(feeTx
                    ? [
                          {
                              id: `rv-fee-${purchaseTx.refId}-${Date.now()}`,
                              type: 'PLATFORM_FEE',
                              userId: 'SYSTEM',
                              userDisplay: 'Nền tảng',
                              amountCoins: -Math.abs(Number(feeTx.amountCoins || 0)),
                              amountVnd: 0,
                              status: 'SUCCESS',
                              refId: purchaseTx.refId,
                              storyTitle: purchaseTx.storyTitle,
                              createdAt: now,
                          },
                      ]
                    : []),
            ];

            setTransactions((prev) => [...next, ...prev]);

            // Mock update số dư ví hệ thống: giảm phần fee đã ăn (nếu có)
            const feeCoins = feeTx ? Math.abs(Number(feeTx.amountCoins || 0)) : Math.round(purchaseCoins * 0.3);
            if (typeof systemWalletBalanceCoins === 'number') {
                const newBalance = Math.max(0, systemWalletBalanceCoins - feeCoins);
                setSystemWalletBalanceCoins(newBalance);
                window.dispatchEvent(new CustomEvent('system-wallet:balance', { detail: { balance: newBalance } }));
            }
        } finally {
            setRefundLoadingRef('');
        }
    };

    return (
        <div className="space-y-6">
            {loadError ? (
                <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-[12px] font-semibold text-amber-800">
                    {loadError}
                </div>
            ) : null}
            {/* Header + Tabs */}
            <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900">Ví hệ thống</h1>
                    <p className="text-sm text-slate-500 mt-1">
                        Tổng quan dòng tiền trong hệ thống: coin, thu nhập tác giả, doanh thu nền tảng.
                    </p>
                </div>
                <div className="flex items-center gap-2 border-b border-slate-200">
                    <button
                        onClick={() => setActiveTab('overview')}
                        className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors ${
                            activeTab === 'overview'
                                ? 'border-primary text-primary'
                                : 'border-transparent text-slate-500 hover:text-slate-700'
                        }`}
                    >
                        Tổng quan
                    </button>
                    <button
                        onClick={() => setActiveTab('history')}
                        className={`px-4 py-2 text-sm font-medium border-b-2 transition-colors flex items-center gap-2 ${
                            activeTab === 'history'
                                ? 'border-primary text-primary'
                                : 'border-transparent text-slate-500 hover:text-slate-700'
                        }`}
                    >
                        <History className="w-4 h-4" />
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
                            className="flex items-center gap-2 px-3 py-2 text-sm font-medium text-slate-600 bg-white border border-slate-200 rounded-lg hover:bg-slate-50 disabled:opacity-60"
                        >
                            <RefreshCw className={`w-4 h-4 ${overviewLoading ? 'animate-spin' : ''}`} />
                            {overviewLoading ? 'Đang tải...' : 'Làm mới'}
                        </button>
                    </div>

                    {/* Summary cards */}
                    <div className="grid grid-cols-1 md:grid-cols-2 xl:grid-cols-4 gap-4">
                        <div className="bg-white rounded-xl border border-slate-200 p-4 flex items-start gap-3">
                            <div className="w-10 h-10 rounded-full bg-emerald-50 flex items-center justify-center">
                                <Wallet className="w-5 h-5 text-emerald-500" />
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
                        <div className="lg:col-span-2 bg-white rounded-xl border border-slate-200 p-5">
                            <div className="flex flex-wrap items-center justify-between gap-2 mb-4">
                                <h2 className="text-sm font-semibold text-slate-900">Phân bổ số dư ví</h2>
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
                <div className="bg-white rounded-xl border border-slate-200 overflow-hidden">
                    {/* Filters */}
                    <div className="p-4 border-b border-slate-200 bg-slate-50/50 flex flex-wrap items-end gap-3">
                        <div className="flex items-center gap-2 min-w-[200px] flex-1">
                            <Search className="w-4 h-4 text-slate-400" />
                            <input
                                type="text"
                                placeholder="Tìm theo user, mã tham chiếu..."
                                value={historySearch}
                                onChange={(e) => {
                                    setHistorySearch(e.target.value);
                                    setHistoryPage(1);
                                }}
                                className="flex-1 min-w-0 px-3 py-2 text-sm border border-slate-200 rounded-lg focus:ring-2 focus:ring-primary/20 focus:border-primary"
                            />
                        </div>
                        <div className="flex items-center gap-2">
                            <Filter className="w-4 h-4 text-slate-400" />
                            <select
                                value={historyTypeFilter}
                                onChange={(e) => {
                                    setHistoryTypeFilter(e.target.value);
                                    setHistoryPage(1);
                                }}
                                className="px-3 py-2 text-sm border border-slate-200 rounded-lg text-slate-700"
                            >
                                {TRANSACTION_TYPES.map((t) => (
                                    <option key={t.value || 'all'} value={t.value}>
                                        {t.label}
                                    </option>
                                ))}
                            </select>
                        </div>
                        <input
                            type="date"
                            value={historyDateFrom}
                            onChange={(e) => {
                                setHistoryDateFrom(e.target.value);
                                setHistoryPage(1);
                            }}
                            className="px-3 py-2 text-sm border border-slate-200 rounded-lg text-slate-700"
                        />
                        <span className="text-slate-400">→</span>
                        <input
                            type="date"
                            value={historyDateTo}
                            onChange={(e) => {
                                setHistoryDateTo(e.target.value);
                                setHistoryPage(1);
                            }}
                            className="px-3 py-2 text-sm border border-slate-200 rounded-lg text-slate-700"
                        />
                    </div>

                    {/* Table */}
                    <div className="overflow-x-auto">
                        <table className="w-full text-sm text-left text-slate-700">
                            <thead className="bg-slate-50 text-slate-600 uppercase tracking-wider">
                                <tr>
                                    <th className="px-4 py-3">Thời gian</th>
                                    <th className="px-4 py-3">Loại</th>
                                    <th className="px-4 py-3">User / Tác giả</th>
                                    <th className="px-4 py-3">Coin</th>
                                    <th className="px-4 py-3">VND</th>
                                    <th className="px-4 py-3">Trạng thái</th>
                                    <th className="px-4 py-3">Tham chiếu</th>
                                    <th className="px-4 py-3 text-right">Thao tác</th>
                                </tr>
                            </thead>
                            <tbody>
                                {paginatedTransactions.length === 0 ? (
                                    <tr>
                                        <td colSpan={8} className="px-4 py-12 text-center text-slate-500">
                                            Không có giao dịch nào thỏa bộ lọc.
                                        </td>
                                    </tr>
                                ) : (
                                    paginatedTransactions.map((tx) => (
                                        <tr key={tx.id} className="border-b border-slate-100 hover:bg-slate-50/50">
                                            <td className="px-4 py-3 whitespace-nowrap text-slate-600">
                                                {formatDate(tx.createdAt)}
                                            </td>
                                            <td className="px-4 py-3">
                                                <span className={`inline-flex px-2 py-0.5 rounded text-xs font-medium ${getTypeBadgeClass(tx.type)}`}>
                                                    {getTypeLabel(tx.type)}
                                                </span>
                                            </td>
                                            <td className="px-4 py-3">{tx.userDisplay || tx.userId || '—'}</td>
                                            <td className="px-4 py-3">
                                                {tx.amountCoins !== 0 ? (
                                                    <span className={tx.amountCoins > 0 ? 'text-emerald-600' : 'text-slate-700'}>
                                                        {tx.amountCoins > 0 ? '+' : ''}{formatCoins(tx.amountCoins)}
                                                    </span>
                                                ) : (
                                                    '—'
                                                )}
                                            </td>
                                            <td className="px-4 py-3">
                                                {tx.amountVnd ? formatVnd(tx.amountVnd) : '—'}
                                            </td>
                                            <td className="px-4 py-3">
                                                <span
                                                    className={`inline-flex px-2 py-0.5 rounded text-xs font-medium ${
                                                        tx.status === 'SUCCESS' ? 'bg-emerald-100 text-emerald-700' : 'bg-amber-100 text-amber-700'
                                                    }`}
                                                >
                                                    {tx.status === 'PENDING' ? 'Chờ xử lý' : 'Thành công'}
                                                </span>
                                            </td>
                                            <td className="px-4 py-3">
                                                {tx.refId}
                                                {tx.storyTitle && (
                                                    <span className="block text-xs text-slate-500 truncate max-w-[120px]" title={tx.storyTitle}>
                                                        {tx.storyTitle}
                                                    </span>
                                                )}
                                            </td>
                                            <td className="px-4 py-3 text-right">
                                                {isRefundable(tx) ? (
                                                    <button
                                                        type="button"
                                                        onClick={() => handleRefundCoinsForPurchase(tx)}
                                                        disabled={refundLoadingRef === tx.refId}
                                                        className={`inline-flex items-center rounded-lg px-3 py-1.5 text-xs font-semibold border ${
                                                            refundLoadingRef === tx.refId
                                                                ? 'border-slate-200 bg-slate-100 text-slate-400 cursor-not-allowed'
                                                                : 'border-slate-300 bg-white text-slate-700 hover:bg-slate-50'
                                                        }`}
                                                    >
                                                        {refundLoadingRef === tx.refId ? 'Đang hoàn…' : 'Hoàn coin'}
                                                    </button>
                                                ) : tx.type === 'PURCHASE_CHAPTER' && transactions.some((t) => t.type === 'REFUND' && t.refId === tx.refId) ? (
                                                    <span className="text-xs font-semibold text-slate-400">Đã hoàn</span>
                                                ) : (
                                                    <span className="text-xs text-slate-300">—</span>
                                                )}
                                            </td>
                                        </tr>
                                    ))
                                )}
                            </tbody>
                        </table>
                    </div>

                    {/* Pagination */}
                    {filteredTransactions.length > 0 && (
                        <div className="px-4 py-3 border-t border-slate-200 flex items-center justify-between">
                            <p className="text-xs text-slate-500">
                                Hiển thị {(historyPage - 1) * historyPageSize + 1}–{Math.min(historyPage * historyPageSize, filteredTransactions.length)} / {filteredTransactions.length}
                            </p>
                            <div className="flex items-center gap-1">
                                <button
                                    onClick={() => setHistoryPage((p) => Math.max(1, p - 1))}
                                    disabled={historyPage <= 1}
                                    className="p-2 rounded border border-slate-200 disabled:opacity-50 disabled:cursor-not-allowed hover:bg-slate-50"
                                >
                                    <ChevronLeft className="w-4 h-4" />
                                </button>
                                <span className="px-2 text-sm text-slate-600">
                                    Trang {historyPage} / {totalHistoryPages}
                                </span>
                                <button
                                    onClick={() => setHistoryPage((p) => Math.min(totalHistoryPages, p + 1))}
                                    disabled={historyPage >= totalHistoryPages}
                                    className="p-2 rounded border border-slate-200 disabled:opacity-50 disabled:cursor-not-allowed hover:bg-slate-50"
                                >
                                    <ChevronRight className="w-4 h-4" />
                                </button>
                            </div>
                        </div>
                    )}
                </div>
            )}
        </div>
    );
}
