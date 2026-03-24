import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { ArrowLeft, Shield, AlertCircle, CheckCircle2, Clock3 } from 'lucide-react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { getActivePolicy, getMyAuthorPolicyStatus } from '../../api/policy/policyApi';
import { getAuthorOnboardingStatus } from '../../api/account/accountApi';
import { PolicyBody } from '../../components/policy/PolicyBody';
import { useAuth } from '../../contexts/AuthContext';

export default function PolicyPage() {
    const navigate = useNavigate();
    const [searchParams, setSearchParams] = useSearchParams();
    const { isAuthenticated, becomeAuthor } = useAuth();

    const fromBecomeAuthor = (searchParams.get('from') ?? '').toLowerCase() === 'become-author';
    const nextPath = searchParams.get('next') || '/author';

    const type = useMemo(() => {
        const t = (searchParams.get('type') ?? 'USER').trim().toUpperCase();
        return t || 'USER';
    }, [searchParams]);

    const [policy, setPolicy] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [onboarding, setOnboarding] = useState(null);
    const [authorPolicyStatus, setAuthorPolicyStatus] = useState(null);
    const [authorFlowLoading, setAuthorFlowLoading] = useState(false);
    const [submitError, setSubmitError] = useState('');
    const [submitSuccess, setSubmitSuccess] = useState('');
    const [submitting, setSubmitting] = useState(false);

    useEffect(() => {
        let alive = true;
        setLoading(true);
        setError(null);

        getActivePolicy(type)
            .then((p) => {
                if (!alive) return;
                setPolicy(p);
            })
            .catch((err) => {
                if (!alive) return;
                const data = err?.response?.data;
                const parts = [
                    data?.message,
                    data?.detail,
                    data?.inner,
                    !data?.message && !data?.detail && !data?.inner ? err?.message : null,
                ].filter(Boolean);
                setError(parts.join(' | ') || `Không tải được policy loại ${type}`);
                setPolicy(null);
            })
            .finally(() => {
                if (!alive) return;
                setLoading(false);
            });

        return () => {
            alive = false;
        };
    }, [type]);

    useEffect(() => {
        if (!fromBecomeAuthor || !isAuthenticated) {
            setOnboarding(null);
            setAuthorPolicyStatus(null);
            return;
        }

        let alive = true;
        setAuthorFlowLoading(true);
        setSubmitError('');
        setSubmitSuccess('');

        Promise.all([
            getAuthorOnboardingStatus(),
            getMyAuthorPolicyStatus('AUTHOR'),
        ])
            .then(([onboardingData, authorStatus]) => {
                if (!alive) return;
                setOnboarding(onboardingData);
                setAuthorPolicyStatus(authorStatus);
            })
            .catch((err) => {
                if (!alive) return;
                setSubmitError(
                    err?.response?.data?.message ||
                    err?.message ||
                    'Không tải được trạng thái đăng ký tác giả.'
                );
            })
            .finally(() => {
                if (!alive) return;
                setAuthorFlowLoading(false);
            });

        return () => {
            alive = false;
        };
    }, [fromBecomeAuthor, isAuthenticated]);

    const missingRequirements = onboarding?.missingRequirements ?? [];
    const canSubmitBecomeAuthor =
        !!isAuthenticated &&
        !!policy &&
        !loading &&
        !authorFlowLoading &&
        (onboarding?.isAuthor || onboarding?.canBecomeAuthor);

    const handleSwitchType = (value) => {
        const nextParams = new URLSearchParams(searchParams);
        nextParams.set('type', value);
        setSearchParams(nextParams);
    };

    const handleBecomeAuthor = async () => {
        if (!isAuthenticated) {
            navigate('/login');
            return;
        }

        if (onboarding?.isAuthor) {
            navigate(nextPath);
            return;
        }

        setSubmitting(true);
        setSubmitError('');
        setSubmitSuccess('');

        const result = await becomeAuthor(policy?.id);
        if (!result?.success) {
            setSubmitError(result?.message || 'Không thể đăng ký làm tác giả.');
            setSubmitting(false);
            return;
        }

        setSubmitSuccess('Đăng ký tác giả thành công. Hệ thống đã cập nhật quyền và token mới cho bạn.');

        try {
            const [onboardingData, authorStatus] = await Promise.all([
                getAuthorOnboardingStatus(),
                getMyAuthorPolicyStatus('AUTHOR'),
            ]);
            setOnboarding(onboardingData);
            setAuthorPolicyStatus(authorStatus);
        } catch {
            // Ignore refresh errors here because the role upgrade already succeeded.
        }

        setSubmitting(false);
        setTimeout(() => navigate(nextPath), 800);
    };

    return (
        <div className="min-h-screen bg-background-light dark:bg-background-dark flex flex-col">
            <Header />

            {/* Page Header */}
            <div className="bg-gradient-to-r from-emerald-500 to-emerald-600 py-8">
                <div className="max-w-[1280px] mx-auto px-4">
                    <button
                        onClick={() => navigate(-1)}
                        className="mb-4 flex items-center gap-2 text-white hover:text-gray-100 transition-colors"
                    >
                        <ArrowLeft className="w-5 h-5" />
                        <span>Quay lại</span>
                    </button>
                    <div className="flex items-center gap-3">
                        <div className="w-12 h-12 bg-white/95 rounded-xl flex items-center justify-center shadow-sm">
                            <Shield className="w-6 h-6 text-emerald-500" />
                        </div>
                        <div>
                            <h1 className="text-3xl font-bold text-white">
                                Điều Khoản & Chính Sách
                            </h1>
                            <p className="text-sm text-white/90 mt-1">
                                Thông tin chi tiết về các điều khoản và chính sách của CSW-AI
                            </p>
                        </div>
                    </div>
                </div>
            </div>

            {/* Content */}
            <div className="flex-1">
                <div className="max-w-[1280px] mx-auto px-4 py-8">
                    <div className="bg-white dark:bg-slate-800 rounded-2xl shadow-lg border border-slate-200 dark:border-slate-700 p-6 md:p-8 space-y-6">
                        {/* Important Notice */}
                        <div className="bg-amber-50 dark:bg-amber-950/20 border-l-4 border-amber-500 p-4 rounded-lg">
                            <div className="flex gap-3">
                                <AlertCircle className="w-5 h-5 text-amber-500 flex-shrink-0 mt-0.5" />
                                <div>
                                    <p className="font-semibold text-slate-900 dark:text-white mb-1">Thông Báo Quan Trọng</p>
                                    <p className="text-sm text-slate-600 dark:text-slate-300 leading-relaxed">
                                        Bằng việc tạo tài khoản và sử dụng dịch vụ CSW-AI, bạn xác nhận rằng bạn đã đọc kỹ, hiểu rõ
                                        và đồng ý tuân thủ tất cả các điều khoản, chính sách và quy định được nêu trong tài liệu này.
                                        Nếu bạn không đồng ý với bất kỳ điều khoản nào, vui lòng không sử dụng dịch vụ của chúng tôi.
                                    </p>
                                </div>
                            </div>
                        </div>

                        <div className="flex flex-wrap gap-2">
                            {[
                                { value: 'USER', label: 'Người dùng' },
                                { value: 'AUTHOR', label: 'Tác giả' },
                                { value: 'AI', label: 'AI' },
                                { value: 'DEFAULT', label: 'Mặc định' },
                            ].map((opt) => (
                                <button
                                    key={opt.value}
                                    type="button"
                                    onClick={() => handleSwitchType(opt.value)}
                                    className={`px-3 py-1.5 rounded-full text-sm font-semibold transition-colors ${
                                        type === opt.value
                                            ? 'bg-primary text-white'
                                            : 'bg-slate-100 dark:bg-slate-800 text-slate-600 dark:text-slate-300 hover:bg-slate-200 dark:hover:bg-slate-700'
                                    }`}
                                >
                                    {opt.label}
                                </button>
                            ))}
                        </div>

                        {loading ? (
                            <div className="rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900/40 p-4 text-sm text-slate-600 dark:text-slate-300">
                                Đang tải policy...
                            </div>
                        ) : error ? (
                            <div className="rounded-xl border border-red-200 dark:border-red-700 bg-red-50 dark:bg-red-950/40 p-4 text-sm text-red-800 dark:text-red-200">
                                {error}
                            </div>
                        ) : !policy ? (
                            <div className="rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900/40 p-4 text-sm text-slate-600 dark:text-slate-300">
                                Chưa có policy đang áp dụng cho loại <span className="font-semibold">{type}</span>. Vui lòng tạo policy (và bật active) trong
                                hệ thống quản trị.
                            </div>
                        ) : (
                            <>
                                <div className="flex flex-wrap items-center gap-2 text-sm text-slate-600 dark:text-slate-300">
                                    <span className="font-semibold text-slate-900 dark:text-white">
                                        {policy?.type ?? type}
                                    </span>
                                    {policy?.version ? (
                                        <span className="rounded-full bg-slate-100 dark:bg-slate-800 px-2 py-0.5">
                                            v{policy.version}
                                        </span>
                                    ) : null}
                                    {policy?.isActive === false ? (
                                        <span className="rounded-full bg-amber-100 dark:bg-amber-900/40 text-amber-900 dark:text-amber-200 px-2 py-0.5">
                                            Không active
                                        </span>
                                    ) : null}
                                </div>

                                <PolicyBody content={policy?.content} />
                            </>
                        )}

                        {fromBecomeAuthor && (
                            <div className="rounded-2xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900/40 p-5 space-y-4">
                                <div className="flex items-start gap-3">
                                    <Clock3 className="w-5 h-5 text-primary mt-0.5" />
                                    <div>
                                        <h3 className="text-lg font-bold text-slate-900 dark:text-white">
                                            Trạng thái đăng ký tác giả
                                        </h3>
                                        <p className="text-sm text-slate-600 dark:text-slate-300">
                                            Hoàn tất các điều kiện dưới đây rồi nhấn đồng ý để nâng tài khoản từ người dùng lên tác giả.
                                        </p>
                                    </div>
                                </div>

                                {authorFlowLoading ? (
                                    <div className="text-sm text-slate-600 dark:text-slate-300">
                                        Đang kiểm tra điều kiện đăng ký tác giả...
                                    </div>
                                ) : !isAuthenticated ? (
                                    <div className="rounded-xl border border-amber-200 dark:border-amber-700 bg-amber-50 dark:bg-amber-950/30 p-4 text-sm text-amber-800 dark:text-amber-200">
                                        Bạn cần đăng nhập trước khi đăng ký làm tác giả.
                                    </div>
                                ) : (
                                    <>
                                        <div className="grid gap-3 md:grid-cols-2">
                                            <div className="rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-4">
                                                <p className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">
                                                    Vai trò hiện tại
                                                </p>
                                                <p className="text-sm font-semibold text-slate-900 dark:text-white">
                                                    {onboarding?.currentRole || 'USER'}
                                                </p>
                                            </div>

                                            <div className="rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-4">
                                                <p className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">
                                                    Policy tác giả
                                                </p>
                                                <p className="text-sm font-semibold text-slate-900 dark:text-white">
                                                    {policy?.version ? `Phiên bản v${policy.version}` : 'Đang hiệu lực'}
                                                </p>
                                            </div>

                                            <div className="rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-4">
                                                <p className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">
                                                    Đã chấp nhận policy
                                                </p>
                                                <p className="text-sm font-semibold text-slate-900 dark:text-white">
                                                    {authorPolicyStatus?.hasAccepted || onboarding?.hasAcceptedActivePolicy ? 'Đã chấp nhận' : 'Chưa chấp nhận'}
                                                </p>
                                            </div>

                                            <div className="rounded-xl border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-800 p-4">
                                                <p className="text-xs font-bold uppercase tracking-wider text-slate-500 dark:text-slate-400 mb-1">
                                                    Trạng thái nâng cấp
                                                </p>
                                                <p className="text-sm font-semibold text-slate-900 dark:text-white">
                                                    {onboarding?.isAuthor ? 'Bạn đã là tác giả' : onboarding?.canBecomeAuthor ? 'Đủ điều kiện đăng ký' : 'Chưa đủ điều kiện'}
                                                </p>
                                            </div>
                                        </div>

                                        {missingRequirements.length > 0 && (
                                            <div className="rounded-xl border border-red-200 dark:border-red-700 bg-red-50 dark:bg-red-950/30 p-4">
                                                <p className="text-sm font-semibold text-red-800 dark:text-red-200 mb-2">
                                                    Bạn cần bổ sung các mục sau trước khi đăng ký:
                                                </p>
                                                <div className="space-y-2">
                                                    {missingRequirements.map((item) => (
                                                        <div key={item} className="flex items-start gap-2 text-sm text-red-700 dark:text-red-200">
                                                            <span className="mt-1 h-1.5 w-1.5 rounded-full bg-red-500" />
                                                            <span>{item}</span>
                                                        </div>
                                                    ))}
                                                </div>
                                            </div>
                                        )}

                                        {onboarding?.isAuthor && (
                                            <div className="rounded-xl border border-green-200 dark:border-green-700 bg-green-50 dark:bg-green-950/30 p-4 text-sm text-green-800 dark:text-green-200 flex items-start gap-2">
                                                <CheckCircle2 className="w-4 h-4 mt-0.5" />
                                                <span>Tài khoản của bạn đã là tác giả. Bạn có thể vào khu vực quản lý truyện ngay.</span>
                                            </div>
                                        )}
                                    </>
                                )}
                            </div>
                        )}

                        {submitError && (
                            <div className="rounded-xl border border-red-200 dark:border-red-700 bg-red-50 dark:bg-red-950/40 p-4 text-sm text-red-800 dark:text-red-200">
                                {submitError}
                            </div>
                        )}

                        {submitSuccess && (
                            <div className="rounded-xl border border-green-200 dark:border-green-700 bg-green-50 dark:bg-green-950/40 p-4 text-sm text-green-800 dark:text-green-200">
                                {submitSuccess}
                            </div>
                        )}

                        {fromBecomeAuthor && (
                            <div className="mt-8 pt-6 border-t border-slate-200 dark:border-slate-700">
                                <div className="flex flex-col sm:flex-row gap-3">
                                    <button
                                        type="button"
                                        onClick={() => navigate(-1)}
                                        className="flex-1 px-6 py-3 border-2 border-slate-300 dark:border-slate-600 text-slate-900 dark:text-slate-100 rounded-xl hover:bg-slate-50 dark:hover:bg-slate-700 transition-all font-semibold"
                                    >
                                        Hủy Bỏ
                                    </button>
                                    <button
                                        type="button"
                                        onClick={handleBecomeAuthor}
                                        disabled={!canSubmitBecomeAuthor || submitting}
                                        className="flex-1 px-6 py-3 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-xl hover:shadow-[0_0_20px_rgba(19,236,91,0.5)] transition-all font-bold disabled:opacity-60 disabled:cursor-not-allowed disabled:hover:shadow-none"
                                    >
                                        {submitting
                                            ? 'Đang xử lý...'
                                            : onboarding?.isAuthor
                                                ? 'Vào trang tác giả'
                                                : 'Tôi Đồng Ý Và Đăng Ký Tác Giả'}
                                    </button>
                                </div>
                            </div>
                        )}
                    </div>
                </div>
            </div>

            <Footer />
        </div>
    );
}
