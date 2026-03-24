import { useCallback, useEffect, useState } from 'react';
import { Coins, Calendar, ArrowDown, ArrowUp, RefreshCcw, LockKeyhole } from 'lucide-react';
import * as coinApi from '../../api/coins/coinApi';

export default function ActivityHistory({ mode = 'default' } = {}) {
    const isWalletMode = mode === 'wallet';
    const [filter, setFilter] = useState(isWalletMode ? 'all' : 'all');
    const [timeFilter, setTimeFilter] = useState('all'); // all | 24h | 7d | 30d
    const [rechargeActivities, setRechargeActivities] = useState([]);
    const [unlockActivities, setUnlockActivities] = useState([]);
    const [donateActivities, setDonateActivities] = useState([]);
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
                    createdAtTs: o.createdAt ? new Date(o.createdAt).getTime() : 0,
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

    // Hiện wallet-mode sẽ hiển thị 3 nhóm: nạp tiền / mở khóa chương / donate.
    // Các loại khác (mode != wallet) có thể để trống tuỳ yêu cầu.
    const loadOtherActivities = useCallback(async () => {
        if (!isWalletMode) {
            // Non-wallet mode: không hook unlock/donate.
            setUnlockActivities([]);
            setDonateActivities([]);
            return;
        }

        setUnlockActivities([]);
        setDonateActivities([]);

        const [unlockRes, donateRes] = await Promise.allSettled([
            coinApi.getMyChapterUnlockHistory({ page: 1, pageSize: 50 }),
            coinApi.getMyDonateHistory({ page: 1, pageSize: 50 }),
        ]);

        const unlockOk = unlockRes.status === 'fulfilled' && unlockRes.value?.success;
        const donateOk = donateRes.status === 'fulfilled' && donateRes.value?.success;

        if (unlockRes.status === 'rejected') {
            // vẫn cho phép UI chạy với rechargeActivities
            setUnlockActivities([]);
        }
        if (donateRes.status === 'rejected') {
            setDonateActivities([]);
        }

        const unlockItems = unlockOk && Array.isArray(unlockRes.value?.data?.items) ? unlockRes.value.data.items : [];
        const donateItems = donateOk && Array.isArray(donateRes.value?.data?.items) ? donateRes.value.data.items : [];

        const mappedUnlock = unlockItems.map((o) => {
            const unlockedAt = o?.unlockedAt ?? o?.UnlockedAt ?? o?.unlocked_at ?? null;
            const coinsPaid = o?.coinsPaid ?? o?.CoinsPaid ?? o?.coins_paid ?? 0;
            const chapterTitle = o?.chapterTitle ?? o?.ChapterTitle ?? o?.chapter_title ?? null;
            const storyTitle = o?.storyTitle ?? o?.StoryTitle ?? o?.story_title ?? null;
            const purchaseId = o?.purchaseId ?? o?.PurchaseId ?? o?.purchase_id ?? null;
            const chapterId = o?.chapterId ?? o?.ChapterId ?? o?.chapter_id ?? null;

            const { date, time } = formatApiDateTimeParts(unlockedAt);
            return {
                id: purchaseId ?? `${chapterId ?? ''}-${unlockedAt ?? ''}`,
                type: 'unlock',
                title: `Mở khóa chương (${chapterTitle || storyTitle || '—'})`,
                amount: -(Number(coinsPaid ?? 0) || 0),
                date,
                time,
                status: 'PAID',
                createdAtTs: unlockedAt ? new Date(unlockedAt).getTime() : 0,
            };
        });

        const mappedDonate = donateItems.map((o) => {
            const donatedAt = o?.donatedAt ?? o?.DonatedAt ?? o?.donated_at ?? null;
            const coinsPaid = o?.coinsPaid ?? o?.CoinsPaid ?? o?.coins_paid ?? 0;
            const storyTitle = o?.storyTitle ?? o?.StoryTitle ?? o?.story_title ?? null;
            const donationId = o?.donationId ?? o?.DonationId ?? o?.donation_id ?? null;
            const storyId = o?.storyId ?? o?.StoryId ?? o?.story_id ?? null;

            const { date, time } = formatApiDateTimeParts(donatedAt);
            return {
                id: donationId ?? `${storyId ?? ''}-${donatedAt ?? ''}`,
                type: 'donate',
                title: `Donate cho tác giả (${storyTitle || '—'})`,
                amount: -(Number(coinsPaid ?? 0) || 0),
                date,
                time,
                status: 'PAID',
                createdAtTs: donatedAt ? new Date(donatedAt).getTime() : 0,
            };
        });

        setUnlockActivities(mappedUnlock);
        setDonateActivities(mappedDonate);
    }, [isWalletMode]);

    useEffect(() => {
        let cancelled = false;
        const run = async () => {
            setError('');
            setLoading(true);
            try {
                await loadRechargeActivities();
                await loadOtherActivities();
            } catch (e) {
                if (cancelled) return;
                setError(e?.message || 'Không thể tải lịch sử giao dịch');
            } finally {
                if (!cancelled) setLoading(false);
            }
        };
        run();
        return () => { cancelled = true; };
    }, [loadRechargeActivities, loadOtherActivities]);

    const filteredActivities = (() => {
        const all = [...rechargeActivities, ...unlockActivities, ...donateActivities];
        if (filter === 'all') return all.sort((a, b) => (b.createdAtTs ?? 0) - (a.createdAtTs ?? 0));
        if (filter === 'recharge') return rechargeActivities;
        if (filter === 'unlock') return unlockActivities;
        if (filter === 'donate') return donateActivities;
        return all;
    })();

    const getIcon = (type) => {
        switch (type) {
            case 'recharge':
                return <ArrowDown className="w-5 h-5 text-green-500" />;
            case 'unlock':
                return <LockKeyhole className="w-5 h-5 text-red-500" />;
            case 'donate':
                return <ArrowUp className="w-5 h-5 text-emerald-700" />;
            default:
                return <Coins className="w-5 h-5 text-slate-400" />;
        }
    };

    const formatStatusLabel = (status) => {
        const s = String(status ?? '').toUpperCase();
        switch (s) {
            case 'PAID':
                return 'Đã hoàn tất';
            case 'PENDING':
                return 'Đang chờ';
            default:
                return status ?? '';
        }
    };

    const emptyMessage = (() => {
        if (!isWalletMode) {
            if (filter === 'recharge' || filter === 'all') return 'Không có hoạt động nào';
            return 'Chức năng này đang được phát triển.';
        }
        if (filter === 'recharge') return 'Chưa có giao dịch nạp coin';
        if (filter === 'unlock') return 'Chưa có giao dịch mở khóa chương';
        if (filter === 'donate') return 'Chưa có donate cho tác giả';
        return 'Chưa có giao dịch nào';
    })();

    const isSpendingType = (type) => type === 'unlock' || type === 'donate';

    const matchesTimeFilter = (createdAtTs) => {
        if (timeFilter === 'all') return true;
        if (!createdAtTs) return false;

        const now = Date.now();
        switch (timeFilter) {
            case '24h':
                return createdAtTs >= now - 24 * 60 * 60 * 1000;
            case '7d':
                return createdAtTs >= now - 7 * 24 * 60 * 60 * 1000;
            case '30d':
                return createdAtTs >= now - 30 * 24 * 60 * 60 * 1000;
            default:
                return true;
        }
    };

    const timeFilteredActivities = filteredActivities.filter((a) => matchesTimeFilter(a.createdAtTs));

    return (
        <div className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-8 border border-slate-200 dark:border-slate-700">
            <div className="flex items-center justify-between mb-6">
                <h3 className="text-xl font-bold text-slate-900 dark:text-white">
                    {isWalletMode ? 'Lịch sử giao dịch' : 'Lịch sử hoạt động'}
                </h3>
                <div className="flex items-center gap-2">
                    {(
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
                                Nạp tiền
                            </button>
                            <button
                                onClick={() => setFilter('unlock')}
                                className={`px-4 py-2 text-sm font-semibold rounded-lg transition-colors ${
                                    filter === 'unlock'
                                        ? 'bg-primary text-white'
                                        : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-400'
                                }`}
                            >
                                Mở khóa chương
                            </button>
                            <button
                                onClick={() => setFilter('donate')}
                                className={`px-4 py-2 text-sm font-semibold rounded-lg transition-colors ${
                                    filter === 'donate'
                                        ? 'bg-primary text-white'
                                        : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-400'
                                }`}
                            >
                                Donate cho tác giả
                            </button>
                        </>
                    )}

                    <div className="flex items-center gap-2 ml-1">
                        <button
                            onClick={() => setTimeFilter('all')}
                            className={`px-3 py-2 text-xs font-semibold rounded-lg transition-colors ${
                                timeFilter === 'all'
                                    ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900'
                                    : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300'
                            }`}
                        >
                            Tất cả
                        </button>
                        <button
                            onClick={() => setTimeFilter('7d')}
                            className={`px-3 py-2 text-xs font-semibold rounded-lg transition-colors ${
                                timeFilter === '7d'
                                    ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900'
                                    : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300'
                            }`}
                        >
                            7 ngày
                        </button>
                        <button
                            onClick={() => setTimeFilter('30d')}
                            className={`px-3 py-2 text-xs font-semibold rounded-lg transition-colors ${
                                timeFilter === '30d'
                                    ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900'
                                    : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300'
                            }`}
                        >
                            30 ngày
                        </button>
                        <button
                            onClick={() => setTimeFilter('24h')}
                            className={`px-3 py-2 text-xs font-semibold rounded-lg transition-colors ${
                                timeFilter === '24h'
                                    ? 'bg-slate-900 text-white dark:bg-white dark:text-slate-900'
                                    : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-300'
                            }`}
                        >
                            24h
                        </button>
                    </div>

                    <button
                        onClick={async () => {
                            await loadRechargeActivities();
                            await loadOtherActivities();
                        }}
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

                {loading ? (
                    <div className="text-center py-12 text-slate-500 dark:text-slate-400">
                        Đang tải dữ liệu...
                    </div>
                ) : timeFilteredActivities.length === 0 ? (
                    <div className="text-center py-12 text-slate-500 dark:text-slate-400">
                        {emptyMessage}
                    </div>
                ) : (
                    timeFilteredActivities.map((activity) => (
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
                                        {formatStatusLabel(activity.status)}
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
                                {activity.amount > 0 && !isSpendingType(activity.type) ? '+' : ''}
                                {activity.amount.toLocaleString()} Coins
                            </div>
                        </div>
                    ))
                )}
            </div>
        </div>
    );
}
