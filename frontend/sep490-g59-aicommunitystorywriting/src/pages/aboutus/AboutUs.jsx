import React from 'react';
import { useNavigate } from 'react-router-dom';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { ImageWithFallback } from '../../components/figma/ImageWithFallback';
import { BookOpen, Sparkles, Users, Award, Bot, Heart, Target, Zap, Shield, TrendingUp, Globe, Star, CheckCircle, Lightbulb, MessageCircle, Trophy, Lock, Rocket } from 'lucide-react';

export default function AboutUs() {
  return (
    <div className="w-full bg-white">
      <Header />
      <HeroSection />
      <IntroSection />
      <MissionSection />
      <WhyChooseUsSection />
      <FeaturesSection />
      <HowItWorksSection />
      <StatsSection />
      <ValuesSection />
      <TestimonialsSection />
      <TeamSection />
      <CTASection />
      <Footer />
    </div>
  );
}

function HeroSection() {
  const navigate = useNavigate();

  return (
    <section className="relative py-32 px-6 overflow-hidden bg-gradient-to-br from-[#0F172A] to-[#1E293B]">
      <div className="absolute inset-0 opacity-10">
        <div className="absolute top-20 left-10 w-96 h-96 bg-[#13EC5B] rounded-full blur-[120px]"></div>
        <div className="absolute bottom-20 right-10 w-96 h-96 bg-[#2B7FFF] rounded-full blur-[120px]"></div>
      </div>

      <div className="max-w-[1200px] mx-auto relative z-10">
        <div className="text-center">
          <div className="inline-flex items-center gap-2 px-4 py-2 bg-[#13EC5B]/10 border border-[#13EC5B]/30 rounded-full mb-6">
            <Sparkles className="w-4 h-4 text-[#13EC5B]" />
            <span className="text-[#13EC5B] font-semibold text-sm">Về CSW-AI</span>
          </div>

          <h1 className="text-white font-bold text-5xl md:text-7xl leading-tight mb-8">
            Nơi Câu Chuyện<br />
            <span className="bg-gradient-to-r from-[#13EC5B] to-[#2B7FFF] bg-clip-text text-transparent">
              Trở Nên Sống Động
            </span>
          </h1>

          <p className="text-[#94A3B8] text-xl md:text-2xl mb-12 max-w-4xl mx-auto leading-relaxed">
            CSW-AI là nền tảng viết truyện cộng đồng đầu tiên tại Việt Nam tích hợp công nghệ AI tiên tiến,
            giúp tác giả và độc giả kết nối, sáng tạo và chia sẻ những câu chuyện đầy cảm hứng.
          </p>

          <div className="flex items-center justify-center gap-4 flex-wrap">
            <button
              onClick={() => navigate('/register')}
              className="group px-8 py-4 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-xl hover:shadow-[0_0_40px_rgba(19,236,91,0.5)] transition-all font-bold text-lg flex items-center gap-3"
            >
              Tham Gia Ngay
              <span className="group-hover:translate-x-1 transition-transform">→</span>
            </button>

            <button
              onClick={() => navigate('/story-list')}
              className="px-8 py-4 bg-white/10 backdrop-blur-sm border-2 border-white/20 text-white rounded-xl hover:bg-white/20 transition-all font-bold text-lg flex items-center gap-3"
            >
              <BookOpen className="w-5 h-5" />
              Khám Phá Truyện
            </button>
          </div>
        </div>
      </div>
    </section>
  );
}

function IntroSection() {
  return (
    <section className="py-20 px-6 bg-white">
      <div className="max-w-6xl mx-auto">
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-12 items-center">
          <div>
            <h2 className="text-4xl font-bold text-[#1A2332] mb-6">
              CSW-AI Là Gì?
            </h2>
            <div className="space-y-4 text-lg text-[#90A1B9] leading-relaxed">
              <p>
                <strong className="text-[#1A2332]">CSW-AI (Community Story Writing with AI)</strong> là nền tảng viết truyện trực tuyến
                kết hợp sức mạnh của cộng đồng và công nghệ trí tuệ nhân tạo, được ra đời với mục tiêu dân chủ hóa
                nghệ thuật sáng tạo văn học.
              </p>
              <p>
                Chúng tôi hiểu rằng mỗi người đều có một câu chuyện đáng kể, nhưng không phải ai cũng có thời gian,
                kỹ năng hoặc nguồn lực để biến ý tưởng thành tác phẩm hoàn chỉnh. CSW-AI ra đời để phá bỏ rào cản đó.
              </p>
              <p>
                Với trợ lý AI thông minh, bạn có thể dễ dàng phát triển ý tưởng, xây dựng cốt truyện, tạo nhân vật
                sống động và hoàn thiện từng chương truyện một cách chuyên nghiệp - dù bạn là người mới bắt đầu hay
                tác giả dày dặn kinh nghiệm.
              </p>
            </div>
          </div>

          <div className="relative">
            <div className="relative rounded-2xl overflow-hidden shadow-2xl">
              <ImageWithFallback
                src="https://images.unsplash.com/photo-1600188767964-3c404b885cda?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHx3cml0aW5nJTIwc3RvcnklMjBjcmVhdGl2ZSUyMGF1dGhvcnxlbnwxfHx8fDE3NzA0NTc4NzZ8MA&ixlib=rb-4.1.0&q=80&w=1080"
                alt="Creative Writing"
                className="w-full h-96 object-cover"
              />
              <div className="absolute inset-0 bg-gradient-to-t from-[#0F172A]/60 to-transparent"></div>
              <div className="absolute bottom-6 left-6 right-6">
                <div className="flex items-center gap-4 text-white">
                  <div className="flex -space-x-2">
                    <div className="w-10 h-10 rounded-full bg-[#13EC5B] border-2 border-white"></div>
                    <div className="w-10 h-10 rounded-full bg-[#2B7FFF] border-2 border-white"></div>
                    <div className="w-10 h-10 rounded-full bg-[#FFA500] border-2 border-white"></div>
                  </div>
                  <div>
                    <p className="font-bold">10,000+ Tác Giả</p>
                    <p className="text-sm text-gray-200">Đang sáng tạo mỗi ngày</p>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  );
}

function MissionSection() {
  return (
    <section className="py-20 px-6 bg-gray-50">
      <div className="max-w-6xl mx-auto">
        <div className="text-center mb-16">
          <div className="inline-flex items-center gap-2 px-4 py-2 bg-[#13EC5B]/10 border border-[#13EC5B]/30 rounded-full mb-4">
            <Target className="w-4 h-4 text-[#13EC5B]" />
            <span className="text-[#13EC5B] font-semibold text-sm">Sứ mệnh & Tầm nhìn</span>
          </div>
          <h2 className="text-4xl md:text-5xl font-bold text-[#1A2332] mb-4">
            Trao Quyền Sáng Tạo<br />
            <span className="bg-gradient-to-r from-[#13EC5B] to-[#2B7FFF] bg-clip-text text-transparent">
              Cho Mọi Người
            </span>
          </h2>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
          <div className="bg-white p-8 rounded-2xl border-2 border-gray-100 hover:border-[#13EC5B] transition-all">
            <div className="w-16 h-16 bg-gradient-to-br from-[#13EC5B] to-[#11D350] rounded-xl flex items-center justify-center mb-6">
              <Target className="w-8 h-8 text-white" />
            </div>
            <h3 className="text-2xl font-bold text-[#1A2332] mb-4">Sứ Mệnh</h3>
            <p className="text-[#90A1B9] text-lg leading-relaxed mb-4">
              Chúng tôi tin rằng <strong>mỗi người đều có một câu chuyện đáng kể</strong>. Sứ mệnh của CSW-AI
              là phá bỏ mọi rào cản giữa ý tưởng và tác phẩm hoàn chỉnh, giúp bất kỳ ai cũng có thể trở thành tác giả.
            </p>
            <p className="text-[#90A1B9] text-lg leading-relaxed">
              Chúng tôi cam kết tạo ra một môi trường an toàn, sáng tạo và công bằng, nơi mọi giọng nói đều được
              lắng nghe và mọi câu chuyện đều có cơ hội tỏa sáng.
            </p>
          </div>

          <div className="bg-white p-8 rounded-2xl border-2 border-gray-100 hover:border-[#2B7FFF] transition-all">
            <div className="w-16 h-16 bg-gradient-to-br from-[#2B7FFF] to-[#1E5FD9] rounded-xl flex items-center justify-center mb-6">
              <Rocket className="w-8 h-8 text-white" />
            </div>
            <h3 className="text-2xl font-bold text-[#1A2332] mb-4">Tầm Nhìn</h3>
            <p className="text-[#90A1B9] text-lg leading-relaxed mb-4">
              Trở thành <strong>nền tảng viết truyện hàng đầu Đông Nam Á</strong>, nơi công nghệ AI và cộng đồng
              cùng nhau tạo nên hàng triệu câu chuyện truyền cảm hứng.
            </p>
            <p className="text-[#90A1B9] text-lg leading-relaxed">
              Chúng tôi hướng tới một tương lai nơi việc sáng tạo văn học trở nên dễ dàng, dân chủ và
              mang lại giá trị bền vững cho cả tác giả lẫn độc giả.
            </p>
          </div>
        </div>
      </div>
    </section>
  );
}

function WhyChooseUsSection() {
  const reasons = [
    {
      icon: Bot,
      title: 'AI Thông Minh Nhất',
      description: 'Công nghệ AI được đào tạo trên hàng triệu tác phẩm văn học, hiểu rõ văn phong Việt Nam và có khả năng hỗ trợ sáng tạo đa dạng thể loại.',
      color: 'from-[#13EC5B] to-[#11D350]',
      features: ['Gợi ý cốt truyện logic', 'Tạo nhân vật sống động', 'Điều chỉnh văn phong', 'Kiểm tra lỗi chính tả']
    },
    {
      icon: Users,
      title: 'Cộng Đồng Năng Động',
      description: 'Hơn 10,000 tác giả và 100,000 độc giả đang hoạt động, tạo nên một hệ sinh thái văn học sôi động và đầy cảm hứng.',
      color: 'from-[#2B7FFF] to-[#1E5FD9]',
      features: ['Tương tác thời gian thực', 'Nhóm thảo luận chuyên sâu', 'Sự kiện cộng đồng', 'Mentor 1-1']
    },
    {
      icon: Award,
      title: 'Thu Nhập Thực Tế',
      description: 'Hệ thống coin và chương trả phí giúp bạn kiếm tiền từ tác phẩm. Nhiều tác giả đã có thu nhập ổn định từ CSW-AI.',
      color: 'from-[#FFA500] to-[#FF8C00]',
      features: ['Chương trả phí linh hoạt', 'Hệ thống đề cử', 'Rút tiền nhanh chóng', 'Thống kê chi tiết']
    },
    {
      icon: Shield,
      title: 'Bảo Vệ Toàn Diện',
      description: 'Hệ thống bảo mật đa lớp và chính sách bảo vệ tác quyền nghiêm ngặt, đảm bảo công sức sáng tạo của bạn luôn được tôn trọng.',
      color: 'from-[#FB2C36] to-[#E01F2E]',
      features: ['Mã hóa end-to-end', 'Watermark tự động', 'DMCA protection', 'Hỗ trợ pháp lý']
    },
  ];

  return (
    <section className="py-20 px-6 bg-white">
      <div className="max-w-7xl mx-auto">
        <div className="text-center mb-16">
          <div className="inline-flex items-center gap-2 px-4 py-2 bg-[#13EC5B]/10 border border-[#13EC5B]/30 rounded-full mb-4">
            <Star className="w-4 h-4 text-[#13EC5B]" />
            <span className="text-[#13EC5B] font-semibold text-sm">Tại sao chọn chúng tôi</span>
          </div>
          <h2 className="text-4xl md:text-5xl font-bold text-[#1A2332] mb-4">
            Điều Gì Làm<br />
            <span className="bg-gradient-to-r from-[#13EC5B] to-[#2B7FFF] bg-clip-text text-transparent">
              CSW-AI Khác Biệt?
            </span>
          </h2>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
          {reasons.map((reason, index) => {
            const Icon = reason.icon;
            return (
              <div
                key={index}
                className="group p-8 bg-white border-2 border-gray-100 rounded-2xl hover:border-[#13EC5B] transition-all hover:shadow-2xl"
              >
                <div className={`w-16 h-16 bg-gradient-to-br ${reason.color} rounded-2xl flex items-center justify-center mb-6 group-hover:scale-110 transition-transform`}>
                  <Icon className="w-8 h-8 text-white" />
                </div>
                <h3 className="text-2xl font-bold text-[#1A2332] mb-3">{reason.title}</h3>
                <p className="text-[#90A1B9] text-lg leading-relaxed mb-4">{reason.description}</p>
                <ul className="space-y-2">
                  {reason.features.map((feature, idx) => (
                    <li key={idx} className="flex items-center gap-2 text-[#90A1B9]">
                      <CheckCircle className="w-5 h-5 text-[#13EC5B] flex-shrink-0" />
                      <span>{feature}</span>
                    </li>
                  ))}
                </ul>
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}

function FeaturesSection() {
  const features = [
    {
      icon: Bot,
      title: 'AI Hỗ Trợ Viết Truyện',
      description: 'Trợ lý AI thông minh giúp bạn phát triển ý tưởng, gợi ý cốt truyện, xây dựng nhân vật và hoàn thiện từng chương.',
      details: ['Brainstorming tự động', 'Phân tích cấu trúc truyện', 'Gợi ý diễn biến hợp lý', 'Tối ưu văn phong']
    },
    {
      icon: MessageCircle,
      title: 'Tương Tác Độc Giả',
      description: 'Nhận phản hồi trực tiếp từ độc giả qua bình luận, đánh giá và thảo luận chuyên sâu về tác phẩm.',
      details: ['Bình luận theo chương', 'Hệ thống đánh giá', 'Q&A với tác giả', 'Poll ý kiến độc giả']
    },
    {
      icon: TrendingUp,
      title: 'Phân Tích Chuyên Sâu',
      description: 'Dashboard chi tiết với đầy đủ thống kê: lượt đọc, tương tác, thu nhập và xu hướng độc giả.',
      details: ['Biểu đồ realtime', 'Phân tích độc giả', 'Dự đoán xu hướng', 'So sánh thể loại']
    },
    {
      icon: Award,
      title: 'Kiếm Tiền Từ Truyện',
      description: 'Nhiều hình thức kiếm tiền: chương trả phí, hệ thống coin, đề cử và hợp đồng xuất bản độc quyền.',
      details: ['Chương VIP', 'Đề cử có thưởng', 'Chia sẻ doanh thu ads', 'Xuất bản chính thức']
    },
    {
      icon: Globe,
      title: 'Đa Nền Tảng',
      description: 'Truyện của bạn tự động tối ưu cho mọi thiết bị: web, mobile app (iOS/Android) và tablet.',
      details: ['Responsive design', 'Offline reading', 'Sync đa thiết bị', 'Dark mode']
    },
    {
      icon: Lock,
      title: 'Bảo Mật & Riêng Tư',
      description: 'Mã hóa end-to-end, bảo vệ tác quyền DMCA và chính sách riêng tư nghiêm ngặt.',
      details: ['SSL encryption', 'GDPR compliant', 'IP protection', 'Privacy controls']
    },
  ];

  return (
    <section className="py-20 px-6 bg-gray-50">
      <div className="max-w-7xl mx-auto">
        <div className="text-center mb-16">
          <div className="inline-flex items-center gap-2 px-4 py-2 bg-[#13EC5B]/10 border border-[#13EC5B]/30 rounded-full mb-4">
            <Zap className="w-4 h-4 text-[#13EC5B]" />
            <span className="text-[#13EC5B] font-semibold text-sm">Tính năng</span>
          </div>
          <h2 className="text-4xl md:text-5xl font-bold text-[#1A2332] mb-4">
            Công Cụ Toàn Diện<br />
            <span className="bg-gradient-to-r from-[#13EC5B] to-[#2B7FFF] bg-clip-text text-transparent">
              Cho Tác Giả Chuyên Nghiệp
            </span>
          </h2>
          <p className="text-[#90A1B9] text-lg max-w-3xl mx-auto">
            Từ ý tưởng ban đầu đến tác phẩm hoàn chỉnh, CSW-AI đồng hành cùng bạn với bộ công cụ mạnh mẽ
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {features.map((feature, index) => {
            const Icon = feature.icon;
            return (
              <div
                key={index}
                className="group p-6 bg-white border-2 border-gray-100 rounded-xl hover:border-[#13EC5B] transition-all hover:shadow-xl"
              >
                <div className="w-12 h-12 bg-gradient-to-br from-[#13EC5B] to-[#11D350] rounded-lg flex items-center justify-center mb-4 group-hover:scale-110 transition-transform">
                  <Icon className="w-6 h-6 text-white" />
                </div>
                <h3 className="text-xl font-bold text-[#1A2332] mb-2">{feature.title}</h3>
                <p className="text-[#90A1B9] mb-4 text-sm">{feature.description}</p>
                <ul className="space-y-1">
                  {feature.details.map((detail, idx) => (
                    <li key={idx} className="flex items-center gap-2 text-sm text-[#90A1B9]">
                      <div className="w-1.5 h-1.5 rounded-full bg-[#13EC5B]"></div>
                      <span>{detail}</span>
                    </li>
                  ))}
                </ul>
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}

function HowItWorksSection() {
  const navigate = useNavigate();
  const steps = [
    {
      number: '01',
      title: 'Đăng Ký Tài Khoản',
      description: 'Tạo tài khoản miễn phí chỉ trong 30 giây. Không cần thẻ tín dụng, không cam kết dài hạn.',
      icon: Users,
      color: 'from-[#13EC5B] to-[#11D350]'
    },
    {
      number: '02',
      title: 'Khởi Tạo Dự Án',
      description: 'Chọn thể loại, nhập ý tưởng cơ bản. AI sẽ giúp bạn xây dựng outline và nhân vật ngay lập tức.',
      icon: Lightbulb,
      color: 'from-[#FFA500] to-[#FF8C00]'
    },
    {
      number: '03',
      title: 'Viết & Sáng Tạo',
      description: 'Viết truyện với sự hỗ trợ của AI. Nhận gợi ý, sửa lỗi và tối ưu văn phong theo thời gian thực.',
      icon: BookOpen,
      color: 'from-[#2B7FFF] to-[#1E5FD9]'
    },
    {
      number: '04',
      title: 'Xuất Bản & Kiếm Tiền',
      description: 'Xuất bản truyện, tiếp cận hàng ngàn độc giả và bắt đầu kiếm tiền từ tác phẩm của bạn.',
      icon: Trophy,
      color: 'from-[#FB2C36] to-[#E01F2E]'
    },
  ];

  return (
    <section className="py-20 px-6 bg-white">
      <div className="max-w-6xl mx-auto">
        <div className="text-center mb-16">
          <div className="inline-flex items-center gap-2 px-4 py-2 bg-[#13EC5B]/10 border border-[#13EC5B]/30 rounded-full mb-4">
            <Rocket className="w-4 h-4 text-[#13EC5B]" />
            <span className="text-[#13EC5B] font-semibold text-sm">Cách thức hoạt động</span>
          </div>
          <h2 className="text-4xl md:text-5xl font-bold text-[#1A2332] mb-4">
            Bắt Đầu Chỉ Trong<br />
            <span className="bg-gradient-to-r from-[#13EC5B] to-[#2B7FFF] bg-clip-text text-transparent">
              4 Bước Đơn Giản
            </span>
          </h2>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 gap-8">
          {steps.map((step, index) => {
            const Icon = step.icon;
            return (
              <div key={index} className="relative">
                <div className="flex gap-6 items-start">
                  <div className={`flex-shrink-0 w-16 h-16 bg-gradient-to-br ${step.color} rounded-2xl flex items-center justify-center`}>
                    <Icon className="w-8 h-8 text-white" />
                  </div>
                  <div className="flex-1">
                    <div className="text-6xl font-bold text-gray-100 mb-2">{step.number}</div>
                    <h3 className="text-2xl font-bold text-[#1A2332] mb-3 -mt-8">{step.title}</h3>
                    <p className="text-[#90A1B9] text-lg leading-relaxed">{step.description}</p>
                  </div>
                </div>
              </div>
            );
          })}
        </div>

        <div className="mt-12 text-center">
          <button
            onClick={() => navigate('/register')}
            className="inline-flex items-center gap-2 px-8 py-4 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-xl hover:shadow-[0_0_40px_rgba(19,236,91,0.5)] transition-all font-bold text-lg"
          >
            <Sparkles className="w-5 h-5" />
            Bắt Đầu Ngay - Miễn Phí
            <span>→</span>
          </button>
        </div>
      </div>
    </section>
  );
}

function StatsSection() {
  const stats = [
    { value: '50,000+', label: 'Truyện Đã Xuất Bản', description: 'Từ truyện ngắn đến tiểu thuyết dài kỳ', icon: BookOpen },
    { value: '10,000+', label: 'Tác Giả Hoạt Động', description: 'Cộng đồng sáng tạo không ngừng lớn mạnh', icon: Users },
    { value: '1,000,000+', label: 'Lượt Đọc Mỗi Tháng', description: 'Hàng triệu độc giả đam mê', icon: TrendingUp },
    { value: '100,000+', label: 'Chương AI Hỗ Trợ', description: 'Công nghệ AI giúp tác giả sáng tạo', icon: Bot },
  ];

  return (
    <section className="py-20 px-6 bg-gradient-to-br from-[#0F172A] to-[#1E293B] relative overflow-hidden">
      <div className="absolute inset-0 opacity-10">
        <div className="absolute top-10 left-20 w-96 h-96 bg-[#13EC5B] rounded-full blur-[120px]"></div>
        <div className="absolute bottom-10 right-20 w-96 h-96 bg-[#2B7FFF] rounded-full blur-[120px]"></div>
      </div>

      <div className="max-w-7xl mx-auto relative z-10">
        <div className="text-center mb-16">
          <div className="inline-flex items-center gap-2 px-4 py-2 bg-[#13EC5B]/10 border border-[#13EC5B]/30 rounded-full mb-4">
            <Star className="w-4 h-4 text-[#13EC5B]" />
            <span className="text-[#13EC5B] font-semibold text-sm">Con số ấn tượng</span>
          </div>
          <h2 className="text-4xl md:text-5xl font-bold text-white mb-4">
            Thành Tựu Của<br />
            <span className="bg-gradient-to-r from-[#13EC5B] to-[#2B7FFF] bg-clip-text text-transparent">
              Cộng Đồng Chúng Tôi
            </span>
          </h2>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-8">
          {stats.map((stat, index) => {
            const Icon = stat.icon;
            return (
              <div
                key={index}
                className="group p-8 bg-white/5 backdrop-blur-sm border-2 border-white/10 rounded-2xl hover:bg-white/10 transition-all hover:-translate-y-2"
              >
                <Icon className="w-12 h-12 text-[#13EC5B] mb-4 group-hover:scale-110 transition-transform" />
                <p className="text-white font-bold text-4xl mb-2">{stat.value}</p>
                <p className="text-white font-bold text-lg mb-2">{stat.label}</p>
                <p className="text-[#94A3B8] text-sm">{stat.description}</p>
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}

function ValuesSection() {
  const values = [
    { icon: Heart, title: 'Đam Mê', description: 'Chúng tôi yêu thích văn học và tin vào sức mạnh của câu chuyện trong việc kết nối con người, truyền cảm hứng và thay đổi cuộc sống.' },
    { icon: Users, title: 'Cộng Đồng', description: 'Xây dựng môi trường an toàn, thân thiện và đa dạng nơi mọi người có thể tự do chia sẻ, học hỏi và phát triển cùng nhau.' },
    { icon: Sparkles, title: 'Sáng Tạo', description: 'Khuyến khích sự đổi mới, thử nghiệm và phá vỡ khuôn mẫu. Mỗi tác giả đều có phong cách riêng và được tôn trọng.' },
    { icon: Shield, title: 'Chính Trực', description: 'Tôn trọng quyền tác giả, bảo vệ sự sáng tạo và duy trì môi trường công bằng, minh bạch cho tất cả thành viên.' },
  ];

  return (
    <section className="py-20 px-6 bg-white">
      <div className="max-w-6xl mx-auto">
        <div className="text-center mb-16">
          <div className="inline-flex items-center gap-2 px-4 py-2 bg-[#13EC5B]/10 border border-[#13EC5B]/30 rounded-full mb-4">
            <Heart className="w-4 h-4 text-[#13EC5B]" />
            <span className="text-[#13EC5B] font-semibold text-sm">Giá trị cốt lõi</span>
          </div>
          <h2 className="text-4xl md:text-5xl font-bold text-[#1A2332] mb-4">
            Những Gì Chúng Tôi<br />
            <span className="bg-gradient-to-r from-[#13EC5B] to-[#2B7FFF] bg-clip-text text-transparent">
              Tin Tưởng & Theo Đuổi
            </span>
          </h2>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-8">
          {values.map((value, index) => {
            const Icon = value.icon;
            return (
              <div
                key={index}
                className="group text-center p-8 bg-gradient-to-br from-white to-gray-50 border-2 border-gray-100 rounded-2xl hover:border-[#13EC5B] transition-all hover:shadow-xl hover:-translate-y-2"
              >
                <div className="w-20 h-20 bg-gradient-to-br from-[#13EC5B] to-[#11D350] rounded-full flex items-center justify-center mx-auto mb-6 group-hover:scale-110 transition-transform">
                  <Icon className="w-10 h-10 text-white" />
                </div>
                <h3 className="text-2xl font-bold text-[#1A2332] mb-3">{value.title}</h3>
                <p className="text-[#90A1B9] leading-relaxed">{value.description}</p>
              </div>
            );
          })}
        </div>
      </div>
    </section>
  );
}

function TestimonialsSection() {
  const testimonials = [
    { name: 'Nguyễn Minh Anh', role: 'Tác giả truyện Tu Tiên', avatar: 'https://images.unsplash.com/photo-1738566061505-556830f8b8f5?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&q=80&w=1080', content: 'CSW-AI đã thay đổi hoàn toàn cách tôi viết truyện. AI không chỉ giúp tôi phát triển ý tưởng mà còn giúp tối ưu văn phong. Thu nhập từ truyện của tôi đã tăng 300% trong 6 tháng!', stats: '2.5M lượt đọc' },
    { name: 'Trần Thúy Hằng', role: 'Tác giả Ngôn Tình', avatar: 'https://images.unsplash.com/photo-1611199340099-91a595a86812?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&q=80&w=1080', content: 'Tôi từng nghĩ viết truyện chỉ dành cho những người có tài năng đặc biệt. CSW-AI đã chứng minh điều ngược lại. Cộng đồng ở đây rất nhiệt tình và AI thực sự hữu ích!', stats: '1.8M lượt đọc' },
    { name: 'Lê Hoàng Nam', role: 'Tác giả Huyền Huyễn', avatar: 'https://images.unsplash.com/photo-1754954865833-c6ee8cb8726d?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&q=80&w=1080', content: 'Dashboard phân tích chi tiết giúp tôi hiểu rõ độc giả của mình. Tôi biết họ thích gì, không thích gì và điều chỉnh cốt truyện phù hợp. Đây là công cụ không thể thiếu!', stats: '3.2M lượt đọc' },
  ];

  return (
    <section className="py-20 px-6 bg-gray-50">
      <div className="max-w-6xl mx-auto">
        <div className="text-center mb-16">
          <div className="inline-flex items-center gap-2 px-4 py-2 bg-[#13EC5B]/10 border border-[#13EC5B]/30 rounded-full mb-4">
            <MessageCircle className="w-4 h-4 text-[#13EC5B]" />
            <span className="text-[#13EC5B] font-semibold text-sm">Câu chuyện thành công</span>
          </div>
          <h2 className="text-4xl md:text-5xl font-bold text-[#1A2332] mb-4">
            Tác Giả Nói Gì<br />
            <span className="bg-gradient-to-r from-[#13EC5B] to-[#2B7FFF] bg-clip-text text-transparent">
              Về CSW-AI?
            </span>
          </h2>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
          {testimonials.map((testimonial, index) => (
            <div key={index} className="bg-white p-8 rounded-2xl border-2 border-gray-100 hover:border-[#13EC5B] transition-all hover:shadow-xl">
              <div className="flex items-center gap-4 mb-6">
                <ImageWithFallback src={testimonial.avatar} alt={testimonial.name} className="w-16 h-16 rounded-full object-cover" />
                <div>
                  <h4 className="font-bold text-[#1A2332]">{testimonial.name}</h4>
                  <p className="text-sm text-[#90A1B9]">{testimonial.role}</p>
                  <p className="text-xs text-[#13EC5B] font-semibold">{testimonial.stats}</p>
                </div>
              </div>
              <p className="text-[#90A1B9] leading-relaxed italic">&quot;{testimonial.content}&quot;</p>
              <div className="flex gap-1 mt-4">
                {[...Array(5)].map((_, i) => (
                  <Star key={i} className="w-4 h-4 fill-yellow-400 text-yellow-400" />
                ))}
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}

function TeamSection() {
  const teams = [
    { role: 'Đội Ngũ Kỹ Thuật', description: 'Phát triển và duy trì nền tảng với công nghệ AI tiên tiến nhất. Đội ngũ engineers giàu kinh nghiệm từ các tập đoàn công nghệ hàng đầu.', icon: Bot, members: '15+ kỹ sư' },
    { role: 'Đội Ngũ Nội Dung', description: 'Hỗ trợ tác giả, quản lý chất lượng nội dung và tổ chức các sự kiện văn học. Team biên tập viên chuyên nghiệp với nhiều năm kinh nghiệm.', icon: BookOpen, members: '20+ biên tập viên' },
    { role: 'Đội Ngũ Cộng Đồng', description: 'Kết nối và chăm sóc cộng đồng tác giả & độc giả 24/7. Luôn lắng nghe và giải đáp mọi thắc mắc nhanh chóng.', icon: Users, members: '10+ community managers' },
  ];

  return (
    <section className="py-20 px-6 bg-white">
      <div className="max-w-6xl mx-auto">
        <div className="text-center mb-16">
          <div className="inline-flex items-center gap-2 px-4 py-2 bg-[#13EC5B]/10 border border-[#13EC5B]/30 rounded-full mb-4">
            <Users className="w-4 h-4 text-[#13EC5B]" />
            <span className="text-[#13EC5B] font-semibold text-sm">Đội ngũ của chúng tôi</span>
          </div>
          <h2 className="text-4xl md:text-5xl font-bold text-[#1A2332] mb-4">
            Những Người Đằng Sau<br />
            <span className="bg-gradient-to-r from-[#13EC5B] to-[#2B7FFF] bg-clip-text text-transparent">
              CSW-AI
            </span>
          </h2>
          <p className="text-[#90A1B9] text-lg max-w-3xl mx-auto">
            Một đội ngũ đam mê văn học và công nghệ, cam kết mang đến trải nghiệm tốt nhất cho cộng đồng
          </p>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
          {teams.map((team, index) => {
            const Icon = team.icon;
            return (
              <div key={index} className="group p-8 bg-white border-2 border-gray-100 rounded-2xl hover:border-[#13EC5B] transition-all hover:shadow-xl hover:-translate-y-2 text-center">
                <div className="w-20 h-20 bg-gradient-to-br from-[#13EC5B] to-[#11D350] rounded-full flex items-center justify-center mx-auto mb-6 group-hover:scale-110 transition-transform">
                  <Icon className="w-10 h-10 text-white" />
                </div>
                <h3 className="text-2xl font-bold text-[#1A2332] mb-2">{team.role}</h3>
                <p className="text-sm text-[#13EC5B] font-semibold mb-4">{team.members}</p>
                <p className="text-[#90A1B9] leading-relaxed">{team.description}</p>
              </div>
            );
          })}
        </div>

        <div className="mt-16 text-center">
          <p className="text-[#90A1B9] text-lg mb-6">Chúng tôi luôn tìm kiếm những tài năng mới để gia nhập đội ngũ</p>
          <button className="px-8 py-3 bg-white border-2 border-[#13EC5B] text-[#13EC5B] rounded-xl hover:bg-[#13EC5B] hover:text-white transition-all font-bold">
            Xem Các Vị Trí Tuyển Dụng →
          </button>
        </div>
      </div>
    </section>
  );
}

function CTASection() {
  const navigate = useNavigate();

  return (
    <section className="py-20 px-6 bg-gradient-to-br from-[#0F172A] to-[#1E293B] relative overflow-hidden">
      <div className="absolute inset-0 opacity-10">
        <div className="absolute top-10 left-20 w-96 h-96 bg-[#13EC5B] rounded-full blur-[120px]"></div>
        <div className="absolute bottom-10 right-20 w-96 h-96 bg-[#2B7FFF] rounded-full blur-[120px]"></div>
      </div>

      <div className="max-w-4xl mx-auto text-center relative z-10">
        <div className="inline-flex items-center gap-2 px-4 py-2 bg-[#13EC5B]/10 border border-[#13EC5B]/30 rounded-full mb-6">
          <Sparkles className="w-4 h-4 text-[#13EC5B]" />
          <span className="text-[#13EC5B] font-semibold text-sm">Bắt đầu hành trình</span>
        </div>

        <h2 className="text-white font-bold text-4xl md:text-6xl leading-tight mb-6">
          Câu Chuyện Của Bạn<br />
          <span className="bg-gradient-to-r from-[#13EC5B] to-[#2B7FFF] bg-clip-text text-transparent">
            Đang Chờ Được Kể
          </span>
        </h2>

        <p className="text-[#94A3B8] text-xl mb-10 max-w-3xl mx-auto leading-relaxed">
          Tham gia cộng đồng 10,000+ tác giả đang sáng tạo mỗi ngày. Bắt đầu viết truyện với AI ngay hôm nay - hoàn toàn miễn phí!
        </p>

        <div className="flex items-center justify-center gap-4 flex-wrap mb-8">
          <button
            onClick={() => navigate('/register')}
            className="group px-10 py-5 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-xl hover:shadow-[0_0_40px_rgba(19,236,91,0.5)] transition-all font-bold text-xl flex items-center gap-3"
          >
            <Sparkles className="w-6 h-6" />
            Đăng Ký Miễn Phí
            <span className="group-hover:translate-x-1 transition-transform">→</span>
          </button>

          <button
            onClick={() => navigate('/story-list')}
            className="px-10 py-5 bg-white/10 backdrop-blur-sm border-2 border-white/20 text-white rounded-xl hover:bg-white/20 transition-all font-bold text-xl flex items-center gap-3"
          >
            <BookOpen className="w-5 h-5" />
            Khám Phá Truyện
          </button>
        </div>

        <div className="flex items-center justify-center gap-8 text-[#94A3B8] text-sm flex-wrap">
          <div className="flex items-center gap-2">
            <CheckCircle className="w-5 h-5 text-[#13EC5B]" />
            <span>Miễn phí 100%</span>
          </div>
          <div className="flex items-center gap-2">
            <CheckCircle className="w-5 h-5 text-[#13EC5B]" />
            <span>Không cần thẻ tín dụng</span>
          </div>
          <div className="flex items-center gap-2">
            <CheckCircle className="w-5 h-5 text-[#13EC5B]" />
            <span>Bắt đầu ngay lập tức</span>
          </div>
        </div>
      </div>
    </section>
  );
}
