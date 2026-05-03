import { FileText, CheckCircle, XCircle, Eye, Pencil, ToggleLeft, ToggleRight, Trash2 } from 'lucide-react';

const TYPE_LABELS = { USER: 'Người dùng', AUTHOR: 'Tác giả', AI: 'AI', DEFAULT: 'Mặc định' };

function formatDate(value) {
    if (!value) return '—';
    const raw = String(value).trim();
    const hasTimezone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(raw);
    const d = new Date(hasTimezone ? raw : `${raw}Z`);
    if (Number.isNaN(d.getTime())) return raw;
    return d.toLocaleString('vi-VN', {
        timeZone: 'Asia/Ho_Chi_Minh',
        hour12: false,
    });
}

export function PolicyList({ policies, loading, onView, onEdit, onToggleActive, onDelete }) {
    if (loading) {
        return (
            <div className="rounded-xl border border-[#c9f0d8] bg-white p-12 text-center">
                <div className="text-4xl mb-4">⏳</div>
                <p className="text-slate-500 text-sm">Đang tải...</p>
            </div>
        );
    }

    if (!policies?.length) {
        return (
            <div className="rounded-xl border border-[#c9f0d8] bg-white p-12 text-center">
                <div className="text-4xl mb-4">📄</div>
                <h3 className="text-lg font-semibold text-slate-800 mb-1">Chưa có policy nào</h3>
                <p className="text-slate-500 text-sm">Thêm policy mới hoặc thay đổi bộ lọc</p>
            </div>
        );
    }

    return (
        <div className="rounded-xl border border-[#c9f0d8] bg-white overflow-hidden">
            <div className="overflow-x-auto">
                <table className="w-full border-collapse text-left text-slate-800">
                    <thead>
                        <tr className="border-b border-[#c9f0d8] bg-[#f0faf5]">
                            <th className="px-4 py-3 text-left text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">Loại</th>
                            <th className="px-4 py-3 text-left text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">Phiên bản</th>
                            <th className="px-4 py-3 text-left text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">Trạng thái</th>
                            <th className="px-4 py-3 text-left text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">Yêu cầu ký lại</th>
                            <th className="px-4 py-3 text-left text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">Ngày tạo</th>
                            <th className="px-4 py-3 text-left text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">Kích hoạt lúc</th>
                            <th className="px-4 py-3 text-right text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">Thao tác</th>
                        </tr>
                    </thead>
                    <tbody>
                        {policies.map((policy) => (
                            <tr key={policy.id} className="border-t border-[#c9f0d8] bg-white transition-colors first:border-t-0 hover:bg-[#f7fcf9]">
                                <td className="px-4 py-3">
                                    <span className="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-lg bg-slate-100 text-slate-700 text-sm font-medium">
                                        <FileText className="w-4 h-4" />
                                        {(TYPE_LABELS[policy.type] ?? policy.type) || '—'}
                                    </span>
                                </td>
                                <td className="px-4 py-3 font-medium text-slate-800">{policy.version || '—'}</td>
                                <td className="px-4 py-3">
                                    {policy.isActive ? (
                                        <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold bg-emerald-100 text-emerald-800">
                                            <CheckCircle className="w-3.5 h-3.5" /> Đang dùng
                                        </span>
                                    ) : (
                                        <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold bg-slate-100 text-slate-600">
                                            <XCircle className="w-3.5 h-3.5" /> Tắt
                                        </span>
                                    )}
                                </td>
                                <td className="px-4 py-3 text-slate-600 text-sm">{policy.requireResign ? 'Có' : 'Không'}</td>
                                <td className="px-4 py-3 text-slate-600 text-sm">{formatDate(policy.createdAt)}</td>
                                <td className="px-4 py-3 text-slate-600 text-sm">{formatDate(policy.activatedAt)}</td>
                                <td className="px-4 py-3 text-right">
                                    <div className="flex items-center justify-end gap-2">
                                        <button
                                            type="button"
                                            onClick={() => onView?.(policy)}
                                            className="p-2 rounded-lg text-slate-600 hover:bg-emerald-100 hover:text-emerald-700 transition-colors"
                                            title="Xem nội dung"
                                        >
                                            <Eye className="w-4 h-4" />
                                        </button>
                                        <button
                                            type="button"
                                            onClick={() => onEdit?.(policy)}
                                            className="p-2 rounded-lg text-slate-600 hover:bg-slate-100 hover:text-slate-800 transition-colors"
                                            title="Chỉnh sửa"
                                        >
                                            <Pencil className="w-4 h-4" />
                                        </button>
                                        <button
                                            type="button"
                                            onClick={() => onToggleActive?.(policy)}
                                            className="p-2 rounded-lg text-slate-600 hover:bg-slate-100 transition-colors"
                                            title={policy.isActive ? 'Tắt' : 'Bật'}
                                        >
                                            {policy.isActive ? <ToggleRight className="w-5 h-5 text-emerald-600" /> : <ToggleLeft className="w-5 h-5 text-slate-400" />}
                                        </button>
                                        <button
                                            type="button"
                                            onClick={() => onDelete?.(policy)}
                                            className="p-2 rounded-lg text-slate-600 hover:bg-red-50 hover:text-red-700 transition-colors"
                                            title="Xóa policy"
                                        >
                                            <Trash2 className="w-4 h-4" />
                                        </button>
                                    </div>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
}
