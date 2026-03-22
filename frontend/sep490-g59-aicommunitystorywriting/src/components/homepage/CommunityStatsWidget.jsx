import { useEffect, useState } from 'react';
import { Activity, BookOpen, Users, Eye } from 'lucide-react';
import { getCommunityStats } from '../../api/community/communityApi';

function formatCompact(num) {
  if (num === null || num === undefined || Number.isNaN(Number(num))) return '0';
  const n = Number(num);
  if (n >= 1e9) return `${(n / 1e9).toFixed(1)}B`;
  if (n >= 1e6) return `${(n / 1e6).toFixed(1)}M`;
  if (n >= 1e3) return `${(n / 1e3).toFixed(1)}K`;
  return n.toLocaleString('vi-VN');
}

export function CommunityStatsWidget() {
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState(null);

  useEffect(() => {
    let cancelled = false;
    (async () => {
      setLoading(true);
      setError(null);
      try {
        const data = await getCommunityStats();
        if (!cancelled) setStats(data);
      } catch (e) {
        if (!cancelled) {
          setError(e?.message ?? 'Không tải được');
          setStats(null);
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();
    return () => {
      cancelled = true;
    };
  }, []);

  const rows = stats
    ? [
        {
          label: 'Truyện đã xuất bản',
          value: formatCompact(stats.publishedStoriesCount),
          raw: stats.publishedStoriesCount,
          icon: BookOpen,
          color: '#2B7FFF',
        },
        {
          label: 'Tác giả',
          value: formatCompact(stats.authorsCount),
          raw: stats.authorsCount,
          icon: Users,
          color: '#13EC5B',
        },
        {
          label: 'Tổng lượt xem',
          value: formatCompact(stats.totalViews),
          raw: stats.totalViews,
          icon: Eye,
          color: '#FB2C36',
        },
      ]
    : [];

  return (
    <div className="bg-white rounded-2xl border border-gray-200 p-5">
      <div className="flex items-center gap-2 mb-1">
        <Activity className="w-5 h-5 text-[#9D4EDD]" />
        <h3 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[18px]">
          Số liệu cộng đồng
        </h3>
      </div>
      <p className="text-[11px] text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] mb-4">
        Cập nhật theo truyện trạng thái đã xuất bản
      </p>

      {loading ? (
        <div className="text-center py-6 text-[#90A1B9] text-sm font-['Plus_Jakarta_Sans',sans-serif]">
          Đang tải...
        </div>
      ) : error ? (
        <div className="text-center py-4 text-red-500 text-xs font-['Plus_Jakarta_Sans',sans-serif]">{error}</div>
      ) : (
        <div className="space-y-3">
          {rows.map((row) => {
            const Icon = row.icon;
            return (
              <div
                key={row.label}
                className="flex items-center gap-3 p-3 rounded-xl bg-gray-50/80 border border-gray-100"
              >
                <div
                  className="w-10 h-10 rounded-lg flex items-center justify-center flex-shrink-0"
                  style={{ backgroundColor: `${row.color}18` }}
                >
                  <Icon className="w-5 h-5" style={{ color: row.color }} />
                </div>
                <div className="flex-1 min-w-0">
                  <p className="text-[11px] text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-medium uppercase tracking-wide">
                    {row.label}
                  </p>
                  <p className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[20px] leading-tight">
                    {row.value}
                  </p>
                </div>
              </div>
            );
          })}
        </div>
      )}

      {!loading && !error && stats ? (
        <p className="mt-3 text-[10px] text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] leading-snug">
          * Tác giả: số người có ít nhất một truyện đã xuất bản.
          {stats?.statsSource === 'stories' ? (
            <>
              {' '}
              <span className="text-[#94a3b8]">
                (Tính từ API truyện công khai khi /community/stats chưa khả dụng.)
              </span>
            </>
          ) : null}
        </p>
      ) : null}
    </div>
  );
}
