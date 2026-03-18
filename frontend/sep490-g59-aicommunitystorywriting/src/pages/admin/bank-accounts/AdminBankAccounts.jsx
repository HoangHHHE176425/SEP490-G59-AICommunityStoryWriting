import { useMemo, useState } from 'react';

const MOCK_BANK_ACCOUNTS = [
    {
        user_id: 'U-0002',
        bank_name: 'Vietcombank',
        account_number: '0123456789',
        account_holder_name: 'TRẦN THỊ B',
        branch_name: 'CN TP.HCM',
        is_verified: false,
        updated_at: '2026-03-10T04:00:00Z',
    },
    {
        user_id: 'U-0004',
        bank_name: 'Techcombank',
        account_number: '2345678901',
        account_holder_name: 'PHẠM THỊ D',
        branch_name: 'CN Hà Nội',
        is_verified: true,
        updated_at: '2026-03-12T09:30:00Z',
    },
];

function formatTime(iso) {
    const d = new Date(iso);
    if (Number.isNaN(d.getTime())) return iso || '—';
    return d.toLocaleString('vi-VN');
}

function maskAccountNumber(value) {
    const s = String(value || '').replace(/\s+/g, '');
    if (!s) return '—';
    if (s.length <= 4) return s;
    return `${'•'.repeat(Math.max(0, s.length - 4))}${s.slice(-4)}`;
}

export function AdminBankAccounts() {
    const [accounts, setAccounts] = useState(() => MOCK_BANK_ACCOUNTS);
    const [filterVerified, setFilterVerified] = useState('ALL'); // ALL | VERIFIED | UNVERIFIED
    const [query, setQuery] = useState('');
    const [toast, setToast] = useState('');

    const filtered = useMemo(() => {
        const q = query.trim().toLowerCase();
        return accounts
            .filter((a) => {
                if (filterVerified === 'VERIFIED' && !a.is_verified) return false;
                if (filterVerified === 'UNVERIFIED' && a.is_verified) return false;
                if (!q) return true;
                const hay = [
                    a.user_id,
                    a.bank_name,
                    a.account_number,
                    a.account_holder_name,
                    a.branch_name,
                ]
                    .filter(Boolean)
                    .join(' ')
                    .toLowerCase();
                return hay.includes(q);
            })
            .sort((x, y) => new Date(y.updated_at) - new Date(x.updated_at));
    }, [accounts, filterVerified, query]);

    const counts = useMemo(() => {
        const total = accounts.length;
        const verified = accounts.filter((a) => a.is_verified).length;
        return { total, verified, unverified: total - verified };
    }, [accounts]);

    const showToast = (msg) => {
        setToast(msg);
        window.setTimeout(() => setToast(''), 2200);
    };

    const toggleVerify = (idx) => {
        setAccounts((list) =>
            list.map((a, i) =>
                i === idx
                    ? { ...a, is_verified: !a.is_verified, updated_at: new Date().toISOString() }
                    : a
            )
        );
        const next = !accounts[idx].is_verified;
        showToast(next ? 'Đã xác thực tài khoản ngân hàng.' : 'Đã huỷ xác thực tài khoản ngân hàng.');
    };

    return (
        <div className="space-y-5">
            {toast ? (
                <div className="rounded-xl border border-emerald-200 bg-emerald-50 px-4 py-3 text-[12px] font-semibold text-emerald-800">
                    {toast}
                </div>
            ) : null}

            <div className="flex items-start justify-between gap-4">
                <div>
                    <h1 className="text-lg md:text-xl font-bold text-slate-900">
                        Xác thực tài khoản ngân hàng (Author)
                    </h1>
                    <p className="mt-1 text-[11px] text-slate-500">
                        FE demo: duyệt trạng thái xác thực cho các tài khoản trong bảng <span className="font-semibold">author_bank_accounts</span>.
                    </p>
                </div>
                <div className="flex items-center gap-2">
                    <span className="inline-flex items-center rounded-full bg-slate-100 px-2 py-1 text-[10px] font-semibold text-slate-700">
                        {counts.total} tổng
                    </span>
                    <span className="inline-flex items-center rounded-full bg-emerald-50 px-2 py-1 text-[10px] font-semibold text-emerald-700 ring-1 ring-emerald-200">
                        {counts.verified} verified
                    </span>
                    <span className="inline-flex items-center rounded-full bg-amber-50 px-2 py-1 text-[10px] font-semibold text-amber-700 ring-1 ring-amber-200">
                        {counts.unverified} unverified
                    </span>
                </div>
            </div>

            <section className="bg-white rounded-xl border border-slate-200 shadow-sm p-5 space-y-4">
                <div className="flex flex-wrap items-center gap-2">
                    {[
                        { id: 'ALL', label: 'Tất cả' },
                        { id: 'UNVERIFIED', label: 'Chưa xác thực' },
                        { id: 'VERIFIED', label: 'Đã xác thực' },
                    ].map((t) => (
                        <button
                            key={t.id}
                            type="button"
                            onClick={() => setFilterVerified(t.id)}
                            className={`rounded-full px-3 py-1.5 text-[11px] font-semibold ring-1 transition ${
                                filterVerified === t.id
                                    ? 'bg-primary/10 text-primary ring-primary/20'
                                    : 'bg-white text-slate-700 ring-slate-200 hover:bg-slate-50'
                            }`}
                        >
                            {t.label}
                        </button>
                    ))}

                    <input
                        value={query}
                        onChange={(e) => setQuery(e.target.value)}
                        placeholder="Tìm theo user_id, ngân hàng, số TK, chủ TK…"
                        className="ml-auto flex-1 min-w-[240px] rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary"
                    />
                    <button
                        type="button"
                        onClick={() => {
                            setQuery('');
                            setFilterVerified('ALL');
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
                                <th className="px-3 py-2 font-medium">User</th>
                                <th className="px-3 py-2 font-medium">Ngân hàng</th>
                                <th className="px-3 py-2 font-medium">Số TK</th>
                                <th className="px-3 py-2 font-medium">Chủ TK</th>
                                <th className="px-3 py-2 font-medium">Chi nhánh</th>
                                <th className="px-3 py-2 font-medium">Xác thực</th>
                                <th className="px-3 py-2 font-medium">Cập nhật</th>
                                <th className="px-3 py-2 font-medium text-right">Hành động</th>
                            </tr>
                        </thead>
                        <tbody className="bg-white">
                            {filtered.length === 0 ? (
                                <tr>
                                    <td className="px-3 py-10 text-center text-slate-500" colSpan={8}>
                                        Không có dữ liệu.
                                    </td>
                                </tr>
                            ) : (
                                filtered.map((a, idx) => {
                                    const isVerified = !!a.is_verified;
                                    return (
                                        <tr
                                            key={`${a.user_id}-${a.account_number}-${idx}`}
                                            className={`border-t border-slate-100 ${idx % 2 === 1 ? 'bg-slate-50/40' : ''}`}
                                        >
                                            <td className="px-3 py-2 text-slate-700 font-semibold">{a.user_id}</td>
                                            <td className="px-3 py-2 text-slate-700">{a.bank_name}</td>
                                            <td className="px-3 py-2 text-slate-700">{maskAccountNumber(a.account_number)}</td>
                                            <td className="px-3 py-2 text-slate-800 font-semibold">{a.account_holder_name}</td>
                                            <td className="px-3 py-2 text-slate-600">{a.branch_name || '-'}</td>
                                            <td className="px-3 py-2">
                                                {isVerified ? (
                                                    <span className="inline-flex items-center rounded-full bg-emerald-50 px-2 py-0.5 text-[10px] font-semibold text-emerald-700 ring-1 ring-emerald-200">
                                                        Verified
                                                    </span>
                                                ) : (
                                                    <span className="inline-flex items-center rounded-full bg-amber-50 px-2 py-0.5 text-[10px] font-semibold text-amber-700 ring-1 ring-amber-200">
                                                        Unverified
                                                    </span>
                                                )}
                                            </td>
                                            <td className="px-3 py-2 text-slate-600">{formatTime(a.updated_at)}</td>
                                            <td className="px-3 py-2 text-right">
                                                <button
                                                    type="button"
                                                    onClick={() => toggleVerify(accounts.indexOf(a))}
                                                    className={`rounded-lg border px-3 py-2 text-[11px] font-semibold ${
                                                        isVerified
                                                            ? 'border-amber-200 bg-amber-50 text-amber-700 hover:bg-amber-100'
                                                            : 'border-emerald-200 bg-emerald-50 text-emerald-700 hover:bg-emerald-100'
                                                    }`}
                                                >
                                                    {isVerified ? 'Huỷ xác thực' : 'Xác thực'}
                                                </button>
                                            </td>
                                        </tr>
                                    );
                                })
                            )}
                        </tbody>
                    </table>
                </div>
            </section>
        </div>
    );
}

