import { Clock, CheckCircle, XCircle, Eye, FileText, BookOpen, UserCheck, AlertCircle, RotateCcw } from 'lucide-react';
import { getSlaBadgeStyle, formatPolicySlaCountdown, normalizeTimeStatus } from '../../../utils/moderatorReviewSla';
import { formatApiDateTimeLocalVi } from '../../../utils/apiDateTime';

export function PublicationList({
    publications,
    onViewDetail,
    onClaimStory,
    onClaimChapter,
    claimingId,
    showClaimButton,
    showModeratorSla = false,
    onReleaseAllClaimsForStory,
    releasingAllClaimsStoryId = null,
}) {
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

    const getTypeBadge = (pub) => {
        const isStoryGroup = pub.type === 'story_group';
        const isStory = pub.type === 'story' || pub.type === 'new_story';
        const label = isStoryGroup || isStory ? 'Truyện' : 'Chương';
        const isStoryStyle = isStory || isStoryGroup;
        return (
            <div style={{
                display: 'inline-flex',
                alignItems: 'center',
                gap: '0.25rem',
                padding: '0.25rem 0.625rem',
                backgroundColor: isStoryStyle ? '#e0f2fe' : '#f3e8ff',
                color: isStoryStyle ? '#075985' : '#6b21a8',
                fontSize: '0.75rem',
                fontWeight: 600,
                borderRadius: '0.375rem'
            }}>
                {isStoryStyle ? <BookOpen style={{ width: '12px', height: '12px' }} /> : <FileText style={{ width: '12px', height: '12px' }} />}
                {label}
            </div>
        );
    };

    const formatDate = (dateString) => {
        if (!dateString) return '—';
        const date = new Date(dateString);
        if (Number.isNaN(date.getTime())) return '—';
        const now = new Date();
        const diffMs = now - date;
        const diffMins = Math.floor(diffMs / 60000);
        const diffHours = Math.floor(diffMs / 3600000);
        const diffDays = Math.floor(diffMs / 86400000);

        if (diffMins < 60) return `${diffMins} phút trước`;
        if (diffHours < 24) return `${diffHours} giờ trước`;
        return `${diffDays} ngày trước`;
    };

    const claimedAtForPub = (pub) => {
        if (pub.type === 'story_group') {
            return pub.representativePublication?.claimedAt ?? null;
        }
        return pub.claimedAt ?? null;
    };

    const timeStatusForPub = (pub) => {
        if (pub.type === 'story_group') {
            return normalizeTimeStatus(pub.slaTimeStatus) ?? normalizeTimeStatus(pub.representativePublication?.timeStatus);
        }
        return normalizeTimeStatus(pub.timeStatus);
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
                <h3 style={{ fontSize: '1.25rem', fontWeight: 600, color: '#1e293b', margin: 0, marginBottom: '0.5rem' }}>
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
                        {/* Cover Image — dùng ảnh truyện; chương không có ảnh riêng thì lấy từ truyện hoặc placeholder */}
                        {pub.storyCover ? (
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
                                onError={(e) => {
                                    e.target.style.display = 'none';
                                    e.target.nextSibling?.style?.display && (e.target.nextSibling.style.display = 'flex');
                                }}
                            />
                        ) : null}
                        <div
                            style={{
                                display: pub.storyCover ? 'none' : 'flex',
                                width: '80px',
                                height: '112px',
                                borderRadius: '8px',
                                flexShrink: 0,
                                backgroundColor: '#e2e8f0',
                                alignItems: 'center',
                                justifyContent: 'center',
                                color: '#94a3b8'
                            }}
                            aria-hidden
                        >
                            <BookOpen style={{ width: '32px', height: '32px' }} />
                        </div>

                        {/* Content */}
                        <div style={{ flex: 1, minWidth: 0 }}>
                            {/* Header */}
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', marginBottom: '0.75rem', gap: '1rem', flexWrap: 'wrap' }}>
                                <div style={{ flex: 1, minWidth: 0 }}>
                                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '0.5rem', flexWrap: 'wrap' }}>
                                        {getTypeBadge(pub)}
                                        {getStatusBadge(pub.status)}
                                        {showModeratorSla && pub.status === 'pending' && timeStatusForPub(pub) && (() => {
                                            const sla = getSlaBadgeStyle(timeStatusForPub(pub));
                                            return (
                                                <span style={{
                                                    fontSize: '0.75rem',
                                                    fontWeight: 600,
                                                    padding: '0.25rem 0.5rem',
                                                    borderRadius: '9999px',
                                                    backgroundColor: sla.bg,
                                                    color: sla.color,
                                                }}>
                                                    SLA: {sla.label}
                                                </span>
                                            );
                                        })()}
                                        {showModeratorSla && pub.status === 'pending' && pub.hasPendingEscalation && (
                                            <span style={{
                                                fontSize: '0.75rem',
                                                fontWeight: 600,
                                                padding: '0.25rem 0.5rem',
                                                borderRadius: '9999px',
                                                backgroundColor: '#fef2f2',
                                                color: '#b91c1c',
                                                display: 'inline-flex',
                                                alignItems: 'center',
                                                gap: '0.25rem',
                                            }}>
                                                <AlertCircle style={{ width: '12px', height: '12px' }} />
                                                Đơn báo cáo chờ admin
                                            </span>
                                        )}
                                        {showClaimButton && pub.claimedByDisplayName && (
                                            <span style={{
                                                fontSize: '0.75rem',
                                                color: pub.isClaimedByMe ? '#065f46' : '#64748b',
                                                backgroundColor: pub.isClaimedByMe ? '#d1fae5' : '#f1f5f9',
                                                padding: '0.25rem 0.5rem',
                                                borderRadius: '9999px',
                                                fontWeight: 500
                                            }}>
                                                {pub.isClaimedByMe ? 'Đã nhận bởi bạn' : `Đã nhận: ${pub.claimedByDisplayName}`}
                                            </span>
                                        )}
                                    </div>
                                    <h3 style={{
                                        fontSize: '1.125rem',
                                        fontWeight: 600,
                                        color: '#1e293b',
                                        margin: 0,
                                        marginBottom: '0.25rem'
                                    }}>
                                        {pub.storyTitle}
                                        {pub.type === 'chapter' && pub.chapterTitle && (
                                            <span style={{ fontWeight: 500, color: '#64748b', fontSize: '0.9375rem' }}> — {pub.chapterTitle}</span>
                                        )}
                                    </h3>
                                    {pub.type === 'chapter' && pub.isEditRequest && (
                                        <div style={{
                                            display: 'inline-flex',
                                            alignItems: 'center',
                                            marginTop: '0.375rem',
                                            padding: '0.25rem 0.5rem',
                                            backgroundColor: '#fef3c7',
                                            color: '#92400e',
                                            fontSize: '0.75rem',
                                            fontWeight: 600,
                                            borderRadius: '6px',
                                            border: '1px solid #f59e0b'
                                        }}>
                                            Yêu cầu chỉnh sửa (chương đã xuất bản)
                                        </div>
                                    )}
                                    <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>
                                        {pub.author ? <>Tác giả: <span style={{ fontWeight: 500, color: '#475569' }}>{pub.author}</span></> : null}
                                        {pub.type === 'chapter' && pub.wordCount != null ? ` • ${pub.wordCount} từ` : null}
                                    </p>
                                    {showModeratorSla && pub.status === 'pending' && (() => {
                                        const atStr = pub.adminRejectedReleaseAt ?? null;
                                        const claimedAtStr = claimedAtForPub(pub);
                                        const currentFlagRaw = pub.isCurrentClaimRejection;
                                        const hasCurrentFlag = typeof currentFlagRaw === 'boolean';
                                        const at = atStr ? new Date(atStr).getTime() : NaN;
                                        const claimedAt = claimedAtStr ? new Date(claimedAtStr).getTime() : NaN;
                                        const hasAdminRejectInfo = !!(pub.adminRejectedReleaseNote || pub.adminRejectedReleaseAt);
                                        const isCurrentClaimCycle = hasCurrentFlag
                                            ? Boolean(currentFlagRaw)
                                            : (!Number.isFinite(at) || !Number.isFinite(claimedAt) || at >= claimedAt);
                                        return hasAdminRejectInfo && isCurrentClaimCycle;
                                    })() && (
                                            <div
                                                style={{
                                                    marginTop: '0.75rem',
                                                    padding: '0.75rem 0.875rem',
                                                    backgroundColor: '#fff7ed',
                                                    border: '1px solid #fdba74',
                                                    borderRadius: '8px',
                                                }}
                                                onClick={(e) => e.stopPropagation()}
                                            >
                                                <div style={{ fontSize: '0.75rem', fontWeight: 700, color: '#9a3412', marginBottom: '0.35rem', display: 'flex', alignItems: 'center', gap: '0.35rem' }}>
                                                    <AlertCircle style={{ width: '14px', height: '14px', flexShrink: 0 }} />
                                                    Quản trị viên đã từ chối đơn hủy nhận duyệt
                                                </div>
                                                {pub.adminRejectedReleaseAt ? (
                                                    <div style={{ fontSize: '0.7rem', color: '#64748b', marginBottom: '0.35rem' }}>
                                                        Thời điểm: {formatApiDateTimeLocalVi(pub.adminRejectedReleaseAt)}
                                                    </div>
                                                ) : null}
                                                <div style={{ fontSize: '0.8125rem', color: '#431407', whiteSpace: 'pre-wrap', lineHeight: 1.45 }}>
                                                    <strong style={{ color: '#7c2d12' }}>Lý do / ghi chú:</strong>{' '}
                                                    {pub.adminRejectedReleaseNote && String(pub.adminRejectedReleaseNote).trim()
                                                        ? pub.adminRejectedReleaseNote
                                                        : 'Quản trị viên không nhập ghi chú.'}
                                                </div>
                                            </div>
                                        )}
                                    {/* Từ chối gia hạn: chỉ hiển thị trong dialog chi tiết, theo từng chương (không gộp trên thẻ truyện). */}
                                </div>

                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexShrink: 0, flexWrap: 'wrap' }}>
                                    {showClaimButton && (pub.type === 'story' || pub.type === 'chapter') && !pub.isClaimedByMe && (() => {
                                        const pubId = pub.type === 'story' ? (pub.storyId ?? pub.id) : (pub.chapterId ?? pub.id);
                                        const isClaiming = claimingId === pubId;
                                        const claimedByOther = pub.claimedByDisplayName && !pub.isClaimedByMe;
                                        const disabled = claimedByOther || isClaiming;
                                        let label = 'Nhận duyệt đơn';
                                        if (isClaiming) label = '...';
                                        else if (claimedByOther) label = `Đã nhận bởi ${pub.claimedByDisplayName}`;
                                        return (
                                            <button
                                                onClick={(e) => {
                                                    e.stopPropagation();
                                                    if (disabled) return;
                                                    if (pub.type === 'story') onClaimStory?.(pub.storyId ?? pub.id);
                                                    else onClaimChapter?.(pub.chapterId ?? pub.id);
                                                }}
                                                disabled={disabled}
                                                style={{
                                                    padding: '0.625rem 1rem',
                                                    backgroundColor: disabled ? '#e2e8f0' : '#0ea5e9',
                                                    color: disabled ? '#64748b' : '#ffffff',
                                                    fontSize: '0.8125rem',
                                                    fontWeight: 600,
                                                    borderRadius: '8px',
                                                    border: 'none',
                                                    cursor: disabled ? 'not-allowed' : 'pointer',
                                                    opacity: isClaiming ? 0.7 : 1,
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    gap: '0.375rem',
                                                    whiteSpace: 'nowrap'
                                                }}
                                            >
                                                <UserCheck style={{ width: '14px', height: '14px' }} />
                                                {label}
                                            </button>
                                        );
                                    })()}
                                    {showModeratorSla && pub.status === 'pending' && pub.type === 'story_group'
                                        && Array.isArray(pub.chapters) && pub.chapters.some((c) => c.isClaimedByMe)
                                        && !pub.hasPendingEscalation && typeof onReleaseAllClaimsForStory === 'function' && (() => {
                                            const sid = pub.storyId ?? pub.id;
                                            const busy = releasingAllClaimsStoryId != null && String(releasingAllClaimsStoryId) === String(sid);
                                            return (
                                                <button
                                                    type="button"
                                                    onClick={(e) => {
                                                        e.stopPropagation();
                                                        if (busy) return;
                                                        onReleaseAllClaimsForStory(pub);
                                                    }}
                                                    disabled={busy}
                                                    title="Hủy nhận duyệt tất cả chương của truyện này và trả về hàng đợi"
                                                    style={{
                                                        padding: '0.625rem 1rem',
                                                        backgroundColor: busy ? '#e2e8f0' : '#fef2f2',
                                                        color: busy ? '#64748b' : '#b91c1c',
                                                        fontSize: '0.8125rem',
                                                        fontWeight: 600,
                                                        borderRadius: '8px',
                                                        border: '1px solid #fecaca',
                                                        cursor: busy ? 'wait' : 'pointer',
                                                        display: 'flex',
                                                        alignItems: 'center',
                                                        gap: '0.375rem',
                                                        whiteSpace: 'nowrap',
                                                    }}
                                                >
                                                    <RotateCcw style={{ width: '14px', height: '14px' }} />
                                                    {busy ? 'Đang xử lý...' : 'Hủy nhận duyệt'}
                                                </button>
                                            );
                                        })()}
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
                            </div>

                            {/* Info — Số chương, Độ tuổi phù hợp, Nộp lúc, Duyệt lúc */}
                            <div style={{
                                display: 'grid',
                                gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))',
                                gap: '1rem',
                                marginBottom: '0.75rem'
                            }}>
                                {(pub.type === 'story_group' && pub.chapterCount != null) || (pub.type !== 'story_group' && pub.totalChapters != null) ? (
                                    <div>
                                        <div style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.25rem' }}>
                                            Số chương
                                        </div>
                                        <div style={{ fontSize: '0.875rem', fontWeight: 600, color: '#1e293b' }}>
                                            {pub.type === 'story_group' ? pub.chapterCount : pub.totalChapters} chương
                                        </div>
                                    </div>
                                ) : null}
                                <div>
                                    <div style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.25rem' }}>
                                        Độ tuổi phù hợp
                                    </div>
                                    <div style={{ fontSize: '0.875rem', fontWeight: 600, color: '#1e293b' }}>
                                        {pub.ageRating ? ({ ALL: 'Phù hợp mọi lứa tuổi', '13+': 'Từ 13 tuổi', '16+': 'Từ 16 tuổi', '18+': 'Từ 18 tuổi' })[String(pub.ageRating).toUpperCase()] ?? pub.ageRating : '—'}
                                    </div>
                                </div>
                                {pub.type === 'chapter' && pub.wordCount != null && (
                                    <div>
                                        <div style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.25rem' }}>
                                            Số từ
                                        </div>
                                        <div style={{ fontSize: '0.875rem', fontWeight: 600, color: '#1e293b' }}>
                                            {pub.wordCount} từ
                                        </div>
                                    </div>
                                )}

                                {!(showModeratorSla && pub.status === 'pending') && (
                                    <div>
                                        <div style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.25rem' }}>
                                            Nộp lúc
                                        </div>
                                        <div style={{ fontSize: '0.875rem', fontWeight: 600, color: '#1e293b' }}>
                                            {formatDate(
                                                pub.type === 'story_group' && pub.representativePublication
                                                    ? pub.representativePublication.submittedAt
                                                    : pub.submittedAt
                                            )}
                                        </div>
                                    </div>
                                )}

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

                            {showModeratorSla && pub.status === 'pending' && claimedAtForPub(pub) && (() => {
                                const { line } = formatPolicySlaCountdown(claimedAtForPub(pub));
                                if (!line) return null;
                                return (
                                    <div style={{
                                        marginBottom: '0.75rem',
                                        padding: '0.5rem 0.75rem',
                                        backgroundColor: '#f8fafc',
                                        borderRadius: '8px',
                                        borderLeft: '3px solid #0ea5e9',
                                        fontSize: '0.8125rem',
                                        color: '#334155',
                                    }}>
                                        {line}
                                    </div>
                                );
                            })()}

                            {/* Categories */}
                            {Array.isArray(pub.categories) && pub.categories.length > 0 && (
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
                            )}

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

                            {/* Description — truyện hoặc chương (chương dùng mô tả truyện) */}
                            {pub.description && (
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
