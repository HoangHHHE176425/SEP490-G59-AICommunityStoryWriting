import { Star, Play, Bookmark, Flag } from 'lucide-react';

export function StoryHeader({ story, isFollowing, onToggleFollow, onOpenRating, onOpenReport, onReadStory }) {
    return (
        <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 overflow-hidden">
            <div className="p-6">
                <div className="flex flex-col sm:flex-row gap-6">
                    {/* Cover Image */}
                    <div className="shrink-0">
                        <div className="w-full sm:w-48 aspect-[2/3] rounded-lg overflow-hidden shadow-lg">
                            <img
                                src={story.cover}
                                alt={story.title}
                                className="w-full h-full object-cover"
                            />
                        </div>
                    </div>

                    {/* Info */}
                    <div className="flex-1 min-w-0">
                        <h1 className="text-2xl sm:text-3xl font-bold text-slate-900 dark:text-white mb-3">
                            {story.title}
                        </h1>

                        {/* Author */}
                        <div className="mb-4">
                            <p className="text-xs text-slate-500 dark:text-slate-400 mb-1">Tác giả</p>
                            <a href="#" className="inline-block group">
                                <p className="text-sm font-semibold text-slate-900 dark:text-white group-hover:text-primary transition-colors">
                                    {story.author.name}
                                </p>
                            </a>
                        </div>

                        {/* Genres */}
                        <div className="mb-4">
                            <p className="text-xs text-slate-500 dark:text-slate-400 mb-2">Thể loại</p>
                            <div className="flex flex-wrap gap-2">
                                {story.genre.map((genre) => (
                                    <span
                                        key={genre}
                                        className="px-3 py-1 bg-primary/10 text-primary text-xs font-semibold rounded-full"
                                    >
                                        {genre}
                                    </span>
                                ))}
                                <span className="px-3 py-1 bg-amber-50 dark:bg-amber-950/30 text-amber-600 dark:text-amber-400 text-xs font-semibold rounded-full">
                                    {story.status}
                                </span>
                            </div>
                        </div>

                        {/* Rating */}
                        <div className="flex items-center gap-4 mb-4">
                            <div className="flex items-center gap-1">
                                <Star className="w-5 h-5 fill-amber-400 text-amber-400" />
                                <span className="text-lg font-bold text-slate-900 dark:text-white">{story.rating}</span>
                                <span className="text-sm text-slate-500 dark:text-slate-400">
                                    ({story.totalRatings.toLocaleString()} đánh giá)
                                </span>
                            </div>
                        </div>

                        {/* Stats */}
                        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-6">
                            <div>
                                <div className="text-slate-500 dark:text-slate-400 text-xs mb-1">
                                    Lượt xem
                                </div>
                                <p className="font-bold text-slate-900 dark:text-white">
                                    {(story.totalViews / 1000000).toFixed(1)}M
                                </p>
                            </div>
                            <div>
                                <div className="text-slate-500 dark:text-slate-400 text-xs mb-1">
                                    Bình luận
                                </div>
                                <p className="font-bold text-slate-900 dark:text-white">
                                    {(story.comments / 1000).toFixed(1)}K
                                </p>
                            </div>
                            <div>
                                <div className="text-slate-500 dark:text-slate-400 text-xs mb-1">
                                    Số chương
                                </div>
                                <p className="font-bold text-slate-900 dark:text-white">
                                    {story.chapters}
                                </p>
                            </div>
                            <div>
                                <div className="text-slate-500 dark:text-slate-400 text-xs mb-1">
                                    Cập nhật
                                </div>
                                <p className="font-bold text-slate-900 dark:text-white">
                                    {story.lastUpdate}
                                </p>
                            </div>
                        </div>

                        {/* Action Buttons */}
                        <div className="flex flex-wrap gap-3">
                            <button
                                onClick={onReadStory}
                                className="flex items-center gap-2 px-6 py-2.5 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all"
                            >
                                <Play className="w-4 h-4" />
                                Đọc truyện
                            </button>
                            <button
                                onClick={onToggleFollow}
                                className={`flex items-center gap-2 px-6 py-2.5 text-sm font-bold rounded-full transition-all ${isFollowing
                                    ? 'bg-primary/10 text-primary border-2 border-primary'
                                    : 'bg-slate-100 dark:bg-slate-800 text-slate-900 dark:text-white hover:bg-slate-200 dark:hover:bg-slate-700'
                                    }`}
                            >
                                <Bookmark className="w-4 h-4" />
                                {isFollowing ? 'Đang theo dõi' : 'Theo dõi'}
                            </button>
                            <button
                                onClick={onOpenRating}
                                className="flex items-center gap-2 px-4 py-2.5 bg-amber-50 dark:bg-amber-950/30 text-amber-600 dark:text-amber-400 text-sm font-bold rounded-full hover:bg-amber-100 dark:hover:bg-amber-900/40 transition-all"
                            >
                                <Star className="w-4 h-4" />
                                Đánh giá
                            </button>
                            <button
                                onClick={onOpenReport}
                                className="flex items-center gap-2 px-4 py-2.5 bg-red-50 dark:bg-red-950/30 text-red-600 dark:text-red-400 text-sm font-bold rounded-full hover:bg-red-100 dark:hover:bg-red-900/40 transition-all"
                            >
                                <Flag className="w-4 h-4" />
                                Báo cáo
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            {/* Description */}
            <div className="border-t border-slate-200 dark:border-slate-800 p-6">
                <h3 className="font-bold text-slate-900 dark:text-white mb-3">Giới thiệu</h3>
                <div className="text-slate-600 dark:text-slate-400 whitespace-pre-line leading-relaxed">
                    {story.description}
                </div>
            </div>
        </div>
    );
}
