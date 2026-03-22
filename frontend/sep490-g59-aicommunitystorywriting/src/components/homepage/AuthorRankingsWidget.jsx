import { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { ImageWithFallback } from '../figma/ImageWithFallback';
import { Trophy } from 'lucide-react';
import { getStories } from '../../api/story/storyApi';
import { getProfileByUserId } from '../../api/account/accountApi';
import { isAuthorGuid } from '../../api/author/authorApi';
import { useAuth } from '../../contexts/AuthContext';
import { resolveAuthorAvatarUrl, resolveAuthorDisplayName } from '../../utils/storyAuthorAvatar';

function formatCompactPoints(num) {
  if (num === null || num === undefined || Number.isNaN(Number(num))) return '0';
  const n = Number(num);
  if (n >= 1e6) return `${(n / 1e6).toFixed(1)}M`;
  if (n >= 1e3) return `${(n / 1e3).toFixed(1)}K`;
  return n.toLocaleString('vi-VN');
}

export function AuthorRankingsWidget() {
  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();
  const [rankings, setRankings] = useState([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      try {
        const res = await getStories({
          status: 'PUBLISHED',
          page: 1,
          pageSize: 40,
          sortBy: 'total_views',
          sortOrder: 'desc',
        });
        const items = Array.isArray(res?.items) ? res.items : Array.isArray(res?.Items) ? res.Items : [];
        if (items.length === 0) {
          if (!cancelled) setRankings([]);
          return;
        }

        const authorAgg = new Map();
        for (const s of items) {
          const authorId = s?.authorId ?? s?.AuthorId;
          if (!authorId) continue;
          const views = Number(s?.totalViews ?? s?.TotalViews ?? 0) || 0;
          const nameFromStory = String(s?.authorName ?? s?.AuthorName ?? '').trim() || null;
          const prev = authorAgg.get(authorId);
          if (!prev) {
            authorAgg.set(authorId, {
              authorId,
              points: views,
              sampleStory: s,
              authorName: nameFromStory,
            });
          } else {
            authorAgg.set(authorId, {
              ...prev,
              points: (prev.points ?? 0) + views,
              authorName: prev.authorName || nameFromStory,
              sampleStory: prev.sampleStory ?? s,
            });
          }
        }

        const list = Array.from(authorAgg.values()).sort((a, b) => (b.points ?? 0) - (a.points ?? 0));
        const top = list.slice(0, 5);

        const profiles =
          isAuthenticated && top.length > 0
            ? await Promise.all(top.map((a) => getProfileByUserId(a.authorId).catch(() => null)))
            : top.map(() => null);

        const mapped = top.map((a, idx) => {
          const profile = profiles[idx];
          const displayName = resolveAuthorDisplayName(a.sampleStory || {}, profile);
          const avatar = resolveAuthorAvatarUrl(a.sampleStory || {}, profile, displayName);
          return {
            rank: idx + 1,
            authorId: a.authorId,
            name: displayName,
            pointsLabel: `${formatCompactPoints(a.points)} điểm`,
            avatar,
          };
        });

        if (!cancelled) setRankings(mapped);
      } catch {
        if (!cancelled) setRankings([]);
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, [isAuthenticated]);

  const goAuthor = (authorId) => {
    if (authorId && isAuthorGuid(authorId)) navigate(`/authors/${authorId}`);
  };

  return (
    <div className="bg-white rounded-2xl border border-gray-200 p-5">
      <div className="flex items-center gap-2 mb-4">
        <Trophy className="w-5 h-5 text-[#FFA500]" />
        <h3 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[18px]">
          Bảng Xếp Hạng Tác Giả
        </h3>
      </div>
      <p className="text-[11px] text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] mb-3 -mt-1">
        Theo tổng lượt xem truyện đã xuất bản
      </p>
      <div className="space-y-3">
        {loading ? (
          <div className="text-center py-6 text-[#90A1B9] text-sm">Đang tải...</div>
        ) : rankings.length === 0 ? (
          <div className="text-center py-6 text-[#90A1B9] text-sm">Chưa có dữ liệu</div>
        ) : (
          rankings.map((author) => (
            <button
              key={author.authorId}
              type="button"
              onClick={() => goAuthor(author.authorId)}
              className="w-full flex items-center gap-3 p-3 rounded-lg hover:bg-gray-50 transition-colors text-left"
            >
              <div
                className={`flex items-center justify-center w-7 h-7 rounded-full font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[13px] flex-shrink-0 ${
                  author.rank <= 3 ? 'bg-gradient-to-br from-[#FFA500] to-[#FF8C00] text-white' : 'bg-gray-100 text-[#90A1B9]'
                }`}
              >
                {author.rank}
              </div>
              <div className="w-10 h-10 rounded-full overflow-hidden flex-shrink-0 border border-gray-100">
                <ImageWithFallback src={author.avatar} alt={author.name} className="w-full h-full object-cover" />
              </div>
              <div className="flex-1 min-w-0">
                <p className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[13px] truncate">
                  {author.name}
                </p>
                <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[11px]">
                  {author.pointsLabel}
                </p>
              </div>
            </button>
          ))
        )}
      </div>
      <button
        type="button"
        onClick={() => navigate('/story-list')}
        className="w-full mt-4 py-2 text-[#13EC5B] hover:text-[#11D350] font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[13px]"
      >
        Khám phá truyện →
      </button>
    </div>
  );
}
