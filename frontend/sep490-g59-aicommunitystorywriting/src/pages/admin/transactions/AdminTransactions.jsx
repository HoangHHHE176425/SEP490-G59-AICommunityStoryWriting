import { useEffect, useMemo, useState } from 'react';
import { approveWithdraw, getAdminTransactions, rejectWithdraw } from '../../../api/admin/transactionsApi';

function safeBankAccount(tx) {
    // API returns bankAccount (object) or null. Older mock used snake_case.
    const b = tx?.bankAccount ?? null;
    if (!b || typeof b !== 'object') return null;
    return {
        bank_name: b.bank_name ?? b.bankName ?? b.bank_name_snapshot ?? '-',
        account_number: b.account_number ?? b.accountNumber ?? '-',
        account_holder_name: b.account_holder_name ?? b.accountHolderName ?? '-',
        branch_name: b.branch_name ?? b.branchName ?? '-',
        is_verified: b.is_verified ?? b.isVerified ?? null,
        updated_at: b.updated_at ?? b.updatedAt ?? null,
    };
}

function maskAccountNumber(value) {
    const s = String(value || '');
    const digits = s.replace(/\s+/g, '');
    if (!digits) return '-';
    if (digits.length <= 4) return digits;
    return `${'•'.repeat(Math.max(0, digits.length - 4))}${digits.slice(-4)}`;
}

function formatVnd(amount) {
    try {
        return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(amount || 0);
    } catch {
        return `${amount || 0} VND`;
    }
}

function formatTime(iso) {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return iso;
    return d.toLocaleString('vi-VN');
}

function statusPill(status) {
    const s = String(status || '').toUpperCase();
    if (s === 'SUCCESS') return 'bg-emerald-50 text-emerald-700 ring-emerald-200';
    if (s === 'PENDING') return 'bg-amber-50 text-amber-700 ring-amber-200';
    if (s === 'FAILED' || s === 'CANCELLED') return 'bg-red-50 text-red-700 ring-red-200';
    return 'bg-slate-100 text-slate-700 ring-slate-200';
}

function typeLabel(type) {
    const t = String(type || '').toUpperCase();
    if (t === 'DEPOSIT') return 'Nạp tiền';
    if (t === 'WITHDRAW') return 'Rút tiền';
    return t || '-';
}

function typeBadge(type) {
    const t = String(type || '').toUpperCase();
    if (t === 'DEPOSIT') return 'bg-sky-50 text-sky-700 ring-sky-200';
    if (t === 'WITHDRAW') return 'bg-fuchsia-50 text-fuchsia-700 ring-fuchsia-200';
    return 'bg-slate-100 text-slate-700 ring-slate-200';
}

export function AdminTransactions() {
    const [transactions, setTransactions] = useState(() => []);
    const [typeFilter, setTypeFilter] = useState('ALL'); // ALL | DEPOSIT | WITHDRAW
    const [statusFilter, setStatusFilter] = useState('ALL'); // ALL | SUCCESS | PENDING | FAILED
    const [query, setQuery] = useState('');
    const [fromDate, setFromDate] = useState('');
    const [toDate, setToDate] = useState('');
    const [selected, setSelected] = useState(null);
    const [actionLoading, setActionLoading] = useState(false);
    const [toast, setToast] = useState('');
    const [loading, setLoading] = useState(false);
    const [page, setPage] = useState(1);
    const [pageSize] = useState(20);
    const [totalCount, setTotalCount] = useState(0);
    const [totalPages, setTotalPages] = useState(1);
    const [loadError, setLoadError] = useState('');

    async function loadList(nextPage = page) {
        try {
            setLoading(true);
            setLoadError('');
            const res = await getAdminTransactions({
                type: typeFilter,
                status: statusFilter,
                q: query || undefined,
                from: fromDate || undefined,
                to: toDate || undefined,
                page: nextPage,
                pageSize,
            });
            setTransactions(res.items ?? []);
            setTotalCount(res.totalCount ?? 0);
            setTotalPages(res.totalPages ?? 1);
            setPage(res.page ?? nextPage);
        } catch (err) {
            setLoadError(err?.response?.status === 401 ? 'Chưa đăng nhập Admin hoặc token hết hạn.' : 'Không tải được danh sách giao dịch.');
            setTransactions([]);
            setTotalCount(0);
            setTotalPages(1);
        } finally {
            setLoading(false);
        }
    }

    // Load whenever filters change
    useEffect(() => {
        setPage(1);
        loadList(1);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [typeFilter, statusFilter, query, fromDate, toDate]);

    // Keep a "filtered" alias to minimize UI changes
    const filtered = useMemo(() => transactions, [transactions]);

    const selectedFresh = useMemo(() => {
        if (!selected?.id) return null;
        return transactions.find((t) => t.id === selected.id) ?? selected;
    }, [selected, transactions]);

    const canReviewWithdraw = selectedFresh?.type === 'WITHDRAW' && selectedFresh?.status === 'PENDING';

    async function handleWithdrawDecision(decision) {
        if (!selectedFresh) return;
        if (!canReviewWithdraw) return;
        const ok = window.confirm(
            decision === 'APPROVE'
                ? `Duyệt giao dịch rút ${formatVnd(selectedFresh.amountVnd)} cho ${selectedFresh.user?.email}?`
                : `Từ chối giao dịch rút ${formatVnd(selectedFresh.amountVnd)} cho ${selectedFresh.user?.email}?`
        );
        if (!ok) return;

        try {
            setActionLoading(true);
            const adminNote =
                decision === 'APPROVE'
                    ? 'Đã duyệt bởi Admin'
                    : 'Bị từ chối bởi Admin';
            if (decision === 'APPROVE') {
                await approveWithdraw(selectedFresh.id, adminNote);
            } else {
                await rejectWithdraw(selectedFresh.id, adminNote);
            }

            setToast(decision === 'APPROVE' ? 'Đã duyệt yêu cầu rút tiền.' : 'Đã từ chối yêu cầu rút tiền.');
            window.setTimeout(() => setToast(''), 2500);
            setSelected(null);
            await loadList(page);
        } finally {
            setActionLoading(false);
        }
    }

    return (
        <div className="space-y-5">
            {loadError ? (
                <div className="rounded-xl border border-amber-200 bg-amber-50 px-4 py-3 text-[12px] font-semibold text-amber-800">
                    {loadError}
                </div>
            ) : null}
            {toast ? (
                <div className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-[12px] font-semibold text-emerald-800">
                    {toast}
                </div>
            ) : null}
            <div className="flex items-start justify-between gap-4">
                <div>
                    <h1 className="text-lg md:text-xl font-bold text-slate-900">
                        Lịch sử giao dịch (Nạp / Rút)
                    </h1>
                    <p className="mt-1 text-[11px] text-slate-500">
                        Màn hình FE demo để theo dõi giao dịch ví. Sau này chỉ cần thay nguồn dữ liệu bằng API thật.
                    </p>
                </div>
                <span className="inline-flex items-center rounded-full bg-slate-100 px-2 py-1 text-[10px] font-semibold text-slate-700">
                    {loading ? '...' : `${totalCount} giao dịch`}
                </span>
            </div>

            <section className="bg-white rounded-xl border border-slate-200 shadow-sm p-5 space-y-4">
                <div className="flex flex-wrap items-center gap-2">
                    {[
                        { id: 'ALL', label: 'Tất cả' },
                        { id: 'DEPOSIT', label: 'Nạp tiền' },
                        { id: 'WITHDRAW', label: 'Rút tiền' },
                    ].map((t) => (
                        <button
                            key={t.id}
                            type="button"
                            onClick={() => setTypeFilter(t.id)}
                            className={`rounded-full px-3 py-1.5 text-[11px] font-semibold ring-1 transition ${
                                typeFilter === t.id
                                    ? 'bg-primary/10 text-primary ring-primary/20'
                                    : 'bg-white text-slate-700 ring-slate-200 hover:bg-slate-50'
                            }`}
                        >
                            {t.label}
                        </button>
                    ))}

                    <div className="h-5 w-px bg-slate-200 mx-1" />

                    <select
                        value={statusFilter}
                        onChange={(e) => setStatusFilter(e.target.value)}
                        className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-[11px] text-slate-900"
                    >
                        <option value="ALL">Tất cả trạng thái</option>
                        <option value="SUCCESS">Thành công</option>
                        <option value="PENDING">Đang xử lý</option>
                        <option value="FAILED">Thất bại</option>
                        <option value="CANCELLED">Đã hủy</option>
                    </select>

                    <div className="flex items-center gap-2 ml-auto">
                        <input
                            type="date"
                            value={fromDate}
                            onChange={(e) => setFromDate(e.target.value)}
                            className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-[11px] text-slate-900"
                        />
                        <span className="text-[11px] text-slate-400">→</span>
                        <input
                            type="date"
                            value={toDate}
                            onChange={(e) => setToDate(e.target.value)}
                            className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-[11px] text-slate-900"
                        />
                    </div>
                </div>

                <div className="flex flex-wrap items-center gap-2">
                    <input
                        value={query}
                        onChange={(e) => setQuery(e.target.value)}
                        placeholder="Tìm theo mã giao dịch, email, gateway ref…"
                        className="flex-1 min-w-[240px] rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary"
                    />
                    <button
                        type="button"
                        onClick={() => {
                            setQuery('');
                            setFromDate('');
                            setToDate('');
                            setStatusFilter('ALL');
                            setTypeFilter('ALL');
                            setPage(1);
                        }}
                        className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-[11px] font-semibold text-slate-700 hover:bg-slate-50"
                    >
                        Reset
                    </button>
                </div>

                <div className="overflow-hidden rounded-lg border border-slate-200">
                    <table className="w-full text-[11px]">
                        <thead className="bg-slate-50">
                            <tr className="text-left text-slate-500">
                                <th className="px-3 py-2 font-medium">Thời gian</th>
                                <th className="px-3 py-2 font-medium">Người dùng</th>
                                <th className="px-3 py-2 font-medium">Loại</th>
                                <th className="px-3 py-2 font-medium text-right">Số tiền</th>
                                <th className="px-3 py-2 font-medium">Phương thức</th>
                                <th className="px-3 py-2 font-medium">Trạng thái</th>
                                <th className="px-3 py-2 font-medium">Mã tham chiếu</th>
                            </tr>
                        </thead>
                        <tbody className="bg-white">
                            {filtered.length === 0 ? (
                                <tr>
                                    <td className="px-3 py-10 text-center text-slate-500" colSpan={7}>
                                        {loading ? 'Đang tải...' : 'Không có giao dịch phù hợp bộ lọc.'}
                                    </td>
                                </tr>
                            ) : (
                                filtered.map((tx, idx) => (
                                    <tr
                                        key={tx.id}
                                        className={`border-t border-slate-100 cursor-pointer ${
                                            idx % 2 === 1 ? 'bg-slate-50/40' : ''
                                        } hover:bg-primary/5`}
                                        onClick={() => setSelected(tx)}
                                    >
                                        <td className="px-3 py-2 text-slate-700 whitespace-nowrap">
                                            {formatTime(tx.createdAt)}
                                        </td>
                                        <td className="px-3 py-2">
                                            <div className="font-semibold text-slate-800">{tx.user?.name}</div>
                                            <div className="text-slate-500">{tx.user?.email}</div>
                                        </td>
                                        <td className="px-3 py-2">
                                            <span
                                                className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-semibold ring-1 ${typeBadge(
                                                    tx.type
                                                )}`}
                                            >
                                                {typeLabel(tx.type)}
                                            </span>
                                        </td>
                                        <td className="px-3 py-2 text-right font-semibold text-slate-900">
                                            {formatVnd(tx.amountVnd)}
                                        </td>
                                        <td className="px-3 py-2 text-slate-700">{tx.method}</td>
                                        <td className="px-3 py-2">
                                            <span
                                                className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-semibold ring-1 ${statusPill(
                                                    tx.status
                                                )}`}
                                            >
                                                {tx.status}
                                            </span>
                                        </td>
                                        <td className="px-3 py-2 text-slate-600">{tx.gatewayRef || '-'}</td>
                                    </tr>
                                ))
                            )}
                        </tbody>
                    </table>
                </div>
            </section>

            {selectedFresh && (
                <div className="fixed inset-0 z-50 flex items-end md:items-center justify-center">
                    <div
                        className="absolute inset-0 bg-black/40"
                        onClick={() => setSelected(null)}
                    />
                    <div className="relative w-full md:max-w-2xl bg-white rounded-t-2xl md:rounded-2xl shadow-xl border border-slate-200 p-5">
                        <div className="flex items-start justify-between gap-4">
                            <div>
                                <h2 className="text-base font-bold text-slate-900">Chi tiết giao dịch</h2>
                                <p className="text-[11px] text-slate-500 mt-1">{selectedFresh.id}</p>
                            </div>
                            <div className="flex items-center gap-2">
                                {canReviewWithdraw ? (
                                    <>
                                        <button
                                            type="button"
                                            disabled={actionLoading}
                                            onClick={() => handleWithdrawDecision('REJECT')}
                                            className={`rounded-lg border px-3 py-2 text-[11px] font-semibold ${
                                                actionLoading
                                                    ? 'border-slate-200 bg-slate-100 text-slate-400 cursor-not-allowed'
                                                    : 'border-red-200 bg-red-50 text-red-700 hover:bg-red-100'
                                            }`}
                                        >
                                            {actionLoading ? 'Đang xử lý…' : 'Từ chối'}
                                        </button>
                                        <button
                                            type="button"
                                            disabled={actionLoading}
                                            onClick={() => handleWithdrawDecision('APPROVE')}
                                            className={`rounded-lg border px-3 py-2 text-[11px] font-semibold ${
                                                actionLoading
                                                    ? 'border-slate-200 bg-slate-100 text-slate-400 cursor-not-allowed'
                                                    : 'border-emerald-200 bg-emerald-50 text-emerald-700 hover:bg-emerald-100'
                                            }`}
                                        >
                                            {actionLoading ? 'Đang xử lý…' : 'Duyệt'}
                                        </button>
                                    </>
                                ) : null}
                                <button
                                    type="button"
                                    onClick={() => setSelected(null)}
                                    className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-[11px] font-semibold text-slate-700 hover:bg-slate-50"
                                >
                                    Đóng
                                </button>
                            </div>
                        </div>

                        <div className="mt-4 grid grid-cols-1 md:grid-cols-2 gap-3 text-[11px]">
                            <div className="rounded-xl bg-slate-50 p-3">
                                <p className="text-slate-500">Người dùng</p>
                                <p className="mt-1 font-semibold text-slate-900">{selectedFresh.user?.name}</p>
                                <p className="text-slate-600">{selectedFresh.user?.email}</p>
                            </div>
                            <div className="rounded-xl bg-slate-50 p-3">
                                <p className="text-slate-500">Thời gian</p>
                                <p className="mt-1 font-semibold text-slate-900">{formatTime(selectedFresh.createdAt)}</p>
                                <p className="text-slate-600">Phương thức: {selectedFresh.method}</p>
                            </div>
                            <div className="rounded-xl bg-slate-50 p-3">
                                <p className="text-slate-500">Loại giao dịch</p>
                                <p className="mt-1 font-semibold text-slate-900">{typeLabel(selectedFresh.type)}</p>
                                <p className="text-slate-600">Trạng thái: {selectedFresh.status}</p>
                            </div>
                            <div className="rounded-xl bg-slate-50 p-3">
                                <p className="text-slate-500">Số tiền</p>
                                <p className="mt-1 font-semibold text-slate-900">{formatVnd(selectedFresh.amountVnd)}</p>
                                <p className="text-slate-600">Mã tham chiếu: {selectedFresh.gatewayRef || '-'}</p>
                            </div>
                            {selectedFresh.type === 'WITHDRAW' && (
                                <div className="md:col-span-2 rounded-xl bg-slate-50 p-3">
                                    <p className="text-slate-500">Thông tin nhận</p>
                                    <p className="mt-1 text-slate-800">
                                        Hệ thống hiện không lưu trường <span className="font-semibold">Ngân hàng</span> và <span className="font-semibold">Số tài khoản</span> trong DB.
                                    </p>
                                </div>
                            )}
                            <div className="md:col-span-2 rounded-xl bg-slate-50 p-3">
                                <p className="text-slate-500">Ghi chú</p>
                                <p className="mt-1 text-slate-800">{selectedFresh.note || '-'}</p>
                            </div>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

