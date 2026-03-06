import { useState, useEffect, useCallback, useRef } from 'react';
import { X, CheckCircle, XCircle, BookOpen, FileText, Clock, User, Calendar } from 'lucide-react';
import { getChapters, getChapterById } from '../../../api/chapter/chapterApi';
import { approveStory, approveChapter, rejectStory, rejectChapter } from '../../../api/moderator/moderatorApi';
import { createModeratorHubConnection } from '../../../api/moderator/moderatorHub';
import { useToast } from '../../author/story-editor/Toast';

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
        publishedAt: item.publishedAt ?? item.PublishedAt ?? null,
    };
}

/** Map chương từ story_group (tab Từ chối: publication.chapters) sang format modal */
function mapStoryGroupChapterToModal(ch) {
    const orderIndex = ch.orderIndex ?? 0;
    return {
        id: ch.id ?? ch.chapterId,
        chapterNumber: orderIndex + 1,
        title: ch.chapterTitle ?? '',
        content: null,
        wordCount: ch.wordCount ?? 0,
        status: ch.status ?? 'rejected',
        publishedAt: null,
    };
}

export function PublicationDetailModal({ publication, onClose, onApprove, onReject, onRefresh }) {
    const { showToast, ToastContainer } = useToast();
    const [chapters, setChapters] = useState([]);
    const [chaptersLoading, setChaptersLoading] = useState(true);
    const [chapterContents, setChapterContents] = useState({});
    const [selectedChapter, setSelectedChapter] = useState(null);
    const [showRejectForm, setShowRejectForm] = useState(false);
    const [showRejectConfirm, setShowRejectConfirm] = useState(false);
    const [showApproveConfirm, setShowApproveConfirm] = useState(false);
    const [rejectionReason, setRejectionReason] = useState('');
    const [isSubmitting, setIsSubmitting] = useState(false);
    /** Sau khi duyệt chương 1 đã gọi approveStory rồi thì không gọi lại khi duyệt chương 2, 3... (publication.status không đổi trong modal) */
    const storyApprovedInSessionRef = useRef(false);
    /** Vừa từ chối trong phiên này → không hiển thị khối "Đã từ chối xuất bản" / "Lý do từ chối" để moderator duyệt liên tiếp thoải mái */
    const justRejectedInSessionRef = useRef(false);

    const storyId = publication?.storyId ?? publication?.story_id ?? publication?.id;

    const fetchChaptersForStory = useCallback((sid, options = {}) => {
        if (!sid) return;
        if (options.showLoading !== false) setChaptersLoading(true);
        const pubStatus = options.publicationStatus ?? 'pending';
        const params = { storyId: sid, pageSize: 100 };
        if (pubStatus === 'approved') params.status = 'PUBLISHED';
        else if (pubStatus === 'pending') params.status = 'PENDING_REVIEW';
        getChapters(params)
            .then((res) => {
                const items = res?.items ?? res?.Items ?? [];
                const mapped = items.map(mapChapterItem);
                setChapters(mapped);
                setSelectedChapter((prev) => (prev && mapped.some((c) => c.id === prev.id)) ? prev : (mapped[0] ?? null));
            })
            .catch(() => setChapters([]))
            .finally(() => setChaptersLoading(false));
    }, []);

    useEffect(() => {
        storyApprovedInSessionRef.current = false;
        justRejectedInSessionRef.current = false;
        const id = setTimeout(() => {
            if (!storyId) {
                setChapters([]);
                setChaptersLoading(false);
                setSelectedChapter(null);
                return;
            }
            setChapters([]);
            setSelectedChapter(null);
            setChapterContents({});
            // Tab Đã duyệt / Từ chối: item là story_group có sẵn danh sách chương (đã duyệt hoặc bị từ chối) — chỉ hiển thị các chương đó, không gọi API lấy hết chương.
            if (publication?.type === 'story_group' && Array.isArray(publication?.chapters) && publication.chapters.length > 0) {
                const mapped = publication.chapters.map(mapStoryGroupChapterToModal);
                setChapters(mapped);
                setSelectedChapter(mapped[0] ?? null);
                setChaptersLoading(false);
                return;
            }
            fetchChaptersForStory(storyId, { publicationStatus: publication?.status });
        }, 0);
        return () => clearTimeout(id);
    }, [storyId, publication?.status, publication?.type, publication?.chapters, fetchChaptersForStory]);

    /** Real-time: khi có claim/approve/reject, refetch danh sách chương trong modal (bỏ qua khi đang xem story_group từ tab Từ chối). */
    const refetchChaptersRef = useRef(() => { });
    refetchChaptersRef.current = () => {
        if (!storyId) return;
        if (publication?.type === 'story_group' && publication?.chapters?.length > 0) return;
        fetchChaptersForStory(storyId, { showLoading: false, publicationStatus: publication?.status });
    };
    useEffect(() => {
        if (!storyId) return;
        const { stop } = createModeratorHubConnection(() => refetchChaptersRef.current());
        return () => { stop(); };
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

    const openApproveConfirm = () => {
        if (selectedChapter) setShowApproveConfirm(true);
    };

    const handleApproveConfirm = async () => {
        if (!selectedChapter) return;
        setShowApproveConfirm(false);
        setIsSubmitting(true);
        try {
            // Gọi approveStory khi chưa duyệt truyện trong phiên (để set story PUBLISHED). Bắt 404 để không chặn duyệt chương khi truyện đã PUBLISHED.
            const needApproveStory = publication.status !== 'approved' && !storyApprovedInSessionRef.current;
            if (needApproveStory) {
                try {
                    await approveStory(storyId);
                } catch (err) {
                    if (err?.response?.status === 404) {
                        storyApprovedInSessionRef.current = true;
                    } else {
                        throw err;
                    }
                }
                storyApprovedInSessionRef.current = true;
            }
            await approveChapter(selectedChapter.id);
            showToast('Duyệt chương thành công!', 'success');
            const remaining = chapters.filter(c => c.id !== selectedChapter.id);
            setChapters(remaining);
            setSelectedChapter(remaining[0] ?? null);
            setChapterContents((prev) => {
                const next = { ...prev };
                delete next[selectedChapter.id];
                return next;
            });
            onRefresh?.();
            if (remaining.length === 0) {
                onApprove(publication.id);
            }
        } catch (err) {
            showToast(err?.response?.data?.message ?? err?.message ?? 'Không thể duyệt xuất bản. Vui lòng thử lại.', 'error');
        } finally {
            setIsSubmitting(false);
        }
    };

    const openRejectConfirm = () => {
        if (!rejectionReason.trim()) {
            showToast('Vui lòng nhập lý do từ chối', 'error');
            return;
        }
        setShowRejectConfirm(true);
    };

    const handleRejectSubmit = async () => {
        setShowRejectConfirm(false);
        if (!rejectionReason.trim()) return;
        setIsSubmitting(true);
        try {
            if (selectedChapter) {
                await rejectChapter(selectedChapter.id, rejectionReason.trim());
                showToast('Đã từ chối chương.', 'success');
                const remaining = chapters.filter(c => c.id !== selectedChapter.id);
                setChapters(remaining);
                setSelectedChapter(remaining[0] ?? null);
                setChapterContents((prev) => {
                    const next = { ...prev };
                    delete next[selectedChapter.id];
                    return next;
                });
                onRefresh?.();
                // Gọi rejectStory khi không còn chương chờ duyệt. Bắt 404 (truyện đã PUBLISHED sau khi duyệt chương trước) để vẫn đóng form và không hiện toast lỗi.
                const isStoryRow = publication.type === 'story' || publication.type === 'new_story';
                if (remaining.length === 0 && isStoryRow && publication.status !== 'approved') {
                    try {
                        await rejectStory(storyId, rejectionReason.trim());
                        onReject(publication.id);
                    } catch (rejectErr) {
                        if (rejectErr?.response?.status === 404) {
                            onRefresh?.();
                        } else {
                            throw rejectErr;
                        }
                    }
                }
                if (remaining.length === 0) {
                    onClose?.();
                }
            } else {
                if (publication.type === 'story' || publication.type === 'new_story') {
                    try {
                        await rejectStory(storyId, rejectionReason.trim());
                        showToast('Đã từ chối truyện.', 'success');
                        onReject(publication.id);
                        onRefresh?.();
                    } catch (rejectErr) {
                        if (rejectErr?.response?.status === 404) {
                            showToast('Truyện không còn ở trạng thái chờ duyệt.', 'info');
                        } else {
                            throw rejectErr;
                        }
                    }
                } else {
                    showToast('Truyện không còn ở trạng thái chờ duyệt.', 'info');
                }
                onClose?.();
            }
        } catch (err) {
            showToast(err?.response?.data?.message ?? err?.message ?? 'Không thể từ chối. Vui lòng thử lại.', 'error');
        } finally {
            setShowRejectForm(false);
            setRejectionReason('');
            justRejectedInSessionRef.current = true;
            setIsSubmitting(false);
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
            approved: '#13ec5b',
            rejected: '#ef4444'
        };
        return colors[status] || '#64748b';
    };

    return (
        <>
            <ToastContainer />
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
                            {publication?.status !== 'approved' && (
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
                                        backgroundColor: `${getStatusColor(chapters.length > 0 ? 'pending' : publication.status)}20`,
                                        color: getStatusColor(chapters.length > 0 ? 'pending' : publication.status),
                                        fontSize: '0.75rem',
                                        fontWeight: 600,
                                        borderRadius: '9999px',
                                        border: `2px solid ${getStatusColor(chapters.length > 0 ? 'pending' : publication.status)}`
                                    }}>
                                        {(chapters.length > 0 || publication.status === 'pending') && <Clock style={{ width: '14px', height: '14px' }} />}
                                        {chapters.length === 0 && publication.status === 'approved' && <CheckCircle style={{ width: '14px', height: '14px' }} />}
                                        {chapters.length === 0 && publication.status === 'rejected' && <XCircle style={{ width: '14px', height: '14px' }} />}
                                        {chapters.length > 0 ? 'Chờ duyệt' : publication.status === 'pending' ? 'Chờ duyệt' : publication.status === 'approved' ? 'Đã duyệt' : 'Từ chối'}
                                    </div>
                                </div>
                            )}

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
                                    <span>{publication.author ?? '—'}</span>
                                </div>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                                    <Calendar style={{ width: '14px', height: '14px' }} />
                                    <span>{formatDate(publication.submittedAt)}</span>
                                </div>
                                <div>
                                    {publication.totalChapters != null ? `${publication.totalChapters} chương` : null}
                                </div>
                                {publication.claimedByDisplayName && (
                                    <span style={{
                                        padding: '0.25rem 0.5rem',
                                        backgroundColor: publication.isClaimedByMe ? '#d1fae5' : '#f1f5f9',
                                        color: publication.isClaimedByMe ? '#065f46' : '#64748b',
                                        borderRadius: '9999px',
                                        fontSize: '0.75rem',
                                        fontWeight: 600
                                    }}>
                                        {publication.isClaimedByMe ? 'Đã nhận bởi bạn' : `Đã nhận: ${publication.claimedByDisplayName}`}
                                    </span>
                                )}
                            </div>
                        </div>

                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexShrink: 0 }}>
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
                                <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>
                                    {publication?.status === 'approved' ? 'Đang tải danh sách chương đã xuất bản...' : publication?.status === 'rejected' ? 'Đang tải danh sách chương...' : 'Đang tải danh sách chương chờ duyệt...'}
                                </p>
                            </div>
                        ) : chapters.length === 0 ? (
                            <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '3rem' }}>
                                <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>
                                    {publication?.status === 'approved' ? 'Không có chương nào đã xuất bản' : publication?.status === 'rejected' ? 'Không có chương nào' : 'Không có chương nào đang chờ kiểm duyệt'}
                                </p>
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
                                                {publication?.status === 'approved' ? 'Chương đã xuất bản' : publication?.status === 'rejected' ? 'Chương' : 'Chương chờ duyệt'}
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
                                                    {publication?.status === 'approved' && chapter.publishedAt && (
                                                        <div style={{ fontSize: '0.6875rem', color: '#10b981', marginTop: '0.25rem' }}>
                                                            Duyệt: {formatDate(chapter.publishedAt)}
                                                        </div>
                                                    )}
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
                                                {publication?.status === 'approved' && selectedChapter.publishedAt && (
                                                    <div style={{ fontSize: '0.8125rem', color: '#10b981', marginTop: '0.375rem' }}>
                                                        Duyệt lúc: {formatDate(selectedChapter.publishedAt)}
                                                    </div>
                                                )}
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

                    {/* Footer - Actions - Chỉ hiển thị khi đang chờ duyệt (có chương chờ duyệt), không hiện khi xem lịch sử đã duyệt/từ chối */}
                    {chapters.length > 0 && !showRejectForm && publication?.status === 'pending' && (
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
                                onClick={openApproveConfirm}
                                disabled={isSubmitting || !selectedChapter}
                                style={{
                                    padding: '0.75rem 1.5rem',
                                    backgroundColor: '#13ec5b',
                                    color: '#ffffff',
                                    fontSize: '0.875rem',
                                    fontWeight: 700,
                                    borderRadius: '8px',
                                    border: 'none',
                                    cursor: (isSubmitting || !selectedChapter) ? 'not-allowed' : 'pointer',
                                    transition: 'all 0.2s',
                                    opacity: (isSubmitting || !selectedChapter) ? 0.5 : 1,
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: '0.5rem'
                                }}
                                onMouseEnter={(e) => {
                                    if (!isSubmitting && selectedChapter) {
                                        e.currentTarget.style.backgroundColor = '#10d954';
                                    }
                                }}
                                onMouseLeave={(e) => {
                                    if (!isSubmitting && selectedChapter) {
                                        e.currentTarget.style.backgroundColor = '#13ec5b';
                                    }
                                }}
                            >
                                <CheckCircle style={{ width: '18px', height: '18px' }} />
                                {isSubmitting ? 'Đang xử lý...' : 'Duyệt chương'}
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
                                    onClick={openRejectConfirm}
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

                    {/* Already Reviewed Info - Chỉ hiển thị khi không còn chương chờ duyệt. Ẩn nếu vừa từ chối trong phiên để moderator duyệt liên tiếp không bị hiện lại lý do từ chối */}
                    {chapters.length === 0 && publication.status !== 'pending' && !justRejectedInSessionRef.current && (
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

            {/* Dialog xác nhận duyệt xuất bản */}
            {showApproveConfirm && (
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
                        zIndex: 10000
                    }}
                    onClick={() => setShowApproveConfirm(false)}
                >
                    <div
                        style={{
                            backgroundColor: '#ffffff',
                            borderRadius: '12px',
                            padding: '1.5rem',
                            maxWidth: '400px',
                            width: '90%',
                            boxShadow: '0 20px 60px rgba(0, 0, 0, 0.3)'
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <h3 style={{ fontSize: '1.125rem', fontWeight: 600, color: '#1e293b', margin: '0 0 1rem 0' }}>
                            Xác nhận duyệt chương
                        </h3>
                        <p style={{ fontSize: '0.875rem', color: '#64748b', margin: '0 0 1.5rem 0', lineHeight: 1.5 }}>
                            Bạn có chắc chắn muốn duyệt xuất bản chương "{selectedChapter?.title}"?
                        </p>
                        <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                            <button
                                onClick={() => setShowApproveConfirm(false)}
                                style={{
                                    padding: '0.625rem 1.25rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    color: '#64748b',
                                    backgroundColor: '#f1f5f9',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: 'pointer'
                                }}
                            >
                                Hủy
                            </button>
                            <button
                                onClick={handleApproveConfirm}
                                style={{
                                    padding: '0.625rem 1.25rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    color: '#ffffff',
                                    backgroundColor: '#13ec5b',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: 'pointer'
                                }}
                            >
                                Xác nhận
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Popup xác nhận từ chối duyệt */}
            {showRejectConfirm && (
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
                        zIndex: 10000
                    }}
                    onClick={() => setShowRejectConfirm(false)}
                >
                    <div
                        style={{
                            backgroundColor: '#ffffff',
                            borderRadius: '12px',
                            padding: '1.5rem',
                            maxWidth: '400px',
                            width: '90%',
                            boxShadow: '0 20px 60px rgba(0, 0, 0, 0.3)'
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <h3 style={{ fontSize: '1.125rem', fontWeight: 600, color: '#1e293b', margin: '0 0 1rem 0' }}>
                            Xác nhận từ chối duyệt
                        </h3>
                        <p style={{ fontSize: '0.875rem', color: '#64748b', margin: '0 0 1.5rem 0', lineHeight: 1.5 }}>
                            Bạn có chắc muốn từ chối xuất bản này? Hành động này không thể hoàn tác.
                        </p>
                        <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                            <button
                                onClick={() => setShowRejectConfirm(false)}
                                style={{
                                    padding: '0.625rem 1.25rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    color: '#64748b',
                                    backgroundColor: '#f1f5f9',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: 'pointer'
                                }}
                            >
                                Hủy
                            </button>
                            <button
                                onClick={handleRejectSubmit}
                                style={{
                                    padding: '0.625rem 1.25rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    color: '#ffffff',
                                    backgroundColor: '#ef4444',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: 'pointer'
                                }}
                            >
                                Xác nhận
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </>
    );
}
