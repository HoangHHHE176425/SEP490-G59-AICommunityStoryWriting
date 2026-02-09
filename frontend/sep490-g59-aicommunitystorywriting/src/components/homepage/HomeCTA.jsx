import { useNavigate } from 'react-router-dom';
import { Sparkles, BookOpen } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';

export function HomeCTA() {
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();

  return (
    <section className="relative bg-gradient-to-br from-[#0F172A] to-[#1E293B] overflow-hidden">
      <div className="absolute inset-0 opacity-10">
        <div className="absolute top-10 left-20 w-96 h-96 bg-[#13EC5B] rounded-full blur-[120px]"></div>
        <div className="absolute bottom-10 right-20 w-96 h-96 bg-[#2B7FFF] rounded-full blur-[120px]"></div>
      </div>
      <div className="max-w-[1400px] mx-auto px-6 py-20 relative z-10">
        <div className="text-center max-w-[800px] mx-auto">
          <div className="inline-flex items-center gap-2 px-4 py-2 bg-[#13EC5B]/10 border border-[#13EC5B]/30 rounded-full mb-6">
            <Sparkles className="w-4 h-4 text-[#13EC5B]" />
            <span className="text-[#13EC5B] font-semibold text-[14px]">Bắt đầu ngay hôm nay</span>
          </div>
          <h2 className="text-white font-bold text-[56px] leading-tight mb-6">
            Sẵn Sàng Sáng Tạo
            <br />
            <span className="bg-gradient-to-r from-[#13EC5B] to-[#2B7FFF] bg-clip-text text-transparent">Câu Chuyện Của Bạn?</span>
          </h2>
          <p className="text-[#94A3B8] font-normal text-[20px] mb-10 leading-relaxed">
            Tham gia cộng đồng hàng ngàn tác giả và độc giả đam mê văn học. Viết truyện với sự hỗ trợ của AI ngay hôm nay!
          </p>
          <div className="flex items-center justify-center gap-4 flex-wrap">
            {isAuthenticated ? (
              <>
                <button onClick={() => navigate('/author')} className="group px-10 py-5 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-xl hover:shadow-[0_0_40px_rgba(19,236,91,0.5)] transition-all font-bold text-[18px] flex items-center gap-3">
                  <Sparkles className="w-5 h-5" />
                  Sáng tạo
                  <span className="group-hover:translate-x-1 transition-transform">→</span>
                </button>
                <button onClick={() => navigate('/story-list')} className="px-10 py-5 bg-white/10 backdrop-blur-sm border-2 border-white/20 text-white rounded-xl hover:bg-white/20 transition-all font-bold text-[18px] flex items-center gap-3">
                  <BookOpen className="w-5 h-5" />
                  Khám phá truyện
                </button>
              </>
            ) : (
              <>
                <button onClick={() => navigate('/register')} className="group px-10 py-5 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-xl hover:shadow-[0_0_40px_rgba(19,236,91,0.5)] transition-all font-bold text-[18px] flex items-center gap-3">
                  <Sparkles className="w-5 h-5" />
                  Đăng Ký Miễn Phí
                  <span className="group-hover:translate-x-1 transition-transform">→</span>
                </button>
                <button onClick={() => navigate('/login')} className="px-10 py-5 bg-white/10 backdrop-blur-sm border-2 border-white/20 text-white rounded-xl hover:bg-white/20 transition-all font-bold text-[18px]">
                  Đăng Nhập
                </button>
              </>
            )}
          </div>
          <p className="text-[#94A3B8] font-normal text-[14px] mt-8">Miễn phí 100% • Không cần thẻ tín dụng • Bắt đầu ngay lập tức</p>
        </div>
      </div>
    </section>
  );
}
