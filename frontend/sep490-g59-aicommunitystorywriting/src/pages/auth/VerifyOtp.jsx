import { useMemo, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { AlertCircle, CheckCircle, KeyRound, Mail, ShieldCheck } from 'lucide-react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { useAuth } from '../../contexts/AuthContext';

function useQuery() {
    const { search } = useLocation();
    return useMemo(() => new URLSearchParams(search), [search]);
}

export default function VerifyOtp() {
    const navigate = useNavigate();
    const query = useQuery();
    const { verifyOtp } = useAuth();

    const [formData, setFormData] = useState({
        email: query.get('email') || '',
        otpCode: '',
    });
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState(false);

    const handleChange = (e) => {
        setFormData((prev) => ({ ...prev, [e.target.name]: e.target.value }));
        setError('');
    };

    const handleSubmit = async (e) => {
        e.preventDefault();
        setError('');
        setSuccess(false);

        if (!formData.email || !formData.otpCode) {
            setError('Vui lòng nhập email và mã OTP');
            return;
        }

        setLoading(true);
        try {
            const res = await verifyOtp(formData.email, formData.otpCode);
            if (res.success) {
                setSuccess(true);
                setTimeout(() => navigate(`/login`), 1200);
            } else {
                setError(res.message || 'Xác thực OTP thất bại');
            }
        } catch {
            setError('Đã xảy ra lỗi. Vui lòng thử lại.');
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="min-h-screen bg-background-light dark:bg-background-dark flex flex-col">
            <Header />
            <div className="flex-1 flex items-center justify-center px-4 py-12">
                <div className="w-full max-w-md">
                    <div className="bg-white dark:bg-slate-800 rounded-2xl shadow-xl p-8 border border-slate-200 dark:border-slate-700">
                        <div className="flex justify-center mb-6">
                            <div className="size-12 bg-primary rounded-lg flex items-center justify-center text-white shadow-lg">
                                <ShieldCheck className="w-7 h-7" />
                            </div>
                        </div>

                        <div className="text-center mb-8">
                            <h1 className="text-3xl font-bold text-slate-900 dark:text-white mb-2">
                                Xác Thực OTP
                            </h1>
                            <p className="text-slate-600 dark:text-slate-400">
                                Nhập mã OTP đã được gửi về email của bạn
                            </p>
                        </div>

                        {success && (
                            <div className="mb-6 p-4 bg-green-50 dark:bg-green-950/30 border border-green-200 dark:border-green-800 rounded-lg flex items-center gap-3">
                                <CheckCircle className="w-5 h-5 text-green-600 dark:text-green-400 flex-shrink-0" />
                                <p className="text-sm text-green-600 dark:text-green-400">
                                    Xác thực thành công! Đang chuyển sang đăng nhập...
                                </p>
                            </div>
                        )}

                        {error && (
                            <div className="mb-6 p-4 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 rounded-lg flex items-center gap-3">
                                <AlertCircle className="w-5 h-5 text-red-600 dark:text-red-400 flex-shrink-0" />
                                <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
                            </div>
                        )}

                        <form onSubmit={handleSubmit} className="space-y-4">
                            <div>
                                <label className="block text-sm font-semibold text-slate-700 dark:text-slate-300 mb-2">
                                    Email
                                </label>
                                <div className="relative">
                                    <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                        <Mail className="w-5 h-5 text-slate-400" />
                                    </div>
                                    <input
                                        name="email"
                                        type="email"
                                        value={formData.email}
                                        onChange={handleChange}
                                        className="block w-full pl-10 pr-4 py-3 bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-900 dark:text-white placeholder-slate-400 focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all outline-none"
                                        placeholder="email@example.com"
                                        required
                                    />
                                </div>
                            </div>

                            <div>
                                <label className="block text-sm font-semibold text-slate-700 dark:text-slate-300 mb-2">
                                    Mã OTP
                                </label>
                                <div className="relative">
                                    <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                        <KeyRound className="w-5 h-5 text-slate-400" />
                                    </div>
                                    <input
                                        name="otpCode"
                                        type="text"
                                        value={formData.otpCode}
                                        onChange={handleChange}
                                        className="block w-full pl-10 pr-4 py-3 bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-900 dark:text-white placeholder-slate-400 focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all outline-none"
                                        placeholder="Nhập OTP (ví dụ: 123456)"
                                        required
                                    />
                                </div>
                            </div>

                            <button
                                type="submit"
                                disabled={loading}
                                className="w-full py-3 bg-primary text-white font-bold rounded-lg hover:bg-primary/90 transition-all disabled:opacity-50 disabled:cursor-not-allowed shadow-lg shadow-primary/25"
                            >
                                {loading ? 'Đang xác thực...' : 'Xác thực'}
                            </button>
                        </form>

                        <div className="mt-6 text-center">
                            <p className="text-sm text-slate-600 dark:text-slate-400">
                                Đã xác thực rồi?{' '}
                                <Link
                                    to="/login"
                                    className="font-semibold text-primary hover:text-primary/80 transition-colors"
                                >
                                    Đăng nhập
                                </Link>
                            </p>
                        </div>

                        <div className="mt-4 text-center text-xs text-slate-500 dark:text-slate-500 flex items-center justify-center gap-2">
                            <AlertCircle className="w-4 h-4" />
                            <span>OTP có thể nằm trong mục Spam/Junk.</span>
                        </div>
                    </div>
                </div>
            </div>
            <Footer />
        </div>
    );
}

