import { resolveBackendUrl } from './resolveBackendUrl';
import { createInitialAvatarDataUrl } from './avatarFallback';

/**
 * Avatar fallback chữ cái đầu (data URL) khi guest không có ảnh từ API profile.
 */
export function svgAvatarDataUrlFromName(name) {
    return createInitialAvatarDataUrl(name, 256);
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
