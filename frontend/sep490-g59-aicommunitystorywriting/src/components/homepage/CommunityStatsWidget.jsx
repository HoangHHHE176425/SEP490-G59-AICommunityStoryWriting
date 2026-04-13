import { Activity, BookOpen, Users, Eye } from 'lucide-react';
import { useCommunityStats } from '../../hooks/useCommunityStats';

function formatCompact(num) {
  if (num === null || num === undefined || Number.isNaN(Number(num))) return '0';
  const n = Number(num);
  if (n >= 1e9) return `${(n / 1e9).toFixed(1)}B`;
  if (n >= 1e6) return `${(n / 1e6).toFixed(1)}M`;
  if (n >= 1e3) return `${(n / 1e3).toFixed(1)}K`;
  return n.toLocaleString('vi-VN');
}

/**
 * Thống kê sidebar — GET /api/community/stats (guest OK).
 * @param {{ skipFetch?: boolean, stats?: object, loading?: boolean, error?: string | null }} props
 *   Khi skipFetch=true: truyền stats/loading/error từ cha (một lần gọi API).
 */
export function CommunityStatsWidget({ skipFetch = false, stats: statsProp, loading: loadingProp, error: errorProp } = {}) {
  const internal = useCommunityStats({ enabled: !skipFetch });

  const stats = skipFetch ? statsProp : internal.stats;
  const loading = skipFetch ? loadingProp : internal.loading;
  const error = skipFetch ? errorProp : internal.error;

  const rows = stats
    ? [
        {
          label: 'Truyện công khai',
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
      <p className="mb-4 text-[12px] leading-snug text-[#64748b] font-['Plus_Jakarta_Sans',sans-serif]">
        Thống kê từ các truyện đang được hiển thị công khai trên nền tảng (đã xuất bản, không bị ẩn theo quy định).
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
        <p className="mt-3 text-[11px] leading-snug text-[#94a3b8] font-['Plus_Jakarta_Sans',sans-serif]">
          * <span className="text-[#64748b]">Tác giả</span> là số người viết khác nhau, mỗi người chỉ tính một lần nếu có ít
          nhất một truyện thuộc nhóm truyện công khai ở trên.
          {stats?.statsSource === 'stories' ? (
            <span className="text-[#94a3b8]"> Số liệu có thể là ước lượng khi hệ thống đang đồng bộ.</span>
          ) : null}
        </p>
      ) : null}
    </div>
  );
}
