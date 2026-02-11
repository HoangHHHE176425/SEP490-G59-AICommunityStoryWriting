import React from 'react';
import { ImageWithFallback } from '../figma/ImageWithFallback';
import { Sparkles, Eye, Heart } from 'lucide-react';
import maphapsutoicao from '../../assets/image/maphapsutoicao.jpg';
import dinhmenh from '../../assets/image/dinhmenh.jpg';
import cyber from '../../assets/image/cyber.jpg';
import phamNhanTuTien from '../../assets/image/pham-nhan-tu-tien.jpg';

export function NewAuthorDebutsSection() {
  const debuts = [
    {
      id: 1,
      story: 'Học Đường Tu Ma',
      author: {
        name: 'Bạch Tuyết',
        avatar: 'https://images.unsplash.com/photo-1611199340099-91a595a86812?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHdvbWFuJTIwYXV0aG9yJTIwd3JpdGVyfGVufDF8fHx8MTc3MDI4NTE1M3ww&ixlib=rb-4.1.0&q=80&w=1080',
        joinDate: '1 tuần trước'
      },
      genre: 'Học Đường',
      chapters: 12,
      views: '62K',
      likes: '3.2K',
      description: 'Tác giả mới tài năng với góc nhìn độc đáo về thế giới học đường đầy phép thuật',
      image: phamNhanTuTien
    },
    {
      id: 2,
      story: 'Ma Pháp Học Viện',
      author: {
        name: 'Hắc Phong',
        avatar: 'https://images.unsplash.com/photo-1738566061505-556830f8b8f5?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMG1hbiUyMHBvcnRyYWl0JTIwcHJvZmVzc2lvbmFsfGVufDF8fHx8MTc3MDIxODA0OXww&ixlib=rb-4.1.0&q=80&w=1080',
        joinDate: '2 tuần trước'
      },
      genre: 'Huyền Huyễn',
      chapters: 15,
      views: '125K',
      likes: '8.9K',
      description: 'Debut ấn tượng với hệ thống ma pháp độc đáo, được AI hỗ trợ xây dựng thế giới',
      image: maphapsutoicao
    },
    {
      id: 3,
      story: 'Tổng Tài Yêu Em',
      author: {
        name: 'Lan Hương',
        avatar: 'https://images.unsplash.com/photo-1581065178026-390bc4e78dad?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHdvbWFuJTIwcHJvZmVzc2lvbmFsJTIwcG9ydHJhaXR8ZW58MXx8fHwxNzcwMjc3OTQ1fDA&ixlib=rb-4.1.0&q=80&w=1080',
        joinDate: '3 tuần trước'
      },
      genre: 'Ngôn Tình',
      chapters: 20,
      views: '98K',
      likes: '6.5K',
      description: 'Câu chuyện ngọt ngào được cộng đồng đóng góp ý tưởng phát triển',
      image: dinhmenh
    },
    {
      id: 4,
      story: 'Cyber Chiến Binh',
      author: {
        name: 'Thanh Lương',
        avatar: 'https://images.unsplash.com/photo-1754954865833-c6ee8cb8726d?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHlvdW5nJTIwbWFuJTIwY3JlYXRpdmV8ZW58MXx8fHwxNzcwMjg1MTU2fDA&ixlib=rb-4.1.0&q=80&w=1080',
        joinDate: '5 ngày trước'
      },
      genre: 'Khoa Huyễn',
      chapters: 8,
      views: '85K',
      likes: '5.1K',
      description: 'Tác phẩm khoa huyễn tương lai với AI co-writing hỗ trợ worldbuilding',
      image: cyber
    },
  ];

  return (
    <section className="bg-white rounded-2xl border border-gray-200 p-6">
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-gradient-to-br from-[#13EC5B] to-[#11D350] rounded-lg flex items-center justify-center">
            <Sparkles className="w-5 h-5 text-white" />
          </div>
          <div>
            <h2 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[24px]">
              Tác Giả Mới Debut
            </h2>
            <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[14px]">
              Truyện đầu tay từ tài năng mới
            </p>
          </div>
        </div>
        <button className="text-[#13EC5B] hover:text-[#11D350] font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[14px] flex items-center gap-1">
          Xem tất cả
          <span>→</span>
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {debuts.map((item) => (
          <div key={item.id} className="group p-5 border border-gray-200 rounded-xl hover:border-[#13EC5B] hover:shadow-md transition-all cursor-pointer">
            <div className="flex gap-4">
              {/* Story Image */}
              <div className="w-24 h-32 rounded-lg overflow-hidden flex-shrink-0">
                <ImageWithFallback src={item.image} alt={item.story} className="w-full h-full object-cover" />
              </div>

              {/* Content */}
              <div className="flex-1 min-w-0">
                {/* Story Title */}
                <h3 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[17px] mb-2 group-hover:text-[#13EC5B] transition-colors">
                  {item.story}
                </h3>

                {/* Author Info */}
                <div className="flex items-center gap-2 mb-2">
                  <div className="w-8 h-8 rounded-full overflow-hidden border border-gray-200">
                    <ImageWithFallback src={item.author.avatar} alt={item.author.name} className="w-full h-full object-cover" />
                  </div>
                  <div className="flex-1">
                    <p className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[14px]">
                      {item.author.name}
                    </p>
                    <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[11px]">
                      Tham gia {item.author.joinDate}
                    </p>
                  </div>
                  <span className="px-2 py-1 bg-[#13EC5B] text-white rounded font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[10px]">
                    MỚI
                  </span>
                </div>

                {/* Description */}
                <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[13px] mb-3 line-clamp-2 leading-relaxed">
                  {item.description}
                </p>

                {/* Stats */}
                <div className="flex items-center gap-4">
                  <span className="px-2 py-1 bg-[#2B7FFF]/10 text-[#2B7FFF] rounded font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[11px]">
                    {item.genre}
                  </span>
                  <div className="flex items-center gap-3 text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[12px]">
                    <span>{item.chapters} chương</span>
                    <span>•</span>
                    <div className="flex items-center gap-1">
                      <Eye className="w-3 h-3" />
                      <span>{item.views}</span>
                    </div>
                    <span>•</span>
                    <div className="flex items-center gap-1">
                      <Heart className="w-3 h-3" />
                      <span>{item.likes}</span>
                    </div>
                  </div>
                </div>
              </div>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
