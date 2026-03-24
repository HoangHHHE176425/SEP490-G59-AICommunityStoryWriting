import { useState, useEffect, useMemo } from 'react';
import { useSearchParams } from 'react-router-dom';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { Pagination } from '../../components/pagination/Pagination';
import { FilterSidebar } from '../../components/story-list/FilterSidebar';
import { BrowseTopBar } from '../../components/story-list/BrowseTopBar';
import { StoryCard } from '../../components/story-list/StoryCard';
import { StoryListItem } from '../../components/story-list/StoryListItem';
import { EmptyState } from '../../components/story-list/EmptyState';
import { getStories } from '../../api/story/storyApi';
import { getAllCategories } from '../../api/category/categoryApi';
import { mapStoryListItemToBrowseStory } from '../../utils/storyBrowseMap';
import {
    STORY_BROWSE_PRESET,
    parseBrowsePreset,
    getBrowsePresetBanner,
    isPoolBasedPreset,
} from '../../constants/storyBrowsePresets';
import {
    fetchStoriesSortedByAuthorFollowers,
    fetchDebutFirstStoryPerAuthor,
} from '../../utils/storyBrowsePresetPool';
import { browseUi as browsePageUi } from '../../components/story-list/storyBrowseUi';

function useMediaQuery(query) {
    const [matches, setMatches] = useState(() =>
        typeof window !== 'undefined' ? window.matchMedia(query).matches : false
    );
    useEffect(() => {
        const m = window.matchMedia(query);
        const onChange = () => setMatches(m.matches);
        m.addEventListener('change', onChange);
        setMatches(m.matches);
        return () => m.removeEventListener('change', onChange);
    }, [query]);
    return matches;
}

export function StoryBrowse() {
    const [searchParams, setSearchParams] = useSearchParams();
    const presetFromUrl = parseBrowsePreset(searchParams.get('preset'));
    const searchFromUrl = searchParams.get('search') ?? '';
    const [searchQuery, setSearchQuery] = useState(searchFromUrl);
    const [debouncedSearch, setDebouncedSearch] = useState(searchFromUrl);

    useEffect(() => {
        setSearchQuery(searchFromUrl);
        setDebouncedSearch(searchFromUrl);
    }, [searchFromUrl]);

    useEffect(() => {
        const t = setTimeout(() => setDebouncedSearch(searchQuery.trim()), 400);
        return () => clearTimeout(t);
    }, [searchQuery]);

    const [viewMode, setViewMode] = useState('grid');
    const [sortBy, setSortBy] = useState('newest');
    const [currentPage, setCurrentPage] = useState(1);
    const [showMobileFilter, setShowMobileFilter] = useState(false);
    const isLg = useMediaQuery('(min-width: 1024px)');

    const [selectedCategoryIds, setSelectedCategoryIds] = useState([]);
    const [selectedStatus, setSelectedStatus] = useState('all');
    const [selectedAgeRating, setSelectedAgeRating] = useState('all');
    const [selectedChapterScale, setSelectedChapterScale] = useState('all');

    const [categories, setCategories] = useState([]);
    const [stories, setStories] = useState([]);
    const [totalCount, setTotalCount] = useState(0);
    const [loading, setLoading] = useState(true);
    const [listError, setListError] = useState(null);

    const itemsPerPage = 24;

    const filtersDefault = useMemo(
        () =>
            selectedCategoryIds.length === 0 &&
            selectedStatus === 'all' &&
            selectedAgeRating === 'all' &&
            selectedChapterScale === 'all' &&
            !debouncedSearch,
        [selectedCategoryIds, selectedStatus, selectedAgeRating, selectedChapterScale, debouncedSearch]
    );

    const useClientPool = filtersDefault && isPoolBasedPreset(presetFromUrl);

    useEffect(() => {
        setCurrentPage(1);
    }, [presetFromUrl]);

    useEffect(() => {
        if (presetFromUrl === STORY_BROWSE_PRESET.AUTHOR_RANKING_VIEWS) {
            setSortBy('views');
        } else if (isPoolBasedPreset(presetFromUrl)) {
            setSortBy('newest');
        }
    }, [presetFromUrl]);

    useEffect(() => {
        let cancelled = false;
        (async () => {
            try {
                const raw = await getAllCategories({ includeInactive: false });
                const arr = Array.isArray(raw) ? raw : [];
                const normalized = arr
                    .map((x) => ({
                        id: String(x.id ?? x.Id ?? ''),
                        name: String(x.name ?? x.Name ?? '').trim(),
                    }))
                    .filter((x) => x.id && x.name);
                normalized.sort((a, b) => a.name.localeCompare(b.name, 'vi'));
                if (!cancelled) setCategories(normalized);
            } catch {
                if (!cancelled) setCategories([]);
            }
        })();
        return () => {
            cancelled = true;
        };
    }, []);

    useEffect(() => {
        if (useClientPool) return;
        let cancelled = false;
        (async () => {
            setLoading(true);
            setListError(null);
            try {
                const params = {
                    status: 'PUBLISHED',
                    page: currentPage,
                    pageSize: itemsPerPage,
                };
                if (debouncedSearch) params.search = debouncedSearch;
                if (selectedCategoryIds.length > 0) params.categoryIds = selectedCategoryIds;

                if (selectedStatus === 'ongoing') params.storyProgressStatus = 'ONGOING';
                else if (selectedStatus === 'completed') params.storyProgressStatus = 'COMPLETED';
                else if (selectedStatus === 'hiatus') params.storyProgressStatus = 'HIATUS';

                if (selectedAgeRating === '13+') params.ageRating = '13+';
                else if (selectedAgeRating === '16+') params.ageRating = '16+';
                else if (selectedAgeRating === '18+') params.ageRating = '18+';

                if (selectedChapterScale === 'under20') params.maxTotalChapters = 19;
                else if (selectedChapterScale === '20to99') {
                    params.minTotalChapters = 20;
                    params.maxTotalChapters = 99;
                } else if (selectedChapterScale === '100plus') params.minTotalChapters = 100;

                if (presetFromUrl === STORY_BROWSE_PRESET.AUTHOR_RANKING_VIEWS) {
                    params.sortBy = 'total_views';
                    params.sortOrder = 'desc';
                } else {
                    switch (sortBy) {
                        case 'popular':
                        case 'views':
                            params.sortBy = 'total_views';
                            params.sortOrder = 'desc';
                            break;
                        case 'rating':
                            params.sortBy = 'avg_rating';
                            params.sortOrder = 'desc';
                            break;
                        case 'newest':
                        default:
                            params.sortBy = 'created_at';
                            params.sortOrder = 'desc';
                    }
                }

                const res = await getStories(params);
                const items = Array.isArray(res?.items) ? res.items : Array.isArray(res?.Items) ? res.Items : [];
                const mapped = items.map(mapStoryListItemToBrowseStory).filter(Boolean);
                const tc = Number(res?.totalCount ?? res?.TotalCount ?? mapped.length);

                if (!cancelled) {
                    setStories(mapped);
                    setTotalCount(Number.isFinite(tc) ? tc : mapped.length);
                }
            } catch (e) {
                if (!cancelled) {
                    setStories([]);
                    setTotalCount(0);
                    setListError(e?.message ?? 'Không tải được danh sách truyện');
                }
            } finally {
                if (!cancelled) setLoading(false);
            }
        })();
        return () => {
            cancelled = true;
        };
    }, [
        useClientPool,
        currentPage,
        sortBy,
        debouncedSearch,
        selectedCategoryIds,
        selectedStatus,
        selectedAgeRating,
        selectedChapterScale,
        presetFromUrl,
    ]);

    useEffect(() => {
        if (!useClientPool) return;
        let cancelled = false;
        (async () => {
            setLoading(true);
            setListError(null);
            try {
                const pool =
                    presetFromUrl === STORY_BROWSE_PRESET.TOP_AUTHOR_FOLLOW
                        ? await fetchStoriesSortedByAuthorFollowers()
                        : await fetchDebutFirstStoryPerAuthor();
                if (cancelled) return;
                const total = pool.length;
                const start = (currentPage - 1) * itemsPerPage;
                const slice = pool.slice(start, start + itemsPerPage);
                const mapped = slice.map(mapStoryListItemToBrowseStory).filter(Boolean);
                setStories(mapped);
                setTotalCount(total);
            } catch (e) {
                if (!cancelled) {
                    setStories([]);
                    setTotalCount(0);
                    setListError(e?.message ?? 'Không tải được danh sách truyện');
                }
            } finally {
                if (!cancelled) setLoading(false);
            }
        })();
        return () => {
            cancelled = true;
        };
    }, [useClientPool, presetFromUrl, currentPage, itemsPerPage]);

    const totalPages = Math.max(1, Math.ceil(totalCount / itemsPerPage));

    const clearAllFilters = () => {
        setSelectedCategoryIds([]);
        setSelectedStatus('all');
        setSelectedAgeRating('all');
        setSelectedChapterScale('all');
        setSearchQuery('');
        setCurrentPage(1);
        const next = new URLSearchParams(searchParams);
        next.delete('search');
        next.delete('preset');
        setSearchParams(next, { replace: true });
    };

    const activeFiltersCount =
        (presetFromUrl ? 1 : 0) +
        selectedCategoryIds.length +
        (selectedStatus !== 'all' ? 1 : 0) +
        (selectedAgeRating !== 'all' ? 1 : 0) +
        (selectedChapterScale !== 'all' ? 1 : 0);

    const handleSearchChange = (value) => {
        setSearchQuery(value);
        setCurrentPage(1);
        const next = new URLSearchParams(searchParams);
        if ((value || '').trim()) {
            next.set('search', value.trim());
            const p = parseBrowsePreset(next.get('preset'));
            if (p && isPoolBasedPreset(p)) next.delete('preset');
        } else {
            next.delete('search');
        }
        setSearchParams(next, { replace: true });
    };

    /** Chọn preset tác giả từ sidebar — pool preset xóa thể loại + trạng thái để khớp logic tải dữ liệu */
    const handleAuthorPresetChange = (value) => {
        setCurrentPage(1);
        if (
            value === STORY_BROWSE_PRESET.TOP_AUTHOR_FOLLOW ||
            value === STORY_BROWSE_PRESET.NEW_AUTHOR_DEBUT
        ) {
            setSelectedCategoryIds([]);
            setSelectedStatus('all');
        }
        const next = new URLSearchParams(searchParams);
        if (!value) {
            next.delete('preset');
        } else {
            next.set('preset', value);
        }
        setSearchParams(next, { replace: true });
    };

    /** Giống setState: nhận mảng hoặc updater (prev) => next — FilterSidebar dùng updater khi tick thể loại */
    const setCategoryIdsAndResetPage = (action) => {
        const p = parseBrowsePreset(searchParams.get('preset'));
        if (p && isPoolBasedPreset(p)) {
            const next = new URLSearchParams(searchParams);
            next.delete('preset');
            setSearchParams(next, { replace: true });
        }
        setSelectedCategoryIds(action);
        setCurrentPage(1);
    };

    const handleStatusChange = (value) => {
        const p = parseBrowsePreset(searchParams.get('preset'));
        if (p && isPoolBasedPreset(p)) {
            const next = new URLSearchParams(searchParams);
            next.delete('preset');
            setSearchParams(next, { replace: true });
        }
        setSelectedStatus(value);
        setCurrentPage(1);
    };

    const stripPoolPresetFromUrl = () => {
        const p = parseBrowsePreset(searchParams.get('preset'));
        if (p && isPoolBasedPreset(p)) {
            const next = new URLSearchParams(searchParams);
            next.delete('preset');
            setSearchParams(next, { replace: true });
        }
    };

    const handleAgeRatingChange = (value) => {
        stripPoolPresetFromUrl();
        setSelectedAgeRating(value);
        setCurrentPage(1);
    };

    const handleChapterScaleChange = (value) => {
        stripPoolPresetFromUrl();
        setSelectedChapterScale(value);
        setCurrentPage(1);
    };

    return (
        <div
            style={{
                minHeight: '100vh',
                backgroundColor: browsePageUi.pageBg,
                display: 'flex',
                flexDirection: 'column',
                fontFamily: browsePageUi.font,
            }}
        >
            <Header />

            <div style={{ maxWidth: '1280px', margin: '0 auto', padding: '2rem 1.25rem', width: '100%', flex: 1 }}>
                <div
                    style={{
                        display: 'grid',
                        gridTemplateColumns: isLg ? 'minmax(268px, 300px) 1fr' : '1fr',
                        gap: '1.75rem',
                        alignItems: 'start',
                    }}
                >
                    <aside style={{ display: isLg ? 'block' : showMobileFilter ? 'block' : 'none' }}>
                        <FilterSidebar
                            authorPreset={presetFromUrl}
                            onAuthorPresetChange={handleAuthorPresetChange}
                            categories={categories}
                            selectedCategoryIds={selectedCategoryIds}
                            setSelectedCategoryIds={setCategoryIdsAndResetPage}
                            selectedStatus={selectedStatus}
                            setSelectedStatus={handleStatusChange}
                            selectedAgeRating={selectedAgeRating}
                            setSelectedAgeRating={handleAgeRatingChange}
                            selectedChapterScale={selectedChapterScale}
                            setSelectedChapterScale={handleChapterScaleChange}
                            activeFiltersCount={activeFiltersCount}
                            clearAllFilters={clearAllFilters}
                        />
                    </aside>

                    <main>
                        <BrowseTopBar
                            searchQuery={searchQuery}
                            setSearchQuery={handleSearchChange}
                            viewMode={viewMode}
                            setViewMode={setViewMode}
                            sortBy={sortBy}
                            setSortBy={(v) => {
                                setSortBy(v);
                                setCurrentPage(1);
                                const next = new URLSearchParams(searchParams);
                                if (next.has('preset')) {
                                    next.delete('preset');
                                    setSearchParams(next, { replace: true });
                                }
                            }}
                            totalResults={totalCount}
                            showMobileFilter={showMobileFilter}
                            setShowMobileFilter={setShowMobileFilter}
                            activeFiltersCount={activeFiltersCount}
                            isMobile={!isLg}
                            presetBanner={getBrowsePresetBanner(presetFromUrl)}
                        />

                        {listError && (
                            <div
                                style={{
                                    padding: '0.875rem 1rem',
                                    marginBottom: '1rem',
                                    background: browsePageUi.dangerSoft,
                                    color: browsePageUi.danger,
                                    borderRadius: browsePageUi.radiusSm,
                                    fontSize: '0.875rem',
                                    border: `1px solid rgba(185, 28, 28, 0.2)`,
                                }}
                            >
                                {listError}
                            </div>
                        )}

                        {loading ? (
                            <div
                                style={{
                                    textAlign: 'center',
                                    padding: '4rem 1.5rem',
                                    color: browsePageUi.textMuted,
                                    fontSize: '0.9375rem',
                                    letterSpacing: '0.02em',
                                }}
                                role="status"
                                aria-live="polite"
                            >
                                Đang tải truyện…
                            </div>
                        ) : stories.length === 0 ? (
                            <EmptyState onClearFilters={clearAllFilters} />
                        ) : (
                            <>
                                {viewMode === 'grid' ? (
                                    <div
                                        style={{
                                            display: 'grid',
                                            gridTemplateColumns: 'repeat(auto-fill, minmax(152px, 1fr))',
                                            gap: '1.25rem',
                                            alignItems: 'stretch',
                                        }}
                                    >
                                        {stories.map((story) => (
                                            <StoryCard key={story.id} story={story} />
                                        ))}
                                    </div>
                                ) : (
                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                                        {stories.map((story) => (
                                            <StoryListItem key={story.id} story={story} />
                                        ))}
                                    </div>
                                )}

                                <div style={{ marginTop: '2rem' }}>
                                    <Pagination
                                        currentPage={currentPage}
                                        totalPages={totalPages}
                                        totalItems={totalCount}
                                        itemsPerPage={itemsPerPage}
                                        onPageChange={setCurrentPage}
                                        itemLabel="truyện"
                                    />
                                </div>
                            </>
                        )}
                    </main>
                </div>
            </div>

            <Footer />
        </div>
    );
}
