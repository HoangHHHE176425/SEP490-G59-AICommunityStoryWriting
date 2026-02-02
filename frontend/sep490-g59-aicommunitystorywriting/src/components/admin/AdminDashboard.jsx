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

export function AdminDashboard() {
    const stats = [
        {
            title: 'Tổng người dùng',
            value: '45,231',
            change: '+12.5%',
            isIncrease: true,
            icon: Users,
            color: 'blue'
        },
        {
            title: 'Tổng truyện',
            value: '8,492',
            change: '+8.2%',
            isIncrease: true,
            icon: FileText,
            color: 'green'
        },
        {
            title: 'Lượt xem',
            value: '2.4M',
            change: '+18.7%',
            isIncrease: true,
            icon: Eye,
            color: 'purple'
        },
        {
            title: 'Doanh thu',
            value: '128M',
            change: '-2.4%',
            isIncrease: false,
            icon: DollarSign,
            color: 'amber'
        },
    ];

    const recentActivities = [
        {
            id: 1,
            user: 'Nguyễn Văn A',
            action: 'đã đăng truyện mới',
            title: 'Tu Tiên Chi Lộ',
            time: '5 phút trước',
            avatar: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=40&h=40&fit=crop'
        },
        {
            id: 2,
            user: 'Trần Thị B',
            action: 'đã bình luận',
            title: 'Đấu Phá Thương Khung',
            time: '10 phút trước',
            avatar: 'https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=40&h=40&fit=crop'
        },
        {
            id: 3,
            user: 'Lê Văn C',
            action: 'đã theo dõi tác giả',
            title: 'Thiên Tằm Thổ Đậu',
            time: '20 phút trước',
            avatar: 'https://images.unsplash.com/photo-1599566150163-29194dcaad36?w=40&h=40&fit=crop'
        },
        {
            id: 4,
            user: 'Phạm Thị D',
            action: 'đã đánh giá',
            title: 'Vũ Luyện Đỉnh Phong',
            time: '1 giờ trước',
            avatar: 'https://images.unsplash.com/photo-1438761681033-6461ffad8d80?w=40&h=40&fit=crop'
        },
        {
            id: 5,
            user: 'Hoàng Văn E',
            action: 'đã mua xu',
            title: '1,000 xu',
            time: '2 giờ trước',
            avatar: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=40&h=40&fit=crop'
        },
    ];

    const topStories = [
        {
            id: 1,
            title: 'Tu Tiên Chi Lộ: Hành Trình Vạn Năm',
            author: 'Thiên Tằm Thổ Đậu',
            views: '2.4M',
            rating: 4.8,
            cover: 'https://images.unsplash.com/photo-1589998059171-988d887df646?w=80&h=120&fit=crop'
        },
        {
            id: 2,
            title: 'Đấu Phá Thương Khung',
            author: 'Thiên Tằm Thổ Đậu',
            views: '1.8M',
            rating: 4.7,
            cover: 'https://images.unsplash.com/photo-1612036801632-22b5e9e5b8b0?w=80&h=120&fit=crop'
        },
        {
            id: 3,
            title: 'Vũ Luyện Đỉnh Phong',
            author: 'Ngã Cật Tây Hồng Thị',
            views: '1.5M',
            rating: 4.6,
            cover: 'https://images.unsplash.com/photo-1614729939124-032898bb2e23?w=80&h=120&fit=crop'
        },
        {
            id: 4,
            title: 'Thần Ấn Vương Tọa',
            author: 'Đường Gia Tam Thiếu',
            views: '1.2M',
            rating: 4.5,
            cover: 'https://images.unsplash.com/photo-1610926597998-259c59f2e7e7?w=80&h=120&fit=crop'
        },
    ];

    return (
        <div className="space-y-6">
            {/* Page Header */}
            <div>
                <h1 className="text-2xl font-bold text-slate-900 dark:text-white">
                    Dashboard
                </h1>
                <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
                    Chào mừng trở lại! Đây là tổng quan hệ thống của bạn.
                </p>
            </div>

            {/* Stats Grid */}
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                {stats.map((stat) => {
                    const Icon = stat.icon;
                    const colorClasses = {
                        blue: 'bg-blue-100 dark:bg-blue-950/30 text-blue-600 dark:text-blue-400',
                        green: 'bg-green-100 dark:bg-green-950/30 text-green-600 dark:text-green-400',
                        purple: 'bg-purple-100 dark:bg-purple-950/30 text-purple-600 dark:text-purple-400',
                        amber: 'bg-amber-100 dark:bg-amber-950/30 text-amber-600 dark:text-amber-400',
                    };

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
                                {stat.value}
                            </p>
                            <div className={`flex items-center gap-1 text-xs font-semibold ${stat.isIncrease ? 'text-green-600 dark:text-green-400' : 'text-red-600 dark:text-red-400'
                                }`}>
                                {stat.isIncrease ? <ArrowUp className="w-3 h-3" /> : <ArrowDown className="w-3 h-3" />}
                                {stat.change} so với tháng trước
                            </div>
                        </div>
                    );
                })}
            </div>

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* Recent Activities */}
                <div className="lg:col-span-2 bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800">
                    <div className="p-6 border-b border-slate-200 dark:border-slate-800">
                        <h2 className="text-lg font-bold text-slate-900 dark:text-white">
                            Hoạt động gần đây
                        </h2>
                    </div>
                    <div className="p-6">
                        <div className="space-y-4">
                            {recentActivities.map((activity) => (
                                <div key={activity.id} className="flex items-start gap-4">
                                    <img
                                        src={activity.avatar}
                                        alt={activity.user}
                                        className="w-10 h-10 rounded-full"
                                    />
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

                {/* Top Stories */}
                <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800">
                    <div className="p-6 border-b border-slate-200 dark:border-slate-800">
                        <h2 className="text-lg font-bold text-slate-900 dark:text-white">
                            Truyện nổi bật
                        </h2>
                    </div>
                    <div className="p-6">
                        <div className="space-y-4">
                            {topStories.map((story, index) => (
                                <div key={story.id} className="flex items-start gap-3">
                                    <div className="shrink-0 flex items-center justify-center w-6 h-6 rounded-full bg-primary/10 text-primary text-xs font-bold">
                                        {index + 1}
                                    </div>
                                    <img
                                        src={story.cover}
                                        alt={story.title}
                                        className="w-12 h-16 object-cover rounded"
                                    />
                                    <div className="flex-1 min-w-0">
                                        <p className="font-semibold text-sm text-slate-900 dark:text-white line-clamp-2">
                                            {story.title}
                                        </p>
                                        <p className="text-xs text-slate-500 dark:text-slate-400 mt-1">
                                            {story.author}
                                        </p>
                                        <div className="flex items-center gap-3 mt-1">
                                            <span className="text-xs text-slate-500 dark:text-slate-400">
                                                👁️ {story.views}
                                            </span>
                                            <span className="text-xs text-amber-600 dark:text-amber-400">
                                                ⭐ {story.rating}
                                            </span>
                                        </div>
                                    </div>
                                </div>
                            ))}
                        </div>
                    </div>
                </div>
            </div>

            {/* Chart Area */}
            <div className="grid grid-cols-1 lg:grid-cols-2 gap-6">
                <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 p-6">
                    <h2 className="text-lg font-bold text-slate-900 dark:text-white mb-4">
                        Thống kê lượt xem
                    </h2>
                    <div className="h-64 flex items-end justify-between gap-2">
                        {[65, 45, 78, 52, 88, 45, 92, 73, 56, 84, 67, 91].map((height, index) => (
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

                <div className="bg-white dark:bg-slate-900 rounded-xl border border-slate-200 dark:border-slate-800 p-6">
                    <h2 className="text-lg font-bold text-slate-900 dark:text-white mb-4">
                        Thống kê người dùng
                    </h2>
                    <div className="h-64 flex items-center justify-center">
                        <div className="relative w-48 h-48">
                            {/* Simple pie chart representation */}
                            <div className="absolute inset-0 rounded-full" style={{
                                background: 'conic-gradient(#13ec5b 0deg 180deg, #3b82f6 180deg 270deg, #f59e0b 270deg 360deg)'
                            }}></div>
                            <div className="absolute inset-4 bg-white dark:bg-slate-900 rounded-full flex items-center justify-center">
                                <div className="text-center">
                                    <p className="text-2xl font-bold text-slate-900 dark:text-white">45K</p>
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
                            <p className="text-sm font-bold text-slate-900 dark:text-white">50%</p>
                        </div>
                        <div className="text-center">
                            <div className="flex items-center justify-center gap-2 mb-1">
                                <div className="w-3 h-3 rounded-full bg-blue-500"></div>
                                <span className="text-xs text-slate-500 dark:text-slate-400">Tác giả</span>
                            </div>
                            <p className="text-sm font-bold text-slate-900 dark:text-white">25%</p>
                        </div>
                        <div className="text-center">
                            <div className="flex items-center justify-center gap-2 mb-1">
                                <div className="w-3 h-3 rounded-full bg-amber-500"></div>
                                <span className="text-xs text-slate-500 dark:text-slate-400">VIP</span>
                            </div>
                            <p className="text-sm font-bold text-slate-900 dark:text-white">25%</p>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
