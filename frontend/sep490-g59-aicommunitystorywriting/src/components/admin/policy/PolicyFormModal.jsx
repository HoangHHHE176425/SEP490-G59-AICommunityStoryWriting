import { useState, useEffect } from 'react';
import { X } from 'lucide-react';

const TYPE_OPTIONS = [
    { value: 'USER', label: 'Người dùng' },
    { value: 'AUTHOR', label: 'Tác giả' },
    { value: 'AI', label: 'AI' },
    { value: 'DEFAULT', label: 'Mặc định' },
];

export function PolicyFormModal({ policy, onClose, onSave, saving }) {
    const [type, setType] = useState('USER');
    const [version, setVersion] = useState('');
    const [content, setContent] = useState('');
    const [isActive, setIsActive] = useState(false);
    const [requireResign, setRequireResign] = useState(false);

    useEffect(() => {
        if (policy) {
            setType(policy.type || 'USER');
            setVersion(policy.version || '');
            setContent(policy.content || '');
            setIsActive(!!policy.isActive);
            setRequireResign(!!policy.requireResign);
        } else {
            setType('USER');
            setVersion('1.0');
            setContent('');
            setIsActive(false);
            setRequireResign(false);
        }
    }, [policy]);

    const handleSubmit = (e) => {
        e.preventDefault();
        onSave?.({ type, version, content, isActive, requireResign });
    };

    const isEdit = !!policy?.id;

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm" onClick={onClose}>
            <div
                className="bg-white rounded-2xl shadow-xl max-w-2xl w-full max-h-[90vh] overflow-hidden flex flex-col"
                onClick={(e) => e.stopPropagation()}
            >
                <div className="flex items-center justify-between p-4 border-b border-slate-200">
                    <h2 className="text-xl font-bold text-slate-900">{isEdit ? 'Chỉnh sửa Policy' : 'Thêm Policy mới'}</h2>
                    <button type="button" onClick={onClose} className="p-2 rounded-lg hover:bg-slate-100 text-slate-500">
                        <X className="w-5 h-5" />
                    </button>
                </div>
                <form onSubmit={handleSubmit} className="flex flex-col flex-1 overflow-hidden">
                    <div className="p-6 overflow-y-auto space-y-4">
                        <div>
                            <label className="block text-sm font-medium text-slate-700 mb-1">Loại policy</label>
                            <select
                                value={type}
                                onChange={(e) => setType(e.target.value)}
                                className="w-full px-4 py-2 border border-slate-200 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500"
                                required
                            >
                                {TYPE_OPTIONS.map((opt) => (
                                    <option key={opt.value} value={opt.value}>{opt.label}</option>
                                ))}
                            </select>
                        </div>
                        <div>
                            <label className="block text-sm font-medium text-slate-700 mb-1">Phiên bản</label>
                            <input
                                type="text"
                                value={version}
                                onChange={(e) => setVersion(e.target.value)}
                                placeholder="VD: 1.0"
                                className="w-full px-4 py-2 border border-slate-200 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500"
                                required
                            />
                        </div>
                        <div>
                            <label className="block text-sm font-medium text-slate-700 mb-1">Nội dung</label>
                            <textarea
                                value={content}
                                onChange={(e) => setContent(e.target.value)}
                                rows={10}
                                placeholder="Nội dung điều khoản / chính sách (HTML hoặc văn bản)..."
                                className="w-full px-4 py-2 border border-slate-200 rounded-lg focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500 font-mono text-sm"
                                required
                            />
                        </div>
                        <div className="flex flex-wrap gap-6">
                            <label className="inline-flex items-center gap-2 cursor-pointer">
                                <input
                                    type="checkbox"
                                    checked={isActive}
                                    onChange={(e) => setIsActive(e.target.checked)}
                                    className="rounded border-slate-300 text-emerald-600 focus:ring-emerald-500"
                                />
                                <span className="text-sm font-medium text-slate-700">Đang áp dụng</span>
                            </label>
                            <label className="inline-flex items-center gap-2 cursor-pointer">
                                <input
                                    type="checkbox"
                                    checked={requireResign}
                                    onChange={(e) => setRequireResign(e.target.checked)}
                                    className="rounded border-slate-300 text-emerald-600 focus:ring-emerald-500"
                                />
                                <span className="text-sm font-medium text-slate-700">Yêu cầu ký lại khi cập nhật</span>
                            </label>
                        </div>
                    </div>
                    <div className="p-4 border-t border-slate-200 flex gap-2">
                        <button type="button" onClick={onClose} className="flex-1 px-4 py-2.5 border border-slate-300 rounded-xl font-semibold text-slate-700 hover:bg-slate-50">
                            Hủy
                        </button>
                        <button
                            type="submit"
                            disabled={saving}
                            className="flex-1 px-4 py-2.5 bg-emerald-500 text-white rounded-xl font-semibold hover:bg-emerald-600 disabled:opacity-50"
                        >
                            {saving ? 'Đang lưu...' : isEdit ? 'Cập nhật' : 'Thêm mới'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}
