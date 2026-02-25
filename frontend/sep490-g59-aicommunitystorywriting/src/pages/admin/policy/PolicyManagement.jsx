import { useState, useEffect, useCallback } from 'react';
import { PolicyList } from '../../../components/admin/policy/PolicyList';
import { PolicyFormModal } from '../../../components/admin/policy/PolicyFormModal';
import { PolicyViewModal } from '../../../components/admin/policy/PolicyViewModal';
import { Pagination } from '../../../components/pagination/Pagination';
import {
    getPolicies,
    getPolicyStats,
    createPolicy,
    updatePolicy,
    setPolicyActive,
} from '../../../api/admin/policyManagementApi';
import { Plus } from 'lucide-react';

const PAGE_SIZE = 10;
const FILTER_TYPE = [
    { value: '', label: 'Tất cả' },
    { value: 'USER', label: 'Người dùng' },
    { value: 'AUTHOR', label: 'Tác giả' },
    { value: 'AI', label: 'AI' },
    { value: 'DEFAULT', label: 'Mặc định' },
];

export function PolicyManagement() {
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

    const handleEdit = (policy) => {
        setEditingPolicy(policy);
        setShowForm(true);
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
            } else {
                await createPolicy(payload);
            }
            handleCloseForm();
            loadPolicies(currentPage);
            loadStats();
        } catch (err) {
            console.error(err);
        } finally {
            setSaving(false);
        }
    };

    const handleToggleActive = async (policy) => {
        try {
            await setPolicyActive(policy.id, !policy.isActive);
            loadPolicies(currentPage);
            loadStats();
        } catch (err) {
            console.error(err);
        }
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

            <div className="bg-white rounded-xl border border-slate-200 p-4 mb-6 flex flex-wrap gap-4">
                <div className="flex items-center gap-2 flex-wrap">
                    <span className="text-sm font-medium text-slate-600">Loại:</span>
                    {FILTER_TYPE.map((opt) => (
                        <button
                            key={opt.value}
                            type="button"
                            onClick={() => setFilterType(opt.value)}
                            className={`px-3 py-1.5 rounded-full text-sm font-medium transition-colors ${
                                filterType === opt.value ? 'bg-emerald-500 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                            }`}
                        >
                            {opt.label}
                        </button>
                    ))}
                </div>
                <div className="flex items-center gap-2">
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
                            className={`px-3 py-1.5 rounded-full text-sm font-medium transition-colors ${
                                filterActive === opt.value ? 'bg-emerald-500 text-white' : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
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
                        onView={setViewingPolicy}
                        onEdit={handleEdit}
                        onToggleActive={handleToggleActive}
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

            {viewingPolicy && <PolicyViewModal policy={viewingPolicy} onClose={() => setViewingPolicy(null)} />}
            {showForm && (
                <PolicyFormModal
                    policy={editingPolicy}
                    onClose={handleCloseForm}
                    onSave={handleSave}
                    saving={saving}
                />
            )}
        </div>
    );
}
