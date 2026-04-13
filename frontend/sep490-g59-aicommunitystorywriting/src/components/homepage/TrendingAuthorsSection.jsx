import { ImageWithFallback } from '../figma/ImageWithFallback';
import React, { useEffect, useState } from 'react';
import { TrendingUp, Flame, CheckCircle, UserPlus, UserMinus } from 'lucide-react';
import { getStories } from '../../api/story/storyApi';
import { getProfileByUserId } from '../../api/account/accountApi';
import {
  getAuthorFollowing,
  getAuthorFollowersCount,
  getAuthorNewFollowersThisWeek,
  followAuthor,
  unfollowAuthor,
} from '../../api/author/authorApi';
import { resolveAuthorAvatarUrl } from '../../utils/storyAuthorAvatar';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';

export function TrendingAuthorsSection() {
  const [trendingAuthors, setTrendingAuthors] = useState([
    {
      id: 1,
      name: 'Nguyệt Hạ',
      avatar: 'https://images.unsplash.com/photo-1611199340099-91a595a86812?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHdvbWFuJTIwYXV0aG9yJTIwd3JpdGVyfGVufDF8fHx8MTc3MDI4NTE1M3ww&ixlib=rb-4.1.0&q=80&w=1080',
      latestStory: 'Ánh Trăng Đêm',
      genre: 'Ngôn Tình',
      growth: '+245%',
      followers: '12K',
      followersNum: 12000,
      newFollowers: '+8.2K',
      newFollowersNum: 8200,
      reason: 'Viral trên MXH',
      verified: true,
      isFollowing: false
    },
    {
      id: 2,
      name: 'Phong Vân',
      avatar: 'https://images.unsplash.com/photo-1754954865833-c6ee8cb8726d?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHlvdW5nJTIwbWFuJTIwY3JlYXRpdmV8ZW58MXx8fHwxNzcwMjg1MTU2fDA&ixlib=rb-4.1.0&q=80&w=1080',
      latestStory: 'Chiến Thần Trở Về',
      genre: 'Huyền Huyễn',
      growth: '+189%',
      followers: '9.5K',
      followersNum: 9500,
      newFollowers: '+6.1K',
      newFollowersNum: 6100,
      reason: 'Giải nhất cuộc thi',
      verified: false,
      isFollowing: false
    },
    {
      id: 3,
      name: 'Linh Lan',
      avatar: 'https://images.unsplash.com/photo-1581065178026-390bc4e78dad?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxhc2lhbiUyMHdvbWFuJTIwcHJvZmVzc2lvbmFsJTIwcG9ydHJhaXR8ZW58MXx8fHwxNzcwMjc3OTQ1fDA&ixlib=rb-4.1.0&q=80&w=1080',
      latestStory: 'Học Viện Ma Pháp',
      genre: 'Học Đường',
      growth: '+156%',
      followers: '7.8K',
      followersNum: 7800,
      newFollowers: '+4.9K',
      newFollowersNum: 4900,
      reason: 'Hợp tác AI xuất sắc',
      verified: true,
      isFollowing: false
    },
  ]);

  const navigate = useNavigate();
  const { isAuthenticated } = useAuth();

  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(null);
  const [followLoadingByAuthorId, setFollowLoadingByAuthorId] = useState({});

  const parseCompactNumber = (input) => {
    if (input === null || input === undefined) return 0;
    const raw = String(input).trim().replace(/,/g, '');
    if (!raw) return 0;
    const sign = raw.startsWith('-') ? -1 : 1;
    const s = raw.replace(/^[-+]/, '');
    if (s.endsWith('M')) return sign * parseFloat(s.slice(0, -1)) * 1e6;
    if (s.endsWith('K')) return sign * parseFloat(s.slice(0, -1)) * 1e3;
    return sign * parseFloat(s) || 0;
  };

  const formatCompactNumber = (num) => {
    if (num === null || num === undefined || Number.isNaN(Number(num))) return '0';
    const n = Number(num);
    if (n >= 1e6) return `${(n / 1e6).toFixed(1)}M`;
    if (n >= 1e3) return `${(n / 1e3).toFixed(1)}K`;
    return String(Math.round(n));
  };

  useEffect(() => {
    let cancelled = false;

    async function loadFromApi() {
      setLoading(true);
      setLoadError(null);
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
          if (!cancelled) setTrendingAuthors([]);
          return;
        }

        // Group by authorId using totalViews to approximate "trending"
        const authorAgg = new Map(); // authorId => { authorId, views, latestStory, genre }
        for (const s of items) {
          const authorId = s?.authorId ?? s?.AuthorId;
          if (!authorId) continue;

          const views = s?.totalViews ?? s?.TotalViews ?? 0;
          const categoryNamesStr = s?.categoryNames ?? s?.CategoryNames ?? '';
          const categoryNamesArr = categoryNamesStr
            ? String(categoryNamesStr).split(',').map((x) => x.trim()).filter(Boolean)
            : [];
          const genre = categoryNamesArr[0] ?? 'Chưa phân loại';
          const storyTitle = s?.title ?? s?.Title ?? '';

          const prev = authorAgg.get(authorId);
          const nameFromStory = String(s?.authorName ?? s?.AuthorName ?? '').trim() || null;
          if (!prev) {
            authorAgg.set(authorId, {
              authorId,
              views: Number(views) || 0,
              latestStory: storyTitle,
              genre,
              authorName: nameFromStory,
              sampleStory: s,
            });
          } else {
            authorAgg.set(authorId, {
              ...prev,
              views: (prev.views ?? 0) + (Number(views) || 0),
              genre: prev.genre || genre,
              authorName: prev.authorName || nameFromStory,
              sampleStory: prev.sampleStory ?? s,
            });
          }
        }

        const authorList = Array.from(authorAgg.values()).sort((a, b) => (b.views ?? 0) - (a.views ?? 0));
        const topForMetrics = authorList.slice(0, Math.min(20, authorList.length));

        const followerCounts = await Promise.all(
          topForMetrics.map((a) => getAuthorFollowersCount(a.authorId).catch(() => 0))
        );
        const weeklyCounts = await Promise.all(
          topForMetrics.map((a) => getAuthorNewFollowersThisWeek(a.authorId).catch(() => 0))
        );

        const enriched = topForMetrics.map((a, idx) => ({
          ...a,
          followersNum: Math.max(0, Number(followerCounts[idx]) || 0),
          newFollowersThisWeek: Math.max(0, Number(weeklyCounts[idx]) || 0),
        }));

        enriched.sort((a, b) => {
          const dw = (b.newFollowersThisWeek ?? 0) - (a.newFollowersThisWeek ?? 0);
          if (dw !== 0) return dw;
          return (b.views ?? 0) - (a.views ?? 0);
        });

        const topAuthors = enriched.slice(0, 3);

        const profiles =
          isAuthenticated && topAuthors.length > 0
            ? await Promise.all(topAuthors.map((a) => getProfileByUserId(a.authorId).catch(() => null)))
            : topAuthors.map(() => null);
        const profileMap = {};
        topAuthors.forEach((a, idx) => {
          profileMap[a.authorId] = profiles[idx];
        });

        const maxWeekly = Math.max(1, ...topAuthors.map((a) => a.newFollowersThisWeek ?? 0));

        const mapped = topAuthors.map((a) => {
          const profile = profileMap[a.authorId];
          const displayName =
            profile?.displayName?.trim() || a.authorName || 'Tác giả';
          const followersNum = a.followersNum;
          const followers = formatCompactNumber(followersNum);
          const verified = Boolean(profile?.isVerified);

          const w = a.newFollowersThisWeek ?? 0;
          const growthPct = Math.max(0, Math.round((w / maxWeekly) * 100));
          const newFollowersNum = w;
          const newFollowers = `+${formatCompactNumber(newFollowersNum)}`;

          return {
            id: a.authorId,
            name: displayName,
            avatar: resolveAuthorAvatarUrl(a.sampleStory || {}, profile, displayName),
            latestStory: a.latestStory,
            genre: a.genre,
            growth: `+${growthPct}%`,
            followers,
            followersNum,
            newFollowers,
            newFollowersNum,
            reason: verified ? 'Uy tín trong cộng đồng' : 'Đang nổi bật theo lượt xem',
            verified,
            isFollowing: false,
          };
        });

        if (!cancelled) setTrendingAuthors(mapped);
      } catch (e) {
        if (!cancelled) setLoadError(e?.message ?? 'Không tải được trending authors');
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    loadFromApi();
    return () => {
      cancelled = true;
    };
  }, [isAuthenticated]);

  useEffect(() => {
    if (loadError) console.error('TrendingAuthorsSection load error:', loadError);
  }, [loadError]);

  const authorIdsKey = trendingAuthors
    .map((a) => a?.id)
    .filter(Boolean)
    .sort()
    .join(',');

  useEffect(() => {
    if (!isAuthenticated) return;
    if (!authorIdsKey) return;

    const authorIds = Array.from(new Set(trendingAuthors.map((a) => a?.id).filter(Boolean)));
    if (authorIds.length === 0) return;

    let cancelled = false;

    (async () => {
      try {
        const followingResults = await Promise.all(
          authorIds.map((id) =>
            getAuthorFollowing(id)
              .then((data) => !!(data?.following ?? data?.Following))
              .catch(() => false)
          )
        );

        if (cancelled) return;
        const followingMap = {};
        authorIds.forEach((id, idx) => {
          followingMap[id] = followingResults[idx];
        });

        setTrendingAuthors((prev) =>
          prev.map((a) => {
            const fid = a?.id;
            if (!fid) return a;
            if (followingMap[fid] === undefined) return a;
            return { ...a, isFollowing: followingMap[fid] };
          })
        );
      } catch {
        // Best-effort
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [isAuthenticated, authorIdsKey]);

  const handleToggleFollow = async (authorId) => {
    if (!authorId) return;

    const target = trendingAuthors.find((a) => a?.id === authorId);
    if (!target) return;

    if (!isAuthenticated) {
      navigate('/login');
      return;
    }

    if (followLoadingByAuthorId[authorId]) return;

    const nextFollowing = !target?.isFollowing;
    setFollowLoadingByAuthorId((prev) => ({ ...prev, [authorId]: true }));

    try {
      if (nextFollowing) await followAuthor(authorId);
      else await unfollowAuthor(authorId);

      const [followersNum, newFollowersNum] = await Promise.all([
        getAuthorFollowersCount(authorId),
        getAuthorNewFollowersThisWeek(authorId),
      ]);

      setTrendingAuthors((prev) => {
        const updated = prev.map((a) => {
          if (a?.id !== authorId) return a;
          return {
            ...a,
            isFollowing: nextFollowing,
            followersNum,
            followers: formatCompactNumber(followersNum),
            newFollowersNum,
            newFollowers: `+${formatCompactNumber(newFollowersNum)}`,
          };
        });
        const maxW = Math.max(1, ...updated.map((x) => x.newFollowersNum ?? 0));
        return updated.map((x) => ({
          ...x,
          growth: `+${Math.max(0, Math.round(((x.newFollowersNum ?? 0) / maxW) * 100))}%`,
        }));
      });
    } catch (err) {
      console.error('TrendingAuthorsSection follow toggle failed:', err);
    } finally {
      setFollowLoadingByAuthorId((prev) => ({ ...prev, [authorId]: false }));
    }
  };

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
              Theo follower mới từ Thứ Hai tuần này (giờ máy chủ)
            </p>
          </div>
        </div>
      </div>

      <div className="space-y-4">
        {loading ? (
          <div className="text-center py-10 text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] text-[14px]">
            Đang tải trending...
          </div>
        ) : loadError ? (
          <div className="text-center py-10 text-red-500 font-['Plus_Jakarta_Sans',sans-serif] text-[14px]">
            {loadError}
          </div>
        ) : trendingAuthors.length === 0 ? (
          <div className="text-center py-10 text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] text-[14px]">
            Không có dữ liệu
          </div>
        ) : (
          trendingAuthors.map((author, index) => (
          <div key={author.id} className="group relative p-5 border border-gray-200 rounded-xl hover:border-[#FB2C36] hover:shadow-md transition-all">
            {/* Rank Badge */}
            <div className="absolute top-4 left-4 w-8 h-8 bg-gradient-to-br from-[#FFA500] to-[#FF8C00] rounded-lg flex items-center justify-center text-white font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[16px] shadow-lg">
              {index + 1}
            </div>

            <div className="flex items-center gap-4 ml-10">
              {/* Avatar + info — bấm vào → trang tác giả (trừ nút Theo dõi) */}
              <Link
                to={`/authors/${author.id}`}
                className="flex items-center gap-4 flex-1 min-w-0 rounded-xl -m-1 p-1 hover:bg-gray-50/80 transition-colors cursor-pointer text-left"
              >
                <div className="relative flex-shrink-0">
                  <div className="w-16 h-16 rounded-xl overflow-hidden border-2 border-[#FB2C36]/30">
                    <ImageWithFallback src={author.avatar} alt={author.name} className="w-full h-full object-cover" />
                  </div>
                  <div className="absolute -bottom-1 -right-1 w-6 h-6 bg-[#FB2C36] rounded-full flex items-center justify-center pointer-events-none">
                    <Flame className="w-3 h-3 text-white" />
                  </div>
                </div>

                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2 mb-1 flex-wrap">
                    <h3 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[17px] group-hover:text-[#FB2C36] transition-colors">
                      {author.name}
                    </h3>
                    {author.verified && (
                      <CheckCircle className="w-4 h-4 text-[#2B7FFF] flex-shrink-0" />
                    )}
                    <span className="px-2 py-0.5 bg-[#FB2C36]/10 text-[#FB2C36] rounded-full font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[11px] flex items-center gap-1">
                      <TrendingUp className="w-3 h-3" />
                      {author.growth}
                    </span>
                  </div>
                  <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[13px] mb-2">
                    <span className="text-[#1A2332] font-semibold">{author.latestStory}</span> • {author.genre}
                  </p>
                  <div className="flex items-center gap-4 text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[12px] flex-wrap">
                    <span>{author.followers} followers</span>
                    <span>•</span>
                    <span className="text-[#13EC5B] font-semibold">{author.newFollowers} tuần này</span>
                    <span>•</span>
                    <span>{author.reason}</span>
                  </div>
                </div>
              </Link>

              {/* Follow Button */}
              <button
                type="button"
                onClick={() => handleToggleFollow(author.id)}
                disabled={followLoadingByAuthorId[author.id]}
                className={`flex-shrink-0 px-4 py-2 rounded-lg transition-colors font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[13px] flex items-center gap-2 ${
                  author.isFollowing
                    ? 'bg-slate-200 text-slate-700 hover:bg-slate-300'
                    : 'bg-[#FB2C36]/10 text-[#FB2C36] hover:bg-[#FB2C36] hover:text-white'
                }`}
              >
                {followLoadingByAuthorId[author.id] ? (
                  'Đang xử lý...'
                ) : author.isFollowing ? (
                  <>
                    <UserMinus className="w-4 h-4" />
                    Bỏ theo dõi
                  </>
                ) : (
                  <>
                    <UserPlus className="w-4 h-4" />
                    Theo dõi
                  </>
                )}
              </button>
            </div>
          </div>
          ))
        )}
      </div>
    </section>
  );
}
