import { useEffect, useMemo, useState } from 'react';
import { UserPlus, X } from 'lucide-react';

const ROLE_OPTIONS = [
    { value: 'USER', label: 'Người dùng' },
    { value: 'AUTHOR', label: 'Tác giả' },
    { value: 'MODERATOR', label: 'Kiểm duyệt' },
    { value: 'ADMIN', label: 'Quản trị' },
    { value: 'COMPLIANCE', label: 'Compliance' },
];

const STATUS_OPTIONS = [
    { value: 'ACTIVE', label: 'Hoạt động' },
    { value: 'INACTIVE', label: 'Không hoạt động' },
    { value: 'PENDING', label: 'Chờ xác thực' },
    { value: 'BANNED', label: 'Đã khóa' },
];

const EMPTY_FORM = {
    email: '',
    password: '',
    role: 'USER',
    status: 'ACTIVE',
    nickname: '',
};

function validateForm(form) {
    if (!form.email.trim()) return 'Email là bắt buộc.';
    const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
    if (!emailRegex.test(form.email.trim())) return 'Email không đúng định dạng.';
    if (!form.password) return 'Mật khẩu là bắt buộc.';
    if (form.password.length < 6) return 'Mật khẩu phải có ít nhất 6 ký tự.';
    return '';
}

export function CreateUserModal({ open, onClose, onSubmit }) {
    const [form, setForm] = useState(EMPTY_FORM);
    const [submitting, setSubmitting] = useState(false);
    const [error, setError] = useState('');

    useEffect(() => {
        if (!open) return;
        setForm(EMPTY_FORM);
        setSubmitting(false);
        setError('');
    }, [open]);

    const formError = useMemo(() => validateForm(form), [form]);

    if (!open) return null;

    const handleChange = (field) => (e) => {
        const value = e.target.value;
        setForm((prev) => ({ ...prev, [field]: value }));
        if (error) setError('');
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        if (formError) {
            setError(formError);
            return;
        }
        setSubmitting(true);
        setError('');
        try {
            await onSubmit?.({
                email: form.email.trim(),
                password: form.password,
                role: form.role,
                status: form.status,
                nickname: form.nickname.trim() || undefined,
            });
            onClose?.();
        } catch (e2) {
            const msg = e2?.response?.data?.message || e2?.message || 'Không thể tạo tài khoản.';
            setError(String(msg));
        } finally {
            setSubmitting(false);
        }
    };

    return (
        <div
            className="fixed inset-0 z-[110] flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm"
            role="dialog"
            aria-modal="true"
            aria-labelledby="create-user-title"
            onClick={() => {
                if (!submitting) onClose?.();
            }}
        >
            <div
                className="bg-white rounded-2xl shadow-xl max-w-2xl w-full border border-slate-200 overflow-hidden"
                onClick={(e) => e.stopPropagation()}
            >
                <div className="flex items-center justify-between p-4 border-b border-slate-200">
                    <h3 id="create-user-title" className="text-lg font-bold text-slate-900 flex items-center gap-2">
                        <UserPlus className="h-5 w-5 text-emerald-600" />
                        Tạo tài khoản mới
                    </h3>
                    <button
                        type="button"
                        onClick={() => {
                            if (!submitting) onClose?.();
                        }}
                        className="p-2 rounded-lg hover:bg-slate-100 text-slate-500 hover:text-slate-700"
                        disabled={submitting}
                        aria-label="Đóng"
                    >
                        <X className="h-5 w-5" />
                    </button>
                </div>

                <form onSubmit={handleSubmit} className="p-6 space-y-4">
                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                        <label className="block">
                            <span className="text-sm font-medium text-slate-700">Email *</span>
                            <input
                                type="email"
                                value={form.email}
                                onChange={handleChange('email')}
                                className="mt-1 w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-emerald-500/30 focus:border-emerald-500"
                                placeholder="example@email.com"
                                autoComplete="off"
                            />
                        </label>
                        <label className="block">
                            <span className="text-sm font-medium text-slate-700">Mật khẩu *</span>
                            <input
                                type="password"
                                value={form.password}
                                onChange={handleChange('password')}
                                className="mt-1 w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-emerald-500/30 focus:border-emerald-500"
                                placeholder="Tối thiểu 6 ký tự"
                                autoComplete="new-password"
                            />
                        </label>
                    </div>

                    <div className="grid grid-cols-1 sm:grid-cols-2 gap-4">
                        <label className="block">
                            <span className="text-sm font-medium text-slate-700">Vai trò</span>
                            <select
                                value={form.role}
                                onChange={handleChange('role')}
                                className="mt-1 w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-emerald-500/30 focus:border-emerald-500"
                            >
                                {ROLE_OPTIONS.map((option) => (
                                    <option key={option.value} value={option.value}>
                                        {option.label}
                                    </option>
                                ))}
                            </select>
                        </label>
                        <label className="block">
                            <span className="text-sm font-medium text-slate-700">Trạng thái</span>
                            <select
                                value={form.status}
                                onChange={handleChange('status')}
                                className="mt-1 w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-emerald-500/30 focus:border-emerald-500"
                            >
                                {STATUS_OPTIONS.map((option) => (
                                    <option key={option.value} value={option.value}>
                                        {option.label}
                                    </option>
                                ))}
                            </select>
                        </label>
                    </div>

                    <label className="block">
                        <span className="text-sm font-medium text-slate-700">Biệt danh (không bắt buộc)</span>
                        <input
                            type="text"
                            value={form.nickname}
                            onChange={handleChange('nickname')}
                            className="mt-1 w-full px-3 py-2 border border-slate-200 rounded-lg text-sm focus:ring-2 focus:ring-emerald-500/30 focus:border-emerald-500"
                            placeholder="Tên hiển thị"
                            autoComplete="off"
                        />
                    </label>

                    {error ? <div className="rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">{error}</div> : null}

                    <div className="pt-2 flex flex-col-reverse sm:flex-row gap-2 sm:justify-end">
                        <button
                            type="button"
                            onClick={() => onClose?.()}
                            disabled={submitting}
                            className="px-4 py-2.5 rounded-xl border border-slate-300 font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-50"
                        >
                            Hủy
                        </button>
                        <button
                            type="submit"
                            disabled={submitting}
                            className="px-4 py-2.5 rounded-xl bg-emerald-600 text-white font-semibold hover:bg-emerald-700 disabled:opacity-50"
                        >
                            {submitting ? 'Đang tạo…' : 'Tạo tài khoản'}
                        </button>
                    </div>
                </form>
            </div>
        </div>
    );
}
