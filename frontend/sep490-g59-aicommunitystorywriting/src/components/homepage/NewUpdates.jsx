import { ChevronRight, Play } from 'lucide-react';

const books = [
    {
        id: 1,
        title: 'Võ Luyện Điên Phong',
        author: 'Mạc Mặc',
        chapter: 'Chương 5420',
        cover: 'https://images.unsplash.com/photo-1598669266459-eef1467c15be?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxmYW50YXN5JTIwd2FycmlvciUyMGJvb2t8ZW58MXx8fHwxNzY4NDg2MzI5fDA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral',
        badge: 'HOT',
        badgeColor: 'bg-red-500'
    },
    {
        id: 2,
        title: 'Thần Đạo Đan Tôn',
        author: 'Cô Độc Bại Thân',
        chapter: 'Chương 128',
        cover: 'https://images.unsplash.com/photo-1762554914464-1ea94ff92f49?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxtYWdpYyUyMHNwZWxsJTIwYm9va3xlbnwxfHx8fDE3Njg0ODYzMjl8MA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral',
        badge: 'NEW',
        badgeColor: 'bg-blue-500'
    },
    {
        id: 3,
        title: 'Linh Vũ Thiên Hạ',
        author: 'Vũ Phong',
        chapter: 'Chương 3401',
        cover: 'https://images.unsplash.com/photo-1764768306669-d0ab6d67b00b?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhbmNpZW50JTIwc3dvcmR8ZW58MXx8fHwxNzY4NDU5ODkxfDA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral',
        badge: null,
        badgeColor: ''
    },
    {
        id: 4,
        title: 'Phàm Nhân Tu Tiên',
        author: 'Vong Ngữ',
        chapter: 'Chương 2451',
        cover: 'https://images.unsplash.com/photo-1610926597998-fc7f2c1b89b0?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxteXN0aWNhbCUyMGRyYWdvbnxlbnwxfHx8fDE3Njg0ODYzMzB8MA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral',
        badge: null,
        badgeColor: ''
    },
    {
        id: 5,
        title: 'Đại Chúa Tể',
        author: 'Thiên Tàm Thổ Đậu',
        chapter: 'Chương 1876',
        cover: 'https://images.unsplash.com/photo-1500245804862-0692ee1bbee8?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxyb21hbnRpYyUyMHN1bnNldHxlbnwxfHx8fDE3Njg0NTI0MDh8MA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral',
        badge: 'HOT',
        badgeColor: 'bg-red-500'
    },
    {
        id: 6,
        title: 'Bí Ẩn Thế Giới',
        author: 'Minh Trinh',
        chapter: 'Chương 987',
        cover: 'https://images.unsplash.com/photo-1633901605644-e7f62844a460?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxteXN0ZXJpb3VzJTIwZm9yZXN0fGVufDF8fHx8MTc2ODQ4NjMzMHww&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral',
        badge: 'NEW',
        badgeColor: 'bg-blue-500'
    },
    {
        id: 7,
        title: 'Kiếm Đạo Độc Tôn',
        author: 'Kiếm Du Thái Hư',
        chapter: 'Chương 4521',
        cover: 'https://images.unsplash.com/photo-1598669266459-eef1467c15be?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxmYW50YXN5JTIwd2FycmlvciUyMGJvb2t8ZW58MXx8fHwxNzY4NDg2MzI5fDA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral',
        badge: null,
        badgeColor: ''
    },
    {
        id: 8,
        title: 'Vạn Cổ Đệ Nhất Thần',
        author: 'Phong Thanh Dương',
        chapter: 'Chương 3245',
        cover: 'https://images.unsplash.com/photo-1762554914464-1ea94ff92f49?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxtYWdpYyUyMHNwZWxsJTIwYm9va3xlbnwxfHx8fDE3Njg0ODYzMjl8MA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral',
        badge: null,
        badgeColor: ''
    }
];

export function NewUpdates() {
    return (
        <section>
            <div className="flex items-center justify-between mb-6 px-2 border-l-4 border-primary">
                <h3 className="text-xl font-bold dark:text-white">Truyện Mới Cập Nhật</h3>
                <a className="text-sm font-semibold text-primary hover:underline flex items-center gap-1" href="#">
                    Xem tất cả <ChevronRight className="w-4 h-4" />
                </a>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-4 md:gap-6">
                {books.map((book) => (
                    <div key={book.id} className="group flex flex-col gap-3">
                        <div className="relative aspect-[3/4] rounded-xl overflow-hidden shadow-sm group-hover:shadow-xl group-hover:-translate-y-1 transition-all duration-300">
                            <img
                                alt={book.title}
                                className="w-full h-full object-cover"
                                src={book.cover}
                            />
                            {book.badge && (
                                <div className={`absolute top-2 right-2 ${book.badgeColor} text-white text-[10px] font-bold px-2 py-0.5 rounded shadow-lg`}>
                                    {book.badge}
                                </div>
                            )}
                            <div className="absolute inset-0 bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center">
                                <button className="size-12 bg-primary text-white rounded-full flex items-center justify-center shadow-lg transform scale-90 group-hover:scale-100 transition-transform">
                                    <Play className="w-5 h-5 ml-0.5" fill="currentColor" />
                                </button>
                            </div>
                        </div>
                        <div>
                            <h4 className="font-bold text-sm md:text-base line-clamp-1 group-hover:text-primary transition-colors">
                                {book.title}
                            </h4>
                            <p className="text-xs text-slate-500 dark:text-slate-400">{book.author}</p>
                            <div className="mt-1 flex items-center gap-2">
                                <span className="text-[10px] font-bold text-primary bg-primary/10 px-1.5 py-0.5 rounded">
                                    {book.chapter}
                                </span>
                            </div>
                        </div>
                    </div>
                ))}
            </div>
        </section>
    );
}
