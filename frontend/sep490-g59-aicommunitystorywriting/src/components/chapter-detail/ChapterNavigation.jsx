import { ChevronLeft, ChevronRight } from 'lucide-react';

export function ChapterNavigation({
    currentChapter,
    totalChapters,
    onPrevChapter,
    onNextChapter
}) {
    const isFirstChapter = currentChapter === 1;
    const isLastChapter = currentChapter === totalChapters;

    return (
        <div style={{ maxWidth: '800px', margin: '0 auto', padding: '0 1.5rem 3rem' }}>
            <div style={{ display: 'flex', gap: '1rem' }}>
                <button
                    onClick={onPrevChapter}
                    disabled={isFirstChapter}
                    style={{
                        flex: 1,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        gap: '0.5rem',
                        padding: '1rem',
                        backgroundColor: isFirstChapter ? '#f1f5f9' : '#ffffff',
                        border: '1px solid #e2e8f0',
                        borderRadius: '0.75rem',
                        fontSize: '0.875rem',
                        fontWeight: 600,
                        color: isFirstChapter ? '#94a3b8' : '#1e293b',
                        cursor: isFirstChapter ? 'not-allowed' : 'pointer',
                        transition: 'all 0.2s'
                    }}
                    onMouseEnter={(e) => {
                        if (!isFirstChapter) {
                            e.currentTarget.style.backgroundColor = '#f8fafc';
                            e.currentTarget.style.borderColor = '#13ec5b';
                        }
                    }}
                    onMouseLeave={(e) => {
                        if (!isFirstChapter) {
                            e.currentTarget.style.backgroundColor = '#ffffff';
                            e.currentTarget.style.borderColor = '#e2e8f0';
                        }
                    }}
                >
                    <ChevronLeft style={{ width: '20px', height: '20px' }} />
                    Chương trước
                </button>

                <button
                    onClick={onNextChapter}
                    disabled={isLastChapter}
                    style={{
                        flex: 1,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        gap: '0.5rem',
                        padding: '1rem',
                        backgroundColor: isLastChapter ? '#f1f5f9' : '#13ec5b',
                        border: 'none',
                        borderRadius: '0.75rem',
                        fontSize: '0.875rem',
                        fontWeight: 600,
                        color: isLastChapter ? '#94a3b8' : '#ffffff',
                        cursor: isLastChapter ? 'not-allowed' : 'pointer',
                        transition: 'all 0.2s'
                    }}
                    onMouseEnter={(e) => {
                        if (!isLastChapter) {
                            e.currentTarget.style.backgroundColor = '#10d352';
                        }
                    }}
                    onMouseLeave={(e) => {
                        if (!isLastChapter) {
                            e.currentTarget.style.backgroundColor = '#13ec5b';
                        }
                    }}
                >
                    Chương tiếp theo
                    <ChevronRight style={{ width: '20px', height: '20px' }} />
                </button>
            </div>
        </div>
    );
}
