import { stripHtmlToText } from '../../../utils/richText';

// Helper function to count words
const countWords = (text) => {
    const plain = stripHtmlToText(text);
    if (!plain) return 0;
    return plain.split(/\s+/).filter(word => word.length > 0).length;
};

export function ChapterList({
    chapters,
    currentChapterIndex,
    onChapterSelect,
    minChapters
}) {
    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            {/* Warning if insufficient chapters */}
            {chapters.length < minChapters && (
                <div style={{
                    padding: '1rem',
                    backgroundColor: '#fef2f2',
                    border: '1px solid #fecaca',
                    borderRadius: '4px',
                    fontSize: '0.875rem',
                    color: '#ef4444',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.5rem'
                }}>
                    <span>⚠️</span>
                    <span>Cần thêm ít nhất {minChapters} chương để có thể đăng bài (Hiện có: {chapters.length})</span>
                </div>
            )}

            {/* Chapter List Overview */}
            <div style={{
                backgroundColor: '#f9fafb',
                border: '1px solid #e5e7eb',
                borderRadius: '4px',
                padding: '1rem'
            }}>
                <h4 style={{ fontSize: '0.875rem', fontWeight: 600, color: '#333333', marginBottom: '0.75rem' }}>
                    Hãy bắt đầu sáng tác chương đầu tiên của bạn
                </h4>
                <div style={{
                    display: 'grid',
                    gap: '0.5rem',
                    maxHeight: '200px',
                    overflowY: 'auto'
                }}>
                    {chapters.map((ch, idx) => (
                        <div
                            key={ch.id}
                            onClick={() => onChapterSelect(idx)}
                            style={{
                                padding: '0.5rem 0.75rem',
                                backgroundColor: idx === currentChapterIndex ? '#f0fdf4' : '#ffffff',
                                border: `1px solid ${idx === currentChapterIndex ? '#13ec5b' : '#e5e7eb'}`,
                                borderRadius: '4px',
                                cursor: 'pointer',
                                transition: 'all 0.2s',
                                display: 'flex',
                                justifyContent: 'space-between',
                                alignItems: 'center'
                            }}
                            onMouseEnter={(e) => {
                                if (idx !== currentChapterIndex) {
                                    e.currentTarget.style.backgroundColor = '#f9fafb';
                                }
                            }}
                            onMouseLeave={(e) => {
                                if (idx !== currentChapterIndex) {
                                    e.currentTarget.style.backgroundColor = '#ffffff';
                                }
                            }}
                        >
                            <div>
                                <span style={{ fontSize: '0.875rem', fontWeight: 600, color: '#333333' }}>
                                    Chương {ch.number}
                                </span>
                                {ch.title && (
                                    <span style={{ fontSize: '0.875rem', color: '#6b7280', marginLeft: '0.5rem' }}>
                                        - {ch.title}
                                    </span>
                                )}
                            </div>
                            <div style={{ fontSize: '0.75rem', color: countWords(ch.content) > 0 ? '#10b981' : '#ef4444' }}>
                                {countWords(ch.content) > 0 ? `${countWords(ch.content)} từ` : 'Chưa có nội dung'}
                            </div>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
}
