import React, { useState } from 'react';
import { ImageWithFallback } from '../figma/ImageWithFallback';
import { 
  Sparkles, Brain, Users, Star, UserPlus, CheckCircle, 
  ChevronLeft, ChevronRight
} from 'lucide-react';
import thandaoco from '../../assets/image/thandaoco.jpg';
import voluendinhphong from '../../assets/image/voluendinhphong.jpg';
import linhVuThienHa from '../../assets/image/linh-vu-thien-ha.jpg';

export function HeroAuthorStoriesBanner() {
  const [currentSlide, setCurrentSlide] = useState(0);
  const [isTransitioning, setIsTransitioning] = useState(false);

  const featuredAuthorStories = [
    {
      id: 1,
      storyTitle: 'Hành Trình Vạn Cổ',
      author: {
        name: 'Cô Độc Bại Thân',
        avatar: 'https://images.unsplash.com/photo-1738566061505-556830f8b8f5?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMG1hbiUyMHBvcnRyYWl0JTIwcHJvZmVzc2lvbmFsfGVufDF8fHx8MTc3MDIxODA0OXww&ixlib=rb-4.1.0&q=80&w=1080',
        followers: '125K',
        verified: true
      },
      description: 'Tác phẩm mới nhất từ bậc thầy tiên hiệp, thế giới tu tiên hoành tráng với hệ thống tu luyện độc đáo.',
      genre: 'Tu Tiên',
      chapters: 245,
      views: '2.5M',
      likes: '125K',
      rating: 4.9,
      coWriting: true,
      aiAssisted: 65,
      badge: 'ĐỈNH CAO',
      image: thandaoco,
    },
    {
      id: 2,
      storyTitle: 'Võ Luyện Điên Phong',
      author: {
        name: 'Mạc Mặc',
        avatar: 'https://images.unsplash.com/photo-1686543972602-da0c7ea61ce2?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMG1hbGUlMjB3cml0ZXIlMjBnbGFzc2VzfGVufDF8fHx8MTc3MDI4NTE1M3ww&ixlib=rb-4.1.0&q=80&w=1080',
        followers: '98K',
        verified: true
      },
      description: 'Hành trình tu luyện võ đạo đầy cảm hứng, cốt truyện hấp dẫn qua từng chương.',
      genre: 'Võ Hiệp',
      chapters: 312,
      views: '3.2M',
      likes: '156K',
      rating: 4.8,
      coWriting: true,
      aiAssisted: 45,
      badge: 'NỔI BẬT',
      image: voluendinhphong,
    },
    {
      id: 3,
      storyTitle: 'Linh Vũ Thiên Hạ',
      author: {
        name: 'Vũ Phong',
        avatar: 'https://images.unsplash.com/photo-1754954865833-c6ee8cb8726d?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHlvdW5nJTIwbWFuJTIwY3JlYXRpdmV8ZW58MXx8fHwxNzcwMjg1MTU2fDA&ixlib=rb-4.1.0&q=80&w=1080',
        followers: '87K',
        verified: true
      },
      description: 'Truyện đoạt giải Tác Phẩm Của Năm - sự kết hợp hoàn hảo giữa văn phong và cốt truyện.',
      genre: 'Huyền Huyễn',
      chapters: 189,
      views: '1.8M',
      likes: '98K',
      rating: 4.9,
      coWriting: true,
      aiAssisted: 78,
      badge: 'AWARD WINNING',
      image: linhVuThienHa,
    },
  ];

  const handleSlideChange = (newSlide) => {
    if (isTransitioning) return;
    setIsTransitioning(true);
    setTimeout(() => {
      setCurrentSlide(newSlide);
      setTimeout(() => setIsTransitioning(false), 50);
    }, 300);
  };

  const nextSlide = () => {
    const newSlide = (currentSlide + 1) % featuredAuthorStories.length;
    handleSlideChange(newSlide);
  };

  const prevSlide = () => {
    const newSlide = (currentSlide - 1 + featuredAuthorStories.length) % featuredAuthorStories.length;
    handleSlideChange(newSlide);
  };

  const current = featuredAuthorStories[currentSlide];

  return (
    <section className="relative bg-gradient-to-br from-[#0A0F16] to-[#0F1923] overflow-hidden">
      <style>{`
        @keyframes fadeSlideIn {
          from { opacity: 0; transform: translateX(30px); }
          to { opacity: 1; transform: translateX(0); }
        }
        @keyframes fadeSlideOut {
          from { opacity: 1; transform: translateX(0); }
          to { opacity: 0; transform: translateX(-30px); }
        }
        .slide-in { animation: fadeSlideIn 0.4s ease-out; }
        .slide-out { animation: fadeSlideOut 0.3s ease-in; }
      `}</style>

      {/* Decorative Background Elements */}
      <div className="absolute inset-0 overflow-hidden pointer-events-none">
        <div className="absolute top-0 right-0 w-[800px] h-[800px] bg-gradient-to-br from-[#13EC5B]/10 to-transparent rounded-full blur-3xl"></div>
        <div className="absolute bottom-0 left-0 w-[700px] h-[700px] bg-gradient-to-tr from-[#2B7FFF]/10 to-transparent rounded-full blur-3xl"></div>
        <div className="absolute top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[600px] h-[600px] bg-gradient-to-br from-[#FB2C36]/5 to-transparent rounded-full blur-3xl"></div>
      </div>

      <div className="max-w-[1400px] mx-auto px-6 py-16 relative z-10">
        <div className="relative h-[540px] rounded-3xl overflow-hidden shadow-2xl" style={{background: 'linear-gradient(135deg, #0A0F16 0%, #0F1923 100%)'}}>
          <div className={`absolute inset-0 transition-opacity duration-300 ${isTransitioning ? 'slide-out' : 'slide-in'}`}>
            {/* Background Image with Gradient Overlay */}
            <div className="absolute inset-0">
              {/* Story Image */}
              <ImageWithFallback 
                src={current.image} 
                alt="Background" 
                className="w-full h-full object-cover object-top"
                style={{ 
                  objectPosition: 'center 20%',
                  imageRendering: 'auto',
                  WebkitImageRendering: 'auto',
                  backfaceVisibility: 'hidden',
                  transform: 'translateZ(0)',
                  willChange: 'transform'
                }}
              />
              {/* Gradient Overlay: từ đen đậm sang màu nhạt dần */}
              <div className="absolute inset-0 bg-gradient-to-r from-[#0A0F16] via-[#0A0F16]/70 to-[#0A0F16]/30 z-10"></div>
            </div>

            {/* Content */}
            <div className="relative z-20 h-full flex items-center px-16">
              <div className="flex items-center gap-12 w-full max-w-[1200px]">
                {/* Author Avatar with Glow */}
                <div className="relative flex-shrink-0">
                  <div className="relative">
                    {/* Animated Glow */}
                    <div className="absolute -inset-2 bg-gradient-to-br from-[#13EC5B]/20 via-[#2B7FFF]/20 to-[#13EC5B]/20 rounded-3xl blur-2xl opacity-70 animate-pulse"></div>
                    {/* Avatar */}
                    <div className="relative w-48 h-48 rounded-2xl overflow-hidden ring-4 ring-white/10 shadow-2xl">
                      <ImageWithFallback 
                        src={current.author.avatar} 
                        alt={current.author.name} 
                        className="w-full h-full object-cover" 
                      />
                    </div>
                  </div>
                  {current.author.verified && (
                    <div className="absolute -bottom-3 left-1/2 -translate-x-1/2">
                      <div className="px-4 py-2 bg-gradient-to-r from-[#2B7FFF] to-[#1E5FCC] rounded-full flex items-center gap-1.5 shadow-xl ring-2 ring-white">
                        <CheckCircle className="w-3.5 h-3.5 text-white" />
                        <span className="text-white font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[11px]">Verified</span>
                      </div>
                    </div>
                  )}
                </div>

                {/* Story Info */}
                <div className="flex-1 min-w-0">
                  {/* Badges */}
                  <div className="flex items-center gap-3 mb-4">
                    <span className="px-4 py-2 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-xl font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[12px] flex items-center gap-2 shadow-lg shadow-[#13EC5B]/30">
                      <Sparkles className="w-4 h-4" />
                      {current.badge}
                    </span>
                    <span className="px-4 py-2 bg-[#2B7FFF]/10 border-2 border-[#2B7FFF]/30 rounded-xl text-[#2B7FFF] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[12px]">
                      {current.genre}
                    </span>
                    {current.coWriting && (
                      <span className="px-3 py-2 bg-white/5 border border-white/10 rounded-xl text-white font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[11px] flex items-center gap-1.5">
                        <Brain className="w-3.5 h-3.5" />
                        AI {current.aiAssisted}%
                      </span>
                    )}
                  </div>

                  {/* Story Title */}
                  <h2 className="text-white font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[52px] leading-[1.1] mb-3 tracking-tight">
                    {current.storyTitle}
                  </h2>

                  {/* Author Info */}
                  <div className="flex items-center gap-3 mb-5">
                    <span className="text-[#94A3B8] font-['Plus_Jakarta_Sans',sans-serif] font-medium text-[17px]">
                      Tác giả:
                    </span>
                    <span className="text-white font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[19px]">
                      {current.author.name}
                    </span>
                    <div className="flex items-center gap-1.5 px-3 py-1.5 bg-white/5 rounded-lg border border-white/10">
                      <Users className="w-4 h-4 text-[#94A3B8]" />
                      <span className="text-white font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[14px]">
                        {current.author.followers}
                      </span>
                    </div>
                  </div>

                  {/* Description */}
                  <p className="text-[#94A3B8] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[16px] mb-6 leading-relaxed max-w-[650px]">
                    {current.description}
                  </p>

                  {/* Stats Grid */}
                  <div className="grid grid-cols-4 gap-4 mb-6">
                    <div className="bg-white/[0.03] border border-white/10 rounded-xl p-4 hover:border-white/20 hover:shadow-lg transition-all">
                      <p className="text-white font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[22px] mb-1">{current.chapters}</p>
                      <p className="text-[#94A3B8] font-['Plus_Jakarta_Sans',sans-serif] font-medium text-[12px]">Chương</p>
                    </div>
                    <div className="bg-white/[0.03] border border-white/10 rounded-xl p-4 hover:border-white/20 hover:shadow-lg transition-all">
                      <p className="text-white font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[22px] mb-1">{current.views}</p>
                      <p className="text-[#94A3B8] font-['Plus_Jakarta_Sans',sans-serif] font-medium text-[12px]">Lượt đọc</p>
                    </div>
                    <div className="bg-white/[0.03] border border-white/10 rounded-xl p-4 hover:border-white/20 hover:shadow-lg transition-all">
                      <p className="text-white font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[22px] mb-1">{current.likes}</p>
                      <p className="text-[#94A3B8] font-['Plus_Jakarta_Sans',sans-serif] font-medium text-[12px]">Lượt thích</p>
                    </div>
                    <div className="bg-white/[0.03] border border-white/10 rounded-xl p-4 hover:border-white/20 hover:shadow-lg transition-all">
                      <div className="flex items-center gap-1.5 mb-1">
                        <Star className="w-4 h-4 text-[#FFA500] fill-[#FFA500]" />
                        <p className="text-white font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[22px]">{current.rating}</p>
                      </div>
                      <p className="text-[#94A3B8] font-['Plus_Jakarta_Sans',sans-serif] font-medium text-[12px]">Đánh giá</p>
                    </div>
                  </div>

                  {/* CTA Buttons */}
                  <div className="flex items-center gap-3">
                    <button className="px-6 py-3 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-xl hover:shadow-xl hover:scale-[1.02] transition-all font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[14px] shadow-lg shadow-[#13EC5B]/30">
                      Đọc ngay
                    </button>
                    <button className="px-6 py-3 bg-white/5 border border-white/10 text-white rounded-xl hover:border-white/20 hover:bg-white/10 transition-all font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[14px] flex items-center gap-2">
                      <UserPlus className="w-4 h-4" />
                      Theo dõi
                    </button>
                  </div>
                </div>
              </div>
            </div>
          </div>

          {/* Navigation Arrows */}
          <button 
            onClick={prevSlide} 
            disabled={isTransitioning} 
            className="absolute left-6 top-1/2 -translate-y-1/2 z-30 w-12 h-12 bg-white/10 border border-white/20 rounded-full flex items-center justify-center hover:bg-white/20 hover:scale-110 transition-all shadow-lg disabled:opacity-30 disabled:cursor-not-allowed disabled:hover:scale-100"
          >
            <ChevronLeft className="w-6 h-6 text-white" />
          </button>
          <button 
            onClick={nextSlide} 
            disabled={isTransitioning} 
            className="absolute right-6 top-1/2 -translate-y-1/2 z-30 w-12 h-12 bg-white/10 border border-white/20 rounded-full flex items-center justify-center hover:bg-white/20 hover:scale-110 transition-all shadow-lg disabled:opacity-30 disabled:cursor-not-allowed disabled:hover:scale-100"
          >
            <ChevronRight className="w-6 h-6 text-white" />
          </button>

          {/* Dots Indicator */}
          <div className="absolute bottom-6 left-1/2 -translate-x-1/2 z-30 flex items-center gap-2.5">
            {featuredAuthorStories.map((_, index) => (
              <button 
                key={index} 
                onClick={() => handleSlideChange(index)} 
                disabled={isTransitioning} 
                className={`h-2.5 rounded-full transition-all disabled:cursor-not-allowed ${
                  index === currentSlide 
                    ? 'w-8 bg-[#13EC5B] shadow-lg shadow-[#13EC5B]/50' 
                    : 'w-2.5 bg-white/30 hover:bg-white/40'
                }`} 
              />
            ))}
          </div>
        </div>
      </div>
    </section>
  );
}
