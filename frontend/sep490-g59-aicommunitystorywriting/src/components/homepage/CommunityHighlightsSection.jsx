import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { ImageWithFallback } from '../figma/ImageWithFallback';
import { Sparkles, BookOpen } from 'lucide-react';
import { getStories } from '../../api/story/storyApi';
import { getChapters } from '../../api/chapter/chapterApi';
import { resolveAuthorAvatarUrl, resolveAuthorDisplayName } from '../../utils/storyAuthorAvatar';

function formatRelativeTime(dateValue) {
  try {
    const d = new Date(dateValue);
    if (Number.isNaN(d.getTime())) return '—';
    const diffMs = Date.now() - d.getTime();
    if (diffMs < 0) return 'Vừa xong';
    const diffMin = Math.floor(diffMs / (60 * 1000));
    if (diffMin < 1) return 'Vừa xong';
    if (diffMin < 60) return `${diffMin} phút trước`;
    const diffHours = Math.floor(diffMin / 60);
    if (diffHours < 48) return `${diffHours} giờ trước`;
    const diffDays = Math.floor(diffHours / 24);
    if (diffDays < 30) return `${diffDays} ngày trước`;
    const diffMonths = Math.floor(diffDays / 30);
    return `${Math.max(1, diffMonths)} tháng trước`;
  } catch {
    return '—';
  }
}

function storyMetaFromItem(s) {
  const id = s?.id ?? s?.Id;
  if (!id) return null;
  const title = s?.title ?? s?.Title ?? 'Không tên';
  const authorId = s?.authorId ?? s?.AuthorId ?? null;
  const authorName = resolveAuthorDisplayName(s, null);
  const avatar = resolveAuthorAvatarUrl(s, null, authorName);
  return { id: String(id), title, authorId, authorName, avatar, raw: s };
}

/** Tách chuỗi để in đậm các đoạn «…» (tên truyện/chương). */
function ContentWithQuotes({ text, accentColor }) {
  const parts = String(text).split(/(«[^»]*»)/g);
  return (
    <span className="font-sans antialiased leading-relaxed">
      {parts.map((part, i) => {
        if (part.startsWith('«') && part.endsWith('»')) {
          return (
            <span
              key={i}
              className="font-sans font-extrabold antialiased drop-shadow-[0_1px_0_rgba(255,255,255,0.8)]"
              style={{ color: accentColor }}
            >
              {part}
            </span>
          );
        }
        return (
          <span key={i} className="font-sans font-medium antialiased text-[#334155]">
            {part}
          </span>
        );
      })}
    </span>
  );
}

const TYPE_THEME = {
  new_story: {
    label: 'Truyện mới',
    border: 'border-l-[4px] border-l-[#13EC5B]',
    bg: 'bg-gradient-to-r from-[#13EC5B]/[0.12] via-white to-white',
    iconBg: 'bg-[#13EC5B]/20 shadow-inner',
    badge: 'bg-[#13EC5B]/15 text-[#0d9e43] border-[#13EC5B]/35',
    ring: 'group-hover:ring-[#13EC5B]/40',
    shadow: 'shadow-[0_2px_12px_rgba(19,236,91,0.08)] hover:shadow-[0_8px_28px_rgba(19,236,91,0.14)]',
  },
  new_chapter: {
    label: 'Chương mới',
    border: 'border-l-[4px] border-l-[#2B7FFF]',
    bg: 'bg-gradient-to-r from-[#2B7FFF]/[0.11] via-white to-white',
    iconBg: 'bg-[#2B7FFF]/20 shadow-inner',
    badge: 'bg-[#2B7FFF]/12 text-[#1a5fcc] border-[#2B7FFF]/35',
    ring: 'group-hover:ring-[#2B7FFF]/40',
    shadow: 'shadow-[0_2px_12px_rgba(43,127,255,0.08)] hover:shadow-[0_8px_28px_rgba(43,127,255,0.15)]',
  },
  ai_chapter: {
    label: 'AI',
    border: 'border-l-[4px] border-l-[#9D4EDD]',
    bg: 'bg-gradient-to-r from-[#9D4EDD]/[0.10] via-[#13EC5B]/[0.06] to-white',
    iconBg: 'bg-gradient-to-br from-[#9D4EDD]/25 to-[#13EC5B]/20 shadow-inner',
    badge: 'bg-gradient-to-r from-[#9D4EDD]/15 to-[#13EC5B]/12 text-[#6b21a8] border-[#9D4EDD]/30',
    ring: 'group-hover:ring-[#9D4EDD]/35',
    shadow: 'shadow-[0_2px_14px_rgba(157,78,221,0.10)] hover:shadow-[0_10px_32px_rgba(157,78,221,0.16)]',
  },
};

/**
 * Hoạt động cộng đồng: truyện mới, chương mới, chương có đối chiếu AI (dữ liệu từ API công khai, guest xem được).
 */
export function CommunityHighlightsSection() {
  const [activities, setActivities] = useState([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState(null);

  useEffect(() => {
    let cancelled = false;

    async function load() {
      setLoading(true);
      setLoadError(null);
      try {
        const [storiesRes, chaptersRes] = await Promise.all([
          getStories({
            status: 'PUBLISHED',
            page: 1,
            pageSize: 45,
            sortBy: 'created_at',
            sortOrder: 'desc',
          }),
          getChapters({
            status: 'PUBLISHED',
            page: 1,
            pageSize: 40,
            sortBy: 'published_at',
            sortOrder: 'desc',
          }),
        ]);

        const storyItems = Array.isArray(storiesRes?.items)
          ? storiesRes.items
          : Array.isArray(storiesRes?.Items)
            ? storiesRes.Items
            : [];
        const chapterItems = Array.isArray(chaptersRes?.items)
          ? chaptersRes.items
          : Array.isArray(chaptersRes?.Items)
            ? chaptersRes.Items
            : [];

        const storyMap = new Map();
        for (const s of storyItems) {
          const meta = storyMetaFromItem(s);
          if (meta) storyMap.set(meta.id, meta);
        }

        const missingStoryIds = [];
        for (const ch of chapterItems) {
          const sid = ch?.storyId ?? ch?.StoryId;
          if (sid && !storyMap.has(String(sid))) missingStoryIds.push(String(sid));
        }
        const uniqueMissing = [...new Set(missingStoryIds)];
        if (uniqueMissing.length > 0) {
          const extraRes = await getStories({
            status: 'PUBLISHED',
            page: 1,
            pageSize: Math.min(100, Math.max(50, uniqueMissing.length)),
            includeStoryIds: uniqueMissing,
            sortBy: 'created_at',
            sortOrder: 'desc',
          });
          const extraItems = Array.isArray(extraRes?.items)
            ? extraRes.items
            : Array.isArray(extraRes?.Items)
              ? extraRes.Items
              : [];
          for (const s of extraItems) {
            const meta = storyMetaFromItem(s);
            if (meta) storyMap.set(meta.id, meta);
          }
        }

        const list = [];

        for (const s of storyItems.slice(0, 12)) {
          const meta = storyMetaFromItem(s);
          if (!meta) continue;
          const t = s?.createdAt ?? s?.CreatedAt ?? s?.updatedAt ?? s?.UpdatedAt;
          list.push({
            key: `new-story-${meta.id}`,
            type: 'new_story',
            authorName: meta.authorName,
            avatar: meta.avatar,
            content: `Đã đăng truyện mới «${meta.title}»`,
            timeLabel: formatRelativeTime(t),
            sortTime: t ? new Date(t).getTime() : 0,
            href: `/story/${meta.id}`,
            icon: BookOpen,
            color: '#13EC5B',
            themeKey: 'new_story',
          });
        }

        const seenChapter = new Set();
        for (const ch of chapterItems) {
          const chId = ch?.id ?? ch?.Id;
          if (!chId || seenChapter.has(String(chId))) continue;
          seenChapter.add(String(chId));

          const storyId = String(ch?.storyId ?? ch?.StoryId ?? '');
          const chapterTitle = ch?.title ?? ch?.Title ?? 'Chương';
          const storyTitle = ch?.storyTitle ?? ch?.StoryTitle ?? storyMap.get(storyId)?.title ?? 'Truyện';
          const meta = storyMap.get(storyId);
          const authorName = meta?.authorName ?? 'Tác giả';
          const avatar = meta?.avatar ?? resolveAuthorAvatarUrl(meta?.raw ?? {}, null, authorName);

          const aiPct = ch?.aiSimilarityPercent ?? ch?.AiSimilarityPercent;
          const hasAi = aiPct != null && Number(aiPct) > 0;

          const pub = ch?.publishedAt ?? ch?.PublishedAt ?? ch?.updatedAt ?? ch?.UpdatedAt;

          if (hasAi) {
            list.push({
              key: `ai-ch-${chId}`,
              type: 'ai_chapter',
              authorName,
              avatar,
              content: `Chương «${chapterTitle}» có đối chiếu nội dung AI — «${storyTitle}»`,
              timeLabel: formatRelativeTime(pub),
              sortTime: pub ? new Date(pub).getTime() : 0,
              href: storyId ? `/story/${storyId}` : '/story-list',
              icon: Sparkles,
              color: '#9D4EDD',
              themeKey: 'ai_chapter',
            });
          } else {
            list.push({
              key: `new-ch-${chId}`,
              type: 'new_chapter',
              authorName,
              avatar,
              content: `Đăng chương mới «${chapterTitle}» — «${storyTitle}»`,
              timeLabel: formatRelativeTime(pub),
              sortTime: pub ? new Date(pub).getTime() : 0,
              href: storyId ? `/story/${storyId}` : '/story-list',
              icon: BookOpen,
              color: '#2B7FFF',
              themeKey: 'new_chapter',
            });
          }
        }

        list.sort((a, b) => (b.sortTime || 0) - (a.sortTime || 0));
        const trimmed = list.slice(0, 4);

        if (!cancelled) setActivities(trimmed);
      } catch (e) {
        if (!cancelled) {
          setLoadError(e?.message ?? 'Không tải được hoạt động');
          setActivities([]);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    load();
    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (loadError) console.error('CommunityHighlightsSection:', loadError);
  }, [loadError]);

  const empty = !loading && activities.length === 0;

  return (
    <section
      lang="vi"
      className="relative overflow-hidden rounded-2xl border border-gray-200/90 bg-white p-6 shadow-[0_4px_24px_rgba(26,35,50,0.06)]"
    >
      <div className="pointer-events-none absolute -right-16 -top-16 h-40 w-40 rounded-full bg-gradient-to-br from-[#FFA500]/15 to-transparent blur-2xl" />
      <div className="pointer-events-none absolute -bottom-12 -left-12 h-32 w-32 rounded-full bg-gradient-to-tr from-[#13EC5B]/10 to-transparent blur-2xl" />

      <div className="relative flex items-center gap-3 mb-6">
        <div className="flex h-11 w-11 shrink-0 items-center justify-center rounded-xl bg-gradient-to-br from-[#FFA500] to-[#E67E00] shadow-lg shadow-orange-500/25 ring-2 ring-white">
          <Sparkles className="h-5 w-5 text-white drop-shadow-sm" />
        </div>
        <div>
          <h2 className="font-sans text-[24px] font-bold tracking-tight text-[#1A2332] antialiased">
            Hoạt Động Cộng Đồng
          </h2>
          <p className="mt-0.5 font-sans text-[14px] font-medium text-[#64748b] antialiased">
            Truyện mới, chương mới và chương có kiểm tra AI (theo dữ liệu nền tảng)
          </p>
        </div>
      </div>

      <div className="relative space-y-3.5">
        {loading ? (
          <div className="text-center py-10 font-sans text-[14px] text-[#90A1B9] antialiased">
            Đang tải hoạt động...
          </div>
        ) : loadError ? (
          <div className="text-center py-8 font-sans text-[13px] text-red-500 antialiased">
            {loadError}
          </div>
        ) : empty ? (
          <div className="text-center py-8 font-sans text-[14px] text-[#90A1B9] antialiased">
            Chưa có hoạt động để hiển thị
          </div>
        ) : (
          activities.map((item) => {
            const Icon = item.icon;
            const theme = TYPE_THEME[item.themeKey] ?? TYPE_THEME.new_chapter;
            return (
              <Link
                key={item.key}
                to={item.href}
                className={`group relative flex items-start gap-4 rounded-2xl border border-gray-200/90 p-4 pl-[18px] transition-all duration-200 hover:-translate-y-0.5 hover:border-gray-300/80 ${theme.border} ${theme.bg} ${theme.shadow}`}
              >
                <div
                  className={`relative h-[52px] w-[52px] shrink-0 overflow-hidden rounded-full border-2 border-white shadow-md transition-transform duration-200 ring-2 ring-transparent group-hover:scale-[1.03] ${theme.ring}`}
                >
                  <ImageWithFallback src={item.avatar} alt={item.authorName} className="h-full w-full object-cover" />
                </div>
                <div className="min-w-0 flex-1 pt-0.5">
                  <div className="mb-2 flex flex-wrap items-center gap-2 gap-y-1">
                    <h3 className="truncate font-sans text-[15px] font-bold text-[#1A2332] antialiased transition-colors group-hover:text-[#0f172a]">
                      {item.authorName}
                    </h3>
                    <span
                      className={`inline-flex shrink-0 rounded-full border px-2 py-0.5 font-sans text-[10px] font-bold uppercase tracking-wide antialiased ${theme.badge}`}
                    >
                      {theme.label}
                    </span>
                  </div>
                  <div className="flex gap-3">
                    <div
                      className={`mt-0.5 flex h-9 w-9 shrink-0 items-center justify-center rounded-xl ${theme.iconBg}`}
                    >
                      <Icon className="h-[18px] w-[18px]" style={{ color: item.color }} strokeWidth={2.25} />
                    </div>
                    <div className="min-w-0 flex-1">
                      <p className="font-sans text-[14px] leading-snug line-clamp-3 antialiased">
                        <ContentWithQuotes text={item.content} accentColor={item.color} />
                      </p>
                      <div className="mt-2 flex items-center gap-1.5">
                        <span className="inline-block h-1 w-1 rounded-full bg-[#94a3b8]" aria-hidden />
                        <span className="font-sans text-[12px] font-semibold text-[#64748b] antialiased">
                          {item.timeLabel}
                        </span>
                      </div>
                    </div>
                  </div>
                </div>
              </Link>
            );
          })
        )}
      </div>
    </section>
  );
}
