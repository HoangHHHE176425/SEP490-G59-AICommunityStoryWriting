import {
    FileText,
    MessageSquare,
    ShieldAlert,
    Inbox,
    MoreVertical,
    ArrowRight,
} from 'lucide-react';
import { useEffect, useState } from 'react';
import {
    getComplianceCommentReports,
    getComplianceStoryReports,
} from '../../api/admin/adminComplianceApi';

function readPaged(data) {
    if (Array.isArray(data)) {
        return { items: data, totalCount: data.length, page: 1 };
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

const colorClasses = {
    blue: 'bg-blue-100 dark:bg-blue-950/30 text-blue-600 dark:text-blue-400',
    green: 'bg-green-100 dark:bg-green-950/30 text-green-600 dark:text-green-400',
    purple: 'bg-purple-100 dark:bg-purple-950/30 text-purple-600 dark:text-purple-400',
    amber: 'bg-amber-100 dark:bg-amber-950/30 text-amber-600 dark:text-amber-400',
};

/**
 * Tổng quan cho role COMPLIANCE — số liệu hàng đợi + phân bổ (lịch sử chi tiết xem tab Lịch sử trong Quản lý vi phạm).
 */
export function ComplianceDashboard({ onNavigateToViolations } = {}) {
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [stats, setStats] = useState([
        { title: 'Truyện chờ nhận', value: '0', icon: FileText, color: 'blue' },
        { title: 'Bình luận chờ nhận', value: '0', icon: MessageSquare, color: 'green' },
        { title: 'Truyện đang xử lý (của tôi)', value: '0', icon: ShieldAlert, color: 'purple' },
        { title: 'Bình luận đang xử lý (của tôi)', value: '0', icon: Inbox, color: 'amber' },
    ]);
    const [unclaimedTotal, setUnclaimedTotal] = useState(0);
    const [mineTotal, setMineTotal] = useState(0);

    useEffect(() => {
        let cancelled = false;
        (async () => {
            setLoading(true);
            setError('');
            try {
                const [storyUnRes, commentUnRes, storyMineRes, commentMineRes] = await Promise.all([
                    getComplianceStoryReports({
                        page: 1,
                        pageSize: 1,
                        groupByStory: true,
                        claimFilter: 'unclaimed',
                        statuses: 'NEW,IN_REVIEW',
                    }).catch(() => null),
                    getComplianceCommentReports({
                        page: 1,
                        pageSize: 1,
                        claimFilter: 'unclaimed',
                        status: 'NEW,IN_REVIEW',
                    }).catch(() => null),
                    getComplianceStoryReports({
                        page: 1,
                        pageSize: 1,
                        groupByStory: true,
                        claimFilter: 'mine',
                        statuses: 'NEW,IN_REVIEW',
                    }).catch(() => null),
                    getComplianceCommentReports({
                        page: 1,
                        pageSize: 1,
                        claimFilter: 'mine',
                        status: 'NEW,IN_REVIEW',
                    }).catch(() => null),
                ]);

                const su = readPaged(storyUnRes).totalCount;
                const cu = readPaged(commentUnRes).totalCount;
                const sm = readPaged(storyMineRes).totalCount;
                const cm = readPaged(commentMineRes).totalCount;

                if (!cancelled) {
                    setStats([
                        {
                            title: 'Truyện chờ nhận',
                            value: su.toLocaleString('vi-VN'),
                            icon: FileText,
                            color: 'blue',
                        },
                        {
                            title: 'Bình luận chờ nhận',
                            value: cu.toLocaleString('vi-VN'),
                            icon: MessageSquare,
                            color: 'green',
                        },
                        {
                            title: 'Truyện đang xử lý (của tôi)',
                            value: sm.toLocaleString('vi-VN'),
                            icon: ShieldAlert,
                            color: 'purple',
                        },
                        {
                            title: 'Bình luận đang xử lý (của tôi)',
                            value: cm.toLocaleString('vi-VN'),
                            icon: Inbox,
                            color: 'amber',
                        },
                    ]);
                    setUnclaimedTotal(su + cu);
                    setMineTotal(sm + cm);
                }
            } catch (e) {
                if (!cancelled) setError(e?.message ?? 'Không tải được tổng quan.');
            } finally {
                if (!cancelled) setLoading(false);
            }
        })();
        return () => {
            cancelled = true;
        };
    }, []);

    const denom = unclaimedTotal + mineTotal;
    const minePct = denom > 0 ? Math.round((mineTotal / denom) * 100) : 0;
    const unclaimedPct = denom > 0 ? Math.round((unclaimedTotal / denom) * 100) : 0;
    const reviewedDeg = denom > 0 ? Math.round((mineTotal / denom) * 360) : 0;

    return (
        <div className="space-y-6">
            <div className="flex flex-col gap-3 sm:flex-row sm:items-start sm:justify-between">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900 dark:text-white">
                        Tổng quan xử lý vi phạm
                    </h1>
                    <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
                        Theo dõi nhanh hàng đợi báo cáo truyện và bình luận. Lịch sử xử lý chi tiết xem trong{' '}
                        <span className="font-medium text-slate-600 dark:text-slate-300">Quản lý vi phạm → Lịch sử xử lý vi phạm</span>.
                    </p>
                </div>
                {typeof onNavigateToViolations === 'function' ? (
                    <button
                        type="button"
                        onClick={() => onNavigateToViolations()}
                        className="inline-flex items-center justify-center gap-2 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-2.5 text-sm font-semibold text-emerald-800 hover:bg-emerald-100 transition-colors shrink-0"
                    >
                        Mở quản lý vi phạm
                        <ArrowRight className="w-4 h-4" />
                    </button>
                ) : null}
            </div>

            {error ? (
                <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm font-semibold text-red-700">
                    {error}
                </div>
            ) : null}

            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                {stats.map((stat) => {
                    const Icon = stat.icon;
                    return (
                        <div
                            key={stat.title}
                            className="bg-white dark:bg-slate-900 rounded-xl p-5 border border-slate-200 dark:border-slate-800 hover:shadow-lg transition-shadow"
                        >
                            <div className="flex items-start justify-between mb-4">
                                <div className={`w-12 h-12 rounded-lg flex items-center justify-center ${colorClasses[stat.color]}`}>
                                    <Icon className="w-6 h-6" />
                                </div>
                                <button type="button" className="p-1 hover:bg-slate-100 dark:hover:bg-slate-800 rounded transition-colors" aria-hidden>
                                    <MoreVertical className="w-4 h-4 text-slate-400" />
                                </button>
                            </div>
                            <p className="text-sm text-slate-500 dark:text-slate-400 mb-1">{stat.title}</p>
                            <p className="text-2xl font-bold text-slate-900 dark:text-white mb-2">
                                {loading ? '…' : stat.value}
                            </p>
                            <p className="text-xs font-semibold text-slate-500 dark:text-slate-400">Cập nhật gần nhất</p>
                        </div>
                    );
                })}
            </div>

            <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 p-6 md:p-8">
                <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-4 mb-6">
                    <div>
                        <h2 className="text-lg font-bold text-slate-900 dark:text-white">
                            Phân bổ hàng đợi
                        </h2>
                        <p className="text-sm text-slate-500 dark:text-slate-400 mt-1 max-w-xl">
                            So sánh đơn chưa ai nhận (toàn hệ thống) và đơn bạn đang giữ (trạng thái mới / đang xử lý).
                        </p>
                    </div>
                    {typeof onNavigateToViolations === 'function' ? (
                        <button
                            type="button"
                            onClick={() => onNavigateToViolations()}
                            className="text-sm font-semibold text-emerald-700 hover:underline shrink-0 self-start md:self-center"
                        >
                            Vào hàng đợi xử lý →
                        </button>
                    ) : null}
                </div>

                {denom <= 0 && !loading ? (
                    <p className="text-sm text-slate-600 dark:text-slate-300 py-10 text-center">
                        Hiện không có báo cáo mở trong hàng đợi công khai hoặc trên tài khoản của bạn.
                    </p>
                ) : (
                    <div className="flex flex-col sm:flex-row items-center justify-center gap-10 sm:gap-16">
                        <div className="relative w-48 h-48 shrink-0">
                            <div
                                className="absolute inset-0 rounded-full"
                                style={{
                                    background: `conic-gradient(#13ec5b 0deg ${reviewedDeg}deg, #e2e8f0 ${reviewedDeg}deg 360deg)`,
                                }}
                            />
                            <div className="absolute inset-6 rounded-full bg-white dark:bg-slate-900 flex flex-col items-center justify-center">
                                <p className="text-3xl font-bold text-slate-900 dark:text-white">
                                    {loading ? '…' : `${denom}`}
                                </p>
                                <p className="text-xs text-slate-500 dark:text-slate-400 text-center px-2 mt-0.5">Tổng đơn mở</p>
                            </div>
                        </div>
                        <div className="w-full max-w-md grid grid-cols-1 sm:grid-cols-2 gap-4">
                            <div className="px-4 py-3 rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-800/40">
                                <span className="text-xs font-medium text-slate-500 dark:text-slate-400">Chờ nhận (toàn hệ thống)</span>
                                <span className="block text-xl font-bold text-slate-900 dark:text-white mt-1">
                                    {loading ? '…' : `${unclaimedTotal}`}
                                </span>
                                <span className="text-xs text-slate-500">{loading ? '' : `${unclaimedPct}% tổng đơn mở`}</span>
                            </div>
                            <div className="px-4 py-3 rounded-xl border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-800/40">
                                <span className="text-xs font-medium text-slate-500 dark:text-slate-400">Đang xử lý (tôi đang giữ)</span>
                                <span className="block text-xl font-bold text-slate-900 dark:text-white mt-1">
                                    {loading ? '…' : `${mineTotal}`}
                                </span>
                                <span className="text-xs text-slate-500">{loading ? '' : `${minePct}% tổng đơn mở`}</span>
                            </div>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
}
