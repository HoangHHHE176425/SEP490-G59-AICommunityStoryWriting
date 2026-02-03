import { Star, ChevronLeft, ChevronRight } from 'lucide-react';

const recommendedBooks = [
    {
        id: 1,
        title: 'Tuyệt Thế Đường Môn',
        author: 'Đường Gia Tam Thiếu',
        cover: 'https://images.unsplash.com/photo-1598669266459-eef1467c15be?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxmYW50YXN5JTIwd2FycmlvciUyMGJvb2t8ZW58MXx8fHwxNzY4NDg2MzI5fDA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral'
    },
    {
        id: 2,
        title: 'Đấu Phá Thương Khung',
        author: 'Thiên Tằm Thổ Đậu',
        cover: 'https://images.unsplash.com/photo-1762554914464-1ea94ff92f49?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxtYWdpYyUyMHNwZWxsJTIwYm9va3xlbnwxfHx8fDE3Njg0ODYzMjl8MA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral'
    },
    {
        id: 3,
        title: 'Kiếm Đạo Độc Tôn',
        author: 'Kiếm Du Thái Hư',
        cover: 'https://images.unsplash.com/photo-1764768306669-d0ab6d67b00b?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhbmNpZW50JTIwc3dvcmR8ZW58MXx8fHwxNzY4NDU5ODkxfDA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral'
    },
    {
        id: 4,
        title: 'Quỷ Bí Chi Chủ',
        author: 'Ái Tiềm Thủy Đích Ô Tặc',
        cover: 'https://images.unsplash.com/photo-1610926597998-fc7f2c1b89b0?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxteXN0aWNhbCUyMGRyYWdvbnxlbnwxfHx8fDE3Njg0ODYzMzB8MA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral'
    }
];

export function RecommendedStories() {
    return (
        <section className="bg-primary/5 dark:bg-primary/10 p-6 md:p-8 rounded-2xl">
            <div className="flex items-center justify-between mb-6">
                <div className="flex items-center gap-3">
                    <Star className="w-7 h-7 text-primary" fill="currentColor" />
                    <h3 className="text-xl font-bold dark:text-white">Truyện Đề Cử</h3>
                </div>
                <div className="flex gap-2">
                    <button className="size-8 rounded-full border border-slate-300 dark:border-slate-600 flex items-center justify-center hover:bg-white dark:hover:bg-slate-700 transition-colors">
                        <ChevronLeft className="w-4 h-4" />
                    </button>
                    <button className="size-8 rounded-full border border-slate-300 dark:border-slate-600 flex items-center justify-center hover:bg-white dark:hover:bg-slate-700 transition-colors">
                        <ChevronRight className="w-4 h-4" />
                    </button>
                </div>
            </div>

            <div className="grid grid-cols-2 md:grid-cols-4 gap-6">
                {recommendedBooks.map((book) => (
                    <div key={book.id} className="flex flex-col gap-3">
                        <div className="relative aspect-[3/4] rounded-xl overflow-hidden shadow-md hover:shadow-xl transition-shadow cursor-pointer">
                            <img
                                alt={book.title}
                                className="w-full h-full object-cover hover:scale-105 transition-transform duration-300"
                                src={book.cover}
                            />
                        </div>
                        <h4 className="font-bold text-sm line-clamp-2 dark:text-white">{book.title}</h4>
                        <p className="text-xs text-slate-500 dark:text-slate-400">{book.author}</p>
                    </div>
                ))}
            </div>
        </section>
    );
}