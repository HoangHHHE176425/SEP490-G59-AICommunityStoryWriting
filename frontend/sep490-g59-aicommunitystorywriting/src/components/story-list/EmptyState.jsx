/* eslint-disable no-dupe-keys */
export function EmptyState({ onClearFilters }) {
    return (
        <div style={{
            backgroundColor: '#ffffff',
            borderRadius: '12px',
            padding: '4rem 2rem',
            textAlign: 'center',
            border: '1px solid #e2e8f0'
        }}>
            <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>📚</div>
            <h3 style={{ fontSize: '1.25rem', fontWeight: 600, color: '#1e293b', marginBottom: '0.5rem', margin: 0, marginBottom: '0.5rem' }}>
                Không tìm thấy truyện nào
            </h3>
            <p style={{ fontSize: '0.875rem', color: '#64748b', marginBottom: '1.5rem', margin: 0, marginBottom: '1.5rem' }}>
                Thử thay đổi bộ lọc hoặc từ khóa tìm kiếm
            </p>
            <button
                onClick={onClearFilters}
                style={{
                    padding: '0.625rem 1.5rem',
                    backgroundColor: '#13ec5b',
                    color: '#ffffff',
                    fontSize: '0.875rem',
                    fontWeight: 700,
                    borderRadius: '9999px',
                    border: 'none',
                    cursor: 'pointer',
                    transition: 'background-color 0.2s'
                }}
                onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#10d954'}
                onMouseLeave={(e) => e.currentTarget.style.backgroundColor = '#13ec5b'}
            >
                Xóa bộ lọc
            </button>
        </div>
    );
}
