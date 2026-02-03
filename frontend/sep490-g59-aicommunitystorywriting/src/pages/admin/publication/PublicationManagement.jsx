import { useState } from 'react';
import { PublicationList } from '../../../components/admin/publication/PublicationList';
import { PublicationDetailModal } from '../../../components/admin/publication/PublicationDetailModal';

export function PublicationManagement() {
    const [selectedPublication, setSelectedPublication] = useState(null);
    const [filterStatus, setFilterStatus] = useState('pending'); // 'pending' | 'approved' | 'rejected' | 'all'

    // Mock data - Danh sách truyện chờ xuất bản
    const publications = [
        {
            id: 1,
            storyId: 101,
            storyTitle: 'Tu Tiên Chi Lộ: Hành Trình Vạn Năm',
            storyCover: 'https://images.unsplash.com/photo-1589998059171-988d887df646?w=300&h=400&fit=crop',
            author: 'Quyền Đình',
            authorId: 501,
            type: 'new_story', // 'new_story' | 'new_chapter'
            status: 'pending', // 'pending' | 'approved' | 'rejected'
            submittedAt: '2026-02-03T10:30:00',
            reviewedAt: null,
            reviewedBy: null,
            rejectionReason: null,
            chapters: [
                {
                    id: 1001,
                    chapterNumber: 1,
                    title: 'Khởi đầu hành trình',
                    content: 'Trên đỉnh núi Tu Tiên, một thiếu niên đang ngồi thiền định. Tên của cậu là Trần Vũ, một người bình thường đến từ làng nhỏ dưới chân núi.\n\nHôm nay, cậu đã quyết định bước vào con đường tu tiên đầy gian nan. Không ai biết trước được những gì đang chờ đợi cậu phía trước...\n\n"Ta phải trở nên mạnh mẽ hơn!" Trần Vũ thầm nghĩ trong lòng.\n\nGió núi thổi qua, mang theo hơi lạnh của mùa đông. Nhưng trong lòng cậu, ngọn lửa quyết tâm đang bùng cháy mãnh liệt.',
                    wordCount: 125,
                    status: 'pending'
                },
                {
                    id: 1002,
                    chapterNumber: 2,
                    title: 'Gặp sư phụ',
                    content: 'Sáng sớm hôm sau, Trần Vũ được gọi đến gặp trưởng môn. Đó là một lão nhân với bộ râu dài và đôi mắt sâu thẳm.\n\n"Ngươi có thực sự muốn tu tiên?" Lão nhân hỏi.\n\n"Đệ tử quyết tâm!" Trần Vũ quỳ xuống đáp.\n\nLão nhân gật đầu, "Tốt lắm. Ta nhận ngươi làm đệ tử."',
                    wordCount: 89,
                    status: 'pending'
                }
            ],
            totalChapters: 2,
            totalWords: 214,
            categories: ['Tiên hiệp', 'Huyền huyễn'],
            description: 'Một thiếu niên bình thường bước vào con đường tu tiên đầy gian nan. Liệu cậu có thể vượt qua những thử thách và đạt được đỉnh cao của tu tiên?'
        },
        {
            id: 2,
            storyId: 102,
            storyTitle: 'Kiếm Đạo Độc Tôn',
            storyCover: 'https://images.unsplash.com/photo-1612036801632-8e4cf4e2e1b7?w=300&h=400&fit=crop',
            author: 'Phong Hỏa',
            authorId: 502,
            type: 'new_chapter',
            status: 'pending',
            submittedAt: '2026-02-03T09:15:00',
            reviewedAt: null,
            reviewedBy: null,
            rejectionReason: null,
            chapters: [
                {
                    id: 2001,
                    chapterNumber: 524,
                    title: 'Đại chiến',
                    content: 'Thanh kiếm trong tay Dương Huyền phát ra ánh sáng rực rỡ. Trước mặt là kẻ thù hùng mạnh nhất từ trước đến nay.\n\n"Kiếm Đạo Độc Tôn!" Dương Huyền gầm lên.\n\nMột đạo kiếm khí khổng lồ tách đôi bầu trời...',
                    wordCount: 67,
                    status: 'pending'
                }
            ],
            totalChapters: 1,
            totalWords: 67,
            categories: ['Kiếm hiệp'],
            description: 'Chương mới của series Kiếm Đạo Độc Tôn'
        },
        {
            id: 3,
            storyId: 103,
            storyTitle: 'Ngôn Tình Mùa Hè',
            storyCover: 'https://images.unsplash.com/photo-1544947950-fa07a98d237f?w=300&h=400&fit=crop',
            author: 'Minh Nguyệt',
            authorId: 503,
            type: 'new_story',
            status: 'approved',
            submittedAt: '2026-02-02T14:20:00',
            reviewedAt: '2026-02-02T16:45:00',
            reviewedBy: 'Admin User',
            rejectionReason: null,
            chapters: [
                {
                    id: 3001,
                    chapterNumber: 1,
                    title: 'Buổi sáng đầu tiên',
                    content: 'Ánh nắng mùa hè chiếu qua cửa sổ...',
                    wordCount: 45,
                    status: 'approved'
                }
            ],
            totalChapters: 1,
            totalWords: 45,
            categories: ['Ngôn tình', 'Teen'],
            description: 'Câu chuyện tình yêu ngọt ngào'
        },
        {
            id: 4,
            storyId: 104,
            storyTitle: 'Bí Ẩn Biệt Thự Cổ',
            storyCover: 'https://images.unsplash.com/photo-1518709268805-4e9042af9f23?w=300&h=400&fit=crop',
            author: 'Hắc Ảnh',
            authorId: 504,
            type: 'new_chapter',
            status: 'rejected',
            submittedAt: '2026-02-01T11:30:00',
            reviewedAt: '2026-02-01T15:20:00',
            reviewedBy: 'Admin User',
            rejectionReason: 'Nội dung chứa yếu tố bạo lực quá mức. Vui lòng chỉnh sửa theo hướng dẫn cộng đồng.',
            chapters: [
                {
                    id: 4001,
                    chapterNumber: 29,
                    title: 'Hành lang tối',
                    content: 'Máu chảy ròng ròng...',
                    wordCount: 23,
                    status: 'rejected'
                }
            ],
            totalChapters: 1,
            totalWords: 23,
            categories: ['Kinh dị'],
            description: 'Chương bị từ chối do vi phạm quy định'
        }
    ];

    // Filter publications
    const filteredPublications = publications.filter(pub => {
        if (filterStatus === 'all') return true;
        return pub.status === filterStatus;
    });

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
        // Refresh list
    };

    const handleReject = (publicationId, reason) => {
        // TODO: API call to reject
        console.log('Rejected publication:', publicationId, 'Reason:', reason);
        setSelectedPublication(null);
        // Refresh list
    };

    // Statistics
    const stats = {
        pending: publications.filter(p => p.status === 'pending').length,
        approved: publications.filter(p => p.status === 'approved').length,
        rejected: publications.filter(p => p.status === 'rejected').length,
        total: publications.length
    };

    return (
        <div style={{ padding: '2rem' }}>
            {/* Header */}
            <div style={{ marginBottom: '2rem' }}>
                <h1 style={{
                    fontSize: '1.875rem',
                    fontWeight: 700,
                    color: '#1e293b',
                    marginBottom: '0.5rem',
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
            <PublicationList
                publications={filteredPublications}
                onViewDetail={handleViewDetail}
            />

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
