import { useEffect, useState } from 'react';
import { getCommunityStats } from '../api/community/communityApi';

/**
 * GET /api/community/stats — guest OK, không bắt buộc JWT.
 * @param {{ enabled?: boolean }} options — enabled=false: không gọi API (dùng khi dữ liệu lấy từ component cha).
 */
export function useCommunityStats({ enabled = true } = {}) {
  const [stats, setStats] = useState(null);
  const [loading, setLoading] = useState(Boolean(enabled));
  const [error, setError] = useState(null);

  useEffect(() => {
    if (!enabled) {
      setLoading(false);
      return undefined;
    }

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
  }, [enabled]);

  return { stats, loading, error };
}
