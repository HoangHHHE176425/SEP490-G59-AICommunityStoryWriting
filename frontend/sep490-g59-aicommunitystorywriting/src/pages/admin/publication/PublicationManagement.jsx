import { useState, useEffect, useCallback } from 'react';
import { PublicationList } from '../../../components/admin/publication/PublicationList';
import { PublicationDetailModal } from '../../../components/admin/publication/PublicationDetailModal';
import { Pagination } from '../../../components/pagination/Pagination';
import { getStories } from '../../../api/story/storyApi';
import { getChapters } from '../../../api/chapter/chapterApi';
import { getPendingStories, getPendingChapters, claimStory, claimChapter } from '../../../api/moderator/moderatorApi';
import { resolveBackendUrl } from '../../../utils/resolveBackendUrl';

/** Map API story item sang format publication cho PublicationList / PublicationDetailModal */
function mapStoryToPublication(item) {
    const statusApi = (item.status ?? item.Status ?? '').toUpperCase();
    const statusMap = {
        PENDING_REVIEW: 'pending',
        PUBLISHED: 'approved',
        REJECTED: 'rejected',
    };
    const status = statusMap[statusApi] ?? 'pending';
    const categoryNamesStr = item.categoryNames ?? item.CategoryNames ?? '';
    const categoryNamesArr = categoryNamesStr
        ? String(categoryNamesStr).split(',').map((s) => s.trim()).filter(Boolean)
        : [];
    const coverPath = item.coverImage ?? item.CoverImage;
    const storyId = item.id ?? item.Id;
    return {
        id: storyId,
        storyId,
        storyTitle: item.title ?? item.Title ?? '',
        storyCover: coverPath ? resolveBackendUrl(coverPath) : '',
        author: item.authorName ?? item.AuthorName ?? 'N/A',
        authorId: item.authorId ?? item.AuthorId ?? null,
        type: 'new_story',
        status,
        submittedAt: item.createdAt ?? item.CreatedAt ?? item.updatedAt ?? item.UpdatedAt ?? null,
        reviewedAt: null,
        reviewedBy: null,
        rejectionReason: item.rejectionReason ?? item.RejectionReason ?? null,
        chapters: [],
        totalChapters: item.totalChapters ?? item.TotalChapters ?? 0,
        totalWords: 0,
        categories: categoryNamesArr,
        description: item.summary ?? item.Summary ?? '',
    };
}

const STATUS_PARAM_MAP = {
    pending: 'PENDING_REVIEW',
    approved: 'PUBLISHED',
    rejected: 'REJECTED',
    all: null,
};

const PAGE_SIZE = 10;
/** Backend khuyến nghị: load lại danh sách duyệt/từ chối mỗi 30 giây để cập nhật khi có thay đổi từ nơi khác */
const REFRESH_INTERVAL_MS = 30 * 1000;

/** Map item từ moderator/stories/pending sang format dùng chung (type story). */
function mapPendingStoryToItem(s) {
    const id = s.id ?? s.Id;
    const coverPath = s.coverImage ?? s.CoverImage;
    const categoryNamesStr = s.categoryNames ?? s.CategoryNames ?? '';
    const categoryNamesArr = categoryNamesStr ? String(categoryNamesStr).split(',').map((x) => x.trim()).filter(Boolean) : [];
    return {
        id,
        storyId: id,
        type: 'story',
        storyTitle: s.title ?? s.Title ?? '',
        storyCover: coverPath ? resolveBackendUrl(coverPath) : '',
        author: s.authorName ?? s.AuthorName ?? 'N/A',
        authorId: s.authorId ?? s.AuthorId ?? null,
        status: 'pending',
        submittedAt: s.createdAt ?? s.CreatedAt ?? s.updatedAt ?? s.UpdatedAt ?? null,
        totalChapters: s.totalChapters ?? s.TotalChapters ?? 0,
        categories: categoryNamesArr,
        description: s.summary ?? s.Summary ?? '',
        isClaimedByMe: s.isClaimedByMe ?? s.IsClaimedByMe ?? false,
        claimedByDisplayName: s.claimedByDisplayName ?? s.ClaimedByDisplayName ?? null,
        claimedAt: s.claimedAt ?? s.ClaimedAt ?? null,
    };
}

/** Map item từ moderator/chapters/pending sang format dùng chung (type chapter). */
function mapPendingChapterToItem(c) {
    const id = c.id ?? c.Id;
    const storyId = c.storyId ?? c.StoryId;
    return {
        id,
        chapterId: id,
        storyId,
        type: 'chapter',
        storyTitle: c.storyTitle ?? c.StoryTitle ?? '',
        storyCover: '',
        chapterTitle: c.title ?? c.Title ?? '',
        orderIndex: c.orderIndex ?? c.OrderIndex ?? 0,
        author: null,
        authorId: null,
        status: 'pending',
        submittedAt: c.createdAt ?? c.CreatedAt ?? null,
        totalChapters: null,
        categories: [],
        wordCount: c.wordCount ?? c.WordCount ?? 0,
        isClaimedByMe: c.isClaimedByMe ?? c.IsClaimedByMe ?? false,
        claimedByDisplayName: c.claimedByDisplayName ?? c.ClaimedByDisplayName ?? null,
        claimedAt: c.claimedAt ?? c.ClaimedAt ?? null,
    };
}

export function PublicationManagement() {
    const [selectedPublication, setSelectedPublication] = useState(null);
    const [filterStatus, setFilterStatus] = useState('pending'); // 'pending' | 'approved' | 'rejected' | 'all'
    const [claimFilter, setClaimFilter] = useState('all'); // 'all' | 'UNCLAIMED' | 'CLAIMED' — chỉ áp dụng khi filterStatus === 'pending'
    const [publications, setPublications] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [statsData, setStatsData] = useState({ pending: 0, approved: 0, rejected: 0, total: 0 });
    const [currentPage, setCurrentPage] = useState(1);
    const [totalPages, setTotalPages] = useState(1);
    const [totalCount, setTotalCount] = useState(0);
    const [claimingId, setClaimingId] = useState(null); // id đang gọi claim
    const [showClaimModal, setShowClaimModal] = useState(false); // modal "Chọn truyện để nhận duyệt"
    const [claimConfirmTarget, setClaimConfirmTarget] = useState(null); // { type: 'story', id, title } khi cần popup xác nhận
    const [claimModalStories, setClaimModalStories] = useState([]);
    const [claimModalLoading, setClaimModalLoading] = useState(false);

    const loadClaimModalStories = useCallback(() => {
        setClaimModalLoading(true);
        getPendingStories({ claimFilter: 'UNCLAIMED', pageSize: 100 })
            .then((res) => {
                const items = res?.items ?? res?.Items ?? [];
                setClaimModalStories(items.map(mapPendingStoryToItem));
            })
            .catch(() => setClaimModalStories([]))
            .finally(() => setClaimModalLoading(false));
    }, []);

    const loadPublications = useCallback((page = 1, options = {}) => {
        const silent = options.silent === true;
        if (!silent) {
            setLoading(true);
            setError(null);
        }

        if (filterStatus === 'pending') {
            Promise.all([
                getPendingStories({ pageSize: 500, claimFilter: claimFilter === 'all' ? undefined : claimFilter }),
                getPendingChapters({ pageSize: 500, claimFilter: claimFilter === 'all' ? undefined : claimFilter })
            ])
                .then(([storiesRes, chaptersRes]) => {
                    const storyItems = storiesRes?.items ?? storiesRes?.Items ?? [];
                    const chapterItems = chaptersRes?.items ?? chaptersRes?.Items ?? [];
                    const storyList = storyItems.map(mapPendingStoryToItem);
                    const chapterList = chapterItems.map(mapPendingChapterToItem);
                    const combined = [...storyList, ...chapterList];
                    setPublications(combined);
                    const total = combined.length;
                    setTotalCount(total);
                    setTotalPages(Math.max(1, Math.ceil(total / PAGE_SIZE)));
                    setCurrentPage(Math.min(page, Math.max(1, Math.ceil(total / PAGE_SIZE))));
                })
                .catch((err) => {
                    if (!silent) setError(err?.response?.data?.message ?? err?.message ?? 'Không tải được danh sách. Bạn cần đăng nhập với vai trò MODERATOR hoặc ADMIN.');
                    setPublications([]);
                    setTotalCount(0);
                    setTotalPages(1);
                })
                .finally(() => { if (!silent) setLoading(false); });
            return;
        }

        const statusParam = STATUS_PARAM_MAP[filterStatus];
        const params = { page, pageSize: PAGE_SIZE };
        if (statusParam) params.status = statusParam;

        getStories(params)
            .then((res) => {
                const items = res?.items ?? res?.Items ?? [];
                const total = res?.totalCount ?? res?.totalItems ?? res?.total ?? items.length;
                const pages = res?.totalPages ?? Math.max(1, Math.ceil(total / PAGE_SIZE));
                setPublications(items.map(mapStoryToPublication));
                setTotalCount(total);
                setTotalPages(pages);
                setCurrentPage(res?.page ?? page);
            })
            .catch((err) => {
                if (!silent) setError(err?.message ?? 'Không tải được danh sách truyện');
                setPublications([]);
                setTotalCount(0);
                setTotalPages(1);
            })
            .finally(() => { if (!silent) setLoading(false); });
    }, [filterStatus, claimFilter]);

    const handlePageChange = (page) => {
        setCurrentPage(page);
        if (filterStatus !== 'pending') loadPublications(page);
    };

    const loadStats = useCallback(() => {
        Promise.all([
            getStories({ pageSize: 500 }),
            getChapters({ status: 'PENDING_REVIEW', pageSize: 500 })
        ])
            .then(([storiesRes, chaptersRes]) => {
                const storyItems = storiesRes?.items ?? storiesRes?.Items ?? [];
                const chapterItems = chaptersRes?.items ?? chaptersRes?.Items ?? [];
                const storyIdsWithPendingChapters = new Set(chapterItems.map(c => String(c.storyId ?? c.StoryId)).filter(Boolean));
                const mapped = storyItems.map(mapStoryToPublication);
                const pendingCount = mapped.filter(p => storyIdsWithPendingChapters.has(String(p.storyId ?? p.id))).length;
                setStatsData({
                    pending: pendingCount,
                    approved: mapped.filter(p => p.status === 'approved').length,
                    rejected: mapped.filter(p => p.status === 'rejected').length,
                    total: mapped.length
                });
            })
            .catch(() => setStatsData({ pending: 0, approved: 0, rejected: 0, total: 0 }));
    }, []);

    useEffect(() => {
        const id = setTimeout(() => {
            setCurrentPage(1);
            loadPublications(1);
        }, 0);
        return () => clearTimeout(id);
    }, [loadPublications]);

    useEffect(() => {
        const id = setTimeout(() => loadStats(), 0);
        return () => clearTimeout(id);
    }, [loadStats]);

    useEffect(() => {
        const intervalId = setInterval(() => {
            loadPublications(currentPage, { silent: true });
            loadStats();
        }, REFRESH_INTERVAL_MS);
        return () => clearInterval(intervalId);
    }, [loadPublications, loadStats, currentPage]);

    useEffect(() => {
        if (showClaimModal) loadClaimModalStories();
    }, [showClaimModal, loadClaimModalStories]);

    const filteredPublications = filterStatus === 'pending'
        ? publications.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE)
        : publications;

    const handleViewDetail = (publication) => {
        setSelectedPublication(publication);
    };

    const handleCloseDetail = () => {
        setSelectedPublication(null);
    };

    const handleClaimStory = async (storyId) => {
        setClaimingId(storyId);
        try {
            await claimStory(storyId);
            loadPublications(currentPage);
            loadStats();
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? 'Không thể nhận duyệt đơn.';
            alert(msg);
        } finally {
            setClaimingId(null);
        }
    };

    const handleClaimChapter = async (chapterId) => {
        setClaimingId(chapterId);
        try {
            await claimChapter(chapterId);
            loadPublications(currentPage);
            loadStats();
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? 'Không thể nhận duyệt đơn.';
            alert(msg);
        } finally {
            setClaimingId(null);
        }
    };

    /** Xác nhận nhận duyệt từ popup (sau khi bấm "Nhận duyệt đơn" trong modal Chọn truyện). */
    const handleConfirmClaimFromModal = async () => {
        if (!claimConfirmTarget || claimConfirmTarget.type !== 'story') return;
        const storyId = claimConfirmTarget.id;
        setClaimingId(storyId);
        setClaimConfirmTarget(null);
        try {
            await claimStory(storyId);
            loadPublications(currentPage);
            loadStats();
            loadClaimModalStories();
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? 'Không thể nhận duyệt đơn.';
            alert(msg);
        } finally {
            setClaimingId(null);
        }
    };

    const handleApprove = (publicationId) => {
        // TODO: API call to approve
        console.log('Approved publication:', publicationId);
        setSelectedPublication(null);
        loadPublications();
        loadStats();
    };

    const handleReject = (publicationId, reason) => {
        // TODO: API call to reject
        console.log('Rejected publication:', publicationId, 'Reason:', reason);
        setSelectedPublication(null);
        loadPublications();
        loadStats();
    };

    const stats = statsData;

    return (
        <div style={{ padding: '2rem' }}>
            {/* Header */}
            <div style={{ marginBottom: '2rem' }}>
                <h1 style={{
                    fontSize: '1.875rem',
                    fontWeight: 700,
                    color: '#1e293b',
                    margin: 0,
                    marginBottom: '0.5rem'
                }}>
                    Quản lý xuất bản
                </h1>
                <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>
                    Duyệt và phê duyệt các truyện, chương mới từ tác giả. Moderator chỉ thấy truyện/chương trùng thể loại được gán (bảng moderator_category_assignments).
                </p>
            </div>

            {/* Statistics Cards */}
            <div style={{
                display: 'grid',
                gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
                gap: '1rem',
                marginBottom: '2rem'
            }}>
                <div style={{
                    backgroundColor: '#fff3cd',
                    borderRadius: '12px',
                    padding: '1.5rem',
                    border: '2px solid #ffc107'
                }}>
                    <div style={{ fontSize: '2rem', fontWeight: 700, color: '#856404', marginBottom: '0.25rem' }}>
                        {stats.pending}
                    </div>
                    <div style={{ fontSize: '0.875rem', color: '#856404', fontWeight: 600 }}>
                        Chờ duyệt
                    </div>
                </div>

                <div style={{
                    backgroundColor: '#d1fae5',
                    borderRadius: '12px',
                    padding: '1.5rem',
                    border: '2px solid #10b981'
                }}>
                    <div style={{ fontSize: '2rem', fontWeight: 700, color: '#065f46', marginBottom: '0.25rem' }}>
                        {stats.approved}
                    </div>
                    <div style={{ fontSize: '0.875rem', color: '#065f46', fontWeight: 600 }}>
                        Đã duyệt
                    </div>
                </div>

                <div style={{
                    backgroundColor: '#fee2e2',
                    borderRadius: '12px',
                    padding: '1.5rem',
                    border: '2px solid #ef4444'
                }}>
                    <div style={{ fontSize: '2rem', fontWeight: 700, color: '#991b1b', marginBottom: '0.25rem' }}>
                        {stats.rejected}
                    </div>
                    <div style={{ fontSize: '0.875rem', color: '#991b1b', fontWeight: 600 }}>
                        Từ chối
                    </div>
                </div>

                <div style={{
                    backgroundColor: '#e0f2fe',
                    borderRadius: '12px',
                    padding: '1.5rem',
                    border: '2px solid #0ea5e9'
                }}>
                    <div style={{ fontSize: '2rem', fontWeight: 700, color: '#075985', marginBottom: '0.25rem' }}>
                        {stats.total}
                    </div>
                    <div style={{ fontSize: '0.875rem', color: '#075985', fontWeight: 600 }}>
                        Tổng cộng
                    </div>
                </div>
            </div>

            {/* Một nút "Nhận duyệt đơn" + bộ lọc — chỉ khi tab Chờ duyệt */}
            {filterStatus === 'pending' && (
                <div style={{
                    backgroundColor: '#ffffff',
                    borderRadius: '12px',
                    padding: '0.75rem 1rem',
                    marginBottom: '1rem',
                    border: '1px solid #e2e8f0',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.75rem',
                    flexWrap: 'wrap'
                }}>
                    <button
                        onClick={() => setShowClaimModal(true)}
                        style={{
                            padding: '0.5rem 1rem',
                            fontSize: '0.875rem',
                            fontWeight: 600,
                            backgroundColor: '#0ea5e9',
                            color: '#ffffff',
                            border: 'none',
                            borderRadius: '8px',
                            cursor: 'pointer',
                            display: 'inline-flex',
                            alignItems: 'center',
                            gap: '0.375rem'
                        }}
                    >
                        Nhận duyệt đơn
                    </button>
                    <span style={{ fontSize: '0.875rem', fontWeight: 600, color: '#475569' }}>Nhận đơn:</span>
                    {[
                        { value: 'all', label: 'Tất cả' },
                        { value: 'UNCLAIMED', label: 'Chưa nhận' },
                        { value: 'CLAIMED', label: 'Đã nhận của tôi' }
                    ].map(tab => (
                        <button
                            key={tab.value}
                            onClick={() => setClaimFilter(tab.value)}
                            style={{
                                padding: '0.5rem 1rem',
                                fontSize: '0.8125rem',
                                fontWeight: 600,
                                backgroundColor: claimFilter === tab.value ? '#13ec5b' : 'transparent',
                                color: claimFilter === tab.value ? '#ffffff' : '#64748b',
                                border: claimFilter === tab.value ? 'none' : '1px solid #e2e8f0',
                                borderRadius: '9999px',
                                cursor: 'pointer',
                                transition: 'all 0.2s'
                            }}
                        >
                            {tab.label}
                        </button>
                    ))}
                </div>
            )}

            {/* Filter Tabs */}
            <div style={{
                backgroundColor: '#ffffff',
                borderRadius: '12px',
                padding: '1rem',
                marginBottom: '1.5rem',
                border: '1px solid #e2e8f0',
                display: 'flex',
                gap: '0.5rem',
                flexWrap: 'wrap'
            }}>
                {[
                    { value: 'pending', label: 'Chờ duyệt', color: '#ffc107' },
                    { value: 'approved', label: 'Đã duyệt', color: '#10b981' },
                    { value: 'rejected', label: 'Từ chối', color: '#ef4444' },
                    { value: 'all', label: 'Tất cả', color: '#64748b' }
                ].map(tab => (
                    <button
                        key={tab.value}
                        onClick={() => setFilterStatus(tab.value)}
                        style={{
                            padding: '0.625rem 1.25rem',
                            fontSize: '0.875rem',
                            fontWeight: 600,
                            backgroundColor: filterStatus === tab.value ? tab.color : 'transparent',
                            color: filterStatus === tab.value ? '#ffffff' : '#64748b',
                            border: filterStatus === tab.value ? 'none' : '1px solid #e2e8f0',
                            borderRadius: '9999px',
                            cursor: 'pointer',
                            transition: 'all 0.2s'
                        }}
                        onMouseEnter={(e) => {
                            if (filterStatus !== tab.value) {
                                e.currentTarget.style.backgroundColor = '#f8fafc';
                            }
                        }}
                        onMouseLeave={(e) => {
                            if (filterStatus !== tab.value) {
                                e.currentTarget.style.backgroundColor = 'transparent';
                            }
                        }}
                    >
                        {tab.label} ({
                            tab.value === 'all' ? stats.total :
                                tab.value === 'pending' ? stats.pending :
                                    tab.value === 'approved' ? stats.approved :
                                        stats.rejected
                        })
                    </button>
                ))}
            </div>

            {/* Publications List */}
            {loading ? (
                <div style={{
                    backgroundColor: '#ffffff',
                    borderRadius: '12px',
                    padding: '4rem 2rem',
                    textAlign: 'center',
                    border: '1px solid #e2e8f0'
                }}>
                    <div style={{ fontSize: '2rem', marginBottom: '1rem' }}>⏳</div>
                    <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>Đang tải danh sách truyện...</p>
                </div>
            ) : error ? (
                <div style={{
                    backgroundColor: '#fee2e2',
                    borderRadius: '12px',
                    padding: '1.5rem',
                    border: '1px solid #ef4444'
                }}>
                    <p style={{ fontSize: '0.875rem', color: '#991b1b', margin: 0 }}>{error}</p>
                </div>
            ) : (
                <>
                    <div style={{ backgroundColor: '#ffffff', borderRadius: '12px', border: '1px solid #e2e8f0', overflow: 'hidden' }}>
                        <PublicationList
                            publications={filteredPublications}
                            onViewDetail={handleViewDetail}
                            onClaimStory={handleClaimStory}
                            onClaimChapter={handleClaimChapter}
                            claimingId={claimingId}
                            showClaimButton={false}
                        />
                        {totalPages > 1 && (
                            <Pagination
                                currentPage={currentPage}
                                totalPages={totalPages}
                                totalItems={totalCount}
                                itemsPerPage={PAGE_SIZE}
                                onPageChange={handlePageChange}
                                itemLabel="truyện"
                            />
                        )}
                    </div>
                </>
            )}

            {/* Modal "Chọn truyện để nhận duyệt" — danh sách truyện trùng category với moderator */}
            {showClaimModal && (
                <div
                    style={{
                        position: 'fixed',
                        inset: 0,
                        backgroundColor: 'rgba(0,0,0,0.5)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 1000,
                        padding: '1rem'
                    }}
                    onClick={() => setShowClaimModal(false)}
                >
                    <div
                        style={{
                            backgroundColor: '#fff',
                            borderRadius: '12px',
                            maxWidth: '560px',
                            width: '100%',
                            maxHeight: '85vh',
                            overflow: 'hidden',
                            display: 'flex',
                            flexDirection: 'column',
                            boxShadow: '0 20px 40px rgba(0,0,0,0.15)'
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div style={{ padding: '1rem 1.25rem', borderBottom: '1px solid #e2e8f0', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <h2 style={{ margin: 0, fontSize: '1.125rem', fontWeight: 600, color: '#1e293b' }}>
                                Chọn truyện để nhận duyệt
                            </h2>
                            <button
                                type="button"
                                onClick={() => setShowClaimModal(false)}
                                style={{ background: 'none', border: 'none', fontSize: '1.5rem', cursor: 'pointer', color: '#64748b', lineHeight: 1 }}
                                aria-label="Đóng"
                            >
                                ×
                            </button>
                        </div>
                        <div style={{ padding: '1rem', overflow: 'auto', flex: 1 }}>
                            {claimModalLoading ? (
                                <p style={{ textAlign: 'center', color: '#64748b', margin: 0 }}>Đang tải danh sách...</p>
                            ) : claimModalStories.length === 0 ? (
                                <p style={{ textAlign: 'center', color: '#64748b', margin: 0 }}>
                                    Không có truyện nào chưa nhận (trùng thể loại với bạn).
                                </p>
                            ) : (
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                                    {claimModalStories.map((s) => (
                                        <div
                                            key={s.id}
                                            style={{
                                                display: 'flex',
                                                alignItems: 'center',
                                                gap: '1rem',
                                                padding: '0.75rem',
                                                border: '1px solid #e2e8f0',
                                                borderRadius: '8px',
                                                backgroundColor: '#fafafa'
                                            }}
                                        >
                                            <img
                                                src={s.storyCover}
                                                alt=""
                                                style={{ width: '48px', height: '64px', objectFit: 'cover', borderRadius: '6px', flexShrink: 0 }}
                                            />
                                            <div style={{ flex: 1, minWidth: 0 }}>
                                                <div style={{ fontWeight: 600, color: '#1e293b', marginBottom: '0.25rem' }}>{s.storyTitle}</div>
                                                <div style={{ fontSize: '0.8125rem', color: '#64748b' }}>{s.author}</div>
                                                {Array.isArray(s.categories) && s.categories.length > 0 && (
                                                    <div style={{ display: 'flex', gap: '0.25rem', flexWrap: 'wrap', marginTop: '0.25rem' }}>
                                                        {s.categories.slice(0, 3).map((c) => (
                                                            <span key={c} style={{ fontSize: '0.7rem', padding: '0.125rem 0.375rem', backgroundColor: '#e2e8f0', borderRadius: '4px', color: '#475569' }}>{c}</span>
                                                        ))}
                                                    </div>
                                                )}
                                            </div>
                                            <button
                                                type="button"
                                                onClick={() => setClaimConfirmTarget({ type: 'story', id: s.storyId ?? s.id, title: s.storyTitle })}
                                                disabled={claimingId === (s.storyId ?? s.id)}
                                                style={{
                                                    padding: '0.5rem 0.875rem',
                                                    fontSize: '0.8125rem',
                                                    fontWeight: 600,
                                                    backgroundColor: '#0ea5e9',
                                                    color: '#fff',
                                                    border: 'none',
                                                    borderRadius: '8px',
                                                    cursor: claimingId === (s.storyId ?? s.id) ? 'wait' : 'pointer',
                                                    opacity: claimingId === (s.storyId ?? s.id) ? 0.7 : 1
                                                }}
                                            >
                                                {claimingId === (s.storyId ?? s.id) ? '...' : 'Nhận duyệt đơn'}
                                            </button>
                                        </div>
                                    ))}
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            )}

            {/* Popup xác nhận nhận duyệt */}
            {claimConfirmTarget && (
                <div
                    style={{
                        position: 'fixed',
                        inset: 0,
                        backgroundColor: 'rgba(0,0,0,0.5)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 1001,
                        padding: '1rem'
                    }}
                    onClick={() => setClaimConfirmTarget(null)}
                >
                    <div
                        style={{
                            backgroundColor: '#fff',
                            borderRadius: '12px',
                            padding: '1.5rem',
                            maxWidth: '400px',
                            width: '100%',
                            boxShadow: '0 20px 40px rgba(0,0,0,0.15)'
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <p style={{ margin: 0, marginBottom: '1rem', fontSize: '0.9375rem', color: '#1e293b' }}>
                            Bạn có chắc muốn nhận duyệt truyện <strong>"{claimConfirmTarget.title}"</strong>?
                        </p>
                        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
                            <button
                                type="button"
                                onClick={() => setClaimConfirmTarget(null)}
                                style={{ padding: '0.5rem 1rem', fontSize: '0.875rem', fontWeight: 600, backgroundColor: '#f1f5f9', color: '#475569', border: 'none', borderRadius: '8px', cursor: 'pointer' }}
                            >
                                Hủy
                            </button>
                            <button
                                type="button"
                                onClick={handleConfirmClaimFromModal}
                                style={{ padding: '0.5rem 1rem', fontSize: '0.875rem', fontWeight: 600, backgroundColor: '#0ea5e9', color: '#fff', border: 'none', borderRadius: '8px', cursor: 'pointer' }}
                            >
                                Xác nhận
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Detail Modal */}
            {selectedPublication && (
                <PublicationDetailModal
                    publication={selectedPublication}
                    onClose={handleCloseDetail}
                    onApprove={handleApprove}
                    onReject={handleReject}
                    onRefresh={() => {
                        loadPublications(currentPage);
                        loadStats();
                    }}
                    onClaimStory={handleClaimStory}
                    claimingId={claimingId}
                />
            )}
        </div>
    );
}
