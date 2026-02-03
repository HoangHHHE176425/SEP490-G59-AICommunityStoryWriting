import {
    ChevronLeft,
    Home,
    List,
    Settings,
    Bookmark,
    BookmarkCheck,
    Share2
} from 'lucide-react';

export function ChapterNavBar({
    story,
    chapter,
    isBookmarked,
    onBack,
    onHome,
    onToggleChapterList,
    onToggleSettings,
    onToggleBookmark,
    onShare
}) {
    return (
        <div
            style={{
                position: 'sticky',
                top: 0,
                backgroundColor: '#ffffff',
                borderBottom: '1px solid #e2e8f0',
                zIndex: 100,
                boxShadow: '0 1px 3px rgba(0, 0, 0, 0.1)'
            }}
        >
            <div style={{ maxWidth: '1200px', margin: '0 auto', padding: '1rem 1.5rem' }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '1rem', flexWrap: 'wrap' }}>
                    {/* Left: Back & Home */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                        <button
                            onClick={onBack}
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.25rem',
                                padding: '0.5rem 1rem',
                                backgroundColor: 'transparent',
                                border: '1px solid #e2e8f0',
                                borderRadius: '0.5rem',
                                fontSize: '0.875rem',
                                fontWeight: 600,
                                color: '#64748b',
                                cursor: 'pointer',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                e.currentTarget.style.backgroundColor = '#f8fafc';
                                e.currentTarget.style.borderColor = '#13ec5b';
                                e.currentTarget.style.color = '#13ec5b';
                            }}
                            onMouseLeave={(e) => {
                                e.currentTarget.style.backgroundColor = 'transparent';
                                e.currentTarget.style.borderColor = '#e2e8f0';
                                e.currentTarget.style.color = '#64748b';
                            }}
                        >
                            <ChevronLeft style={{ width: '16px', height: '16px' }} />
                            <span className="hidden sm:inline">Quay lại</span>
                        </button>

                        <button
                            onClick={onHome}
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.25rem',
                                padding: '0.5rem 1rem',
                                backgroundColor: 'transparent',
                                border: '1px solid #e2e8f0',
                                borderRadius: '0.5rem',
                                fontSize: '0.875rem',
                                fontWeight: 600,
                                color: '#64748b',
                                cursor: 'pointer',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                e.currentTarget.style.backgroundColor = '#f8fafc';
                                e.currentTarget.style.borderColor = '#13ec5b';
                                e.currentTarget.style.color = '#13ec5b';
                            }}
                            onMouseLeave={(e) => {
                                e.currentTarget.style.backgroundColor = 'transparent';
                                e.currentTarget.style.borderColor = '#e2e8f0';
                                e.currentTarget.style.color = '#64748b';
                            }}
                        >
                            <Home style={{ width: '16px', height: '16px' }} />
                            <span className="hidden sm:inline">Trang chủ</span>
                        </button>
                    </div>

                    {/* Center: Story Info */}
                    <div style={{ flex: '1 1 300px', minWidth: 0 }}>
                        <h1 style={{ fontSize: '1rem', fontWeight: 'bold', color: '#1e293b', margin: 0, overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                            {story.title}
                        </h1>
                        <p style={{ fontSize: '0.75rem', color: '#64748b', margin: '0.125rem 0 0 0' }}>
                            Chương {chapter.number}: {chapter.title}
                        </p>
                    </div>

                    {/* Right: Actions */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                        <button
                            onClick={onToggleChapterList}
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.25rem',
                                padding: '0.5rem',
                                backgroundColor: 'transparent',
                                border: 'none',
                                borderRadius: '0.5rem',
                                color: '#64748b',
                                cursor: 'pointer',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                e.currentTarget.style.backgroundColor = '#f8fafc';
                                e.currentTarget.style.color = '#13ec5b';
                            }}
                            onMouseLeave={(e) => {
                                e.currentTarget.style.backgroundColor = 'transparent';
                                e.currentTarget.style.color = '#64748b';
                            }}
                            title="Danh sách chương"
                        >
                            <List style={{ width: '20px', height: '20px' }} />
                        </button>

                        <button
                            onClick={onToggleSettings}
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.25rem',
                                padding: '0.5rem',
                                backgroundColor: 'transparent',
                                border: 'none',
                                borderRadius: '0.5rem',
                                color: '#64748b',
                                cursor: 'pointer',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                e.currentTarget.style.backgroundColor = '#f8fafc';
                                e.currentTarget.style.color = '#13ec5b';
                            }}
                            onMouseLeave={(e) => {
                                e.currentTarget.style.backgroundColor = 'transparent';
                                e.currentTarget.style.color = '#64748b';
                            }}
                            title="Cài đặt"
                        >
                            <Settings style={{ width: '20px', height: '20px' }} />
                        </button>

                        <button
                            onClick={onToggleBookmark}
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.25rem',
                                padding: '0.5rem',
                                backgroundColor: 'transparent',
                                border: 'none',
                                borderRadius: '0.5rem',
                                color: isBookmarked ? '#13ec5b' : '#64748b',
                                cursor: 'pointer',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                e.currentTarget.style.backgroundColor = '#f8fafc';
                            }}
                            onMouseLeave={(e) => {
                                e.currentTarget.style.backgroundColor = 'transparent';
                            }}
                            title={isBookmarked ? 'Đã đánh dấu' : 'Đánh dấu'}
                        >
                            {isBookmarked ? (
                                <BookmarkCheck style={{ width: '20px', height: '20px' }} />
                            ) : (
                                <Bookmark style={{ width: '20px', height: '20px' }} />
                            )}
                        </button>

                        <button
                            onClick={onShare}
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.25rem',
                                padding: '0.5rem',
                                backgroundColor: 'transparent',
                                border: 'none',
                                borderRadius: '0.5rem',
                                color: '#64748b',
                                cursor: 'pointer',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                e.currentTarget.style.backgroundColor = '#f8fafc';
                                e.currentTarget.style.color = '#13ec5b';
                            }}
                            onMouseLeave={(e) => {
                                e.currentTarget.style.backgroundColor = 'transparent';
                                e.currentTarget.style.color = '#64748b';
                            }}
                            title="Chia sẻ"
                        >
                            <Share2 style={{ width: '20px', height: '20px' }} />
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}
