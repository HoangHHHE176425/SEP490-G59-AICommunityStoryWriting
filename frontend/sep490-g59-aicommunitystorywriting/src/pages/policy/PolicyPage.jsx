import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import { ArrowLeft, Shield, AlertCircle } from 'lucide-react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { getActivePolicy } from '../../api/policy/policyApi';
import { PolicyBody } from '../../components/policy/PolicyBody';

export default function PolicyPage() {
    const navigate = useNavigate();
    const [searchParams, setSearchParams] = useSearchParams();

    const type = useMemo(() => {
        const t = (searchParams.get('type') ?? 'USER').trim().toUpperCase();
        return t || 'USER';
    }, [searchParams]);

    const [policy, setPolicy] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

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

    return (
        <div className="w-full bg-white min-h-screen">
            <Header />
            
            {/* Page Header */}
            <div className="bg-gradient-to-r from-[#13EC5B] to-[#11D350] py-8">
                <div className="max-w-4xl mx-auto px-4">
                    <button
                        onClick={() => navigate(-1)}
                        className="mb-4 flex items-center gap-2 text-white hover:text-gray-100 transition-colors"
                    >
                        <ArrowLeft className="w-5 h-5" />
                        <span>Quay lại</span>
                    </button>
                    <div className="flex items-center gap-3">
                        <div className="w-12 h-12 bg-white rounded-xl flex items-center justify-center">
                            <Shield className="w-6 h-6 text-[#13EC5B]" />
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
            <div className="max-w-4xl mx-auto px-4 py-8">
                <div className="bg-white rounded-2xl shadow-lg p-6 md:p-8 space-y-6">
                    {/* Important Notice */}
                    <div className="bg-gradient-to-r from-[#FFF4E6] to-[#FFF9F0] border-l-4 border-[#FFA500] p-4 rounded-lg">
                        <div className="flex gap-3">
                            <AlertCircle className="w-5 h-5 text-[#FFA500] flex-shrink-0 mt-0.5" />
                            <div>
                                <p className="font-semibold text-[#1A2332] mb-1">Thông Báo Quan Trọng</p>
                                <p className="text-sm text-[#90A1B9] leading-relaxed">
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
                                onClick={() => setSearchParams({ type: opt.value })}
                                className={`px-3 py-1.5 rounded-full text-sm font-semibold transition-colors ${
                                    type === opt.value
                                        ? 'bg-emerald-500 text-white'
                                        : 'bg-slate-100 text-slate-600 hover:bg-slate-200'
                                }`}
                            >
                                {opt.label}
                            </button>
                        ))}
                    </div>

                    {loading ? (
                        <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
                            Đang tải policy...
                        </div>
                    ) : error ? (
                        <div className="rounded-xl border border-red-200 bg-red-50 p-4 text-sm text-red-800">
                            {error}
                        </div>
                    ) : !policy ? (
                        <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
                            Chưa có policy đang áp dụng cho loại <span className="font-semibold">{type}</span>. Vui lòng tạo policy (và bật active) trong database.
                        </div>
                    ) : (
                        <>
                            <div className="flex flex-wrap items-center gap-2 text-sm text-slate-600">
                                <span className="font-semibold text-slate-900">
                                    {policy?.type ?? type}
                                </span>
                                {policy?.version ? (
                                    <span className="rounded-full bg-slate-100 px-2 py-0.5">
                                        v{policy.version}
                                    </span>
                                ) : null}
                                {policy?.isActive === false ? (
                                    <span className="rounded-full bg-amber-100 text-amber-900 px-2 py-0.5">
                                        Không active
                                    </span>
                                ) : null}
                            </div>

                            <PolicyBody content={policy?.content} />
                        </>
                    )}
                </div>
            </div>

            <Footer />
        </div>
    );
}
