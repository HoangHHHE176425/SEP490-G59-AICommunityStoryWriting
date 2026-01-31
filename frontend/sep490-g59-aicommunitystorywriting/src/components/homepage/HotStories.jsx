import { Flame, ChevronRight, Eye, MessageCircle } from 'lucide-react';

const hotBooks = [
    {
        id: 1,
        title: 'Tiên Võ Đế Tôn',
        author: 'Lục Đạo Tam Sinh',
        chapter: 'Chương 6789',
        views: '125M',
        comments: '45K',
        cover: 'https://images.unsplash.com/photo-1598669266459-eef1467c15be?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxmYW50YXN5JTIwd2FycmlvciUyMGJvb2t8ZW58MXx8fHwxNzY4NDg2MzI5fDA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral'
    },
    {
        id: 2,
        title: 'Vạn Cổ Đệ Nhất Thần',
        author: 'Phong Thanh Dương',
        chapter: 'Chương 5432',
        views: '98M',
        comments: '38K',
        cover: 'https://images.unsplash.com/photo-1762554914464-1ea94ff92f49?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxtYWdpYyUyMHNwZWxsJTIwYm9va3xlbnwxfHx8fDE3Njg0ODYzMjl8MA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral'
    },
    {
        id: 3,
        title: 'Long Vương Truyền Thuyết',
        author: 'Đường Gia Tam Thiếu',
        chapter: 'Chương 4567',
        views: '87M',
        comments: '32K',
        cover: 'https://images.unsplash.com/photo-1764768306669-d0ab6d67b00b?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhbmNpZW50JTIwc3dvcmR8ZW58MXx8fHwxNzY4NDU5ODkxfDA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral'
    },
    {
        id: 4,
        title: 'Vũ Động Càn Khôn',
        author: 'Thiên Tằm Thổ Đậu',
        chapter: 'Chương 3890',
        views: '76M',
        comments: '28K',
        cover: 'https://images.unsplash.com/photo-1610926597998-fc7f2c1b89b0?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxteXN0aWNhbCUyMGRyYWdvbnxlbnwxfHx8fDE3Njg0ODYzMzB8MA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral'
    },
    {
        id: 5,
        title: 'Nguyên Tôn',
        author: 'Thiên Tằm Thổ Đậu',
        chapter: 'Chương 2987',
        views: '65M',
        comments: '25K',
        cover: 'https://images.unsplash.com/photo-1500245804862-0692ee1bbee8?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxyb21hbnRpYyUyMHN1bnNldHxlbnwxfHx8fDE3Njg0NTI0MDh8MA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral'
    },
    {
        id: 6,
        title: 'Huyết Ma Nhân Gian',
        author: 'Mộng Nhập Thần Cơ',
        chapter: 'Chương 2345',
        views: '54M',
        comments: '21K',
        cover: 'https://images.unsplash.com/photo-1633901605644-e7f62844a460?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxteXN0ZXJpb3VzJTIwZm9yZXN0fGVufDF8fHx8MTc2ODQ4NjMzMHww&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral'
    }
];

export function HotStories() {
    return (
        <section>
            <div className="flex items-center justify-between mb-6 px-2 border-l-4 border-primary">
                <div className="flex items-center gap-3">
                    <Flame className="w-6 h-6 text-orange-500" />
                    <h3 className="text-xl font-bold dark:text-white">Truyện Hot Nhất</h3>
                </div>
                <a className="text-sm font-semibold text-primary hover:underline flex items-center gap-1" href="#">
                    Xem tất cả <ChevronRight className="w-4 h-4" />
                </a>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-3 gap-4 md:gap-6">
                {hotBooks.map((book) => (
                    <div key={book.id} className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-100 dark:border-slate-800 overflow-hidden hover:shadow-lg transition-shadow group cursor-pointer">
                        <div className="flex gap-4 p-4">
                            <div className="relative w-20 h-28 rounded-lg overflow-hidden shrink-0">
                                <img
                                    alt={book.title}
                                    className="w-full h-full object-cover group-hover:scale-110 transition-transform duration-300"
                                    src={book.cover}
                                />
                            </div>
                            <div className="flex-1 flex flex-col justify-between min-w-0">
                                <div>
                                    <h4 className="font-bold text-sm line-clamp-2 group-hover:text-primary transition-colors mb-1">
                                        {book.title}
                                    </h4>
                                    <p className="text-xs text-slate-500 dark:text-slate-400 mb-2">{book.author}</p>
                                    <span className="text-[10px] font-bold text-primary bg-primary/10 px-1.5 py-0.5 rounded inline-block">
                                        {book.chapter}
                                    </span>
                                </div>
                                <div className="flex items-center gap-3 text-xs text-slate-500 dark:text-slate-400 mt-2">
                                    <span className="flex items-center gap-1">
                                        <Eye className="w-3 h-3" />
                                        {book.views}
                                    </span>
                                    <span className="flex items-center gap-1">
                                        <MessageCircle className="w-3 h-3" />
                                        {book.comments}
                                    </span>
                                </div>
                            </div>
                        </div>
                    </div>
                ))}
            </div>
        </section>
    );
}