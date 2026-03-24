import { Search, SlidersHorizontal, Grid3x3, List } from 'lucide-react';
import { browseUi as u } from './storyBrowseUi';

export function BrowseTopBar({
    searchQuery,
    setSearchQuery,
    viewMode,
    setViewMode,
    sortBy,
    setSortBy,
    totalResults,
    showMobileFilter,
    setShowMobileFilter,
    activeFiltersCount,
    isMobile = false,
    /** @type {{ title: string, detail: string } | null} */
    presetBanner = null,
}) {
    return (
        <div
            style={{
                backgroundColor: u.surface,
                borderRadius: u.radius,
                padding: '1.25rem',
                marginBottom: '1.5rem',
                border: `1px solid ${u.border}`,
                boxShadow: u.shadow,
                fontFamily: u.font,
            }}
        >
            {presetBanner && (
                <div
                    style={{
                        marginBottom: '1rem',
                        padding: '0.5rem 0.875rem',
                        borderRadius: u.radiusSm,
                        backgroundColor: u.surfaceMuted,
                        border: `1px solid ${u.border}`,
                        fontSize: '0.8125rem',
                        fontWeight: 600,
                        color: u.textSecondary,
                        display: 'flex',
                        alignItems: 'center',
                        gap: '0.5rem',
                    }}
                >
                    <span
                        style={{
                            fontSize: '0.625rem',
                            fontWeight: 700,
                            letterSpacing: '0.06em',
                            color: u.accent,
                            textTransform: 'uppercase',
                        }}
                    >
                        Chế độ
                    </span>
                    <span style={{ color: u.text }}>{presetBanner.title}</span>
                </div>
            )}

            <div style={{ display: 'flex', gap: '0.875rem', marginBottom: '1rem', flexWrap: 'wrap', alignItems: 'center' }}>
                <div style={{ flex: 1, minWidth: '260px', position: 'relative' }}>
                    <Search
                        style={{
                            position: 'absolute',
                            left: '0.875rem',
                            top: '50%',
                            transform: 'translateY(-50%)',
                            width: 18,
                            height: 18,
                            color: u.textSubtle,
                            pointerEvents: 'none',
                        }}
                    />
                    <input
                        type="text"
                        value={searchQuery}
                        onChange={(e) => setSearchQuery(e.target.value)}
                        placeholder="Tìm kiếm truyện, tác giả, thể loại…"
                        style={{
                            width: '100%',
                            padding: '0.6875rem 1rem 0.6875rem 2.75rem',
                            fontSize: '0.875rem',
                            fontFamily: u.font,
                            border: `1px solid ${u.border}`,
                            borderRadius: u.radiusSm,
                            outline: 'none',
                            transition: 'border-color 0.15s, box-shadow 0.15s',
                            backgroundColor: u.surfaceMuted,
                            color: u.text,
                        }}
                        onFocus={(e) => {
                            e.target.style.borderColor = u.accent;
                            e.target.style.boxShadow = `0 0 0 3px ${u.focusRing}`;
                            e.target.style.backgroundColor = u.surface;
                        }}
                        onBlur={(e) => {
                            e.target.style.borderColor = u.border;
                            e.target.style.boxShadow = 'none';
                            e.target.style.backgroundColor = u.surfaceMuted;
                        }}
                    />
                </div>

                {isMobile && (
                    <button
                        type="button"
                        onClick={() => setShowMobileFilter(!showMobileFilter)}
                        style={{
                            padding: '0.5625rem 1rem',
                            backgroundColor: u.surfaceMuted,
                            color: u.textSecondary,
                            fontSize: '0.8125rem',
                            fontWeight: 600,
                            fontFamily: u.font,
                            borderRadius: u.radiusSm,
                            border: `1px solid ${u.border}`,
                            cursor: 'pointer',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '0.5rem',
                            transition: 'background-color 0.15s, border-color 0.15s',
                        }}
                        onMouseEnter={(e) => {
                            e.currentTarget.style.backgroundColor = '#eef0f3';
                            e.currentTarget.style.borderColor = u.borderStrong;
                        }}
                        onMouseLeave={(e) => {
                            e.currentTarget.style.backgroundColor = u.surfaceMuted;
                            e.currentTarget.style.borderColor = u.border;
                        }}
                    >
                        <SlidersHorizontal style={{ width: 16, height: 16 }} />
                        Bộ lọc
                        {activeFiltersCount > 0 && (
                            <span
                                style={{
                                    minWidth: '1.125rem',
                                    height: '1.125rem',
                                    padding: '0 0.35rem',
                                    backgroundColor: u.accent,
                                    color: '#ffffff',
                                    fontSize: '0.625rem',
                                    fontWeight: 700,
                                    borderRadius: u.radiusPill,
                                    display: 'inline-flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                }}
                            >
                                {activeFiltersCount}
                            </span>
                        )}
                    </button>
                )}

                <div
                    style={{
                        display: 'flex',
                        gap: '2px',
                        backgroundColor: u.surfaceMuted,
                        padding: '3px',
                        borderRadius: u.radiusSm,
                        border: `1px solid ${u.border}`,
                    }}
                >
                    <button
                        type="button"
                        aria-label="Lưới"
                        onClick={() => setViewMode('grid')}
                        style={{
                            padding: '0.45rem 0.65rem',
                            backgroundColor: viewMode === 'grid' ? u.surface : 'transparent',
                            border: 'none',
                            borderRadius: 6,
                            cursor: 'pointer',
                            boxShadow: viewMode === 'grid' ? '0 1px 2px rgba(0,0,0,0.06)' : 'none',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                        }}
                    >
                        <Grid3x3
                            style={{
                                width: 18,
                                height: 18,
                                color: viewMode === 'grid' ? u.accent : u.textMuted,
                            }}
                        />
                    </button>
                    <button
                        type="button"
                        aria-label="Danh sách"
                        onClick={() => setViewMode('list')}
                        style={{
                            padding: '0.45rem 0.65rem',
                            backgroundColor: viewMode === 'list' ? u.surface : 'transparent',
                            border: 'none',
                            borderRadius: 6,
                            cursor: 'pointer',
                            boxShadow: viewMode === 'list' ? '0 1px 2px rgba(0,0,0,0.06)' : 'none',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                        }}
                    >
                        <List
                            style={{
                                width: 18,
                                height: 18,
                                color: viewMode === 'list' ? u.accent : u.textMuted,
                            }}
                        />
                    </button>
                </div>
            </div>

            <div
                style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    flexWrap: 'wrap',
                    gap: '1rem',
                    paddingTop: '0.875rem',
                    borderTop: `1px solid ${u.border}`,
                }}
            >
                <p style={{ fontSize: '0.875rem', color: u.textMuted, margin: 0, fontFamily: u.font }}>
                    Tìm thấy{' '}
                    <span style={{ fontWeight: 700, color: u.text }}>{totalResults}</span> truyện
                </p>

                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    <span style={{ fontSize: '0.8125rem', color: u.textMuted, fontFamily: u.font }}>Sắp xếp</span>
                    <select
                        value={sortBy}
                        onChange={(e) => setSortBy(e.target.value)}
                        style={{
                            padding: '0.45rem 2rem 0.45rem 0.65rem',
                            fontSize: '0.8125rem',
                            fontWeight: 500,
                            fontFamily: u.font,
                            border: `1px solid ${u.border}`,
                            borderRadius: u.radiusSm,
                            backgroundColor: u.surface,
                            color: u.text,
                            cursor: 'pointer',
                            outline: 'none',
                            appearance: 'none',
                            backgroundImage: `url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 24 24' stroke='%236b7280'%3E%3Cpath stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M19 9l-7 7-7-7'%3E%3C/path%3E%3C/svg%3E")`,
                            backgroundRepeat: 'no-repeat',
                            backgroundPosition: 'right 0.45rem center',
                            backgroundSize: '1rem',
                        }}
                    >
                        <option value="newest">Mới nhất</option>
                        <option value="popular">Phổ biến nhất</option>
                        <option value="views">Lượt xem</option>
                        <option value="rating">Đánh giá</option>
                    </select>
                </div>
            </div>
        </div>
    );
}
