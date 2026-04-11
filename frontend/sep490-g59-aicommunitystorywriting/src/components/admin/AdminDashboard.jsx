import {
    TrendingUp,
    Users,
    FileText,
    Eye,
    MessageSquare,
    DollarSign,
    ArrowUp,
    ArrowDown
} from 'lucide-react';

import { useEffect, useMemo, useState } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { Pagination } from '../pagination/Pagination';
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
    // eslint: prop này được truyền từ nơi gọi (để điều hướng), nhưng dashboard hiện chưa sử dụng trực tiếp.
    // Dùng `void` để tránh lỗi ESLint "assigned a value but never used".
    void onNavigatePublicationStatus;
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
    const [activityPage, setActivityPage] = useState(1);
    const [topStories, setTopStories] = useState([]);
    const [chartUsersTotal, setChartUsersTotal] = useState(0);
    const [chartUsersReaders, setChartUsersReaders] = useState(0);
    const [chartUsersAuthors, setChartUsersAuthors] = useState(0);
    const [viewChartPoints, setViewChartPoints] = useState([]);

    // Moderator-only counts (để render dashboard đúng nhiệm vụ duyệt)
    const [modPendingStoriesCount, setModPendingStoriesCount] = useState(0);
    const [modPendingChaptersCount, setModPendingChaptersCount] = useState(0);
    const [modReviewedStoriesCount, setModReviewedStoriesCount] = useState(0);
    const [modReviewedChaptersCount, setModReviewedChaptersCount] = useState(0);
    void modPendingStoriesCount;
    void modPendingChaptersCount;
    const ACTIVITY_PAGE_SIZE = 10;

    const chartsVip = useMemo(() => {
        const total = Number(chartUsersTotal ?? 0);
        const readers = Number(chartUsersReaders ?? 0);
        const authors = Number(chartUsersAuthors ?? 0);
        return Math.max(total - readers - authors, 0);
    }, [chartUsersTotal, chartUsersReaders, chartUsersAuthors]);

    const viewBars = useMemo(() => {
        if (!Array.isArray(viewChartPoints) || viewChartPoints.length === 0) {
            return [];
        }
        const maxViews = Math.max(...viewChartPoints.map((p) => Number(p?.views ?? 0)), 1);
        return viewChartPoints.map((point, index) => {
            const views = Number(point?.views ?? 0);
            const height = 10 + Math.round((views / maxViews) * 80);
            return {
                key: point?.id ?? `v-${index}`,
                label: point?.label ?? `#${index + 1}`,
                views,
                height,
            };
        });
    }, [viewChartPoints]);

    const statsToShow = useMemo(() => {
        if (!isModeratorPanel) return stats;
        // Kiểm duyệt viên: ưu tiên đơn chờ duyệt + kết quả duyệt truyện/chương.
        return [stats?.[0], stats?.[2], stats?.[3]].filter(Boolean);
    }, [isModeratorPanel, stats]);

    useEffect(() => {
        let cancelled = false;
        (async () => {
            setLoading(true);
            setError('');
            try {
                if (isModeratorPanel) {
                    const [
                        pendingStoriesUnclaimedRes,
                        pendingStoriesClaimedRes,
                        pendingChaptersClaimedRes,
                        reviewedStoriesPublishedRes,
                        reviewedStoriesRejectedRes,
                        reviewedChaptersPublishedRes,
                        reviewedChaptersRejectedRes,
                    ] = await Promise.all([
                        getPendingStories({ claimFilter: 'UNCLAIMED', page: 1, pageSize: 50 }).catch(() => null),
                        getPendingStories({ claimFilter: 'CLAIMED', page: 1, pageSize: 50 }).catch(() => null),
                        getPendingChapters({ claimFilter: 'CLAIMED', page: 1, pageSize: 50 }).catch(() => null),
                        getModeratorReviewedStories({ status: 'PUBLISHED', page: 1, pageSize: 200, sortBy: 'updated_at', sortOrder: 'desc' }).catch(() => null),
                        getModeratorReviewedStories({ status: 'REJECTED', page: 1, pageSize: 200, sortBy: 'updated_at', sortOrder: 'desc' }).catch(() => null),
                        getModeratorReviewedChapters({ status: 'PUBLISHED', page: 1, pageSize: 200, sortBy: 'updated_at', sortOrder: 'desc' }).catch(() => null),
                        getModeratorReviewedChapters({ status: 'REJECTED', page: 1, pageSize: 200, sortBy: 'updated_at', sortOrder: 'desc' }).catch(() => null),
                    ]);

                    const toItems = (res) => (Array.isArray(res?.items) ? res.items : (Array.isArray(res?.Items) ? res.Items : []));
                    const toCount = (res, items) => (
                        Number(res?.totalCount ?? res?.TotalCount ?? res?.total ?? res?.Total ?? 0) || items.length || 0
                    );

                    const pendingStoriesUnclaimedItems = toItems(pendingStoriesUnclaimedRes);
                    const pendingStoriesClaimedItems = toItems(pendingStoriesClaimedRes);
                    const pendingChaptersClaimedItems = toItems(pendingChaptersClaimedRes);
                    const pendingStoriesItems = [...pendingStoriesUnclaimedItems, ...pendingStoriesClaimedItems];

                    const reviewedStoriesPublishedItems = toItems(reviewedStoriesPublishedRes);
                    const reviewedStoriesRejectedItems = toItems(reviewedStoriesRejectedRes);
                    const reviewedChaptersPublishedItems = toItems(reviewedChaptersPublishedRes);
                    const reviewedChaptersRejectedItems = toItems(reviewedChaptersRejectedRes);
                    // Dashboard kiểm duyệt viên cần khớp tab "Chờ duyệt" (đơn đang xử lý),
                    // không tính đơn chưa nhận.
                    const pendingStoriesCount = toCount(pendingStoriesClaimedRes, pendingStoriesClaimedItems);
                    const pendingChaptersCount = toCount(pendingChaptersClaimedRes, pendingChaptersClaimedItems);
                    const pendingOrderCount = (() => {
                        const normalizeId = (v) => (v != null ? String(v).toLowerCase() : '');
                        const ids = new Set();
                        pendingStoriesClaimedItems.forEach((s) => {
                            const sid = normalizeId(s?.id ?? s?.Id ?? s?.storyId ?? s?.StoryId);
                            if (sid) ids.add(sid);
                        });
                        pendingChaptersClaimedItems.forEach((c) => {
                            const sid = normalizeId(c?.storyId ?? c?.StoryId);
                            if (sid) ids.add(sid);
                        });
                        return ids.size;
                    })();
                    const reviewedStoriesCount = toCount(reviewedStoriesPublishedRes, reviewedStoriesPublishedItems)
                        + toCount(reviewedStoriesRejectedRes, reviewedStoriesRejectedItems);
                    const reviewedChaptersCount = toCount(reviewedChaptersPublishedRes, reviewedChaptersPublishedItems)
                        + toCount(reviewedChaptersRejectedRes, reviewedChaptersRejectedItems);

                    setModPendingStoriesCount(pendingOrderCount);
                    setModPendingChaptersCount(0);
                    setModReviewedStoriesCount(reviewedStoriesCount);
                    setModReviewedChaptersCount(reviewedChaptersCount);

                    const normalizeTs = (item) => (
                        item?.updatedAt ?? item?.UpdatedAt ?? item?.createdAt ?? item?.CreatedAt ?? null
                    );
                    const chapterActivitiesPublished = reviewedChaptersPublishedItems.map((c, idx) => {
                        const storyTitle = c?.storyTitle ?? c?.StoryTitle ?? 'Truyện';
                        const chapterTitle = c?.title ?? c?.Title ?? c?.chapterTitle ?? 'Chương';
                        const orderIndex = Number(c?.orderIndex ?? c?.OrderIndex ?? 0);
                        const chapterNo = Number.isFinite(orderIndex) ? Math.max(1, orderIndex + 1) : null;
                        const user = c?.moderatorName ?? c?.ModeratorName ?? 'Kiểm duyệt viên';
                        const createdAt = normalizeTs(c);
                        return {
                            id: `cp-${idx}-${c?.id ?? c?.Id ?? chapterTitle}`,
                            user,
                            action: 'đã duyệt',
                            title: `${chapterNo ? `chương ${chapterNo}` : 'chương'}: ${chapterTitle} (truyện: ${storyTitle})`,
                            reason: '',
                            createdAt,
                            time: safeTimeAgo(createdAt),
                            avatar: '',
                        };
                    });
                    const chapterActivitiesRejected = reviewedChaptersRejectedItems.map((c, idx) => {
                        const storyTitle = c?.storyTitle ?? c?.StoryTitle ?? 'Truyện';
                        const chapterTitle = c?.title ?? c?.Title ?? c?.chapterTitle ?? 'Chương';
                        const orderIndex = Number(c?.orderIndex ?? c?.OrderIndex ?? 0);
                        const chapterNo = Number.isFinite(orderIndex) ? Math.max(1, orderIndex + 1) : null;
                        const user = c?.moderatorName ?? c?.ModeratorName ?? 'Kiểm duyệt viên';
                        const reason = c?.rejectionReason ?? c?.RejectionReason ?? '';
                        const createdAt = normalizeTs(c);
                        return {
                            id: `cr-${idx}-${c?.id ?? c?.Id ?? chapterTitle}`,
                            user,
                            action: 'đã từ chối duyệt',
                            title: `${chapterNo ? `chương ${chapterNo}` : 'chương'}: ${chapterTitle} (truyện: ${storyTitle})`,
                            reason: String(reason || '').trim(),
                            createdAt,
                            time: safeTimeAgo(createdAt),
                            avatar: '',
                        };
                    });
                    // Nhật ký chỉ hiển thị log DUYỆT/TỪ CHỐI CHƯƠNG theo yêu cầu.
                    const activities = [...chapterActivitiesPublished, ...chapterActivitiesRejected].sort((a, b) => {
                        const ta = a?.createdAt ? new Date(a.createdAt).getTime() : 0;
                        const tb = b?.createdAt ? new Date(b.createdAt).getTime() : 0;
                        return tb - ta;
                    });

                    const reviewedStoryMapped = reviewedStoriesPublishedItems
                        .map(mapStoryListItemToBrowseStory)
                        .filter(Boolean);
                    const topStoriesMapped = reviewedStoryMapped.length
                        ? reviewedStoryMapped.slice(0, 4)
                        : pendingStoriesItems.map(mapStoryListItemToBrowseStory).filter(Boolean).slice(0, 4);

                    const nextStats = [
                        {
                            title: 'Đơn chờ duyệt',
                            value: pendingOrderCount ? pendingOrderCount.toLocaleString('vi-VN') : '0',
                            icon: FileText,
                            color: 'blue',
                        },
                        {
                            title: 'Chương chờ duyệt',
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

                    const total = pendingStoriesCount + pendingChaptersCount + reviewedStoriesCount + reviewedChaptersCount;
                    setStats(nextStats);
                    setRecentActivities(activities);
                    setActivityPage(1);
                    setTopStories(topStoriesMapped);

                    // Pie chart: (pending stories / pending chapters / reviewed)
                    setChartUsersTotal(total);
                    setChartUsersReaders(pendingStoriesCount + pendingChaptersCount);
                    setChartUsersAuthors(reviewedStoriesCount + reviewedChaptersCount);
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
                    const topViewPoints = topMapped.slice(0, 12).map((story, index) => ({
                        id: story?.id ?? `story-${index}`,
                        label: `Top ${index + 1}`,
                        views: Number(story?.views ?? 0),
                    }));

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
                        setActivityPage(1);
                        setTopStories(topMapped.slice(0, 4));
                        setViewChartPoints(topViewPoints);

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

    const activityTotalPages = useMemo(
        () => Math.max(1, Math.ceil(recentActivities.length / ACTIVITY_PAGE_SIZE)),
        [recentActivities, ACTIVITY_PAGE_SIZE]
    );

    const visibleActivities = useMemo(() => {
        if (!isModeratorPanel) return recentActivities;
        const start = (activityPage - 1) * ACTIVITY_PAGE_SIZE;
        return recentActivities.slice(start, start + ACTIVITY_PAGE_SIZE);
    }, [isModeratorPanel, recentActivities, activityPage, ACTIVITY_PAGE_SIZE]);

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
                            Nhật ký hoạt động
                        </h2>
                    </div>
                    <div className="p-6">
                        <div className="space-y-4">
                            {(loading ? [] : visibleActivities).map((activity) => (
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
                                        {activity.reason ? (
                                            <p className="text-xs text-red-600 dark:text-red-400 mt-1">
                                                Lý do từ chối: {activity.reason}
                                            </p>
                                        ) : null}
                                        <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
                                            {activity.time}
                                        </p>
                                    </div>
                                </div>
                            ))}
                        </div>
                        {isModeratorPanel && !loading && recentActivities.length > 0 ? (
                            <div className="mt-4">
                                <Pagination
                                    currentPage={activityPage}
                                    totalPages={activityTotalPages}
                                    totalItems={recentActivities.length}
                                    itemsPerPage={ACTIVITY_PAGE_SIZE}
                                    onPageChange={setActivityPage}
                                    itemLabel="hoạt động"
                                />
                            </div>
                        ) : null}
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
                            Thống kê kiểm duyệt viên
                        </h2>

                        {(() => {
                            const pendingTotal = Number(modPendingStoriesCount ?? 0);
                            const reviewedStories = Number(modReviewedStoriesCount ?? 0);
                            const reviewedChapters = Number(modReviewedChaptersCount ?? 0);
                            const reviewedTotal = reviewedChapters;
                            const denom = pendingTotal + reviewedTotal;
                            const progress = denom > 0 ? Math.round((reviewedTotal / denom) * 100) : 0;
                            const pendingPct = denom > 0 ? Math.round((pendingTotal / denom) * 100) : 0;
                            const reviewedDeg = denom > 0 ? Math.round((reviewedTotal / denom) * 360) : 0;

                            return (
                                <>
                                    <div className="mt-2 flex flex-col items-center">
                                        <div className="relative w-44 h-44">
                                            <div
                                                className="absolute inset-0 rounded-full"
                                                style={{
                                                    background: `conic-gradient(#13ec5b 0deg ${reviewedDeg}deg, #e2e8f0 ${reviewedDeg}deg 360deg)`,
                                                }}
                                            />
                                            <div className="absolute inset-5 rounded-full bg-white dark:bg-slate-900 flex flex-col items-center justify-center">
                                                <p className="text-3xl font-bold text-slate-900 dark:text-white">
                                                    {loading ? '...' : `${progress}%`}
                                                </p>
                                                <p className="text-xs text-slate-500 dark:text-slate-400">Tiến độ xử lý đơn</p>
                                            </div>
                                        </div>

                                        <div className="mt-4 w-full grid grid-cols-2 gap-3">
                                            <div
                                                className="px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-800/40"
                                            >
                                                <span className="text-xs text-slate-500 dark:text-slate-400">Đang chờ duyệt đơn</span>
                                                <span className="text-xs font-bold text-slate-900 dark:text-white">
                                                    {loading ? '...' : `${pendingTotal} (${pendingPct}%)`}
                                                </span>
                                            </div>
                                            <div
                                                className="px-3 py-2 rounded-lg border border-slate-200 dark:border-slate-800 bg-slate-50 dark:bg-slate-800/40"
                                            >
                                                <span className="text-xs text-slate-500 dark:text-slate-400">Đã duyệt chương</span>
                                                <span className="text-xs font-bold text-slate-900 dark:text-white">
                                                    {loading ? '...' : `${reviewedTotal} (${progress}%)`}
                                                </span>
                                                {!loading ? (
                                                    <p className="text-[11px] text-slate-500 dark:text-slate-400 mt-1 mb-0">
                                                        Truyện đã duyệt: {reviewedStories}
                                                    </p>
                                                ) : null}
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
                            {(loading ? [] : viewBars).map((bar) => (
                                <div
                                    key={bar.key}
                                    className="flex-1 bg-primary/20 hover:bg-primary/40 rounded-t transition-colors relative group cursor-pointer"
                                    style={{ height: `${bar.height}%` }}
                                >
                                    <div className="absolute -top-8 left-1/2 -translate-x-1/2 bg-slate-900 dark:bg-white text-white dark:text-slate-900 px-2 py-1 rounded text-xs opacity-0 group-hover:opacity-100 transition-opacity whitespace-nowrap">
                                        {formatStoryViews(bar.views)}
                                    </div>
                                </div>
                            ))}
                        </div>
                        <div className="flex justify-between mt-4 text-xs text-slate-500 dark:text-slate-400">
                            {(loading ? [] : viewBars).map((bar) => (
                                <span key={`lbl-${bar.key}`}>{bar.label}</span>
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
                                {(() => {
                                    const total = Number(chartUsersTotal ?? 0);
                                    const readers = Number(chartUsersReaders ?? 0);
                                    const authors = Number(chartUsersAuthors ?? 0);
                                    const vip = Number(chartsVip ?? 0);
                                    if (total <= 0) {
                                        return (
                                            <div
                                                className="absolute inset-0 rounded-full"
                                                style={{ background: '#e2e8f0' }}
                                            />
                                        );
                                    }
                                    const readerDeg = Math.round((readers / total) * 360);
                                    const authorDeg = Math.round((authors / total) * 360);
                                    const vipDeg = Math.max(0, 360 - readerDeg - authorDeg);
                                    const authorStart = readerDeg;
                                    const vipStart = readerDeg + authorDeg;
                                    return (
                                        <div
                                            className="absolute inset-0 rounded-full"
                                            style={{
                                                background: `conic-gradient(#13ec5b 0deg ${readerDeg}deg, #3b82f6 ${authorStart}deg ${vipStart}deg, #f59e0b ${vipStart}deg ${vipStart + vipDeg}deg)`,
                                            }}
                                        />
                                    );
                                })()}
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
