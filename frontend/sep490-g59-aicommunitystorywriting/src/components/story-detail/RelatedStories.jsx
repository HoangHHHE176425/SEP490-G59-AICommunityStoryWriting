import { Star, Play, ChevronLeft, ChevronRight } from 'lucide-react';
import { useRef } from 'react';

export function RelatedStories({ stories }) {
    const scrollContainerRef = useRef(null);

    const scrollLeft = () => {
        if (scrollContainerRef.current) {
            scrollContainerRef.current.scrollBy({ left: -800, behavior: 'smooth' });
        }
    };

    const scrollRight = () => {
        if (scrollContainerRef.current) {
            scrollContainerRef.current.scrollBy({ left: 800, behavior: 'smooth' });
        }
    };

    return (
        <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-6 md:p-8">
            <div className="flex items-center justify-between mb-6">
                <h2 className="text-2xl font-bold text-slate-900 dark:text-white">Truyện liên quan</h2>
                <div className="flex items-center gap-3">
                    <button
                        onClick={scrollLeft}
                        className="size-9 rounded-full border border-slate-300 dark:border-slate-600 flex items-center justify-center hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
                    >
                        <ChevronLeft className="w-5 h-5" />
                    </button>
                    <button
                        onClick={scrollRight}
                        className="size-9 rounded-full border border-slate-300 dark:border-slate-600 flex items-center justify-center hover:bg-slate-100 dark:hover:bg-slate-800 transition-colors"
                    >
                        <ChevronRight className="w-5 h-5" />
                    </button>
                </div>
            </div>

            <div className="relative overflow-hidden -mx-2">
                <div
                    ref={scrollContainerRef}
                    className="flex gap-4 md:gap-6 overflow-x-auto scroll-smooth px-2 pb-2 scrollbar-hide"
                    style={{ scrollbarWidth: 'none', msOverflowStyle: 'none' }}
                >
                    {stories.map((story) => (
                        <a
                            key={story.id}
                            href="#"
                            className="group flex flex-col gap-3 flex-shrink-0 w-[140px] sm:w-[160px] md:w-[180px]"
                        >
                            <div className="relative aspect-[2/3] rounded-lg overflow-hidden shadow-sm group-hover:shadow-xl group-hover:-translate-y-1 transition-all duration-300">
                                <img
                                    src={story.cover}
                                    alt={story.title}
                                    className="w-full h-full object-cover"
                                />
                                <div className="absolute inset-0 bg-black/40 opacity-0 group-hover:opacity-100 transition-opacity flex items-center justify-center">
                                    <button className="size-12 bg-primary text-white rounded-full flex items-center justify-center shadow-lg transform scale-90 group-hover:scale-100 transition-transform">
                                        <Play className="w-5 h-5 ml-0.5" fill="currentColor" />
                                    </button>
                                </div>
                            </div>
                            <div>
                                <h4 className="font-bold text-sm line-clamp-2 group-hover:text-primary transition-colors mb-1">
                                    {story.title}
                                </h4>
                                <p className="text-xs text-slate-500 dark:text-slate-400 mb-1 line-clamp-1">
                                    {story.author}
                                </p>
                                <div className="flex items-center gap-2">
                                    <div className="flex items-center gap-0.5">
                                        <Star className="w-3 h-3 fill-amber-400 text-amber-400" />
                                        <span className="text-xs font-semibold text-slate-900 dark:text-white">
                                            {story.rating.toFixed(1)}
                                        </span>
                                    </div>
                                    <span className="text-xs text-slate-500 dark:text-slate-400">
                                        {story.chapters} chương
                                    </span>
                                </div>
                            </div>
                        </a>
                    ))}
                </div>
            </div>
        </div>
    );
}
