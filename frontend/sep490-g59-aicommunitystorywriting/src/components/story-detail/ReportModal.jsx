import { X, Flag } from 'lucide-react';
import { useState } from 'react';
import {
    MODERATOR_ESCALATION_REASON_MIN_WORDS,
    countModeratorEscalationReasonWords,
} from '../../utils/moderatorReviewSla';

const REASON_OPTIONS = [
    { value: 'COPYRIGHT', label: 'Xâm phạm bản quyền nội dung' },
    { value: 'SEXUAL_EXPLICIT', label: 'Nội dung khiêu dâm / 18+' },
    { value: 'VIOLENCE_THREATS', label: 'Nội dung bạo lực hoặc đe dọa' },
    { value: 'HATE_SPEECH', label: 'Ngôn từ thù ghét / phân biệt đối xử' },
    { value: 'HARASSMENT', label: 'Quấy rối hoặc bắt nạt' },
    { value: 'MISINFORMATION', label: 'Thông tin sai sự thật / gây hiểu lầm' },
    { value: 'SPAM_AD', label: 'Spam / quảng cáo trái phép' },
    { value: 'OTHER', label: 'Lý do khác' },
];

const REASON_SEVERITY_ORDER = {
    copyright: 100,
    sexual: 95,
    sexual_explicit: 95,
    'sexual-content': 95,
    'adult-content': 95,
    violence: 90,
    threat: 88,
    'hate-speech': 85,
    hate_speech: 85,
    hateful: 85,
    harassment: 80,
    bullying: 80,
    abusive: 75,
    misinformation: 70,
    false_information: 70,
    spam: 60,
    spam_ad: 60,
    advertisement: 55,
    ads: 55,
    other: 10,
};

function getReasonSeverity(option) {
    const value = String(option?.value ?? '').trim().toLowerCase();
    const label = String(option?.label ?? '').trim().toLowerCase();

    if (REASON_SEVERITY_ORDER[value] != null) return REASON_SEVERITY_ORDER[value];

    if (label.includes('bản quyền')) return 100;
    if (label.includes('tình dục') || label.includes('18+')) return 95;
    if (label.includes('bạo lực') || label.includes('đe dọa')) return 90;
    if (label.includes('thù ghét') || label.includes('phân biệt')) return 85;
    if (label.includes('quấy rối') || label.includes('bắt nạt')) return 80;
    if (label.includes('xúc phạm')) return 75;
    if (label.includes('thông tin sai')) return 70;
    if (label.includes('spam') || label.includes('quảng cáo')) return 60;
    if (label.includes('khác')) return 10;

    return 50;
}

function sortReasonOptionsBySeverity(options) {
    return [...options].sort((a, b) => {
        const sa = getReasonSeverity(a);
        const sb = getReasonSeverity(b);
        if (sa !== sb) return sb - sa;
        return String(a?.label ?? '').localeCompare(String(b?.label ?? ''), 'vi');
    });
}

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
    onClearError = null,
}) {
    const [reportReason, setReportReason] = useState('');
    const [reportDetails, setReportDetails] = useState('');

    if (!isOpen) return null;

    const descriptionWordCount = countModeratorEscalationReasonWords(reportDetails);
    const descriptionMeetsMinWords = descriptionWordCount >= MODERATOR_ESCALATION_REASON_MIN_WORDS;
    const descriptionIncomplete =
        reportDetails.trim().length > 0 && !descriptionMeetsMinWords;

    const normalizedReasonOptions = Array.isArray(reasonOptions) && reasonOptions.length > 0
        ? reasonOptions
        : REASON_OPTIONS;
    const sortedReasonOptions = sortReasonOptionsBySeverity(normalizedReasonOptions);

    const handleSubmit = async () => {
        if (!reportReason || !descriptionMeetsMinWords) return;

        const reasonLabel = normalizedReasonOptions.find((o) => o.value === reportReason)?.label ?? reportReason;
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
                            onChange={(e) => {
                                setReportReason(e.target.value);
                                onClearError?.();
                            }}
                            className="w-full h-11 px-3 bg-white dark:bg-slate-800 border border-slate-300 dark:border-slate-600 rounded-lg text-sm text-slate-900 dark:text-slate-100 outline-none focus:border-red-400 dark:focus:border-red-500 focus:ring-2 focus:ring-red-300/60 dark:focus:ring-red-500/30"
                        >
                            <option value="">Chọn lý do</option>
                            {sortedReasonOptions.map((opt) => (
                                <option key={opt.value} value={opt.value}>{opt.label}</option>
                            ))}
                        </select>
                    </div>

                    {errorMessage ? (
                        <div
                            role="alert"
                            className="mb-4 rounded-lg border border-red-300 bg-red-50 px-3 py-2.5 text-sm font-medium text-red-700 dark:border-red-800 dark:bg-red-950/40 dark:text-red-300"
                        >
                            {errorMessage}
                        </div>
                    ) : null}

                    <div className="mb-4">
                        <label className="block text-base font-semibold text-slate-800 dark:text-slate-100 mb-2">
                            Mô tả chi tiết (bắt buộc, tối thiểu 50 từ)
                        </label>
                        <textarea
                            value={reportDetails}
                            onChange={(e) => {
                                setReportDetails(e.target.value);
                                onClearError?.();
                            }}
                            maxLength={8000}
                            className={`w-full p-3 bg-white dark:bg-slate-800 border rounded-lg text-sm text-slate-900 dark:text-slate-100 outline-none focus:ring-2 resize-none ${
                                descriptionIncomplete
                                    ? 'border-red-400 dark:border-red-600 focus:border-red-500 dark:focus:border-red-500 focus:ring-red-300/60 dark:focus:ring-red-500/30'
                                    : 'border-slate-300 dark:border-slate-600 focus:border-red-400 dark:focus:border-red-500 focus:ring-red-300/60 dark:focus:ring-red-500/30'
                            }`}
                            rows={6}
                            placeholder="Mô tả rõ vi phạm hoặc ngữ cảnh (ít nhất 50 từ)..."
                        />
                        <p
                            className={`mt-1.5 text-sm ${descriptionMeetsMinWords ? 'text-slate-500 dark:text-slate-400' : 'text-red-600 dark:text-red-400 font-medium'}`}
                        >
                            {descriptionWordCount}/{MODERATOR_ESCALATION_REASON_MIN_WORDS} từ
                            {!descriptionMeetsMinWords ? ' — cần thêm nội dung để gửi báo cáo.' : ''}
                        </p>
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
                        disabled={!reportReason || submitting || !descriptionMeetsMinWords}
                        className="min-w-32 px-5 py-2.5 bg-red-600 text-white text-sm font-bold rounded-full shadow-sm hover:bg-red-700 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                        {submitting ? 'Đang gửi...' : 'Gửi báo cáo'}
                    </button>
                </div>
            </div>
        </div>
    );
}
