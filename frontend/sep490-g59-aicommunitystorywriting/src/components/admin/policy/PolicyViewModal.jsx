import { X, FileText } from 'lucide-react';

function formatDate(value) {
    if (!value) return '—';
    return new Date(value).toLocaleString('vi-VN');
}

export function PolicyViewModal({ policy, onClose }) {
    if (!policy) return null;

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm" onClick={onClose}>
            <div
                className="bg-white rounded-2xl shadow-xl max-w-3xl w-full max-h-[90vh] overflow-hidden flex flex-col"
                onClick={(e) => e.stopPropagation()}
            >
                <div className="flex items-center justify-between p-4 border-b border-slate-200">
                    <div className="flex items-center gap-2">
                        <FileText className="w-5 h-5 text-emerald-600" />
                        <h2 className="text-xl font-bold text-slate-900">Policy: {policy.type} — v{policy.version}</h2>
                    </div>
                    <button type="button" onClick={onClose} className="p-2 rounded-lg hover:bg-slate-100 text-slate-500">
                        <X className="w-5 h-5" />
                    </button>
                </div>
                <div className="p-4 border-b border-slate-100 text-sm text-slate-500 flex flex-wrap gap-4">
                    <span>Trạng thái: {policy.isActive ? 'Đang áp dụng' : 'Tắt'}</span>
                    <span>Yêu cầu ký lại: {policy.requireResign ? 'Có' : 'Không'}</span>
                    <span>Tạo lúc: {formatDate(policy.createdAt)}</span>
                    <span>Kích hoạt: {formatDate(policy.activatedAt)}</span>
                </div>
                <div className="p-6 overflow-y-auto flex-1">
                    <div className="prose prose-slate max-w-none text-slate-700 whitespace-pre-wrap font-mono text-sm bg-slate-50 rounded-lg p-4">
                        {policy.content || '—'}
                    </div>
                </div>
                <div className="p-4 border-t border-slate-200">
                    <button type="button" onClick={onClose} className="w-full px-4 py-2.5 border border-slate-300 rounded-xl font-semibold text-slate-700 hover:bg-slate-50">
                        Đóng
                    </button>
                </div>
            </div>
        </div>
    );
}
