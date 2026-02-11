import { ImageWithFallback } from '../figma/ImageWithFallback';
import { TrendingUp, Flame, CheckCircle, UserPlus } from 'lucide-react';

export function TrendingAuthorsSection() {
  const trendingAuthors = [
    {
      id: 1,
      name: 'Nguyệt Hạ',
      avatar: 'https://images.unsplash.com/photo-1611199340099-91a595a86812?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHdvbWFuJTIwYXV0aG9yJTIwd3JpdGVyfGVufDF8fHx8MTc3MDI4NTE1M3ww&ixlib=rb-4.1.0&q=80&w=1080',
      latestStory: 'Ánh Trăng Đêm',
      genre: 'Ngôn Tình',
      growth: '+245%',
      followers: '12K',
      newFollowers: '+8.2K',
      reason: 'Viral trên MXH',
      verified: true
    },
    {
      id: 2,
      name: 'Phong Vân',
      avatar: 'https://images.unsplash.com/photo-1754954865833-c6ee8cb8726d?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHlvdW5nJTIwbWFuJTIwY3JlYXRpdmV8ZW58MXx8fHwxNzcwMjg1MTU2fDA&ixlib=rb-4.1.0&q=80&w=1080',
      latestStory: 'Chiến Thần Trở Về',
      genre: 'Huyền Huyễn',
      growth: '+189%',
      followers: '9.5K',
      newFollowers: '+6.1K',
      reason: 'Giải nhất cuộc thi',
      verified: false
    },
    {
      id: 3,
      name: 'Linh Lan',
      avatar: 'https://images.unsplash.com/photo-1581065178026-390bc4e78dad?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHdvbWFuJTIwcHJvZmVzc2lvbmFsJTIwcG9ydHJhaXR8ZW58MXx8fHwxNzcwMjc3OTQ1fDA&ixlib=rb-4.1.0&q=80&w=1080',
      latestStory: 'Học Viện Ma Pháp',
      genre: 'Học Đường',
      growth: '+156%',
      followers: '7.8K',
      newFollowers: '+4.9K',
      reason: 'Hợp tác AI xuất sắc',
      verified: true
    },
  ];

  return (
    <section className="bg-white rounded-2xl border border-gray-200 p-6">
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-gradient-to-br from-[#FB2C36] to-[#E01F2E] rounded-lg flex items-center justify-center">
            <TrendingUp className="w-5 h-5 text-white" />
          </div>
          <div>
            <h2 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[24px]">
              Tác Giả Đang Trending
            </h2>
            <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[14px]">
              Tăng trưởng nhanh nhất tuần này
            </p>
          </div>
        </div>
      </div>

      <div className="space-y-4">
        {trendingAuthors.map((author, index) => (
          <div key={author.id} className="group relative p-5 border border-gray-200 rounded-xl hover:border-[#FB2C36] hover:shadow-md transition-all cursor-pointer">
            {/* Rank Badge */}
            <div className="absolute top-4 left-4 w-8 h-8 bg-gradient-to-br from-[#FFA500] to-[#FF8C00] rounded-lg flex items-center justify-center text-white font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[16px] shadow-lg">
              {index + 1}
            </div>

            <div className="flex items-center gap-4 ml-10">
              {/* Avatar */}
              <div className="relative flex-shrink-0">
                <div className="w-16 h-16 rounded-xl overflow-hidden border-2 border-[#FB2C36]/30">
                  <ImageWithFallback src={author.avatar} alt={author.name} className="w-full h-full object-cover" />
                </div>
                <div className="absolute -bottom-1 -right-1 w-6 h-6 bg-[#FB2C36] rounded-full flex items-center justify-center">
                  <Flame className="w-3 h-3 text-white" />
                </div>
              </div>

              {/* Info */}
              <div className="flex-1">
                <div className="flex items-center gap-2 mb-1">
                  <h3 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[17px] group-hover:text-[#FB2C36] transition-colors">
                    {author.name}
                  </h3>
                  {author.verified && (
                    <CheckCircle className="w-4 h-4 text-[#2B7FFF]" />
                  )}
                  <span className="px-2 py-0.5 bg-[#FB2C36]/10 text-[#FB2C36] rounded-full font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[11px] flex items-center gap-1">
                    <TrendingUp className="w-3 h-3" />
                    {author.growth}
                  </span>
                </div>
                <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[13px] mb-2">
                  <span className="text-[#1A2332] font-semibold">{author.latestStory}</span> • {author.genre}
                </p>
                <div className="flex items-center gap-4 text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[12px]">
                  <span>{author.followers} followers</span>
                  <span>•</span>
                  <span className="text-[#13EC5B] font-semibold">{author.newFollowers} tuần này</span>
                  <span>•</span>
                  <span>{author.reason}</span>
                </div>
              </div>

              {/* Follow Button */}
              <button className="px-4 py-2 bg-[#FB2C36]/10 text-[#FB2C36] rounded-lg hover:bg-[#FB2C36] hover:text-white transition-colors font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[13px] flex items-center gap-2">
                <UserPlus className="w-4 h-4" />
                Theo dõi
              </button>
            </div>
          </div>
        ))}
      </div>
    </section>
  );
}
