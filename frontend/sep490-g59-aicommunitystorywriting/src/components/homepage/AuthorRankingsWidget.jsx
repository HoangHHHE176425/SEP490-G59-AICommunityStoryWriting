import { ImageWithFallback } from '../figma/ImageWithFallback';
import { Trophy } from 'lucide-react';

export function AuthorRankingsWidget() {
  const rankings = [
    { rank: 1, name: 'Cô Độc Bại Thân', points: '12,580', avatar: 'https://images.unsplash.com/photo-1738566061505-556830f8b8f5?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMG1hbiUyMHBvcnRyYWl0JTIwcHJvZmVzc2lvbmFsfGVufDF8fHx8MTc3MDIxODA0OXww&ixlib=rb-4.1.0&q=80&w=1080' },
    { rank: 2, name: 'Mạc Mặc', points: '11,245', avatar: 'https://images.unsplash.com/photo-1686543972602-da0c7ea61ce2?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMG1hbGUlMjB3cml0ZXIlMjBnbGFzc2VzfGVufDF8fHx8MTc3MDI4NTE1M3ww&ixlib=rb-4.1.0&q=80&w=1080' },
    { rank: 3, name: 'Vũ Phong', points: '10,892', avatar: 'https://images.unsplash.com/photo-1754954865833-c6ee8cb8726d?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHlvdW5nJTIwbWFuJTIwY3JlYXRpdmV8ZW58MXx8fHwxNzcwMjg1MTU2fDA&ixlib=rb-4.1.0&q=80&w=1080' },
    { rank: 4, name: 'Thiên Tàm Thổ Đậu', points: '9,567', avatar: 'https://images.unsplash.com/photo-1665391909720-b1e6d6d96c90?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMGVsZGVybHklMjBtYW4lMjB3aXNlfGVufDF8fHx8MTc3MDI4NTE1NXww&ixlib=rb-4.1.0&q=80&w=1080' },
    { rank: 5, name: 'Vong Ngữ', points: '8,934', avatar: 'https://images.unsplash.com/photo-1611199340099-91a595a86812?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHdvbWFuJTIwYXV0aG9yJTIwd3JpdGVyfGVufDF8fHx8MTc3MDI4NTE1M3ww&ixlib=rb-4.1.0&q=80&w=1080' },
  ];

  return (
    <div className="bg-white rounded-2xl border border-gray-200 p-5">
      <div className="flex items-center gap-2 mb-4">
        <Trophy className="w-5 h-5 text-[#FFA500]" />
        <h3 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[18px]">
          Bảng Xếp Hạng Tác Giả
        </h3>
      </div>
      <div className="space-y-3">
        {rankings.map((author) => (
          <div key={author.rank} className="flex items-center gap-3 p-3 rounded-lg hover:bg-gray-50 transition-colors cursor-pointer">
            <div className={`flex items-center justify-center w-7 h-7 rounded-full font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[13px] flex-shrink-0 ${
              author.rank <= 3 ? 'bg-gradient-to-br from-[#FFA500] to-[#FF8C00] text-white' : 'bg-gray-100 text-[#90A1B9]'
            }`}>
              {author.rank}
            </div>
            <div className="w-10 h-10 rounded-full overflow-hidden flex-shrink-0">
              <ImageWithFallback src={author.avatar} alt={author.name} className="w-full h-full object-cover" />
            </div>
            <div className="flex-1 min-w-0">
              <p className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[13px] truncate">
                {author.name}
              </p>
              <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[11px]">
                {author.points} điểm
              </p>
            </div>
          </div>
        ))}
      </div>
      <button className="w-full mt-4 py-2 text-[#13EC5B] hover:text-[#11D350] font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[13px]">
        Xem bảng xếp hạng đầy đủ →
      </button>
    </div>
  );
}
