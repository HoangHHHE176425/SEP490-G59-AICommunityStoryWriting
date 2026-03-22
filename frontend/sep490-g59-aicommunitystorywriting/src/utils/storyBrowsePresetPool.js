import { getStories } from '../api/story/storyApi';
import { getAuthorFollowersCount } from '../api/author/authorApi';

const PRESET_MAX_PAGES = 6;
const PRESET_PAGE_SIZE = 50;

async function fetchFlatStoryPool(sortBy, sortOrder) {
    const merged = [];
    for (let page = 1; page <= PRESET_MAX_PAGES; page += 1) {
        const res = await getStories({
            status: 'PUBLISHED',
            page,
            pageSize: PRESET_PAGE_SIZE,
            sortBy,
            sortOrder,
        });
        const items = Array.isArray(res?.items) ? res.items : Array.isArray(res?.Items) ? res.Items : [];
        merged.push(...items);
        if (items.length < PRESET_PAGE_SIZE) break;
    }
    return merged;
}

/**
 * Truyện xếp theo lượt follow tác giả (cao → thấp), tie-break theo lượt xem truyện.
 * Dữ liệu: pool các trang API sort theo total_views.
 */
export async function fetchStoriesSortedByAuthorFollowers() {
    const merged = await fetchFlatStoryPool('total_views', 'desc');
    const authorIds = [...new Set(merged.map((s) => s?.authorId ?? s?.AuthorId).filter(Boolean))];
    const counts = await Promise.all(authorIds.map((id) => getAuthorFollowersCount(id).catch(() => 0)));
    const followMap = {};
    authorIds.forEach((id, i) => {
        followMap[id] = counts[i];
    });
    merged.sort((a, b) => {
        const aid = a?.authorId ?? a?.AuthorId;
        const bid = b?.authorId ?? b?.AuthorId;
        const fa = aid != null ? followMap[aid] ?? 0 : 0;
        const fb = bid != null ? followMap[bid] ?? 0 : 0;
        if (fb !== fa) return fb - fa;
        const va = Number(a?.totalViews ?? a?.TotalViews ?? 0);
        const vb = Number(b?.totalViews ?? b?.TotalViews ?? 0);
        return vb - va;
    });
    return merged;
}

/**
 * Mỗi tác giả một truyện: lấy truyện công khai **cũ nhất** trong pool (created_at asc),
 * rồi hiển thị mới nhất trước (debut gần đây).
 */
export async function fetchDebutFirstStoryPerAuthor() {
    const merged = await fetchFlatStoryPool('created_at', 'asc');
    const seen = new Set();
    const out = [];
    for (const s of merged) {
        const aid = s?.authorId ?? s?.AuthorId;
        if (!aid || seen.has(aid)) continue;
        seen.add(aid);
        out.push(s);
    }
    out.sort((a, b) => {
        const ta = new Date(a?.createdAt ?? a?.CreatedAt ?? 0).getTime();
        const tb = new Date(b?.createdAt ?? b?.CreatedAt ?? 0).getTime();
        return tb - ta;
    });
    return out;
}
