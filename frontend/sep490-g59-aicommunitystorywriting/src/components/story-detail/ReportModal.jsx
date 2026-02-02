import { X } from 'lucide-react';
import { useState } from 'react';

// eslint-disable-next-line no-unused-vars
export function ReportModal({ isOpen, onClose, onSubmit, title, type }) {
    const [reportReason, setReportReason] = useState('');
    const [reportDetails, setReportDetails] = useState('');

    if (!isOpen) return null;

    const handleSubmit = () => {
        if (reportReason) {
            onSubmit(reportReason, reportDetails);
            setReportReason('');
            setReportDetails('');
            onClose();
        }
    };

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
            <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-2xl max-w-md w-full border border-slate-200 dark:border-slate-800">
                <div className="flex items-center justify-between p-6 border-b border-slate-200 dark:border-slate-800">
                    <h3 className="text-xl font-bold text-slate-900 dark:text-white">{title}</h3>
                    <button
                        onClick={onClose}
                        className="p-2 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-full transition-colors"
                    >
                        <X className="w-5 h-5" />
                    </button>
                </div>

                <div className="p-6">
                    <div className="mb-6">
                        <label className="block text-sm font-semibold text-slate-900 dark:text-white mb-2">
                            Lý do báo cáo
                        </label>
                        <select
                            value={reportReason}
                            onChange={(e) => setReportReason(e.target.value)}
                            className="w-full p-3 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary/50"
                        >
                            <option value="">Chọn lý do</option>
                            <option value="spam">Spam</option>
                            <option value="abusive">Ngôn từ xúc phạm</option>
                            <option value="inappropriate">Nội dung không phù hợp</option>
                            <option value="copyright">Vi phạm bản quyền</option>
                            <option value="violence">Bạo lực</option>
                            <option value="sexual">Nội dung khiêu dâm</option>
                            <option value="other">Khác</option>
                        </select>
                    </div>

                    <div className="mb-6">
                        <label className="block text-sm font-semibold text-slate-900 dark:text-white mb-2">
                            Chi tiết báo cáo (tùy chọn)
                        </label>
                        <textarea
                            value={reportDetails}
                            onChange={(e) => setReportDetails(e.target.value)}
                            placeholder="Nhập thêm thông tin nếu cần..."
                            className="w-full p-3 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary/50 resize-none"
                            rows={4}
                        />
                    </div>

                    <div className="flex gap-3">
                        <button
                            onClick={onClose}
                            className="flex-1 px-4 py-2.5 bg-slate-100 dark:bg-slate-800 text-slate-900 dark:text-white text-sm font-bold rounded-full hover:bg-slate-200 dark:hover:bg-slate-700 transition-all"
                        >
                            Hủy
                        </button>
                        <button
                            onClick={handleSubmit}
                            disabled={!reportReason}
                            className="flex-1 px-4 py-2.5 bg-red-50 dark:bg-red-950/30 text-red-600 dark:text-red-400 text-sm font-bold rounded-full hover:bg-red-100 dark:hover:bg-red-900/40 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                            Gửi báo cáo
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}
