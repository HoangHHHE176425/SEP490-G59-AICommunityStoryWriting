import { Link } from 'react-router-dom';
import { Eye, Heart, BookOpen, Star } from 'lucide-react';
import { ImageWithFallback } from '../figma/ImageWithFallback';
import { formatStoryViews, formatStoryFollows } from '../../utils/storyBrowseMap';
import { browseUi as u } from './storyBrowseUi';

function statusPill(story) {
    if (story.status === 'completed') {
        return { label: 'Hoàn thành', bg: '#d1fae5', color: '#065f46' };
    }
    if (story.status === 'hiatus') {
        return { label: 'Tạm dừng', bg: '#e2e8f0', color: '#475569' };
    }
    return { label: 'Đang ra', bg: '#fef3c7', color: '#92400e' };
}

export function StoryListItem({ story }) {
    const pill = statusPill(story);

    return (
        <Link to={`/story/${story.id}`} style={{ textDecoration: 'none', color: 'inherit', display: 'block' }}>
            <div
                style={{
                    backgroundColor: u.surface,
                    borderRadius: u.radius,
                    padding: '1.25rem',
                    border: `1px solid ${u.border}`,
                    boxShadow: u.shadow,
                    display: 'flex',
                    gap: '1.25rem',
                    transition: 'border-color 0.2s, box-shadow 0.2s',
                    cursor: 'pointer',
                    fontFamily: u.font,
                }}
                onMouseEnter={(e) => {
                    e.currentTarget.style.borderColor = u.accentBorder;
                    e.currentTarget.style.boxShadow = u.shadowHover;
                }}
                onMouseLeave={(e) => {
                    e.currentTarget.style.borderColor = u.border;
                    e.currentTarget.style.boxShadow = u.shadow;
                }}
            >
                <ImageWithFallback
                    src={story.cover}
                    alt={story.title}
                    style={{
                        width: '100px',
                        height: '140px',
                        objectFit: 'cover',
                        borderRadius: u.radiusSm,
                        flexShrink: 0,
                    }}
                />

                <div style={{ flex: 1, minWidth: 0 }}>
                    <div
                        style={{
                            display: 'flex',
                            justifyContent: 'space-between',
                            alignItems: 'start',
                            marginBottom: '0.5rem',
                            flexWrap: 'wrap',
                            gap: '0.5rem',
                        }}
                    >
                        <div style={{ flex: 1, minWidth: 0 }}>
                            <h4
                                style={{
                                    fontSize: '1rem',
                                    fontWeight: 600,
                                    color: '#1e293b',
                                    marginBottom: '0.25rem',
                                    margin: 0,
                                }}
                            >
                                {story.title}
                            </h4>
                            <p
                                style={{
                                    fontSize: '0.875rem',
                                    color: '#64748b',
                                    marginBottom: '0.5rem',
                                    margin: 0,
                                    marginTop: '0.25rem',
                                }}
                            >
                                {story.author}
                            </p>
                        </div>
                        <span
                            style={{
                                padding: '0.25rem 0.75rem',
                                backgroundColor: pill.bg,
                                color: pill.color,
                                fontSize: '0.75rem',
                                fontWeight: 600,
                                borderRadius: '9999px',
                                whiteSpace: 'nowrap',
                            }}
                        >
                            {pill.label}
                        </span>
                    </div>

                    <p
                        style={{
                            fontSize: '0.875rem',
                            color: u.textMuted,
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            display: '-webkit-box',
                            WebkitLineClamp: 2,
                            WebkitBoxOrient: 'vertical',
                            margin: 0,
                            marginBottom: '0.75rem',
                        }}
                    >
                        {story.description || '—'}
                    </p>

                    <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.75rem', flexWrap: 'wrap' }}>
                        {(story.categories || []).map((cat) => {
                            const key = typeof cat === 'object' && cat?.id ? cat.id : cat;
                            const label = typeof cat === 'object' && cat?.name ? cat.name : String(cat);
                            return (
                                <span
                                    key={key}
                                    style={{
                                        padding: '0.25rem 0.5rem',
                                        backgroundColor: u.surfaceMuted,
                                        color: u.textSecondary,
                                        fontSize: '0.75rem',
                                        borderRadius: u.radiusSm,
                                        border: `1px solid ${u.border}`,
                                    }}
                                >
                                    {label}
                                </span>
                            );
                        })}
                    </div>

                    <div
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '1.5rem',
                            fontSize: '0.875rem',
                            color: u.textMuted,
                            flexWrap: 'wrap',
                        }}
                    >
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                            <BookOpen style={{ width: '14px', height: '14px' }} />
                            <span>{story.chapters} chương</span>
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                            <Eye style={{ width: '14px', height: '14px' }} />
                            <span>{formatStoryViews(story.views)} lượt xem</span>
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                            <Heart style={{ width: '14px', height: '14px' }} />
                            <span>{formatStoryFollows(story.follows)} yêu thích</span>
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                            <Star style={{ width: '14px', height: '14px', fill: '#fbbf24', color: '#fbbf24' }} />
                            <span>{story.rating}</span>
                        </div>
                    </div>
                </div>
            </div>
        </Link>
    );
}
