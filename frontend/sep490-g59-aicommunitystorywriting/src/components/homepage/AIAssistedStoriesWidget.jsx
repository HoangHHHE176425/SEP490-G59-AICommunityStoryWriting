import { ImageWithFallback } from '../figma/ImageWithFallback';
import { Sparkles, Brain, PenTool, ChevronRight, Eye, Star, BookOpen } from 'lucide-react';
import linhVuThienHa from '../../assets/image/linh-vu-thien-ha.jpg';
import cyber from '../../assets/image/cyber.jpg';
import maphapsutoicao from '../../assets/image/maphapsutoicao.jpg';

export function AIAssistedStoriesWidget() {
  const aiStories = [
    {
      id: 1,
      story: 'Thế Giới Song Song',
      author: {
        name: 'Vũ Phong',
        avatar: 'https://images.unsplash.com/photo-1754954865833-c6ee8cb8726d?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHlvdW5nJTIwbWFuJTIwY3JlYXRpdmV8ZW58MXx8fHwxNzcwMjg1MTU2fDA&ixlib=rb-4.1.0&q=80&w=1080',
      },
      genre: 'Huyền Huyễn',
      chapters: 156,
      views: '1.8M',
      rating: 4.8,
      aiFeatures: ['Gợi ý cốt truyện', 'Phát triển nhân vật'],
      image: linhVuThienHa
    },
    {
      id: 2,
      story: 'Cyber Thế Giới',
      author: {
        name: 'Thanh Lương',
        avatar: 'https://images.unsplash.com/photo-1754954865833-c6ee8cb8726d?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHlvdW5nJTIwbWFuJTIwY3JlYXRpdmV8ZW58MXx8fHwxNzcwMjg1MTU2fDA&ixlib=rb-4.1.0&q=80&w=1080',
      },
      genre: 'Khoa Huyễn',
      chapters: 98,
      views: '2.6M',
      rating: 4.6,
      aiFeatures: ['Xây dựng thế giới', 'Kiểm tra logic'],
      image: cyber
    },
    {
      id: 3,
      story: 'Ma Pháp Sư Tối Cao',
      author: {
        name: 'Minh Trinh',
        avatar: 'https://images.unsplash.com/photo-1581065178026-390bc4e78dad?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHdvbWFuJTIwcHJvZmVzc2lvbmFsJTIwcG9ydHJhaXR8ZW58MXx8fHwxNzcwMjc3OTQ1fDA&ixlib=rb-4.1.0&q=80&w=1080',
      },
      genre: 'Huyền Huyễn',
      chapters: 156,
      views: '2.9M',
      rating: 4.7,
      aiFeatures: ['Tạo đối thoại', 'Cảnh hành động'],
      image: maphapsutoicao
    },
  ];

  return (
    <section className="relative bg-gradient-to-br from-[#13EC5B]/10 via-white to-[#2B7FFF]/5 rounded-2xl border-2 border-[#13EC5B]/30 p-8 overflow-hidden">
      {/* Decorative Elements */}
      <div className="absolute top-0 right-0 w-64 h-64 bg-[#13EC5B]/5 rounded-full blur-3xl"></div>
      <div className="absolute bottom-0 left-0 w-48 h-48 bg-[#2B7FFF]/5 rounded-full blur-3xl"></div>
      
      <div className="relative">
        <div className="flex items-center justify-between mb-6">
          <div className="flex items-center gap-3">
            <div className="relative">
              <div className="absolute inset-0 bg-gradient-to-br from-[#13EC5B] to-[#11D350] rounded-xl blur-md opacity-60 animate-pulse"></div>
              <div className="relative w-12 h-12 bg-gradient-to-br from-[#13EC5B] to-[#11D350] rounded-xl flex items-center justify-center shadow-lg">
                <Sparkles className="w-6 h-6 text-white" />
              </div>
            </div>
            <div>
              <h3 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[24px] flex items-center gap-2">
                Truyện Sử Dụng AI Đồng Sáng Tác
                <span className="px-3 py-1 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-full font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[11px] shadow-lg">
                  HOT
                </span>
              </h3>
              <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-medium text-[14px]">
                AI trở thành đồng tác giả, cùng sáng tạo nội dung • <span className="text-[#13EC5B] font-bold">2,450+ tác giả</span> đang sử dụng
              </p>
            </div>
          </div>
          <button className="px-5 py-2.5 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-xl hover:shadow-xl transition-all font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[14px] flex items-center gap-2">
            Xem tất cả
            <ChevronRight className="w-4 h-4" />
          </button>
        </div>

        <div className="grid grid-cols-3 gap-5">
          {aiStories.map((item) => (
            <div key={item.id} className="group cursor-pointer bg-white rounded-2xl border border-gray-200 hover:border-[#13EC5B] hover:shadow-2xl transition-all duration-300 overflow-hidden">
              <div className="relative">
                <ImageWithFallback 
                  src={item.image} 
                  alt={item.story} 
                  className="w-full h-56 object-cover group-hover:scale-110 transition-transform duration-500" 
                />
                {/* AI Badge with Animation */}
                <div className="absolute top-3 right-3">
                  <div className="relative">
                    <div className="absolute inset-0 bg-[#13EC5B] rounded-lg blur-md opacity-60 animate-pulse"></div>
                    <div className="relative px-3 py-1.5 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-lg font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[11px] flex items-center gap-1.5 shadow-lg">
                      <Sparkles className="w-3.5 h-3.5" />
                      AI Hỗ trợ
                    </div>
                  </div>
                </div>
                {/* Genre */}
                <div className="absolute top-3 left-3">
                  <span className="px-3 py-1.5 bg-black/50 backdrop-blur-md text-white rounded-lg font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[11px]">
                    {item.genre}
                  </span>
                </div>
                {/* Overlay Gradient */}
                <div className="absolute inset-0 bg-gradient-to-t from-black/60 via-transparent to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300"></div>
              </div>
              
              <div className="p-4">
                <h4 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[16px] mb-2 line-clamp-1 group-hover:text-[#13EC5B] transition-colors">
                  {item.story}
                </h4>
                
                <div className="flex items-center gap-2 mb-3">
                  <div className="w-6 h-6 rounded-full overflow-hidden border-2 border-[#13EC5B]/30">
                    <ImageWithFallback src={item.author.avatar} alt={item.author.name} className="w-full h-full object-cover" />
                  </div>
                  <span className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-medium text-[12px] line-clamp-1">
                    {item.author.name}
                  </span>
                </div>
                
                {/* AI Features */}
                <div className="flex items-center gap-2 mb-3 flex-wrap">
                  {item.aiFeatures.map((feature, idx) => (
                    <span key={idx} className="px-2 py-1 bg-[#13EC5B]/10 text-[#13EC5B] rounded-md font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[10px]">
                      {feature}
                    </span>
                  ))}
                </div>
                
                <div className="flex items-center justify-between pt-3 border-t border-gray-100">
                  <div className="flex items-center gap-1">
                    <Eye className="w-4 h-4 text-[#90A1B9]" />
                    <span className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[12px]">
                      {item.views}
                    </span>
                  </div>
                  <div className="flex items-center gap-1">
                    <Star className="w-4 h-4 text-[#FFA500] fill-[#FFA500]" />
                    <span className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[12px]">
                      {item.rating}
                    </span>
                  </div>
                  <div className="flex items-center gap-1">
                    <BookOpen className="w-4 h-4 text-[#90A1B9]" />
                    <span className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[12px]">
                      {item.chapters}
                    </span>
                  </div>
                </div>
              </div>
            </div>
          ))}
        </div>

        {/* Bottom CTA */}
        <div className="mt-6 p-5 bg-gradient-to-r from-[#13EC5B]/10 to-[#2B7FFF]/10 rounded-xl border border-[#13EC5B]/20">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-4">
              <div className="flex items-center -space-x-3">
                <div className="w-10 h-10 rounded-full bg-[#13EC5B] flex items-center justify-center border-2 border-white shadow-lg">
                  <Brain className="w-5 h-5 text-white" />
                </div>
                <div className="w-10 h-10 rounded-full bg-[#2B7FFF] flex items-center justify-center border-2 border-white shadow-lg">
                  <PenTool className="w-5 h-5 text-white" />
                </div>
                <div className="w-10 h-10 rounded-full bg-[#FB2C36] flex items-center justify-center border-2 border-white shadow-lg">
                  <Sparkles className="w-5 h-5 text-white" />
                </div>
              </div>
              <div>
                <p className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[15px]">
                  Bắt đầu viết với AI ngay hôm nay
                </p>
                <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[13px]">
                  Miễn phí cho tác giả mới • Không cần thẻ tín dụng
                </p>
              </div>
            </div>
            <button className="px-6 py-3 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-xl hover:shadow-xl hover:scale-105 transition-all font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[14px] flex items-center gap-2">
              <Sparkles className="w-4 h-4" />
              Thử ngay
            </button>
          </div>
        </div>
      </div>
    </section>
  );
}
