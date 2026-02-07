import { Link } from 'react-router-dom';
import { ImageWithFallback } from '../figma/ImageWithFallback';
import { Eye, Flame, Star } from 'lucide-react';
import { storyImages } from '../../assets/image/storyImages';

const STORIES = [
  { id: 1, title: 'Phong Vân Thiên Hạ', author: 'Ngạo Thiên Tử', chapter: 'Chương 1245', genre: 'Võ Hiệp', views: '3.8M', rating: 4.9, badge: 'HOT' },
  { id: 2, title: 'Kiếm Thần Truyền Kỳ', author: 'Huyền Vũ', chapter: 'Chương 987', genre: 'Tu Tiên', views: '3.2M', rating: 4.8, badge: 'HOT' },
  { id: 3, title: 'Ma Pháp Sư Tối Cao', author: 'Minh Trinh', chapter: 'Chương 756', genre: 'Huyền Huyễn', views: '2.9M', rating: 4.7 },
  { id: 4, title: 'Cyber Thế Giới', author: 'Thanh Lương', chapter: 'Chương 543', genre: 'Khoa Huyễn', views: '2.6M', rating: 4.6, badge: 'HOT' },
  { id: 5, title: 'Tình Yêu Và Vận Mệnh', author: 'Kim Dung', chapter: 'Chương 321', genre: 'Ngôn Tình', views: '2.3M', rating: 4.8 },
  { id: 6, title: 'Quỷ Dạ Truyền Thuyết', author: 'Vũ Phong', chapter: 'Chương 198', genre: 'Kinh Dị', views: '2.1M', rating: 4.5, badge: 'HOT' },
];

export function HotStories() {
  return (
    <section className="bg-white rounded-2xl border border-gray-200 p-6">
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-gradient-to-br from-[#FB2C36] to-[#E01F2E] rounded-lg flex items-center justify-center">
            <Flame className="w-5 h-5 text-white" />
          </div>
          <div>
            <h2 className="text-[#1A2332] font-bold text-[24px]">Truyện Hot</h2>
            <p className="text-[#90A1B9] font-normal text-[14px]">Truyện được yêu thích nhất hiện nay</p>
          </div>
        </div>
        <Link to="/story-list" className="text-[#13EC5B] hover:text-[#11D350] font-semibold text-[14px] flex items-center gap-1">
          Xem tất cả <span>→</span>
        </Link>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {STORIES.map((story) => (
          <div key={story.id} className="group relative overflow-hidden rounded-xl border border-gray-200 hover:border-[#FB2C36] hover:shadow-xl transition-all cursor-pointer">
            <div className="relative h-48 overflow-hidden">
              <ImageWithFallback src={storyImages[story.title]} alt={story.title} className="w-full h-full object-cover object-[center_25%] group-hover:scale-110 transition-transform duration-300" />
              <div className="absolute inset-0 bg-gradient-to-t from-black/75 via-black/25 to-transparent"></div>
              {story.badge && (
                <div className="absolute top-3 right-3">
                  <span className="px-3 py-1 bg-[#FB2C36] text-white rounded-full font-bold text-[11px] flex items-center gap-1 shadow-lg">
                    <Flame className="w-3 h-3" /> {story.badge}
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
                  <span className="text-[#13EC5B] font-bold text-[11px]">{story.chapter}</span>
                </div>
              </div>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
