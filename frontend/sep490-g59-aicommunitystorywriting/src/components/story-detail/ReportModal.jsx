import { X, Flag } from 'lucide-react';
import { useState } from 'react';

const REASON_OPTIONS = [
    { value: 'spam', label: 'Spam' },
    { value: 'abusive', label: 'Ngôn từ xúc phạm' },
    { value: 'inappropriate', label: 'Nội dung không phù hợp' },
    { value: 'copyright', label: 'Vi phạm bản quyền' },
    { value: 'violence', label: 'Bạo lực' },
    { value: 'sexual', label: 'Nội dung 18+' },
    { value: 'other', label: 'Khác' },
];

/**
 * Chuẩn payload báo cáo khớp với màn Quản lý vi phạm:
 * { reason, description, evidence, evidenceLinks[], evidenceImages[], storyId?, storyTitle?, chapterId?, chapterTitle?, targetType, targetId? }
 */
export function ReportModal({
    isOpen,
    onClose,
    onSubmit,
    title,
    type,
    storyId = null,
    storyTitle = null,
    chapterId = null,
    chapterTitle = null,
    targetId = null,
    reasonOptions = [],
    submitting = false,
    errorMessage = null,
}) {
    const [reportReason, setReportReason] = useState('');
    const [reportDetails, setReportDetails] = useState('');

    if (!isOpen) return null;

    const normalizedReasonOptions = Array.isArray(reasonOptions) && reasonOptions.length > 0
        ? reasonOptions
        : REASON_OPTIONS;

    const handleSubmit = async () => {
        if (!reportReason) return;

        const reasonLabel = REASON_OPTIONS.find((o) => o.value === reportReason)?.label ?? reportReason;
        const payload = {
            reason: reasonLabel,
            reasonCode: reportReason,
            description: reportDetails,
            storyId: storyId ?? undefined,
            storyTitle: storyTitle ?? undefined,
            chapterId: chapterId ?? undefined,
            chapterTitle: chapterTitle ?? undefined,
            targetType: type,
            targetId: targetId ?? undefined,
        };
        const ok = await onSubmit(payload);
        if (ok === false) return;
        setReportReason('');
        setReportDetails('');
        onClose();
    };

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/55 backdrop-blur-sm">
            <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-2xl max-w-2xl w-full border border-slate-200 dark:border-slate-700 overflow-hidden flex flex-col">
                <div className="flex items-center justify-between px-6 py-4 border-b border-slate-200 dark:border-slate-700 shrink-0">
                    <h3 className="text-2xl font-bold text-slate-900 dark:text-white flex items-center gap-2">
                        <span className="inline-flex items-center justify-center w-8 h-8 rounded-full bg-red-100 dark:bg-red-900/30">
                            <Flag className="w-4 h-4 text-red-600 dark:text-red-400" />
                        </span>
                        {title}
                    </h3>
                    <button
                        type="button"
                        onClick={onClose}
                        className="p-1.5 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition-colors"
                    >
                        <X className="w-6 h-6 text-slate-500 dark:text-slate-300" />
                    </button>
                </div>

                <div className="px-6 py-5 overflow-y-auto">
                    {type === 'story' && (
                        <div className="mb-4 text-slate-500 dark:text-slate-300 text-base">{storyTitle || 'Truyện'}</div>
                    )}
                    <div className="mb-4">
                        <label className="block text-base font-semibold text-slate-800 dark:text-slate-100 mb-2">
                            Lý do
                        </label>
                        <select
                            value={reportReason}
                            onChange={(e) => setReportReason(e.target.value)}
                            className="w-full h-11 px-3 bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-600 rounded-lg text-sm text-slate-900 dark:text-slate-100 outline-none focus:border-red-400 dark:focus:border-red-500 focus:ring-2 focus:ring-red-300/60 dark:focus:ring-red-500/30"
                        >
                            <option value="">Chọn lý do</option>
                            {normalizedReasonOptions.map((opt) => (
                                <option key={opt.value} value={opt.value}>{opt.label}</option>
                            ))}
                        </select>
                    </div>

                    {errorMessage ? (
                        <div className="mb-4 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-900/20 dark:text-red-300">
                            {errorMessage}
                        </div>
                    ) : null}

                    <div className="mb-4">
                        <label className="block text-base font-semibold text-slate-800 dark:text-slate-100 mb-2">
                            Mô tả thêm (tùy chọn)
                        </label>
                        <textarea
                            value={reportDetails}
                            onChange={(e) => setReportDetails(e.target.value)}
                            className="w-full p-3 bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-600 rounded-lg text-sm text-slate-900 dark:text-slate-100 outline-none focus:border-red-400 dark:focus:border-red-500 focus:ring-2 focus:ring-red-300/60 dark:focus:ring-red-500/30 resize-none"
                            rows={4}
                            placeholder="Nhập mô tả ngắn gọn (nếu có)..."
                        />
                    </div>
                </div>

                <div className="border-t border-slate-200 dark:border-slate-700 px-6 py-4 flex justify-end gap-3">
                    <button
                        type="button"
                        onClick={onClose}
                        className="min-w-28 px-4 py-2.5 bg-slate-100 dark:bg-slate-700 text-slate-800 dark:text-slate-100 text-sm font-semibold rounded-full hover:bg-slate-200 dark:hover:bg-slate-600 transition-colors"
                    >
                        Hủy
                    </button>
                    <button
                        type="button"
                        onClick={handleSubmit}
                        disabled={!reportReason || submitting}
                        className="min-w-32 px-5 py-2.5 bg-red-600 text-white text-sm font-bold rounded-full shadow-sm hover:bg-red-700 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                        {submitting ? 'Đang gửi...' : 'Gửi báo cáo'}
                    </button>
                </div>
            </div>
        </div>
    );
}
