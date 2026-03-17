import React from 'react';
import { Eye, AlertCircle, Clock, ChevronRight, Users } from 'lucide-react';

const ViolationList = ({ violations, onViewDetail }) => {
    const getPriorityStyle = (priority) => {
        const map = {
            critical: { backgroundColor: '#fee2e2', color: '#b91c1c', borderColor: '#fca5a5' },
            high: { backgroundColor: '#ffedd5', color: '#c2410c', borderColor: '#fdba74' },
            medium: { backgroundColor: '#fef9c3', color: '#a16207', borderColor: '#fde047' },
            low: { backgroundColor: '#dbeafe', color: '#1d4ed8', borderColor: '#93c5fd' },
        };
        return { padding: '0.25rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', fontWeight: 500, border: '1px solid', ...(map[priority] || { backgroundColor: '#f3f4f6', color: '#374151', borderColor: '#d1d5db' }) };
    };

    const getStatusStyle = (status) => {
        const map = {
            pending: { backgroundColor: '#ffedd5', color: '#c2410c', borderColor: '#fdba74' },
            in_review: { backgroundColor: '#dbeafe', color: '#1d4ed8', borderColor: '#93c5fd' },
            resolved: { backgroundColor: '#dcfce7', color: '#15803d', borderColor: '#86efac' },
            rejected: { backgroundColor: '#f3f4f6', color: '#374151', borderColor: '#d1d5db' },
        };
        return { padding: '0.25rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', fontWeight: 500, border: '1px solid', ...(map[status] || { backgroundColor: '#f3f4f6', color: '#374151', borderColor: '#d1d5db' }) };
    };

    const getStatusText = (status) => {
        switch (status) {
            case 'pending':
                return 'Chờ xử lý';
            case 'in_review':
                return 'Đang xem xét';
            case 'resolved':
                return 'Đã giải quyết';
            case 'rejected':
                return 'Đã từ chối';
            default:
                return status;
        }
    };

    const getPriorityText = (priority) => {
        switch (priority) {
            case 'critical':
                return 'Nghiêm trọng';
            case 'high':
                return 'Cao';
            case 'medium':
                return 'Trung bình';
            case 'low':
                return 'Thấp';
            default:
                return priority;
        }
    };

    const formatDate = (dateString) => {
        const date = new Date(dateString);
        return date.toLocaleDateString('vi-VN', {
            day: '2-digit',
            month: '2-digit',
            year: 'numeric',
            hour: '2-digit',
            minute: '2-digit',
        });
    };

    if (violations.length === 0) {
        return (
            <div style={{ backgroundColor: '#fff', borderRadius: '0.5rem', border: '1px solid #e2e8f0', padding: '3rem', textAlign: 'center' }}>
                <AlertCircle style={{ width: 64, height: 64, color: '#d1d5db', margin: '0 auto 1rem' }} />
                <h3 style={{ fontSize: '1.25rem', fontWeight: 600, color: '#4b5563', marginBottom: '0.5rem', margin: 0 }}>
                    Không có báo cáo vi phạm
                </h3>
                <p style={{ color: '#6b7280', margin: 0 }}>
                    Không tìm thấy báo cáo vi phạm nào phù hợp với tiêu chí tìm kiếm
                </p>
            </div>
        );
    }

    return (
        <div style={{ backgroundColor: '#fff', borderRadius: '0.5rem', border: '1px solid #e2e8f0', overflow: 'hidden' }}>
            <div style={{ overflowX: 'auto' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
                    <thead>
                        <tr style={{ backgroundColor: '#f8fafc' }}>
                            <th style={{ padding: '0.75rem 1rem', textAlign: 'left', fontWeight: 600, color: '#111827' }}>Nội dung bị báo cáo</th>
                            <th style={{ padding: '0.75rem 1rem', textAlign: 'left', fontWeight: 600, color: '#111827' }}>Tác giả</th>
                            <th style={{ padding: '0.75rem 1rem', textAlign: 'left', fontWeight: 600, color: '#111827' }}>Số báo cáo</th>
                            <th style={{ padding: '0.75rem 1rem', textAlign: 'left', fontWeight: 600, color: '#111827' }}>Mức độ</th>
                            <th style={{ padding: '0.75rem 1rem', textAlign: 'left', fontWeight: 600, color: '#111827' }}>Trạng thái</th>
                            <th style={{ padding: '0.75rem 1rem', textAlign: 'left', fontWeight: 600, color: '#111827' }}>Báo cáo đầu/cuối</th>
                            <th style={{ padding: '0.75rem 1rem', textAlign: 'right', fontWeight: 600, color: '#111827' }}>Thao tác</th>
                        </tr>
                    </thead>
                    <tbody>
                        {violations.map((violation) => (
                            <tr key={violation.id} style={{ borderTop: '1px solid #e2e8f0' }}>
                                <td style={{ padding: '0.75rem 1rem' }}>
                                    <div>
                                        <p style={{ fontWeight: 500, color: '#111827', margin: 0 }}>{violation.storyTitle}</p>
                                        {violation.chapterNumber ? (
                                            <p style={{ fontSize: '0.875rem', color: '#6b7280', margin: '0.25rem 0 0 0' }}>
                                                Chương {violation.chapterNumber}: {violation.chapterTitle}
                                            </p>
                                        ) : (
                                            <p style={{ fontSize: '0.875rem', color: '#6b7280', margin: '0.25rem 0 0 0' }}>Toàn bộ truyện</p>
                                        )}
                                    </div>
                                </td>
                                <td style={{ padding: '0.75rem 1rem', color: '#374151' }}>{violation.authorName}</td>
                                <td style={{ padding: '0.75rem 1rem' }}>
                                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                        <div style={{ display: 'inline-flex', alignItems: 'center', gap: '0.25rem', backgroundColor: '#fef2f2', color: '#b91c1c', padding: '0.25rem 0.5rem', borderRadius: '9999px' }}>
                                            <Users style={{ width: 16, height: 16 }} />
                                            <span style={{ fontWeight: 600 }}>{violation.reportCount}</span>
                                        </div>
                                        {violation.reportCount > 1 && <span style={{ fontSize: '0.75rem', color: '#6b7280' }}>báo cáo</span>}
                                    </div>
                                </td>
                                <td style={{ padding: '0.75rem 1rem' }}>
                                    <span style={{ display: 'inline-block', ...getPriorityStyle(violation.priority) }}>
                                        {getPriorityText(violation.priority)}
                                    </span>
                                </td>
                                <td style={{ padding: '0.75rem 1rem' }}>
                                    <span style={{ display: 'inline-block', ...getStatusStyle(violation.status) }}>
                                        {getStatusText(violation.status)}
                                    </span>
                                </td>
                                <td style={{ padding: '0.75rem 1rem', fontSize: '0.875rem' }}>
                                    <div>
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem', color: '#4b5563' }}>
                                            <Clock style={{ width: 12, height: 12 }} />
                                            <span style={{ fontSize: '0.75rem' }}>Đầu:</span>
                                            <span>{formatDate(violation.firstReportedAt)}</span>
                                        </div>
                                        {violation.reportCount > 1 && (
                                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem', color: '#6b7280', marginTop: '0.25rem' }}>
                                                <Clock style={{ width: 12, height: 12 }} />
                                                <span style={{ fontSize: '0.75rem' }}>Cuối:</span>
                                                <span>{formatDate(violation.lastReportedAt)}</span>
                                            </div>
                                        )}
                                    </div>
                                </td>
                                <td style={{ padding: '0.75rem 1rem', textAlign: 'right' }}>
                                    <button
                                        type="button"
                                        onClick={() => onViewDetail(violation)}
                                        style={{
                                            display: 'inline-flex', alignItems: 'center', gap: '0.25rem',
                                            padding: '0.375rem 0.75rem', border: '1px solid #e2e8f0', borderRadius: '0.5rem',
                                            backgroundColor: 'transparent', cursor: 'pointer', fontSize: '0.875rem',
                                        }}
                                        onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = 'rgba(19, 236, 91, 0.15)'; e.currentTarget.style.borderColor = '#13ec5b'; e.currentTarget.style.color = '#0f172a'; }}
                                        onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = 'transparent'; e.currentTarget.style.borderColor = '#e2e8f0'; e.currentTarget.style.color = ''; }}
                                    >
                                        <Eye style={{ width: 16, height: 16 }} />
                                        Xử lý
                                        <ChevronRight style={{ width: 16, height: 16 }} />
                                    </button>
                                </td>
                            </tr>
                        ))}
                    </tbody>
                </table>
            </div>
        </div>
    );
};

export default ViolationList;