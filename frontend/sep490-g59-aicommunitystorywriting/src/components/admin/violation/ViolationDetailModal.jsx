import React, { useState } from 'react';
import {
    X,
    AlertTriangle,
    User,
    Clock,
    FileText,
    Eye,
    EyeOff,
    MessageSquare,
    CheckCircle,
    XCircle,
    ExternalLink,
    Send,
    Edit3,
    Trash2,
    Mail,
    Shield,
} from 'lucide-react';

const ViolationDetailModal = ({ violation, onClose, onUpdate }) => {
    const [reviewNote, setReviewNote] = useState(violation.reviewNote || '');
    const [selectedAction, setSelectedAction] = useState('');
    const [selectedSeverity, setSelectedSeverity] = useState('');
    const [notificationMessage, setNotificationMessage] = useState('');
    const [isProcessing, setIsProcessing] = useState(false);
    const [showContentSnapshot, setShowContentSnapshot] = useState(false);

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

    const handleTakeAction = async () => {
        if (!selectedAction) {
            alert('Vui lòng chọn hành động xử lý');
            return;
        }

        if (!selectedSeverity) {
            alert('Vui lòng đánh giá mức độ vi phạm');
            return;
        }

        if (!reviewNote.trim()) {
            alert('Vui lòng nhập ghi chú xem xét');
            return;
        }

        if (!notificationMessage.trim()) {
            alert('Vui lòng nhập thông báo gửi cho người báo cáo');
            return;
        }

        setIsProcessing(true);

        setTimeout(() => {
            const updates = {
                status: selectedAction === 'reject' ? 'rejected' : 'resolved',
                reviewNote,
                reviewedAt: new Date().toISOString(),
                reviewedBy: 'Compliance Officer',
                action: selectedAction,
                severity: selectedSeverity,
                reports: violation.reports.map(r => ({
                    ...r,
                    notified: true,
                    notificationSentAt: new Date().toISOString(),
                    resolutionMessage: notificationMessage,
                })),
            };

            onUpdate(violation.id, updates);
            setIsProcessing(false);
            onClose();
        }, 1500);
    };

    return (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 50, padding: '1rem' }}>
            <div style={{ backgroundColor: '#fff', borderRadius: '0.75rem', boxShadow: '0 25px 50px -12px rgba(0,0,0,0.25)', width: '100%', maxWidth: '64rem', maxHeight: '90vh', overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
                {/* Header - màu hệ thống primary */}
                <div style={{ background: 'linear-gradient(to right, #13ec5b, #0ec252)', padding: '1.5rem' }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                            <div style={{ width: 48, height: 48, backgroundColor: 'rgba(15,23,42,0.15)', borderRadius: '50%', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                <AlertTriangle style={{ width: 24, height: 24, color: '#0f172a' }} />
                            </div>
                            <div>
                                <h2 style={{ fontSize: '1.5rem', fontWeight: 700, color: '#0f172a', margin: 0 }}>
                                    Xử lý Báo cáo Vi phạm
                                </h2>
                                <p style={{ color: '#334155', fontSize: '0.875rem', marginTop: '0.25rem', margin: 0 }}>
                                    Mã: #{violation.id} • {violation.reportCount} báo cáo từ cộng đồng
                                </p>
                            </div>
                        </div>
                        <button
                            type="button"
                            onClick={onClose}
                            style={{ padding: '0.5rem', border: 'none', background: 'transparent', color: '#0f172a', cursor: 'pointer', borderRadius: '0.5rem' }}
                            onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = 'rgba(15,23,42,0.1)'; }}
                            onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = 'transparent'; }}
                        >
                            <X style={{ width: 20, height: 20 }} />
                        </button>
                    </div>
                </div>

                {/* Content */}
                <div style={{ flex: 1, overflowY: 'auto', padding: '1.5rem' }}>
                    {/* Story Info */}
                    <div style={{ backgroundColor: '#f9fafb', borderRadius: '0.5rem', padding: '1rem', marginBottom: '1.5rem' }}>
                        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: '1rem', flexWrap: 'wrap', gap: '0.5rem' }}>
                            <div style={{ flex: 1, minWidth: 0 }}>
                                <h3 style={{ fontSize: '1.25rem', fontWeight: 700, color: '#111827', marginBottom: '0.5rem', margin: 0 }}>
                                    {violation.storyTitle}
                                </h3>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem', fontSize: '0.875rem', color: '#4b5563', flexWrap: 'wrap' }}>
                                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                        <User style={{ width: 16, height: 16 }} />
                                        <span>Tác giả: {violation.authorName}</span>
                                    </div>
                                    {violation.chapterNumber ? (
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                            <FileText style={{ width: 16, height: 16 }} />
                                            <span>Chương {violation.chapterNumber}: {violation.chapterTitle}</span>
                                        </div>
                                    ) : (
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                            <FileText style={{ width: 16, height: 16 }} />
                                            <span>Toàn bộ truyện</span>
                                        </div>
                                    )}
                                </div>
                            </div>
                            <button
                                type="button"
                                style={{ display: 'inline-flex', alignItems: 'center', gap: '0.5rem', padding: '0.375rem 0.75rem', border: '1px solid #e5e7eb', borderRadius: '0.5rem', background: 'transparent', cursor: 'pointer', fontSize: '0.875rem' }}
                                onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = 'rgba(19, 236, 91, 0.2)'; e.currentTarget.style.color = '#111827'; }}
                                onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = 'transparent'; e.currentTarget.style.color = ''; }}
                            >
                                <ExternalLink style={{ width: 16, height: 16 }} />
                                Xem nội dung
                            </button>
                        </div>
                        <div style={{ display: 'flex', gap: '0.75rem', flexWrap: 'wrap' }}>
                            <span style={{ display: 'inline-block', padding: '0.25rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', fontWeight: 500, border: '1px solid', backgroundColor: '#ffedd5', color: '#c2410c', borderColor: '#fdba74' }}>
                                Mức độ: {getPriorityText(violation.priority)}
                            </span>
                        </div>
                    </div>

                    {/* Content Snapshot */}
                    <div style={{ marginBottom: '1.5rem' }}>
                        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', marginBottom: '0.75rem' }}>
                            <h4 style={{ fontWeight: 600, color: '#111827', margin: 0 }}>Nội dung bị báo cáo</h4>
                            <button
                                type="button"
                                style={{ display: 'inline-flex', alignItems: 'center', gap: '0.5rem', padding: '0.375rem 0.75rem', border: '1px solid #e5e7eb', borderRadius: '0.5rem', background: 'transparent', cursor: 'pointer', fontSize: '0.875rem' }}
                                onClick={() => setShowContentSnapshot(!showContentSnapshot)}
                            >
                                {showContentSnapshot ? (<><EyeOff style={{ width: 16, height: 16 }} /> Ẩn nội dung</>) : (<><Eye style={{ width: 16, height: 16 }} /> Xem nội dung</>)}
                            </button>
                        </div>
                        {showContentSnapshot && (
                            <div style={{ backgroundColor: '#f3f4f6', border: '1px solid #d1d5db', borderRadius: '0.5rem', padding: '1rem' }}>
                                <p style={{ fontSize: '0.875rem', color: '#374151', whiteSpace: 'pre-wrap', margin: 0 }}>
                                    {violation.contentSnapshot}
                                </p>
                            </div>
                        )}
                    </div>

                    {/* All Reports */}
                    <div className="mb-6">
                        <h4 className="font-semibold text-gray-900 mb-3">
                            Danh sách báo cáo ({violation.reportCount})
                        </h4>
                        <div className="space-y-3">
                            {violation.reports.map((report, index) => (
                                <div
                                    key={report.id}
                                    className="bg-white border border-gray-200 rounded-lg p-4"
                                >
                                    <div className="flex items-start justify-between mb-3">
                                        <div className="flex items-center gap-3">
                                            <div className="w-8 h-8 bg-orange-100 rounded-full flex items-center justify-center">
                                                <span className="text-sm font-semibold text-orange-700">
                                                    {index + 1}
                                                </span>
                                            </div>
                                            <div>
                                                <div className="flex items-center gap-2">
                                                    <User className="w-4 h-4 text-gray-500" />
                                                    <span className="font-medium text-gray-900">
                                                        {report.reportedBy}
                                                    </span>
                                                    <span className="text-sm text-gray-500">
                                                        ({report.reportedByEmail})
                                                    </span>
                                                </div>
                                                <div className="flex items-center gap-2 text-sm text-gray-500 mt-1">
                                                    <Clock className="w-3 h-3" />
                                                    <span>{formatDate(report.reportedAt)}</span>
                                                </div>
                                            </div>
                                        </div>
                                        <span style={{ display: 'inline-block', padding: '0.25rem 0.5rem', borderRadius: '9999px', fontSize: '0.75rem', fontWeight: 500, border: '1px solid #fca5a5', backgroundColor: '#fef2f2', color: '#b91c1c' }}>
                                            {report.reason}
                                        </span>
                                    </div>

                                    <div className="space-y-2">
                                        <div>
                                            <label className="text-xs font-semibold text-gray-600">
                                                Mô tả:
                                            </label>
                                            <p className="text-sm text-gray-800 mt-1">
                                                {report.description}
                                            </p>
                                        </div>
                                        {report.evidence && (
                                            <div>
                                                <label className="text-xs font-semibold text-gray-600">
                                                    Bằng chứng:
                                                </label>
                                                <p className="text-sm text-gray-700 mt-1 italic">
                                                    {report.evidence}
                                                </p>
                                            </div>
                                        )}
                                        {(report.evidenceLinks && report.evidenceLinks.length > 0) && (
                                            <div style={{ marginTop: '0.5rem' }}>
                                                <span style={{ fontSize: '0.75rem', fontWeight: 600, color: '#475569' }}>Link bằng chứng:</span>
                                                <ul style={{ margin: '0.25rem 0 0 0', paddingLeft: '1rem', fontSize: '0.875rem', color: '#13ec5b' }}>
                                                    {report.evidenceLinks.map((url, i) => (
                                                        <li key={i}>
                                                            <a href={url} target="_blank" rel="noopener noreferrer" style={{ color: '#13ec5b', textDecoration: 'underline' }}>{url}</a>
                                                        </li>
                                                    ))}
                                                </ul>
                                            </div>
                                        )}
                                        {(report.evidenceImages && report.evidenceImages.length > 0) && (
                                            <div style={{ marginTop: '0.5rem' }}>
                                                <span style={{ fontSize: '0.75rem', fontWeight: 600, color: '#475569' }}>Ảnh bằng chứng:</span>
                                                <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem', marginTop: '0.25rem' }}>
                                                    {report.evidenceImages.map((src, i) => (
                                                        <a key={i} href={src} target="_blank" rel="noopener noreferrer" style={{ display: 'block', fontSize: '0.75rem', color: '#13ec5b', wordBreak: 'break-all' }}>
                                                            <img src={src} alt={`Bằng chứng ${i + 1}`} style={{ maxWidth: 120, maxHeight: 90, objectFit: 'cover', borderRadius: '0.375rem', border: '1px solid #e2e8f0', display: 'block', marginBottom: 2 }} onError={(e) => { e.target.style.display = 'none'; }} />
                                                            {src}
                                                        </a>
                                                    ))}
                                                </div>
                                            </div>
                                        )}
                                        {report.notified && (
                                            <div className="bg-green-50 border border-green-200 rounded p-2 mt-2">
                                                <div className="flex items-center gap-2 text-sm text-green-700">
                                                    <CheckCircle className="w-4 h-4" />
                                                    <span className="font-medium">
                                                        Đã thông báo kết quả - {formatDate(report.notificationSentAt)}
                                                    </span>
                                                </div>
                                                {report.resolutionMessage && (
                                                    <p className="text-sm text-green-800 mt-1 pl-6">
                                                        {report.resolutionMessage}
                                                    </p>
                                                )}
                                            </div>
                                        )}
                                    </div>
                                </div>
                            ))}
                        </div>
                    </div>

                    {violation.reviewedAt && (
                        <div style={{ backgroundColor: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: '0.5rem', padding: '1rem', marginBottom: '1.5rem' }}>
                            <h4 style={{ fontWeight: 600, color: '#0f172a', marginBottom: '0.5rem', margin: 0 }}>
                                Thông tin xem xét
                            </h4>
                            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '0.75rem', fontSize: '0.875rem' }}>
                                <div>
                                    <span style={{ color: '#475569' }}>Người xem xét:</span>{' '}
                                    <span style={{ color: '#0f172a', fontWeight: 500 }}>{violation.reviewedBy}</span>
                                </div>
                                <div>
                                    <span style={{ color: '#475569' }}>Thời gian:</span>{' '}
                                    <span style={{ color: '#0f172a' }}>{formatDate(violation.reviewedAt)}</span>
                                </div>
                            </div>
                            {violation.reviewNote && (
                                <div style={{ marginTop: '0.75rem' }}>
                                    <span style={{ color: '#475569' }}>Ghi chú:</span>
                                    <p style={{ color: '#0f172a', marginTop: '0.25rem', margin: 0 }}>{violation.reviewNote}</p>
                                </div>
                            )}
                        </div>
                    )}

                    {/* Action Section - Only show if not resolved/rejected */}
                    {(violation.status === 'pending' || violation.status === 'in_review') && (
                        <div className="border-t border-gray-200 pt-6">
                            <h3 className="text-lg font-bold text-gray-900 mb-4 flex items-center gap-2">
                                <Shield className="w-5 h-5 text-[#13ec5b]" />
                                Đánh giá và Xử lý Vi phạm
                            </h3>

                            <div className="space-y-4">
                                {/* Severity Assessment */}
                                <div style={{ marginBottom: '1rem' }}>
                                    <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 600, color: '#374151', marginBottom: '0.5rem' }}>
                                        1. Đánh giá mức độ vi phạm *
                                    </label>
                                    <select
                                        value={selectedSeverity}
                                        onChange={(e) => setSelectedSeverity(e.target.value)}
                                        style={{ width: '100%', padding: '0.5rem 0.75rem', border: '1px solid #e5e7eb', borderRadius: '0.5rem', fontSize: '0.875rem', outline: 'none' }}
                                    >
                                        <option value="">Chọn mức độ vi phạm dựa trên quy định hệ thống</option>
                                        <option value="minor">Vi phạm nhẹ - Cảnh báo lần đầu</option>
                                        <option value="moderate">Vi phạm trung bình - Yêu cầu chỉnh sửa</option>
                                        <option value="serious">Vi phạm nghiêm trọng - Ẩn tạm thời</option>
                                        <option value="critical">Vi phạm rất nghiêm trọng - Gỡ bỏ ngay</option>
                                    </select>
                                </div>

                                {/* Action */}
                                <div style={{ marginBottom: '1rem' }}>
                                    <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 600, color: '#374151', marginBottom: '0.5rem' }}>
                                        2. Hành động xử lý *
                                    </label>
                                    <select
                                        value={selectedAction}
                                        onChange={(e) => setSelectedAction(e.target.value)}
                                        style={{ width: '100%', padding: '0.5rem 0.75rem', border: '1px solid #e5e7eb', borderRadius: '0.5rem', fontSize: '0.875rem', outline: 'none' }}
                                    >
                                        <option value="">Chọn hành động xử lý</option>
                                        <option value="send_warning">Gửi cảnh báo tới tác giả</option>
                                        <option value="request_edit">Yêu cầu chỉnh sửa nội dung</option>
                                        <option value="temporary_hide">Ẩn tạm thời nội dung</option>
                                        <option value="remove_content">Gỡ bỏ nội dung vi phạm nghiêm trọng</option>
                                        <option value="reject">Từ chối báo cáo (không đủ căn cứ)</option>
                                    </select>
                                </div>

                                {/* Review Note */}
                                <div style={{ marginBottom: '1rem' }}>
                                    <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 600, color: '#374151', marginBottom: '0.5rem' }}>
                                        3. Ghi chú xem xét (nội bộ) *
                                    </label>
                                    <textarea
                                        value={reviewNote}
                                        onChange={(e) => setReviewNote(e.target.value)}
                                        placeholder="Nhập ghi chú về quyết định xử lý, căn cứ đánh giá..."
                                        style={{ width: '100%', minHeight: 80, padding: '0.5rem 0.75rem', border: '1px solid #e5e7eb', borderRadius: '0.5rem', fontSize: '0.875rem', outline: 'none', resize: 'vertical' }}
                                    />
                                </div>

                                {/* Notification Message */}
                                <div style={{ marginBottom: '1rem' }}>
                                    <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.875rem', fontWeight: 600, color: '#374151', marginBottom: '0.5rem' }}>
                                        <Mail style={{ width: 16, height: 16, color: '#13ec5b' }} />
                                        4. Thông báo gửi cho người báo cáo *
                                    </label>
                                    <textarea
                                        value={notificationMessage}
                                        onChange={(e) => setNotificationMessage(e.target.value)}
                                        placeholder="Nhập thông báo kết quả xử lý sẽ được gửi đến tất cả người báo cáo..."
                                        style={{ width: '100%', minHeight: 100, padding: '0.5rem 0.75rem', border: '1px solid #e5e7eb', borderRadius: '0.5rem', fontSize: '0.875rem', outline: 'none', resize: 'vertical' }}
                                    />
                                    <p style={{ fontSize: '0.75rem', color: '#6b7280', marginTop: '0.5rem', margin: 0 }}>
                                        Thông báo này sẽ được gửi đến {violation.reportCount} người đã báo cáo
                                    </p>
                                </div>
                            </div>
                        </div>
                    )}
                </div>

                {/* Footer */}
                {(violation.status === 'pending' || violation.status === 'in_review') && (
                    <div style={{ borderTop: '1px solid #e5e7eb', padding: '1.5rem', backgroundColor: '#f9fafb' }}>
                        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.75rem' }}>
                            <button
                                type="button"
                                onClick={onClose}
                                style={{ padding: '0.5rem 1rem', border: '1px solid #e5e7eb', borderRadius: '0.5rem', background: 'transparent', cursor: 'pointer', fontSize: '0.875rem' }}
                            >
                                Đóng
                            </button>
                            <button
                                type="button"
                                onClick={handleTakeAction}
                                disabled={isProcessing}
                                style={{
                                    display: 'inline-flex', alignItems: 'center', gap: '0.5rem',
                                    padding: '0.5rem 1rem', backgroundColor: '#13ec5b', color: '#111827',
                                    border: 'none', borderRadius: '0.5rem', fontWeight: 600, cursor: isProcessing ? 'not-allowed' : 'pointer', opacity: isProcessing ? 0.8 : 1,
                                }}
                            >
                                {isProcessing ? (
                                    <>
                                        <span style={{ width: 16, height: 16, border: '2px solid #111827', borderTopColor: 'transparent', borderRadius: '50%' }} />
                                        Đang xử lý...
                                    </>
                                ) : (
                                    <>
                                        <Send style={{ width: 16, height: 16 }} />
                                        Xác nhận xử lý & Gửi thông báo
                                    </>
                                )}
                            </button>
                        </div>
                    </div>
                )}
            </div>
        </div>
    );
};

export default ViolationDetailModal;