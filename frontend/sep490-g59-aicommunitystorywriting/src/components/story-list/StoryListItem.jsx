import { Eye, Heart, BookOpen, Star } from 'lucide-react';

export function StoryListItem({ story }) {
    return (
        <div
            style={{
                backgroundColor: '#ffffff',
                borderRadius: '12px',
                padding: '1.25rem',
                border: '1px solid #e2e8f0',
                display: 'flex',
                gap: '1.25rem',
                transition: 'all 0.2s',
                cursor: 'pointer'
            }}
            onMouseEnter={(e) => {
                e.currentTarget.style.borderColor = '#13ec5b';
                e.currentTarget.style.boxShadow = '0 4px 12px rgba(19, 236, 91, 0.1)';
            }}
            onMouseLeave={(e) => {
                e.currentTarget.style.borderColor = '#e2e8f0';
                e.currentTarget.style.boxShadow = 'none';
            }}
        >
            {/* Cover */}
            <img
                src={story.cover}
                alt={story.title}
                style={{
                    width: '100px',
                    height: '140px',
                    objectFit: 'cover',
                    borderRadius: '8px',
                    flexShrink: 0
                }}
            />

            {/* Content */}
            <div style={{ flex: 1, minWidth: 0 }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', marginBottom: '0.5rem', flexWrap: 'wrap', gap: '0.5rem' }}>
                    <div style={{ flex: 1, minWidth: 0 }}>
                        <h4 style={{
                            fontSize: '1rem',
                            fontWeight: 600,
                            color: '#1e293b',
                            marginBottom: '0.25rem',
                            margin: 0
                        }}>
                            {story.title}
                        </h4>
                        <p style={{ fontSize: '0.875rem', color: '#64748b', marginBottom: '0.5rem', margin: 0, marginTop: '0.25rem' }}>
                            {story.author}
                        </p>
                    </div>
                    <span style={{
                        padding: '0.25rem 0.75rem',
                        backgroundColor: story.status === 'completed' ? '#d1fae5' : '#fef3c7',
                        color: story.status === 'completed' ? '#065f46' : '#92400e',
                        fontSize: '0.75rem',
                        fontWeight: 600,
                        borderRadius: '9999px',
                        whiteSpace: 'nowrap'
                    }}>
                        {story.status === 'completed' ? 'Hoàn thành' : 'Đang ra'}
                    </span>
                </div>

                <p style={{
                    fontSize: '0.875rem',
                    color: '#64748b',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                    display: '-webkit-box',
                    WebkitLineClamp: 2,
                    WebkitBoxOrient: 'vertical',
                    margin: 0,
                    marginBottom: '0.75rem',
                }}>
                    {story.description}
                </p>

                {/* Categories */}
                <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.75rem', flexWrap: 'wrap' }}>
                    {story.categories.map(cat => (
                        <span
                            key={cat}
                            style={{
                                padding: '0.25rem 0.5rem',
                                backgroundColor: '#f8fafc',
                                color: '#475569',
                                fontSize: '0.75rem',
                                borderRadius: '0.25rem',
                                border: '1px solid #e2e8f0'
                            }}
                        >
                            {cat}
                        </span>
                    ))}
                </div>

                {/* Stats */}
                <div style={{ display: 'flex', alignItems: 'center', gap: '1.5rem', fontSize: '0.875rem', color: '#64748b', flexWrap: 'wrap' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                        <BookOpen style={{ width: '14px', height: '14px' }} />
                        <span>{story.chapters} chương</span>
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                        <Eye style={{ width: '14px', height: '14px' }} />
                        <span>{(story.views / 1000).toFixed(0)}K lượt xem</span>
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                        <Heart style={{ width: '14px', height: '14px' }} />
                        <span>{(story.follows / 1000).toFixed(1)}K theo dõi</span>
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                        <Star style={{ width: '14px', height: '14px', fill: '#fbbf24', color: '#fbbf24' }} />
                        <span>{story.rating}</span>
                    </div>
                </div>
            </div>
        </div>
    );
}
