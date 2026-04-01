import { useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { ImageWithFallback } from '../figma/ImageWithFallback';
import { Sparkles, Brain, PenTool, ChevronRight, Eye, Star, BookOpen } from 'lucide-react';
import { resolveBackendUrl } from '../../utils/resolveBackendUrl';
import { formatStoryViews } from '../../utils/storyBrowseMap';
import { getStories } from '../../api/story/storyApi';
import { getChaptersByStoryId } from '../../api/chapter/chapterApi';

export function AIAssistedStoriesWidget() {
  const TAKE = 3;
  const CANDIDATE_PAGE_SIZE = 12;

  const [aiStories, setAiStories] = useState([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState('');

  const skeletonCards = useMemo(() => Array.from({ length: TAKE }), []);

  useEffect(() => {
    let cancelled = false;

    const run = async () => {
      setLoading(true);
      setError('');

      try {
        // Lấy danh sách truyện PUBLISHED và sắp theo views.
        const res = await getStories({
          status: 'PUBLISHED',
          page: 1,
          pageSize: CANDIDATE_PAGE_SIZE,
          sortBy: 'total_views',
          sortOrder: 'desc',
        });

        const items = Array.isArray(res?.items) ? res.items : Array.isArray(res?.Items) ? res.Items : [];

        // Lọc: truyện có ít nhất 1 chapter có AI contribution/similarity > 0.
        const checked = await Promise.all(
          items.map(async (s) => {
            const storyId = s?.id ?? s?.Id;
            if (!storyId) return null;

            let chapters = [];
            try {
              const chaptersRes = await getChaptersByStoryId(storyId);
              chapters = Array.isArray(chaptersRes)
                ? chaptersRes
                : Array.isArray(chaptersRes?.items)
                  ? chaptersRes.items
                  : Array.isArray(chaptersRes?.Items)
                    ? chaptersRes.Items
                    : [];
            } catch {
              chapters = [];
            }

            let maxAiContribution = 0;
            let maxAiSimilarity = 0;
            for (const c of chapters) {
              const contribRaw = c?.AiContributionRatio ?? c?.aiContributionRatio ?? 0;
              const simRaw = c?.AiSimilarityPercent ?? c?.aiSimilarityPercent ?? 0;
              const contrib = Number(contribRaw);
              const sim = Number(simRaw);
              if (Number.isFinite(contrib)) maxAiContribution = Math.max(maxAiContribution, contrib);
              if (Number.isFinite(sim)) maxAiSimilarity = Math.max(maxAiSimilarity, sim);
            }

            const hasAi = maxAiContribution > 0 || maxAiSimilarity > 0;
            if (!hasAi) return null;

            return {
              story: s,
              maxAiContribution,
              maxAiSimilarity,
            };
          })
        );

        const qualifiedRecords = checked
          .filter(Boolean)
          .sort((a, b) => {
            const av = Number(a?.story?.totalViews ?? a?.story?.TotalViews ?? 0) || 0;
            const bv = Number(b?.story?.totalViews ?? b?.story?.TotalViews ?? 0) || 0;
            return bv - av;
          });

        const top = qualifiedRecords.slice(0, TAKE);

        const mapped = top.map((rec) => {
          const raw = rec?.story ?? {};
          const id = raw?.id ?? raw?.Id;
          const story = raw?.title ?? raw?.Title ?? '';
          const authorName = raw?.authorName ?? raw?.AuthorName ?? 'Tác giả';
          const authorAvatarUrl = raw?.authorAvatarUrl ?? raw?.AuthorAvatarUrl ?? raw?.author_avatar_url ?? null;
          const coverPath = raw?.coverImage ?? raw?.CoverImage ?? raw?.cover_image ?? null;

          const categoriesStr = raw?.categoryNames ?? raw?.CategoryNames ?? '';
          const categories = categoriesStr
            ? String(categoriesStr)
                .split(',')
                .map((x) => x.trim())
                .filter(Boolean)
            : [];
          const genre = categories[0] ?? 'Truyện';

          const views = formatStoryViews(raw?.totalViews ?? raw?.TotalViews ?? 0);

          const ratingNum = raw?.avgRating ?? raw?.AvgRating;
          const rating =
            ratingNum != null && Number.isFinite(Number(ratingNum)) ? Number(ratingNum).toFixed(1) : '—';

          const chaptersNum =
            Number(raw?.publishedChaptersCount ?? raw?.PublishedChaptersCount ?? raw?.totalChapters ?? raw?.TotalChapters ?? 0) || 0;

          const cover = coverPath ? resolveBackendUrl(String(coverPath)) : '';
          const avatar = authorAvatarUrl ? resolveBackendUrl(String(authorAvatarUrl)) : '';

          const aiFeatures = [
            rec?.maxAiContribution > 0 ? 'Đồng sáng tác' : 'Hỗ trợ AI',
            rec?.maxAiSimilarity > 0 ? 'Kiểm tra logic & độ tương đồng' : 'Gợi ý nội dung',
          ];

          return {
            id: String(id),
            story,
            author: { name: authorName, avatar },
            genre,
            chapters: chaptersNum,
            views,
            rating,
            aiFeatures,
            image: cover,
          };
        });

        if (!cancelled) setAiStories(mapped);
      } catch (e) {
        if (!cancelled) setError(e?.message ?? 'Không tải được danh sách truyện AI.');
      } finally {
        if (!cancelled) setLoading(false);
      }
    };

    run();

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <section
      id="ai-assisted-stories"
      className="relative bg-gradient-to-br from-[#13EC5B]/10 via-white to-[#2B7FFF]/5 rounded-2xl border-2 border-[#13EC5B]/30 p-8 overflow-hidden scroll-mt-24"
    >
      {/* Decorative Elements */}
      <div className="absolute top-0 right-0 w-64 h-64 bg-[#13EC5B]/5 rounded-full blur-3xl"></div>
      <div className="absolute bottom-0 left-0 w-48 h-48 bg-[#2B7FFF]/5 rounded-full blur-3xl"></div>
      
      <div className="relative">
        <div className="flex items-center justify-between mb-6">
          <div className="flex items-center gap-3">
            <div className="relative">
              <div className="absolute inset-0 bg-gradient-to-br from-[#13EC5B] to-[#11D350] rounded-xl blur-md opacity-60 animate-pulse"></div>
              <div className="relative w-12 h-12 bg-gradient-to-br from-[#13EC5B] to-[#11D350] rounded-xl flex items-center justify-center shadow-lg">
                <Sparkles className="w-6 h-6 text-white" />
              </div>
            </div>
            <div>
              <h3 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[24px] flex items-center gap-2">
                Truyện Sử Dụng AI Đồng Sáng Tác
                <span className="px-3 py-1 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-full font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[11px] shadow-lg">
                  HOT
                </span>
              </h3>
              <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-medium text-[14px]">
                AI trở thành đồng tác giả, cùng sáng tạo nội dung • <span className="text-[#13EC5B] font-bold">2,450+ tác giả</span> đang sử dụng
              </p>
            </div>
          </div>
          <button className="px-5 py-2.5 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-xl hover:shadow-xl transition-all font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[14px] flex items-center gap-2">
            Xem tất cả
            <ChevronRight className="w-4 h-4" />
          </button>
        </div>

        {error ? (
          <div className="text-sm text-red-700 font-semibold mb-4">{error}</div>
        ) : null}

        <div className="grid grid-cols-3 gap-5">
          {loading
            ? skeletonCards.map((_, i) => (
                <div
                  key={`ai-sk-${i}`}
                  className="bg-white rounded-2xl border border-gray-200 overflow-hidden animate-pulse"
                >
                  <div className="w-full h-56 bg-gray-100" />
                  <div className="p-4 space-y-3">
                    <div className="h-5 bg-gray-100 rounded w-3/4" />
                    <div className="h-4 bg-gray-100 rounded w-1/2" />
                    <div className="h-4 bg-gray-100 rounded w-full" />
                    <div className="h-4 bg-gray-100 rounded w-full" />
                  </div>
                </div>
              ))
            : aiStories.map((item) => (
                <Link
                  key={item.id}
                  to={`/story/${item.id}`}
                  style={{ textDecoration: 'none', color: 'inherit' }}
                >
                  <div className="group cursor-pointer bg-white rounded-2xl border border-gray-200 hover:border-[#13EC5B] hover:shadow-2xl transition-all duration-300 overflow-hidden">
              <div className="relative">
                <ImageWithFallback 
                  src={item.image} 
                  alt={item.story} 
                  className="w-full h-56 object-cover group-hover:scale-110 transition-transform duration-500" 
                />
                {/* AI Badge with Animation */}
                <div className="absolute top-3 right-3">
                  <div className="relative">
                    <div className="absolute inset-0 bg-[#13EC5B] rounded-lg blur-md opacity-60 animate-pulse"></div>
                    <div className="relative px-3 py-1.5 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-lg font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[11px] flex items-center gap-1.5 shadow-lg">
                      <Sparkles className="w-3.5 h-3.5" />
                      AI Hỗ trợ
                    </div>
                  </div>
                </div>
                {/* Genre */}
                <div className="absolute top-3 left-3">
                  <span className="px-3 py-1.5 bg-black/50 backdrop-blur-md text-white rounded-lg font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[11px]">
                    {item.genre}
                  </span>
                </div>
                {/* Overlay Gradient */}
                <div className="absolute inset-0 bg-gradient-to-t from-black/60 via-transparent to-transparent opacity-0 group-hover:opacity-100 transition-opacity duration-300"></div>
              </div>
              
              <div className="p-4">
                <h4 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[16px] mb-2 line-clamp-1 group-hover:text-[#13EC5B] transition-colors">
                  {item.story}
                </h4>
                
                <div className="flex items-center gap-2 mb-3">
                  <div className="w-6 h-6 rounded-full overflow-hidden border-2 border-[#13EC5B]/30">
                    <ImageWithFallback src={item.author.avatar} alt={item.author.name} className="w-full h-full object-cover" />
                  </div>
                  <span className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-medium text-[12px] line-clamp-1">
                    {item.author.name}
                  </span>
                </div>
                
                {/* AI Features */}
                <div className="flex items-center gap-2 mb-3 flex-wrap">
                  {item.aiFeatures.map((feature, idx) => (
                    <span key={idx} className="px-2 py-1 bg-[#13EC5B]/10 text-[#13EC5B] rounded-md font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[10px]">
                      {feature}
                    </span>
                  ))}
                </div>
                
                <div className="flex items-center justify-between pt-3 border-t border-gray-100">
                  <div className="flex items-center gap-1">
                    <Eye className="w-4 h-4 text-[#90A1B9]" />
                    <span className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[12px]">
                      {item.views}
                    </span>
                  </div>
                  <div className="flex items-center gap-1">
                    <Star className="w-4 h-4 text-[#FFA500] fill-[#FFA500]" />
                    <span className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[12px]">
                      {item.rating}
                    </span>
                  </div>
                  <div className="flex items-center gap-1">
                    <BookOpen className="w-4 h-4 text-[#90A1B9]" />
                    <span className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[12px]">
                      {item.chapters}
                    </span>
                  </div>
                </div>
              </div>
                  </div>
                </Link>
              ))}
        </div>

        {/* Bottom CTA */}
        <div className="mt-6 p-5 bg-gradient-to-r from-[#13EC5B]/10 to-[#2B7FFF]/10 rounded-xl border border-[#13EC5B]/20">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-4">
              <div className="flex items-center -space-x-3">
                <div className="w-10 h-10 rounded-full bg-[#13EC5B] flex items-center justify-center border-2 border-white shadow-lg">
                  <Brain className="w-5 h-5 text-white" />
                </div>
                <div className="w-10 h-10 rounded-full bg-[#2B7FFF] flex items-center justify-center border-2 border-white shadow-lg">
                  <PenTool className="w-5 h-5 text-white" />
                </div>
                <div className="w-10 h-10 rounded-full bg-[#FB2C36] flex items-center justify-center border-2 border-white shadow-lg">
                  <Sparkles className="w-5 h-5 text-white" />
                </div>
              </div>
              <div>
                <p className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[15px]">
                  Bắt đầu viết với AI ngay hôm nay
                </p>
                <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[13px]">
                  Miễn phí cho tác giả mới • Không cần thẻ tín dụng
                </p>
              </div>
            </div>
            <button className="px-6 py-3 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-xl hover:shadow-xl hover:scale-105 transition-all font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[14px] flex items-center gap-2">
              <Sparkles className="w-4 h-4" />
              Thử ngay
            </button>
          </div>
        </div>
      </div>
    </section>
  );
}
