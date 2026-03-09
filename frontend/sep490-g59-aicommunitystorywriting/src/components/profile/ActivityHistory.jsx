import { useCallback, useEffect, useState } from 'react';
import { Coins, Calendar, ArrowDown, ArrowUp, RefreshCcw } from 'lucide-react';
import * as coinApi from '../../api/coins/coinApi';

export default function ActivityHistory({ mode = 'default' } = {}) {
    const isWalletMode = mode === 'wallet';
    const [filter, setFilter] = useState(isWalletMode ? 'recharge' : 'all');
    const [rechargeActivities, setRechargeActivities] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');

    const formatApiDateTimeParts = (value) => {
        if (!value) return { date: '', time: '' };
        const s = String(value);
        const hasTimezone = /([zZ]|[+-]\d{2}:\d{2})$/.test(s);
        const iso = hasTimezone ? s : `${s}Z`;
        const d = new Date(iso);
        if (Number.isNaN(d.getTime())) return { date: s, time: '' };
        return {
            date: d.toLocaleDateString(),
            time: d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' }),
        };
    };

    const loadRechargeActivities = useCallback(async () => {
        setError('');
        setLoading(true);
        try {
            const res = await coinApi.getMyCoinOrders({ take: 50 });
            if (!res.success) throw new Error(res.message);
            const items = Array.isArray(res.data) ? res.data : [];
            const mapped = items.map((o) => {
                const { date, time } = formatApiDateTimeParts(o.createdAt);
                return {
                    id: o.id,
                    type: 'recharge',
                    title: `Nạp coin (${o.paymentGateway || 'PAYOS'})`,
                    amount: o.coinsGranted ?? 0,
                    date,
                    time,
                    status: o.status || 'PENDING',
                };
            });
            setRechargeActivities(mapped);
        } catch (e) {
            setError(e?.message || 'Không thể tải lịch sử nạp coin');
            setRechargeActivities([]);
        } finally {
            setLoading(false);
        }
    }, []);

    useEffect(() => {
        loadRechargeActivities();
    }, [loadRechargeActivities]);

    const filteredActivities = (() => {
        if (isWalletMode) return rechargeActivities;
        if (filter === 'recharge' || filter === 'all') return rechargeActivities;
        return [];
    })();

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
            <div className="flex items-center justify-between mb-6">
                <h3 className="text-xl font-bold text-slate-900 dark:text-white">
                    {isWalletMode ? 'Lịch sử giao dịch' : 'Lịch sử hoạt động'}
                </h3>
                <div className="flex items-center gap-2">
                    {!isWalletMode && (
                        <>
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
                        </>
                    )}
                    <button
                        onClick={loadRechargeActivities}
                        disabled={loading}
                        className="ml-2 inline-flex items-center gap-2 px-4 py-2 text-sm font-semibold rounded-lg border border-slate-200 dark:border-slate-600 hover:bg-slate-50 dark:hover:bg-slate-700 transition-colors disabled:opacity-50"
                    >
                        <RefreshCcw className="w-4 h-4" />
                        Làm mới
                    </button>
                </div>
            </div>

            <div className="space-y-4">
                {error && (
                    <div className="p-4 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 rounded-lg text-sm text-red-700 dark:text-red-300">
                        {error}
                    </div>
                )}

                {!isWalletMode && filter !== 'all' && filter !== 'recharge' ? (
                    <div className="text-center py-12 text-slate-500 dark:text-slate-400">
                        Chức năng này đang được phát triển.
                    </div>
                ) : loading ? (
                    <div className="text-center py-12 text-slate-500 dark:text-slate-400">
                        Đang tải dữ liệu...
                    </div>
                ) : filteredActivities.length === 0 ? (
                    <div className="text-center py-12 text-slate-500 dark:text-slate-400">
                        {isWalletMode ? 'Chưa có giao dịch nạp coin' : 'Không có hoạt động nào'}
                    </div>
                ) : (
                    filteredActivities.map((activity) => (
                        <div
                            key={activity.id}
                            className="flex items-center gap-4 p-4 border border-slate-200 dark:border-slate-700 rounded-lg hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-colors"
                        >
                            <div className="flex-shrink-0">
                                {getIcon(activity.type)}
                            </div>
                            <div className="flex-1">
                                <div className="font-semibold text-slate-900 dark:text-white">
                                    {activity.title}
                                    <span
                                        className={`ml-2 inline-block px-2 py-0.5 rounded text-xs font-bold ${
                                            activity.status === 'PAID'
                                                ? 'bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400'
                                                : activity.status === 'PENDING'
                                                  ? 'bg-amber-100 dark:bg-amber-900/30 text-amber-700 dark:text-amber-400'
                                                  : 'bg-slate-100 dark:bg-slate-700 text-slate-700 dark:text-slate-200'
                                        }`}
                                    >
                                        {activity.status}
                                    </span>
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
        </div>
    );
}
