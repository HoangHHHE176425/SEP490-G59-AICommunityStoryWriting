import { Calendar } from 'lucide-react';

export function CommunityEventsWidget() {
  const events = [
    { id: 1, title: 'Cuộc Thi Viết Truyện 2026', date: 'Kết thúc: 31/03', color: '#FB2C36' },
    { id: 2, title: 'Workshop: AI Co-Writing', date: '15/02 - 14:00', color: '#13EC5B' },
    { id: 3, title: 'Gặp Gỡ Tác Giả', date: '20/02 - 19:00', color: '#2B7FFF' },
  ];

  return (
    <div className="bg-white rounded-2xl border border-gray-200 p-5">
      <div className="flex items-center gap-2 mb-4">
        <Calendar className="w-5 h-5 text-[#2B7FFF]" />
        <h3 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[18px]">
          Sự Kiện
        </h3>
      </div>
      <div className="space-y-3">
        {events.map((event) => (
          <div key={event.id} className="p-3 border border-gray-200 rounded-lg hover:border-[#13EC5B] hover:shadow-md transition-all cursor-pointer">
            <div className="flex items-start gap-2 mb-2">
              <div className="w-2 h-2 rounded-full mt-1.5 flex-shrink-0" style={{ backgroundColor: event.color }}></div>
              <h4 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[13px] leading-tight">
                {event.title}
              </h4>
            </div>
            <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[12px] ml-4">
              {event.date}
            </p>
          </div>
        ))}
      </div>
      <button className="w-full mt-3 py-2 bg-[#2B7FFF]/10 text-[#2B7FFF] rounded-lg hover:bg-[#2B7FFF] hover:text-white transition-all font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[13px]">
        Xem tất cả sự kiện
      </button>
    </div>
  );
}
