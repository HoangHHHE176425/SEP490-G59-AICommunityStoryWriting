export function ChapterSidebar({ show, chapters, currentChapter, onClose, onChapterSelect }) {
    if (!show) return null;

    return (
        <>
            {/* Backdrop */}
            <div
                onClick={onClose}
                style={{
                    position: 'fixed',
                    top: 0,
                    left: 0,
                    right: 0,
                    bottom: 0,
                    backgroundColor: 'rgba(0, 0, 0, 0.5)',
                    zIndex: 110
                }}
            />

            {/* Sidebar */}
            <div
                style={{
                    position: 'fixed',
                    top: 0,
                    right: 0,
                    bottom: 0,
                    width: '100%',
                    maxWidth: '400px',
                    backgroundColor: '#ffffff',
                    zIndex: 120,
                    overflowY: 'auto',
                    boxShadow: '-4px 0 12px rgba(0, 0, 0, 0.15)'
                }}
            >
                {/* Header */}
                <div style={{ padding: '1.5rem', borderBottom: '1px solid #e2e8f0', display: 'flex', justifyContent: 'space-between', alignItems: 'center', position: 'sticky', top: 0, backgroundColor: '#ffffff', zIndex: 10 }}>
                    <h3 style={{ fontSize: '1.125rem', fontWeight: 'bold', color: '#1e293b', margin: 0 }}>
                        Danh sách chương
                    </h3>
                    <button
                        onClick={onClose}
                        style={{
                            padding: '0.5rem',
                            backgroundColor: 'transparent',
                            border: 'none',
                            borderRadius: '0.375rem',
                            cursor: 'pointer',
                            fontSize: '1.5rem',
                            color: '#64748b',
                            lineHeight: 1
                        }}
                    >
                        ×
                    </button>
                </div>

                {/* Chapter List */}
                <div style={{ padding: '1rem' }}>
                    {chapters.map((ch) => (
                        <button
                            key={ch.number}
                            onClick={() => !ch.isLocked && onChapterSelect(ch)}
                            style={{
                                width: '100%',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'space-between',
                                padding: '0.75rem 1rem',
                                backgroundColor: ch.number === currentChapter ? 'rgba(19, 236, 91, 0.1)' : 'transparent',
                                border: 'none',
                                borderBottom: '1px solid #f1f5f9',
                                textAlign: 'left',
                                cursor: ch.isLocked ? 'not-allowed' : 'pointer',
                                transition: 'background-color 0.2s',
                                opacity: ch.isLocked ? 0.5 : 1
                            }}
                            onMouseEnter={(e) => {
                                if (!ch.isLocked && ch.number !== currentChapter) {
                                    e.currentTarget.style.backgroundColor = '#f8fafc';
                                }
                            }}
                            onMouseLeave={(e) => {
                                if (!ch.isLocked && ch.number !== currentChapter) {
                                    e.currentTarget.style.backgroundColor = 'transparent';
                                }
                            }}
                        >
                            <div>
                                <p style={{ fontSize: '0.875rem', fontWeight: ch.number === currentChapter ? 'bold' : 'normal', color: ch.number === currentChapter ? '#13ec5b' : '#1e293b', margin: 0 }}>
                                    Chương {ch.number}
                                </p>
                                <p style={{ fontSize: '0.75rem', color: '#64748b', margin: '0.125rem 0 0 0' }}>
                                    {ch.title}
                                </p>
                            </div>
                            {ch.isLocked && (
                                <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>🔒</span>
                            )}
                        </button>
                    ))}
                </div>
            </div>
        </>
    );
}
