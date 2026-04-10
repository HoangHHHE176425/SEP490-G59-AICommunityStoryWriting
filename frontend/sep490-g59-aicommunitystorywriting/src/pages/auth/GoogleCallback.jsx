import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { useAuth } from '../../contexts/AuthContext';
import { AlertCircle, CheckCircle, BookOpen } from 'lucide-react';

export default function GoogleCallback() {
    const navigate = useNavigate();
    const { fetchProfile } = useAuth();
    const [searchParams] = useSearchParams();
    const accessToken = searchParams.get('accessToken');
    const callbackError = searchParams.get('error');
    const returnUrl = useMemo(() => searchParams.get('returnUrl') || '/home', [searchParams]);

    const [error, setError] = useState('');

    useEffect(() => {
        const run = async () => {
            if (callbackError) {
                localStorage.removeItem('accessToken');
                setError(callbackError);
                return;
            }

            if (!accessToken) {
                setError('Không nhận được access token từ Google.');
                return;
            }

            try {
                localStorage.setItem('accessToken', accessToken);
                await fetchProfile();
                navigate(returnUrl, { replace: true });
            } catch (e) {
                setError(e?.message || 'Đăng nhập Google thất bại.');
            }
        };

        run();
    }, [accessToken, callbackError, fetchProfile, navigate, returnUrl]);

    return (
        <div className="min-h-screen bg-background-light dark:bg-background-dark flex flex-col">
            <Header />
            <div className="flex-1 flex items-center justify-center px-4 py-12">
                <div className="w-full max-w-md">
                    <div className="bg-white dark:bg-slate-800 rounded-2xl shadow-xl p-8 border border-slate-200 dark:border-slate-700">
                        <div className="flex justify-center mb-6">
                            <div className="size-12 bg-primary rounded-lg flex items-center justify-center text-white shadow-lg">
                                <BookOpen className="w-7 h-7" />
                            </div>
                        </div>

                        <div className="text-center mb-6">
                            <h1 className="text-2xl font-bold text-slate-900 dark:text-white mb-2">
                                Đang đăng nhập bằng Google...
                            </h1>
                            <p className="text-slate-600 dark:text-slate-400">
                                Vui lòng chờ trong giây lát.
                            </p>
                        </div>

                        {!error ? (
                            <div className="mb-6 p-4 bg-emerald-50 dark:bg-emerald-950/30 border border-emerald-200 dark:border-emerald-800 rounded-lg flex items-center gap-3">
                                <CheckCircle className="w-5 h-5 text-green-600 dark:text-green-400 flex-shrink-0" />
                                <p className="text-sm text-green-600 dark:text-green-400">
                                    Đang xác thực...
                                </p>
                            </div>
                        ) : (
                            <div className="mb-6 p-4 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 rounded-lg flex items-center gap-3">
                                <AlertCircle className="w-5 h-5 text-red-600 dark:text-red-400 flex-shrink-0" />
                                <p className="text-sm text-red-600 dark:text-red-400">
                                    {error}
                                </p>
                            </div>
                        )}

                        <div className="mt-4 text-center text-xs text-slate-500 dark:text-slate-500">
                            Nếu trang không tự chuyển, hãy thử lại.
                        </div>
                    </div>
                </div>
            </div>
            <Footer />
        </div>
    );
}

