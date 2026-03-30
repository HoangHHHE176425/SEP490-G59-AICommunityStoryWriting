import { useState, useEffect } from 'react';
import { X, User, Mail, Shield, Calendar, LogIn, Ban, CheckCircle, Phone, FileText } from 'lucide-react';
import { resolveBackendUrl } from '../../../utils/resolveBackendUrl';
import { getUserDisplayName, updateUserRole } from '../../../api/admin/userManagementApi';
import { useAuth } from '../../../contexts/AuthContext';

const ROLE_LABELS = { USER: 'Người dùng', AUTHOR: 'Tác giả', MODERATOR: 'Kiểm duyệt', ADMIN: 'Quản trị', COMPLIANCE: 'Compliance' };
const EDITABLE_ROLES = ['USER', 'AUTHOR', 'MODERATOR', 'ADMIN', 'COMPLIANCE'];

const STATUS_LABELS = {
    ACTIVE: 'Hoạt động',
    PENDING: 'Chờ xác thực',
    INACTIVE: 'Không hoạt động',
    BANNED: 'Đã khóa',
    DELETED: 'Đã xóa',
};

function formatDate(value) {
    if (!value) return '—';
    return new Date(value).toLocaleString('vi-VN');
}

export function UserDetailModal({ user, onClose, onBlock, onUnblock, onAssignModerator }) {
    const { user: currentUser } = useAuth();
    const [selectedRole, setSelectedRole] = useState('USER');
    const [savingRole, setSavingRole] = useState(false);
    const [roleError, setRoleError] = useState('');

    useEffect(() => {
        if (user?.role) {
            setSelectedRole(String(user.role).toUpperCase());
        }
        setRoleError('');
    }, [user?.id, user?.role]);

    if (!user) return null;

    const isBanned = user.status === 'BANNED';
    const roleDirty = user.role !== selectedRole;
    const isSelf = Boolean(currentUser?.id && user?.id && String(currentUser.id) === String(user.id));
    const isSelfAdminRole = isSelf && String(user?.role ?? '').toUpperCase() === 'ADMIN';

    const handleSaveRole = async () => {
        if (isSelfAdminRole) {
            setRoleError('Bạn không thể tự thay đổi role của chính mình.');
            return;
        }
        if (!roleDirty) return;
        setSavingRole(true);
        setRoleError('');
        try {
            await updateUserRole(user.id, selectedRole);
            onAssignModerator?.(selectedRole);
        } catch (e) {
            const msg =
                e?.response?.data?.message ||
                e?.message ||
                'Không thể cập nhật vai trò.';
            setRoleError(msg);
        } finally {
            setSavingRole(false);
        }
    };

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm" onClick={onClose}>
            <div
                className="bg-white rounded-2xl shadow-xl max-w-xl w-full max-h-[90vh] overflow-hidden flex flex-col"
                onClick={(e) => e.stopPropagation()}
            >
                <div className="flex items-center justify-between p-4 border-b border-slate-200">
                    <h2 className="text-xl font-bold text-slate-900">Chi tiết người dùng</h2>
                    <button type="button" onClick={onClose} className="p-2 rounded-lg hover:bg-slate-100 text-slate-500 hover:text-slate-700">
                        <X className="w-5 h-5" />
                    </button>
                </div>
                <div className="p-6 overflow-y-auto space-y-4">
                    <div className="flex justify-center">
                        <div className="w-20 h-20 rounded-full bg-slate-200 flex items-center justify-center overflow-hidden">
                            {user.avatarUrl ? (
                                <img src={resolveBackendUrl(user.avatarUrl)} alt="" className="w-full h-full object-cover" />
                            ) : (
                                <User className="w-10 h-10 text-slate-500" />
                            )}
                        </div>
                    </div>

                    <div>
                        <p className="text-sm text-slate-500 mb-0.5">Biệt danh / Tên hiển thị</p>
                        <p className="font-medium text-slate-900">{getUserDisplayName(user)}</p>
                    </div>
                    <div className="flex items-center gap-2">
                        <Mail className="w-4 h-4 text-slate-400 flex-shrink-0" />
                        <div>
                            <p className="text-sm text-slate-500">Email</p>
                            <p className="font-medium text-slate-900">{user.email || '—'}</p>
                        </div>
                    </div>
                    <div className="flex items-center gap-2">
                        <Phone className="w-4 h-4 text-slate-400 flex-shrink-0" />
                        <div>
                            <p className="text-sm text-slate-500">Số điện thoại</p>
                            <p className="font-medium text-slate-900">{user.phone || '—'}</p>
                        </div>
                    </div>
                    <div>
                        <p className="text-sm text-slate-500 mb-0.5">CMND/CCCD</p>
                        <p className="font-medium text-slate-900">{user.idNumber || '—'}</p>
                    </div>
                    <div>
                        <p className="text-sm text-slate-500 mb-0.5">Giới thiệu (Bio)</p>
                        <p className="font-medium text-slate-900">{user.bio || '—'}</p>
                    </div>
                    <div>
                        <p className="text-sm text-slate-500 mb-0.5">Mô tả</p>
                        <p className="font-medium text-slate-900">{user.description || '—'}</p>
                    </div>
                    <div className="flex items-center gap-2">
                        <Shield className="w-4 h-4 text-slate-400 flex-shrink-0" />
                        <div>
                            <p className="text-sm text-slate-500">Vai trò hiện tại</p>
                            <p className="font-medium text-slate-900">{ROLE_LABELS[user.role] ?? user.role}</p>
                        </div>
                    </div>
                    <div>
                        <p className="text-sm text-slate-500 mb-0.5">Trạng thái</p>
                        <p className="font-medium text-slate-900">{STATUS_LABELS[user.status] ?? user.status}</p>
                    </div>
                    <div>
                        <p className="text-sm text-slate-500 mb-0.5">Xác thực email</p>
                        <p className="font-medium text-slate-900">{user.emailVerifiedAt ? `Đã xác thực (${formatDate(user.emailVerifiedAt)})` : 'Chưa xác thực'}</p>
                    </div>
                    <div className="flex items-center gap-2">
                        <Calendar className="w-4 h-4 text-slate-400 flex-shrink-0" />
                        <div>
                            <p className="text-sm text-slate-500">Ngày đăng ký</p>
                            <p className="font-medium text-slate-900">{formatDate(user.createdAt)}</p>
                        </div>
                    </div>
                    <div className="flex items-center gap-2">
                        <FileText className="w-4 h-4 text-slate-400 flex-shrink-0" />
                        <div>
                            <p className="text-sm text-slate-500">Cập nhật lần cuối</p>
                            <p className="font-medium text-slate-900">{formatDate(user.updatedAt ?? user.profileUpdatedAt)}</p>
                        </div>
                    </div>
                    <div className="flex items-center gap-2">
                        <LogIn className="w-4 h-4 text-slate-400 flex-shrink-0" />
                        <div>
                            <p className="text-sm text-slate-500">Đăng nhập gần nhất</p>
                            <p className="font-medium text-slate-900">{formatDate(user.lastLoginAt)}</p>
                        </div>
                    </div>

                    {/* Thay đổi vai trò (moderator = chỉ đổi role, không gán thể loại) */}
                    <div className="border-t border-slate-200 pt-4 mt-4">
                        <div className="flex items-center gap-2 mb-2">
                            <Shield className="w-4 h-4 text-emerald-600" />
                            <h3 className="font-semibold text-slate-900">Thay đổi vai trò</h3>
                        </div>
                        <p className="text-sm text-slate-500 mb-3">
                            Chọn vai trò mới cho tài khoản (ví dụ <strong>Kiểm duyệt</strong> để cấp quyền moderator). Không còn gán theo thể loại truyện.
                        </p>
                        {isSelfAdminRole ? (
                            <p className="text-sm text-amber-700 mb-2">
                                Tài khoản ADMIN hiện tại không được phép tự đổi role.
                            </p>
                        ) : null}
                        {roleError ? (
                            <p className="text-sm text-red-600 mb-2">{roleError}</p>
                        ) : null}
                        <div className="flex flex-col sm:flex-row gap-2 sm:items-center">
                            <select
                                value={selectedRole}
                                onChange={(e) => setSelectedRole(e.target.value)}
                                className="flex-1 min-w-0 px-3 py-2 border border-slate-200 rounded-lg text-slate-900 text-sm focus:ring-2 focus:ring-emerald-500/30 focus:border-emerald-500"
                            >
                                {EDITABLE_ROLES.map((r) => (
                                    <option key={r} value={r}>
                                        {ROLE_LABELS[r] ?? r}
                                    </option>
                                ))}
                            </select>
                            <button
                                type="button"
                                disabled={savingRole || !roleDirty || isSelfAdminRole}
                                onClick={handleSaveRole}
                                className="px-4 py-2 bg-emerald-500 text-white rounded-lg font-semibold text-sm hover:bg-emerald-600 disabled:opacity-50 disabled:cursor-not-allowed whitespace-nowrap"
                            >
                                {savingRole ? 'Đang lưu...' : 'Cập nhật vai trò'}
                            </button>
                        </div>
                    </div>
                </div>
                <div className="p-4 border-t border-slate-200 flex gap-2">
                    <button type="button" onClick={onClose} className="flex-1 px-4 py-2.5 border border-slate-300 rounded-xl font-semibold text-slate-700 hover:bg-slate-50">
                        Đóng
                    </button>
                    {isBanned ? (
                        <button type="button" onClick={() => { onUnblock?.(user); onClose(); }} className="flex-1 px-4 py-2.5 bg-emerald-500 text-white rounded-xl font-semibold hover:bg-emerald-600 flex items-center justify-center gap-2">
                            <CheckCircle className="w-4 h-4" /> Mở khóa
                        </button>
                    ) : user.status !== 'DELETED' ? (
                        <button type="button" onClick={() => { onBlock?.(user); onClose(); }} className="flex-1 px-4 py-2.5 bg-red-500 text-white rounded-xl font-semibold hover:bg-red-600 flex items-center justify-center gap-2">
                            <Ban className="w-4 h-4" /> Khóa tài khoản
                        </button>
                    ) : null}
                </div>
            </div>
        </div>
    );
}
