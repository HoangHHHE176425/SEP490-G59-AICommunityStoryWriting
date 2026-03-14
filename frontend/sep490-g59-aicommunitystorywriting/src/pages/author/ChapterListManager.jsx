import { useState, useEffect, useRef, useCallback } from 'react';
import { Plus, Eye, MessageSquare, Book, ListOrdered, Send, Undo2, Pencil, Trash2, ArrowLeft, AlertCircle, ChevronDown, ChevronRight, GitBranch } from 'lucide-react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { getChapters, getChapterById, updateChapter, unpublishChapter, deleteChapter, getChapterRejectionReason, getChapterVersions, deleteChapterVersion, submitChapterVersion, unsubmitChapterVersion } from '../../api/chapter/chapterApi';
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

export function ChapterListManager({ story, onBack, onAddChapter, onEditChapter, onViewChapter, onAddVersion, onEditVersion }) {
    const storyId = story?.id ?? story?.Id;
    const [chapters, setChapters] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [currentPage, setCurrentPage] = useState(1);
    const [totalPages, setTotalPages] = useState(1);
    const [totalCount, setTotalCount] = useState(0);
    const [hasPublishedChapter, setHasPublishedChapter] = useState(false);
    const [hasPendingReviewChapter, setHasPendingReviewChapter] = useState(false);
    /** Set orderIndex (0-based) chương đã PUBLISHED — dùng để hiển thị trạng thái. */
    const [publishedOrderIndices, setPublishedOrderIndices] = useState(new Set());
    /** Set orderIndex (0-based) chương đang PENDING_REVIEW — cho phép gửi chương tiếp theo khi chương trước đã gửi (published hoặc pending). */
    const [pendingOrderIndices, setPendingOrderIndices] = useState(new Set());
    /** Chương đang được mở rộng để xem/tạo version (click vào hàng chương). */
    const [expandedChapterId, setExpandedChapterId] = useState(null);
    /** Danh sách version theo chapterId (load từ API khi mở panel). */
    const [chapterVersionsMap, setChapterVersionsMap] = useState({});
    /** ChapterId đang load versions (để hiển thị loading). */
    const [loadingVersionsForChapterId, setLoadingVersionsForChapterId] = useState(null);

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
                        getChapters({ storyId, status: 'PUBLISHED', pageSize: 500 }),
                        getChapters({ storyId, status: 'PENDING_REVIEW', pageSize: 500 })
                    ]).then(([publishedRes, pendingRes]) => {
                        const publishedList = Array.isArray(publishedRes) ? publishedRes : (publishedRes?.items ?? publishedRes?.Items ?? []);
                        const pendingList = Array.isArray(pendingRes) ? pendingRes : (pendingRes?.items ?? pendingRes?.Items ?? []);
                        setHasPublishedChapter(publishedList.length > 0);
                        setHasPendingReviewChapter(pendingList.length > 0);
                        setPublishedOrderIndices(new Set(publishedList.map((c) => c.orderIndex ?? c.OrderIndex ?? 0)));
                        setPendingOrderIndices(new Set(pendingList.map((c) => c.orderIndex ?? c.OrderIndex ?? 0)));
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
                setPublishedOrderIndices(new Set());
                return;
            }
            setLoading(true);
            setError(null);
            Promise.all([
                getChapters({ storyId, page: 1, pageSize: CHAPTERS_PAGE_SIZE }),
                getChapters({ storyId, status: 'PUBLISHED', pageSize: 500 }),
                getChapters({ storyId, status: 'PENDING_REVIEW', pageSize: 500 })
            ])
                .then(([res, publishedRes, pendingRes]) => {
                    const rawItems = Array.isArray(res) ? res : (res?.items ?? res?.Items ?? []);
                    const total = res?.totalCount ?? res?.totalItems ?? res?.total ?? rawItems.length;
                    const pages = res?.totalPages ?? Math.max(1, Math.ceil(total / CHAPTERS_PAGE_SIZE));
                    const publishedList = Array.isArray(publishedRes) ? publishedRes : (publishedRes?.items ?? publishedRes?.Items ?? []);
                    const pendingList = Array.isArray(pendingRes) ? pendingRes : (pendingRes?.items ?? pendingRes?.Items ?? []);
                    const publishedSet = new Set(publishedList.map((c) => c.orderIndex ?? c.OrderIndex ?? 0));
                    const pendingSet = new Set(pendingList.map((c) => c.orderIndex ?? c.OrderIndex ?? 0));
                    if (!cancelled) {
                        setChapters(rawItems.map((item) => ({ ...mapChapterFromApi(item), content: item.content ?? item.Content ?? '' })));
                        setTotalCount(total);
                        setTotalPages(pages);
                        setCurrentPage(res?.page ?? 1);
                        setHasPublishedChapter(publishedList.length > 0);
                        setHasPendingReviewChapter(pendingList.length > 0);
                        setPublishedOrderIndices(publishedSet);
                        setPendingOrderIndices(pendingSet);
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

    /** Load danh sách version của một chapter (gọi API). */
    const loadVersionsForChapter = useCallback((chapterId) => {
        if (!chapterId) return;
        setLoadingVersionsForChapterId(chapterId);
        getChapterVersions(chapterId)
            .then((list) => {
                const arr = Array.isArray(list) ? list : [];
                setChapterVersionsMap((prev) => ({ ...prev, [chapterId]: arr }));
            })
            .catch(() => setChapterVersionsMap((prev) => ({ ...prev, [chapterId]: [] })))
            .finally(() => setLoadingVersionsForChapterId(null));
    }, []);

    /** Khi mở panel version của một chương thì load versions từ API (defer để tránh setState đồng bộ trong effect). */
    useEffect(() => {
        if (!expandedChapterId) return;
        const tid = setTimeout(() => loadVersionsForChapter(expandedChapterId), 0);
        return () => clearTimeout(tid);
    }, [expandedChapterId, loadVersionsForChapter]);

    /** Khi tab được focus lại (vd: duyệt version ở tab khác rồi chuyển về tab này), refetch list version để version đã duyệt biến mất ngay. */
    useEffect(() => {
        const onVisibilityChange = () => {
            if (document.visibilityState === 'visible' && expandedChapterId) {
                loadVersionsForChapter(expandedChapterId);
            }
        };
        document.addEventListener('visibilitychange', onVisibilityChange);
        return () => document.removeEventListener('visibilitychange', onVisibilityChange);
    }, [expandedChapterId, loadVersionsForChapter]);

    const [actioningChapterId, setActioningChapterId] = useState(null);
    const [actioningVersionId, setActioningVersionId] = useState(null);
    const [confirmDialog, setConfirmDialog] = useState({ open: false, action: null, chapterId: null, versionId: null, versionTitle: null });
    const [rejectionReasonModal, setRejectionReasonModal] = useState({ open: false, title: '', reason: null, rejectedAt: null, loading: false });

    const handleDeleteChapter = (chapterId) => {
        if (!chapterId) return;
        deleteChapter(chapterId)
            .then(() => loadChapters(currentPage))
            .catch((err) => {
                const msg = err?.response?.data?.message ?? err?.message ?? 'Xóa chương thất bại';
                alert(msg);
            })
            .finally(() => setActioningChapterId(null));
    };

    const openPublishConfirm = (chapterId) => {
        setConfirmDialog({ open: true, action: 'publish', chapterId, versionId: null, versionTitle: null });
    };
    const openUnpublishConfirm = (chapterId) => {
        setConfirmDialog({ open: true, action: 'unpublish', chapterId, versionId: null, versionTitle: null });
    };
    const openDeleteConfirm = (chapterId) => {
        setConfirmDialog({ open: true, action: 'delete', chapterId, versionId: null, versionTitle: null });
    };
    const openVersionSubmitConfirm = (chapterId, versionId, versionTitle) => {
        setConfirmDialog({ open: true, action: 'version_submit', chapterId, versionId, versionTitle });
    };
    const openVersionUnsubmitConfirm = (chapterId, versionId, versionTitle) => {
        setConfirmDialog({ open: true, action: 'version_unsubmit', chapterId, versionId, versionTitle });
    };
    const openVersionDeleteConfirm = (chapterId, versionId, versionTitle) => {
        setConfirmDialog({ open: true, action: 'version_delete', chapterId, versionId, versionTitle });
    };
    const closeConfirmDialog = () => {
        const isVersionAction = confirmDialog.action?.startsWith('version_');
        if ((isVersionAction && !actioningVersionId) || (!isVersionAction && !actioningChapterId))
            setConfirmDialog({ open: false, action: null, chapterId: null, versionId: null, versionTitle: null });
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
                            getChapters({ storyId, status: 'PUBLISHED', pageSize: 500 }),
                            getChapters({ storyId, status: 'PENDING_REVIEW', pageSize: 500 })
                        ]).then(([rPub, rPend]) => {
                            const pubList = Array.isArray(rPub) ? rPub : (rPub?.items ?? rPub?.Items ?? []);
                            const pendList = Array.isArray(rPend) ? rPend : (rPend?.items ?? rPend?.Items ?? []);
                            setHasPublishedChapter(pubList.length > 0);
                            setHasPendingReviewChapter(pendList.length > 0);
                            setPublishedOrderIndices(new Set(pubList.map((c) => c.orderIndex ?? c.OrderIndex ?? 0)));
                            setPendingOrderIndices(new Set(pendList.map((c) => c.orderIndex ?? c.OrderIndex ?? 0)));
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
                        getChapters({ storyId, status: 'PUBLISHED', pageSize: 500 }),
                        getChapters({ storyId, status: 'PENDING_REVIEW', pageSize: 500 })
                    ]).then(([rPub, rPend]) => {
                        const pubList = Array.isArray(rPub) ? rPub : (rPub?.items ?? rPub?.Items ?? []);
                        const pendList = Array.isArray(rPend) ? rPend : (rPend?.items ?? rPend?.Items ?? []);
                        setHasPublishedChapter(pubList.length > 0);
                        setHasPendingReviewChapter(pendList.length > 0);
                        setPublishedOrderIndices(new Set(pubList.map((c) => c.orderIndex ?? c.OrderIndex ?? 0)));
                        setPendingOrderIndices(new Set(pendList.map((c) => c.orderIndex ?? c.OrderIndex ?? 0)));
                    });
                })
                .catch((err) => {
                    alert(err?.message ?? 'Hủy xuất bản thất bại');
                })
                .finally(() => setActioningChapterId(null));
        } else if (action === 'delete') {
            handleDeleteChapter(chapterId);
        } else if (action === 'version_submit' && confirmDialog.versionId) {
            const versionId = confirmDialog.versionId;
            setActioningVersionId(versionId);
            setConfirmDialog({ open: false, action: null, chapterId: null, versionId: null, versionTitle: null });
            submitChapterVersion(chapterId, versionId)
                .then(() => loadVersionsForChapter(chapterId))
                .then(() => loadChapters(currentPage, { silent: true }))
                .catch((err) => {
                    const msg = err?.response?.data?.message ?? err?.message ?? 'Gửi duyệt phiên bản thất bại';
                    const hint = (msg && (msg.includes('DRAFT') || msg.includes('Bản nháp'))) ? '\n\nPhiên bản bị từ chối vẫn được phép gửi lại. Nếu lỗi lặp lại, hãy làm mới trang (F5) và thử lại.' : '';
                    alert(msg + hint);
                })
                .finally(() => setActioningVersionId(null));
        } else if (action === 'version_unsubmit' && confirmDialog.versionId) {
            const versionId = confirmDialog.versionId;
            setActioningVersionId(versionId);
            setConfirmDialog({ open: false, action: null, chapterId: null, versionId: null, versionTitle: null });
            unsubmitChapterVersion(chapterId, versionId)
                .then(() => loadVersionsForChapter(chapterId))
                .then(() => loadChapters(currentPage, { silent: true }))
                .catch((err) => alert(err?.response?.data?.message ?? err?.message ?? 'Hủy gửi duyệt phiên bản thất bại'))
                .finally(() => setActioningVersionId(null));
        } else if (action === 'version_delete' && confirmDialog.versionId) {
            const versionId = confirmDialog.versionId;
            setActioningVersionId(versionId);
            setConfirmDialog({ open: false, action: null, chapterId: null, versionId: null, versionTitle: null });
            deleteChapterVersion(chapterId, versionId)
                .then(() => loadVersionsForChapter(chapterId))
                .catch((err) => alert(err?.response?.data?.message ?? err?.message ?? 'Xóa phiên bản thất bại'))
                .finally(() => setActioningVersionId(null));
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
                                chapters.map((chapter, index) => {
                                    const isExpanded = expandedChapterId === chapter.id;
                                    const versionsRaw = chapterVersionsMap[chapter.id] ?? [];
                                    const versions = versionsRaw.map((v) => ({
                                        id: v.id ?? v.Id,
                                        version_number: v.versionNumber ?? v.version_number,
                                        change_summary: v.titleSnapshot ?? v.change_summary ?? '—',
                                        created_at: v.createdAt ?? v.created_at,
                                        status: v.status ?? 'DRAFT',
                                        rejection_reason: v.rejectionReason ?? v.rejection_reason,
                                        reviewed_at: v.reviewedAt ?? v.reviewed_at,
                                    }));
                                    const hasPendingVersion = versions.some((ver) => (ver.status ?? '').toLowerCase() === 'pending_review');
                                    const chapterIsPendingReview = (chapter.status ?? '').toLowerCase() === 'pending_review';
                                    const canSubmitForPublish = chapter.number === 1 || publishedOrderIndices.has(chapter.number - 2) || pendingOrderIndices.has(chapter.number - 2);
                                    const canSubmitVersion = canSubmitForPublish && !hasPendingVersion && !chapterIsPendingReview;
                                    const versionsLoading = loadingVersionsForChapterId === chapter.id;
                                    const toggleExpand = (e) => {
                                        if (e.target.closest('button')) return;
                                        setExpandedChapterId((prev) => (prev === chapter.id ? null : chapter.id));
                                    };
                                    const handleCreateVersion = (e) => {
                                        e.preventDefault();
                                        e.stopPropagation();
                                        onAddVersion?.(chapter);
                                    };
                                    return (
                                        <div key={chapter.id} style={{ borderBottom: index < chapters.length - 1 ? '1px solid #f3f4f6' : 'none' }}>
                                            <div
                                                style={{
                                                    display: 'grid',
                                                    gridTemplateColumns: '90px 1fr 320px',
                                                    padding: '1rem 1.5rem',
                                                    alignItems: 'center',
                                                    transition: 'background-color 0.2s',
                                                    backgroundColor: isExpanded ? '#f8fafc' : '#ffffff',
                                                }}
                                                onMouseEnter={(e) => {
                                                    if (!isExpanded) e.currentTarget.style.backgroundColor = '#fafafa';
                                                }}
                                                onMouseLeave={(e) => {
                                                    if (!isExpanded) e.currentTarget.style.backgroundColor = '#ffffff';
                                                }}
                                            >
                                                {/* Order + Chevron (click to expand) */}
                                                <div
                                                    role="button"
                                                    tabIndex={0}
                                                    onClick={toggleExpand}
                                                    onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggleExpand(e); } }}
                                                    style={{
                                                        display: 'flex',
                                                        alignItems: 'center',
                                                        gap: '0.5rem',
                                                        cursor: 'pointer',
                                                        outline: 'none',
                                                    }}
                                                >
                                                    {isExpanded ? (
                                                        <ChevronDown size={18} color="#6366f1" style={{ flexShrink: 0 }} />
                                                    ) : (
                                                        <ChevronRight size={18} color="#94a3b8" style={{ flexShrink: 0 }} />
                                                    )}
                                                    <span style={{ fontSize: '0.9375rem', fontWeight: 600, color: '#334155' }}>
                                                        Chương {chapter.number}
                                                    </span>
                                                </div>

                                                {/* Title and Info (click to expand) */}
                                                <div
                                                    role="button"
                                                    tabIndex={0}
                                                    onClick={toggleExpand}
                                                    onKeyDown={(e) => { if (e.key === 'Enter' || e.key === ' ') { e.preventDefault(); toggleExpand(e); } }}
                                                    style={{ display: 'flex', flexDirection: 'column', gap: '0.375rem', minWidth: 0, cursor: 'pointer', outline: 'none' }}
                                                >
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
                                                    {/* Hàng 1: Xem chi tiết, Chỉnh sửa, Xóa — Chỉnh sửa: không cho khi Chờ duyệt hoặc Đã xuất bản; Xóa: chỉ cho khi Bản nháp */}
                                                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                                        <button
                                                            type="button"
                                                            onClick={() => onViewChapter?.(chapter)}
                                                            title="Xem chi tiết chương"
                                                            style={{
                                                                display: 'inline-flex',
                                                                alignItems: 'center',
                                                                gap: '0.25rem',
                                                                padding: '0.4rem 0.75rem',
                                                                backgroundColor: '#f0f9ff',
                                                                border: '1px solid #bae6fd',
                                                                borderRadius: '9999px',
                                                                fontSize: '0.75rem',
                                                                fontWeight: 600,
                                                                color: '#0369a1',
                                                                cursor: 'pointer',
                                                                transition: 'all 0.2s',
                                                                whiteSpace: 'nowrap'
                                                            }}
                                                            onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = '#e0f2fe'; }}
                                                            onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = '#f0f9ff'; }}
                                                        >
                                                            <Eye size={12} />
                                                            Xem chi tiết
                                                        </button>
                                                        <button
                                                            type="button"
                                                            onClick={() => (chapter.status !== 'pending_review' && chapter.status !== 'published') && onEditChapter(chapter)}
                                                            disabled={chapter.status === 'pending_review' || chapter.status === 'published'}
                                                            title={chapter.status === 'pending_review' ? 'Chương đang chờ duyệt, không thể chỉnh sửa' : chapter.status === 'published' ? 'Chương đã xuất bản, không thể chỉnh sửa' : 'Chỉnh sửa chương'}
                                                            style={{
                                                                display: 'inline-flex',
                                                                alignItems: 'center',
                                                                gap: '0.25rem',
                                                                padding: '0.4rem 0.75rem',
                                                                backgroundColor: (chapter.status === 'pending_review' || chapter.status === 'published') ? '#f1f5f9' : '#f0fdf4',
                                                                border: `1px solid ${(chapter.status === 'pending_review' || chapter.status === 'published') ? '#e2e8f0' : '#86efac'}`,
                                                                borderRadius: '9999px',
                                                                fontSize: '0.75rem',
                                                                fontWeight: 600,
                                                                color: (chapter.status === 'pending_review' || chapter.status === 'published') ? '#94a3b8' : '#15803d',
                                                                cursor: (chapter.status === 'pending_review' || chapter.status === 'published') ? 'not-allowed' : 'pointer',
                                                                transition: 'all 0.2s',
                                                                whiteSpace: 'nowrap',
                                                                opacity: (chapter.status === 'pending_review' || chapter.status === 'published') ? 0.8 : 1
                                                            }}
                                                            onMouseEnter={(e) => { if (chapter.status !== 'pending_review' && chapter.status !== 'published') e.currentTarget.style.backgroundColor = '#dcfce7'; }}
                                                            onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = (chapter.status === 'pending_review' || chapter.status === 'published') ? '#f1f5f9' : '#f0fdf4'; }}
                                                        >
                                                            <Pencil size={12} />
                                                            Chỉnh sửa
                                                        </button>
                                                        <button
                                                            onClick={() => chapter.status === 'draft' && openDeleteConfirm(chapter.id)}
                                                            disabled={chapter.status !== 'draft'}
                                                            title={chapter.status === 'draft' ? 'Xóa chương' : 'Chỉ được xóa chương khi ở trạng thái Bản nháp'}
                                                            style={{
                                                                display: 'inline-flex',
                                                                alignItems: 'center',
                                                                gap: '0.25rem',
                                                                padding: '0.4rem 0.75rem',
                                                                backgroundColor: chapter.status === 'draft' ? '#fff' : '#f1f5f9',
                                                                border: `1px solid ${chapter.status === 'draft' ? '#fecaca' : '#e2e8f0'}`,
                                                                borderRadius: '9999px',
                                                                fontSize: '0.75rem',
                                                                fontWeight: 600,
                                                                color: chapter.status === 'draft' ? '#dc2626' : '#94a3b8',
                                                                cursor: chapter.status === 'draft' ? 'pointer' : 'not-allowed',
                                                                transition: 'all 0.2s',
                                                                whiteSpace: 'nowrap',
                                                                opacity: chapter.status === 'draft' ? 1 : 0.8
                                                            }}
                                                            onMouseEnter={(e) => { if (chapter.status === 'draft') e.currentTarget.style.backgroundColor = '#fef2f2'; }}
                                                            onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = chapter.status === 'draft' ? '#fff' : '#f1f5f9'; }}
                                                        >
                                                            <Trash2 size={12} />
                                                            Xóa
                                                        </button>
                                                    </div>
                                                    {/* Hàng 2: Xuất bản hoặc Hủy xuất bản (draft/rejected = Xuất bản; pending_review = Hủy xuất bản) */}
                                                    {(chapter.status === 'draft' || chapter.status === 'pending_review' || chapter.status === 'rejected') && (
                                                        <div style={{ display: 'flex', width: '100%' }}>
                                                            {(chapter.status === 'draft' || chapter.status === 'rejected') && (() => {
                                                                const canSubmitForPublish = chapter.number === 1 || publishedOrderIndices.has(chapter.number - 2) || pendingOrderIndices.has(chapter.number - 2);
                                                                return (
                                                                    <button
                                                                        onClick={() => canSubmitForPublish && handlePublishChapter(chapter.id)}
                                                                        disabled={actioningChapterId === chapter.id || !canSubmitForPublish}
                                                                        title={!canSubmitForPublish ? `Phải gửi chương ${chapter.number - 1} trước khi gửi chương ${chapter.number}.` : 'Gửi chương lên để duyệt xuất bản'}
                                                                        style={{
                                                                            display: 'inline-flex',
                                                                            alignItems: 'center',
                                                                            justifyContent: 'center',
                                                                            gap: '0.25rem',
                                                                            width: '100%',
                                                                            padding: '0.4rem 0.75rem',
                                                                            backgroundColor: canSubmitForPublish ? '#13ec5b' : '#e2e8f0',
                                                                            border: 'none',
                                                                            borderRadius: '9999px',
                                                                            fontSize: '0.75rem',
                                                                            fontWeight: 600,
                                                                            color: canSubmitForPublish ? '#fff' : '#94a3b8',
                                                                            cursor: actioningChapterId === chapter.id || !canSubmitForPublish ? 'not-allowed' : 'pointer',
                                                                            opacity: actioningChapterId === chapter.id ? 0.7 : 1,
                                                                            transition: 'all 0.2s',
                                                                            whiteSpace: 'nowrap'
                                                                        }}
                                                                    >
                                                                        <Send size={12} />
                                                                        {actioningChapterId === chapter.id ? '...' : 'Xuất bản'}
                                                                    </button>
                                                                );
                                                            })()}
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

                                            {/* Panel version khi mở rộng — đồng bộ màu hệ thống, có nút Chỉnh sửa / Xóa / Xuất bản */}
                                            {isExpanded && (
                                                <div
                                                    onClick={(e) => { e.stopPropagation(); }}
                                                    role="presentation"
                                                    style={{
                                                        marginLeft: '3rem',
                                                        marginRight: '1.5rem',
                                                        marginBottom: '1rem',
                                                        padding: '1.25rem 1.5rem',
                                                        backgroundColor: '#ffffff',
                                                        borderRadius: '12px',
                                                        border: '1px solid #e5e7eb',
                                                        boxShadow: '0 1px 3px rgba(0,0,0,0.06)',
                                                    }}
                                                >
                                                    <div style={{
                                                        display: 'flex',
                                                        alignItems: 'center',
                                                        justifyContent: 'space-between',
                                                        flexWrap: 'wrap',
                                                        gap: '1rem',
                                                        marginBottom: '1.25rem',
                                                        paddingBottom: '1rem',
                                                        borderBottom: '1px solid #f3f4f6',
                                                    }}>
                                                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                                                            <div style={{
                                                                width: '36px',
                                                                height: '36px',
                                                                borderRadius: '10px',
                                                                background: 'linear-gradient(135deg, #6366f1 0%, #4f46e5 100%)',
                                                                display: 'flex',
                                                                alignItems: 'center',
                                                                justifyContent: 'center',
                                                            }}>
                                                                <GitBranch size={18} color="#fff" />
                                                            </div>
                                                            <span style={{ fontFamily: "'Plus Jakarta Sans', sans-serif", fontSize: '1rem', fontWeight: 700, color: '#1A2332' }}>
                                                                Phiên bản chương
                                                            </span>
                                                        </div>
                                                        <button
                                                            type="button"
                                                            onClick={handleCreateVersion}
                                                            style={{
                                                                display: 'inline-flex',
                                                                alignItems: 'center',
                                                                gap: '0.5rem',
                                                                padding: '0.5rem 1rem',
                                                                backgroundColor: '#13ec5b',
                                                                color: '#fff',
                                                                border: 'none',
                                                                borderRadius: '9999px',
                                                                fontSize: '0.8125rem',
                                                                fontWeight: 700,
                                                                cursor: 'pointer',
                                                                boxShadow: '0 2px 8px rgba(19, 236, 91, 0.3)',
                                                                fontFamily: "'Plus Jakarta Sans', sans-serif",
                                                            }}
                                                            onMouseEnter={(e) => {
                                                                e.currentTarget.style.backgroundColor = '#10d452';
                                                                e.currentTarget.style.transform = 'translateY(-1px)';
                                                            }}
                                                            onMouseLeave={(e) => {
                                                                e.currentTarget.style.backgroundColor = '#13ec5b';
                                                                e.currentTarget.style.transform = 'translateY(0)';
                                                            }}
                                                        >
                                                            <Plus size={16} />
                                                            Tạo phiên bản
                                                        </button>
                                                    </div>

                                                    {versionsLoading ? (
                                                        <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0, padding: '1.5rem', textAlign: 'center' }}>
                                                            Đang tải danh sách phiên bản...
                                                        </p>
                                                    ) : versions.length === 0 ? (
                                                        <p style={{
                                                            fontSize: '0.875rem',
                                                            color: '#64748b',
                                                            margin: 0,
                                                            padding: '1.5rem',
                                                            textAlign: 'center',
                                                            backgroundColor: '#f8fafc',
                                                            borderRadius: '8px',
                                                            border: '1px dashed #e2e8f0',
                                                        }}>
                                                            Chưa có phiên bản nào. Bấm &quot;Tạo phiên bản&quot; để lưu bản sao nội dung hiện tại.
                                                        </p>
                                                    ) : (
                                                        <div style={{ borderRadius: '8px', border: '1px solid #e5e7eb', overflow: 'hidden' }}>
                                                            <div style={{
                                                                display: 'grid',
                                                                gridTemplateColumns: '80px 1fr 110px 140px 320px',
                                                                gap: '1rem',
                                                                padding: '0.75rem 1rem',
                                                                backgroundColor: '#f9fafb',
                                                                borderBottom: '1px solid #e5e7eb',
                                                                fontSize: '0.75rem',
                                                                fontWeight: 600,
                                                                color: '#6b7280',
                                                                alignItems: 'center',
                                                            }}>
                                                                <div>Phiên bản</div>
                                                                <div>Tiêu đề phiên bản</div>
                                                                <div>Trạng thái</div>
                                                                <div>Ngày tạo</div>
                                                                <div style={{ textAlign: 'center' }}>Hành động</div>
                                                            </div>
                                                            {versions.map((v, vIndex) => {
                                                                const vStatusKey = (v.status ?? 'DRAFT').toUpperCase();
                                                                const vStatusLower = (v.status ?? 'DRAFT').toLowerCase();
                                                                const vStatusDisplay = CHAPTER_STATUS_MAP[vStatusKey] ?? v.status ?? 'DRAFT';
                                                                const vStatusStyle = getChapterStatusStyle(v.status);
                                                                return (
                                                                    <div
                                                                        key={v.id}
                                                                        style={{
                                                                            display: 'grid',
                                                                            gridTemplateColumns: '80px 1fr 110px 140px 320px',
                                                                            gap: '1rem',
                                                                            padding: '0.875rem 1rem',
                                                                            alignItems: 'center',
                                                                            borderBottom: vIndex < versions.length - 1 ? '1px solid #f3f4f6' : 'none',
                                                                            fontSize: '0.8125rem',
                                                                            color: '#334155',
                                                                            transition: 'background-color 0.2s',
                                                                        }}
                                                                        onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = '#fafafa'; }}
                                                                        onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = '#fff'; }}
                                                                    >
                                                                        <span style={{ fontWeight: 700, color: '#6366f1' }}>
                                                                            #{v.version_number}
                                                                        </span>
                                                                        <span style={{ color: '#475569', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                                                            {v.change_summary ?? '—'}
                                                                        </span>
                                                                        <span style={{
                                                                            fontSize: '0.75rem',
                                                                            fontWeight: 600,
                                                                            padding: '0.25rem 0.5rem',
                                                                            borderRadius: '9999px',
                                                                            ...vStatusStyle,
                                                                        }}>
                                                                            {vStatusDisplay}
                                                                        </span>
                                                                        <span style={{ fontSize: '0.75rem', color: '#94a3b8' }}>
                                                                            {v.created_at ? new Date(v.created_at).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '—'}
                                                                        </span>
                                                                        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '0.5rem', flexWrap: 'wrap' }}>
                                                                            <div style={{
                                                                                display: 'flex',
                                                                                flexDirection: 'column',
                                                                                alignItems: 'stretch',
                                                                                justifyContent: 'center',
                                                                                gap: '0.5rem',
                                                                                width: 'fit-content',
                                                                                margin: '0 auto'
                                                                            }}>
                                                                                {/* Hàng 1: Lý do từ chối (nếu rejected), Chỉnh sửa, Xóa — giống chapter */}
                                                                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                                                                    {vStatusLower === 'rejected' && (
                                                                                        <button
                                                                                            type="button"
                                                                                            onClick={(e) => {
                                                                                                e.stopPropagation();
                                                                                                if (v.rejection_reason) {
                                                                                                    setRejectionReasonModal({
                                                                                                        open: true,
                                                                                                        title: `Lý do từ chối: ${v.title_snapshot || v.titleSnapshot || `Phiên bản #${v.version_number}`}`,
                                                                                                        reason: v.rejection_reason,
                                                                                                        rejectedAt: v.reviewed_at ?? null,
                                                                                                        loading: false
                                                                                                    });
                                                                                                } else {
                                                                                                    openChapterRejectionReason(v.title_snapshot || v.titleSnapshot || `Phiên bản #${v.version_number}`, chapter.id);
                                                                                                }
                                                                                            }}
                                                                                            title="Xem lý do từ chối phiên bản"
                                                                                            style={{
                                                                                                display: 'inline-flex',
                                                                                                alignItems: 'center',
                                                                                                gap: '0.25rem',
                                                                                                padding: '0.4rem 0.75rem',
                                                                                                backgroundColor: '#fef2f2',
                                                                                                border: '1px solid #fecaca',
                                                                                                borderRadius: '9999px',
                                                                                                fontSize: '0.75rem',
                                                                                                fontWeight: 600,
                                                                                                color: '#b91c1c',
                                                                                                cursor: 'pointer',
                                                                                                whiteSpace: 'nowrap',
                                                                                                transition: 'all 0.2s'
                                                                                            }}
                                                                                        >
                                                                                            <AlertCircle size={12} />
                                                                                            Lý do từ chối
                                                                                        </button>
                                                                                    )}
                                                                                    <button
                                                                                        type="button"
                                                                                        onClick={(e) => { e.stopPropagation(); if (vStatusLower !== 'pending_review') onEditVersion?.(chapter, v); }}
                                                                                        title={vStatusLower === 'pending_review' ? 'Phiên bản đang chờ duyệt, không thể chỉnh sửa' : 'Chỉnh sửa phiên bản'}
                                                                                        disabled={vStatusLower === 'pending_review'}
                                                                                        style={{
                                                                                            display: 'inline-flex',
                                                                                            alignItems: 'center',
                                                                                            gap: '0.25rem',
                                                                                            padding: '0.4rem 0.75rem',
                                                                                            backgroundColor: vStatusLower === 'pending_review' ? '#f1f5f9' : '#f0fdf4',
                                                                                            border: `1px solid ${vStatusLower === 'pending_review' ? '#e2e8f0' : '#86efac'}`,
                                                                                            borderRadius: '9999px',
                                                                                            fontSize: '0.75rem',
                                                                                            fontWeight: 600,
                                                                                            color: vStatusLower === 'pending_review' ? '#94a3b8' : '#15803d',
                                                                                            cursor: vStatusLower === 'pending_review' ? 'not-allowed' : 'pointer',
                                                                                            opacity: vStatusLower === 'pending_review' ? 0.8 : 1,
                                                                                            whiteSpace: 'nowrap',
                                                                                            transition: 'all 0.2s'
                                                                                        }}
                                                                                    >
                                                                                        <Pencil size={12} />
                                                                                        Chỉnh sửa
                                                                                    </button>
                                                                                    <button
                                                                                        type="button"
                                                                                        onClick={(e) => {
                                                                                            e.stopPropagation();
                                                                                            if (!v.id || vStatusLower === 'published' || vStatusLower === 'pending_review') return;
                                                                                            openVersionDeleteConfirm(chapter.id, v.id, v.title_snapshot || v.titleSnapshot || '');
                                                                                        }}
                                                                                        title={vStatusLower === 'pending_review' ? 'Phiên bản đang chờ duyệt, không thể xóa' : vStatusLower === 'published' ? 'Đã xuất bản' : 'Xóa phiên bản'}
                                                                                        disabled={vStatusLower === 'published' || vStatusLower === 'pending_review'}
                                                                                        style={{
                                                                                            display: 'inline-flex',
                                                                                            alignItems: 'center',
                                                                                            gap: '0.25rem',
                                                                                            padding: '0.4rem 0.75rem',
                                                                                            backgroundColor: (vStatusLower === 'published' || vStatusLower === 'pending_review') ? '#f1f5f9' : '#fff',
                                                                                            border: '1px solid #fecaca',
                                                                                            borderRadius: '9999px',
                                                                                            fontSize: '0.75rem',
                                                                                            fontWeight: 600,
                                                                                            color: (vStatusLower === 'published' || vStatusLower === 'pending_review') ? '#94a3b8' : '#dc2626',
                                                                                            cursor: (vStatusLower === 'published' || vStatusLower === 'pending_review') ? 'not-allowed' : 'pointer',
                                                                                            whiteSpace: 'nowrap',
                                                                                            transition: 'all 0.2s'
                                                                                        }}
                                                                                    >
                                                                                        <Trash2 size={12} />
                                                                                        Xóa
                                                                                    </button>
                                                                                </div>
                                                                                {/* Hàng 2: Xuất bản hoặc Hủy xuất bản — giống chapter */}
                                                                                {(vStatusLower === 'pending_review' || vStatusLower === 'draft' || vStatusLower === 'rejected') && (
                                                                                    <div style={{ display: 'flex', width: '100%' }}>
                                                                                        {vStatusLower === 'pending_review' ? (
                                                                                            <button
                                                                                                type="button"
                                                                                                onClick={(e) => {
                                                                                                    e.stopPropagation();
                                                                                                    openVersionUnsubmitConfirm(chapter.id, v.id, v.title_snapshot || v.titleSnapshot || '');
                                                                                                }}
                                                                                                title="Hủy gửi duyệt phiên bản"
                                                                                                disabled={actioningVersionId === v.id}
                                                                                                style={{
                                                                                                    display: 'inline-flex',
                                                                                                    alignItems: 'center',
                                                                                                    justifyContent: 'center',
                                                                                                    gap: '0.25rem',
                                                                                                    width: '100%',
                                                                                                    padding: '0.4rem 0.75rem',
                                                                                                    backgroundColor: actioningVersionId === v.id ? '#e2e8f0' : '#fff',
                                                                                                    border: '1px solid #f59e0b',
                                                                                                    borderRadius: '9999px',
                                                                                                    fontSize: '0.75rem',
                                                                                                    fontWeight: 600,
                                                                                                    color: '#b45309',
                                                                                                    cursor: actioningVersionId === v.id ? 'not-allowed' : 'pointer',
                                                                                                    opacity: actioningVersionId === v.id ? 0.7 : 1,
                                                                                                    whiteSpace: 'nowrap',
                                                                                                    transition: 'all 0.2s'
                                                                                                }}
                                                                                            >
                                                                                                <Undo2 size={12} />
                                                                                                {actioningVersionId === v.id ? '...' : 'Hủy xuất bản'}
                                                                                            </button>
                                                                                        ) : (vStatusLower === 'draft' || vStatusLower === 'rejected') && (
                                                                                            <button
                                                                                                type="button"
                                                                                                onClick={(e) => {
                                                                                                    e.stopPropagation();
                                                                                                    if (vStatusLower === 'published' || !canSubmitVersion) return;
                                                                                                    openVersionSubmitConfirm(chapter.id, v.id, v.title_snapshot || v.titleSnapshot || '');
                                                                                                }}
                                                                                                title={vStatusLower === 'published' ? 'Đã xuất bản' : !canSubmitVersion ? (!canSubmitForPublish ? `Phải gửi chương ${chapter.number - 1} trước khi gửi chương ${chapter.number}.` : chapterIsPendingReview ? 'Chương gốc đang chờ duyệt, không thể gửi phiên bản.' : 'Chỉ được gửi một phiên bản tại một thời điểm. Hãy hủy phiên bản đang chờ duyệt trước.') : 'Gửi duyệt phiên bản'}
                                                                                                disabled={vStatusLower === 'published' || !canSubmitVersion}
                                                                                                style={{
                                                                                                    display: 'inline-flex',
                                                                                                    alignItems: 'center',
                                                                                                    justifyContent: 'center',
                                                                                                    gap: '0.25rem',
                                                                                                    width: '100%',
                                                                                                    padding: '0.4rem 0.75rem',
                                                                                                    backgroundColor: (vStatusLower === 'published' || !canSubmitVersion) ? '#e2e8f0' : '#13ec5b',
                                                                                                    border: 'none',
                                                                                                    borderRadius: '9999px',
                                                                                                    fontSize: '0.75rem',
                                                                                                    fontWeight: 600,
                                                                                                    color: (vStatusLower === 'published' || !canSubmitVersion) ? '#94a3b8' : '#fff',
                                                                                                    cursor: (vStatusLower === 'published' || !canSubmitVersion) ? 'not-allowed' : 'pointer',
                                                                                                    opacity: (vStatusLower === 'published' || !canSubmitVersion) ? 0.8 : 1,
                                                                                                    whiteSpace: 'nowrap',
                                                                                                    transition: 'all 0.2s'
                                                                                                }}
                                                                                            >
                                                                                                <Send size={12} />
                                                                                                {vStatusLower === 'published' ? 'Đã xuất bản' : !canSubmitVersion ? (!canSubmitForPublish ? `Gửi chương ${chapter.number - 1} trước` : chapterIsPendingReview ? 'Chương gốc đang chờ duyệt' : 'Đã có phiên bản chờ duyệt') : 'Xuất bản'}
                                                                                            </button>
                                                                                        )}
                                                                                    </div>
                                                                                )}
                                                                            </div>
                                                                        </div>
                                                                    </div>
                                                                );
                                                            })}
                                                        </div>
                                                    )}
                                                </div>
                                            )}
                                        </div>
                                    );
                                })
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
                            {confirmDialog.action === 'publish' && 'Xác nhận xuất bản'}
                            {confirmDialog.action === 'unpublish' && 'Xác nhận hủy xuất bản'}
                            {confirmDialog.action === 'delete' && 'Xác nhận xóa chương'}
                            {confirmDialog.action === 'version_submit' && 'Xác nhận gửi duyệt phiên bản'}
                            {confirmDialog.action === 'version_unsubmit' && 'Xác nhận hủy gửi duyệt phiên bản'}
                            {confirmDialog.action === 'version_delete' && 'Xác nhận xóa phiên bản'}
                        </h3>
                        <p style={{ fontSize: '0.875rem', color: '#64748b', margin: '0 0 1.5rem 0', lineHeight: 1.5 }}>
                            {confirmDialog.action === 'publish' && 'Bạn có chắc chắn muốn gửi chương này lên để duyệt xuất bản?'}
                            {confirmDialog.action === 'unpublish' && 'Bạn có chắc chắn muốn hủy xuất bản và đưa chương về bản nháp?'}
                            {confirmDialog.action === 'delete' && 'Bạn có chắc chắn muốn xóa chương này? Hành động này không thể hoàn tác.'}
                            {confirmDialog.action === 'version_submit' && 'Bạn có chắc chắn muốn gửi phiên bản này lên để duyệt xuất bản?'}
                            {confirmDialog.action === 'version_unsubmit' && 'Bạn có chắc chắn muốn hủy gửi duyệt? Phiên bản và chương sẽ về trạng thái Bản nháp.'}
                            {confirmDialog.action === 'version_delete' && 'Bạn có chắc chắn muốn xóa phiên bản này? Hành động này không thể hoàn tác.'}
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
                                    backgroundColor: (confirmDialog.action === 'delete' || confirmDialog.action === 'version_delete') ? '#dc2626' : '#13ec5b',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: 'pointer'
                                }}
                            >
                                {(confirmDialog.action === 'delete' || confirmDialog.action === 'version_delete') ? 'Xóa' : 'Xác nhận'}
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