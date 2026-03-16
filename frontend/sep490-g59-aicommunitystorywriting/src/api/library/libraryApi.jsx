import axiosInstance from '../axiosInstance';

/** Chuẩn hóa một item truyện theo dõi từ BE (camelCase hoặc PascalCase). */
function normalizeFollowedStory(item) {
    return {
        id: item?.id ?? item?.Id,
        title: item?.title ?? item?.Title ?? '',
        slug: item?.slug ?? item?.Slug,
        coverImage: item?.coverImage ?? item?.CoverImage,
        summary: item?.summary ?? item?.Summary,
        authorId: item?.authorId ?? item?.AuthorId,
        authorName: item?.authorName ?? item?.AuthorName,
        status: item?.status ?? item?.Status,
        publishedChaptersCount: item?.publishedChaptersCount ?? item?.PublishedChaptersCount,
        latestUpdatedAt: item?.latestUpdatedAt ?? item?.LatestUpdatedAt,
    };
}

/** Chuẩn hóa một item tác giả theo dõi. */
function normalizeFollowedAuthor(item) {
    return {
        authorId: item?.authorId ?? item?.AuthorId,
        authorName: item?.authorName ?? item?.AuthorName ?? 'Tác giả',
    };
}

/** Chuẩn hóa một item lịch sử đọc. */
function normalizeReadingHistoryItem(item) {
    return {
        storyId: item?.storyId ?? item?.StoryId,
        storyTitle: item?.storyTitle ?? item?.StoryTitle ?? '',
        coverImage: item?.coverImage ?? item?.CoverImage,
        lastReadChapterId: item?.lastReadChapterId ?? item?.LastReadChapterId,
        lastReadChapterTitle: item?.lastReadChapterTitle ?? item?.LastReadChapterTitle,
        lastReadChapterOrder: item?.lastReadChapterOrder ?? item?.LastReadChapterOrder,
        lastReadAt: item?.lastReadAt ?? item?.LastReadAt,
    };
}

/**
 * Lấy thư viện của user đăng nhập: truyện theo dõi (PUBLISHED), tác giả theo dõi, lịch sử đọc.
 * GET api/library — cần đăng nhập.
 * @returns {Promise<{ followedStories: Array, followedAuthors: Array, readingHistory: Array }>}
 */
export async function getMyLibrary() {
    const response = await axiosInstance.get('/library');
    const data = response.data;
    const rawStories = data?.followedStories ?? data?.FollowedStories ?? [];
    const rawAuthors = data?.followedAuthors ?? data?.FollowedAuthors ?? [];
    const rawHistory = data?.readingHistory ?? data?.ReadingHistory ?? [];
    return {
        followedStories: rawStories.map(normalizeFollowedStory),
        followedAuthors: rawAuthors.map(normalizeFollowedAuthor),
        readingHistory: rawHistory.map(normalizeReadingHistoryItem),
    };
}
