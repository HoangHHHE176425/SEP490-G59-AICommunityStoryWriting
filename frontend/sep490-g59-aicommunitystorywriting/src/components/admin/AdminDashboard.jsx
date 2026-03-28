import {
    TrendingUp,
    Users,
    FileText,
    Eye,
    MessageSquare,
    DollarSign,
    ArrowUp,
    ArrowDown,
    MoreVertical
} from 'lucide-react';

import { useEffect, useMemo, useState } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { getStats } from '../../api/admin/userManagementApi';
import { getAdminWalletSummary } from '../../api/admin/walletApi';
import { getStories } from '../../api/story/storyApi';
import { mapStoryListItemToBrowseStory, formatStoryViews } from '../../utils/storyBrowseMap';
import {
    getPendingStories,
    getPendingChapters,
    getModeratorReviewedStories,
    getModeratorReviewedChapters,
} from '../../api/moderator/moderatorApi';
import { getSlaBadgeStyle, normalizeTimeStatus } from '../../utils/moderatorReviewSla';

function formatCompactNumber(n) {
    const num = Number(n ?? 0);
    if (!Number.isFinite(num)) return '0';
    if (num >= 1e9) return `${(num / 1e9).toFixed(1)}B`;
    if (num >= 1e6) return `${(num / 1e6).toFixed(1)}M`;
    if (num >= 1e3) return `${(num / 1e3).toFixed(1)}K`;
    return num.toLocaleString('vi-VN');
}

function formatVndCompact(n) {
    const num = Number(n ?? 0);
    if (!Number.isFinite(num)) return '0';
    // BE: platformRevenueVnd là VND
    if (num >= 1e9) return `${(num / 1e9).toFixed(1)}B`;
    if (num >= 1e6) return `${(num / 1e6).toFixed(1)}M`;
    if (num >= 1e3) return `${(num / 1e3).toFixed(1)}K`;
    return `${Math.round(num)}`;
}

function safeTimeAgo(dateLike) {
    if (!dateLike) return '';
    const d = new Date(dateLike);
    if (Number.isNaN(d.getTime())) return '';
    const diffMs = Date.now() - d.getTime();
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);
    if (diffMins < 60) return `${diffMins} phút trước`;
    if (diffHours < 24) return `${diffHours} giờ trước`;
    if (diffDays < 7) return `${diffDays} ngày trước`;
    return d.toLocaleDateString('vi-VN');
}

export function AdminDashboard({ onNavigatePublicationStatus } = {}) {
    const { isAdmin, role } = useAuth();
    const roleUpper = (role ?? '').toString().toUpperCase();
    const canFetchWallet = isAdmin || roleUpper === 'ADMIN';
    const isModeratorPanel = roleUpper === 'MODERATOR';

    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');

    const [stats, setStats] = useState([
        { title: 'Tổng người dùng', value: '—', icon: Users, color: 'blue' },
        { title: 'Tổng truyện', value: '—', icon: FileText, color: 'green' },
        { title: 'Lượt xem', value: '—', icon: Eye, color: 'purple' },
        { title: 'Doanh thu', value: '—', icon: DollarSign, color: 'amber' },
    ]);

    const [recentActivities, setRecentActivities] = useState([]);
    const [topStories, setTopStories] = useState([]);
    const [chartUsersTotal, setChartUsersTotal] = useState(0);
    const [chartUsersReaders, setChartUsersReaders] = useState(0);
    const [chartUsersAuthors, setChartUsersAuthors] = useState(0);

    // Moderator-only counts (để render dashboard đúng nhiệm vụ duyệt)
    const [modPendingStoriesCount, setModPendingStoriesCount] = useState(0);
    const [modPendingChaptersCount, setModPendingChaptersCount] = useState(0);
    const [modReviewedStoriesCount, setModReviewedStoriesCount] = useState(0);
    const [modReviewedChaptersCount, setModReviewedChaptersCount] = useState(0);

    // SLA breakdown cho backlog pending
    const [modSlaCounts, setModSlaCounts] = useState({
        OnTime: 0,
        Warning: 0,
        Critical: 0,
        Overdue: 0,
        Unknown: 0,
    });

    const chartsVip = useMemo(() => {
        const total = Number(chartUsersTotal ?? 0);
        const readers = Number(chartUsersReaders ?? 0);
        const authors = Number(chartUsersAuthors ?? 0);
        return Math.max(total - readers - authors, 0);
    }, [chartUsersTotal, chartUsersReaders, chartUsersAuthors]);

    const barHeights = useMemo(() => {
        if (!isModeratorPanel) return [65, 45, 78, 52, 88, 45, 92, 73, 56, 84, 67, 91];
        const a = Number(chartUsersReaders ?? 0);
        const b = Number(chartUsersAuthors ?? 0);
        const c = Number(chartsVip ?? 0);
        const sum = a + b + c;
        const safeSum = sum > 0 ? sum : 1;
        const toHeight = (v) => 10 + Math.round((Number(v) / safeSum) * 80); // percent
        const hA = toHeight(a);
        const hB = toHeight(b);
        const hC = toHeight(c);
        return [hA, hB, hC, hA, hB, hC, hA, hB, hC, hA, hB, hC];
    }, [isModeratorPanel, chartUsersReaders, chartUsersAuthors, chartsVip]);

    const statsToShow = useMemo(() => {
        if (!isModeratorPanel) return stats;
        // MODERATOR: giảm số card, ưu tiên 3 nhóm chính
        return [stats?.[0], stats?.[1], stats?.[3]].filter(Boolean);
    }, [isModeratorPanel, stats]);

    useEffect(() => {
        let cancelled = false;
        (async () => {
            setLoading(true);
            setError('');
            try {
                if (isModeratorPanel) {
                    const [
                        pendingStoriesRes,
                        pendingChaptersRes,
                        reviewedStoriesRes,
                        reviewedChaptersRes,
                    ] = await Promise.all([
                        getPendingStories({ page: 1, pageSize: 5 }).catch(() => null),
                        getPendingChapters({ page: 1, pageSize: 5 }).catch(() => null),
                        getModeratorReviewedStories({ status: 'PUBLISHED', page: 1, pageSize: 4 }).catch(() => null),
                        getModeratorReviewedChapters({ status: 'PUBLISHED', page: 1, pageSize: 4 }).catch(() => null),
                    ]);

                    const pendingStoriesItems = Array.isArray(pendingStoriesRes?.items)
                        ? pendingStoriesRes.items
                        : Array.isArray(pendingStoriesRes?.Items)
                            ? pendingStoriesRes.Items
                            : [];
                    const pendingChaptersItems = Array.isArray(pendingChaptersRes?.items)
                        ? pendingChaptersRes.items
                        : Array.isArray(pendingChaptersRes?.Items)
                            ? pendingChaptersRes.Items
                            : [];
                    const reviewedStoriesItems = Array.isArray(reviewedStoriesRes?.items)
                        ? reviewedStoriesRes.items
                        : Array.isArray(reviewedStoriesRes?.Items)
                            ? reviewedStoriesRes.Items
                            : [];
                    const reviewedChaptersItems = Array.isArray(reviewedChaptersRes?.items)
                        ? reviewedChaptersRes.items
                        : Array.isArray(reviewedChaptersRes?.Items)
                            ? reviewedChaptersRes.Items
                            : [];

                    const pendingStoriesCount =
                        Number(pendingStoriesRes?.totalCount ?? pendingStoriesRes?.TotalCount ?? pendingStoriesRes?.total ?? pendingStoriesRes?.Total ?? 0) ||
                        pendingStoriesItems.length ||
                        0;
                    const pendingChaptersCount =
                        Number(pendingChaptersRes?.totalCount ?? pendingChaptersRes?.TotalCount ?? pendingChaptersRes?.total ?? pendingChaptersRes?.Total ?? 0) ||
                        pendingChaptersItems.length ||
                        0;
                    const reviewedStoriesCount =
                        Number(reviewedStoriesRes?.totalCount ?? reviewedStoriesRes?.TotalCount ?? reviewedStoriesRes?.total ?? reviewedStoriesRes?.Total ?? 0) ||
                        reviewedStoriesItems.length ||
                        0;
                    const reviewedChaptersCount =
                        Number(reviewedChaptersRes?.totalCount ?? reviewedChaptersRes?.TotalCount ?? reviewedChaptersRes?.total ?? reviewedChaptersRes?.Total ?? 0) ||
                        reviewedChaptersItems.length ||
                        0;

                    // SLA breakdown cho backlog pending (chỉ dựa trên item đang trả về ở page đầu tiên).
                    // Nếu backend trả ít hơn pageSize thì vẫn hiển thị được thống kê "ước lượng".
                    const allPending = [
                        ...(Array.isArray(pendingStoriesItems) ? pendingStoriesItems : []),
                        ...(Array.isArray(pendingChaptersItems) ? pendingChaptersItems : []),
                    ];
                    const slaInit = { OnTime: 0, Warning: 0, Critical: 0, Overdue: 0, Unknown: 0 };
                    for (const it of allPending) {
                        const raw =
                            it?.timeStatus ??
                            it?.TimeStatus ??
                            it?.time_status ??
                            it?.timeStatusRaw ??
                            null;
                        const n = normalizeTimeStatus(raw);
                        if (n && typeof slaInit[n] === 'number') slaInit[n] += 1;
                        else slaInit.Unknown += 1;
                    }
                    setModSlaCounts(slaInit);

                    setModPendingStoriesCount(pendingStoriesCount);
                    setModPendingChaptersCount(pendingChaptersCount);
                    setModReviewedStoriesCount(reviewedStoriesCount);
                    setModReviewedChaptersCount(reviewedChaptersCount);

                    const activities = [];
                    // 3 pending stories
                    pendingStoriesItems.slice(0, 3).forEach((s, idx) => {
                        const author =
                            s?.authorName ?? s?.AuthorName ?? s?.author ?? s?.Author ?? 'Tác giả';
                        activities.push({
                            id: idx + 1,
                            user: author,
                            action: 'đang chờ duyệt truyện',
                            title: s?.title ?? s?.Title ?? s?.storyTitle ?? '',
                            time: safeTimeAgo(s?.updatedAt ?? s?.UpdatedAt ?? s?.createdAt ?? s?.CreatedAt),
                            avatar: '',
                        });
                    });
                    // fill remaining with pending chapters
                    pendingChaptersItems.slice(0, Math.max(0, 5 - activities.length)).forEach((c, idx) => {
                        const author = c?.storyAuthorName ?? c?.story_author_name ?? c?.authorName ?? c?.AuthorName ?? 'Tác giả';
                        activities.push({
                            id: activities.length + idx + 1,
                            user: author,
                            action: 'đang chờ duyệt chương',
                            title: c?.chapterTitle ?? c?.Title ?? c?.title ?? '',
                            time: safeTimeAgo(c?.updatedAt ?? c?.UpdatedAt ?? c?.createdAt ?? c?.CreatedAt),
                            avatar: '',
                        });
                    });

                    const reviewedStoryMapped = reviewedStoriesItems
                        .map(mapStoryListItemToBrowseStory)
                        .filter(Boolean);
                    const topStoriesMapped = reviewedStoryMapped.length
                        ? reviewedStoryMapped.slice(0, 4)
                        : pendingStoriesItems.map(mapStoryListItemToBrowseStory).filter(Boolean).slice(0, 4);

                    const nextStats = [
                        {
                            title: 'Chờ duyệt truyện',
                            value: pendingStoriesCount ? pendingStoriesCount.toLocaleString('vi-VN') : '0',
                            icon: FileText,
                            color: 'blue',
                        },
                        {
                            title: 'Chờ duyệt chương',
                            value: pendingChaptersCount ? pendingChaptersCount.toLocaleString('vi-VN') : '0',
                            icon: MessageSquare,
                            color: 'green',
                        },
                        {
                            title: 'Đã duyệt truyện',
                            value: reviewedStoriesCount ? reviewedStoriesCount.toLocaleString('vi-VN') : '0',
                            icon: Eye,
                            color: 'purple',
                        },
                        {
                            title: 'Đã duyệt chương',
                            value: reviewedChaptersCount ? reviewedChaptersCount.toLocaleString('vi-VN') : '0',
                            icon: TrendingUp,
                            color: 'amber',
                        },
                    ];

                    const total = pendingStoriesCount + pendingChaptersCount + reviewedStoriesCount;
                    setStats(nextStats);
                    setRecentActivities(activities.slice(0, 5));
                    setTopStories(topStoriesMapped);

                    // Pie chart: (pending stories / pending chapters / reviewed)
                    setChartUsersTotal(total);
                    setChartUsersReaders(pendingStoriesCount);
                    setChartUsersAuthors(pendingChaptersCount);
                } else {
                    const [
                        userStats,
                        storyCountRes,
                        topViewsRes,
                        recentStoriesRes,
                        walletSummaryRes,
                    ] = await Promise.all([
                        getStats().catch(() => null),
                        getStories({ status: 'PUBLISHED', page: 1, pageSize: 1 }).catch(() => null),
                        getStories({ status: 'PUBLISHED', page: 1, pageSize: 12, sortBy: 'total_views', sortOrder: 'desc' }).catch(() => null),
                        getStories({ status: 'PUBLISHED', page: 1, pageSize: 5, sortBy: 'created_at', sortOrder: 'desc' }).catch(() => null),
                        canFetchWallet ? getAdminWalletSummary().catch(() => null) : Promise.resolve(null),
                    ]);

                    const totalUsers = Number(userStats?.total ?? userStats?.Total ?? 0) || 0;
                    const authorsFromUserStats = Number(userStats?.authors ?? userStats?.Authors ?? 0) || 0;
                    const storyTotalCount = Number(storyCountRes?.totalCount ?? storyCountRes?.TotalCount ?? 0) || 0;

                    const topRaw = Array.isArray(topViewsRes?.items) ? topViewsRes.items : (topViewsRes?.Items ?? []);
                    const topMapped = topRaw.map(mapStoryListItemToBrowseStory).filter(Boolean);
                    const topViewsTotalApprox = topMapped.reduce((sum, s) => sum + Number(s?.views ?? 0), 0);

                    const recentRaw = Array.isArray(recentStoriesRes?.items) ? recentStoriesRes.items : (recentStoriesRes?.Items ?? []);
                    const recentMapped = recentRaw.map(mapStoryListItemToBrowseStory).filter(Boolean);
                    const recentTimeList = recentRaw.map((r) => r?.createdAt ?? r?.CreatedAt ?? r?.created_at ?? r?.Created_at ?? r?.updatedAt ?? r?.UpdatedAt);

                    const mappedRecentActivities = recentMapped.map((s, idx) => ({
                        id: idx + 1,
                        user: s?.author ?? 'Tác giả',
                        action: 'đã đăng truyện mới',
                        title: s?.title ?? '',
                        time: safeTimeAgo(recentTimeList?.[idx]),
                        avatar: '',
                    }));

                    const walletSummary = walletSummaryRes ?? {};
                    const platformRevenueVnd = Number(walletSummary.platformRevenueVnd ?? walletSummary.platformRevenue ?? 0) || 0;
                    const activeReaders = Number(walletSummary.activeReaders ?? 0) || 0;
                    const activeAuthors = Number(walletSummary.activeAuthors ?? 0) || 0;

                    const nextStats = [
                        {
                            title: 'Tổng người dùng',
                            value: totalUsers ? totalUsers.toLocaleString('vi-VN') : '0',
                            icon: Users,
                            color: 'blue',
                        },
                        {
                            title: 'Tổng truyện',
                            value: storyTotalCount ? storyTotalCount.toLocaleString('vi-VN') : '0',
                            icon: FileText,
                            color: 'green',
                        },
                        {
                            title: 'Lượt xem',
                            value: formatStoryViews(topViewsTotalApprox),
                            icon: Eye,
                            color: 'purple',
                        },
                        {
                            title: 'Doanh thu',
                            value: platformRevenueVnd ? `${formatVndCompact(platformRevenueVnd)}VND` : '—',
                            icon: DollarSign,
                            color: 'amber',
                        },
                    ];

                    if (!cancelled) {
                        setStats(nextStats);
                        setRecentActivities(mappedRecentActivities);
                        setTopStories(topMapped.slice(0, 4));

                        // Pie chart: active readers / authors (wallet summary)
                        setChartUsersTotal(totalUsers);
                        setChartUsersReaders(activeReaders || totalUsers - authorsFromUserStats);
                        setChartUsersAuthors(activeAuthors || authorsFromUserStats);
                    }
                }

                if (!cancelled) setLoading(false);
            } catch (e) {
                if (!cancelled) {
                    setError(e?.message ?? 'Không tải được dashboard.');
                    setLoading(false);
                }
            }
        })();

        return () => {
            cancelled = true;
        };
    }, [canFetchWallet, isModeratorPanel]);

    const colorClasses = useMemo(
        () => ({
            blue: 'bg-blue-100 dark:bg-blue-950/30 text-blue-600 dark:text-blue-400',
            green: 'bg-green-100 dark:bg-green-950/30 text-green-600 dark:text-green-400',
            purple: 'bg-purple-100 dark:bg-purple-950/30 text-purple-600 dark:text-purple-400',
            amber: 'bg-amber-100 dark:bg-amber-950/30 text-amber-600 dark:text-amber-400',
        }),
        []
    );

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div>
                <h1 className="text-2xl font-bold text-slate-900 dark:text-white">
                    {isModeratorPanel ? 'Tổng quan kiểm duyệt' : 'Dashboard'}
                </h1>
                <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
                    {isModeratorPanel
                        ? 'Theo dõi nhanh khối lượng duyệt truyện/chương và tiến độ xử lý.'
                        : 'Chào mừng trở lại! Đây là tổng quan hệ thống của bạn.'}
                </p>
            </div>

            {error ? (
                <div className="rounded-xl border border-red-200 bg-red-50 px-4 py-3 text-sm font-semibold text-red-700">
                    {error}
                </div>
            ) : null}

            {/* Stats Grid */}
            <div className={`grid grid-cols-1 sm:grid-cols-2 ${isModeratorPanel ? 'lg:grid-cols-3' : 'lg:grid-cols-4'} gap-4`}>
                {statsToShow.map((stat) => {
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
                                <button className="p-1 hover:bg-slate-100 dark:hover:bg-slate-800 rounded transition-colors">
                                    <MoreVertical className="w-4 h-4 text-slate-400" />
                                </button>
                            </div>
                            <p className="text-sm text-slate-500 dark:text-slate-400 mb-1">
                                {stat.title}
                            </p>
                            <p className="text-2xl font-bold text-slate-900 dark:text-white mb-2">
                                {loading ? '…' : stat.value}
                            </p>
                            <div className="flex items-center gap-1 text-xs font-semibold text-slate-500 dark:text-slate-400">
                                Cập nhật gần nhất
                            </div>
                        </div>
                    );
                })}
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* Recent Activities */}
                <div className={`lg:${isModeratorPanel ? 'col-span-3' : 'col-span-2'} bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800`}>
                    <div className="p-6 border-b border-slate-200 dark:border-slate-800">
                        <h2 className="text-lg font-bold text-slate-900 dark:text-white">
                            Hoạt động gần đây
                        </h2>
                    </div>
                    <div className="p-6">
                        <div className="space-y-4">
                            {(loading ? [] : recentActivities).map((activity) => (
                                <div key={activity.id} className="flex items-start gap-4">
                                    {activity.avatar ? (
                                        <img src={activity.avatar} alt={activity.user} className="w-10 h-10 rounded-full" />
                                    ) : (
                                        <div className="w-10 h-10 rounded-full bg-primary/10 text-primary flex items-center justify-center font-bold text-sm shrink-0">
                                            {(activity.user?.[0] ?? 'T').toUpperCase()}
                                        </div>
                                    )}
                                    <div className="flex-1 min-w-0">
                                        <p className="text-sm text-slate-900 dark:text-white">
                                            <span className="font-semibold">{activity.user}</span>{' '}
                                            <span className="text-slate-500 dark:text-slate-400">{activity.action}</span>{' '}
                                            <span className="font-semibold">{activity.title}</span>
                                        </p>
                                        <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
                                            {activity.time}
                                        </p>
                                    </div>
                                </div>
                            ))}
                        </div>
                    </div>
                </div>

                {!isModeratorPanel ? (
                    /* Top Stories (chỉ hiển thị cho ADMIN để không quá nhiều thông tin cho MODERATOR) */
                    <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800">
                        <div className="p-6 border-b border-slate-200 dark:border-slate-800">
                            <h2 className="text-lg font-bold text-slate-900 dark:text-white">
                                Truyện nổi bật
                            </h2>
                        </div>
                        <div className="p-6">
                            <div className="space-y-4">
                                {(loading ? [] : topStories).map((story, index) => (
                                    <div key={story.id} className="flex items-start gap-3">
                                        <div className="shrink-0 flex items-center justify-center w-6 h-6 rounded-full bg-primary/10 text-primary text-xs font-bold">
                                            {index + 1}
                                        </div>
                                        {story.cover ? (
                                            <img
                                                src={story.cover}
                                                alt={story.title}
                                                className="w-12 h-16 object-cover rounded"
                                            />
                                        ) : (
                                            <div className="w-12 h-16 rounded bg-slate-100 dark:bg-slate-800" />
                                        )}
                                        <div className="flex-1 min-w-0">
                                            <p className="font-semibold text-sm text-slate-900 dark:text-white line-clamp-2">
                                                {story.title}
                                            </p>
                                            <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
                                                {story.author}
                                            </p>
                                            <div className="flex items-center gap-3 mt-1">
                                                <span className="text-xs text-slate-500 dark:text-slate-400">
                                                    👁️ {formatStoryViews(story.views)}
                                                </span>
                                                <span className="text-xs text-amber-600 dark:text-amber-400">
                                                    ⭐ {story.rating ?? '—'}
                                                </span>
                                            </div>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        </div>
                    </div>
                ) : null}
            </div>

            {/* Chart Area */}
            <div className={`grid grid-cols-1 ${isModeratorPanel ? '' : 'lg:grid-cols-2'} gap-6`}>
                {isModeratorPanel ? (
                    <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 p-6">
                        <h2 className="text-lg font-bold text-slate-900 dark:text-white mb-2">
                            Thống kê MODERATOR
                        </h2>

                        {(() => {
                            const pendingTotal = Number(modPendingStoriesCount ?? 0) + Number(modPendingChaptersCount ?? 0);
                            const reviewedTotal = Number(modReviewedStoriesCount ?? 0) + Number(modReviewedChaptersCount ?? 0);
                            const denom = pendingTotal + reviewedTotal;
                            const progress = denom > 0 ? Math.round((reviewedTotal / denom) * 100) : 0;

                            const totalSla =
                                Number(modSlaCounts.OnTime ?? 0) +
                                Number(modSlaCounts.Warning ?? 0) +
                                Number(modSlaCounts.Critical ?? 0) +
                                Number(modSlaCounts.Overdue ?? 0) +
                                Number(modSlaCounts.Unknown ?? 0);
                            const pct = (v) => (totalSla > 0 ? (Number(v) / totalSla) * 100 : 0);

                            const segment = (widthPct, bg) =>
                                widthPct > 0 ? (
                                    <div style={{ width: `${widthPct}%`, background: bg }} className="h-full" />
                                ) : null;

                            return (
                                <>
                                    <p className="text-sm text-slate-500 dark:text-slate-400">
                                        Pending: <span className="font-semibold">{loading ? '...' : pendingTotal}</span> • Đã duyệt: <span className="font-semibold">{loading ? '...' : reviewedTotal}</span>
                                    </p>
                                    <div className="mt-3">
                                        <div className="flex items-center justify-between text-xs text-slate-500 dark:text-slate-400 mb-2">
                                            <span>Tiến độ</span>
                                            <span className="font-semibold text-slate-700 dark:text-slate-200">{loading ? '...' : `${progress}%`}</span>
                                        </div>
                                        <div className="w-full h-2 rounded-full bg-slate-100 dark:bg-slate-800 overflow-hidden">
                                            <div
                                                className="h-full rounded-full"
                                                style={{
                                                    width: `${progress}%`,
                                                    background: '#13ec5b',
                                                }}
                                            />
                                        </div>
                                    </div>

                                    <div className="mt-5">
                                        <div className="flex items-center justify-between text-sm font-semibold text-slate-900 dark:text-white mb-3">
                                            <span>SLA backlog (ước lượng từ danh sách page đầu)</span>
                                            <span className="text-xs font-semibold text-slate-500 dark:text-slate-400">
                                                {totalSla ? `${totalSla} item` : ''}
                                            </span>
                                        </div>

                                        <div className="w-full h-2 rounded-full bg-slate-100 dark:bg-slate-800 overflow-hidden flex">
                                            {segment(pct(modSlaCounts.OnTime), '#d1fae5')}
                                            {segment(pct(modSlaCounts.Warning), '#fef3c7')}
                                            {segment(pct(modSlaCounts.Critical), '#ffedd5')}
                                            {segment(pct(modSlaCounts.Overdue), '#fee2e2')}
                                            {segment(pct(modSlaCounts.Unknown), '#f1f5f9')}
                                        </div>

                                        <div className="mt-3 grid grid-cols-2 gap-2">
                                            {(['OnTime', 'Warning', 'Critical', 'Overdue']).map((key) => {
                                                const style = getSlaBadgeStyle(key);
                                                const value = modSlaCounts[key] ?? 0;
                                                return (
                                                    <div
                                                        key={key}
                                                        className="flex items-center justify-between px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-800/40"
                                                    >
                                                        <span className="text-xs font-semibold" style={{ color: style.color }}>
                                                            {style.label}
                                                        </span>
                                                        <span className="text-xs font-bold text-slate-900 dark:text-white">
                                                            {loading ? '...' : value}
                                                        </span>
                                                    </div>
                                                );
                                            })}
                                            <div
                                                className="flex items-center justify-between px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-800/40"
                                            >
                                                <span className="text-xs font-semibold" style={{ color: '#475569' }}>
                                                    Khác
                                                </span>
                                                <span className="text-xs font-bold text-slate-900 dark:text-white">
                                                    {loading ? '...' : modSlaCounts.Unknown}
                                                </span>
                                            </div>
                                        </div>
                                    </div>
                                </>
                            );
                        })()}
                    </div>
                ) : (
                    <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 p-6">
                        <h2 className="text-lg font-bold text-slate-900 dark:text-white mb-4">
                            Thống kê lượt xem
                        </h2>
                        <div className="h-64 flex items-end justify-between gap-2">
                            {barHeights.map((height, index) => (
                                <div
                                    key={index}
                                    className="flex-1 bg-primary/20 hover:bg-primary/40 rounded-t transition-colors relative group cursor-pointer"
                                    style={{ height: `${height}%` }}
                                >
                                    <div className="absolute -top-8 left-1/2 -translate-x-1/2 bg-slate-900 dark:bg-white text-white dark:text-slate-900 px-2 py-1 rounded text-xs opacity-0 group-hover:opacity-100 transition-opacity whitespace-nowrap">
                                        {height}K views
                                    </div>
                                </div>
                            ))}
                        </div>
                        <div className="flex justify-between mt-4 text-xs text-slate-500 dark:text-slate-400">
                            {['T1', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'T8', 'T9', 'T10', 'T11', 'T12'].map((month) => (
                                <span key={month}>{month}</span>
                            ))}
                        </div>
                    </div>
                )}

                {!isModeratorPanel ? (
                    <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 p-6">
                        <h2 className="text-lg font-bold text-slate-900 dark:text-white mb-4">
                            Thống kê người dùng
                        </h2>
                        <div className="h-64 flex items-center justify-center">
                            <div className="relative w-48 h-48">
                                {/* Simple pie chart representation */}
                                <div
                                    className="absolute inset-0 rounded-full"
                                    style={{
                                        background: 'conic-gradient(#13ec5b 0deg 180deg, #3b82f6 180deg 270deg, #f59e0b 270deg 360deg)'
                                    }}
                                ></div>
                                <div className="absolute inset-4 bg-white dark:bg-slate-900 rounded-full flex items-center justify-center">
                                    <div className="text-center">
                                        <p className="text-2xl font-bold text-slate-900 dark:text-white">
                                            {chartUsersTotal ? formatCompactNumber(chartUsersTotal) : '—'}
                                        </p>
                                        <p className="text-xs text-slate-500 dark:text-slate-400">Tổng users</p>
                                    </div>
                                </div>
                            </div>
                        </div>
                        <div className="grid grid-cols-3 gap-4 mt-6">
                            <div className="text-center">
                                <div className="flex items-center justify-center gap-2 mb-1">
                                    <div className="w-3 h-3 rounded-full bg-primary"></div>
                                    <span className="text-xs text-slate-500 dark:text-slate-400">Độc giả</span>
                                </div>
                                <p className="text-sm font-bold text-slate-900 dark:text-white">
                                    {chartUsersTotal ? `${Math.round((chartUsersReaders / chartUsersTotal) * 100)}%` : '—'}
                                </p>
                            </div>
                            <div className="text-center">
                                <div className="flex items-center justify-center gap-2 mb-1">
                                    <div className="w-3 h-3 rounded-full bg-blue-500"></div>
                                    <span className="text-xs text-slate-500 dark:text-slate-400">Tác giả</span>
                                </div>
                                <p className="text-sm font-bold text-slate-900 dark:text-white">
                                    {chartUsersTotal ? `${Math.round((chartUsersAuthors / chartUsersTotal) * 100)}%` : '—'}
                                </p>
                            </div>
                            <div className="text-center">
                                <div className="flex items-center justify-center gap-2 mb-1">
                                    <div className="w-3 h-3 rounded-full bg-amber-500"></div>
                                    <span className="text-xs text-slate-500 dark:text-slate-400">VIP</span>
                                </div>
                                <p className="text-sm font-bold text-slate-900 dark:text-white">
                                    {chartUsersTotal ? `${Math.round((chartsVip / chartUsersTotal) * 100)}%` : '—'}
                                </p>
                            </div>
                        </div>
                    </div>
                ) : null}
            </div>
        </div>
    );
}
