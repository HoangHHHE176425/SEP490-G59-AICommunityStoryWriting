import { useEffect, useMemo, useState } from 'react';
import { X, Shield, AlertCircle } from 'lucide-react';
import { getActivePolicy } from '../api/policy/policyApi';
import { PolicyBody } from './policy/PolicyBody';

export function PolicyModal({ isOpen, onClose, onAccept, onDecline, type: typeProp = 'USER' }) {
    if (!isOpen) return null;

    const type = useMemo(() => (typeProp ?? 'USER').trim().toUpperCase() || 'USER', [typeProp]);
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
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
            <div className="bg-white rounded-2xl shadow-2xl max-w-4xl w-full max-h-[90vh] flex flex-col">
                {/* Header */}
                <div className="flex items-center justify-between p-6 border-b border-gray-200">
                    <div className="flex items-center gap-3">
                        <div className="w-12 h-12 bg-gradient-to-br from-[#13EC5B] to-[#11D350] rounded-xl flex items-center justify-center">
                            <Shield className="w-6 h-6 text-white" />
                        </div>
                        <div>
                            <h2 className="text-2xl font-bold text-[#1A2332]">
                                Điều Khoản & Chính Sách
                            </h2>
                            <p className="text-sm text-[#90A1B9]">
                                Vui lòng đọc và đồng ý trước khi tiếp tục
                            </p>
                        </div>
                    </div>
                    {onClose && (
                        <button
                            onClick={onClose}
                            className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
                        >
                            <X className="w-6 h-6 text-[#90A1B9]" />
                        </button>
                    )}
                </div>

                {/* Content */}
                <div className="flex-1 overflow-y-auto p-6 space-y-6">
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
                                <span className="font-semibold text-slate-900">{policy?.type ?? type}</span>
                                {policy?.version ? (
                                    <span className="rounded-full bg-slate-100 px-2 py-0.5">v{policy.version}</span>
                                ) : null}
                            </div>
                            <PolicyBody content={policy?.content} />
                        </>
                    )}
                </div>

                {/* Footer Actions */}
                <div className="p-6 border-t border-gray-200 bg-gray-50">
                    <div className="flex flex-col sm:flex-row gap-3">
                        <button
                            type="button"
                            onClick={onDecline}
                            className="flex-1 px-6 py-3 border-2 border-gray-300 text-[#1A2332] rounded-xl hover:bg-gray-100 transition-all font-semibold"
                        >
                            Hủy Bỏ
                        </button>
                        <button
                            type="button"
                            onClick={onAccept}
                            className="flex-1 px-6 py-3 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-xl hover:shadow-[0_0_20px_rgba(19,236,91,0.5)] transition-all font-bold"
                        >
                            Tôi Đồng Ý Các Điều Khoản
                        </button>
                    </div>
                    <p className="text-xs text-[#90A1B9] text-center mt-4">
                        Bằng việc nhấn &quot;Tôi Đồng Ý&quot;, bạn xác nhận đã đọc và chấp nhận tất cả các điều khoản trên
                    </p>
                </div>
            </div>
        </div>
    );
}
