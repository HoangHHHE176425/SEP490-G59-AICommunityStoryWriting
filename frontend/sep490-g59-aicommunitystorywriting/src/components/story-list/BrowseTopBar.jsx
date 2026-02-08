import { Search, SlidersHorizontal, Grid3x3, List } from 'lucide-react';

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
    activeFiltersCount
}) {
    return (
        <div style={{
            backgroundColor: '#ffffff',
            borderRadius: '12px',
            padding: '1.25rem',
            marginBottom: '1.5rem',
            border: '1px solid #e2e8f0'
        }}>
            {/* Search + Actions */}
            <div style={{ display: 'flex', gap: '1rem', marginBottom: '1rem', flexWrap: 'wrap' }}>
                {/* Search */}
                <div style={{ flex: 1, minWidth: '250px', position: 'relative' }}>
                    <Search style={{
                        position: 'absolute',
                        left: '1rem',
                        top: '50%',
                        transform: 'translateY(-50%)',
                        width: '18px',
                        height: '18px',
                        color: '#94a3b8',
                        pointerEvents: 'none'
                    }} />
                    <input
                        type="text"
                        value={searchQuery}
                        onChange={(e) => setSearchQuery(e.target.value)}
                        placeholder="Tìm kiếm truyện, tác giả..."
                        style={{
                            width: '100%',
                            padding: '0.75rem 1rem 0.75rem 3rem',
                            fontSize: '0.875rem',
                            border: '1px solid #e2e8f0',
                            borderRadius: '9999px',
                            outline: 'none',
                            transition: 'all 0.2s'
                        }}
                        onFocus={(e) => {
                            e.target.style.borderColor = '#13ec5b';
                            e.target.style.boxShadow = '0 0 0 3px rgba(19, 236, 91, 0.1)';
                        }}
                        onBlur={(e) => {
                            e.target.style.borderColor = '#e2e8f0';
                            e.target.style.boxShadow = 'none';
                        }}
                    />
                </div>

                {/* Mobile Filter Toggle */}
                {window.innerWidth < 1024 && (
                    <button
                        onClick={() => setShowMobileFilter(!showMobileFilter)}
                        style={{
                            padding: '0.5rem 1rem',
                            backgroundColor: '#f1f5f9',
                            color: '#475569',
                            fontSize: '0.875rem',
                            fontWeight: 600,
                            borderRadius: '9999px',
                            border: 'none',
                            cursor: 'pointer',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '0.5rem',
                            transition: 'background-color 0.2s'
                        }}
                        onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#e2e8f0'}
                        onMouseLeave={(e) => e.currentTarget.style.backgroundColor = '#f1f5f9'}
                    >
                        <SlidersHorizontal style={{ width: '16px', height: '16px' }} />
                        Bộ lọc
                        {activeFiltersCount > 0 && (
                            <span style={{
                                padding: '0.125rem 0.5rem',
                                backgroundColor: '#13ec5b',
                                color: '#ffffff',
                                fontSize: '0.75rem',
                                fontWeight: 700,
                                borderRadius: '9999px'
                            }}>
                                {activeFiltersCount}
                            </span>
                        )}
                    </button>
                )}

                {/* View Mode Toggle */}
                <div style={{ display: 'flex', gap: '0.5rem', backgroundColor: '#f8fafc', padding: '0.25rem', borderRadius: '0.5rem' }}>
                    <button
                        onClick={() => setViewMode('grid')}
                        style={{
                            padding: '0.5rem 0.75rem',
                            backgroundColor: viewMode === 'grid' ? '#ffffff' : 'transparent',
                            border: 'none',
                            borderRadius: '0.375rem',
                            cursor: 'pointer',
                            transition: 'all 0.2s',
                            boxShadow: viewMode === 'grid' ? '0 1px 2px rgba(0,0,0,0.05)' : 'none',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center'
                        }}
                    >
                        <Grid3x3 style={{ width: '18px', height: '18px', color: viewMode === 'grid' ? '#13ec5b' : '#64748b' }} />
                    </button>
                    <button
                        onClick={() => setViewMode('list')}
                        style={{
                            padding: '0.5rem 0.75rem',
                            backgroundColor: viewMode === 'list' ? '#ffffff' : 'transparent',
                            border: 'none',
                            borderRadius: '0.375rem',
                            cursor: 'pointer',
                            transition: 'all 0.2s',
                            boxShadow: viewMode === 'list' ? '0 1px 2px rgba(0,0,0,0.05)' : 'none',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center'
                        }}
                    >
                        <List style={{ width: '18px', height: '18px', color: viewMode === 'list' ? '#13ec5b' : '#64748b' }} />
                    </button>
                </div>
            </div>

            {/* Sort + Result Count */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', flexWrap: 'wrap', gap: '1rem' }}>
                <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>
                    Tìm thấy <span style={{ fontWeight: 600, color: '#1e293b' }}>{totalResults}</span> truyện
                </p>

                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    <span style={{ fontSize: '0.875rem', color: '#64748b' }}>Sắp xếp:</span>
                    <select
                        value={sortBy}
                        onChange={(e) => setSortBy(e.target.value)}
                        style={{
                            padding: '0.5rem 2rem 0.5rem 0.75rem',
                            fontSize: '0.875rem',
                            fontWeight: 500,
                            border: '1px solid #e2e8f0',
                            borderRadius: '0.5rem',
                            backgroundColor: '#ffffff',
                            color: '#1e293b',
                            cursor: 'pointer',
                            outline: 'none',
                            appearance: 'none',
                            backgroundImage: `url("data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 24 24' stroke='%2364748b'%3E%3Cpath stroke-linecap='round' stroke-linejoin='round' stroke-width='2' d='M19 9l-7 7-7-7'%3E%3C/path%3E%3C/svg%3E")`,
                            backgroundRepeat: 'no-repeat',
                            backgroundPosition: 'right 0.5rem center',
                            backgroundSize: '1rem'
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
