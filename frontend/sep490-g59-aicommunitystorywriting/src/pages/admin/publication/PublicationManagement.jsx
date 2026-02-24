import { useState, useEffect, useCallback } from 'react';
import { PublicationList } from '../../../components/admin/publication/PublicationList';
import { PublicationDetailModal } from '../../../components/admin/publication/PublicationDetailModal';
import { getStories } from '../../../api/story/storyApi';
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

export function PublicationManagement() {
    const [selectedPublication, setSelectedPublication] = useState(null);
    const [filterStatus, setFilterStatus] = useState('pending'); // 'pending' | 'approved' | 'rejected' | 'all'
    const [publications, setPublications] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [statsData, setStatsData] = useState({ pending: 0, approved: 0, rejected: 0, total: 0 });

    const loadPublications = useCallback(() => {
        setLoading(true);
        setError(null);
        const statusParam = STATUS_PARAM_MAP[filterStatus];
        const params = { page: 1, pageSize: 100 };
        if (statusParam) params.status = statusParam;

        getStories(params)
            .then((res) => {
                const items = res?.items ?? res?.Items ?? [];
                setPublications(items.map(mapStoryToPublication));
            })
            .catch((err) => {
                setError(err?.message ?? 'Không tải được danh sách truyện');
                setPublications([]);
            })
            .finally(() => setLoading(false));
    }, [filterStatus]);

    const loadStats = useCallback(() => {
        getStories({ page: 1, pageSize: 500 })
            .then((res) => {
                const items = res?.items ?? res?.Items ?? [];
                const mapped = items.map(mapStoryToPublication);
                setStatsData({
                    pending: mapped.filter(p => p.status === 'pending').length,
                    approved: mapped.filter(p => p.status === 'approved').length,
                    rejected: mapped.filter(p => p.status === 'rejected').length,
                    total: mapped.length
                });
            })
            .catch(() => setStatsData({ pending: 0, approved: 0, rejected: 0, total: 0 }));
    }, []);

    useEffect(() => {
        const id = setTimeout(() => loadPublications(), 0);
        return () => clearTimeout(id);
    }, [loadPublications]);

    useEffect(() => {
        const id = setTimeout(() => loadStats(), 0);
        return () => clearTimeout(id);
    }, [loadStats]);

    const filteredPublications = publications;

    const handleViewDetail = (publication) => {
        setSelectedPublication(publication);
    };

    const handleCloseDetail = () => {
        setSelectedPublication(null);
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
                    Duyệt và phê duyệt các truyện, chương mới từ tác giả
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
                <PublicationList
                    publications={filteredPublications}
                    onViewDetail={handleViewDetail}
                />
            )}

            {/* Detail Modal */}
            {selectedPublication && (
                <PublicationDetailModal
                    publication={selectedPublication}
                    onClose={handleCloseDetail}
                    onApprove={handleApprove}
                    onReject={handleReject}
                />
            )}
        </div>
    );
}
