import { useState, useEffect, useCallback } from 'react';
import { UserList } from '../../../components/admin/user/UserList';
import { UserDetailModal } from '../../../components/admin/user/UserDetailModal';
import { CreateUserModal } from '../../../components/admin/user/CreateUserModal';
import { Pagination } from '../../../components/pagination/Pagination';
import { createUser, getUsers, getStats, updateUserStatus, deleteUser, getUserDisplayName } from '../../../api/admin/userManagementApi';
import { Search, Ban, X, UserPlus } from 'lucide-react';

const PAGE_SIZE = 10;
const FILTER_TABS = [
    { value: '', label: 'Tất cả' },
    { value: 'ACTIVE', label: 'Hoạt động' },
    { value: 'PENDING', label: 'Chờ xác thực' },
    { value: 'INACTIVE', label: 'Không hoạt động' },
    { value: 'BANNED', label: 'Đã khóa' },
];

export function UserManagement() {
    const [users, setUsers] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [stats, setStats] = useState({ total: 0, active: 0, inactive: 0, banned: 0, pending: 0, authors: 0, moderators: 0 });
    const [currentPage, setCurrentPage] = useState(1);
    const [totalPages, setTotalPages] = useState(1);
    const [totalCount, setTotalCount] = useState(0);
    const [filterStatus, setFilterStatus] = useState('');
    const [search, setSearch] = useState('');
    const [searchInput, setSearchInput] = useState('');
    const [selectedUser, setSelectedUser] = useState(null);
    const [blockConfirmUser, setBlockConfirmUser] = useState(null);
    const [blockConfirmLoading, setBlockConfirmLoading] = useState(false);
    const [createModalOpen, setCreateModalOpen] = useState(false);
    const [createMessage, setCreateMessage] = useState(null);

    const loadUsers = useCallback((page = 1) => {
        setLoading(true);
        setError(null);
        getUsers({ page, pageSize: PAGE_SIZE, search, status: filterStatus || undefined })
            .then((res) => {
                setUsers(res.items ?? []);
                setTotalCount(res.totalCount ?? 0);
                setTotalPages(res.totalPages ?? 1);
                setCurrentPage(res.page ?? page);
            })
            .catch((err) => {
                setError(err?.message ?? 'Không tải được danh sách người dùng');
                setUsers([]);
                setTotalCount(0);
                setTotalPages(1);
            })
            .finally(() => setLoading(false));
    }, [search, filterStatus]);

    const loadStats = useCallback(() => {
        getStats()
            .then(setStats)
            .catch(() => setStats({ total: 0, active: 0, inactive: 0, banned: 0, pending: 0, authors: 0, moderators: 0 }));
    }, []);

    useEffect(() => {
        loadUsers(1);
    }, [loadUsers]);

    useEffect(() => {
        loadStats();
    }, [loadStats]);

    const handlePageChange = (page) => {
        setCurrentPage(page);
        loadUsers(page);
    };

    const handleSearchSubmit = (e) => {
        e.preventDefault();
        setSearch(searchInput.trim());
        setCurrentPage(1);
    };

    const handleFilterChange = (value) => {
        setFilterStatus(value);
        setCurrentPage(1);
    };

    const handleViewDetail = (user) => setSelectedUser(user);
    const handleCloseDetail = () => setSelectedUser(null);

    const requestBlockUser = (user) => {
        if (!user?.id) return;
        setBlockConfirmUser(user);
    };

    const cancelBlockConfirm = () => {
        if (blockConfirmLoading) return;
        setBlockConfirmUser(null);
    };

    const confirmBlockUser = async () => {
        if (!blockConfirmUser?.id) return;
        setBlockConfirmLoading(true);
        try {
            await updateUserStatus(blockConfirmUser.id, 'BANNED');
            loadUsers(currentPage);
            loadStats();
            setSelectedUser(null);
            setBlockConfirmUser(null);
        } catch (err) {
            console.error(err);
        } finally {
            setBlockConfirmLoading(false);
        }
    };

    const handleUnblock = async (user) => {
        try {
            await updateUserStatus(user.id, 'ACTIVE');
            loadUsers(currentPage);
            loadStats();
            setSelectedUser(null);
        } catch (err) {
            console.error(err);
        }
    };

    const handleDeleteUser = async (user) => {
        const res = await deleteUser(user.id);
        if (res.success) {
            loadUsers(currentPage);
            loadStats();
        }
        return res;
    };

    const handleCreateUser = async (payload) => {
        const created = await createUser(payload);
        setCreateMessage({
            type: 'success',
            text: `Đã tạo tài khoản ${created?.email || payload.email} thành công.`,
        });
        setCurrentPage(1);
        loadUsers(1);
        loadStats();
    };

    return (
        <div className="p-8">
            <div className="mb-8 flex flex-col sm:flex-row sm:items-start sm:justify-between gap-3">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900 mb-1">Quản lý người dùng</h1>
                    <p className="text-sm text-slate-500">Xem và quản lý tài khoản người dùng, tác giả</p>
                </div>
                <button
                    type="button"
                    onClick={() => setCreateModalOpen(true)}
                    className="inline-flex items-center justify-center gap-2 px-4 py-2.5 bg-emerald-600 text-white rounded-lg font-semibold text-sm hover:bg-emerald-700"
                >
                    <UserPlus className="w-4 h-4" />
                    Tạo user mới
                </button>
            </div>

            {createMessage ? (
                <div className="mb-6 rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-sm text-emerald-800 flex items-start justify-between gap-3">
                    <span>{createMessage.text}</span>
                    <button
                        type="button"
                        onClick={() => setCreateMessage(null)}
                        className="shrink-0 rounded-md px-2 py-1 text-emerald-700 hover:bg-emerald-100"
                    >
                        Đóng
                    </button>
                </div>
            ) : null}

            {/* Thống kê */}
            <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-7 gap-4 mb-8">
                <div className="rounded-xl border-2 border-amber-200 bg-amber-50 p-4">
                    <div className="text-2xl font-bold text-amber-800">{stats.total}</div>
                    <div className="text-sm font-semibold text-amber-700">Tổng user</div>
                </div>
                <div className="rounded-xl border-2 border-emerald-200 bg-emerald-50 p-4">
                    <div className="text-2xl font-bold text-emerald-800">{stats.active}</div>
                    <div className="text-sm font-semibold text-emerald-700">Hoạt động</div>
                </div>
                <div className="rounded-xl border-2 border-yellow-200 bg-yellow-50 p-4">
                    <div className="text-2xl font-bold text-yellow-800">{stats.pending}</div>
                    <div className="text-sm font-semibold text-yellow-700">Chờ xác thực</div>
                </div>
                <div className="rounded-xl border-2 border-slate-200 bg-slate-50 p-4">
                    <div className="text-2xl font-bold text-slate-700">{stats.inactive}</div>
                    <div className="text-sm font-semibold text-slate-600">Không hoạt động</div>
                </div>
                <div className="rounded-xl border-2 border-red-200 bg-red-50 p-4">
                    <div className="text-2xl font-bold text-red-800">{stats.banned}</div>
                    <div className="text-sm font-semibold text-red-700">Đã khóa</div>
                </div>
                <div className="rounded-xl border-2 border-blue-200 bg-blue-50 p-4">
                    <div className="text-2xl font-bold text-blue-800">{stats.authors}</div>
                    <div className="text-sm font-semibold text-blue-700">Tác giả</div>
                </div>
                <div className="rounded-xl border-2 border-violet-200 bg-violet-50 p-4">
                    <div className="text-2xl font-bold text-violet-800">{stats.moderators}</div>
                    <div className="text-sm font-semibold text-violet-700">Kiểm duyệt</div>
                </div>
            </div>

            {/* Tìm kiếm + Filter */}
            <div className="bg-white rounded-xl border border-slate-200 p-4 mb-6 flex flex-col sm:flex-row gap-4 flex-wrap">
                <form onSubmit={handleSearchSubmit} className="flex-1 min-w-[200px] flex gap-2">
                    <div className="relative flex-1">
                        <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
                        <input
                            type="text"
                            value={searchInput}
                            onChange={(e) => setSearchInput(e.target.value)}
                            placeholder="Tìm theo email, tên..."
                            className="w-full pl-9 pr-4 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-emerald-500 focus:border-emerald-500"
                        />
                    </div>
                    <button
                        type="submit"
                        className="px-4 py-2 bg-emerald-500 text-white rounded-lg font-semibold text-sm hover:bg-emerald-600"
                    >
                        Tìm kiếm
                    </button>
                </form>
                <div className="flex gap-2 flex-wrap">
                    {FILTER_TABS.map((tab) => (
                        <button
                            key={tab.value}
                            type="button"
                            onClick={() => handleFilterChange(tab.value)}
                            className={`px-4 py-2 rounded-full text-sm font-semibold transition-colors ${
                                filterStatus === tab.value
                                    ? 'bg-emerald-500 text-white'
                                    : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                            }`}
                        >
                            {tab.label} ({tab.value === '' ? stats.total : tab.value === 'ACTIVE' ? stats.active : tab.value === 'PENDING' ? stats.pending : tab.value === 'INACTIVE' ? stats.inactive : stats.banned})
                        </button>
                    ))}
                </div>
            </div>

            {/* Bảng user */}
            {error ? (
                <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-red-800 text-sm">{error}</div>
            ) : (
                <>
                    <UserList
                        users={users}
                        loading={loading}
                        onViewDetail={handleViewDetail}
                        onBlock={requestBlockUser}
                        onUnblock={handleUnblock}
                    />
                    {totalPages > 1 && (
                        <div className="mt-4">
                            <Pagination
                                currentPage={currentPage}
                                totalPages={totalPages}
                                totalItems={totalCount}
                                itemsPerPage={PAGE_SIZE}
                                onPageChange={handlePageChange}
                                itemLabel="người dùng"
                            />
                        </div>
                    )}
                </>
            )}

            {selectedUser && (
                <UserDetailModal
                    user={selectedUser}
                    onClose={handleCloseDetail}
                    onBlock={requestBlockUser}
                    onUnblock={handleUnblock}
                    onDeleteUser={handleDeleteUser}
                    onAssignModerator={(newRole) => {
                        if (newRole) {
                            setSelectedUser((prev) => (prev ? { ...prev, role: newRole } : null));
                        }
                        loadUsers(currentPage);
                        loadStats();
                    }}
                />
            )}

            <CreateUserModal
                open={createModalOpen}
                onClose={() => setCreateModalOpen(false)}
                onSubmit={handleCreateUser}
            />

            {blockConfirmUser ? (
                <div
                    className="fixed inset-0 z-[100] flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm"
                    role="dialog"
                    aria-modal="true"
                    aria-labelledby="block-confirm-title"
                    onClick={cancelBlockConfirm}
                >
                    <div
                        className="bg-white rounded-2xl shadow-xl max-w-md w-full p-6 border border-slate-200"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="flex items-start gap-3">
                            <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-red-100 text-red-600">
                                <Ban className="h-5 w-5" />
                            </div>
                            <div className="min-w-0 flex-1">
                                <h3 id="block-confirm-title" className="text-lg font-bold text-slate-900">
                                    Xác nhận khóa tài khoản
                                </h3>
                                <p className="mt-2 text-sm text-slate-600 leading-relaxed">
                                    Bạn có chắc muốn <strong className="text-red-700">khóa (ban)</strong> tài khoản{' '}
                                    <span className="font-semibold text-slate-800">
                                        {getUserDisplayName(blockConfirmUser)}
                                    </span>
                                    {blockConfirmUser.email ? (
                                        <>
                                            {' '}
                                            <span className="text-slate-500">({blockConfirmUser.email})</span>
                                        </>
                                    ) : null}
                                    ? Người dùng sẽ không đăng nhập được cho đến khi được mở khóa.
                                </p>
                            </div>
                            <button
                                type="button"
                                onClick={cancelBlockConfirm}
                                className="shrink-0 rounded-lg p-1.5 text-slate-400 hover:bg-slate-100 hover:text-slate-600"
                                aria-label="Đóng"
                            >
                                <X className="h-5 w-5" />
                            </button>
                        </div>
                        <div className="mt-6 flex flex-col-reverse sm:flex-row gap-2 sm:justify-end">
                            <button
                                type="button"
                                disabled={blockConfirmLoading}
                                onClick={cancelBlockConfirm}
                                className="px-4 py-2.5 rounded-xl border border-slate-300 font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                            >
                                Hủy
                            </button>
                            <button
                                type="button"
                                disabled={blockConfirmLoading}
                                onClick={confirmBlockUser}
                                className="px-4 py-2.5 rounded-xl bg-red-600 text-white font-semibold hover:bg-red-700 disabled:opacity-50"
                            >
                                {blockConfirmLoading ? 'Đang xử lý…' : 'Khóa tài khoản'}
                            </button>
                        </div>
                    </div>
                </div>
            ) : null}
        </div>
    );
}
