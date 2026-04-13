import { User, Mail, Shield, Clock, Ban, CheckCircle, Eye, Phone, AlertCircle } from 'lucide-react';
import { resolveBackendUrl } from '../../../utils/resolveBackendUrl';
import { getUserDisplayName } from '../../../api/admin/userManagementApi';

const ROLE_LABELS = { USER: 'Người dùng', AUTHOR: 'Tác giả', MODERATOR: 'Kiểm duyệt', ADMIN: 'Quản trị', COMPLIANCE: 'Compliance' };
const STATUS_CONFIG = {
    ACTIVE: { label: 'Hoạt động', bg: '#d1fae5', color: '#065f46', icon: CheckCircle },
    PENDING: { label: 'Chờ xác thực', bg: '#fef3c7', color: '#92400e', icon: AlertCircle },
    INACTIVE: { label: 'Không hoạt động', bg: '#f1f5f9', color: '#475569', icon: Clock },
    BANNED: { label: 'Đã khóa', bg: '#fee2e2', color: '#991b1b', icon: Ban },
    DELETED: { label: 'Đã xóa', bg: '#f1f5f9', color: '#64748b', icon: Ban },
};

function formatDate(value) {
    if (!value) return '—';
    const d = new Date(value);
    return d.toLocaleDateString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
}

export function UserList({ users, onViewDetail, onBlock, onUnblock, loading }) {
    if (loading) {
        return (
            <div className="rounded-xl border border-[#c9f0d8] bg-white p-12 text-center">
                <div className="text-4xl mb-4">⏳</div>
                <p className="text-slate-500 text-sm">Đang tải danh sách...</p>
            </div>
        );
    }

    if (!users?.length) {
        return (
            <div className="rounded-xl border border-[#c9f0d8] bg-white p-12 text-center">
                <div className="text-4xl mb-4">👥</div>
                <h3 className="text-lg font-semibold text-slate-800 mb-1">Chưa có người dùng</h3>
                <p className="text-slate-500 text-sm">Thử thay đổi bộ lọc hoặc từ khóa tìm kiếm</p>
            </div>
        );
    }

    return (
        <div className="rounded-xl border border-[#c9f0d8] bg-white overflow-hidden">
            <div className="overflow-x-auto">
                <table className="w-full border-collapse text-left text-slate-800">
                    <thead>
                        <tr className="border-b border-[#c9f0d8] bg-[#f0faf5]">
                            <th className="px-4 py-3 text-left text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">Người dùng / Biệt danh</th>
                            <th className="px-4 py-3 text-left text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">Email</th>
                            <th className="px-4 py-3 text-left text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">SĐT</th>
                            <th className="px-4 py-3 text-left text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">Vai trò</th>
                            <th className="px-4 py-3 text-left text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">Trạng thái</th>
                            <th className="px-4 py-3 text-left text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">Xác thực email</th>
                            <th className="px-4 py-3 text-left text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">Đăng ký</th>
                            <th className="px-4 py-3 text-right text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">Thao tác</th>
                        </tr>
                    </thead>
                    <tbody>
                        {users.map((user) => {
                            const statusCfg = STATUS_CONFIG[user.status] ?? STATUS_CONFIG.INACTIVE;
                            const StatusIcon = statusCfg.icon;
                            return (
                                <tr
                                    key={user.id}
                                    className="border-t border-[#c9f0d8] bg-white transition-colors first:border-t-0 hover:bg-[#f7fcf9]"
                                >
                                    <td className="px-4 py-3">
                                        <div className="flex items-center gap-3">
                                            <div className="w-10 h-10 rounded-full bg-slate-200 flex items-center justify-center overflow-hidden flex-shrink-0">
                                                {user.avatarUrl ? (
                                                    <img src={resolveBackendUrl(user.avatarUrl)} alt="" className="w-full h-full object-cover" />
                                                ) : (
                                                    <User className="w-5 h-5 text-slate-500" />
                                                )}
                                            </div>
                                            <span className="font-medium text-slate-800">{getUserDisplayName(user)}</span>
                                        </div>
                                    </td>
                                    <td className="px-4 py-3">
                                        <span className="flex items-center gap-1.5 text-slate-600">
                                            <Mail className="w-4 h-4 text-slate-400" />
                                            {user.email}
                                        </span>
                                    </td>
                                    <td className="px-4 py-3">
                                        <span className="flex items-center gap-1.5 text-slate-600">
                                            <Phone className="w-4 h-4 text-slate-400" />
                                            {user.phone || '—'}
                                        </span>
                                    </td>
                                    <td className="px-4 py-3">
                                        <span className="inline-flex items-center gap-1 px-2.5 py-1 rounded-lg bg-slate-100 text-slate-700 text-sm font-medium">
                                            <Shield className="w-3.5 h-3.5" />
                                            {ROLE_LABELS[user.role] ?? user.role}
                                        </span>
                                    </td>
                                    <td className="px-4 py-3">
                                        <span
                                            className="inline-flex items-center gap-1 px-2.5 py-1 rounded-full text-xs font-semibold"
                                            style={{ backgroundColor: statusCfg.bg, color: statusCfg.color }}
                                        >
                                            <StatusIcon className="w-3.5 h-3.5" />
                                            {statusCfg.label}
                                        </span>
                                    </td>
                                    <td className="px-4 py-3 text-slate-600 text-sm">
                                        {user.emailVerifiedAt ? <span className="text-emerald-600">Đã xác thực</span> : <span className="text-amber-600">Chưa xác thực</span>}
                                    </td>
                                    <td className="px-4 py-3 text-slate-600 text-sm">{formatDate(user.createdAt)}</td>
                                    <td className="px-4 py-3 text-right">
                                        <div className="flex items-center justify-end gap-2">
                                            <button
                                                type="button"
                                                onClick={() => onViewDetail?.(user)}
                                                className="p-2 rounded-lg text-slate-600 hover:bg-emerald-100 hover:text-emerald-700 transition-colors"
                                                title="Xem chi tiết"
                                            >
                                                <Eye className="w-4 h-4" />
                                            </button>
                                            {user.status === 'DELETED' ? null : user.status === 'BANNED' ? (
                                                <button
                                                    type="button"
                                                    onClick={() => onUnblock?.(user)}
                                                    className="p-2 rounded-lg text-slate-600 hover:bg-emerald-100 hover:text-emerald-700 transition-colors"
                                                    title="Mở khóa"
                                                >
                                                    <CheckCircle className="w-4 h-4" />
                                                </button>
                                            ) : (
                                                <button
                                                    type="button"
                                                    onClick={() => onBlock?.(user)}
                                                    className="p-2 rounded-lg text-slate-600 hover:bg-red-100 hover:text-red-600 transition-colors"
                                                    title="Khóa tài khoản"
                                                >
                                                    <Ban className="w-4 h-4" />
                                                </button>
                                            )}
                                        </div>
                                    </td>
                                </tr>
                            );
                        })}
                    </tbody>
                </table>
            </div>
        </div>
    );
}
