import { resolveBackendUrl } from './resolveBackendUrl';

/** Format lượt xem ngắn gọn (card/list). */
export function formatStoryViews(n) {
  const v = Number(n) || 0;
  if (v >= 1e9) return `${(v / 1e9).toFixed(1)}B`;
  if (v >= 1e6) return `${(v / 1e6).toFixed(1)}M`;
  if (v >= 1e3) return `${(v / 1e3).toFixed(1)}K`;
  return String(v);
}

export function formatStoryFollows(n) {
  const v = Number(n) || 0;
  if (v >= 1e6) return `${(v / 1e6).toFixed(1)}M`;
  if (v >= 1e3) return `${(v / 1e3).toFixed(1)}K`;
  return String(v);
}

/**
 * Map item từ GET /stories (StoryListItemDto) → shape dùng StoryCard / StoryListItem.
 */
export function mapStoryListItemToBrowseStory(raw) {
  const id = raw?.id ?? raw?.Id;
  if (id == null) return null;

  const title = raw?.title ?? raw?.Title ?? '';
  const author = raw?.authorName ?? raw?.AuthorName ?? 'Tác giả';
  const coverPath = raw?.coverImage ?? raw?.CoverImage ?? '';
  const cover = coverPath ? resolveBackendUrl(String(coverPath).trim()) : '';

  const progress = String(raw?.storyProgressStatus ?? raw?.StoryProgressStatus ?? 'ONGOING').toUpperCase();
  let status = 'ongoing';
  if (progress === 'COMPLETED') status = 'completed';
  else if (progress === 'HIATUS') status = 'hiatus';

  const categoryNamesStr = raw?.categoryNames ?? raw?.CategoryNames ?? '';
  const categories = categoryNamesStr
    ? String(categoryNamesStr)
        .split(',')
        .map((x) => x.trim())
        .filter(Boolean)
    : [];

  const categoryIds = Array.isArray(raw?.categoryIds)
    ? raw.categoryIds
    : Array.isArray(raw?.CategoryIds)
      ? raw.CategoryIds
      : [];

  const views = Number(raw?.totalViews ?? raw?.TotalViews ?? 0) || 0;
  const ratingNum = raw?.avgRating ?? raw?.AvgRating;
  const rating = ratingNum != null && !Number.isNaN(Number(ratingNum)) ? Number(ratingNum).toFixed(1) : '—';

  const chapters =
    Number(raw?.publishedChaptersCount ?? raw?.PublishedChaptersCount ?? raw?.totalChapters ?? raw?.TotalChapters ?? 0) || 0;

  const follows = Number(raw?.totalFavorites ?? raw?.TotalFavorites ?? 0) || 0;
  const description = raw?.summary ?? raw?.Summary ?? '';

  const ageRaw = String(raw?.ageRating ?? raw?.AgeRating ?? 'ALL').toUpperCase();
  let ageRating = 'all-ages';
  if (ageRaw === '13+') ageRating = '13+';
  else if (ageRaw === '16+') ageRating = '16+';
  else if (ageRaw === '18+') ageRating = '18+';

  const totalChaptersRaw = Number(raw?.totalChapters ?? raw?.TotalChapters ?? chapters) || 0;
  const type = totalChaptersRaw >= 40 ? 'long' : 'short';

  return {
    id: String(id),
    title,
    author,
    cover: cover || undefined,
    type,
    categories,
    categoryIds: categoryIds.map((x) => String(x)),
    status,
    ageRating,
    chapters,
    views,
    follows,
    rating,
    description,
    slug: raw?.slug ?? raw?.Slug ?? '',
  };
}
