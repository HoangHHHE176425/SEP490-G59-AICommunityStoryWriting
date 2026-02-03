export function AuthorCard({ author }) {
    return (
        <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 p-6">
            <h3 className="font-bold text-slate-900 dark:text-white mb-4">Tác giả</h3>
            <div className="flex items-center gap-3 mb-4">
                <img
                    src={author.avatar}
                    alt={author.name}
                    className="w-12 h-12 rounded-full"
                />
                <div className="flex-1">
                    <p className="font-semibold text-slate-900 dark:text-white">
                        {author.name}
                    </p>
                    <p className="text-xs text-slate-500 dark:text-slate-400">
                        {author.followers.toLocaleString()} người theo dõi
                    </p>
                </div>
            </div>
            <button className="w-full py-2 bg-primary/10 text-primary text-sm font-bold rounded-full hover:bg-primary/20 transition-all">
                Theo dõi tác giả
            </button>
        </div>
    );
}
