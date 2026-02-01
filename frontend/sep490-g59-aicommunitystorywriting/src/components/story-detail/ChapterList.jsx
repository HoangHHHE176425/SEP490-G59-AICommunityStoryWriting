import { BookOpen, Clock, Eye, ChevronRight } from 'lucide-react';

export function ChapterList({ chapters }) {
    return (
        <div className="space-y-2">
            {chapters.map((chapter) => (
                <a
                    key={chapter.id}
                    href="#"
                    className="flex items-center justify-between p-4 rounded-lg hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors group"
                >
                    <div className="flex items-center gap-3 flex-1 min-w-0">
                        {chapter.isLocked ? (
                            <div className="w-6 h-6 rounded bg-amber-100 dark:bg-amber-950/30 flex items-center justify-center shrink-0">
                                <span className="text-xs text-amber-600 dark:text-amber-400">🔒</span>
                            </div>
                        ) : (
                            <div className="w-6 h-6 rounded bg-primary/10 flex items-center justify-center shrink-0">
                                <BookOpen className="w-4 h-4 text-primary" />
                            </div>
                        )}
                        <div className="flex-1 min-w-0">
                            <div className="flex items-center gap-2">
                                <p className="font-semibold text-slate-900 dark:text-white group-hover:text-primary transition-colors truncate">
                                    Chương {chapter.id}: {chapter.title}
                                </p>
                                {chapter.isNew && (
                                    <span className="px-2 py-0.5 bg-red-500 text-white text-xs font-bold rounded shrink-0">
                                        MỚI
                                    </span>
                                )}
                            </div>
                            <div className="flex items-center gap-3 text-xs text-slate-500 dark:text-slate-400 mt-1">
                                <span className="flex items-center gap-1">
                                    <Clock className="w-3 h-3" />
                                    {chapter.time}
                                </span>
                                <span className="flex items-center gap-1">
                                    <Eye className="w-3 h-3" />
                                    {chapter.views.toLocaleString()}
                                </span>
                            </div>
                        </div>
                    </div>
                    <ChevronRight className="w-5 h-5 text-slate-400 group-hover:text-primary transition-colors shrink-0" />
                </a>
            ))}
        </div>
    );
}
