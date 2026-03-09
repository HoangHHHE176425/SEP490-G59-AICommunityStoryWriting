import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { BookOpen, Clock, Eye, ChevronRight, Lock, X } from 'lucide-react';

export function ChapterList({ chapters, storyId }) {
    const navigate = useNavigate();
    const [paymentModalChapter, setPaymentModalChapter] = useState(null);

    const handleLockedClick = (e, chapter) => {
        e.preventDefault();
        e.stopPropagation();
        setPaymentModalChapter(chapter);
    };

    const handleGoToChapterPayment = () => {
        if (!paymentModalChapter?.chapterId || !storyId) return;
        setPaymentModalChapter(null);
        navigate(`/chapter?storyId=${encodeURIComponent(storyId)}&chapterId=${encodeURIComponent(paymentModalChapter.chapterId)}`);
    };

    return (
        <>
            <div className="space-y-2">
                {chapters.map((chapter) => {
                    const to = storyId && chapter.chapterId
                        ? `/chapter?storyId=${encodeURIComponent(storyId)}&chapterId=${encodeURIComponent(chapter.chapterId)}`
                        : '#';
                    const isLocked = chapter.isLocked === true;
                    const isLink = !!to && to !== '#' && !isLocked;
                    const Wrapper = isLink ? Link : 'div';
                    const wrapperProps = isLink ? { to } : {};
                    return (
                        <Wrapper
                            key={chapter.id}
                            {...wrapperProps}
                            onClick={isLocked ? (e) => handleLockedClick(e, chapter) : undefined}
                            className="flex items-center justify-between p-4 rounded-lg hover:bg-slate-50 dark:hover:bg-slate-800 transition-colors group cursor-pointer"
                        >
                            <div className="flex items-center gap-3 flex-1 min-w-0">
                                {chapter.isLocked ? (
                                    <div className="w-6 h-6 rounded bg-amber-100 dark:bg-amber-950/30 flex items-center justify-center shrink-0">
                                        <Lock className="w-4 h-4 text-amber-600 dark:text-amber-400" />
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
                                        {chapter.isLocked && (chapter.coinPrice ?? 0) > 0 && (
                                            <span className="px-2 py-0.5 bg-amber-100 dark:bg-amber-900/40 text-amber-700 dark:text-amber-300 text-xs font-semibold rounded shrink-0">
                                                {chapter.coinPrice} xu
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
                        </Wrapper>
                    );
                })}
            </div>

            {/* Payment modal for locked (PAID) chapter */}
            {paymentModalChapter && (
                <>
                    <div
                        className="fixed inset-0 bg-black/50 z-40"
                        onClick={() => setPaymentModalChapter(null)}
                        aria-hidden="true"
                    />
                    <div className="fixed left-1/2 top-1/2 -translate-x-1/2 -translate-y-1/2 z-50 w-full max-w-md rounded-xl bg-white dark:bg-slate-900 shadow-xl border border-slate-200 dark:border-slate-700 p-6">
                        <div className="flex items-center justify-between mb-4">
                            <div className="flex items-center gap-2 text-amber-600 dark:text-amber-400">
                                <Lock className="w-5 h-5" />
                                <span className="font-semibold text-slate-900 dark:text-white">Chương trả phí</span>
                            </div>
                            <button
                                type="button"
                                onClick={() => setPaymentModalChapter(null)}
                                className="p-1 rounded-lg hover:bg-slate-100 dark:hover:bg-slate-800 text-slate-500"
                            >
                                <X className="w-5 h-5" />
                            </button>
                        </div>
                        <p className="text-slate-600 dark:text-slate-400 text-sm mb-2">
                            Chương {paymentModalChapter.id}: {paymentModalChapter.title}
                        </p>
                        <p className="text-slate-500 dark:text-slate-500 text-sm mb-4">
                            Để đọc chương này, bạn cần thanh toán theo giá tác giả đã đặt.
                        </p>
                        <div className="flex items-center justify-between rounded-lg bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 p-4 mb-6">
                            <span className="text-slate-600 dark:text-slate-400 font-medium">Giá:</span>
                            <span className="text-xl font-bold text-amber-600 dark:text-amber-400">
                                {paymentModalChapter.coinPrice ?? 0} xu
                            </span>
                        </div>
                        <div className="flex gap-3">
                            <button
                                type="button"
                                onClick={() => setPaymentModalChapter(null)}
                                className="flex-1 py-2.5 px-4 rounded-lg border border-slate-300 dark:border-slate-600 text-slate-700 dark:text-slate-300 font-medium hover:bg-slate-50 dark:hover:bg-slate-800"
                            >
                                Đóng
                            </button>
                            <button
                                type="button"
                                onClick={handleGoToChapterPayment}
                                className="flex-1 py-2.5 px-4 rounded-lg bg-amber-500 hover:bg-amber-600 text-white font-semibold"
                            >
                                Thanh toán {paymentModalChapter.coinPrice ?? 0} xu
                            </button>
                        </div>
                    </div>
                </>
            )}
        </>
    );
}
