import { SlidersHorizontal } from 'lucide-react';

export function FilterSidebar({
    selectedStoryType,
    setSelectedStoryType,
    selectedCategories,
    setSelectedCategories,
    selectedStatus,
    setSelectedStatus,
    selectedAgeRating,
    setSelectedAgeRating,
    activeFiltersCount,
    clearAllFilters,
    longStoryCategories,
    shortStoryCategories
}) {
    // Get available categories based on story type
    const availableCategories = selectedStoryType === 'long'
        ? longStoryCategories
        : selectedStoryType === 'short'
            ? shortStoryCategories
            : [...longStoryCategories, ...shortStoryCategories];

    const handleCategoryToggle = (category) => {
        setSelectedCategories(prev =>
            prev.includes(category)
                ? prev.filter(c => c !== category)
                : [...prev, category]
        );
    };

    return (
        <div style={{
            position: 'sticky',
            top: '80px',
            backgroundColor: '#ffffff',
            borderRadius: '12px',
            padding: '1.5rem',
            border: '1px solid #e2e8f0'
        }}>
            {/* Header */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    <SlidersHorizontal style={{ width: '20px', height: '20px', color: '#13ec5b' }} />
                    <h3 style={{ fontSize: '1rem', fontWeight: 'bold', color: '#1e293b', margin: 0 }}>
                        Bộ lọc
                    </h3>
                    {activeFiltersCount > 0 && (
                        <span style={{
                            padding: '0.125rem 0.5rem',
                            backgroundColor: '#13ec5b',
                            color: '#ffffff',
                            fontSize: '0.75rem',
                            fontWeight: 600,
                            borderRadius: '9999px'
                        }}>
                            {activeFiltersCount}
                        </span>
                    )}
                </div>
                {activeFiltersCount > 0 && (
                    <button
                        onClick={clearAllFilters}
                        style={{
                            fontSize: '0.75rem',
                            color: '#ef4444',
                            fontWeight: 600,
                            background: 'none',
                            border: 'none',
                            cursor: 'pointer'
                        }}
                    >
                        Xóa tất cả
                    </button>
                )}
            </div>

            {/* Story Type */}
            <div style={{ marginBottom: '1.5rem' }}>
                <h4 style={{ fontSize: '0.875rem', fontWeight: 600, color: '#64748b', marginBottom: '0.75rem', textTransform: 'uppercase' }}>
                    Loại truyện
                </h4>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                    {[
                        { value: 'all', label: 'Tất cả' },
                        { value: 'long', label: 'Truyện dài' },
                        { value: 'short', label: 'Truyện ngắn' }
                    ].map(type => (
                        <button
                            key={type.value}
                            onClick={() => {
                                setSelectedStoryType(type.value);
                                setSelectedCategories([]);
                            }}
                            style={{
                                padding: '0.625rem 1rem',
                                textAlign: 'left',
                                fontSize: '0.875rem',
                                fontWeight: selectedStoryType === type.value ? 600 : 400,
                                backgroundColor: selectedStoryType === type.value ? '#d4fce3' : 'transparent',
                                color: selectedStoryType === type.value ? '#065f46' : '#475569',
                                border: 'none',
                                borderRadius: '0.5rem',
                                cursor: 'pointer',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                if (selectedStoryType !== type.value) {
                                    e.currentTarget.style.backgroundColor = '#f8fafc';
                                }
                            }}
                            onMouseLeave={(e) => {
                                if (selectedStoryType !== type.value) {
                                    e.currentTarget.style.backgroundColor = 'transparent';
                                }
                            }}
                        >
                            {type.label}
                        </button>
                    ))}
                </div>
            </div>

            {/* Categories */}
            <div style={{ marginBottom: '1.5rem' }}>
                <h4 style={{ fontSize: '0.875rem', fontWeight: 600, color: '#64748b', marginBottom: '0.75rem', textTransform: 'uppercase' }}>
                    Thể loại
                </h4>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', maxHeight: '300px', overflowY: 'auto' }}>
                    {availableCategories.map(category => (
                        <label
                            key={category}
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.5rem',
                                padding: '0.5rem',
                                fontSize: '0.875rem',
                                cursor: 'pointer',
                                borderRadius: '0.375rem',
                                transition: 'background-color 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                e.currentTarget.style.backgroundColor = '#f8fafc';
                            }}
                            onMouseLeave={(e) => {
                                e.currentTarget.style.backgroundColor = 'transparent';
                            }}
                        >
                            <input
                                type="checkbox"
                                checked={selectedCategories.includes(category)}
                                onChange={() => handleCategoryToggle(category)}
                                style={{
                                    width: '16px',
                                    height: '16px',
                                    accentColor: '#13ec5b',
                                    cursor: 'pointer'
                                }}
                            />
                            <span style={{ color: '#475569' }}>{category}</span>
                        </label>
                    ))}
                </div>
            </div>

            {/* Status */}
            <div style={{ marginBottom: '1.5rem' }}>
                <h4 style={{ fontSize: '0.875rem', fontWeight: 600, color: '#64748b', marginBottom: '0.75rem', textTransform: 'uppercase' }}>
                    Trạng thái
                </h4>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                    {[
                        { value: 'all', label: 'Tất cả' },
                        { value: 'ongoing', label: 'Đang ra' },
                        { value: 'completed', label: 'Hoàn thành' }
                    ].map(status => (
                        <button
                            key={status.value}
                            onClick={() => setSelectedStatus(status.value)}
                            style={{
                                padding: '0.625rem 1rem',
                                textAlign: 'left',
                                fontSize: '0.875rem',
                                fontWeight: selectedStatus === status.value ? 600 : 400,
                                backgroundColor: selectedStatus === status.value ? '#d4fce3' : 'transparent',
                                color: selectedStatus === status.value ? '#065f46' : '#475569',
                                border: 'none',
                                borderRadius: '0.5rem',
                                cursor: 'pointer',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                if (selectedStatus !== status.value) {
                                    e.currentTarget.style.backgroundColor = '#f8fafc';
                                }
                            }}
                            onMouseLeave={(e) => {
                                if (selectedStatus !== status.value) {
                                    e.currentTarget.style.backgroundColor = 'transparent';
                                }
                            }}
                        >
                            {status.label}
                        </button>
                    ))}
                </div>
            </div>

            {/* Age Rating */}
            <div>
                <h4 style={{ fontSize: '0.875rem', fontWeight: 600, color: '#64748b', marginBottom: '0.75rem', textTransform: 'uppercase' }}>
                    Độ tuổi
                </h4>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                    {[
                        { value: 'all', label: 'Tất cả' },
                        { value: 'all-ages', label: 'Mọi lứa tuổi' },
                        { value: '16+', label: '16+' },
                        { value: '18+', label: '18+' }
                    ].map(rating => (
                        <button
                            key={rating.value}
                            onClick={() => setSelectedAgeRating(rating.value)}
                            style={{
                                padding: '0.625rem 1rem',
                                textAlign: 'left',
                                fontSize: '0.875rem',
                                fontWeight: selectedAgeRating === rating.value ? 600 : 400,
                                backgroundColor: selectedAgeRating === rating.value ? '#d4fce3' : 'transparent',
                                color: selectedAgeRating === rating.value ? '#065f46' : '#475569',
                                border: 'none',
                                borderRadius: '0.5rem',
                                cursor: 'pointer',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                if (selectedAgeRating !== rating.value) {
                                    e.currentTarget.style.backgroundColor = '#f8fafc';
                                }
                            }}
                            onMouseLeave={(e) => {
                                if (selectedAgeRating !== rating.value) {
                                    e.currentTarget.style.backgroundColor = 'transparent';
                                }
                            }}
                        >
                            {rating.label}
                        </button>
                    ))}
                </div>
            </div>
        </div>
    );
}
