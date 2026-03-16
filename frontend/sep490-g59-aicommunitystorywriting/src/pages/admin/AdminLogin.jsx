import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { Mail, Lock, Eye, EyeOff, AlertCircle, ShieldCheck, ArrowLeft } from 'lucide-react';

export function AdminLogin() {
    const navigate = useNavigate();
    const { login } = useAuth();
    const [formData, setFormData] = useState({ email: '', password: '' });
    const [showPassword, setShowPassword] = useState(false);
    const [error, setError] = useState('');
    const [loading, setLoading] = useState(false);

    const handleChange = (e) => {
        setFormData({ ...formData, [e.target.name]: e.target.value });
        setError('');
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setError('');
        setLoading(true);
        if (!formData.email || !formData.password) {
            setError('Vui lòng điền đầy đủ email và mật khẩu.');
            setLoading(false);
            return;
        }
        try {
            const result = await login(formData.email, formData.password);
            if (result.success) {
                navigate('/admin', { replace: true });
            } else {
                setError(result.message || 'Đăng nhập thất bại. Kiểm tra lại email và mật khẩu.');
            }
        } catch (err) {
            setError('Đã xảy ra lỗi. Vui lòng thử lại.');
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="min-h-screen flex">
            {/* Left: branding (ẩn trên mobile) */}
            <div className="hidden lg:flex lg:w-1/2 bg-slate-900 text-white flex-col justify-center px-12 xl:px-20">
                <div className="flex items-center gap-3 mb-6">
                    <div className="w-12 h-12 rounded-xl bg-emerald-500/20 flex items-center justify-center">
                        <ShieldCheck className="w-6 h-6 text-emerald-400" />
                    </div>
                    <span className="text-xl font-bold tracking-tight">Admin Panel</span>
                </div>
                <h1 className="text-2xl xl:text-3xl font-bold text-white mb-3">
                    Đăng nhập quản trị
                </h1>
                <p className="text-slate-400 text-sm max-w-sm">
                    Dành cho Quản trị viên (Admin), Kiểm duyệt (MODERATOR) và Compliance. Sử dụng email và mật khẩu đã được cấp.
                </p>
            </div>

            {/* Right: form */}
            <div className="flex-1 flex items-center justify-center p-6 bg-slate-50">
                <div className="w-full max-w-md">
                    {/* Back to site */}
                    <Link
                        to="/home"
                        className="inline-flex items-center gap-2 text-sm text-slate-600 hover:text-slate-900 mb-8"
                    >
                        <ArrowLeft className="w-4 h-4" />
                        Quay lại trang chủ
                    </Link>

                    <div className="bg-white rounded-2xl shadow-lg border border-slate-200 p-8">
                        <div className="lg:hidden flex items-center gap-2 mb-6">
                            <div className="w-10 h-10 rounded-lg bg-primary/10 flex items-center justify-center">
                                <ShieldCheck className="w-5 h-5 text-primary" />
                            </div>
                            <span className="font-semibold text-slate-900">Đăng nhập quản trị</span>
                        </div>

                        <h2 className="text-xl font-bold text-slate-900 mb-1 lg:mb-2">
                            Đăng nhập quản trị
                        </h2>
                        <p className="text-sm text-slate-500 mb-6">
                            Nhập email và mật khẩu tài khoản quản trị.
                        </p>

                        {error && (
                            <div className="mb-6 p-4 bg-red-50 border border-red-200 rounded-lg flex items-center gap-3">
                                <AlertCircle className="w-5 h-5 text-red-600 flex-shrink-0" />
                                <p className="text-sm text-red-700">{error}</p>
                            </div>
                        )}

                        <form onSubmit={handleSubmit} className="space-y-4">
                            <div>
                                <label htmlFor="admin-email" className="block text-sm font-medium text-slate-700 mb-1">
                                    Email
                                </label>
                                <div className="relative">
                                    <Mail className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-400" />
                                    <input
                                        id="admin-email"
                                        name="email"
                                        type="email"
                                        value={formData.email}
                                        onChange={handleChange}
                                        className="w-full pl-10 pr-4 py-3 border border-slate-300 rounded-lg text-slate-900 placeholder-slate-400 focus:ring-2 focus:ring-primary/40 focus:border-primary outline-none"
                                        placeholder="admin@example.com"
                                        required
                                    />
                                </div>
                            </div>

                            <div>
                                <label htmlFor="admin-password" className="block text-sm font-medium text-slate-700 mb-1">
                                    Mật khẩu
                                </label>
                                <div className="relative">
                                    <Lock className="absolute left-3 top-1/2 -translate-y-1/2 w-5 h-5 text-slate-400" />
                                    <input
                                        id="admin-password"
                                        name="password"
                                        type={showPassword ? 'text' : 'password'}
                                        value={formData.password}
                                        onChange={handleChange}
                                        className="w-full pl-10 pr-12 py-3 border border-slate-300 rounded-lg text-slate-900 placeholder-slate-400 focus:ring-2 focus:ring-primary/40 focus:border-primary outline-none"
                                        placeholder="••••••••"
                                        required
                                    />
                                    <button
                                        type="button"
                                        onClick={() => setShowPassword(!showPassword)}
                                        className="absolute right-3 top-1/2 -translate-y-1/2 text-slate-400 hover:text-slate-600"
                                    >
                                        {showPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                                    </button>
                                </div>
                            </div>

                            <div className="flex justify-end">
                                <Link
                                    to="/forgot-password"
                                    className="text-sm font-medium text-primary hover:text-primary/80"
                                >
                                    Quên mật khẩu?
                                </Link>
                            </div>

                            <button
                                type="submit"
                                disabled={loading}
                                className="w-full py-3 bg-primary text-white font-semibold rounded-lg hover:bg-primary/90 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
                            >
                                {loading ? 'Đang đăng nhập...' : 'Đăng nhập quản trị'}
                            </button>
                        </form>

                        <p className="mt-6 text-center text-sm text-slate-500">
                            Tài khoản thường?{' '}
                            <Link to="/login" className="font-medium text-primary hover:text-primary/80">
                                Đăng nhập tại đây
                            </Link>
                        </p>
                    </div>
                </div>
            </div>
        </div>
    );
}
