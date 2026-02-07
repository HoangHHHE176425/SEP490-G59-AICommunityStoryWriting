import { useState } from 'react';
import { Link } from 'react-router-dom';
import { Eye, Flame, TrendingUp } from 'lucide-react';

const RANKINGS = {
  day: [
    { id: 1, title: 'Võ Luyện Điên Phong', author: 'Mạc Mặc', views: '125K', trend: 'up' },
    { id: 2, title: 'Thần Đạo Đan Tôn', author: 'Cô Độc Bại Thân', views: '98K', trend: 'up' },
    { id: 3, title: 'Linh Vũ Thiên Hạ', author: 'Vũ Phong', views: '87K', trend: 'same' },
    { id: 4, title: 'Đại Chúa Tể', author: 'Thiên Tàm', views: '76K', trend: 'down' },
    { id: 5, title: 'Phàm Nhân Tu Tiên', author: 'Vong Ngữ', views: '65K', trend: 'up' },
    { id: 6, title: 'Ma Giới Truyền Kỳ', author: 'Huyền Vũ', views: '54K', trend: 'up' },
    { id: 7, title: 'Thiên Long Bát Bộ', author: 'Kim Dung', views: '48K', trend: 'same' },
    { id: 8, title: 'Bí Ẩn Thế Giới', author: 'Minh Trinh', views: '42K', trend: 'down' },
    { id: 9, title: 'Kiếm Đạo Độc Tôn', author: 'Thanh Lương', views: '38K', trend: 'up' },
    { id: 10, title: 'Nguyên Tôn', author: 'Thiên Tàm', views: '32K', trend: 'same' },
  ],
  week: [
    { id: 1, title: 'Phàm Nhân Tu Tiên', author: 'Vong Ngữ', views: '542K', trend: 'up' },
    { id: 2, title: 'Võ Luyện Điên Phong', author: 'Mạc Mặc', views: '498K', trend: 'same' },
    { id: 3, title: 'Đại Chúa Tể', author: 'Thiên Tàm', views: '432K', trend: 'up' },
    { id: 4, title: 'Thần Đạo Đan Tôn', author: 'Cô Độc Bại Thân', views: '387K', trend: 'down' },
    { id: 5, title: 'Linh Vũ Thiên Hạ', author: 'Vũ Phong', views: '354K', trend: 'up' },
    { id: 6, title: 'Ma Giới Truyền Kỳ', author: 'Huyền Vũ', views: '298K', trend: 'up' },
    { id: 7, title: 'Thiên Long Bát Bộ', author: 'Kim Dung', views: '276K', trend: 'same' },
    { id: 8, title: 'Bí Ẩn Thế Giới', author: 'Minh Trinh', views: '245K', trend: 'down' },
    { id: 9, title: 'Kiếm Đạo Độc Tôn', author: 'Thanh Lương', views: '198K', trend: 'up' },
    { id: 10, title: 'Nguyên Tôn', author: 'Thiên Tàm', views: '176K', trend: 'same' },
  ],
  month: [
    { id: 1, title: 'Đại Chúa Tể', author: 'Thiên Tàm', views: '2.1M', trend: 'up' },
    { id: 2, title: 'Phàm Nhân Tu Tiên', author: 'Vong Ngữ', views: '1.9M', trend: 'up' },
    { id: 3, title: 'Võ Luyện Điên Phong', author: 'Mạc Mặc', views: '1.7M', trend: 'same' },
    { id: 4, title: 'Linh Vũ Thiên Hạ', author: 'Vũ Phong', views: '1.5M', trend: 'down' },
    { id: 5, title: 'Thần Đạo Đan Tôn', author: 'Cô Độc Bại Thân', views: '1.3M', trend: 'up' },
    { id: 6, title: 'Ma Giới Truyền Kỳ', author: 'Huyền Vũ', views: '1.1M', trend: 'up' },
    { id: 7, title: 'Thiên Long Bát Bộ', author: 'Kim Dung', views: '987K', trend: 'same' },
    { id: 8, title: 'Bí Ẩn Thế Giới', author: 'Minh Trinh', views: '876K', trend: 'down' },
    { id: 9, title: 'Kiếm Đạo Độc Tôn', author: 'Thanh Lương', views: '765K', trend: 'up' },
    { id: 10, title: 'Nguyên Tôn', author: 'Thiên Tàm', views: '654K', trend: 'same' },
  ],
};

const getRankColor = (rank) => {
  if (rank === 1) return 'from-[#FFA500] to-[#FF8C00]';
  if (rank === 2) return 'from-[#C0C0C0] to-[#A8A8A8]';
  if (rank === 3) return 'from-[#CD7F32] to-[#B8722A]';
  return 'from-[#13EC5B] to-[#11D350]';
};

const getTrendIcon = (trend) => {
  if (trend === 'up') return <TrendingUp className="w-3 h-3 text-[#13EC5B]" />;
  if (trend === 'down') return <TrendingUp className="w-3 h-3 text-[#FB2C36] rotate-180" />;
  return <span className="w-3 h-0.5 bg-[#90A1B9]"></span>;
};

export function Rankings() {
  const [activeTab, setActiveTab] = useState('day');
  const currentRankings = RANKINGS[activeTab];

  return (
    <section className="bg-white rounded-2xl border border-gray-200 p-6">
      <div className="flex items-center gap-3 mb-6">
        <div className="w-10 h-10 bg-gradient-to-br from-[#FB2C36] to-[#E01F2E] rounded-lg flex items-center justify-center">
          <Flame className="w-5 h-5 text-white" />
        </div>
        <div>
          <h2 className="text-[#1A2332] font-bold text-[20px]">Bảng Xếp Hạng</h2>
          <p className="text-[#90A1B9] font-normal text-[12px]">Truyện được đọc nhiều nhất</p>
        </div>
      </div>
      <div className="flex items-center gap-2 mb-6 p-1 bg-gray-100 rounded-lg">
        {['day', 'week', 'month'].map((tab) => (
          <button key={tab} onClick={() => setActiveTab(tab)} className={`flex-1 py-2 rounded-lg transition-all font-semibold text-[13px] ${activeTab === tab ? 'bg-white text-[#13EC5B] shadow-sm' : 'text-[#90A1B9] hover:text-[#1A2332]'}`}>
            {tab === 'day' ? 'Ngày' : tab === 'week' ? 'Tuần' : 'Tháng'}
          </button>
        ))}
      </div>
      <div className="space-y-3">
        {currentRankings.map((story, index) => (
          <div key={story.id} className="group flex items-start gap-3 p-3 border border-gray-100 rounded-xl hover:border-[#13EC5B] hover:bg-gray-50 transition-all cursor-pointer">
            <div className={`w-8 h-8 bg-gradient-to-br ${getRankColor(index + 1)} rounded-lg flex items-center justify-center text-white font-bold text-[14px] flex-shrink-0`}>
              {index + 1}
            </div>
            <div className="flex-1 min-w-0">
              <h3 className="text-[#1A2332] font-bold text-[14px] truncate group-hover:text-[#13EC5B] transition-colors mb-1">{story.title}</h3>
              <p className="text-[#90A1B9] font-normal text-[12px] truncate mb-1">{story.author}</p>
              <div className="flex items-center gap-2">
                <div className="flex items-center gap-1">
                  <Eye className="w-3 h-3 text-[#90A1B9]" />
                  <span className="text-[#90A1B9] font-medium text-[11px]">{story.views}</span>
                </div>
                {getTrendIcon(story.trend)}
              </div>
            </div>
          </div>
        ))}
      </div>
      <Link to="/story-list" className="block w-full mt-4 py-3 border border-gray-200 rounded-xl hover:border-[#13EC5B] hover:bg-gray-50 transition-all font-semibold text-[14px] text-[#13EC5B] text-center">
        Xem toàn bộ bảng xếp hạng
      </Link>
    </section>
  );
}
