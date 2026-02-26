import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { ImageWithFallback } from '../figma/ImageWithFallback';
import { Flame, CheckCircle, Eye, Star } from 'lucide-react';
import { getStories } from '../../api/story/storyApi';
import { resolveBackendUrl } from '../../utils/resolveBackendUrl';

function formatViews(num) {
  if (num == null || num === undefined) return '0';
  const n = Number(num);
  if (n >= 1e6) return `${(n / 1e6).toFixed(1)}M`;
  if (n >= 1e3) return `${(n / 1e3).toFixed(1)}K`;
  return String(n);
}

function mapStoryToCard(item) {
  const categoryNamesStr = item.categoryNames ?? item.CategoryNames ?? '';
  const categoryNamesArr = categoryNamesStr
    ? String(categoryNamesStr).split(',').map((s) => s.trim()).filter(Boolean)
    : [];
  const genre = categoryNamesArr[0] ?? 'Chưa phân loại';
  const coverPath = item.coverImage ?? item.CoverImage;
  const imageUrl = coverPath ? resolveBackendUrl(coverPath) : '';
  const totalViews = item.totalViews ?? item.TotalViews ?? 0;
  const rating = Number(item.avgRating ?? item.AvgRating ?? 0);
  return {
    id: item.id ?? item.Id,
    story: item.title ?? item.Title ?? 'Không có tiêu đề',
    author: {
      name: item.authorName ?? item.AuthorName ?? 'Ẩn danh',
      avatar: '',
      followers: '-',
      verified: false
    },
    genre,
    chapters: item.totalChapters ?? item.TotalChapters ?? 0,
    views: formatViews(totalViews),
    rating: rating > 0 ? rating.toFixed(1) : '-',
    trending: false,
    aiAssisted: false,
    image: imageUrl
  };
}

export function TopAuthorStoriesSection() {
  const navigate = useNavigate();
  const [authorStories, setAuthorStories] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    getStories({ status: 'PUBLISHED', pageSize: 6, page: 1 })
      .then((res) => {
        const items = res?.items ?? res?.Items ?? [];
        if (!cancelled) setAuthorStories(items.map(mapStoryToCard));
      })
      .catch((err) => {
        if (!cancelled) {
          setError(err?.message ?? 'Không tải được danh sách truyện');
          setAuthorStories([]);
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => { cancelled = true; };
  }, []);

  return (
    <section className="bg-white rounded-2xl border border-gray-200 p-6">
      <div className="flex items-center justify-between mb-6">
        <div className="flex items-center gap-3">
          <div className="w-10 h-10 bg-gradient-to-br from-[#FB2C36] to-[#E01F2E] rounded-lg flex items-center justify-center">
            <Flame className="w-5 h-5 text-white" />
          </div>
          <div>
            <h2 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[24px]">
              Truyện Từ Tác Giả Hàng Đầu
            </h2>
            <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[14px]">
              Tác phẩm mới nhất từ các tác giả được yêu thích
            </p>
          </div>
        </div>
        <button className="text-[#13EC5B] hover:text-[#11D350] font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[14px] flex items-center gap-1">
          Xem tất cả
          <span>→</span>
        </button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {loading ? (
          <div className="col-span-full text-center py-12 text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] text-[14px]">
            Đang tải truyện...
          </div>
        ) : error ? (
          <div className="col-span-full text-center py-12 text-red-500 font-['Plus_Jakarta_Sans',sans-serif] text-[14px]">
            {error}
          </div>
        ) : authorStories.length === 0 ? (
          <div className="col-span-full text-center py-12 text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] text-[14px]">
            Chưa có truyện nào được xuất bản
          </div>
        ) : (
          authorStories.map((item) => (
            <div
              key={item.id}
              role="button"
              tabIndex={0}
              onClick={() => navigate(`/story/${item.id}`)}
              onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); navigate(`/story/${item.id}`); } }}
              className="group relative overflow-hidden rounded-xl border border-gray-200 hover:border-[#FB2C36] hover:shadow-xl transition-all cursor-pointer"
            >
              {/* Image */}
              <div className="relative h-48 overflow-hidden bg-gray-100">
                <ImageWithFallback src={item.image || undefined} alt={item.story} className="w-full h-full object-cover group-hover:scale-110 transition-transform duration-300" />
                <div className="absolute inset-0 bg-gradient-to-t from-black/80 via-black/40 to-transparent"></div>

                {/* Badges */}
                <div className="absolute top-3 right-3 flex flex-col gap-2">
                  {item.trending && (
                    <span className="px-2 py-1 bg-[#FB2C36] text-white rounded font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[10px] flex items-center gap-1 shadow-lg">
                      <Flame className="w-3 h-3" />
                      HOT
                    </span>
                  )}
                </div>

                {/* Genre */}
                <div className="absolute top-3 left-3">
                  <span className="px-2 py-1 bg-white/20 backdrop-blur-sm border border-white/30 text-white rounded font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[10px]">
                    {item.genre}
                  </span>
                </div>

                {/* Story Title */}
                <div className="absolute bottom-0 inset-x-0 p-4">
                  <h3 className="text-white font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[16px] mb-2 line-clamp-1 group-hover:text-[#13EC5B] transition-colors">
                    {item.story}
                  </h3>

                  {/* Author Info */}
                  <div className="flex items-center gap-2 mb-2">
                    <div className="w-6 h-6 rounded-full overflow-hidden border border-white/50 bg-white/20 flex items-center justify-center">
                      {item.author.avatar ? (
                        <ImageWithFallback src={item.author.avatar} alt={item.author.name} className="w-full h-full object-cover" />
                      ) : (
                        <span className="text-white font-bold text-[10px]">
                          {item.author.name.charAt(0).toUpperCase()}
                        </span>
                      )}
                    </div>
                    <span className="text-gray-200 font-['Plus_Jakarta_Sans',sans-serif] font-medium text-[12px]">
                      {item.author.name}
                    </span>
                    {item.author.verified && (
                      <CheckCircle className="w-3 h-3 text-[#2B7FFF]" />
                    )}
                    <span className="text-gray-300 font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[11px]">
                      • {item.author.followers}
                    </span>
                  </div>

                  {/* Stats */}
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-3">
                      <div className="flex items-center gap-1">
                        <Eye className="w-3 h-3 text-gray-300" />
                        <span className="text-gray-300 font-['Plus_Jakarta_Sans',sans-serif] font-medium text-[11px]">
                          {item.views}
                        </span>
                      </div>
                      <div className="flex items-center gap-1">
                        <Star className="w-3 h-3 text-[#FFA500] fill-[#FFA500]" />
                        <span className="text-white font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[11px]">
                          {item.rating}
                        </span>
                      </div>
                    </div>
                    <span className="text-[#13EC5B] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[11px]">
                      {item.chapters} chương
                    </span>
                  </div>
                </div>
              </div>
            </div>
          ))
        )}
      </div>
    </section>
  );
}
