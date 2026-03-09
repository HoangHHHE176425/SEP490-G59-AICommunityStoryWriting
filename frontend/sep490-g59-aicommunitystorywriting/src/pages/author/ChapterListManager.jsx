import { useState, useEffect, useRef, useCallback } from 'react';
import { Plus, Eye, MessageSquare, Book, ListOrdered, Send, Undo2, Pencil, Trash2, ArrowLeft, AlertCircle } from 'lucide-react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { getChapters, getChapterById, updateChapter, unpublishChapter, getChapterRejectionReason } from '../../api/chapter/chapterApi';
import { getStoryRejectionReason } from '../../api/story/storyApi';
import { updateStory } from '../../api/story/storyApi';
import { Pagination } from '../../components/pagination/Pagination';

const CHAPTER_STATUS_MAP = {
    DRAFT: 'Bản nháp',
    PENDING_REVIEW: 'Chờ duyệt',
    REJECTED: 'Bị từ chối',
    PUBLISHED: 'Đã xuất bản',
    HIDDEN: 'Đã ẩn',
    ARCHIVED: 'Đã lưu trữ',
};

/** Màu trạng thái đồng bộ với màn Truyện của tôi: draft/pending = vàng, published = xanh lá */
function getChapterStatusStyle(status) {
    const s = (status || '').toLowerCase();
    if (s === 'published') return { backgroundColor: '#d1fae5', color: '#065f46' };
    if (s === 'draft' || s === 'pending_review') return { backgroundColor: '#fef3c7', color: '#92400e' };
    if (s === 'rejected') return { backgroundColor: '#fef2f2', color: '#b91c1c' };
    return { backgroundColor: '#f3f4f6', color: '#6b7280' };
}

function mapChapterFromApi(item) {
    const createdAt = item.createdAt ?? item.CreatedAt ?? item.publishedAt ?? item.PublishedAt;
    const updatedAt = createdAt
        ? new Date(createdAt).toLocaleString('vi-VN', { hour: '2-digit', minute: '2-digit', second: '2-digit', day: '2-digit', month: '2-digit', year: 'numeric' })
        : '';
    const status = (item.status ?? item.Status ?? 'DRAFT').toUpperCase();
    const statusDisplay = CHAPTER_STATUS_MAP[status] ?? status;
    const accessTypeApi = (item.accessType ?? item.AccessType ?? 'FREE').toUpperCase();
    const accessType = accessTypeApi === 'PAID' ? 'paid' : 'public';
    const price = item.coinPrice ?? item.CoinPrice ?? 0;
    return {
        id: item.id ?? item.Id,
        number: (item.orderIndex ?? item.OrderIndex ?? 0) + 1,
        title: item.title ?? item.Title ?? '',
        content: '',
        status: status.toLowerCase(),
        statusDisplay,
        accessType,
        price,
        views: 0,
        comments: 0,
        likes: 0,
        updatedAt,
    };
}

const CHAPTERS_PAGE_SIZE = 10;

export function ChapterListManager({ story, onBack, onAddChapter, onEditChapter }) {
    const storyId = story?.id ?? story?.Id;
    const [chapters, setChapters] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [currentPage, setCurrentPage] = useState(1);
    const [totalPages, setTotalPages] = useState(1);
    const [totalCount, setTotalCount] = useState(0);
    const [hasPublishedChapter, setHasPublishedChapter] = useState(false);
    const [hasPendingReviewChapter, setHasPendingReviewChapter] = useState(false);

    const loadChapters = useCallback((page = 1, options = {}) => {
        if (!storyId) return;
        const silent = options.silent === true;
        if (!silent) {
            setLoading(true);
            setError(null);
        }
        getChapters({ storyId, page, pageSize: CHAPTERS_PAGE_SIZE })
            .then((res) => {
                const rawItems = Array.isArray(res) ? res : (res?.items ?? res?.Items ?? []);
                const total = res?.totalCount ?? res?.totalItems ?? res?.total ?? rawItems.length;
                const pages = res?.totalPages ?? Math.max(1, Math.ceil(total / CHAPTERS_PAGE_SIZE));
                setChapters(rawItems.map((item) => ({ ...mapChapterFromApi(item), content: item.content ?? item.Content ?? '' })));
                setTotalCount(total);
                setTotalPages(pages);
                setCurrentPage(res?.page ?? page);
                if (silent) {
                    Promise.all([
                        getChapters({ storyId, status: 'PUBLISHED', pageSize: 1 }),
                        getChapters({ storyId, status: 'PENDING_REVIEW', pageSize: 1 })
                    ]).then(([publishedRes, pendingRes]) => {
                        const publishedList = Array.isArray(publishedRes) ? publishedRes : (publishedRes?.items ?? publishedRes?.Items ?? []);
                        const pendingList = Array.isArray(pendingRes) ? pendingRes : (pendingRes?.items ?? pendingRes?.Items ?? []);
                        setHasPublishedChapter(publishedList.length > 0);
                        setHasPendingReviewChapter(pendingList.length > 0);
                    }).catch(() => { });
                }
            })
            .catch((err) => {
                if (!silent) {
                    setError(err?.message ?? 'Không tải được danh sách chương');
                    setChapters([]);
                    setTotalCount(0);
                    setTotalPages(1);
                }
            })
            .finally(() => { if (!silent) setLoading(false); });
    }, [storyId]);

    useEffect(() => {
        let cancelled = false;
        const id = setTimeout(() => {
            if (!storyId) {
                setChapters([]);
                setLoading(false);
                setTotalCount(0);
                setTotalPages(1);
                setHasPublishedChapter(false);
                setHasPendingReviewChapter(false);
                return;
            }
            setLoading(true);
            setError(null);
            Promise.all([
                getChapters({ storyId, page: 1, pageSize: CHAPTERS_PAGE_SIZE }),
                getChapters({ storyId, status: 'PUBLISHED', pageSize: 1 }),
                getChapters({ storyId, status: 'PENDING_REVIEW', pageSize: 1 })
            ])
                .then(([res, publishedRes, pendingRes]) => {
                    const rawItems = Array.isArray(res) ? res : (res?.items ?? res?.Items ?? []);
                    const total = res?.totalCount ?? res?.totalItems ?? res?.total ?? rawItems.length;
                    const pages = res?.totalPages ?? Math.max(1, Math.ceil(total / CHAPTERS_PAGE_SIZE));
                    const publishedList = Array.isArray(publishedRes) ? publishedRes : (publishedRes?.items ?? publishedRes?.Items ?? []);
                    const pendingList = Array.isArray(pendingRes) ? pendingRes : (pendingRes?.items ?? pendingRes?.Items ?? []);
                    if (!cancelled) {
                        setChapters(rawItems.map((item) => ({ ...mapChapterFromApi(item), content: item.content ?? item.Content ?? '' })));
                        setTotalCount(total);
                        setTotalPages(pages);
                        setCurrentPage(res?.page ?? 1);
                        setHasPublishedChapter(publishedList.length > 0);
                        setHasPendingReviewChapter(pendingList.length > 0);
                    }
                })
                .catch((err) => {
                    if (!cancelled) {
                        setError(err?.message ?? 'Không tải được danh sách chương');
                        setChapters([]);
                        setTotalCount(0);
                        setTotalPages(1);
                        setHasPublishedChapter(false);
                        setHasPendingReviewChapter(false);
                    }
                })
                .finally(() => {
                    if (!cancelled) setLoading(false);
                });
        }, 0);
        return () => {
            cancelled = true;
            clearTimeout(id);
        };
    }, [storyId]);

    /** Real-time: refetch danh sách chương khi tab đang hiển thị (moderator duyệt/từ chối bên tab kia). Backend chỉ có ModeratorHub nên tác giả dùng polling. */
    const POLL_INTERVAL_MS = 1000;
    const currentPageRef = useRef(currentPage);
    const loadChaptersRef = useRef(loadChapters);
    useEffect(() => {
        currentPageRef.current = currentPage;
        loadChaptersRef.current = loadChapters;
    }, [currentPage, loadChapters]);
    useEffect(() => {
        if (!storyId) return;
        const tick = () => {
            if (typeof document !== 'undefined' && document.visibilityState === 'visible') {
                loadChaptersRef.current?.(currentPageRef.current, { silent: true });
            }
        };
        const id = setInterval(tick, POLL_INTERVAL_MS);
        return () => clearInterval(id);
    }, [storyId]);

    const handlePageChange = (page) => {
        setCurrentPage(page);
        loadChapters(page);
    };

    const [actioningChapterId, setActioningChapterId] = useState(null);
    const [confirmDialog, setConfirmDialog] = useState({ open: false, action: null, chapterId: null });
    const [rejectionReasonModal, setRejectionReasonModal] = useState({ open: false, title: '', reason: null, rejectedAt: null, loading: false });

    const handleDeleteChapter = (chapterId) => {
        if (window.confirm('Bạn có chắc chắn muốn xóa chương này?')) {
            setChapters((prev) => prev.filter((ch) => ch.id !== chapterId));
        }
    };

    const openPublishConfirm = (chapterId) => {
        setConfirmDialog({ open: true, action: 'publish', chapterId });
    };
    const openUnpublishConfirm = (chapterId) => {
        setConfirmDialog({ open: true, action: 'unpublish', chapterId });
    };
    const closeConfirmDialog = () => {
        if (!actioningChapterId) setConfirmDialog({ open: false, action: null, chapterId: null });
    };

    const handleConfirmAction = () => {
        const { action, chapterId } = confirmDialog;
        if (!chapterId) return;
        const chapterFromList = chapters.find((c) => c.id === chapterId);
        setActioningChapterId(chapterId);
        setConfirmDialog({ open: false, action: null, chapterId: null });
        if (action === 'publish') {
            const doUpdate = (title, content) =>
                updateChapter(chapterId, { title, content, status: 'PENDING_REVIEW' })
                    .then(() => loadChapters(currentPage))
                    .then(() => {
                        if (!storyId) return;
                        return Promise.all([
                            getChapters({ storyId, status: 'PUBLISHED', pageSize: 1 }),
                            getChapters({ storyId, status: 'PENDING_REVIEW', pageSize: 1 })
                        ]).then(([rPub, rPend]) => {
                            const pubList = Array.isArray(rPub) ? rPub : (rPub?.items ?? rPub?.Items ?? []);
                            const pendList = Array.isArray(rPend) ? rPend : (rPend?.items ?? rPend?.Items ?? []);
                            setHasPublishedChapter(pubList.length > 0);
                            setHasPendingReviewChapter(pendList.length > 0);
                        });
                    })
                    .then(() => {
                        if (!storyId || !story) return;
                        const categoryIds = (story.categories || []).map((c) => (typeof c === 'object' && c != null ? c.id : c)).filter((id) => id && /^[0-9a-fA-F-]{36}$/.test(String(id)));
                        return updateStory(storyId, {
                            title: story.title || 'Untitled',
                            summary: story.summary ?? '',
                            categoryIds,
                            status: 'PENDING_REVIEW',
                            ageRating: story.ageRating ?? 'Phù hợp mọi lứa tuổi',
                            storyProgressStatus: story.progressStatusDisplay ?? story.storyProgressStatus ?? 'Đang ra'
                        });
                    })
                    .catch((err) => {
                        const msg = err?.response?.data?.message ?? err?.message ?? 'Xuất bản thất bại';
                        alert(msg);
                    })
                    .finally(() => setActioningChapterId(null));
            getChapterById(chapterId)
                .then((fullCh) => {
                    const title = (fullCh?.title ?? fullCh?.Title ?? chapterFromList?.title ?? '').trim();
                    const content = fullCh?.content ?? fullCh?.Content ?? chapterFromList?.content ?? '';
                    if (!title) throw new Error('Không lấy được tiêu đề chương');
                    return doUpdate(title, content);
                })
                .catch((err) => {
                    if (chapterFromList?.title) {
                        return doUpdate(chapterFromList.title, chapterFromList.content ?? '');
                    }
                    const msg = err?.response?.data?.message ?? err?.message ?? 'Xuất bản thất bại';
                    alert(msg);
                    setActioningChapterId(null);
                });
        } else if (action === 'unpublish') {
            unpublishChapter(chapterId)
                .then(() => loadChapters(currentPage))
                .then(() => {
                    if (!storyId) return;
                    return Promise.all([
                        getChapters({ storyId, status: 'PUBLISHED', pageSize: 1 }),
                        getChapters({ storyId, status: 'PENDING_REVIEW', pageSize: 1 })
                    ]).then(([rPub, rPend]) => {
                        const pubList = Array.isArray(rPub) ? rPub : (rPub?.items ?? rPub?.Items ?? []);
                        const pendList = Array.isArray(rPend) ? rPend : (rPend?.items ?? rPend?.Items ?? []);
                        setHasPublishedChapter(pubList.length > 0);
                        setHasPendingReviewChapter(pendList.length > 0);
                    });
                })
                .catch((err) => {
                    alert(err?.message ?? 'Hủy xuất bản thất bại');
                })
                .finally(() => setActioningChapterId(null));
        }
    };

    const handlePublishChapter = (chapterId) => {
        openPublishConfirm(chapterId);
    };
    const handleUnpublishChapter = (chapterId) => {
        openUnpublishConfirm(chapterId);
    };

    // Trạng thái truyện: PUBLISHED nếu có ≥1 chương PUBLISHED; nếu không thì PENDING_REVIEW nếu có ≥1 chương PENDING_REVIEW; còn lại Bản nháp / Bị từ chối
    const isStoryRejected = story?.status === 'rejected' || (story?.publishStatus && String(story.publishStatus).includes('từ chối'));
    const derivedStoryStatusDisplay = isStoryRejected ? 'Bị từ chối' : hasPublishedChapter ? 'Đã xuất bản' : hasPendingReviewChapter ? 'Chờ duyệt' : 'Bản nháp';
    const derivedStatusKind = isStoryRejected ? 'rejected' : hasPublishedChapter ? 'published' : hasPendingReviewChapter ? 'pending_review' : 'draft';

    const openStoryRejectionReason = () => {
        if (!storyId) return;
        setRejectionReasonModal({ open: true, title: 'Lý do từ chối truyện', reason: null, rejectedAt: null, loading: true });
        getStoryRejectionReason(storyId)
            .then((data) => setRejectionReasonModal(prev => ({ ...prev, reason: data?.reason ?? null, rejectedAt: data?.rejectedAt ?? null, loading: false })))
            .catch(() => setRejectionReasonModal(prev => ({ ...prev, reason: null, rejectedAt: null, loading: false })));
    };

    const openChapterRejectionReason = (chapterTitle, chapterId) => {
        setRejectionReasonModal({ open: true, title: `Lý do từ chối: ${chapterTitle || 'Chương'}`, reason: null, rejectedAt: null, loading: true });
        getChapterRejectionReason(chapterId)
            .then((data) => setRejectionReasonModal(prev => ({ ...prev, reason: data?.reason ?? null, rejectedAt: data?.rejectedAt ?? null, loading: false })))
            .catch(() => setRejectionReasonModal(prev => ({ ...prev, reason: null, rejectedAt: null, loading: false })));
    };

    const closeRejectionReasonModal = () => setRejectionReasonModal(prev => ({ ...prev, open: false }));

    return (
        <div>
            <Header />
            <div style={{ minHeight: '100vh', backgroundColor: '#f5f5f5', padding: '2rem' }}>
                <div style={{ maxWidth: '1400px', margin: '0 auto' }}>
                    <>
                        {/* Header - format đồng bộ với hệ thống */}
                        <div style={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            flexWrap: 'wrap',
                            gap: '1rem',
                            marginBottom: '1.75rem',
                            padding: '1.25rem 1.5rem',
                            backgroundColor: '#ffffff',
                            borderRadius: '16px',
                            border: '1px solid #e5e7eb',
                            boxShadow: '0 1px 3px rgba(0,0,0,0.06)'
                        }}>
                            <div style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '1rem',
                                flex: '1 1 0',
                                minWidth: 0,
                                overflow: 'hidden'
                            }}>
                                <div style={{
                                    width: '48px',
                                    height: '48px',
                                    borderRadius: '12px',
                                    background: 'linear-gradient(135deg, #6366f1 0%, #4f46e5 100%)',
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    boxShadow: '0 4px 12px rgba(99, 102, 241, 0.3)',
                                    flexShrink: 0
                                }}>
                                    <ListOrdered style={{ width: '26px', height: '26px', color: '#ffffff' }} />
                                </div>
                                <div style={{ minWidth: 0 }}>
                                    <h2 style={{
                                        fontFamily: "'Plus Jakarta Sans', sans-serif",
                                        fontSize: '1.5rem',
                                        fontWeight: 700,
                                        color: '#1A2332',
                                        margin: 0,
                                        letterSpacing: '-0.02em',
                                        lineHeight: 1.3,
                                        overflow: 'hidden',
                                        textOverflow: 'ellipsis',
                                        whiteSpace: 'nowrap'
                                    }}>
                                        Danh sách chương
                                    </h2>
                                    <p style={{
                                        fontFamily: "'Plus Jakarta Sans', sans-serif",
                                        fontSize: '0.875rem',
                                        color: '#90A1B9',
                                        margin: '4px 0 0 0',
                                        fontWeight: 400,
                                        overflow: 'hidden',
                                        textOverflow: 'ellipsis',
                                        whiteSpace: 'nowrap'
                                    }}>
                                        {story?.title || 'Chưa có tiêu đề'}
                                    </p>
                                    <span style={{
                                        fontFamily: "'Plus Jakarta Sans', sans-serif",
                                        fontSize: '0.75rem',
                                        fontWeight: 600,
                                        color: derivedStatusKind === 'published' ? '#065f46' : derivedStatusKind === 'rejected' ? '#b91c1c' : '#92400e',
                                        backgroundColor: derivedStatusKind === 'published' ? '#d1fae5' : derivedStatusKind === 'rejected' ? '#fee2e2' : '#fef3c7',
                                        padding: '4px 10px',
                                        borderRadius: '8px',
                                        marginTop: '8px',
                                        display: 'inline-block'
                                    }}>
                                        Trạng thái truyện: {derivedStoryStatusDisplay}
                                    </span>
                                    {isStoryRejected && (
                                        <button
                                            type="button"
                                            onClick={openStoryRejectionReason}
                                            style={{
                                                display: 'inline-flex',
                                                alignItems: 'center',
                                                gap: '0.375rem',
                                                marginTop: '8px',
                                                marginLeft: '8px',
                                                padding: '0.375rem 0.75rem',
                                                fontSize: '0.75rem',
                                                fontWeight: 600,
                                                color: '#b91c1c',
                                                backgroundColor: '#fef2f2',
                                                border: '1px solid #fecaca',
                                                borderRadius: '8px',
                                                cursor: 'pointer'
                                            }}
                                        >
                                            <AlertCircle size={14} />
                                            Xem lý do từ chối
                                        </button>
                                    )}
                                </div>
                            </div>
                            <button
                                onClick={() => onAddChapter?.(story)}
                                style={{
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: '0.5rem',
                                    padding: '0.75rem 1.5rem',
                                    backgroundColor: '#13ec5b',
                                    border: 'none',
                                    borderRadius: '9999px',
                                    fontSize: '0.875rem',
                                    fontWeight: 700,
                                    color: '#ffffff',
                                    cursor: 'pointer',
                                    transition: 'all 0.2s',
                                    flexShrink: 0,
                                    whiteSpace: 'nowrap',
                                    boxShadow: '0 2px 8px rgba(19, 236, 91, 0.3)',
                                    fontFamily: "'Plus Jakarta Sans', sans-serif"
                                }}
                                onMouseEnter={(e) => {
                                    e.currentTarget.style.backgroundColor = '#10d452';
                                    e.currentTarget.style.transform = 'translateY(-1px)';
                                    e.currentTarget.style.boxShadow = '0 4px 12px rgba(19, 236, 91, 0.35)';
                                }}
                                onMouseLeave={(e) => {
                                    e.currentTarget.style.backgroundColor = '#13ec5b';
                                    e.currentTarget.style.transform = 'translateY(0)';
                                    e.currentTarget.style.boxShadow = '0 2px 8px rgba(19, 236, 91, 0.3)';
                                }}
                            >
                                <Plus style={{ width: '18px', height: '18px' }} />
                                Thêm chương mới
                            </button>
                        </div>

                        {/* Chapter Table */}
                        <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', border: '1px solid #e0e0e0', overflow: 'hidden' }}>
                            {/* Table Header */}
                            <div style={{
                                display: 'grid',
                                gridTemplateColumns: '90px 1fr 320px',
                                padding: '1rem 1.5rem',
                                backgroundColor: '#f9fafb',
                                borderBottom: '1px solid #e0e0e0',
                                fontWeight: 600,
                                fontSize: '0.875rem',
                                color: '#6b7280'
                            }}>
                                <div>Thứ tự</div>
                                <div>Tên chương</div>
                                <div style={{ textAlign: 'center' }}>Hành động</div>
                            </div>

                            {/* Table Body */}
                            {loading ? (
                                <div style={{ padding: '3rem', textAlign: 'center' }}>
                                    <p style={{ fontSize: '0.875rem', color: '#6b7280' }}>Đang tải danh sách chương...</p>
                                </div>
                            ) : error ? (
                                <div style={{ padding: '3rem', textAlign: 'center' }}>
                                    <p style={{ fontSize: '0.875rem', color: '#dc2626', marginBottom: '1rem' }}>{error}</p>
                                    <button
                                        onClick={() => loadChapters(1)}
                                        style={{ padding: '0.5rem 1rem', fontSize: '0.875rem', cursor: 'pointer' }}
                                    >
                                        Thử lại
                                    </button>
                                </div>
                            ) : chapters.length === 0 ? (
                                <div style={{
                                    padding: '3rem',
                                    textAlign: 'center'
                                }}>
                                    <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>📖</div>
                                    <h3 style={{ fontSize: '1.125rem', color: '#6b7280', marginBottom: '0.5rem' }}>
                                        Chưa có chương nào
                                    </h3>
                                    <p style={{ fontSize: '0.875rem', color: '#9ca3af', marginBottom: '1.5rem' }}>
                                        Bắt đầu viết chương đầu tiên cho truyện của bạn
                                    </p>
                                    <button
                                        onClick={() => onAddChapter?.(story)}
                                        style={{
                                            padding: '0.75rem 1.5rem',
                                            backgroundColor: '#13ec5b',
                                            border: 'none',
                                            borderRadius: '9999px',
                                            fontSize: '0.875rem',
                                            fontWeight: 700,
                                            color: '#ffffff',
                                            cursor: 'pointer'
                                        }}
                                    >
                                        Thêm chương mới
                                    </button>
                                </div>
                            ) : (
                                chapters.map((chapter, index) => (
                                    <div
                                        key={chapter.id}
                                        style={{
                                            display: 'grid',
                                            gridTemplateColumns: '90px 1fr 320px',
                                            padding: '1rem 1.5rem',
                                            alignItems: 'center',
                                            borderBottom: index < chapters.length - 1 ? '1px solid #f3f4f6' : 'none',
                                            transition: 'background-color 0.2s'
                                        }}
                                        onMouseEnter={(e) => {
                                            e.currentTarget.style.backgroundColor = '#fafafa';
                                        }}
                                        onMouseLeave={(e) => {
                                            e.currentTarget.style.backgroundColor = '#ffffff';
                                        }}
                                    >
                                        {/* Order */}
                                        <div>
                                            <span style={{ fontSize: '0.9375rem', fontWeight: 600, color: '#334155' }}>
                                                Chương {chapter.number}
                                            </span>
                                        </div>

                                        {/* Title and Info */}
                                        <div style={{ display: 'flex', flexDirection: 'column', gap: '0.375rem', minWidth: 0 }}>
                                            <div style={{ fontSize: '0.9375rem', fontWeight: 600, color: '#1e293b', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                                {chapter.title}
                                            </div>
                                            <div style={{ display: 'flex', alignItems: 'center', flexWrap: 'wrap', gap: '0.5rem' }}>
                                                <span style={{
                                                    padding: '0.15rem 0.5rem',
                                                    borderRadius: '9999px',
                                                    fontSize: '0.6875rem',
                                                    fontWeight: 600,
                                                    ...getChapterStatusStyle(chapter.status)
                                                }}>
                                                    {chapter.statusDisplay}
                                                </span>
                                                {chapter.status === 'rejected' && (
                                                    <button
                                                        type="button"
                                                        onClick={() => openChapterRejectionReason(chapter.title, chapter.id)}
                                                        style={{
                                                            padding: '0.15rem 0.5rem',
                                                            fontSize: '0.6875rem',
                                                            fontWeight: 600,
                                                            color: '#b91c1c',
                                                            backgroundColor: 'transparent',
                                                            border: '1px solid #fecaca',
                                                            borderRadius: '6px',
                                                            cursor: 'pointer'
                                                        }}
                                                    >
                                                        Lý do từ chối
                                                    </button>
                                                )}
                                                <span style={{ fontSize: '0.6875rem', color: '#94a3b8' }}>{chapter.updatedAt}</span>
                                                <span style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.6875rem', color: '#64748b' }}>
                                                    <Eye size={11} /> {chapter.views}
                                                    <MessageSquare size={11} /> {chapter.comments}
                                                    👍 {chapter.likes}
                                                </span>
                                            </div>
                                        </div>

                                        {/* Actions: hàng 1 = Chỉnh sửa, Xóa; hàng 2 = Xuất bản/Hủy xuất bản = tổng width + gap của hàng 1 */}
                                        <div style={{
                                            display: 'flex',
                                            flexDirection: 'column',
                                            alignItems: 'stretch',
                                            justifyContent: 'center',
                                            gap: '0.5rem',
                                            width: 'fit-content',
                                            margin: '0 auto'
                                        }}>
                                            {/* Hàng 1: Chỉnh sửa, Xóa — không cho phép khi chương đang Chờ duyệt (PENDING_REVIEW) */}
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                                <button
                                                    onClick={() => chapter.status !== 'pending_review' && onEditChapter(chapter)}
                                                    disabled={chapter.status === 'pending_review'}
                                                    title={chapter.status === 'pending_review' ? 'Chương đang chờ duyệt, không thể chỉnh sửa' : ''}
                                                    style={{
                                                        display: 'inline-flex',
                                                        alignItems: 'center',
                                                        gap: '0.25rem',
                                                        padding: '0.4rem 0.75rem',
                                                        backgroundColor: chapter.status === 'pending_review' ? '#f1f5f9' : '#f0fdf4',
                                                        border: `1px solid ${chapter.status === 'pending_review' ? '#e2e8f0' : '#86efac'}`,
                                                        borderRadius: '9999px',
                                                        fontSize: '0.75rem',
                                                        fontWeight: 600,
                                                        color: chapter.status === 'pending_review' ? '#94a3b8' : '#15803d',
                                                        cursor: chapter.status === 'pending_review' ? 'not-allowed' : 'pointer',
                                                        transition: 'all 0.2s',
                                                        whiteSpace: 'nowrap',
                                                        opacity: chapter.status === 'pending_review' ? 0.8 : 1
                                                    }}
                                                    onMouseEnter={(e) => { if (chapter.status !== 'pending_review') e.currentTarget.style.backgroundColor = '#dcfce7'; }}
                                                    onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = chapter.status === 'pending_review' ? '#f1f5f9' : '#f0fdf4'; }}
                                                >
                                                    <Pencil size={12} />
                                                    Chỉnh sửa
                                                </button>
                                                <button
                                                    onClick={() => chapter.status !== 'pending_review' && handleDeleteChapter(chapter.id)}
                                                    disabled={chapter.status === 'pending_review'}
                                                    title={chapter.status === 'pending_review' ? 'Chương đang chờ duyệt, không thể xóa' : ''}
                                                    style={{
                                                        display: 'inline-flex',
                                                        alignItems: 'center',
                                                        gap: '0.25rem',
                                                        padding: '0.4rem 0.75rem',
                                                        backgroundColor: chapter.status === 'pending_review' ? '#f1f5f9' : '#fff',
                                                        border: `1px solid ${chapter.status === 'pending_review' ? '#e2e8f0' : '#fecaca'}`,
                                                        borderRadius: '9999px',
                                                        fontSize: '0.75rem',
                                                        fontWeight: 600,
                                                        color: chapter.status === 'pending_review' ? '#94a3b8' : '#dc2626',
                                                        cursor: chapter.status === 'pending_review' ? 'not-allowed' : 'pointer',
                                                        transition: 'all 0.2s',
                                                        whiteSpace: 'nowrap',
                                                        opacity: chapter.status === 'pending_review' ? 0.8 : 1
                                                    }}
                                                    onMouseEnter={(e) => { if (chapter.status !== 'pending_review') e.currentTarget.style.backgroundColor = '#fef2f2'; }}
                                                    onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = chapter.status === 'pending_review' ? '#f1f5f9' : '#fff'; }}
                                                >
                                                    <Trash2 size={12} />
                                                    Xóa
                                                </button>
                                            </div>
                                            {/* Hàng 2: Xuất bản hoặc Hủy xuất bản (draft/rejected = Xuất bản; pending_review = Hủy xuất bản) */}
                                            {(chapter.status === 'draft' || chapter.status === 'pending_review' || chapter.status === 'rejected') && (
                                                <div style={{ display: 'flex', width: '100%' }}>
                                                    {(chapter.status === 'draft' || chapter.status === 'rejected') && (
                                                        <button
                                                            onClick={() => handlePublishChapter(chapter.id)}
                                                            disabled={actioningChapterId === chapter.id}
                                                            style={{
                                                                display: 'inline-flex',
                                                                alignItems: 'center',
                                                                justifyContent: 'center',
                                                                gap: '0.25rem',
                                                                width: '100%',
                                                                padding: '0.4rem 0.75rem',
                                                                backgroundColor: '#13ec5b',
                                                                border: 'none',
                                                                borderRadius: '9999px',
                                                                fontSize: '0.75rem',
                                                                fontWeight: 600,
                                                                color: '#fff',
                                                                cursor: actioningChapterId === chapter.id ? 'not-allowed' : 'pointer',
                                                                opacity: actioningChapterId === chapter.id ? 0.7 : 1,
                                                                transition: 'all 0.2s',
                                                                whiteSpace: 'nowrap'
                                                            }}
                                                        >
                                                            <Send size={12} />
                                                            {actioningChapterId === chapter.id ? '...' : 'Xuất bản'}
                                                        </button>
                                                    )}
                                                    {chapter.status === 'pending_review' && (
                                                        <button
                                                            onClick={() => handleUnpublishChapter(chapter.id)}
                                                            disabled={actioningChapterId === chapter.id}
                                                            style={{
                                                                display: 'inline-flex',
                                                                alignItems: 'center',
                                                                justifyContent: 'center',
                                                                gap: '0.25rem',
                                                                width: '100%',
                                                                padding: '0.4rem 0.75rem',
                                                                backgroundColor: '#fff',
                                                                border: '1px solid #f59e0b',
                                                                borderRadius: '9999px',
                                                                fontSize: '0.75rem',
                                                                fontWeight: 600,
                                                                color: '#b45309',
                                                                cursor: actioningChapterId === chapter.id ? 'not-allowed' : 'pointer',
                                                                opacity: actioningChapterId === chapter.id ? 0.7 : 1,
                                                                transition: 'all 0.2s',
                                                                whiteSpace: 'nowrap'
                                                            }}
                                                        >
                                                            <Undo2 size={12} />
                                                            {actioningChapterId === chapter.id ? '...' : 'Hủy xuất bản'}
                                                        </button>
                                                    )}
                                                </div>
                                            )}
                                        </div>
                                    </div>
                                ))
                            )}
                        </div>

                        {/* Pagination */}
                        {!loading && !error && totalPages > 1 && (
                            <Pagination
                                currentPage={currentPage}
                                totalPages={totalPages}
                                totalItems={totalCount}
                                itemsPerPage={CHAPTERS_PAGE_SIZE}
                                onPageChange={handlePageChange}
                                itemLabel="chương"
                            />
                        )}

                        {/* Back Button - rõ ràng, có viền và màu nền nhìn thấy ngay */}
                        <div style={{ marginTop: '2rem' }}>
                            <button
                                onClick={onBack}
                                style={{
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: '0.5rem',
                                    padding: '0.5rem 1rem',
                                    backgroundColor: '#e2e8f0',
                                    color: '#0f172a',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    borderRadius: '9999px',
                                    border: '1px solid #cbd5e1',
                                    cursor: 'pointer',
                                    transition: 'all 0.2s'
                                }}
                                onMouseEnter={(e) => {
                                    e.currentTarget.style.backgroundColor = '#cbd5e1';
                                    e.currentTarget.style.borderColor = '#94a3b8';
                                }}
                                onMouseLeave={(e) => {
                                    e.currentTarget.style.backgroundColor = '#e2e8f0';
                                    e.currentTarget.style.borderColor = '#cbd5e1';
                                }}
                            >
                                <ArrowLeft style={{ width: '16px', height: '16px' }} />
                                Quay lại
                            </button>
                        </div>
                    </>
                </div>
            </div>
            <Footer />

            {/* Dialog xác nhận Xuất bản / Hủy xuất bản (cùng format với dialog duyệt chương) */}
            {confirmDialog.open && (
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
                    onClick={closeConfirmDialog}
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
                            {confirmDialog.action === 'publish' ? 'Xác nhận xuất bản' : 'Xác nhận hủy xuất bản'}
                        </h3>
                        <p style={{ fontSize: '0.875rem', color: '#64748b', margin: '0 0 1.5rem 0', lineHeight: 1.5 }}>
                            {confirmDialog.action === 'publish'
                                ? 'Bạn có chắc chắn muốn gửi chương này lên để duyệt xuất bản?'
                                : 'Bạn có chắc chắn muốn hủy xuất bản và đưa chương về bản nháp?'}
                        </p>
                        <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                            <button
                                onClick={closeConfirmDialog}
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
                                onClick={handleConfirmAction}
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

            {/* Modal lý do từ chối (truyện hoặc chương) */}
            {rejectionReasonModal.open && (
                <div
                    style={{
                        position: 'fixed',
                        inset: 0,
                        backgroundColor: 'rgba(0,0,0,0.5)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 10001,
                        padding: '1rem'
                    }}
                    onClick={closeRejectionReasonModal}
                >
                    <div
                        style={{
                            backgroundColor: '#fff',
                            borderRadius: '12px',
                            padding: '1.5rem',
                            maxWidth: '440px',
                            width: '100%',
                            boxShadow: '0 20px 60px rgba(0,0,0,0.2)'
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <h3 style={{ margin: '0 0 1rem 0', fontSize: '1.125rem', fontWeight: 600, color: '#1e293b' }}>
                            {rejectionReasonModal.title}
                        </h3>
                        {rejectionReasonModal.loading ? (
                            <p style={{ margin: 0, fontSize: '0.875rem', color: '#64748b' }}>Đang tải...</p>
                        ) : rejectionReasonModal.reason ? (
                            <>
                                <p style={{ margin: 0, fontSize: '0.875rem', color: '#334155', lineHeight: 1.6, whiteSpace: 'pre-wrap' }}>
                                    {rejectionReasonModal.reason}
                                </p>
                                {rejectionReasonModal.rejectedAt && (
                                    <p style={{ margin: '0.75rem 0 0 0', fontSize: '0.75rem', color: '#94a3b8' }}>
                                        Từ chối lúc: {new Date(rejectionReasonModal.rejectedAt).toLocaleString('vi-VN')}
                                    </p>
                                )}
                            </>
                        ) : (
                            <p style={{ margin: 0, fontSize: '0.875rem', color: '#64748b' }}>Không có lý do từ chối hoặc chưa được ghi nhận.</p>
                        )}
                        <div style={{ marginTop: '1.25rem', display: 'flex', justifyContent: 'flex-end' }}>
                            <button
                                type="button"
                                onClick={closeRejectionReasonModal}
                                style={{
                                    padding: '0.5rem 1rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    backgroundColor: '#f1f5f9',
                                    color: '#475569',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: 'pointer'
                                }}
                            >
                                Đóng
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}