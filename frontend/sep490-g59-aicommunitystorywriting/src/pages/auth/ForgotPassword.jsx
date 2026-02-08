import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { Mail, AlertCircle, CheckCircle, ArrowLeft, Lock, KeyRound, Eye, EyeOff } from 'lucide-react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';

export default function ForgotPassword() {
    const { forgotPassword, resetPassword } = useAuth();
    const [email, setEmail] = useState('');
    const [error, setError] = useState('');
    const [requestSent, setRequestSent] = useState(false);
    const [resetSuccess, setResetSuccess] = useState(false);
    const [loading, setLoading] = useState(false);
    const [step, setStep] = useState('request'); // 'request' | 'reset'

    const [resetData, setResetData] = useState({
        otpCode: '',
        newPassword: '',
        confirmPassword: '',
    });
    const [showPassword, setShowPassword] = useState(false);
    const [showConfirmPassword, setShowConfirmPassword] = useState(false);

    const handleSubmit = async (e) => {
        e.preventDefault();
        setError('');
        setRequestSent(false);
        setResetSuccess(false);

        if (!email) {
            setError('Vui lòng nhập email của bạn');
            return;
        }

        const emailRegex = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
        if (!emailRegex.test(email)) {
            setError('Email không hợp lệ');
            return;
        }

        setLoading(true);

        try {
            const result = await forgotPassword(email);
            if (result.success) {
                setRequestSent(true);
                setStep('reset');
            } else {
                setError(result.message || 'Đã xảy ra lỗi. Vui lòng thử lại.');
            }
        } catch (err) {
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
                        {/* Back to Login Link */}
                        <div className="mb-6">
                            <Link
                                to="/login"
                                className="inline-flex items-center gap-2 text-sm text-slate-600 dark:text-slate-400 hover:text-primary transition-colors"
                            >
                                <ArrowLeft className="w-4 h-4" />
                                Quay lại đăng nhập
                            </Link>
                        </div>

                        {/* App Icon */}
                        <div className="flex justify-center mb-6">
                            <div className="size-12 bg-blue-100 dark:bg-blue-900/30 rounded-lg flex items-center justify-center text-blue-600 dark:text-blue-400 shadow-lg">
                                <Mail className="w-7 h-7" />
                            </div>
                        </div>

                        {/* Header */}
                        <div className="text-center mb-8">
                            <h1 className="text-3xl font-bold text-slate-900 dark:text-white mb-2">
                                Quên Mật Khẩu
                            </h1>
                            <p className="text-slate-600 dark:text-slate-400">
                                Nhập email để nhận mã OTP đặt lại mật khẩu
                            </p>
                        </div>

                    {/* Success Message */}
                    {requestSent && (
                        <div className="mb-6 p-4 bg-green-50 dark:bg-green-950/30 border border-green-200 dark:border-green-800 rounded-lg">
                            <div className="flex items-start gap-3">
                                <CheckCircle className="w-5 h-5 text-green-600 dark:text-green-400 flex-shrink-0 mt-0.5" />
                                <div className="flex-1">
                                    <p className="text-sm font-semibold text-green-600 dark:text-green-400 mb-1">
                                        OTP đã được gửi!
                                    </p>
                                    <p className="text-sm text-green-600 dark:text-green-400">
                                        Vui lòng kiểm tra email để lấy mã OTP, sau đó nhập OTP và mật khẩu mới bên dưới.
                                    </p>
                                </div>
                            </div>
                        </div>
                    )}

                    {/* Error Message */}
                    {error && (
                        <div className="mb-6 p-4 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 rounded-lg flex items-center gap-3">
                            <AlertCircle className="w-5 h-5 text-red-600 dark:text-red-400 flex-shrink-0" />
                            <p className="text-sm text-red-600 dark:text-red-400">{error}</p>
                        </div>
                    )}

                    {/* Form */}
                    {step === 'request' ? (
                        <form onSubmit={handleSubmit} className="space-y-6">
                            <div>
                                <label
                                    htmlFor="email"
                                    className="block text-sm font-semibold text-slate-700 dark:text-slate-300 mb-2"
                                >
                                    Email
                                </label>
                                <div className="relative">
                                    <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                        <Mail className="w-5 h-5 text-slate-400" />
                                    </div>
                                    <input
                                        id="email"
                                        name="email"
                                        type="email"
                                        value={email}
                                        onChange={(e) => {
                                            setEmail(e.target.value);
                                            setError('');
                                        }}
                                        className="block w-full pl-10 pr-4 py-3 bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-900 dark:text-white placeholder-slate-400 focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all outline-none"
                                        placeholder="email@example.com"
                                        required
                                    />
                                </div>
                            </div>

                            <button
                                type="submit"
                                disabled={loading}
                                className="w-full py-3 bg-primary text-white font-bold rounded-lg hover:bg-primary/90 transition-all disabled:opacity-50 disabled:cursor-not-allowed shadow-lg shadow-primary/25"
                            >
                                {loading ? 'Đang gửi...' : 'Gửi Email Đặt Lại'}
                            </button>

                            {/* Info Message */}
                            <div className="flex items-start gap-2 text-sm text-slate-500 dark:text-slate-400">
                                <svg className="w-5 h-5 flex-shrink-0 mt-0.5" fill="none" stroke="currentColor" viewBox="0 0 24 24">
                                    <path strokeLinecap="round" strokeLinejoin="round" strokeWidth={2} d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
                                </svg>
                                <p>
                                    Bạn sẽ nhận được email chứa mã OTP để đặt lại mật khẩu.
                                </p>
                            </div>
                        </form>
                    ) : (
                        <form
                            onSubmit={async (e) => {
                                e.preventDefault();
                                setError('');
                                setResetSuccess(false);

                                if (!resetData.otpCode || !resetData.newPassword || !resetData.confirmPassword) {
                                    setError('Vui lòng điền đầy đủ thông tin');
                                    return;
                                }
                                if (resetData.newPassword.length < 6) {
                                    setError('Mật khẩu mới phải có ít nhất 6 ký tự');
                                    return;
                                }
                                if (resetData.newPassword !== resetData.confirmPassword) {
                                    setError('Mật khẩu xác nhận không khớp');
                                    return;
                                }

                                setLoading(true);
                                try {
                                    const res = await resetPassword(
                                        email,
                                        resetData.otpCode,
                                        resetData.newPassword,
                                        resetData.confirmPassword
                                    );
                                    if (res.success) {
                                        setResetSuccess(true);
                                    } else {
                                        setError(res.message || 'Đặt lại mật khẩu thất bại');
                                    }
                                } catch {
                                    setError('Đã xảy ra lỗi. Vui lòng thử lại.');
                                } finally {
                                    setLoading(false);
                                }
                            }}
                            className="space-y-4"
                        >
                            <div className="p-4 bg-slate-50 dark:bg-slate-700 rounded-lg">
                                <p className="text-sm text-slate-600 dark:text-slate-400 mb-2">
                                    <strong className="text-slate-900 dark:text-white">Lưu ý:</strong>
                                </p>
                                <ul className="text-sm text-slate-600 dark:text-slate-400 space-y-1 list-disc list-inside">
                                    <li>Kiểm tra cả thư mục Spam/Junk nếu không thấy email</li>
                                    <li>Mã OTP có thể hết hạn sau một thời gian</li>
                                    <li>Nếu vẫn không nhận được email, vui lòng thử lại sau vài phút</li>
                                </ul>
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
                                        type="text"
                                        value={resetData.otpCode}
                                        onChange={(e) => setResetData((p) => ({ ...p, otpCode: e.target.value }))}
                                        className="block w-full pl-10 pr-4 py-3 bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-900 dark:text-white placeholder-slate-400 focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all outline-none"
                                        placeholder="Nhập OTP"
                                        required
                                    />
                                </div>
                            </div>

                            <div>
                                <label className="block text-sm font-semibold text-slate-700 dark:text-slate-300 mb-2">
                                    Mật khẩu mới
                                </label>
                                <div className="relative">
                                    <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                        <Lock className="w-5 h-5 text-slate-400" />
                                    </div>
                                    <input
                                        type={showPassword ? 'text' : 'password'}
                                        value={resetData.newPassword}
                                        onChange={(e) => setResetData((p) => ({ ...p, newPassword: e.target.value }))}
                                        className="block w-full pl-10 pr-12 py-3 bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-900 dark:text-white placeholder-slate-400 focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all outline-none"
                                        placeholder="Tối thiểu 6 ký tự"
                                        required
                                    />
                                    <button
                                        type="button"
                                        onClick={() => setShowPassword(!showPassword)}
                                        className="absolute inset-y-0 right-0 pr-3 flex items-center text-slate-400 hover:text-slate-600 dark:hover:text-slate-300"
                                    >
                                        {showPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                                    </button>
                                </div>
                            </div>

                            <div>
                                <label className="block text-sm font-semibold text-slate-700 dark:text-slate-300 mb-2">
                                    Xác nhận mật khẩu
                                </label>
                                <div className="relative">
                                    <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                        <Lock className="w-5 h-5 text-slate-400" />
                                    </div>
                                    <input
                                        type={showConfirmPassword ? 'text' : 'password'}
                                        value={resetData.confirmPassword}
                                        onChange={(e) => setResetData((p) => ({ ...p, confirmPassword: e.target.value }))}
                                        className="block w-full pl-10 pr-12 py-3 bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-900 dark:text-white placeholder-slate-400 focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all outline-none"
                                        required
                                    />
                                    <button
                                        type="button"
                                        onClick={() => setShowConfirmPassword(!showConfirmPassword)}
                                        className="absolute inset-y-0 right-0 pr-3 flex items-center text-slate-400 hover:text-slate-600 dark:hover:text-slate-300"
                                    >
                                        {showConfirmPassword ? <EyeOff className="w-5 h-5" /> : <Eye className="w-5 h-5" />}
                                    </button>
                                </div>
                            </div>

                            <button
                                type="submit"
                                disabled={loading}
                                className="w-full py-3 bg-primary text-white font-bold rounded-lg hover:bg-primary/90 transition-all disabled:opacity-50 disabled:cursor-not-allowed shadow-lg shadow-primary/25"
                            >
                                {loading ? 'Đang xử lý...' : 'Đặt lại mật khẩu'}
                            </button>

                            <button
                                type="button"
                                onClick={() => {
                                    setStep('request');
                                    setRequestSent(false);
                                    setResetSuccess(false);
                                    setResetData({ otpCode: '', newPassword: '', confirmPassword: '' });
                                }}
                                className="w-full py-3 bg-slate-100 dark:bg-slate-700 text-slate-700 dark:text-slate-300 font-semibold rounded-lg hover:bg-slate-200 dark:hover:bg-slate-600 transition-all"
                            >
                                Gửi lại OTP
                            </button>

                            {resetSuccess && (
                                <div className="text-center text-sm text-green-600 dark:text-green-400 font-semibold">
                                    Đặt lại mật khẩu thành công. Bạn có thể{' '}
                                    <Link to="/login" className="text-primary hover:text-primary/80">
                                        đăng nhập
                                    </Link>
                                    .
                                </div>
                            )}
                        </form>
                    )}

                </div>
            </div>
            </div>
            <Footer />
        </div>
    );
}
