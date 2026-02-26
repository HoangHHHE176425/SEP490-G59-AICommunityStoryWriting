import { useState, useEffect } from 'react';
import { Plus, Eye, MessageSquare, Book, Send, Undo2, Pencil, Trash2, ArrowLeft } from 'lucide-react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { getChapters, getChapterById, updateChapter, unpublishChapter } from '../../api/chapter/chapterApi';
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
    const [chapters, setChapters] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [currentPage, setCurrentPage] = useState(1);
    const [totalPages, setTotalPages] = useState(1);
    const [totalCount, setTotalCount] = useState(0);

    const loadChapters = (page = 1) => {
        const storyId = story?.id ?? story?.Id;
        if (!storyId) return;
        setLoading(true);
        setError(null);
        getChapters({ storyId, page, pageSize: CHAPTERS_PAGE_SIZE })
            .then((res) => {
                const rawItems = Array.isArray(res) ? res : (res?.items ?? res?.Items ?? []);
                const total = res?.totalCount ?? res?.totalItems ?? res?.total ?? rawItems.length;
                const pages = res?.totalPages ?? Math.max(1, Math.ceil(total / CHAPTERS_PAGE_SIZE));
                setChapters(rawItems.map((item) => ({ ...mapChapterFromApi(item), content: item.content ?? item.Content ?? '' })));
                setTotalCount(total);
                setTotalPages(pages);
                setCurrentPage(res?.page ?? page);
            })
            .catch((err) => {
                setError(err?.message ?? 'Không tải được danh sách chương');
                setChapters([]);
                setTotalCount(0);
                setTotalPages(1);
            })
            .finally(() => setLoading(false));
    };

    useEffect(() => {
        let cancelled = false;
        const storyId = story?.id ?? story?.Id;
        const id = setTimeout(() => {
            if (!storyId) {
                setChapters([]);
                setLoading(false);
                setTotalCount(0);
                setTotalPages(1);
                return;
            }
            setLoading(true);
            setError(null);
            getChapters({ storyId, page: 1, pageSize: CHAPTERS_PAGE_SIZE })
                .then((res) => {
                    const rawItems = Array.isArray(res) ? res : (res?.items ?? res?.Items ?? []);
                    const total = res?.totalCount ?? res?.totalItems ?? res?.total ?? rawItems.length;
                    const pages = res?.totalPages ?? Math.max(1, Math.ceil(total / CHAPTERS_PAGE_SIZE));
                    if (!cancelled) {
                        setChapters(rawItems.map((item) => ({ ...mapChapterFromApi(item), content: item.content ?? item.Content ?? '' })));
                        setTotalCount(total);
                        setTotalPages(pages);
                        setCurrentPage(res?.page ?? 1);
                    }
                })
                .catch((err) => {
                    if (!cancelled) {
                        setError(err?.message ?? 'Không tải được danh sách chương');
                        setChapters([]);
                        setTotalCount(0);
                        setTotalPages(1);
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
    }, [story?.id ?? story?.Id]);

    const handlePageChange = (page) => {
        setCurrentPage(page);
        loadChapters(page);
    };

    const [actioningChapterId, setActioningChapterId] = useState(null);
    const [confirmDialog, setConfirmDialog] = useState({ open: false, action: null, chapterId: null });

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
                    .catch((err) => {
                        alert(err?.message ?? 'Xuất bản thất bại');
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
                    alert(err?.message ?? 'Xuất bản thất bại');
                    setActioningChapterId(null);
                });
        } else if (action === 'unpublish') {
            unpublishChapter(chapterId)
                .then(() => loadChapters(currentPage))
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

    return (
        <div>
            <Header />
            <div style={{ minHeight: '100vh', backgroundColor: '#f5f5f5', padding: '2rem' }}>
                <div style={{ maxWidth: '1400px', margin: '0 auto' }}>
                    <>
                        {/* Header */}
                        <div style={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            gap: '1rem',
                            marginBottom: '2rem'
                        }}>
                            <div style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.75rem',
                                flex: '1 1 0',
                                minWidth: 0,
                                overflow: 'hidden'
                            }}>
                                <Book style={{ width: '24px', height: '24px', color: '#13ec5b', flexShrink: 0 }} />
                                <h2 style={{
                                    fontSize: '1.5rem',
                                    fontWeight: 'bold',
                                    color: '#333333',
                                    margin: 0,
                                    overflow: 'hidden',
                                    textOverflow: 'ellipsis',
                                    whiteSpace: 'nowrap',
                                    maxWidth: '100%'
                                }}>
                                    Danh sách chương - Truyện "{story?.title || 'Untitled'}"
                                </h2>
                            </div>
                            <button
                                onClick={() => onAddChapter?.(story)}
                                style={{
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: '0.5rem',
                                    padding: '0.625rem 1.5rem',
                                    backgroundColor: '#13ec5b',
                                    border: 'none',
                                    borderRadius: '9999px',
                                    fontSize: '0.875rem',
                                    fontWeight: 700,
                                    color: '#ffffff',
                                    cursor: 'pointer',
                                    transition: 'all 0.2s',
                                    flexShrink: 0,
                                    whiteSpace: 'nowrap'
                                }}
                                onMouseEnter={(e) => {
                                    e.currentTarget.style.backgroundColor = '#10d452';
                                    e.currentTarget.style.transform = 'translateY(-2px)';
                                    e.currentTarget.style.boxShadow = '0 4px 12px rgba(19, 236, 91, 0.3)';
                                }}
                                onMouseLeave={(e) => {
                                    e.currentTarget.style.backgroundColor = '#13ec5b';
                                    e.currentTarget.style.transform = 'translateY(0)';
                                    e.currentTarget.style.boxShadow = 'none';
                                }}
                            >
                                <Plus style={{ width: '16px', height: '16px' }} />
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
                                            {/* Hàng 2: Xuất bản hoặc Hủy xuất bản (cùng độ rộng với hàng 1) */}
                                            {(chapter.status === 'draft' || chapter.status === 'pending_review') && (
                                                <div style={{ display: 'flex', width: '100%' }}>
                                                    {chapter.status === 'draft' && (
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
        </div>
    );
}