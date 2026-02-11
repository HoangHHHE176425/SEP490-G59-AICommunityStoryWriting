import { ImageWithFallback } from '../figma/ImageWithFallback';
import { Sparkles, Trophy, BookOpen, Users, Award } from 'lucide-react';

export function CommunityHighlightsSection() {
  const highlights = [
    {
      id: 1,
      type: 'achievement',
      author: 'Cô Độc Bại Thân',
      content: 'Đạt 50 triệu lượt đọc với truyện "Hành Trình Vạn Cổ"',
      time: '2 ngày trước',
      icon: Trophy,
      color: '#FFA500',
      avatar: 'https://images.unsplash.com/photo-1738566061505-556830f8b8f5?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMG1hbiUyMHBvcnRyYWl0JTIwcHJvZmVzc2lvbmFsfGVufDF8fHx8MTc3MDIxODA0OXww&ixlib=rb-4.1.0&q=80&w=1080'
    },
    {
      id: 2,
      type: 'new_chapter',
      author: 'Kim Dung',
      content: 'Đăng chương mới "Tình Yêu Và Vận Mệnh" - Chương 135',
      time: '1 giờ trước',
      icon: BookOpen,
      color: '#13EC5B',
      avatar: 'https://images.unsplash.com/photo-1611199340099-91a595a86812?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHdvbWFuJTIwYXV0aG9yJTIwd3JpdGVyfGVufDF8fHx8MTc3MDI4NTE1M3ww&ixlib=rb-4.1.0&q=80&w=1080'
    },
    {
      id: 3,
      type: 'collaboration',
      author: 'Vũ Phong & 12 tác giả',
      content: 'Mở dự án collaborative mới "Liên Minh Thần Thoại"',
      time: '3 giờ trước',
      icon: Users,
      color: '#2B7FFF',
      avatar: 'https://images.unsplash.com/photo-1754954865833-c6ee8cb8726d?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHlvdW5nJTIwbWFuJTIwY3JlYXRpdmV8ZW58MXx8fHwxNzcwMjg1MTU2fDA&ixlib=rb-4.1.0&q=80&w=1080'
    },
    {
      id: 4,
      type: 'milestone',
      author: 'Mạc Mặc',
      content: 'Hoàn thành truyện thứ 40 và đạt 100K followers',
      time: '5 giờ trước',
      icon: Award,
      color: '#FB2C36',
      avatar: 'https://images.unsplash.com/photo-1686543972602-da0c7ea61ce2?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMG1hbGUlMjB3cml0ZXIlMjBnbGFzc2VzfGVufDF8fHx8MTc3MDI4NTE1M3ww&ixlib=rb-4.1.0&q=80&w=1080'
    },
  ];

  return (
    <section className="bg-white rounded-2xl border border-gray-200 p-6">
      <div className="flex items-center gap-3 mb-6">
        <div className="w-10 h-10 bg-gradient-to-br from-[#FFA500] to-[#FF8C00] rounded-lg flex items-center justify-center">
          <Sparkles className="w-5 h-5 text-white" />
        </div>
        <div>
          <h2 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[24px]">
            Hoạt Động Cộng Đồng
          </h2>
          <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[14px]">
            Cập nhật mới nhất từ các tác giả
          </p>
        </div>
      </div>

      <div className="space-y-3">
        {highlights.map((item) => {
          const Icon = item.icon;
          return (
            <div key={item.id} className="group flex items-center gap-4 p-4 border border-gray-200 rounded-xl hover:border-[#13EC5B] hover:shadow-md transition-all cursor-pointer">
              <div className="w-12 h-12 rounded-xl overflow-hidden border-2 border-gray-200 flex-shrink-0">
                <ImageWithFallback src={item.avatar} alt={item.author} className="w-full h-full object-cover" />
              </div>
              <div className="flex-1 min-w-0">
                <h3 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[14px] mb-1 group-hover:text-[#13EC5B] transition-colors">
                  {item.author}
                </h3>
                <div className="flex items-center gap-2 mb-1">
                  <div className="w-4 h-4 rounded flex items-center justify-center flex-shrink-0" style={{ backgroundColor: `${item.color}20` }}>
                    <Icon className="w-3 h-3" style={{ color: item.color }} />
                  </div>
                  <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[13px] line-clamp-1">
                    {item.content}
                  </p>
                </div>
                <span className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[11px]">
                  {item.time}
                </span>
              </div>
            </div>
          );
        })}
      </div>
    </section>
  );
}
