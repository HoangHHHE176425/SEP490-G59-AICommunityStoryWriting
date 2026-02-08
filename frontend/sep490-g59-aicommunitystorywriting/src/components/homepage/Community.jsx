import { BookOpen, Calendar, Clock, Gift, Heart, MessageCircle, Megaphone, Sparkles, Star, Trophy, Users } from 'lucide-react';

const ACTIVITIES = [
  { id: 1, user: 'Nguyễn Văn A', action: 'vừa bình luận', story: 'Võ Luyện Điên Phong', time: '5 phút trước', type: 'comment' },
  { id: 2, user: 'Trần Thị B', action: 'vừa đánh giá 5 sao', story: 'Thần Đạo Đan Tôn', time: '12 phút trước', type: 'rating' },
  { id: 3, user: 'Lê Văn C', action: 'vừa theo dõi', story: 'Linh Vũ Thiên Hạ', time: '18 phút trước', type: 'follow' },
  { id: 4, user: 'Phạm Thị D', action: 'vừa thêm vào tủ truyện', story: 'Đại Chúa Tể', time: '25 phút trước', type: 'library' },
  { id: 5, user: 'Hoàng Văn E', action: 'vừa chia sẻ', story: 'Phàm Nhân Tu Tiên', time: '32 phút trước', type: 'share' },
  { id: 6, user: 'Vũ Thị F', action: 'vừa bình luận', story: 'Ma Giới Truyền Kỳ', time: '45 phút trước', type: 'comment' },
];

const EVENTS = [
  { id: 1, title: 'Cuộc Thi Viết Truyện Mùa Xuân 2026', description: 'Tổng giải thưởng 50 triệu đồng. Hạn chót: 31/03/2026', date: 'Kết thúc: 31/03/2026', type: 'contest', status: 'active' },
  { id: 2, title: 'Sự Kiện Nạp Coin Tặng 50%', description: 'Nạp coin nhận ngay 50% khuyến mãi. Áp dụng đến hết ngày 28/02', date: 'Còn 23 ngày', type: 'promotion', status: 'hot' },
  { id: 3, title: 'Gặp Gỡ Tác Giả: Cô Độc Bại Thân', description: 'Buổi offline giao lưu cùng tác giả nổi tiếng', date: '15/02/2026 - 14:00', type: 'event', status: 'upcoming' },
];

const getIcon = (type) => {
  const map = { comment: MessageCircle, rating: Star, follow: Heart, library: BookOpen, share: Users };
  const Icon = map[type] || MessageCircle;
  const color = { comment: '#2B7FFF', rating: '#FFA500', follow: '#FB2C36', library: '#13EC5B', share: '#9333EA' };
  return <Icon className="w-4 h-4" style={{ color: color[type] || '#2B7FFF' }} />;
};

const getEventIcon = (type) => {
  const map = { contest: Trophy, promotion: Gift, event: Megaphone };
  const Icon = map[type] || Calendar;
  return <Icon className="w-5 h-5" />;
};

const getEventBadgeColor = (status) => {
  const map = { active: 'bg-[#13EC5B] text-white', hot: 'bg-[#FB2C36] text-white', upcoming: 'bg-[#2B7FFF] text-white' };
  return map[status] || 'bg-[#90A1B9] text-white';
};

const getEventBadgeText = (status) => (status === 'active' ? 'Đang diễn ra' : status === 'hot' ? 'HOT' : 'Sắp tới');

export function Community() {
  return (
    <section className="bg-white rounded-2xl border border-gray-200 p-6">
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-gradient-to-br from-[#2B7FFF] to-[#1E5FD9] rounded-lg flex items-center justify-center">
            <Users className="w-5 h-5 text-white" />
          </div>
          <div>
            <h2 className="text-[#1A2332] font-bold text-[24px]">Hoạt Động Cộng Đồng</h2>
            <p className="text-[#90A1B9] font-normal text-[14px]">Những tương tác và sự kiện mới nhất</p>
          </div>
        </div>
        <button className="text-[#13EC5B] hover:text-[#11D350] font-semibold text-[14px] flex items-center gap-1">
          Xem tất cả <span>→</span>
        </button>
      </div>

      <div className="mb-6">
        <h3 className="text-[#1A2332] font-bold text-[16px] mb-3 flex items-center gap-2">
          <Sparkles className="w-4 h-4 text-[#FFA500]" />
          Sự Kiện Nổi Bật
        </h3>
        <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
          {EVENTS.map((event) => (
            <div key={event.id} className="group relative p-4 border-2 border-gray-200 rounded-xl hover:border-[#13EC5B] hover:shadow-lg transition-all cursor-pointer overflow-hidden">
              <div className="absolute inset-0 bg-gradient-to-br from-[#13EC5B]/5 to-[#2B7FFF]/5 opacity-0 group-hover:opacity-100 transition-opacity"></div>
              <div className="relative z-10">
                <div className="flex items-start justify-between mb-3">
                  <div className="w-12 h-12 bg-gradient-to-br from-[#13EC5B]/20 to-[#2B7FFF]/20 rounded-xl flex items-center justify-center">
                    {getEventIcon(event.type)}
                  </div>
                  <span className={`px-2 py-1 rounded-full font-bold text-[10px] ${getEventBadgeColor(event.status)}`}>{getEventBadgeText(event.status)}</span>
                </div>
                <h4 className="text-[#1A2332] font-bold text-[15px] mb-2 line-clamp-2 group-hover:text-[#13EC5B] transition-colors">{event.title}</h4>
                <p className="text-[#90A1B9] font-normal text-[13px] mb-3 line-clamp-2">{event.description}</p>
                <div className="flex items-center gap-2 text-[#90A1B9]">
                  <Calendar className="w-4 h-4" />
                  <span className="font-medium text-[12px]">{event.date}</span>
                </div>
              </div>
            </div>
          ))}
        </div>
      </div>

      <div>
        <h3 className="text-[#1A2332] font-bold text-[16px] mb-3 flex items-center gap-2">
          <Clock className="w-4 h-4 text-[#2B7FFF]" />
          Hoạt Động Gần Đây
        </h3>
        <div className="space-y-3">
          {ACTIVITIES.map((activity) => (
            <div key={activity.id} className="flex items-start gap-3 p-3 border border-gray-100 rounded-xl hover:border-[#13EC5B] hover:bg-gray-50 transition-all cursor-pointer">
              <div className="w-10 h-10 bg-gradient-to-br from-[#13EC5B]/20 to-[#2B7FFF]/20 rounded-full flex items-center justify-center flex-shrink-0">
                {getIcon(activity.type)}
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-[#1A2332] font-normal text-[14px] mb-1">
                  <span className="font-bold">{activity.user}</span> {activity.action} <span className="font-semibold text-[#13EC5B]">{activity.story}</span>
                </p>
                <p className="text-[#90A1B9] font-normal text-[12px]">{activity.time}</p>
              </div>
            </div>
          ))}
        </div>
      </div>
    </section>
  );
}
