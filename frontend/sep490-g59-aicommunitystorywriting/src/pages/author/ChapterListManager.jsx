import { useState, useEffect } from 'react';
import { Plus, Eye, MessageSquare, Book, Send, Undo2, Pencil, Trash2 } from 'lucide-react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { getChaptersByStoryId, publishChapter, unpublishChapter } from '../../api/chapter/chapterApi';

const CHAPTER_STATUS_MAP = {
    DRAFT: 'Bản nháp',
    PENDING_REVIEW: 'Chờ duyệt',
    REJECTED: 'Bị từ chối',
    PUBLISHED: 'Đã xuất bản',
    HIDDEN: 'Đã ẩn',
    ARCHIVED: 'Đã lưu trữ',
};

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

export function ChapterListManager({ story, onBack, onAddChapter, onEditChapter }) {
    const [chapters, setChapters] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    const loadChapters = () => {
        const storyId = story?.id ?? story?.Id;
        if (!storyId) return;
        setLoading(true);
        setError(null);
        getChaptersByStoryId(storyId)
            .then((res) => {
                const items = Array.isArray(res) ? res : (res?.items ?? res?.Items ?? []);
                setChapters(items.map((item) => ({ ...mapChapterFromApi(item), content: item.content ?? item.Content ?? '' })));
            })
            .catch((err) => {
                setError(err?.message ?? 'Không tải được danh sách chương');
                setChapters([]);
            })
            .finally(() => setLoading(false));
    };

    useEffect(() => {
        let cancelled = false;
        queueMicrotask(() => {
            const storyId = story?.id ?? story?.Id;
            if (!storyId) {
                setChapters([]);
                setLoading(false);
                return;
            }
            setLoading(true);
            setError(null);
            getChaptersByStoryId(storyId)
                .then((res) => {
                    const items = Array.isArray(res) ? res : (res?.items ?? res?.Items ?? []);
                    if (!cancelled) setChapters(items.map((item) => ({ ...mapChapterFromApi(item), content: item.content ?? item.Content ?? '' })));
                })
                .catch((err) => {
                    if (!cancelled) {
                        setError(err?.message ?? 'Không tải được danh sách chương');
                        setChapters([]);
                    }
                })
                .finally(() => {
                    if (!cancelled) setLoading(false);
                });
        });
        return () => { cancelled = true; };
    }, [story?.id ?? story?.Id]);

    const [actioningChapterId, setActioningChapterId] = useState(null); // id khi đang gọi publish/unpublish

    const handleDeleteChapter = (chapterId) => {
        if (window.confirm('Bạn có chắc chắn muốn xóa chương này?')) {
            setChapters((prev) => prev.filter((ch) => ch.id !== chapterId));
        }
    };

    const handlePublishChapter = (chapterId) => {
        setActioningChapterId(chapterId);
        publishChapter(chapterId)
            .then(() => loadChapters())
            .catch((err) => {
                alert(err?.message ?? 'Xuất bản thất bại');
            })
            .finally(() => setActioningChapterId(null));
    };

    const handleUnpublishChapter = (chapterId) => {
        setActioningChapterId(chapterId);
        unpublishChapter(chapterId)
            .then(() => loadChapters())
            .catch((err) => {
                alert(err?.message ?? 'Hủy xuất bản thất bại');
            })
            .finally(() => setActioningChapterId(null));
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
                                        onClick={() => loadChapters()}
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
                                                    backgroundColor: chapter.status === 'published' ? '#dcfce7' : chapter.status === 'pending_review' ? '#fef9c3' : '#f3f4f6',
                                                    color: chapter.status === 'published' ? '#166534' : chapter.status === 'pending_review' ? '#a16207' : '#6b7280'
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
                                            {/* Hàng 1: Chỉnh sửa, Xóa — độ rộng hàng 2 = độ rộng hàng này */}
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                                <button
                                                    onClick={() => onEditChapter(chapter)}
                                                    style={{
                                                        display: 'inline-flex',
                                                        alignItems: 'center',
                                                        gap: '0.25rem',
                                                        padding: '0.4rem 0.75rem',
                                                        backgroundColor: '#f0fdf4',
                                                        border: '1px solid #86efac',
                                                        borderRadius: '9999px',
                                                        fontSize: '0.75rem',
                                                        fontWeight: 600,
                                                        color: '#15803d',
                                                        cursor: 'pointer',
                                                        transition: 'all 0.2s',
                                                        whiteSpace: 'nowrap'
                                                    }}
                                                    onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = '#dcfce7'; }}
                                                    onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = '#f0fdf4'; }}
                                                >
                                                    <Pencil size={12} />
                                                    Chỉnh sửa
                                                </button>
                                                <button
                                                    onClick={() => handleDeleteChapter(chapter.id)}
                                                    style={{
                                                        display: 'inline-flex',
                                                        alignItems: 'center',
                                                        gap: '0.25rem',
                                                        padding: '0.4rem 0.75rem',
                                                        backgroundColor: '#fff',
                                                        border: '1px solid #fecaca',
                                                        borderRadius: '9999px',
                                                        fontSize: '0.75rem',
                                                        fontWeight: 600,
                                                        color: '#dc2626',
                                                        cursor: 'pointer',
                                                        transition: 'all 0.2s',
                                                        whiteSpace: 'nowrap'
                                                    }}
                                                    onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = '#fef2f2'; }}
                                                    onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = '#fff'; }}
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

                        {/* Back Button */}
                        <div style={{ marginTop: '2rem' }}>
                            <button
                                onClick={onBack}
                                style={{
                                    padding: '0.75rem 2rem',
                                    backgroundColor: '#ffffff',
                                    border: '2px solid #13ec5b',
                                    borderRadius: '9999px',
                                    fontSize: '0.875rem',
                                    fontWeight: 700,
                                    color: '#13ec5b',
                                    cursor: 'pointer',
                                    transition: 'all 0.2s'
                                }}
                                onMouseEnter={(e) => {
                                    e.currentTarget.style.backgroundColor = '#f0fdf4';
                                    e.currentTarget.style.borderColor = '#10d452';
                                }}
                                onMouseLeave={(e) => {
                                    e.currentTarget.style.backgroundColor = '#ffffff';
                                    e.currentTarget.style.borderColor = '#13ec5b';
                                }}
                            >
                                Quay lại
                            </button>
                        </div>
                    </>
                </div>
            </div>
            <Footer />
        </div>
    );
}