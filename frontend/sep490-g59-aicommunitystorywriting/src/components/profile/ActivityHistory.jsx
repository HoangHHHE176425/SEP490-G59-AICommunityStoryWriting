import { useMemo, useState } from 'react';
import { Coins, Calendar, ArrowDown, ArrowUp, Search, X, Info, Copy } from 'lucide-react';

export default function ActivityHistory() {
    const [filter, setFilter] = useState('all');
    const [dateFilter, setDateFilter] = useState('all');
    const [search, setSearch] = useState('');
    const [selectedActivity, setSelectedActivity] = useState(null);

    // Mock data - replace with actual API data
    const activities = [
        {
            id: 1,
            type: 'recharge',
            title: 'Nạp coin',
            amount: 1000,
            date: '2026-01-20',
            time: '14:30',
            status: 'success',
        },
        {
            id: 2,
            type: 'unlock',
            title: 'Mở khóa truyện "Kiếm Thánh"',
            amount: -50,
            date: '2026-01-19',
            time: '10:15',
            status: 'success',
        },
        {
            id: 3,
            type: 'payment',
            title: 'Thanh toán đăng truyện',
            amount: -100,
            date: '2026-01-18',
            time: '16:45',
            status: 'success',
        },
        {
            id: 4,
            type: 'recharge',
            title: 'Nạp coin',
            amount: 500,
            date: '2026-01-15',
            time: '09:20',
            status: 'success',
        },
    ];

    const filteredActivities = useMemo(() => {
        let result = [...activities];

        // Loại giao dịch
        if (filter !== 'all') {
            result = result.filter((a) => a.type === filter);
        }

        // Khoảng thời gian
        if (dateFilter !== 'all') {
            const now = new Date();
            const checkDays = (days) => {
                return result.filter((a) => {
                    const d = new Date(a.date);
                    const diffMs = now - d;
                    const diffDays = diffMs / (1000 * 60 * 60 * 24);
                    return diffDays <= days;
                });
            };

            if (dateFilter === '7d') result = checkDays(7);
            if (dateFilter === '30d') result = checkDays(30);
            if (dateFilter === '90d') result = checkDays(90);
        }

        // Tìm kiếm theo tiêu đề / mã
        if (search.trim()) {
            const q = search.trim().toLowerCase();
            result = result.filter(
                (a) =>
                    a.title.toLowerCase().includes(q) ||
                    String(a.id).toLowerCase().includes(q)
            );
        }

        return result;
    }, [activities, filter, dateFilter, search]);

    const getIcon = (type) => {
        switch (type) {
            case 'recharge':
                return <ArrowDown className="w-5 h-5 text-green-500" />;
            case 'unlock':
            case 'payment':
                return <ArrowUp className="w-5 h-5 text-red-500" />;
            default:
                return <Coins className="w-5 h-5 text-slate-400" />;
        }
    };

    return (
        <div className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-8 border border-slate-200 dark:border-slate-700">
            <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between mb-6">
                <div>
                    <h3 className="text-xl font-bold text-slate-900 dark:text-white">
                        Lịch sử hoạt động
                    </h3>
                    <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
                        Xem lại các giao dịch nạp tiền và sử dụng coin gần đây
                    </p>
                </div>
                <div className="flex flex-wrap gap-2 justify-end">
                    <button
                        onClick={() => setFilter('all')}
                        className={`px-4 py-2 text-sm font-semibold rounded-lg transition-colors ${
                            filter === 'all'
                                ? 'bg-primary text-white'
                                : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-400'
                        }`}
                    >
                        Tất cả
                    </button>
                    <button
                        onClick={() => setFilter('recharge')}
                        className={`px-4 py-2 text-sm font-semibold rounded-lg transition-colors ${
                            filter === 'recharge'
                                ? 'bg-primary text-white'
                                : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-400'
                        }`}
                    >
                        Nạp coin
                    </button>
                    <button
                        onClick={() => setFilter('unlock')}
                        className={`px-4 py-2 text-sm font-semibold rounded-lg transition-colors ${
                            filter === 'unlock'
                                ? 'bg-primary text-white'
                                : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-400'
                        }`}
                    >
                        Mở khóa
                    </button>
                    <button
                        onClick={() => setFilter('payment')}
                        className={`px-4 py-2 text-sm font-semibold rounded-lg transition-colors ${
                            filter === 'payment'
                                ? 'bg-primary text-white'
                                : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-400'
                        }`}
                    >
                        Thanh toán
                    </button>
                </div>
            </div>

            {/* Bộ lọc nâng cao */}
            <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between mb-4">
                <div className="flex gap-2">
                    <select
                        value={dateFilter}
                        onChange={(e) => setDateFilter(e.target.value)}
                        className="text-sm rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 px-3 py-2 text-slate-700 dark:text-slate-200 focus:ring-2 focus:ring-primary/40 focus:border-primary outline-none"
                    >
                        <option value="all">Tất cả thời gian</option>
                        <option value="7d">7 ngày gần đây</option>
                        <option value="30d">30 ngày gần đây</option>
                        <option value="90d">3 tháng gần đây</option>
                    </select>
                </div>
                <div className="relative w-full md:max-w-xs">
                    <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400">
                        <Search className="w-4 h-4" />
                    </div>
                    <input
                        type="text"
                        value={search}
                        onChange={(e) => setSearch(e.target.value)}
                        placeholder="Tìm theo tên giao dịch, mã..."
                        className="w-full pl-9 pr-9 py-2 text-sm rounded-lg bg-slate-50 dark:bg-slate-900 border border-slate-200 dark:border-slate-700 text-slate-700 dark:text-slate-200 focus:ring-2 focus:ring-primary/40 focus:border-primary outline-none"
                    />
                    {search && (
                        <button
                            type="button"
                            onClick={() => setSearch('')}
                            className="absolute inset-y-0 right-0 pr-3 flex items-center text-slate-400 hover:text-slate-200"
                        >
                            <X className="w-4 h-4" />
                        </button>
                    )}
                </div>
            </div>

            <div className="space-y-4">
                {filteredActivities.length === 0 ? (
                    <div className="text-center py-12 text-slate-500 dark:text-slate-400">
                        Không tìm thấy giao dịch phù hợp với bộ lọc hiện tại
                    </div>
                ) : (
                    filteredActivities.map((activity) => (
                        <div
                            key={activity.id}
                            className="flex items-center gap-4 p-4 border border-slate-200 dark:border-slate-700 rounded-lg hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-colors cursor-pointer"
                            onClick={() => setSelectedActivity(activity)}
                        >
                            <div className="flex-shrink-0">
                                {getIcon(activity.type)}
                            </div>
                            <div className="flex-1">
                                <div className="font-semibold text-slate-900 dark:text-white">
                                    {activity.title}
                                </div>
                                <div className="flex items-center gap-2 text-sm text-slate-500 dark:text-slate-400">
                                    <Calendar className="w-4 h-4" />
                                    {activity.date} lúc {activity.time}
                                </div>
                            </div>
                            <div
                                className={`font-bold ${
                                    activity.amount > 0
                                        ? 'text-green-600 dark:text-green-400'
                                        : 'text-red-600 dark:text-red-400'
                                }`}
                            >
                                {activity.amount > 0 ? '+' : ''}
                                {activity.amount.toLocaleString()} Coins
                            </div>
                        </div>
                    ))
                )}
            </div>

            {/* Modal chi tiết giao dịch */}
            {selectedActivity && (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-slate-900/60">
                    <div className="bg-white dark:bg-slate-900 rounded-xl shadow-2xl border border-slate-200 dark:border-slate-700 max-w-md w-full mx-4">
                        <div className="flex items-start justify-between px-6 pt-5 pb-3 border-b border-slate-200 dark:border-slate-800">
                            <div className="flex items-center gap-2">
                                <Info className="w-5 h-5 text-primary" />
                                <h4 className="text-base font-semibold text-slate-900 dark:text-white">
                                    Chi tiết giao dịch
                                </h4>
                            </div>
                            <button
                                type="button"
                                onClick={() => setSelectedActivity(null)}
                                className="text-slate-400 hover:text-slate-200"
                            >
                                <X className="w-5 h-5" />
                            </button>
                        </div>
                        <div className="px-6 py-4 space-y-3 text-sm">
                            <div className="flex items-center justify-between">
                                <span className="text-slate-500 dark:text-slate-400">Mã giao dịch</span>
                                <div className="flex items-center gap-2">
                                    <span className="font-mono text-slate-900 dark:text-slate-100">
                                        #{selectedActivity.id}
                                    </span>
                                    <button
                                        type="button"
                                        onClick={() =>
                                            navigator.clipboard?.writeText(String(selectedActivity.id)).catch(() => {})
                                        }
                                        className="p-1 rounded hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-400"
                                        title="Sao chép mã"
                                    >
                                        <Copy className="w-4 h-4" />
                                    </button>
                                </div>
                            </div>
                            <div className="flex items-center justify-between">
                                <span className="text-slate-500 dark:text-slate-400">Loại</span>
                                <span className="font-medium text-slate-900 dark:text-slate-100">
                                    {selectedActivity.type === 'recharge'
                                        ? 'Nạp coin'
                                        : selectedActivity.type === 'unlock'
                                        ? 'Mở khóa'
                                        : 'Thanh toán'}
                                </span>
                            </div>
                            <div className="flex items-center justify-between">
                                <span className="text-slate-500 dark:text-slate-400">Thời gian</span>
                                <span className="font-medium text-slate-900 dark:text-slate-100">
                                    {selectedActivity.date} lúc {selectedActivity.time}
                                </span>
                            </div>
                            <div className="flex items-center justify-between">
                                <span className="text-slate-500 dark:text-slate-400">Số coin</span>
                                <span
                                    className={`font-semibold ${
                                        selectedActivity.amount > 0
                                            ? 'text-green-600 dark:text-green-400'
                                            : 'text-red-600 dark:text-red-400'
                                    }`}
                                >
                                    {selectedActivity.amount > 0 ? '+' : ''}
                                    {selectedActivity.amount.toLocaleString()} Coins
                                </span>
                            </div>
                            <div>
                                <span className="block text-slate-500 dark:text-slate-400 mb-0.5">Mô tả</span>
                                <p className="text-slate-900 dark:text-slate-100">{selectedActivity.title}</p>
                            </div>
                            <div>
                                <span className="block text-slate-500 dark:text-slate-400 mb-0.5">Trạng thái</span>
                                <span className="inline-flex items-center px-2 py-1 rounded-full text-xs font-semibold bg-emerald-50 dark:bg-emerald-900/20 text-emerald-700 dark:text-emerald-300 border border-emerald-100 dark:border-emerald-900/60">
                                    Thành công
                                </span>
                            </div>
                        </div>
                        <div className="px-6 py-4 border-t border-slate-200 dark:border-slate-800 flex justify-end">
                            <button
                                type="button"
                                onClick={() => setSelectedActivity(null)}
                                className="px-4 py-2 text-sm font-semibold rounded-lg bg-slate-900 text-white hover:bg-slate-800 dark:bg-slate-100 dark:text-slate-900 dark:hover:bg-white"
                            >
                                Đóng
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}
