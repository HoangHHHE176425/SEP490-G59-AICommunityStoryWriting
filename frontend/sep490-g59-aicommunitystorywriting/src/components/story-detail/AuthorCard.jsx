import { Link } from 'react-router-dom';

export function AuthorCard({ author }) {
    const avatarUrl = author?.avatar;
    const displayName = author?.name ?? 'Tác giả';
    const authorId = author?.id || author?.userId;
    const rawFollowers = author?.followers ?? author?.followerCount ?? author?.FollowersCount;
    const followersNum = Number(rawFollowers);
    const followersLabel =
        authorId && (rawFollowers === null || rawFollowers === undefined)
            ? '…'
            : Number.isFinite(followersNum)
              ? Math.max(0, followersNum).toLocaleString('vi-VN')
              : '0';
    return (
        <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-6">
            <h3 className="font-bold text-slate-900 dark:text-white mb-4">Tác giả</h3>
            <div className="flex items-center gap-3 mb-4">
                {avatarUrl ? (
                    <img
                        src={avatarUrl}
                        alt={displayName}
                        className="w-12 h-12 rounded-full object-cover"
                    />
                ) : (
                    <div className="w-12 h-12 rounded-full bg-primary/20 text-primary flex items-center justify-center font-bold text-lg">
                        {displayName.charAt(0).toUpperCase()}
                    </div>
                )}
                <div className="flex-1">
                    <p className="font-semibold text-slate-900 dark:text-white">
                        {displayName}
                    </p>
                    <p className="text-xs text-slate-500 dark:text-slate-400">
                        {followersLabel} người theo dõi
                    </p>
                </div>
            </div>
            {authorId ? (
                <Link
                    to={`/authors/${authorId}`}
                    className="w-full inline-flex items-center justify-center py-2 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all"
                >
                    Xem trang tác giả
                </Link>
            ) : (
                <button className="w-full py-2 bg-primary/10 text-primary text-sm font-bold rounded-full hover:bg-primary/20 transition-all">
                    Theo dõi tác giả
                </button>
            )}
        </div>
    );
}
