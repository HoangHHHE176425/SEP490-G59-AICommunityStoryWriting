import { Link } from 'react-router-dom';
import { Eye, Star } from 'lucide-react';
import { ImageWithFallback } from '../figma/ImageWithFallback';
import { formatStoryViews } from '../../utils/storyBrowseMap';
import { browseUi as u } from './storyBrowseUi';

function statusBadge(story) {
    if (story.status === 'completed') return { text: 'Full', bg: '#10b981' };
    if (story.status === 'hiatus') return { text: 'Hiatus', bg: '#64748b' };
    return { text: 'Ongoing', bg: '#f59e0b' };
}

export function StoryCard({ story }) {
    const badge = statusBadge(story);

    return (
        <Link
            to={`/story/${story.id}`}
            style={{
                textDecoration: 'none',
                color: 'inherit',
                display: 'flex',
                height: '100%',
                minHeight: 0,
            }}
        >
            <div
                style={{
                    flex: 1,
                    display: 'flex',
                    flexDirection: 'column',
                    minWidth: 0,
                    width: '100%',
                    backgroundColor: u.surface,
                    borderRadius: u.radius,
                    overflow: 'hidden',
                    border: `1px solid ${u.border}`,
                    boxShadow: u.shadow,
                    transition: 'transform 0.2s ease, box-shadow 0.2s ease, border-color 0.2s ease',
                    cursor: 'pointer',
                    fontFamily: u.font,
                }}
                onMouseEnter={(e) => {
                    e.currentTarget.style.transform = 'translateY(-3px)';
                    e.currentTarget.style.boxShadow = u.shadowHover;
                    e.currentTarget.style.borderColor = u.accentBorder;
                }}
                onMouseLeave={(e) => {
                    e.currentTarget.style.transform = 'translateY(0)';
                    e.currentTarget.style.boxShadow = u.shadow;
                    e.currentTarget.style.borderColor = u.border;
                }}
            >
                <div
                    style={{
                        position: 'relative',
                        paddingBottom: '140%',
                        backgroundColor: u.surfaceMuted,
                        flexShrink: 0,
                        width: '100%',
                    }}
                >
                    <ImageWithFallback
                        src={story.cover}
                        alt={story.title}
                        className="absolute inset-0 h-full w-full object-cover"
                        style={{
                            position: 'absolute',
                            top: 0,
                            left: 0,
                            width: '100%',
                            height: '100%',
                            objectFit: 'cover',
                        }}
                    />
                    <div
                        style={{
                            position: 'absolute',
                            top: '0.5rem',
                            right: '0.5rem',
                            padding: '0.25rem 0.5rem',
                            backgroundColor: badge.bg,
                            color: '#ffffff',
                            fontSize: '0.625rem',
                            fontWeight: 600,
                            borderRadius: '0.25rem',
                            textTransform: 'uppercase',
                        }}
                    >
                        {badge.text}
                    </div>
                </div>

                <div
                    style={{
                        flex: 1,
                        display: 'flex',
                        flexDirection: 'column',
                        padding: '0.75rem',
                        minHeight: 0,
                        gap: '0.35rem',
                    }}
                >
                    <h4
                        style={{
                            fontSize: '0.875rem',
                            fontWeight: 600,
                            color: u.text,
                            margin: 0,
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            display: '-webkit-box',
                            WebkitLineClamp: 2,
                            WebkitBoxOrient: 'vertical',
                            lineHeight: '1.4',
                            minHeight: '2.45rem',
                        }}
                    >
                        {story.title}
                    </h4>
                    <p
                        title={story.author}
                        style={{
                            fontSize: '0.75rem',
                            color: u.textMuted,
                            margin: 0,
                            lineHeight: 1.35,
                            overflow: 'hidden',
                            textOverflow: 'ellipsis',
                            whiteSpace: 'nowrap',
                            minHeight: '1.0125rem',
                        }}
                    >
                        {story.author}
                    </p>

                    <div
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '0.75rem',
                            fontSize: '0.75rem',
                            color: u.textMuted,
                            marginTop: 'auto',
                            paddingTop: '0.35rem',
                        }}
                    >
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem' }}>
                            <Eye style={{ width: '12px', height: '12px' }} />
                            <span>{formatStoryViews(story.views)}</span>
                        </div>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem' }}>
                            <Star style={{ width: '12px', height: '12px', fill: '#fbbf24', color: '#fbbf24' }} />
                            <span>{story.rating}</span>
                        </div>
                    </div>
                </div>
            </div>
        </Link>
    );
}
