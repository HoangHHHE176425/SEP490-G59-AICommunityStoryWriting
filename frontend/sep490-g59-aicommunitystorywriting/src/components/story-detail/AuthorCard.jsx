export function AuthorCard({ author }) {
    const avatarUrl = author?.avatar;
    const displayName = author?.name ?? 'Ẩn danh';
    const followers = typeof author?.followers === 'number' ? author.followers.toLocaleString() : (author?.followers ?? '0');
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
                        {followers} người theo dõi
                    </p>
                </div>
            </div>
            <button className="w-full py-2 bg-primary/10 text-primary text-sm font-bold rounded-full hover:bg-primary/20 transition-all">
                Theo dõi tác giả
            </button>
        </div>
    );
}
