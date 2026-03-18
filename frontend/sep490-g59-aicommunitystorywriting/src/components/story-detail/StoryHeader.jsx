import { Star, Play, Bookmark, Flag } from 'lucide-react';

/** Format số để hiển thị: 1234 -> 1.2K, 1234567 -> 1.2M, nhỏ thì hiển thị nguyên. */
function formatStatNumber(n) {
    const num = Number(n) || 0;
    if (num >= 1_000_000) return `${(num / 1_000_000).toFixed(1)}M`;
    if (num >= 1_000) return `${(num / 1_000).toFixed(1)}K`;
    return num.toLocaleString();
}

export default function StoryHeader({ story, isFollowing, onToggleFollow, onOpenRating, hasUserRated = false, userRatingStars = null, onOpenReport, onReadStory }) {
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

                        {/* Meta lines: simple "label: value" style */}
                        <div className="mb-4 space-y-2 text-sm text-slate-700 dark:text-slate-300">
                            <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                                <span className="text-slate-500 dark:text-slate-400">Tác giả:</span>
                                <span className="font-semibold text-slate-900 dark:text-white">{story.author?.name ?? 'Ẩn danh'}</span>
                            </div>
                            <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                                <span className="text-slate-500 dark:text-slate-400">Thể loại:</span>
                                <div className="flex flex-wrap gap-2">
                                    {(story.genre ?? []).filter(Boolean).length > 0 ? (
                                        (story.genre ?? []).filter(Boolean).map((g) => (
                                            <span
                                                key={g}
                                                className="px-2.5 py-1 rounded-full text-xs font-semibold bg-primary/10 text-primary border border-primary/15"
                                            >
                                                {g}
                                            </span>
                                        ))
                                    ) : (
                                        <span className="px-2.5 py-1 rounded-full text-xs font-semibold bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 border border-slate-200 dark:border-slate-700">
                                            Chưa phân loại
                                        </span>
                                    )}
                                </div>
                            </div>
                            <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
                                <span className="text-slate-500 dark:text-slate-400">Trạng thái truyện:</span>
                                <span className="px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-50 dark:bg-emerald-950/30 text-emerald-700 dark:text-emerald-300 border border-emerald-200/60 dark:border-emerald-900/50">
                                    {story.storyProgressLabel ?? 'Đang ra'}
                                </span>
                            </div>
                        </div>

                        {/* Rating */}
                        <div className="flex items-center gap-4 mb-4">
                            <div className="flex items-center gap-1">
                                <Star className="w-5 h-5 fill-amber-400 text-amber-400" />
                                <span className="text-lg font-bold text-slate-900 dark:text-white">{story.rating}</span>
                                <span className="text-sm text-slate-500 dark:text-slate-400">
                                    ({Number(story.totalRatings ?? 0).toLocaleString()} đánh giá)
                                </span>
                            </div>
                        </div>

                        {/* Stats - số liệu từ API: totalViews, comments */}
                        <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-6">
                            <div>
                                <div className="text-slate-500 dark:text-slate-400 text-xs mb-1">
                                    Lượt xem
                                </div>
                                <p className="font-bold text-slate-900 dark:text-white">
                                    {formatStatNumber(story.totalViews ?? story.views ?? 0)}
                                </p>
                            </div>
                            <div>
                                <div className="text-slate-500 dark:text-slate-400 text-xs mb-1">
                                    Bình luận
                                </div>
                                <p className="font-bold text-slate-900 dark:text-white">
                                    {formatStatNumber(story.comments ?? 0)}
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
                            {hasUserRated ? (
                                <span
                                    className="flex items-center gap-2 px-4 py-2.5 bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-400 text-sm font-bold rounded-full cursor-default"
                                    title="Mỗi tài khoản chỉ được đánh giá một lần"
                                >
                                    <Star className="w-4 h-4 fill-amber-400 text-amber-400" />
                                    Bạn đã đánh giá{userRatingStars != null && userRatingStars > 0 ? ` (${userRatingStars} sao)` : ''}
                                </span>
                            ) : (
                                <button
                                    onClick={onOpenRating}
                                    className="flex items-center gap-2 px-4 py-2.5 bg-amber-50 dark:bg-amber-950/30 text-amber-600 dark:text-amber-400 text-sm font-bold rounded-full hover:bg-amber-100 dark:hover:bg-amber-900/40 transition-all"
                                >
                                    <Star className="w-4 h-4" />
                                    Đánh giá
                                </button>
                            )}
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
