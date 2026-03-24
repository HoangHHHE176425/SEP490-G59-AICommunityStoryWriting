/** Query ?preset=... — chỉ FE (một phần dùng pool + sắp xếp client). */
export const STORY_BROWSE_PRESET = {
    TOP_AUTHOR_FOLLOW: 'top_author_follow',
    AUTHOR_RANKING_VIEWS: 'author_ranking_views',
    NEW_AUTHOR_DEBUT: 'new_author_debut',
};

const VALID = new Set(Object.values(STORY_BROWSE_PRESET));

export function parseBrowsePreset(raw) {
    if (raw == null || typeof raw !== 'string') return '';
    const v = raw.trim();
    return VALID.has(v) ? v : '';
}

export function getBrowsePresetBanner(preset) {
    switch (preset) {
        case STORY_BROWSE_PRESET.TOP_AUTHOR_FOLLOW:
            return {
                title: 'Truyện từ tác giả được follow nhiều',
                detail:
                    'Danh sách ưu tiên theo lượt theo dõi tác giả (trên tập truyện lấy từ API, xếp thêm phía trình duyệt). Đổi bộ lọc hoặc kiểu sắp xếp sẽ thoát chế độ này.',
            };
        case STORY_BROWSE_PRESET.AUTHOR_RANKING_VIEWS:
            return {
                title: 'Theo lượt xem truyện đã xuất bản',
                detail:
                    'Sắp xếp truyện theo lượt xem giảm dần (gần với bảng xếp hạng tác giả theo tổng view, ở cấp từng truyện).',
            };
        case STORY_BROWSE_PRESET.NEW_AUTHOR_DEBUT:
            return {
                title: 'Tác giả mới — truyện đầu tay (ước lượng)',
                detail:
                    'Mỗi tác giả một truyện: ưu tiên truyện công bố sớm nhất trong phạm vi dữ liệu đã tải (FE). Đổi bộ lọc hoặc sắp xếp sẽ thoát chế độ này.',
            };
        default:
            return null;
    }
}

export function isPoolBasedPreset(preset) {
    return (
        preset === STORY_BROWSE_PRESET.TOP_AUTHOR_FOLLOW ||
        preset === STORY_BROWSE_PRESET.NEW_AUTHOR_DEBUT
    );
}

/** Sidebar: value '' = không dùng preset */
export const AUTHOR_PRESET_SIDEBAR_OPTIONS = [
    { value: '', label: 'Tất cả' },
    {
        value: STORY_BROWSE_PRESET.TOP_AUTHOR_FOLLOW,
        label: 'Tác giả được follow nhiều',
    },
    {
        value: STORY_BROWSE_PRESET.AUTHOR_RANKING_VIEWS,
        label: 'Xếp hạng theo lượt xem',
    },
    {
        value: STORY_BROWSE_PRESET.NEW_AUTHOR_DEBUT,
        label: 'Tác giả mới (truyện đầu tay)',
    },
];
