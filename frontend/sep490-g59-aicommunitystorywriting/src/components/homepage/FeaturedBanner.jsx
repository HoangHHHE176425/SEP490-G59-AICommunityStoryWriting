import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ImageWithFallback } from '../figma/ImageWithFallback';
import { BookOpen, ChevronLeft, ChevronRight, Eye, Star } from 'lucide-react';
import { storyImages } from '../../assets/image/storyImages';

const FEATURED_STORIES = [
  { id: 1, title: 'Hành Trình Vạn Cổ: Thần Đạo Dẫn Tôn', author: 'Cô Độc Bại Thân', description: 'Hành trình anh hùng giải cứu thương thế. Một cuộc phiêu lưu không khoan nhượng trong kỷ nguyên linh khí phục hồi đầy kịch tính.', genre: 'Huyền Huyễn', views: '2.5M', chapters: '5420', rating: 4.9, badge: 'HOT' },
  { id: 2, title: 'Võ Luyện Điên Phong', author: 'Mạc Mặc', description: 'Tu luyện võ đạo, vươn tới đỉnh cao. Một hành trình đầy gian nan nhưng không bao giờ từ bỏ.', genre: 'Võ Hiệp', views: '3.2M', chapters: '3401', rating: 4.8, badge: 'NEW' },
  { id: 3, title: 'Linh Vũ Thiên Hạ', author: 'Vũ Phong', description: 'Thiên tài trở về, xưng bá thiên hạ. Một câu chuyện về sự trỗi dậy của thiên tài võ học.', genre: 'Huyền Huyễn', views: '1.8M', chapters: '2876', rating: 4.7, badge: 'HOT' },
  { id: 4, title: 'Đại Chúa Tể', author: 'Thiên Tàm Thổ Đậu', description: 'Từ thiếu niên yếu đuối đến đại chúa tể thống lĩnh vạn giới. Hành trình tu luyện đầy cảm hứng.', genre: 'Huyền Huyễn', views: '2.1M', chapters: '1876', rating: 4.9, badge: 'NEW' },
  { id: 5, title: 'Phàm Nhân Tu Tiên', author: 'Vong Ngữ', description: 'Từ phàm nhân trở thành tu tiên giả. Câu chuyện kinh điển về con đường tu tiên đầy gian khổ.', genre: 'Tu Tiên', views: '2.8M', chapters: '2451', rating: 5.0, badge: 'HOT' },
];

export function FeaturedBanner() {
  const [currentSlide, setCurrentSlide] = useState(0);
  const [isTransitioning, setIsTransitioning] = useState(false);
  const navigate = useNavigate();

  const handleSlideChange = (newSlide) => {
    if (isTransitioning) return;
    setIsTransitioning(true);
    setTimeout(() => {
      setCurrentSlide(newSlide);
      setTimeout(() => setIsTransitioning(false), 50);
    }, 300);
  };

  const nextSlide = () => handleSlideChange((currentSlide + 1) % FEATURED_STORIES.length);
  const prevSlide = () => handleSlideChange((currentSlide - 1 + FEATURED_STORIES.length) % FEATURED_STORIES.length);

  const currentStory = FEATURED_STORIES[currentSlide];

  return (
    <section className="relative bg-gradient-to-br from-[#0F172A] to-[#1E293B] overflow-hidden">
      <style>{`
        @keyframes fadeSlideIn { from { opacity: 0; transform: translateX(30px); } to { opacity: 1; transform: translateX(0); } }
        @keyframes fadeSlideOut { from { opacity: 1; transform: translateX(0); } to { opacity: 0; transform: translateX(-30px); } }
        .slide-in { animation: fadeSlideIn 0.4s ease-out; }
        .slide-out { animation: fadeSlideOut 0.3s ease-in; }
      `}</style>

      <div className="max-w-[1400px] mx-auto px-6 py-12">
        <div className="relative h-[450px] rounded-2xl overflow-hidden">
          <div className={`absolute inset-0 transition-opacity duration-300 ${isTransitioning ? 'slide-out' : 'slide-in'}`}>
            <div className="absolute inset-0 isolate">
              <ImageWithFallback
                src={storyImages[currentStory.title]}
                alt={currentStory.title}
                className="absolute inset-0 w-full h-full min-w-0 min-h-0 object-cover object-[center_30%]"
                style={{ imageRendering: 'auto', transform: 'translateZ(0)', backfaceVisibility: 'hidden' }}
                loading="eager"
              />
              <div className="absolute inset-0 bg-gradient-to-r from-[#0F172A]/95 via-[#0F172A]/50 to-transparent pointer-events-none"></div>
            </div>
            <div className="relative z-10 h-full flex items-center px-12">
              <div className="max-w-[700px]">
                <div className="flex items-center gap-3 mb-4">
                  <span className={`px-3 py-1 rounded-full text-white font-bold text-[12px] ${currentStory.badge === 'HOT' ? 'bg-[#FB2C36]' : 'bg-[#13EC5B]'}`}>
                    {currentStory.badge}
                  </span>
                  <span className="px-3 py-1 bg-[#2B7FFF]/20 border border-[#2B7FFF]/40 rounded-full text-[#2B7FFF] font-semibold text-[12px]">
                    {currentStory.genre}
                  </span>
                </div>
                <h2 className="text-white font-bold text-[48px] leading-tight mb-3">{currentStory.title}</h2>
                <p className="text-[#94A3B8] font-medium text-[18px] mb-4">
                  Tác giả: <span className="text-[#13EC5B]">{currentStory.author}</span>
                </p>
                <p className="text-[#94A3B8] font-normal text-[16px] mb-6 leading-relaxed line-clamp-2">{currentStory.description}</p>
                <div className="flex items-center gap-6 mb-6">
                  <div className="flex items-center gap-2">
                    <Eye className="w-5 h-5 text-[#94A3B8]" />
                    <span className="text-white font-semibold text-[14px]">{currentStory.views}</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <BookOpen className="w-5 h-5 text-[#94A3B8]" />
                    <span className="text-white font-semibold text-[14px]">{currentStory.chapters} chương</span>
                  </div>
                  <div className="flex items-center gap-2">
                    <Star className="w-5 h-5 text-[#FFA500] fill-[#FFA500]" />
                    <span className="text-white font-semibold text-[14px]">{currentStory.rating}</span>
                  </div>
                </div>
                <div className="flex items-center gap-3">
                  <button onClick={() => navigate('/story')} className="px-6 py-3 bg-[#13EC5B] text-white rounded-xl hover:bg-[#11D350] transition-colors font-bold text-[14px]">
                    Đọc ngay
                  </button>
                  <button onClick={() => navigate('/story')} className="px-6 py-3 bg-white/10 backdrop-blur-sm border border-white/20 text-white rounded-xl hover:bg-white/20 transition-colors font-bold text-[14px]">
                    Chi tiết
                  </button>
                </div>
              </div>
            </div>
          </div>

          <button onClick={prevSlide} disabled={isTransitioning} className="absolute left-4 top-1/2 -translate-y-1/2 z-20 w-12 h-12 bg-white/10 backdrop-blur-sm border border-white/20 rounded-full flex items-center justify-center hover:bg-white/20 transition-all disabled:opacity-50 disabled:cursor-not-allowed">
            <ChevronLeft className="w-6 h-6 text-white" />
          </button>
          <button onClick={nextSlide} disabled={isTransitioning} className="absolute right-4 top-1/2 -translate-y-1/2 z-20 w-12 h-12 bg-white/10 backdrop-blur-sm border border-white/20 rounded-full flex items-center justify-center hover:bg-white/20 transition-all disabled:opacity-50 disabled:cursor-not-allowed">
            <ChevronRight className="w-6 h-6 text-white" />
          </button>

          <div className="absolute bottom-6 left-1/2 -translate-x-1/2 z-20 flex items-center gap-2">
            {FEATURED_STORIES.map((_, index) => (
              <button key={index} onClick={() => handleSlideChange(index)} disabled={isTransitioning} className={`h-2 rounded-full transition-all disabled:cursor-not-allowed ${index === currentSlide ? 'w-8 bg-[#13EC5B]' : 'w-2 bg-white/40'}`} />
            ))}
          </div>
        </div>

        <div className="grid grid-cols-5 gap-3 mt-4">
          {FEATURED_STORIES.map((story, index) => (
            <button key={story.id} onClick={() => handleSlideChange(index)} disabled={isTransitioning} className={`group relative h-32 rounded-xl overflow-hidden transition-all disabled:cursor-not-allowed ${index === currentSlide ? 'ring-2 ring-[#13EC5B] scale-105' : 'opacity-60 hover:opacity-100'}`}>
              <ImageWithFallback src={storyImages[story.title]} alt={story.title} className="absolute inset-0 w-full h-full object-cover object-[center_25%]" />
              <div className="absolute inset-0 bg-gradient-to-t from-black/75 via-black/15 to-transparent"></div>
              <div className="absolute bottom-0 inset-x-0 p-2">
                <p className="text-white font-semibold text-[11px] line-clamp-1 drop-shadow-sm">{story.title}</p>
              </div>
            </button>
          ))}
        </div>
      </div>
    </section>
  );
}
