import { Link } from 'react-router-dom';
import { Compass, BookOpen, PenLine, Sparkles, ChevronRight } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';

/**
 * Thay cho widget "Sự kiện" (nội dung tĩnh / không có API).
 * Gợi ý hành động phù hợp nền tảng: đọc truyện, trở thành tác giả, xem mục AI.
 */
export function SidebarDiscoverWidget() {
  const { role } = useAuth();
  const roleUpper = (role ?? '').toString().toUpperCase();
  const isAuthor = roleUpper === 'AUTHOR';
  const becomeAuthorHref = isAuthor ? '/author' : '/policy?type=AUTHOR&from=home-sidebar&next=/author';

  const items = [
    {
      to: '/story-list',
      title: 'Khám phá truyện',
      desc: 'Lọc thể loại, sắp xếp theo lượt xem',
      icon: BookOpen,
      accent: '#2B7FFF',
    },
    {
      to: becomeAuthorHref,
      title: isAuthor ? 'Trang quản lý truyện' : 'Trở thành tác giả',
      desc: isAuthor ? 'Viết & xuất bản tác phẩm' : 'Điều khoản và đăng ký vai trò',
      icon: PenLine,
      accent: '#13EC5B',
    },
    {
      to: '/story-list?usesAi=true',
      title: 'Truyện AI đồng sáng tác',
      desc: 'Danh sách truyện có chương đồng sáng tác với AI',
      icon: Sparkles,
      accent: '#FB2C36',
    },
  ];

  return (
    <div className="bg-white rounded-2xl border border-gray-200 p-5">
      <div className="flex items-center gap-2 mb-1">
        <Compass className="w-5 h-5 text-[#13EC5B]" />
        <h3 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[18px]">
          Khám phá nhanh
        </h3>
      </div>
      <p className="text-[11px] text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] mb-4">
        Lối tắt phù hợp nội dung nền tảng
      </p>
      <div className="space-y-2">
        {items.map((item) => {
          const Icon = item.icon;
          return (
            <Link
              key={item.title}
              to={item.to}
              className="flex items-start gap-3 p-3 rounded-xl border border-gray-200 hover:border-[#13EC5B]/60 hover:bg-gray-50/80 transition-all group"
            >
              <div
                className="w-9 h-9 rounded-lg flex items-center justify-center flex-shrink-0 mt-0.5"
                style={{ backgroundColor: `${item.accent}14` }}
              >
                <Icon className="w-4 h-4" style={{ color: item.accent }} />
              </div>
              <div className="flex-1 min-w-0">
                <div className="flex items-center gap-1">
                  <span className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[13px] leading-tight group-hover:text-[#13EC5B] transition-colors">
                    {item.title}
                  </span>
                  <ChevronRight className="w-3.5 h-3.5 text-[#90A1B9] flex-shrink-0 opacity-0 group-hover:opacity-100 transition-opacity" />
                </div>
                <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] text-[11px] mt-0.5 leading-snug">
                  {item.desc}
                </p>
              </div>
            </Link>
          );
        })}
      </div>
    </div>
  );
}
