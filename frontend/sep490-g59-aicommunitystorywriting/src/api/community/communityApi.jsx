import axiosInstance from '../axiosInstance';
import { getStories } from '../story/storyApi';

/**
 * GET /api/community/stats — thống kê công khai (guest OK).
 * @returns {Promise<{ publishedStoriesCount: number, authorsCount: number, totalViews: number }>}
 */
export async function getCommunityStats() {
  try {
    const res = await axiosInstance.get('/community/stats');
    const d = res?.data ?? {};
    return {
      publishedStoriesCount: Number(d.publishedStoriesCount ?? d.PublishedStoriesCount ?? 0) || 0,
      authorsCount: Number(d.authorsCount ?? d.AuthorsCount ?? 0) || 0,
      totalViews: Number(d.totalViews ?? d.TotalViews ?? 0) || 0,
      statsSource: 'api',
    };
  } catch (e) {
    const status = e?.response?.status;
    // BE chưa có route → không báo lỗi UI, tính từ danh sách truyện public
    if (status === 404 || status === 501) {
      const fromStories = await getCommunityStatsFromPublishedStories();
      return { ...fromStories, statsSource: 'stories' };
    }
    throw e;
  }
}

/**
 * Khi BE chưa triển khai /community/stats (404): gom số liệu từ GET /stories?status=PUBLISHED (phân trang).
 * Khách vẫn gọi được; kết quả khớp logic công khai (truyện đã xuất bản, không compliance ẩn).
 * @returns {Promise<{ publishedStoriesCount: number, authorsCount: number, totalViews: number }>}
 */
export async function getCommunityStatsFromPublishedStories() {
  const pageSize = 100;
  let page = 1;
  let totalCount = 0;
  let totalViews = 0;
  const authorIds = new Set();

  for (;;) {
    const res = await getStories({
      status: 'PUBLISHED',
      page,
      pageSize,
      sortBy: 'created_at',
      sortOrder: 'desc',
    });
    const items = Array.isArray(res?.items) ? res.items : Array.isArray(res?.Items) ? res.Items : [];
    const tc = Number(res?.totalCount ?? res?.TotalCount);
    if (!Number.isNaN(tc) && tc >= 0) totalCount = tc;

    for (const s of items) {
      totalViews += Number(s?.totalViews ?? s?.TotalViews ?? 0) || 0;
      const aid = s?.authorId ?? s?.AuthorId;
      if (aid) authorIds.add(String(aid));
    }

    if (items.length < pageSize) break;
    page += 1;
    if (page > 200) break;
  }

  return {
    publishedStoriesCount: totalCount,
    authorsCount: authorIds.size,
    totalViews,
  };
}
