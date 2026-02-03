/* eslint-disable no-dupe-keys */
import { Clock, CheckCircle, XCircle, Eye, FileText, BookOpen } from 'lucide-react';

export function PublicationList({ publications, onViewDetail }) {
    const getStatusBadge = (status) => {
        const configs = {
            pending: {
                bg: '#fff3cd',
                color: '#856404',
                icon: Clock,
                label: 'Chờ duyệt'
            },
            approved: {
                bg: '#d1fae5',
                color: '#065f46',
                icon: CheckCircle,
                label: 'Đã duyệt'
            },
            rejected: {
                bg: '#fee2e2',
                color: '#991b1b',
                icon: XCircle,
                label: 'Từ chối'
            }
        };

        const config = configs[status];
        const Icon = config.icon;

        return (
            <div style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '0.375rem',
                padding: '0.375rem 0.75rem',
                backgroundColor: config.bg,
                color: config.color,
                fontSize: '0.75rem',
                fontWeight: 600,
                borderRadius: '9999px'
            }}>
                <Icon style={{ width: '14px', height: '14px' }} />
                {config.label}
            </div>
        );
    };

    const getTypeBadge = (type) => {
        const isNewStory = type === 'new_story';
        return (
            <div style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '0.25rem',
                padding: '0.25rem 0.625rem',
                backgroundColor: isNewStory ? '#e0f2fe' : '#f3e8ff',
                color: isNewStory ? '#075985' : '#6b21a8',
                fontSize: '0.75rem',
                fontWeight: 600,
                borderRadius: '0.375rem'
            }}>
                {isNewStory ? <BookOpen style={{ width: '12px', height: '12px' }} /> : <FileText style={{ width: '12px', height: '12px' }} />}
                {isNewStory ? 'Truyện mới' : 'Chương mới'}
            </div>
        );
    };

    const formatDate = (dateString) => {
        const date = new Date(dateString);
        const now = new Date();
        const diffMs = now - date;
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);

        if (diffMins < 60) return `${diffMins} phút trước`;
        if (diffHours < 24) return `${diffHours} giờ trước`;
        return `${diffDays} ngày trước`;
    };

    if (publications.length === 0) {
        return (
            <div style={{
                backgroundColor: '#ffffff',
                borderRadius: '12px',
                padding: '4rem 2rem',
                textAlign: 'center',
                border: '1px solid #e2e8f0'
            }}>
                <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>📋</div>
                <h3 style={{ fontSize: '1.25rem', fontWeight: 600, color: '#1e293b', marginBottom: '0.5rem', margin: 0, marginBottom: '0.5rem' }}>
                    Không có yêu cầu nào
                </h3>
                <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>
                    Chưa có truyện hoặc chương nào cần duyệt
                </p>
            </div>
        );
    }

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            {publications.map(pub => (
                <div
                    key={pub.id}
                    style={{
                        backgroundColor: '#ffffff',
                        borderRadius: '12px',
                        padding: '1.5rem',
                        border: '1px solid #e2e8f0',
                        transition: 'all 0.2s',
                        cursor: 'pointer'
                    }}
                    onMouseEnter={(e) => {
                        e.currentTarget.style.borderColor = '#13ec5b';
                        e.currentTarget.style.boxShadow = '0 4px 12px rgba(19, 236, 91, 0.15)';
                    }}
                    onMouseLeave={(e) => {
                        e.currentTarget.style.borderColor = '#e2e8f0';
                        e.currentTarget.style.boxShadow = 'none';
                    }}
                >
                    <div style={{ display: 'flex', gap: '1.25rem' }}>
                        {/* Cover Image */}
                        <img
                            src={pub.storyCover}
                            alt={pub.storyTitle}
                            style={{
                                width: '80px',
                                height: '112px',
                                objectFit: 'cover',
                                borderRadius: '8px',
                                flexShrink: 0
                            }}
                        />

                        {/* Content */}
                        <div style={{ flex: 1, minWidth: 0 }}>
                            {/* Header */}
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', marginBottom: '0.75rem', gap: '1rem', flexWrap: 'wrap' }}>
                                <div style={{ flex: 1, minWidth: 0 }}>
                                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem', flexWrap: 'wrap' }}>
                                        {getTypeBadge(pub.type)}
                                        {getStatusBadge(pub.status)}
                                    </div>
                                    <h3 style={{
                                        fontSize: '1.125rem',
                                        fontWeight: 600,
                                        color: '#1e293b',
                                        marginBottom: '0.25rem',
                                        margin: 0,
                                        marginBottom: '0.25rem'
                                    }}>
                                        {pub.storyTitle}
                                    </h3>
                                    <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>
                                        Tác giả: <span style={{ fontWeight: 500, color: '#475569' }}>{pub.author}</span>
                                    </p>
                                </div>

                                {/* View Button */}
                                <button
                                    onClick={() => onViewDetail(pub)}
                                    style={{
                                        padding: '0.625rem 1.25rem',
                                        backgroundColor: '#13ec5b',
                                        color: '#ffffff',
                                        fontSize: '0.875rem',
                                        fontWeight: 600,
                                        borderRadius: '8px',
                                        border: 'none',
                                        cursor: 'pointer',
                                        transition: 'all 0.2s',
                                        display: 'flex',
                                        alignItems: 'center',
                                        gap: '0.5rem',
                                        whiteSpace: 'nowrap'
                                    }}
                                    onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#10d954'}
                                    onMouseLeave={(e) => e.currentTarget.style.backgroundColor = '#13ec5b'}
                                >
                                    <Eye style={{ width: '16px', height: '16px' }} />
                                    Xem chi tiết
                                </button>
                            </div>

                            {/* Info */}
                            <div style={{
                                display: 'grid',
                                gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))',
                                gap: '1rem',
                                marginBottom: '0.75rem'
                            }}>
                                <div>
                                    <div style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.25rem' }}>
                                        Số chương
                                    </div>
                                    <div style={{ fontSize: '0.875rem', fontWeight: 600, color: '#1e293b' }}>
                                        {pub.totalChapters} chương
                                    </div>
                                </div>

                                <div>
                                    <div style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.25rem' }}>
                                        Tổng số từ
                                    </div>
                                    <div style={{ fontSize: '0.875rem', fontWeight: 600, color: '#1e293b' }}>
                                        {pub.totalWords.toLocaleString()} từ
                                    </div>
                                </div>

                                <div>
                                    <div style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.25rem' }}>
                                        Nộp lúc
                                    </div>
                                    <div style={{ fontSize: '0.875rem', fontWeight: 600, color: '#1e293b' }}>
                                        {formatDate(pub.submittedAt)}
                                    </div>
                                </div>

                                {pub.reviewedAt && (
                                    <div>
                                        <div style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.25rem' }}>
                                            Duyệt lúc
                                        </div>
                                        <div style={{ fontSize: '0.875rem', fontWeight: 600, color: '#1e293b' }}>
                                            {formatDate(pub.reviewedAt)}
                                        </div>
                                    </div>
                                )}
                            </div>

                            {/* Categories */}
                            <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.75rem', flexWrap: 'wrap' }}>
                                {pub.categories.map(cat => (
                                    <span
                                        key={cat}
                                        style={{
                                            padding: '0.25rem 0.625rem',
                                            backgroundColor: '#f8fafc',
                                            color: '#475569',
                                            fontSize: '0.75rem',
                                            fontWeight: 500,
                                            borderRadius: '0.375rem',
                                            border: '1px solid #e2e8f0'
                                        }}
                                    >
                                        {cat}
                                    </span>
                                ))}
                            </div>

                            {/* Rejection Reason */}
                            {pub.status === 'rejected' && pub.rejectionReason && (
                                <div style={{
                                    padding: '0.75rem',
                                    backgroundColor: '#fef2f2',
                                    borderLeft: '3px solid #ef4444',
                                    borderRadius: '0.5rem'
                                }}>
                                    <div style={{ fontSize: '0.75rem', fontWeight: 600, color: '#991b1b', marginBottom: '0.25rem' }}>
                                        Lý do từ chối:
                                    </div>
                                    <div style={{ fontSize: '0.875rem', color: '#991b1b' }}>
                                        {pub.rejectionReason}
                                    </div>
                                </div>
                            )}

                            {/* Description */}
                            {pub.type === 'new_story' && pub.description && (
                                <p style={{
                                    fontSize: '0.875rem',
                                    color: '#64748b',
                                    margin: 0,
                                    marginTop: '0.5rem',
                                    overflow: 'hidden',
                                    textOverflow: 'ellipsis',
                                    display: '-webkit-box',
                                    WebkitLineClamp: 2,
                                    WebkitBoxOrient: 'vertical'
                                }}>
                                    {pub.description}
                                </p>
                            )}
                        </div>
                    </div>
                </div>
            ))}
        </div>
    );
}
