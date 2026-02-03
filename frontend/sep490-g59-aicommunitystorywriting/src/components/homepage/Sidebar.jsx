import { Eye, MessageSquare, ArrowRight } from 'lucide-react';

const rankings = [
    {
        id: 1,
        rank: 1,
        title: 'Tiên Võ Đế Tôn',
        views: '24.5k lượt xem',
        cover: 'https://images.unsplash.com/photo-1598669266459-eef1467c15be?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxmYW50YXN5JTIwd2FycmlvciUyMGJvb2t8ZW58MXx8fHwxNzY4NDg2MzI5fDA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral',
        isTop: true
    },
    {
        id: 2,
        rank: 2,
        title: 'Vạn Cổ Đệ Nhất Thần',
        views: '18.2k lượt xem',
        cover: 'https://images.unsplash.com/photo-1762554914464-1ea94ff92f49?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxtYWdpYyUyMHNwZWxsJTIwYm9va3xlbnwxfHx8fDE3Njg0ODYzMjl8MA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral',
        isTop: false
    },
    {
        id: 3,
        rank: 3,
        title: 'Đại Chúa Tể',
        views: '15.9k lượt xem',
        cover: 'https://images.unsplash.com/photo-1764768306669-d0ab6d67b00b?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhbmNpZW50JTIwc3dvcmR8ZW58MXx8fHwxNzY4NDU5ODkxfDA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral',
        isTop: false
    },
    {
        id: 4,
        rank: 4,
        title: 'Long Vương Truyền Thuyết',
        views: '12.7k lượt xem',
        cover: 'https://images.unsplash.com/photo-1610926597998-fc7f2c1b89b0?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxteXN0aWNhbCUyMGRyYWdvbnxlbnwxfHx8fDE3Njg0ODYzMzB8MA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral',
        isTop: false
    },
    {
        id: 5,
        rank: 5,
        title: 'Vũ Động Càn Khôn',
        views: '11.3k lượt xem',
        cover: 'https://images.unsplash.com/photo-1500245804862-0692ee1bbee8?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxyb21hbnRpYyUyMHN1bnNldHxlbnwxfHx8fDE3Njg0NTI0MDh8MA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral',
        isTop: false
    }
];

const authors = [
    {
        id: 1,
        name: 'Vong Ngữ',
        stats: '1.2M lượt đọc • 45 truyện',
        avatar: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=100&h=100&fit=crop'
    },
    {
        id: 2,
        name: 'Tiêu Đỉnh',
        stats: '950k lượt đọc • 12 truyện',
        avatar: 'https://images.unsplash.com/photo-1599566150163-29194dcaad36?w=100&h=100&fit=crop'
    },
    {
        id: 3,
        name: 'Thiên Tằm Thổ Đậu',
        stats: '2.5M lượt đọc • 28 truyện',
        avatar: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=100&h=100&fit=crop'
    }
];

export function Sidebar() {
    return (
        <div className="flex flex-col gap-8 sticky top-20">
            {/* Bảng Xếp Hạng */}
            <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-sm border border-slate-100 dark:border-slate-800 p-6">
                <div className="flex items-center justify-between mb-6">
                    <h3 className="text-lg font-bold dark:text-white">Bảng Xếp Hạng</h3>
                    <div className="flex gap-4 text-xs font-bold text-slate-400">
                        <button className="text-primary underline underline-offset-4 decoration-2">Ngày</button>
                        <button className="hover:text-slate-600 dark:hover:text-slate-300">Tuần</button>
                        <button className="hover:text-slate-600 dark:hover:text-slate-300">Tháng</button>
                    </div>
                </div>

                <div className="flex flex-col gap-4">
                    {rankings.map((item) => (
                        <div key={item.id} className="flex items-center gap-4 group cursor-pointer">
                            <span className={`text-2xl font-black italic w-8 text-center ${item.isTop
                                ? 'text-primary/70 group-hover:text-primary'
                                : 'text-slate-200 dark:text-slate-700'
                                } transition-colors`}>
                                {item.rank.toString().padStart(2, '0')}
                            </span>
                            <div className="size-14 rounded-lg overflow-hidden shrink-0">
                                <img
                                    alt={item.title}
                                    className="w-full h-full object-cover"
                                    src={item.cover}
                                />
                            </div>
                            <div className="flex-1 min-w-0">
                                <h5 className="font-bold text-sm line-clamp-1 dark:text-white group-hover:text-primary transition-colors">
                                    {item.title}
                                </h5>
                                <p className="text-xs text-slate-500 flex items-center gap-1">
                                    <Eye className="w-3 h-3" /> {item.views}
                                </p>
                            </div>
                        </div>
                    ))}
                </div>

                <button className="w-full mt-6 py-2 text-sm font-bold text-slate-600 dark:text-slate-400 bg-slate-50 dark:bg-slate-800 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-700 transition-colors">
                    Xem toàn bộ BXH
                </button>
            </div>

            {/* Tác Giả Nổi Bật */}
            <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-sm border border-slate-100 dark:border-slate-800 p-6">
                <h3 className="text-lg font-bold mb-6 dark:text-white">Tác Giả Nổi Bật</h3>
                <div className="flex flex-col gap-5">
                    {authors.map((author) => (
                        <div key={author.id} className="flex items-center gap-4">
                            <div className="size-10 rounded-full bg-primary/20 flex items-center justify-center overflow-hidden border-2 border-primary/50">
                                <img
                                    alt={author.name}
                                    className="w-full h-full object-cover"
                                    src={author.avatar}
                                />
                            </div>
                            <div className="flex-1 min-w-0">
                                <p className="text-sm font-bold dark:text-white truncate">{author.name}</p>
                                <p className="text-[11px] text-slate-500 truncate">{author.stats}</p>
                            </div>
                            <button className="text-primary font-bold text-xs hover:underline">Theo dõi</button>
                        </div>
                    ))}
                </div>
            </div>

            {/* Thông báo cộng đồng */}
            <div className="bg-slate-900 text-white rounded-2xl p-6 relative overflow-hidden">
                <div className="absolute -right-4 -bottom-4 size-24 bg-primary/20 rounded-full blur-3xl"></div>
                <div className="relative z-10">
                    <div className="flex items-center gap-2 mb-4 text-primary">
                        <MessageSquare className="w-5 h-5" />
                        <h3 className="text-sm font-bold uppercase tracking-widest">Cộng đồng</h3>
                    </div>
                    <p className="text-sm font-semibold mb-2">
                        Cuộc thi sáng tác "Hào Khí Việt Nam" chính thức khởi động!
                    </p>
                    <p className="text-xs text-slate-400 mb-4">
                        Tham gia ngay để nhận tổng giải thưởng lên đến 50 triệu đồng.
                    </p>
                    <a className="inline-flex items-center text-xs font-bold text-primary group" href="#">
                        Tìm hiểu ngay
                        <ArrowRight className="w-4 h-4 ml-1 group-hover:translate-x-1 transition-transform" />
                    </a>
                </div>
            </div>
        </div>
    );
}