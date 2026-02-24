import { useState, useEffect, useCallback } from 'react';
import { X, CheckCircle, XCircle, BookOpen, FileText, Clock, User, Calendar } from 'lucide-react';
import { getChapters, getChapterById } from '../../../api/chapter/chapterApi';

/** Map API chapter list item sang format modal cần */
function mapChapterItem(item) {
    const orderIndex = item.orderIndex ?? item.OrderIndex ?? 0;
    return {
        id: item.id ?? item.Id,
        chapterNumber: orderIndex + 1,
        title: item.title ?? item.Title ?? '',
        content: null,
        wordCount: item.wordCount ?? item.WordCount ?? 0,
        status: (item.status ?? item.Status ?? '').toLowerCase(),
    };
}

export function PublicationDetailModal({ publication, onClose, onApprove, onReject }) {
    const [chapters, setChapters] = useState([]);
    const [chaptersLoading, setChaptersLoading] = useState(true);
    const [chapterContents, setChapterContents] = useState({});
    const [selectedChapter, setSelectedChapter] = useState(null);
    const [showRejectForm, setShowRejectForm] = useState(false);
    const [rejectionReason, setRejectionReason] = useState('');
    const [isSubmitting, setIsSubmitting] = useState(false);

    const storyId = publication?.storyId ?? publication?.story_id;

    useEffect(() => {
        const id = setTimeout(() => {
            if (!storyId) {
                setChapters([]);
                setChaptersLoading(false);
                setSelectedChapter(null);
                return;
            }
            setChaptersLoading(true);
            setChapters([]);
            setSelectedChapter(null);
            setChapterContents({});
            getChapters({ storyId, status: 'PENDING_REVIEW', pageSize: 100 })
                .then((res) => {
                    const items = res?.items ?? res?.Items ?? [];
                    const mapped = items.map(mapChapterItem);
                    setChapters(mapped);
                    if (mapped.length > 0) setSelectedChapter(mapped[0]);
                })
                .catch(() => setChapters([]))
                .finally(() => setChaptersLoading(false));
        }, 0);
        return () => clearTimeout(id);
    }, [storyId]);

    const loadChapterContent = useCallback(async (chapterId) => {
        try {
            const data = await getChapterById(chapterId);
            setChapterContents((prev) => ({
                ...prev,
                [chapterId]: data?.content ?? data?.Content ?? '',
            }));
        } catch {
            setChapterContents((prev) => ({ ...prev, [chapterId]: '(Không tải được nội dung)' }));
        }
    }, []);

    useEffect(() => {
        const id = setTimeout(() => {
            if (selectedChapter?.id) loadChapterContent(selectedChapter.id);
        }, 0);
        return () => clearTimeout(id);
    }, [selectedChapter?.id, loadChapterContent]);

    const handleApprove = () => {
        if (window.confirm('Bạn có chắc chắn muốn duyệt xuất bản này?')) {
            setIsSubmitting(true);
            // Simulate API call
            setTimeout(() => {
                onApprove(publication.id);
                setIsSubmitting(false);
            }, 500);
        }
    };

    const handleRejectSubmit = () => {
        if (!rejectionReason.trim()) {
            alert('Vui lòng nhập lý do từ chối');
            return;
        }

        if (window.confirm('Bạn có chắc chắn muốn từ chối xuất bản này?')) {
            setIsSubmitting(true);
            // Simulate API call
            setTimeout(() => {
                onReject(publication.id, rejectionReason);
                setIsSubmitting(false);
            }, 500);
        }
    };

    const formatDate = (dateString) => {
        if (!dateString) return '-';
        const date = new Date(dateString);
        return date.toLocaleString('vi-VN', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit'
        });
    };

    const getStatusColor = (status) => {
        const colors = {
            pending: '#ffc107',
            approved: '#10b981',
            rejected: '#ef4444'
        };
        return colors[status] || '#64748b';
    };

    return (
        <div
            style={{
                position: 'fixed',
                top: 0,
                left: 0,
                right: 0,
                bottom: 0,
                backgroundColor: 'rgba(0, 0, 0, 0.5)',
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                zIndex: 9999,
                padding: '1rem'
            }}
            onClick={onClose}
        >
            <div
                style={{
                    backgroundColor: '#ffffff',
                    borderRadius: '16px',
                    maxWidth: '1200px',
                    width: '100%',
                    maxHeight: '90vh',
                    display: 'flex',
                    flexDirection: 'column',
                    overflow: 'hidden',
                    boxShadow: '0 20px 60px rgba(0, 0, 0, 0.3)'
                }}
                onClick={(e) => e.stopPropagation()}
            >
                {/* Header */}
                <div style={{
                    padding: '1.5rem',
                    borderBottom: '1px solid #e2e8f0',
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'start',
                    gap: '1rem'
                }}>
                    <div style={{ flex: 1, minWidth: 0 }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '0.5rem', flexWrap: 'wrap' }}>
                            <div style={{
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: '0.25rem',
                                padding: '0.25rem 0.625rem',
                                backgroundColor: publication.type === 'new_story' ? '#e0f2fe' : '#f3e8ff',
                                color: publication.type === 'new_story' ? '#075985' : '#6b21a8',
                                fontSize: '0.75rem',
                                fontWeight: 600,
                                borderRadius: '0.375rem'
                            }}>
                                {publication.type === 'new_story' ? <BookOpen style={{ width: '12px', height: '12px' }} /> : <FileText style={{ width: '12px', height: '12px' }} />}
                                {publication.type === 'new_story' ? 'Truyện mới' : 'Chương mới'}
                            </div>

                            <div style={{
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: '0.375rem',
                                padding: '0.375rem 0.75rem',
                                backgroundColor: `${getStatusColor(publication.status)}20`,
                                color: getStatusColor(publication.status),
                                fontSize: '0.75rem',
                                fontWeight: 600,
                                borderRadius: '9999px',
                                border: `2px solid ${getStatusColor(publication.status)}`
                            }}>
                                {publication.status === 'pending' && <Clock style={{ width: '14px', height: '14px' }} />}
                                {publication.status === 'approved' && <CheckCircle style={{ width: '14px', height: '14px' }} />}
                                {publication.status === 'rejected' && <XCircle style={{ width: '14px', height: '14px' }} />}
                                {publication.status === 'pending' ? 'Chờ duyệt' : publication.status === 'approved' ? 'Đã duyệt' : 'Từ chối'}
                            </div>
                        </div>

                        <h2 style={{
                            fontSize: '1.5rem',
                            fontWeight: 700,
                            color: '#1e293b',
                            margin: 0,
                            marginBottom: '0.5rem'
                        }}>
                            {publication.storyTitle}
                        </h2>

                        <div style={{ display: 'flex', alignItems: 'center', gap: '1.5rem', fontSize: '0.875rem', color: '#64748b', flexWrap: 'wrap' }}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                                <User style={{ width: '14px', height: '14px' }} />
                                <span>{publication.author}</span>
                            </div>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                                <Calendar style={{ width: '14px', height: '14px' }} />
                                <span>{formatDate(publication.submittedAt)}</span>
                            </div>
                            <div>
                                {publication.totalChapters} chương
                            </div>
                        </div>
                    </div>

                    <button
                        onClick={onClose}
                        style={{
                            padding: '0.5rem',
                            backgroundColor: 'transparent',
                            border: 'none',
                            cursor: 'pointer',
                            borderRadius: '0.5rem',
                            transition: 'background-color 0.2s',
                            flexShrink: 0
                        }}
                        onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#f8fafc'}
                        onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                    >
                        <X style={{ width: '24px', height: '24px', color: '#64748b' }} />
                    </button>
                </div>

                {/* Body */}
                <div style={{
                    display: 'flex',
                    flex: 1,
                    minHeight: 0,
                    overflow: 'hidden'
                }}>
                    {chaptersLoading ? (
                        <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '3rem' }}>
                            <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>Đang tải danh sách chương chờ duyệt...</p>
                        </div>
                    ) : chapters.length === 0 ? (
                        <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '3rem' }}>
                            <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>Không có chương nào đang chờ kiểm duyệt</p>
                        </div>
                    ) : (
                        <>
                            {/* Sidebar - Chapter List */}
                            {chapters.length >= 1 && (
                                <div style={{
                                    width: '280px',
                                    borderRight: '1px solid #e2e8f0',
                                    display: 'flex',
                                    flexDirection: 'column',
                                    backgroundColor: '#f8fafc'
                                }}>
                                    <div style={{ padding: '1rem', borderBottom: '1px solid #e2e8f0' }}>
                                        <h3 style={{ fontSize: '0.875rem', fontWeight: 600, color: '#64748b', margin: 0, textTransform: 'uppercase' }}>
                                            Chương chờ duyệt
                                        </h3>
                                    </div>
                                    <div style={{ flex: 1, overflowY: 'auto', padding: '0.5rem' }}>
                                        {chapters.map(chapter => (
                                            <button
                                                key={chapter.id}
                                                onClick={() => setSelectedChapter(chapter)}
                                                style={{
                                                    width: '100%',
                                                    padding: '0.75rem',
                                                    marginBottom: '0.5rem',
                                                    textAlign: 'left',
                                                    backgroundColor: selectedChapter?.id === chapter.id ? '#ffffff' : 'transparent',
                                                    border: selectedChapter?.id === chapter.id ? '2px solid #13ec5b' : '1px solid #e2e8f0',
                                                    borderRadius: '8px',
                                                    cursor: 'pointer',
                                                    transition: 'all 0.2s'
                                                }}
                                                onMouseEnter={(e) => {
                                                    if (selectedChapter?.id !== chapter.id) {
                                                        e.currentTarget.style.backgroundColor = '#ffffff';
                                                    }
                                                }}
                                                onMouseLeave={(e) => {
                                                    if (selectedChapter?.id !== chapter.id) {
                                                        e.currentTarget.style.backgroundColor = 'transparent';
                                                    }
                                                }}
                                            >
                                                <div style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.25rem' }}>
                                                    Chương {chapter.chapterNumber}
                                                </div>
                                                <div style={{
                                                    fontSize: '0.875rem',
                                                    fontWeight: 600,
                                                    color: '#1e293b',
                                                    overflow: 'hidden',
                                                    textOverflow: 'ellipsis',
                                                    whiteSpace: 'nowrap'
                                                }}>
                                                    {chapter.title}
                                                </div>
                                                <div style={{ fontSize: '0.75rem', color: '#64748b', marginTop: '0.25rem' }}>
                                                    {chapter.wordCount} từ
                                                </div>
                                            </button>
                                        ))}
                                    </div>
                                </div>
                            )}

                            {/* Main Content */}
                            <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0 }}>
                                {selectedChapter ? (
                                    <>
                                        <div style={{
                                            padding: '1.5rem',
                                            borderBottom: '1px solid #e2e8f0',
                                            backgroundColor: '#f8fafc'
                                        }}>
                                            <div style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.25rem' }}>
                                                CHƯƠNG {selectedChapter.chapterNumber}
                                            </div>
                                            <h3 style={{ fontSize: '1.25rem', fontWeight: 700, color: '#1e293b', margin: 0, marginBottom: '0.5rem' }}>
                                                {selectedChapter.title}
                                            </h3>
                                            <div style={{ fontSize: '0.875rem', color: '#64748b' }}>
                                                {selectedChapter.wordCount} từ
                                            </div>
                                        </div>

                                        <div style={{
                                            flex: 1,
                                            overflowY: 'auto',
                                            padding: '2rem',
                                            backgroundColor: '#ffffff'
                                        }}>
                                            <div style={{
                                                maxWidth: '800px',
                                                margin: '0 auto',
                                                fontSize: '1rem',
                                                lineHeight: 1.8,
                                                color: '#1e293b',
                                                whiteSpace: 'pre-wrap'
                                            }}>
                                                {chapterContents[selectedChapter.id] ?? 'Đang tải nội dung...'}
                                            </div>
                                        </div>
                                    </>
                                ) : null}
                            </div>
                        </>
                    )}
                </div>

                {/* Footer - Actions */}
                {publication.status === 'pending' && !showRejectForm && (
                    <div style={{
                        padding: '1.5rem',
                        borderTop: '1px solid #e2e8f0',
                        display: 'flex',
                        justifyContent: 'flex-end',
                        gap: '1rem',
                        backgroundColor: '#f8fafc'
                    }}>
                        <button
                            onClick={() => setShowRejectForm(true)}
                            disabled={isSubmitting}
                            style={{
                                padding: '0.75rem 1.5rem',
                                backgroundColor: '#ffffff',
                                color: '#ef4444',
                                fontSize: '0.875rem',
                                fontWeight: 700,
                                borderRadius: '8px',
                                border: '2px solid #ef4444',
                                cursor: isSubmitting ? 'not-allowed' : 'pointer',
                                transition: 'all 0.2s',
                                opacity: isSubmitting ? 0.5 : 1,
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.5rem'
                            }}
                            onMouseEnter={(e) => {
                                if (!isSubmitting) {
                                    e.currentTarget.style.backgroundColor = '#fef2f2';
                                }
                            }}
                            onMouseLeave={(e) => {
                                if (!isSubmitting) {
                                    e.currentTarget.style.backgroundColor = '#ffffff';
                                }
                            }}
                        >
                            <XCircle style={{ width: '18px', height: '18px' }} />
                            Từ chối
                        </button>

                        <button
                            onClick={handleApprove}
                            disabled={isSubmitting}
                            style={{
                                padding: '0.75rem 1.5rem',
                                backgroundColor: '#10b981',
                                color: '#ffffff',
                                fontSize: '0.875rem',
                                fontWeight: 700,
                                borderRadius: '8px',
                                border: 'none',
                                cursor: isSubmitting ? 'not-allowed' : 'pointer',
                                transition: 'all 0.2s',
                                opacity: isSubmitting ? 0.5 : 1,
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.5rem'
                            }}
                            onMouseEnter={(e) => {
                                if (!isSubmitting) {
                                    e.currentTarget.style.backgroundColor = '#059669';
                                }
                            }}
                            onMouseLeave={(e) => {
                                if (!isSubmitting) {
                                    e.currentTarget.style.backgroundColor = '#10b981';
                                }
                            }}
                        >
                            <CheckCircle style={{ width: '18px', height: '18px' }} />
                            {isSubmitting ? 'Đang xử lý...' : 'Duyệt xuất bản'}
                        </button>
                    </div>
                )}

                {/* Rejection Form */}
                {showRejectForm && (
                    <div style={{
                        padding: '1.5rem',
                        borderTop: '1px solid #e2e8f0',
                        backgroundColor: '#fef2f2'
                    }}>
                        <label style={{
                            display: 'block',
                            fontSize: '0.875rem',
                            fontWeight: 600,
                            color: '#991b1b',
                            marginBottom: '0.5rem'
                        }}>
                            Lý do từ chối <span style={{ color: '#ef4444' }}>*</span>
                        </label>
                        <textarea
                            value={rejectionReason}
                            onChange={(e) => setRejectionReason(e.target.value)}
                            placeholder="Nhập lý do từ chối xuất bản (bắt buộc)..."
                            rows={4}
                            style={{
                                width: '100%',
                                padding: '0.75rem',
                                fontSize: '0.875rem',
                                border: '2px solid #fca5a5',
                                borderRadius: '8px',
                                resize: 'vertical',
                                outline: 'none',
                                fontFamily: 'inherit',
                                marginBottom: '1rem'
                            }}
                            onFocus={(e) => e.target.style.borderColor = '#ef4444'}
                            onBlur={(e) => e.target.style.borderColor = '#fca5a5'}
                        />

                        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '1rem' }}>
                            <button
                                onClick={() => {
                                    setShowRejectForm(false);
                                    setRejectionReason('');
                                }}
                                disabled={isSubmitting}
                                style={{
                                    padding: '0.75rem 1.5rem',
                                    backgroundColor: '#ffffff',
                                    color: '#64748b',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    borderRadius: '8px',
                                    border: '1px solid #e2e8f0',
                                    cursor: isSubmitting ? 'not-allowed' : 'pointer',
                                    transition: 'all 0.2s',
                                    opacity: isSubmitting ? 0.5 : 1
                                }}
                                onMouseEnter={(e) => {
                                    if (!isSubmitting) {
                                        e.currentTarget.style.backgroundColor = '#f8fafc';
                                    }
                                }}
                                onMouseLeave={(e) => {
                                    if (!isSubmitting) {
                                        e.currentTarget.style.backgroundColor = '#ffffff';
                                    }
                                }}
                            >
                                Hủy
                            </button>

                            <button
                                onClick={handleRejectSubmit}
                                disabled={isSubmitting || !rejectionReason.trim()}
                                style={{
                                    padding: '0.75rem 1.5rem',
                                    backgroundColor: '#ef4444',
                                    color: '#ffffff',
                                    fontSize: '0.875rem',
                                    fontWeight: 700,
                                    borderRadius: '8px',
                                    border: 'none',
                                    cursor: (isSubmitting || !rejectionReason.trim()) ? 'not-allowed' : 'pointer',
                                    transition: 'all 0.2s',
                                    opacity: (isSubmitting || !rejectionReason.trim()) ? 0.5 : 1
                                }}
                                onMouseEnter={(e) => {
                                    if (!isSubmitting && rejectionReason.trim()) {
                                        e.currentTarget.style.backgroundColor = '#dc2626';
                                    }
                                }}
                                onMouseLeave={(e) => {
                                    if (!isSubmitting && rejectionReason.trim()) {
                                        e.currentTarget.style.backgroundColor = '#ef4444';
                                    }
                                }}
                            >
                                {isSubmitting ? 'Đang xử lý...' : 'Xác nhận từ chối'}
                            </button>
                        </div>
                    </div>
                )}

                {/* Already Reviewed Info */}
                {publication.status !== 'pending' && (
                    <div style={{
                        padding: '1.5rem',
                        borderTop: '1px solid #e2e8f0',
                        backgroundColor: publication.status === 'approved' ? '#f0fdf4' : '#fef2f2'
                    }}>
                        <div style={{
                            fontSize: '0.875rem',
                            color: publication.status === 'approved' ? '#065f46' : '#991b1b',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '0.5rem'
                        }}>
                            {publication.status === 'approved' ? <CheckCircle style={{ width: '16px', height: '16px' }} /> : <XCircle style={{ width: '16px', height: '16px' }} />}
                            <span style={{ fontWeight: 600 }}>
                                {publication.status === 'approved' ? 'Đã duyệt xuất bản' : 'Đã từ chối xuất bản'}
                            </span>
                            <span>•</span>
                            <span>{formatDate(publication.reviewedAt)}</span>
                            {publication.reviewedBy && (
                                <>
                                    <span>•</span>
                                    <span>Bởi: {publication.reviewedBy}</span>
                                </>
                            )}
                        </div>

                        {publication.status === 'rejected' && publication.rejectionReason && (
                            <div style={{
                                marginTop: '0.75rem',
                                padding: '0.75rem',
                                backgroundColor: '#ffffff',
                                borderLeft: '3px solid #ef4444',
                                borderRadius: '0.375rem'
                            }}>
                                <div style={{ fontSize: '0.75rem', fontWeight: 600, color: '#991b1b', marginBottom: '0.25rem' }}>
                                    Lý do từ chối:
                                </div>
                                <div style={{ fontSize: '0.875rem', color: '#991b1b' }}>
                                    {publication.rejectionReason}
                                </div>
                            </div>
                        )}
                    </div>
                )}
            </div>
        </div>
    );
}
