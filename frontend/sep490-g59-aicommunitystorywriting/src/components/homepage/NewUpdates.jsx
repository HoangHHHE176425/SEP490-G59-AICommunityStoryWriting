import { Link } from 'react-router-dom';
import { ImageWithFallback } from '../figma/ImageWithFallback';
import { Clock, Eye } from 'lucide-react';
import { storyImages } from '../../assets/image/storyImages';

const STORIES = [
  { id: 1, title: 'Võ Luyện Điên Phong', author: 'Mạc Mặc', chapter: 'Chương 5420', genre: 'Võ Hiệp', views: '2.5M', badge: 'HOT' },
  { id: 2, title: 'Thần Đạo Đan Tôn', author: 'Cô Độc Bại Thân', chapter: 'Chương 3128', genre: 'Tu Tiên', views: '1.8M', badge: 'NEW' },
  { id: 3, title: 'Linh Vũ Thiên Hạ', author: 'Vũ Phong', chapter: 'Chương 3401', genre: 'Huyền Huyễn', views: '1.2M' },
  { id: 4, title: 'Phàm Nhân Tu Tiên', author: 'Vong Ngữ', chapter: 'Chương 2451', genre: 'Tu Tiên', views: '950K' },
  { id: 5, title: 'Đại Chúa Tể', author: 'Thiên Tàm Thổ Đậu', chapter: 'Chương 1876', genre: 'Huyền Huyễn', views: '850K', badge: 'HOT' },
  { id: 6, title: 'Bí Ẩn Thế Giới', author: 'Minh Trinh', chapter: 'Chương 987', genre: 'Trinh Thám', views: '720K', badge: 'NEW' },
  { id: 7, title: 'Ma Giới Truyền Kỳ', author: 'Huyền Vũ', chapter: 'Chương 1456', genre: 'Huyền Huyễn', views: '680K' },
  { id: 8, title: 'Thiên Long Bát Bộ', author: 'Kim Dung', chapter: 'Chương 234', genre: 'Võ Hiệp', views: '590K' },
];

export function NewUpdates() {
  return (
    <section className="bg-white rounded-2xl border border-gray-200 p-6">
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-gradient-to-br from-[#13EC5B] to-[#11D350] rounded-lg flex items-center justify-center">
            <Clock className="w-5 h-5 text-white" />
          </div>
          <div>
            <h2 className="text-[#1A2332] font-bold text-[24px]">Truyện Mới Cập Nhật</h2>
            <p className="text-[#90A1B9] font-normal text-[14px]">Những chương mới nhất từ các tác giả</p>
          </div>
        </div>
        <Link to="/story-list" className="text-[#13EC5B] hover:text-[#11D350] font-semibold text-[14px] flex items-center gap-1">
          Xem tất cả <span>→</span>
        </Link>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
        {STORIES.map((story) => (
          <div key={story.id} className="group p-4 border border-gray-200 rounded-xl hover:border-[#13EC5B] hover:shadow-md transition-all cursor-pointer">
            <div className="flex gap-4">
              <div className="w-16 h-20 rounded-lg overflow-hidden flex-shrink-0">
                <ImageWithFallback src={storyImages[story.title]} alt={story.title} className="w-full h-full object-cover object-[center_25%]" />
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-2 mb-1">
                  <h3 className="text-[#1A2332] font-bold text-[15px] truncate group-hover:text-[#13EC5B] transition-colors">{story.title}</h3>
                  {story.badge && (
                    <span className={`px-2 py-0.5 rounded text-white font-bold text-[10px] ${story.badge === 'HOT' ? 'bg-[#FB2C36]' : 'bg-[#2B7FFF]'}`}>
                      {story.badge}
                    </span>
                  )}
                </div>
                <p className="text-[#90A1B9] font-normal text-[13px] mb-2 truncate">{story.author}</p>
                <div className="flex items-center gap-3 flex-wrap">
                  <span className="inline-block px-2 py-0.5 bg-[#2B7FFF]/10 text-[#2B7FFF] rounded font-semibold text-[11px]">{story.genre}</span>
                  <span className="text-[#13EC5B] font-bold text-[11px]">{story.chapter}</span>
                  <div className="flex items-center gap-1">
                    <Eye className="w-3 h-3 text-[#90A1B9]" />
                    <span className="text-[#90A1B9] font-medium text-[11px]">{story.views}</span>
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
