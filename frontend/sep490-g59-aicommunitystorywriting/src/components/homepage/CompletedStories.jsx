import { Link } from 'react-router-dom';
import { ImageWithFallback } from '../figma/ImageWithFallback';
import { CheckCircle, Eye, Star } from 'lucide-react';
import { storyImages } from '../../assets/image/storyImages';

const STORIES = [
  { id: 1, title: 'Hành Trình Vạn Dặm', author: 'Ngạo Thiên Tử', totalChapters: '2580', genre: 'Võ Hiệp', views: '5.2M', rating: 4.9, badge: 'FULL' },
  { id: 2, title: 'Thiên Đạo Chí Tôn', author: 'Vũ Phong', totalChapters: '3200', genre: 'Tu Tiên', views: '4.8M', rating: 4.8, badge: 'FULL' },
  { id: 3, title: 'Vương Giả Trở Lại', author: 'Huyền Vũ', totalChapters: '1850', genre: 'Huyền Huyễn', views: '4.2M', rating: 4.7 },
  { id: 4, title: 'Tuyệt Đỉnh Võ Thần', author: 'Mạc Mặc', totalChapters: '2100', genre: 'Võ Hiệp', views: '3.9M', rating: 4.8, badge: 'FULL' },
  { id: 5, title: 'Ma Đạo Tông Sư', author: 'Thanh Lương', totalChapters: '1650', genre: 'Huyền Huyễn', views: '3.5M', rating: 4.9 },
  { id: 6, title: 'Kiếm Đạo Độc Tôn', author: 'Vong Ngữ', totalChapters: '2450', genre: 'Tu Tiên', views: '3.2M', rating: 4.6, badge: 'FULL' },
];

export function CompletedStories() {
  return (
    <section className="bg-white rounded-2xl border border-gray-200 p-6">
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-gradient-to-br from-[#2B7FFF] to-[#1E5FD9] rounded-lg flex items-center justify-center">
            <CheckCircle className="w-5 h-5 text-white" />
          </div>
          <div>
            <h2 className="text-[#1A2332] font-bold text-[24px]">Truyện Full</h2>
            <p className="text-[#90A1B9] font-normal text-[14px]">Những truyện đã hoàn thành đáng đọc</p>
          </div>
        </div>
        <Link to="/story-list" className="text-[#13EC5B] hover:text-[#11D350] font-semibold text-[14px] flex items-center gap-1">
          Xem tất cả <span>→</span>
        </Link>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {STORIES.map((story) => (
          <div key={story.id} className="group relative overflow-hidden rounded-xl border border-gray-200 hover:border-[#2B7FFF] hover:shadow-xl transition-all cursor-pointer">
            <div className="relative h-48 overflow-hidden">
              <ImageWithFallback src={storyImages[story.title]} alt={story.title} className="w-full h-full object-cover object-[center_25%] group-hover:scale-110 transition-transform duration-300" />
              <div className="absolute inset-0 bg-gradient-to-t from-black/75 via-black/25 to-transparent"></div>
              {story.badge && (
                <div className="absolute top-3 right-3">
                  <span className="px-3 py-1 bg-[#2B7FFF] text-white rounded-full font-bold text-[11px] flex items-center gap-1 shadow-lg">
                    <CheckCircle className="w-3 h-3" /> {story.badge}
                  </span>
                </div>
              )}
              <div className="absolute top-3 left-3">
                <span className="px-2 py-1 bg-white/20 backdrop-blur-sm border border-white/30 text-white rounded font-semibold text-[10px]">{story.genre}</span>
              </div>
              <div className="absolute bottom-0 inset-x-0 p-4">
                <h3 className="text-white font-bold text-[16px] mb-1 line-clamp-1 group-hover:text-[#13EC5B] transition-colors">{story.title}</h3>
                <p className="text-gray-300 font-normal text-[12px] mb-2">{story.author}</p>
                <div className="flex items-center justify-between">
                  <div className="flex items-center gap-3">
                    <div className="flex items-center gap-1">
                      <Eye className="w-3 h-3 text-gray-300" />
                      <span className="text-gray-300 font-medium text-[11px]">{story.views}</span>
                    </div>
                    <div className="flex items-center gap-1">
                      <Star className="w-3 h-3 text-[#FFA500] fill-[#FFA500]" />
                      <span className="text-white font-semibold text-[11px]">{story.rating}</span>
                    </div>
                  </div>
                  <span className="text-[#13EC5B] font-bold text-[11px]">{story.totalChapters} chương</span>
                </div>
              </div>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
