import { X, Link, Image } from 'lucide-react';
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
}) {
    const [reportReason, setReportReason] = useState('');
    const [reportDetails, setReportDetails] = useState('');
    const [evidenceLinksStr, setEvidenceLinksStr] = useState('');
    const [evidenceImagesStr, setEvidenceImagesStr] = useState('');

    if (!isOpen) return null;

    const evidenceLinks = evidenceLinksStr
        .split(/[\n,]+/)
        .map((s) => s.trim())
        .filter(Boolean);
    const evidenceImages = evidenceImagesStr
        .split(/[\n,]+/)
        .map((s) => s.trim())
        .filter(Boolean);

    const handleSubmit = () => {
        if (!reportReason) return;
        const evidenceText = [reportDetails]
            .concat(evidenceLinks.length ? ['Link bằng chứng: ' + evidenceLinks.join(', ')] : [])
            .concat(evidenceImages.length ? ['Ảnh bằng chứng: ' + evidenceImages.join(', ')] : [])
            .filter(Boolean)
            .join('\n');

        const reasonLabel = REASON_OPTIONS.find((o) => o.value === reportReason)?.label ?? reportReason;
        const payload = {
            reason: reasonLabel,
            reasonCode: reportReason,
            description: reportDetails,
            evidence: evidenceText,
            evidenceLinks,
            evidenceImages,
            storyId: storyId ?? undefined,
            storyTitle: storyTitle ?? undefined,
            chapterId: chapterId ?? undefined,
            chapterTitle: chapterTitle ?? undefined,
            targetType: type,
            targetId: targetId ?? undefined,
        };
        onSubmit(payload);
        setReportReason('');
        setReportDetails('');
        setEvidenceLinksStr('');
        setEvidenceImagesStr('');
        onClose();
    };

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
            <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-2xl max-w-lg w-full border border-slate-200 dark:border-slate-800 max-h-[90vh] overflow-hidden flex flex-col">
                <div className="flex items-center justify-between p-6 border-b border-slate-200 dark:border-slate-800 shrink-0">
                    <h3 className="text-xl font-bold text-slate-900 dark:text-white">{title}</h3>
                    <button
                        type="button"
                        onClick={onClose}
                        className="p-2 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-full transition-colors"
                    >
                        <X className="w-5 h-5" />
                    </button>
                </div>

                <div className="p-6 overflow-y-auto">
                    <div className="mb-4">
                        <label className="block text-sm font-semibold text-slate-900 dark:text-white mb-2">
                            Lý do báo cáo <span className="text-red-500">*</span>
                        </label>
                        <select
                            value={reportReason}
                            onChange={(e) => setReportReason(e.target.value)}
                            className="w-full p-3 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary/50"
                        >
                            <option value="">Chọn lý do</option>
                            {REASON_OPTIONS.map((opt) => (
                                <option key={opt.value} value={opt.value}>{opt.label}</option>
                            ))}
                        </select>
                    </div>

                    <div className="mb-4">
                        <label className="block text-sm font-semibold text-slate-900 dark:text-white mb-2">
                            Chi tiết / mô tả (tùy chọn)
                        </label>
                        <textarea
                            value={reportDetails}
                            onChange={(e) => setReportDetails(e.target.value)}
                            placeholder="Mô tả ngắn gọn vấn đề..."
                            className="w-full p-3 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary/50 resize-none"
                            rows={3}
                        />
                    </div>

                    <div className="mb-4">
                        <label className="block text-sm font-semibold text-slate-900 dark:text-white mb-2 flex items-center gap-2">
                            <Link className="w-4 h-4 text-primary" />
                            Link bằng chứng (mỗi dòng hoặc cách nhau bằng dấu phẩy)
                        </label>
                        <textarea
                            value={evidenceLinksStr}
                            onChange={(e) => setEvidenceLinksStr(e.target.value)}
                            placeholder="https://..."
                            className="w-full p-3 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary/50 resize-none"
                            rows={2}
                        />
                    </div>

                    <div className="mb-6">
                        <label className="block text-sm font-semibold text-slate-900 dark:text-white mb-2 flex items-center gap-2">
                            <Image className="w-4 h-4 text-primary" />
                            URL ảnh bằng chứng (mỗi dòng hoặc cách nhau bằng dấu phẩy)
                        </label>
                        <textarea
                            value={evidenceImagesStr}
                            onChange={(e) => setEvidenceImagesStr(e.target.value)}
                            placeholder="https://..."
                            className="w-full p-3 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary/50 resize-none"
                            rows={2}
                        />
                    </div>

                    <div className="flex gap-3">
                        <button
                            type="button"
                            onClick={onClose}
                            className="flex-1 px-4 py-2.5 bg-slate-100 dark:bg-slate-800 text-slate-900 dark:text-white text-sm font-bold rounded-full hover:bg-slate-200 dark:hover:bg-slate-700 transition-all"
                        >
                            Hủy
                        </button>
                        <button
                            type="button"
                            onClick={handleSubmit}
                            disabled={!reportReason}
                            className="flex-1 px-4 py-2.5 bg-primary text-slate-900 text-sm font-bold rounded-full hover:opacity-90 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                            Gửi báo cáo
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}
