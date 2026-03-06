
import { Eye, Star } from 'lucide-react';

export function StoryCard({ story }) {
    return (
        <div
            style={{
                backgroundColor: '#ffffff',
                borderRadius: '12px',
                overflow: 'hidden',
                border: '1px solid #e2e8f0',
                transition: 'all 0.3s',
                cursor: 'pointer'
            }}
            onMouseEnter={(e) => {
                e.currentTarget.style.transform = 'translateY(-4px)';
                e.currentTarget.style.boxShadow = '0 12px 24px rgba(0,0,0,0.1)';
                e.currentTarget.style.borderColor = '#13ec5b';
            }}
            onMouseLeave={(e) => {
                e.currentTarget.style.transform = 'translateY(0)';
                e.currentTarget.style.boxShadow = 'none';
                e.currentTarget.style.borderColor = '#e2e8f0';
            }}
        >
            {/* Cover */}
            <div style={{ position: 'relative', paddingBottom: '140%', backgroundColor: '#f1f5f9' }}>
                <img
                    src={story.cover}
                    alt={story.title}
                    style={{
                        position: 'absolute',
                        top: 0,
                        left: 0,
                        width: '100%',
                        height: '100%',
                        objectFit: 'cover'
                    }}
                />
                {/* Status Badge */}
                <div style={{
                    position: 'absolute',
                    top: '0.5rem',
                    right: '0.5rem',
                    padding: '0.25rem 0.5rem',
                    backgroundColor: story.status === 'completed' ? '#10b981' : '#f59e0b',
                    color: '#ffffff',
                    fontSize: '0.625rem',
                    fontWeight: 600,
                    borderRadius: '0.25rem',
                    textTransform: 'uppercase'
                }}>
                    {story.status === 'completed' ? 'Full' : 'Ongoing'}
                </div>
            </div>

            {/* Info */}
            <div style={{ padding: '0.75rem' }}>
                <h4 style={{
                    fontSize: '0.875rem',
                    fontWeight: 600,
                    color: '#1e293b',
                    marginBottom: '0.25rem',
                    overflow: 'hidden',
                    textOverflow: 'ellipsis',
                    display: '-webkit-box',
                    WebkitLineClamp: 2,
                    WebkitBoxOrient: 'vertical',
                    lineHeight: '1.4',
                    minHeight: '2.45rem'
                }}>
                    {story.title}
                </h4>
                <p style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.5rem' }}>
                    {story.author}
                </p>

                {/* Stats */}
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', fontSize: '0.75rem', color: '#64748b' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem' }}>
                        <Eye style={{ width: '12px', height: '12px' }} />
                        <span>{(story.views / 1000).toFixed(0)}K</span>
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem' }}>
                        <Star style={{ width: '12px', height: '12px', fill: '#fbbf24', color: '#fbbf24' }} />
                        <span>{story.rating}</span>
                    </div>
                </div>
            </div>
        </div>
    );
}
