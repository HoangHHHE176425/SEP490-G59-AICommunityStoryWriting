import { useNavigate } from 'react-router-dom';
import { ArrowLeft, Shield, FileText, AlertCircle } from 'lucide-react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { PolicyContent } from '../../components/PolicyContent';

export default function PolicyPage() {
    const navigate = useNavigate();

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

                    <PolicyContent />
                </div>
            </div>

            <Footer />
        </div>
    );
}
