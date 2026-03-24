import React, { useState } from 'react';
import { Search, AlertTriangle, Shield, FileText, Filter } from 'lucide-react';
import ViolationList from '../../../components/admin/violation/ViolationList';
import ViolationDetailModal from '../../../components/admin/violation/ViolationDetailModal';
import PolicyModal from '../../../components/admin/violation/PolicyModal';

const ViolationManagement = () => {
    const [searchQuery, setSearchQuery] = useState('');
    const [statusFilter, setStatusFilter] = useState('all');
    const [selectedViolation, setSelectedViolation] = useState(null);
    const [showPolicyModal, setShowPolicyModal] = useState(false);
    const [activeTab, setActiveTab] = useState('pending');

    // Mock data - reported violations with multiple reports
    const [violations, setViolations] = useState([
        {
            id: 1,
            storyId: 101,
            storyTitle: 'Hành Trình Về Phương Đông',
            authorName: 'Nguyễn Văn A',
            authorId: 1001,
            chapterNumber: 15,
            chapterTitle: 'Cuộc chiến cuối cùng',
            status: 'pending',
            priority: 'high',
            reportCount: 3,
            reports: [
                {
                    id: 1001,
                    reportedBy: 'User123',
                    reportedByEmail: 'user123@example.com',
                    reportedAt: '2026-03-05T10:30:00',
                    reason: 'Nội dung 18+',
                    description: 'Chương 15 có nội dung nhạy cảm không phù hợp với cộng đồng',
                    evidence: 'Đoạn văn từ dòng 45-60 mô tả cảnh không phù hợp',
                    evidenceLinks: ['https://example.com/screenshot.png'],
                    evidenceImages: ['https://example.com/evidence1.jpg'],
                },
                {
                    id: 1002,
                    reportedBy: 'User456',
                    reportedByEmail: 'user456@example.com',
                    reportedAt: '2026-03-05T14:20:00',
                    reason: 'Nội dung 18+',
                    description: 'Chương này có nhiều nội dung không lành mạnh',
                    evidence: 'Nhiều đoạn văn có nội dung phản cảm',
                },
                {
                    id: 1003,
                    reportedBy: 'Moderator1',
                    reportedByEmail: 'mod1@example.com',
                    reportedAt: '2026-03-05T16:00:00',
                    reason: 'Vi phạm quy định cộng đồng',
                    description: 'Nội dung không phù hợp với độ tuổi 13+',
                    evidence: 'Xác nhận từ nhiều báo cáo người dùng',
                },
            ],
            contentSnapshot: 'Đoạn nội dung chương 15: Lorem ipsum dolor sit amet... [Nội dung nhạy cảm đã bị ẩn]',
            firstReportedAt: '2026-03-05T10:30:00',
            lastReportedAt: '2026-03-05T16:00:00',
        },
        {
            id: 2,
            storyId: 102,
            storyTitle: 'Tình Yêu Và Tham Vọng',
            authorName: 'Trần Thị B',
            authorId: 1002,
            chapterNumber: null,
            chapterTitle: null,
            status: 'pending',
            priority: 'critical',
            reportCount: 5,
            reports: [
                {
                    id: 2001,
                    reportedBy: 'User789',
                    reportedByEmail: 'user789@example.com',
                    reportedAt: '2026-03-04T15:20:00',
                    reason: 'Vi phạm bản quyền',
                    description: 'Truyện copy nguyên văn từ tác phẩm "Tình yêu muôn màu" của tác giả X',
                    evidence: 'Link nguồn: https://example.com/original-work',
                    evidenceLinks: ['https://example.com/original-work', 'https://example.com/compare'],
                    evidenceImages: [],
                },
                {
                    id: 2002,
                    reportedBy: 'Author_Original',
                    reportedByEmail: 'author@example.com',
                    reportedAt: '2026-03-04T16:00:00',
                    reason: 'Vi phạm bản quyền',
                    description: 'Tôi là tác giả gốc, đây là tác phẩm của tôi bị sao chép',
                    evidence: 'Bằng chứng: File word gốc, thời gian xuất bản',
                },
                {
                    id: 2003,
                    reportedBy: 'User101',
                    reportedByEmail: 'user101@example.com',
                    reportedAt: '2026-03-04T17:30:00',
                    reason: 'Vi phạm bản quyền',
                    description: 'Truyện này giống hệt truyện đã publish trên platform khác',
                    evidence: 'So sánh nội dung chương 1-5',
                },
                {
                    id: 2004,
                    reportedBy: 'Moderator2',
                    reportedByEmail: 'mod2@example.com',
                    reportedAt: '2026-03-04T18:00:00',
                    reason: 'Vi phạm bản quyền',
                    description: 'Xác nhận vi phạm bản quyền nghiêm trọng',
                    evidence: 'Đã kiểm tra và xác thực',
                },
                {
                    id: 2005,
                    reportedBy: 'User202',
                    reportedByEmail: 'user202@example.com',
                    reportedAt: '2026-03-04T19:00:00',
                    reason: 'Vi phạm bản quyền',
                    description: 'Nội dung sao chép 100%',
                    evidence: 'Phát hiện qua công cụ so sánh văn bản',
                },
            ],
            contentSnapshot: 'Nội dung toàn bộ truyện...',
            firstReportedAt: '2026-03-04T15:20:00',
            lastReportedAt: '2026-03-04T19:00:00',
        },
        {
            id: 3,
            storyId: 103,
            storyTitle: 'Kiếm Hiệp Giang Hồ',
            authorName: 'Lê Văn C',
            authorId: 1003,
            chapterNumber: 8,
            chapterTitle: 'Đại chiến võ lâm',
            status: 'in_review',
            priority: 'medium',
            reportCount: 2,
            reports: [
                {
                    id: 3001,
                    reportedBy: 'User456',
                    reportedByEmail: 'user456@example.com',
                    reportedAt: '2026-03-03T09:15:00',
                    reason: 'Spam quảng cáo',
                    description: 'Chèn nhiều link quảng cáo trong nội dung',
                    evidence: 'Link: https://spam-link.com xuất hiện 10 lần',
                },
                {
                    id: 3002,
                    reportedBy: 'User303',
                    reportedByEmail: 'user303@example.com',
                    reportedAt: '2026-03-03T11:00:00',
                    reason: 'Spam quảng cáo',
                    description: 'Quảng cáo sản phẩm không liên quan',
                    evidence: 'Nhiều đoạn quảng cáo sản phẩm gaming',
                },
            ],
            contentSnapshot: 'Chương 8: Nội dung... [Link quảng cáo] ... tiếp tục nội dung...',
            firstReportedAt: '2026-03-03T09:15:00',
            lastReportedAt: '2026-03-03T11:00:00',
            reviewNote: 'Đang xem xét mức độ vi phạm',
            reviewedAt: '2026-03-04T10:00:00',
            reviewedBy: 'Admin1',
        },
        {
            id: 4,
            storyId: 104,
            storyTitle: 'Mộng Mơ Tuổi Học Trò',
            authorName: 'Phạm Thị D',
            authorId: 1004,
            chapterNumber: 20,
            chapterTitle: 'Kết thúc có hậu',
            status: 'resolved',
            priority: 'low',
            reportCount: 1,
            reports: [
                {
                    id: 4001,
                    reportedBy: 'User789',
                    reportedByEmail: 'user789@example.com',
                    reportedAt: '2026-03-02T14:45:00',
                    reason: 'Bạo lực',
                    description: 'Mô tả cảnh bạo lực quá chi tiết',
                    evidence: 'Đoạn mô tả đánh nhau từ trang 5-7',
                    notified: true,
                    notificationSentAt: '2026-03-03T12:00:00',
                    resolutionMessage: 'Tác giả đã chỉnh sửa nội dung theo yêu cầu. Nội dung hiện tại đã phù hợp với quy định.',
                },
            ],
            contentSnapshot: 'Chương 20: Nội dung...',
            firstReportedAt: '2026-03-02T14:45:00',
            lastReportedAt: '2026-03-02T14:45:00',
            reviewNote: 'Yêu cầu tác giả chỉnh sửa nội dung bạo lực. Tác giả đã hoàn thành chỉnh sửa.',
            reviewedAt: '2026-03-03T11:30:00',
            reviewedBy: 'Admin2',
            action: 'request_edit',
            actionCompletedAt: '2026-03-03T11:45:00',
        },
        {
            id: 5,
            storyId: 105,
            storyTitle: 'Đế Vương Trở Về',
            authorName: 'Hoàng Văn E',
            authorId: 1005,
            chapterNumber: 5,
            chapterTitle: 'Trả thù',
            status: 'rejected',
            priority: 'low',
            reportCount: 1,
            reports: [
                {
                    id: 5001,
                    reportedBy: 'User999',
                    reportedByEmail: 'user999@example.com',
                    reportedAt: '2026-03-01T16:20:00',
                    reason: 'Nội dung không phù hợp',
                    description: 'Sử dụng ngôn từ xúc phạm, kích động',
                    evidence: 'Các từ ngữ không phù hợp ở đoạn 3',
                    notified: true,
                    notificationSentAt: '2026-03-02T09:15:00',
                    resolutionMessage: 'Sau khi xem xét kỹ lưỡng, nội dung báo cáo không đủ căn cứ. Các từ ngữ sử dụng phù hợp với ngữ cảnh văn học.',
                },
            ],
            contentSnapshot: 'Chương 5: Nội dung...',
            firstReportedAt: '2026-03-01T16:20:00',
            lastReportedAt: '2026-03-01T16:20:00',
            reviewNote: 'Báo cáo không đủ căn cứ. Nội dung phù hợp với thể loại kiếm hiệp.',
            reviewedAt: '2026-03-02T09:00:00',
            reviewedBy: 'Admin1',
            action: 'reject',
        },
    ]);

    // Statistics
    const stats = {
        pending: violations.filter(v => v.status === 'pending').length,
        in_review: violations.filter(v => v.status === 'in_review').length,
        resolved: violations.filter(v => v.status === 'resolved').length,
        rejected: violations.filter(v => v.status === 'rejected').length,
    };

    const handleViewDetail = (violation) => {
        setSelectedViolation(violation);
    };

    const handleCloseDetail = () => {
        setSelectedViolation(null);
    };

    const handleUpdateViolation = (id, updates) => {
        setViolations(prev =>
            prev.map(v => (v.id === id ? { ...v, ...updates } : v))
        );
    };

    const filteredViolations = violations.filter(v => {
        const matchesSearch =
            v.storyTitle.toLowerCase().includes(searchQuery.toLowerCase()) ||
            v.authorName.toLowerCase().includes(searchQuery.toLowerCase()) ||
            v.reports.some(r => r.reason.toLowerCase().includes(searchQuery.toLowerCase()));

        const matchesStatus = activeTab === 'all' || v.status === activeTab;

        const matchesPriority = statusFilter === 'all' || v.priority === statusFilter;

        return matchesSearch && matchesStatus && matchesPriority;
    });

    return (
        <div style={{ padding: '2rem', maxWidth: '1400px', margin: '0 auto' }}>
            {/* Header */}
            <div style={{ marginBottom: '2rem' }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.5rem', flexWrap: 'wrap', gap: '0.5rem' }}>
                    <div>
                        <h1 style={{ fontSize: '1.875rem', fontWeight: 700, color: '#0f172a', margin: 0 }}>
                            Xử lý Báo cáo Vi phạm
                        </h1>
                        <p style={{ color: '#475569', marginTop: '0.25rem', margin: 0 }}>
                            Xem và xử lý các báo cáo vi phạm nội dung từ cộng đồng (Hậu kiểm)
                        </p>
                    </div>
                    <button
                        type="button"
                        onClick={() => setShowPolicyModal(true)}
                        style={{
                            display: 'inline-flex', alignItems: 'center', gap: '0.5rem',
                            padding: '0.5rem 1rem', backgroundColor: '#13ec5b', color: '#111827',
                            border: 'none', borderRadius: '0.5rem', fontWeight: 600, cursor: 'pointer',
                        }}
                        onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = '#11d351'; }}
                        onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = '#13ec5b'; }}
                    >
                        <Shield style={{ width: '16px', height: '16px' }} />
                        Quy định Vi phạm
                    </button>
                </div>
            </div>

            {/* Stats Cards - màu hệ thống: primary #13ec5b, slate */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1rem', marginBottom: '1.5rem' }}>
                <div style={{ backgroundColor: 'rgba(19, 236, 91, 0.12)', border: '1px solid rgba(19, 236, 91, 0.35)', borderRadius: '0.5rem', padding: '1rem' }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <div>
                            <p style={{ fontSize: '0.875rem', color: '#0f172a', fontWeight: 500, margin: 0 }}>Chờ xử lý</p>
                            <p style={{ fontSize: '1.5rem', fontWeight: 700, color: '#0f172a', marginTop: '0.25rem', margin: 0 }}>{stats.pending}</p>
                        </div>
                        <div style={{ width: 48, height: 48, backgroundColor: 'rgba(19, 236, 91, 0.25)', borderRadius: '50%', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                            <AlertTriangle style={{ width: 24, height: 24, color: '#13ec5b' }} />
                        </div>
                    </div>
                </div>
                <div style={{ backgroundColor: '#f1f5f9', border: '1px solid #e2e8f0', borderRadius: '0.5rem', padding: '1rem' }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <div>
                            <p style={{ fontSize: '0.875rem', color: '#475569', fontWeight: 500, margin: 0 }}>Đang xem xét</p>
                            <p style={{ fontSize: '1.5rem', fontWeight: 700, color: '#0f172a', marginTop: '0.25rem', margin: 0 }}>{stats.in_review}</p>
                        </div>
                        <div style={{ width: 48, height: 48, backgroundColor: '#e2e8f0', borderRadius: '50%', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                            <FileText style={{ width: 24, height: 24, color: '#64748b' }} />
                        </div>
                    </div>
                </div>
                <div style={{ backgroundColor: 'rgba(19, 236, 91, 0.08)', border: '1px solid rgba(19, 236, 91, 0.3)', borderRadius: '0.5rem', padding: '1rem' }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <div>
                            <p style={{ fontSize: '0.875rem', color: '#0f172a', fontWeight: 500, margin: 0 }}>Đã giải quyết</p>
                            <p style={{ fontSize: '1.5rem', fontWeight: 700, color: '#13ec5b', marginTop: '0.25rem', margin: 0 }}>{stats.resolved}</p>
                        </div>
                        <div style={{ width: 48, height: 48, backgroundColor: 'rgba(19, 236, 91, 0.2)', borderRadius: '50%', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                            <Shield style={{ width: 24, height: 24, color: '#13ec5b' }} />
                        </div>
                    </div>
                </div>
                <div style={{ backgroundColor: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: '0.5rem', padding: '1rem' }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <div>
                            <p style={{ fontSize: '0.875rem', color: '#64748b', fontWeight: 500, margin: 0 }}>Đã từ chối</p>
                            <p style={{ fontSize: '1.5rem', fontWeight: 700, color: '#475569', marginTop: '0.25rem', margin: 0 }}>{stats.rejected}</p>
                        </div>
                        <div style={{ width: 48, height: 48, backgroundColor: '#e2e8f0', borderRadius: '50%', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                            <FileText style={{ width: 24, height: 24, color: '#64748b' }} />
                        </div>
                    </div>
                </div>
            </div>

            {/* Search and Filter */}
            <div style={{ backgroundColor: '#fff', borderRadius: '0.5rem', border: '1px solid #e2e8f0', padding: '1rem', marginBottom: '1.5rem' }}>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                    <div style={{ flex: 1, position: 'relative' }}>
                        <Search style={{ position: 'absolute', left: 12, top: '50%', transform: 'translateY(-50%)', width: 20, height: 20, color: '#9ca3af' }} />
                        <input
                            type="text"
                            placeholder="Tìm kiếm theo tên truyện, tác giả, loại vi phạm..."
                            value={searchQuery}
                            onChange={(e) => setSearchQuery(e.target.value)}
                            style={{
                                width: '100%', padding: '0.5rem 0.75rem 0.5rem 2.5rem', border: '1px solid #e2e8f0',
                                borderRadius: '0.5rem', fontSize: '0.875rem', outline: 'none',
                            }}
                        />
                    </div>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                        <Filter style={{ width: 16, height: 16, color: '#6b7280' }} />
                        <select
                            value={statusFilter}
                            onChange={(e) => setStatusFilter(e.target.value)}
                            style={{
                                padding: '0.5rem 0.75rem', border: '1px solid #e2e8f0', borderRadius: '0.5rem',
                                fontSize: '0.875rem', minWidth: 200, outline: 'none',
                            }}
                        >
                            <option value="all">Tất cả mức độ</option>
                            <option value="critical">Nghiêm trọng</option>
                            <option value="high">Cao</option>
                            <option value="medium">Trung bình</option>
                            <option value="low">Thấp</option>
                        </select>
                    </div>
                </div>
            </div>

            {/* Tabs */}
            <div style={{ marginBottom: '1.5rem' }}>
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', marginBottom: '1rem', borderBottom: '1px solid #e5e7eb', paddingBottom: '0.5rem' }}>
                    {[
                        { value: 'pending', label: `Chờ xử lý (${stats.pending})` },
                        { value: 'in_review', label: `Đang xem xét (${stats.in_review})` },
                        { value: 'resolved', label: `Đã giải quyết (${stats.resolved})` },
                        { value: 'rejected', label: `Đã từ chối (${stats.rejected})` },
                        { value: 'all', label: `Tất cả (${violations.length})` },
                    ].map((tab) => (
                        <button
                            key={tab.value}
                            type="button"
                            onClick={() => setActiveTab(tab.value)}
                            style={{
                                padding: '0.5rem 1rem', border: 'none', borderRadius: '0.5rem', fontSize: '0.875rem', fontWeight: 500,
                                cursor: 'pointer', backgroundColor: activeTab === tab.value ? 'rgba(19, 236, 91, 0.15)' : 'transparent', color: activeTab === tab.value ? '#13ec5b' : '#475569',
                            }}
                        >
                            {tab.label}
                        </button>
                    ))}
                </div>
                <ViolationList
                    violations={filteredViolations}
                    onViewDetail={handleViewDetail}
                />
            </div>

            {/* Modals */}
            {selectedViolation && (
                <ViolationDetailModal
                    violation={selectedViolation}
                    onClose={handleCloseDetail}
                    onUpdate={handleUpdateViolation}
                />
            )}

            {showPolicyModal && (
                <PolicyModal onClose={() => setShowPolicyModal(false)} />
            )}
        </div>
    );
};

export default ViolationManagement;