import { PenTool, BookOpen, Sparkles, Award, Brain, ArrowRight } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import { useNavigate } from 'react-router-dom';

export function CTASection() {
  const { isAuthenticated } = useAuth();
  const navigate = useNavigate();

  const features = [
    { icon: Brain, text: 'AI hỗ trợ viết truyện', color: 'from-[#13EC5B] to-[#11D350]' },
    { icon: Sparkles, text: 'Cộng đồng sáng tạo', color: 'from-[#2B7FFF] to-[#1E5FCC]' },
    { icon: Award, text: 'Kiếm tiền từ đam mê', color: 'from-[#9D4EDD] to-[#7B2CBF]' },
  ];

  return (
    <section className="bg-gradient-to-br from-[#13EC5B] via-[#2B7FFF] to-[#9D4EDD] py-20 mt-12 relative overflow-hidden">
      {/* Decorative Background Elements */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute top-0 right-0 w-[600px] h-[600px] bg-[#2B7FFF]/20 rounded-full blur-3xl"></div>
        <div className="absolute bottom-0 left-0 w-[500px] h-[500px] bg-[#9D4EDD]/15 rounded-full blur-3xl"></div>
      </div>

      <div className="max-w-[1400px] mx-auto px-6 relative z-10">
        <div className="text-center max-w-[800px] mx-auto">
          {/* Icons */}
          <div className="flex items-center justify-center gap-3 mb-6">
            <div className="w-16 h-16 bg-gradient-to-br from-[#13EC5B] to-[#11D350] backdrop-blur-sm rounded-2xl flex items-center justify-center shadow-lg">
              <PenTool className="w-8 h-8 text-white" />
            </div>
            <div className="w-16 h-16 bg-gradient-to-br from-[#2B7FFF] to-[#1E5FCC] backdrop-blur-sm rounded-2xl flex items-center justify-center shadow-lg">
              <BookOpen className="w-8 h-8 text-white" />
            </div>
            <div className="w-16 h-16 bg-gradient-to-br from-[#9D4EDD] to-[#7B2CBF] backdrop-blur-sm rounded-2xl flex items-center justify-center shadow-lg">
              <Brain className="w-8 h-8 text-white" />
            </div>
          </div>

          {/* Title */}
          <h2 className="text-white font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[48px] leading-tight mb-4">
            Bắt Đầu Hành Trình Sáng Tạo
          </h2>

          {/* Description */}
          <p className="text-white/95 font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[19px] mb-6 leading-relaxed">
            Tham gia cộng đồng hơn 12,000 tác giả, viết truyện với công cụ hỗ trợ AI và kiếm tiền từ đam mê của bạn.
          </p>

          {/* Features */}
          <div className="flex items-center justify-center gap-6 mb-10 flex-wrap">
            {features.map((feature, index) => {
              const Icon = feature.icon;
              return (
                <div key={index} className="flex items-center gap-2 px-4 py-2 bg-white/10 backdrop-blur-sm rounded-lg border border-white/20">
                  <div className={`w-6 h-6 bg-gradient-to-br ${feature.color} rounded flex items-center justify-center`}>
                    <Icon className="w-3.5 h-3.5 text-white" />
                  </div>
                  <span className="text-white font-['Plus_Jakarta_Sans',sans-serif] font-medium text-[14px]">
                    {feature.text}
                  </span>
                </div>
              );
            })}
          </div>

          {/* CTA Buttons */}
          {!isAuthenticated ? (
            <div className="flex items-center justify-center gap-4 flex-wrap">
              <button 
                onClick={() => navigate('/register')}
                className="px-10 py-4 bg-white text-[#13EC5B] rounded-xl hover:bg-gray-100 hover:scale-105 transition-all font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[16px] flex items-center gap-2 shadow-xl">
              <Sparkles className="w-5 h-5" />
              Đăng Ký Ngay
            </button>
            <button 
              onClick={() => navigate('/stories')}
              className="px-10 py-4 bg-[#1A2332] text-white rounded-xl hover:bg-[#0F172A] hover:scale-105 transition-all font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[16px] flex items-center gap-2 shadow-xl">
              <BookOpen className="w-5 h-5" />
              Khám Phá Truyện
              <ArrowRight className="w-5 h-5" />
            </button>
          </div>
          ) : (
            <div className="flex items-center justify-center gap-4 flex-wrap">
              <button 
                onClick={() => navigate('/author/stories')}
                className="px-10 py-4 bg-white text-[#13EC5B] rounded-xl hover:bg-gray-100 hover:scale-105 transition-all font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[16px] flex items-center gap-2 shadow-xl">
                <Sparkles className="w-5 h-5" />
                Bắt Đầu Viết Ngay
              </button>
              <button 
                onClick={() => navigate('/stories')}
                className="px-10 py-4 bg-[#1A2332] text-white rounded-xl hover:bg-[#0F172A] hover:scale-105 transition-all font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[16px] flex items-center gap-2 shadow-xl">
                <BookOpen className="w-5 h-5" />
                Khám Phá Truyện
                <ArrowRight className="w-5 h-5" />
              </button>
            </div>
          )}
        </div>
      </div>
    </section>
  );
}
