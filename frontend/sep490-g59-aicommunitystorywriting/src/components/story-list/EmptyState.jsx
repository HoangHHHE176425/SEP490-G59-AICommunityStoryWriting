import { browseUi as u } from './storyBrowseUi';

export function EmptyState({ onClearFilters }) {
    return (
        <div
            style={{
                backgroundColor: u.surface,
                borderRadius: u.radius,
                padding: '3.5rem 2rem',
                textAlign: 'center',
                border: `1px solid ${u.border}`,
                boxShadow: u.shadow,
                fontFamily: u.font,
            }}
        >
            <div
                style={{
                    width: 64,
                    height: 64,
                    margin: '0 auto 1.25rem',
                    borderRadius: '50%',
                    backgroundColor: u.accentSoft,
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    fontSize: '1.75rem',
                }}
                aria-hidden
            >
                📚
            </div>
            <h3
                style={{
                    fontSize: '1.125rem',
                    fontWeight: 700,
                    color: u.text,
                    margin: 0,
                    marginBottom: '0.5rem',
                }}
            >
                Không tìm thấy truyện nào
            </h3>
            <p style={{ fontSize: '0.875rem', color: u.textMuted, margin: 0, marginBottom: '1.5rem', maxWidth: 360, marginLeft: 'auto', marginRight: 'auto' }}>
                Thử thay đổi bộ lọc hoặc từ khóa tìm kiếm
            </p>
            <button
                type="button"
                onClick={onClearFilters}
                style={{
                    padding: '0.625rem 1.5rem',
                    backgroundColor: u.accent,
                    color: '#ffffff',
                    fontSize: '0.875rem',
                    fontWeight: 600,
                    fontFamily: u.font,
                    borderRadius: u.radiusSm,
                    border: 'none',
                    cursor: 'pointer',
                    transition: 'background-color 0.15s',
                }}
                onMouseEnter={(e) => {
                    e.currentTarget.style.backgroundColor = u.accentHover;
                }}
                onMouseLeave={(e) => {
                    e.currentTarget.style.backgroundColor = u.accent;
                }}
            >
                Xóa bộ lọc
            </button>
        </div>
    );
}
