import { resolveBackendUrl } from './resolveBackendUrl';

/**
 * Avatar fallback chữ cái đầu (data URL) khi guest không có ảnh từ API profile.
 */
export function svgAvatarDataUrlFromName(name) {
    const initial = (String(name || 'T').trim()[0] || 'T').toUpperCase();
    const svg = `
      <svg xmlns="http://www.w3.org/2000/svg" width="256" height="256">
        <defs>
          <linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
            <stop offset="0" stop-color="#13EC5B"/>
            <stop offset="1" stop-color="#2B7FFF"/>
          </linearGradient>
        </defs>
        <rect width="256" height="256" rx="40" fill="url(#g)"/>
        <text x="50%" y="54%" dominant-baseline="middle" text-anchor="middle"
              font-family="Arial, Helvetica, sans-serif" font-size="120" font-weight="800" fill="white">${initial}</text>
      </svg>
    `.trim();
    return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
}

/** Tên tác giả ưu tiên profile, sau đó field từ story list/detail. */
export function resolveAuthorDisplayName(item, profile) {
    const fromStory =
        item?.authorName ??
        item?.AuthorName ??
        item?.author?.name ??
        item?.author?.displayName ??
        item?.authorDisplayName ??
        item?.AuthorDisplayName ??
        item?.createdByName ??
        item?.CreatedByName ??
        null;
    const trimmed = typeof fromStory === 'string' ? fromStory.trim() : '';
    return profile?.displayName?.trim() || trimmed || 'Tác giả';
}

/**
 * URL avatar tác giả: ưu tiên profile (đã đăng nhập), sau đó ảnh kèm list/detail truyện (AuthorAvatarUrl),
 * cuối cùng SVG chữ cái đầu (guest / chưa có ảnh).
 */
export function resolveAuthorAvatarUrl(storyItem, profile, displayNameOverride = null) {
    const nameForInitial =
        (typeof displayNameOverride === 'string' && displayNameOverride.trim()) ||
        resolveAuthorDisplayName(storyItem, profile);

    const avatarFromStory =
        storyItem?.authorAvatarUrl ??
        storyItem?.AuthorAvatarUrl ??
        storyItem?.authorAvatar ??
        storyItem?.AuthorAvatar ??
        storyItem?.author?.avatar ??
        storyItem?.author?.avatarUrl ??
        storyItem?.author?.AvatarUrl ??
        storyItem?.avatarUrl ??
        storyItem?.AvatarUrl ??
        null;

    const raw = profile?.avatarUrl || avatarFromStory;
    if (raw && typeof raw === 'string' && raw.trim()) {
        return resolveBackendUrl(raw.trim());
    }
    return svgAvatarDataUrlFromName(nameForInitial);
}
