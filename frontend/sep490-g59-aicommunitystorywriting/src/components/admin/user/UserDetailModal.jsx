import { useState, useEffect } from 'react';
import { X, User, Mail, Shield, Calendar, LogIn, Ban, CheckCircle, Phone, FileText, BookOpen } from 'lucide-react';
import { resolveBackendUrl } from '../../../utils/resolveBackendUrl';
import { getUserDisplayName } from '../../../api/admin/userManagementApi';
import { updateUserRole, assignModeratorCategories, getModeratorCategories } from '../../../api/admin/userManagementApi';
import { getAllCategories } from '../../../api/category/categoryApi';

const ROLE_LABELS = { USER: 'Người dùng', AUTHOR: 'Tác giả', MODERATOR: 'Kiểm duyệt', ADMIN: 'Quản trị' };
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
    const [categories, setCategories] = useState([]);
    const [selectedCategoryIds, setSelectedCategoryIds] = useState([]);
    const [moderatorCategoryIds, setModeratorCategoryIds] = useState([]);
    const [savingModerator, setSavingModerator] = useState(false);

    useEffect(() => {
        getAllCategories({ includeInactive: false }).then((data) => {
            const list = Array.isArray(data) ? data : (data?.items ?? data?.Items ?? []);
            setCategories(list);
        }).catch(() => setCategories([]));
    }, []);

    useEffect(() => {
        if (!user?.id) return;
        getModeratorCategories(user.id).then((res) => {
            const ids = res?.categoryIds ?? [];
            setModeratorCategoryIds(ids);
            setSelectedCategoryIds(ids);
        }).catch(() => {
            setModeratorCategoryIds(user.moderatorCategoryIds ?? []);
            setSelectedCategoryIds(user.moderatorCategoryIds ?? []);
        });
    }, [user?.id, user?.moderatorCategoryIds]);

    if (!user) return null;

    const isBanned = user.status === 'BANNED';
    const isModerator = user.role === 'MODERATOR';

    const toggleCategory = (id) => {
        setSelectedCategoryIds((prev) =>
            prev.includes(id) ? prev.filter((x) => x !== id) : [...prev, id]
        );
    };

    const handleAssignModerator = async () => {
        if (selectedCategoryIds.length === 0) return;
        setSavingModerator(true);
        try {
            await updateUserRole(user.id, 'MODERATOR');
            await assignModeratorCategories(user.id, selectedCategoryIds);
            setModeratorCategoryIds(selectedCategoryIds);
            onAssignModerator?.();
        } catch (e) {
            console.error(e);
        } finally {
            setSavingModerator(false);
        }
    };

    const handleRemoveModerator = async () => {
        setSavingModerator(true);
        try {
            await updateUserRole(user.id, 'USER');
            await assignModeratorCategories(user.id, []);
            setModeratorCategoryIds([]);
            setSelectedCategoryIds([]);
            onAssignModerator?.();
        } catch (e) {
            console.error(e);
        } finally {
            setSavingModerator(false);
        }
    };

    const getCategoryName = (id) => categories.find((c) => (c.id ?? c.Id) === id)?.name ?? id;

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
                            <p className="text-sm text-slate-500">Vai trò</p>
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

                    {/* Gán moderator theo thể loại truyện */}
                    <div className="border-t border-slate-200 pt-4 mt-4">
                        <div className="flex items-center gap-2 mb-2">
                            <BookOpen className="w-4 h-4 text-emerald-600" />
                            <h3 className="font-semibold text-slate-900">Gán moderator (kiểm duyệt theo thể loại truyện)</h3>
                        </div>
                        <p className="text-sm text-slate-500 mb-3">Chọn các thể loại truyện mà user này được quyền kiểm duyệt. Sau đó nhấn &quot;Gán làm moderator&quot;.</p>
                        {moderatorCategoryIds.length > 0 && (
                            <p className="text-sm text-emerald-700 mb-2">Đang kiểm duyệt: {moderatorCategoryIds.map(getCategoryName).join(', ') || '—'}</p>
                        )}
                        <div className="flex flex-wrap gap-2 mb-3 max-h-32 overflow-y-auto">
                            {categories.map((cat) => {
                                const id = cat.id ?? cat.Id;
                                const name = cat.name ?? cat.Name ?? id;
                                const checked = selectedCategoryIds.includes(id);
                                return (
                                    <label key={id} className="inline-flex items-center gap-1.5 cursor-pointer">
                                        <input
                                            type="checkbox"
                                            checked={checked}
                                            onChange={() => toggleCategory(id)}
                                            className="rounded border-slate-300 text-emerald-600 focus:ring-emerald-500"
                                        />
                                        <span className="text-sm text-slate-700">{name}</span>
                                    </label>
                                );
                            })}
                            {categories.length === 0 && <span className="text-sm text-slate-500">Đang tải thể loại...</span>}
                        </div>
                        <div className="flex gap-2">
                            <button
                                type="button"
                                disabled={savingModerator || selectedCategoryIds.length === 0}
                                onClick={handleAssignModerator}
                                className="px-4 py-2 bg-emerald-500 text-white rounded-lg font-semibold text-sm hover:bg-emerald-600 disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                {savingModerator ? 'Đang lưu...' : 'Gán làm moderator'}
                            </button>
                            {isModerator && (
                                <button
                                    type="button"
                                    disabled={savingModerator}
                                    onClick={handleRemoveModerator}
                                    className="px-4 py-2 border border-slate-300 text-slate-700 rounded-lg font-semibold text-sm hover:bg-slate-50 disabled:opacity-50"
                                >
                                    Bỏ quyền moderator
                                </button>
                            )}
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
