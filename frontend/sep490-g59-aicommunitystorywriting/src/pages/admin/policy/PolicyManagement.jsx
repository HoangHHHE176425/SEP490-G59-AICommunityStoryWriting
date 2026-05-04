import { useState, useEffect, useCallback } from 'react';
import { PolicyList } from '../../../components/admin/policy/PolicyList';
import { PolicyFormModal } from '../../../components/admin/policy/PolicyFormModal';
import { PolicyViewModal } from '../../../components/admin/policy/PolicyViewModal';
import { Pagination } from '../../../components/pagination/Pagination';
import { useToast } from '../../../components/author/story-editor/Toast';
import {
    getPolicies,
    getPolicyById,
    getPolicyStats,
    createPolicy,
    updatePolicy,
    setPolicyActive,
    deletePolicy,
} from '../../../api/admin/policyManagementApi';
import { Plus } from 'lucide-react';

const PAGE_SIZE = 10;
const FILTER_TYPE = [
    { value: '', label: 'Tất cả' },
    { value: 'USER', label: 'Người dùng' },
    { value: 'AUTHOR', label: 'Tác giả' },
    { value: 'AI', label: 'AI' },
];

function getApiErrorMessage(err, fallback) {
    const data = err?.response?.data;
    if (typeof data === 'string' && data.trim()) return data;
    if (data && typeof data === 'object') {
        if (typeof data.message === 'string' && data.message.trim()) return data.message;
        if (typeof data.Message === 'string' && data.Message.trim()) return data.Message;
        if (typeof data.detail === 'string' && data.detail.trim()) return data.detail;
        if (typeof data.title === 'string' && data.title.trim()) return data.title;
        if (data.errors && typeof data.errors === 'object') {
            const firstError = Object.values(data.errors).flat().find(Boolean);
            if (typeof firstError === 'string' && firstError.trim()) return firstError;
        }
    }
    if (typeof err?.message === 'string' && err.message.trim()) return err.message;
    return fallback;
}

export function PolicyManagement() {
    const { showToast, ToastContainer } = useToast();
    const [policies, setPolicies] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [stats, setStats] = useState({ total: 0, active: 0, byType: {} });
    const [currentPage, setCurrentPage] = useState(1);
    const [totalPages, setTotalPages] = useState(1);
    const [totalCount, setTotalCount] = useState(0);
    const [filterType, setFilterType] = useState('');
    const [filterActive, setFilterActive] = useState(null); // true | false | null (all)
    const [viewingPolicy, setViewingPolicy] = useState(null);
    const [editingPolicy, setEditingPolicy] = useState(null);
    const [showForm, setShowForm] = useState(false);
    const [saving, setSaving] = useState(false);
    const [loadingDetailId, setLoadingDetailId] = useState(null);
    const [confirmDelete, setConfirmDelete] = useState(null);

    const loadPolicies = useCallback((page = 1) => {
        setLoading(true);
        setError(null);
        getPolicies({
            page,
            pageSize: PAGE_SIZE,
            type: filterType || undefined,
            isActive: filterActive !== null ? filterActive : undefined,
        })
            .then((res) => {
                setPolicies(res.items ?? []);
                setTotalCount(res.totalCount ?? 0);
                setTotalPages(res.totalPages ?? 1);
                setCurrentPage(res.page ?? page);
            })
            .catch((err) => {
                setError(err?.message ?? 'Không tải được danh sách policy');
                setPolicies([]);
                setTotalCount(0);
                setTotalPages(1);
            })
            .finally(() => setLoading(false));
    }, [filterType, filterActive]);

    const loadStats = useCallback(() => {
        getPolicyStats()
            .then(setStats)
            .catch(() => setStats({ total: 0, active: 0, byType: {} }));
    }, []);

    useEffect(() => {
        loadPolicies(1);
    }, [loadPolicies]);

    useEffect(() => {
        loadStats();
    }, [loadStats]);

    const handlePageChange = (page) => {
        setCurrentPage(page);
        loadPolicies(page);
    };

    const handleAdd = () => {
        setEditingPolicy(null);
        setShowForm(true);
    };

    const handleView = async (policy) => {
        if (!policy?.id) return;
        setLoadingDetailId(policy.id);
        try {
            const detail = await getPolicyById(policy.id);
            setViewingPolicy(detail ?? policy);
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? 'Không tải được chi tiết policy.';
            showToast(msg, 'error');
        } finally {
            setLoadingDetailId(null);
        }
    };

    const handleEdit = async (policy) => {
        if (!policy?.id) return;
        setLoadingDetailId(policy.id);
        try {
            const detail = await getPolicyById(policy.id);
            setEditingPolicy(detail ?? policy);
            setShowForm(true);
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? 'Không tải được chi tiết policy để chỉnh sửa.';
            showToast(msg, 'error');
        } finally {
            setLoadingDetailId(null);
        }
    };

    const handleCloseForm = () => {
        setShowForm(false);
        setEditingPolicy(null);
    };

    const handleSave = async (payload) => {
        setSaving(true);
        try {
            if (editingPolicy?.id) {
                await updatePolicy(editingPolicy.id, payload);
                showToast('Cập nhật policy thành công.', 'success');
            } else {
                await createPolicy(payload);
                showToast('Tạo policy thành công.', 'success');
            }
            handleCloseForm();
            loadPolicies(currentPage);
            loadStats();
        } catch (err) {
            showToast(getApiErrorMessage(err, 'Không lưu được policy.'), 'error');
        } finally {
            setSaving(false);
        }
    };

    const handleToggleActive = async (policy) => {
        try {
            await setPolicyActive(policy.id, !policy.isActive);
            loadPolicies(currentPage);
            loadStats();
            showToast(policy.isActive ? 'Đã tắt policy.' : 'Đã bật policy.', 'success');
        } catch (err) {
            showToast(getApiErrorMessage(err, 'Không cập nhật được trạng thái policy.'), 'error');
        }
    };

    const executeDelete = async (policy) => {
        if (!policy?.id) return;
        try {
            await deletePolicy(policy.id);
            if (viewingPolicy?.id === policy.id) {
                setViewingPolicy(null);
            }
            if (editingPolicy?.id === policy.id) {
                setShowForm(false);
                setEditingPolicy(null);
            }
            loadPolicies(currentPage);
            loadStats();
            showToast('Xóa policy thành công.', 'success');
        } catch (err) {
            showToast(getApiErrorMessage(err, 'Không xóa được policy.'), 'error');
        }
    };

    const handleDelete = (policy) => {
        if (!policy?.id) return;
        const label = [policy.type, policy.version].filter(Boolean).join(' · ') || policy.id;
        setConfirmDelete({
            id: policy.id,
            label,
        });
    };

    const handleConfirmDelete = async () => {
        if (!confirmDelete?.id) return;
        const target = policies.find((p) => p.id === confirmDelete.id);
        if (!target) {
            setConfirmDelete(null);
            return;
        }
        setConfirmDelete(null);
        await executeDelete(target);
    };

    return (
        <div className="p-8">
            <div className="mb-8 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                <div>
                    <h1 className="text-2xl font-bold text-slate-900 mb-1">Quản lý Policy</h1>
                    <p className="text-sm text-slate-500">Điều khoản, chính sách theo loại (USER, AUTHOR, AI)</p>
                </div>
                <button
                    type="button"
                    onClick={handleAdd}
                    className="inline-flex items-center justify-center gap-2 px-4 py-2.5 bg-emerald-500 text-white rounded-xl font-semibold hover:bg-emerald-600"
                >
                    <Plus className="w-5 h-5" /> Thêm Policy
                </button>
            </div>

            <div className="grid grid-cols-2 sm:grid-cols-4 gap-4 mb-8">
                <div className="rounded-xl border-2 border-amber-200 bg-amber-50 p-4">
                    <div className="text-2xl font-bold text-amber-800">{stats.total}</div>
                    <div className="text-sm font-semibold text-amber-700">Tổng policy</div>
                </div>
                <div className="rounded-xl border-2 border-emerald-200 bg-emerald-50 p-4">
                    <div className="text-2xl font-bold text-emerald-800">{stats.active}</div>
                    <div className="text-sm font-semibold text-emerald-700">Đang áp dụng</div>
                </div>
            </div>

            <div className="bg-[#f8fdfb] rounded-xl border border-[#c9f0d8] p-4 mb-6 flex flex-wrap gap-4">
                <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-sm font-medium text-slate-600">Loại:</span>
                    {FILTER_TYPE.map((opt) => (
                        <button
                            key={opt.value}
                            type="button"
                            onClick={() => setFilterType(opt.value)}
                            className={`px-3 py-1.5 rounded-full text-sm font-medium transition-colors ring-1 ${
                                filterType === opt.value
                                    ? 'bg-[#22c55e] text-white ring-[#22c55e]'
                                    : 'bg-white text-slate-700 ring-[#cbd5e1] hover:bg-[#f7fcf9]'
                            }`}
                        >
                            {opt.label}
                        </button>
                    ))}
                </div>
                <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-sm font-medium text-slate-600">Trạng thái:</span>
                    {[
                        { value: null, label: 'Tất cả' },
                        { value: true, label: 'Đang dùng' },
                        { value: false, label: 'Tắt' },
                    ].map((opt) => (
                        <button
                            key={String(opt.value)}
                            type="button"
                            onClick={() => setFilterActive(opt.value)}
                            className={`px-3 py-1.5 rounded-full text-sm font-medium transition-colors ring-1 ${
                                filterActive === opt.value
                                    ? 'bg-[#22c55e] text-white ring-[#22c55e]'
                                    : 'bg-white text-slate-700 ring-[#cbd5e1] hover:bg-[#f7fcf9]'
                            }`}
                        >
                            {opt.label}
                        </button>
                    ))}
                </div>
            </div>

            {error ? (
                <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-red-800 text-sm">{error}</div>
            ) : (
                <>
                    <PolicyList
                        policies={policies}
                        loading={loading}
                        onView={handleView}
                        onEdit={handleEdit}
                        onToggleActive={handleToggleActive}
                        onDelete={handleDelete}
                    />
                    {totalPages > 1 && (
                        <div className="mt-4">
                            <Pagination
                                currentPage={currentPage}
                                totalPages={totalPages}
                                totalItems={totalCount}
                                itemsPerPage={PAGE_SIZE}
                                onPageChange={handlePageChange}
                                itemLabel="policy"
                            />
                        </div>
                    )}
                </>
            )}

            {loadingDetailId ? (
                <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/30 backdrop-blur-[1px]">
                    <div className="rounded-xl bg-white px-4 py-3 text-sm font-medium text-slate-700 shadow-lg border border-slate-200">
                        Đang tải chi tiết policy...
                    </div>
                </div>
            ) : null}
            {viewingPolicy && <PolicyViewModal policy={viewingPolicy} onClose={() => setViewingPolicy(null)} />}
            {showForm && (
                <PolicyFormModal
                    policy={editingPolicy}
                    onClose={handleCloseForm}
                    onSave={handleSave}
                    saving={saving}
                />
            )}
            {confirmDelete ? (
                <div
                    className="fixed inset-0 z-[1000] flex items-center justify-center bg-black/50 p-4"
                    onClick={() => setConfirmDelete(null)}
                >
                    <div
                        className="w-full max-w-md overflow-hidden rounded-xl border border-slate-200 bg-white shadow-2xl"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="border-b border-slate-200 px-5 py-4">
                            <h3 className="text-base font-bold text-slate-900">Xác nhận xóa policy</h3>
                        </div>
                        <div className="px-5 py-4">
                            <p className="text-sm text-slate-700">
                                Bạn có chắc chắn muốn xóa policy "{confirmDelete.label}"? Thao tác này không thể hoàn tác.
                            </p>
                        </div>
                        <div className="flex justify-end gap-2 px-5 pb-4">
                            <button
                                type="button"
                                onClick={() => setConfirmDelete(null)}
                                className="rounded-lg border border-slate-300 px-3 py-2 text-sm font-semibold text-slate-700 hover:bg-slate-50"
                            >
                                Hủy
                            </button>
                            <button
                                type="button"
                                onClick={handleConfirmDelete}
                                className="rounded-lg bg-red-600 px-3 py-2 text-sm font-bold text-white hover:bg-red-700"
                            >
                                Xóa
                            </button>
                        </div>
                    </div>
                </div>
            ) : null}
            <ToastContainer />
        </div>
    );
}
