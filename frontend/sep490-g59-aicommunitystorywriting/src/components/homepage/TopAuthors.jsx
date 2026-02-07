import { ImageWithFallback } from '../figma/ImageWithFallback';
import { Award, CheckCircle } from 'lucide-react';

const AUTHORS = [
  { id: 1, name: 'Cô Độc Bại Thân', stories: 45, followers: '125K', avatar: 'https://images.unsplash.com/photo-1738566061505-556830f8b8f5?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&q=80&w=1080', verified: true },
  { id: 2, name: 'Mạc Mặc', stories: 38, followers: '98K', avatar: 'https://images.unsplash.com/photo-1686543972602-da0c7ea61ce2?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&q=80&w=1080', verified: true },
  { id: 3, name: 'Vũ Phong', stories: 52, followers: '87K', avatar: 'https://images.unsplash.com/photo-1754954865833-c6ee8cb8726d?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&q=80&w=1080', verified: true },
  { id: 4, name: 'Thiên Tàm Thổ Đậu', stories: 29, followers: '76K', avatar: 'https://images.unsplash.com/photo-1665391909720-b1e6d6d96c90?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&q=80&w=1080', verified: true },
  { id: 5, name: 'Vong Ngữ', stories: 41, followers: '65K', avatar: 'https://images.unsplash.com/photo-1611199340099-91a595a86812?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&q=80&w=1080', verified: true },
  { id: 6, name: 'Kim Dung', stories: 33, followers: '54K', avatar: 'https://images.unsplash.com/photo-1581065178026-390bc4e78dad?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&q=80&w=1080', verified: true },
];

export function TopAuthors() {
  return (
    <section className="bg-white rounded-2xl border border-gray-200 p-6">
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-gradient-to-br from-[#FFA500] to-[#FF8C00] rounded-lg flex items-center justify-center">
            <Award className="w-5 h-5 text-white" />
          </div>
          <div>
            <h2 className="text-[#1A2332] font-bold text-[24px]">Tác Giả Xuất Sắc</h2>
            <p className="text-[#90A1B9] font-normal text-[14px]">Những cây bút hàng đầu của nền tảng</p>
          </div>
        </div>
        <button className="text-[#13EC5B] hover:text-[#11D350] font-semibold text-[14px] flex items-center gap-1">
          Xem tất cả <span>→</span>
        </button>
      </div>
      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {AUTHORS.map((author) => (
          <div key={author.id} className="group p-4 border border-gray-200 rounded-xl hover:border-[#13EC5B] hover:shadow-md transition-all cursor-pointer">
            <div className="flex items-center gap-3">
              <div className="w-14 h-14 rounded-full overflow-hidden flex-shrink-0 border-2 border-[#13EC5B]/20">
                <ImageWithFallback src={author.avatar} alt={author.name} className="w-full h-full object-cover" />
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-1 mb-1">
                  <h3 className="text-[#1A2332] font-bold text-[15px] truncate group-hover:text-[#13EC5B] transition-colors">{author.name}</h3>
                  {author.verified && <CheckCircle className="w-4 h-4 text-[#2B7FFF] flex-shrink-0" />}
                </div>
                <div className="flex items-center gap-3 text-[#90A1B9] font-normal text-[12px]">
                  <span>{author.stories} truyện</span>
                  <span>•</span>
                  <span>{author.followers} người theo dõi</span>
                </div>
              </div>
            </div>
            <button className="w-full mt-3 py-2 bg-[#13EC5B]/10 text-[#13EC5B] rounded-lg hover:bg-[#13EC5B] hover:text-white transition-colors font-semibold text-[13px]">
              Theo dõi
            </button>
          </div>
        ))}
      </div>
    </section>
  );
}
