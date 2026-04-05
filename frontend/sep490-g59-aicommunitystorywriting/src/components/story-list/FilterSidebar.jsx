import { SlidersHorizontal } from 'lucide-react';
import { AUTHOR_PRESET_SIDEBAR_OPTIONS } from '../../constants/storyBrowsePresets';
import { browseUi as u } from './storyBrowseUi';

const sectionTitle = {
    fontSize: '0.6875rem',
    fontWeight: 700,
    letterSpacing: '0.08em',
    color: u.textSubtle,
    marginBottom: '0.625rem',
    textTransform: 'uppercase',
    fontFamily: u.font,
};

function filterButtonStyle(active) {
    return {
        padding: '0.5625rem 0.75rem',
        textAlign: 'left',
        fontSize: '0.8125rem',
        fontWeight: active ? 600 : 500,
        fontFamily: u.font,
        color: active ? u.accentText : u.textSecondary,
        backgroundColor: active ? u.accentSoftStrong : 'transparent',
        border: `1px solid ${active ? u.accentBorder : u.border}`,
        borderRadius: u.radiusSm,
        cursor: 'pointer',
        transition: 'background-color 0.15s, border-color 0.15s, color 0.15s',
    };
}

/**
 * @param {Object} props
 * @param {string} props.authorPreset
 * @param {(value: string) => void} props.onAuthorPresetChange
 * @param {{ id: string, name: string }[]} props.categories
 * @param {string[]} props.selectedCategoryIds
 */
export function FilterSidebar({
    authorPreset = '',
    onAuthorPresetChange,
    categories = [],
    selectedCategoryIds,
    setSelectedCategoryIds,
    selectedStatus,
    setSelectedStatus,
    selectedAgeRating = 'all',
    setSelectedAgeRating,
    selectedChapterScale = 'all',
    setSelectedChapterScale,
    selectedAiUsage = 'all',
    setSelectedAiUsage,
    activeFiltersCount,
    clearAllFilters,
}) {
    const handleCategoryToggle = (categoryId) => {
        setSelectedCategoryIds((prev) =>
            prev.includes(categoryId) ? prev.filter((c) => c !== categoryId) : [...prev, categoryId]
        );
    };

    return (
        <div
            style={{
                position: 'sticky',
                top: '80px',
                backgroundColor: u.surface,
                borderRadius: u.radius,
                padding: '1.25rem',
                border: `1px solid ${u.border}`,
                boxShadow: u.shadow,
                fontFamily: u.font,
            }}
        >
            <div
                style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    marginBottom: '1.125rem',
                    paddingBottom: '1rem',
                    borderBottom: `1px solid ${u.border}`,
                }}
            >
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    <div
                        style={{
                            width: '36px',
                            height: '36px',
                            borderRadius: u.radiusSm,
                            backgroundColor: u.accentSoft,
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'center',
                        }}
                    >
                        <SlidersHorizontal style={{ width: 18, height: 18, color: u.accent }} />
                    </div>
                    <div>
                        <h3 style={{ fontSize: '0.9375rem', fontWeight: 700, color: u.text, margin: 0, lineHeight: 1.2 }}>
                            Bộ lọc
                        </h3>
                    </div>
                    {activeFiltersCount > 0 && (
                        <span
                            style={{
                                minWidth: '1.25rem',
                                height: '1.25rem',
                                padding: '0 0.375rem',
                                backgroundColor: u.accent,
                                color: '#fff',
                                fontSize: '0.6875rem',
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
                </div>
                {activeFiltersCount > 0 && (
                    <button
                        type="button"
                        onClick={clearAllFilters}
                        style={{
                            fontSize: '0.75rem',
                            color: u.danger,
                            fontWeight: 600,
                            background: 'none',
                            border: 'none',
                            cursor: 'pointer',
                            fontFamily: u.font,
                        }}
                    >
                        Xóa tất cả
                    </button>
                )}
            </div>

            <div style={{ marginBottom: '1.25rem' }}>
                <h4 style={{ ...sectionTitle }}>Ưu tiên tác giả</h4>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.375rem' }}>
                    {AUTHOR_PRESET_SIDEBAR_OPTIONS.map((opt) => {
                        const active = authorPreset === opt.value;
                        return (
                            <button
                                key={opt.value || 'none'}
                                type="button"
                                onClick={() => onAuthorPresetChange(opt.value)}
                                style={filterButtonStyle(active)}
                                onMouseEnter={(e) => {
                                    if (!active) {
                                        e.currentTarget.style.backgroundColor = u.surfaceMuted;
                                        e.currentTarget.style.borderColor = u.borderStrong;
                                    }
                                }}
                                onMouseLeave={(e) => {
                                    if (!active) {
                                        e.currentTarget.style.backgroundColor = 'transparent';
                                        e.currentTarget.style.borderColor = u.border;
                                    }
                                }}
                            >
                                {opt.label}
                            </button>
                        );
                    })}
                </div>
            </div>

            <div
                style={{
                    marginBottom: '1.25rem',
                    paddingTop: '1rem',
                    borderTop: `1px solid ${u.border}`,
                }}
            >
                <h4 style={{ ...sectionTitle }}>Thể loại</h4>
                {categories.length === 0 ? (
                    <p style={{ fontSize: '0.8125rem', color: u.textSubtle, margin: 0 }}>Đang tải thể loại…</p>
                ) : (
                    <div
                        style={{
                            display: 'flex',
                            flexDirection: 'column',
                            gap: '0.25rem',
                            maxHeight: '280px',
                            overflowY: 'auto',
                            paddingRight: '0.25rem',
                        }}
                    >
                        {categories.map((c) => {
                            const checked = selectedCategoryIds.includes(c.id);
                            return (
                                <label
                                    key={c.id}
                                    style={{
                                        display: 'flex',
                                        alignItems: 'center',
                                        gap: '0.5rem',
                                        padding: '0.4375rem 0.5rem',
                                        fontSize: '0.8125rem',
                                        cursor: 'pointer',
                                        borderRadius: u.radiusSm,
                                        transition: 'background-color 0.15s',
                                        color: u.textSecondary,
                                        fontFamily: u.font,
                                    }}
                                    onMouseEnter={(e) => {
                                        e.currentTarget.style.backgroundColor = u.surfaceMuted;
                                    }}
                                    onMouseLeave={(e) => {
                                        e.currentTarget.style.backgroundColor = 'transparent';
                                    }}
                                >
                                    <input
                                        type="checkbox"
                                        checked={checked}
                                        onChange={() => handleCategoryToggle(c.id)}
                                        style={{
                                            width: 15,
                                            height: 15,
                                            accentColor: u.accent,
                                            cursor: 'pointer',
                                        }}
                                    />
                                    <span>{c.name}</span>
                                </label>
                            );
                        })}
                    </div>
                )}
            </div>

            <div
                style={{
                    paddingTop: '1rem',
                    borderTop: `1px solid ${u.border}`,
                    marginBottom: '1.25rem',
                }}
            >
                <h4 style={{ ...sectionTitle }}>Trạng thái ra truyện</h4>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.375rem' }}>
                    {[
                        { value: 'all', label: 'Tất cả' },
                        { value: 'ongoing', label: 'Đang ra' },
                        { value: 'completed', label: 'Hoàn thành' },
                        { value: 'hiatus', label: 'Tạm dừng' },
                    ].map((status) => {
                        const active = selectedStatus === status.value;
                        return (
                            <button
                                key={status.value}
                                type="button"
                                onClick={() => setSelectedStatus(status.value)}
                                style={filterButtonStyle(active)}
                                onMouseEnter={(e) => {
                                    if (!active) {
                                        e.currentTarget.style.backgroundColor = u.surfaceMuted;
                                        e.currentTarget.style.borderColor = u.borderStrong;
                                    }
                                }}
                                onMouseLeave={(e) => {
                                    if (!active) {
                                        e.currentTarget.style.backgroundColor = 'transparent';
                                        e.currentTarget.style.borderColor = u.border;
                                    }
                                }}
                            >
                                {status.label}
                            </button>
                        );
                    })}
                </div>
            </div>

            <div
                style={{
                    paddingTop: '1rem',
                    borderTop: `1px solid ${u.border}`,
                    marginBottom: '1.25rem',
                }}
            >
                <h4 style={{ ...sectionTitle }}>Đồng sáng tác AI</h4>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.375rem' }}>
                    {[
                        { value: 'all', label: 'Tất cả' },
                        { value: 'uses_ai', label: 'Có sử dụng AI' },
                        { value: 'no_ai', label: 'Không dùng AI' },
                    ].map((opt) => {
                        const active = selectedAiUsage === opt.value;
                        return (
                            <button
                                key={opt.value}
                                type="button"
                                onClick={() => setSelectedAiUsage(opt.value)}
                                style={filterButtonStyle(active)}
                                onMouseEnter={(e) => {
                                    if (!active) {
                                        e.currentTarget.style.backgroundColor = u.surfaceMuted;
                                        e.currentTarget.style.borderColor = u.borderStrong;
                                    }
                                }}
                                onMouseLeave={(e) => {
                                    if (!active) {
                                        e.currentTarget.style.backgroundColor = 'transparent';
                                        e.currentTarget.style.borderColor = u.border;
                                    }
                                }}
                            >
                                {opt.label}
                            </button>
                        );
                    })}
                </div>
            </div>

            <div style={{ paddingTop: '1rem', borderTop: `1px solid ${u.border}`, marginBottom: '1.25rem' }}>
                <h4 style={{ ...sectionTitle }}>Độ tuổi phù hợp</h4>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.375rem' }}>
                    {[
                        { value: 'all', label: 'Tất cả' },
                        { value: '13+', label: '13+' },
                        { value: '16+', label: '16+' },
                        { value: '18+', label: '18+' },
                    ].map((opt) => {
                        const active = selectedAgeRating === opt.value;
                        return (
                            <button
                                key={opt.value}
                                type="button"
                                onClick={() => setSelectedAgeRating(opt.value)}
                                style={filterButtonStyle(active)}
                                onMouseEnter={(e) => {
                                    if (!active) {
                                        e.currentTarget.style.backgroundColor = u.surfaceMuted;
                                        e.currentTarget.style.borderColor = u.borderStrong;
                                    }
                                }}
                                onMouseLeave={(e) => {
                                    if (!active) {
                                        e.currentTarget.style.backgroundColor = 'transparent';
                                        e.currentTarget.style.borderColor = u.border;
                                    }
                                }}
                            >
                                {opt.label}
                            </button>
                        );
                    })}
                </div>
            </div>

            <div style={{ paddingTop: '1rem', borderTop: `1px solid ${u.border}` }}>
                <h4 style={{ ...sectionTitle }}>Quy mô (số chương)</h4>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.375rem' }}>
                    {[
                        { value: 'all', label: 'Tất cả' },
                        { value: 'under20', label: 'Dưới 20 chương' },
                        { value: '20to99', label: '20 – 99 chương' },
                        { value: '100plus', label: 'Từ 100 chương' },
                    ].map((opt) => {
                        const active = selectedChapterScale === opt.value;
                        return (
                            <button
                                key={opt.value}
                                type="button"
                                onClick={() => setSelectedChapterScale(opt.value)}
                                style={filterButtonStyle(active)}
                                onMouseEnter={(e) => {
                                    if (!active) {
                                        e.currentTarget.style.backgroundColor = u.surfaceMuted;
                                        e.currentTarget.style.borderColor = u.borderStrong;
                                    }
                                }}
                                onMouseLeave={(e) => {
                                    if (!active) {
                                        e.currentTarget.style.backgroundColor = 'transparent';
                                        e.currentTarget.style.borderColor = u.border;
                                    }
                                }}
                            >
                                {opt.label}
                            </button>
                        );
                    })}
                </div>
            </div>
        </div>
    );
}
