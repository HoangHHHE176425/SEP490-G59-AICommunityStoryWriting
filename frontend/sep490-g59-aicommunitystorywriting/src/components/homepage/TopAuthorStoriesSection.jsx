import React from 'react';
import { ImageWithFallback } from '../figma/ImageWithFallback';
import { Flame, CheckCircle, Eye, Star } from 'lucide-react';
import phongvanthienha from '../../assets/image/phongvanthienha.jpg';
import thanhvanphong from '../../assets/image/thanhvanphong.jpg';
import maphapsutoicao from '../../assets/image/maphapsutoicao.jpg';
import cyber from '../../assets/image/cyber.jpg';
import dinhmenh from '../../assets/image/dinhmenh.jpg';
import quyDaXoa from '../../assets/image/quy-da-xoa.jpg';

export function TopAuthorStoriesSection() {
  const authorStories = [
    {
      id: 1,
      story: 'Phong Vân Thiên Hạ',
      author: {
        name: 'Ngạo Thiên Tử',
        avatar: 'https://images.unsplash.com/photo-1738566061505-556830f8b8f5?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMG1hbiUyMHBvcnRyYWl0JTIwcHJvZmVzc2lvbmFsfGVufDF8fHx8MTc3MDIxODA0OXww&ixlib=rb-4.1.0&q=80&w=1080',
        followers: '125K',
        verified: true
      },
      genre: 'Võ Hiệp',
      chapters: 245,
      views: '3.8M',
      rating: 4.9,
      trending: true,
      aiAssisted: true,
      image: phongvanthienha
    },
    {
      id: 2,
      story: 'Kiếm Thần Truyền Kỳ',
      author: {
        name: 'Huyền Vũ',
        avatar: 'https://images.unsplash.com/photo-1686543972602-da0c7ea61ce2?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMG1hbGUlMjB3cml0ZXIlMjBnbGFzc2VzfGVufDF8fHx8MTc3MDI4NTE1M3ww&ixlib=rb-4.1.0&q=80&w=1080',
        followers: '98K',
        verified: true
      },
      genre: 'Tu Tiên',
      chapters: 189,
      views: '3.2M',
      rating: 4.8,
      trending: true,
      aiAssisted: true,
      image: thanhvanphong
    },
    {
      id: 3,
      story: 'Ma Pháp Sư Tối Cao',
      author: {
        name: 'Minh Trinh',
        avatar: 'https://images.unsplash.com/photo-1581065178026-390bc4e78dad?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHdvbWFuJTIwcHJvZmVzc2lvbmFsJTIwcG9ydHJhaXR8ZW58MXx8fHwxNzcwMjc3OTQ1fDA&ixlib=rb-4.1.0&q=80&w=1080',
        followers: '76K',
        verified: true
      },
      genre: 'Huyền Huyễn',
      chapters: 156,
      views: '2.9M',
      rating: 4.7,
      trending: false,
      aiAssisted: false,
      image: maphapsutoicao
    },
    {
      id: 4,
      story: 'Cyber Thế Giới',
      author: {
        name: 'Thanh Lương',
        avatar: 'https://images.unsplash.com/photo-1754954865833-c6ee8cb8726d?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHlvdW5nJTIwbWFuJTIwY3JlYXRpdmV8ZW58MXx8fHwxNzcwMjg1MTU2fDA&ixlib=rb-4.1.0&q=80&w=1080',
        followers: '54K',
        verified: true
      },
      genre: 'Khoa Huyễn',
      chapters: 98,
      views: '2.6M',
      rating: 4.6,
      trending: true,
      aiAssisted: true,
      image: cyber
    },
    {
      id: 5,
      story: 'Tình Yêu Và Vận Mệnh',
      author: {
        name: 'Kim Dung',
        avatar: 'https://images.unsplash.com/photo-1611199340099-91a595a86812?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHdvbWFuJTIwYXV0aG9yJTIwd3JpdGVyfGVufDF8fHx8MTc3MDI4NTE1M3ww&ixlib=rb-4.1.0&q=80&w=1080',
        followers: '65K',
        verified: true
      },
      genre: 'Ngôn Tình',
      chapters: 134,
      views: '2.3M',
      rating: 4.8,
      trending: false,
      aiAssisted: true,
      image: dinhmenh
    },
    {
      id: 6,
      story: 'Quỷ Dạ Truyền Thuyết',
      author: {
        name: 'Vũ Phong',
        avatar: 'https://images.unsplash.com/photo-1754954865833-c6ee8cb8726d?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHlvdW5nJTIwbWFuJTIwY3JlYXRpdmV8ZW58MXx8fHwxNzcwMjg1MTU2fDA&ixlib=rb-4.1.0&q=80&w=1080',
        followers: '87K',
        verified: true
      },
      genre: 'Kinh Dị',
      chapters: 112,
      views: '2.1M',
      rating: 4.5,
      trending: true,
      aiAssisted: false,
      image: quyDaXoa
    },
  ];

  return (
    <section className="bg-white rounded-2xl border border-gray-200 p-6">
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-gradient-to-br from-[#FB2C36] to-[#E01F2E] rounded-lg flex items-center justify-center">
            <Flame className="w-5 h-5 text-white" />
          </div>
          <div>
            <h2 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[24px]">
              Truyện Từ Tác Giả Hàng Đầu
            </h2>
            <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[14px]">
              Tác phẩm mới nhất từ các tác giả được yêu thích
            </p>
          </div>
        </div>
        <button className="text-[#13EC5B] hover:text-[#11D350] font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[14px] flex items-center gap-1">
          Xem tất cả
          <span>→</span>
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {authorStories.map((item) => (
          <div key={item.id} className="group relative overflow-hidden rounded-xl border border-gray-200 hover:border-[#FB2C36] hover:shadow-xl transition-all cursor-pointer">
            {/* Image */}
            <div className="relative h-48 overflow-hidden">
              <ImageWithFallback src={item.image} alt={item.story} className="w-full h-full object-cover group-hover:scale-110 transition-transform duration-300" />
              <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-black/40 to-transparent"></div>

              {/* Badges */}
              <div className="absolute top-3 right-3 flex flex-col gap-2">
                {item.trending && (
                  <span className="px-2 py-1 bg-[#FB2C36] text-white rounded font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[10px] flex items-center gap-1 shadow-lg">
                    <Flame className="w-3 h-3" />
                    HOT
                  </span>
                )}
              </div>

              {/* Genre */}
              <div className="absolute top-3 left-3">
                <span className="px-2 py-1 bg-white/20 backdrop-blur-sm border border-white/30 text-white rounded font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[10px]">
                  {item.genre}
                </span>
              </div>

              {/* Story Title */}
              <div className="absolute bottom-0 inset-x-0 p-4">
                <h3 className="text-white font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[16px] mb-2 line-clamp-1 group-hover:text-[#13EC5B] transition-colors">
                  {item.story}
                </h3>

                {/* Author Info */}
                <div className="flex items-center gap-2 mb-2">
                  <div className="w-6 h-6 rounded-full overflow-hidden border border-white/50">
                    <ImageWithFallback src={item.author.avatar} alt={item.author.name} className="w-full h-full object-cover" />
                  </div>
                  <span className="text-gray-200 font-['Plus_Jakarta_Sans',sans-serif] font-medium text-[12px]">
                    {item.author.name}
                  </span>
                  {item.author.verified && (
                    <CheckCircle className="w-3 h-3 text-[#2B7FFF]" />
                  )}
                  <span className="text-gray-300 font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[11px]">
                    • {item.author.followers}
                  </span>
                </div>

                {/* Stats */}
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="flex items-center gap-1">
                      <Eye className="w-3 h-3 text-gray-300" />
                      <span className="text-gray-300 font-['Plus_Jakarta_Sans',sans-serif] font-medium text-[11px]">
                        {item.views}
                      </span>
                    </div>
                    <div className="flex items-center gap-1">
                      <Star className="w-3 h-3 text-[#FFA500] fill-[#FFA500]" />
                      <span className="text-white font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[11px]">
                        {item.rating}
                      </span>
                    </div>
                  </div>
                  <span className="text-[#13EC5B] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[11px]">
                    {item.chapters} chương
                  </span>
                </div>
              </div>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
