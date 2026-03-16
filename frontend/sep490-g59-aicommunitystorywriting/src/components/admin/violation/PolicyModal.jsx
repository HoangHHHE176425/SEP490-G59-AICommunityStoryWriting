import React, { useState } from 'react';
import { X, Shield, AlertTriangle, Info, CheckCircle, FileText } from 'lucide-react';

const PolicyModal = ({ onClose }) => {
    const [activeTab, setActiveTab] = useState('process');

    const policies = {
        process: {
            title: 'Quy trình Xử lý',
            icon: FileText,
            color: 'green',
            rules: [
                {
                    title: '1. Tiếp nhận Báo cáo',
                    description:
                        'Compliance Officer nhận báo cáo vi phạm từ người dùng qua hệ thống. Mỗi nội dung có thể có nhiều báo cáo từ nhiều người khác nhau.',
                    severity: 'info',
                    actions: [
                        'Xem danh sách truyện/chương bị báo cáo',
                        'Kiểm tra số lượng báo cáo và mức độ ưu tiên',
                        'Xem chi tiết từng báo cáo (người báo cáo, lý do, bằng chứng)',
                    ],
                },
                {
                    title: '2. Đánh giá Vi phạm',
                    description:
                        'Xem xét nội dung bị báo cáo và đánh giá mức độ vi phạm dựa trên quy định của hệ thống.',
                    severity: 'info',
                    actions: [
                        'Đọc kỹ nội dung bị báo cáo',
                        'So sánh với quy định chính sách nội dung',
                        'Phân loại mức độ: Nhẹ / Trung bình / Nghiêm trọng / Rất nghiêm trọng',
                    ],
                },
                {
                    title: '3. Thực hiện Hành động',
                    description:
                        'Chọn và thực hiện hành động xử lý phù hợp với mức độ vi phạm.',
                    severity: 'info',
                    actions: [
                        'Gửi cảnh báo tới tác giả (vi phạm nhẹ)',
                        'Yêu cầu chỉnh sửa nội dung (vi phạm trung bình)',
                        'Ẩn tạm thời nội dung (vi phạm nghiêm trọng)',
                        'Gỡ bỏ nội dung ngay lập tức (vi phạm rất nghiêm trọng)',
                    ],
                },
                {
                    title: '4. Ghi nhận & Thông báo',
                    description:
                        'Ghi nhận kết quả xử lý và gửi thông báo cho tất cả người báo cáo về kết quả.',
                    severity: 'info',
                    actions: [
                        'Viết ghi chú xem xét nội bộ',
                        'Soạn thông báo kết quả cho người báo cáo',
                        'Hệ thống tự động gửi email/thông báo cho người báo cáo',
                        'Lưu trữ lịch sử xử lý',
                    ],
                },
            ],
        },
        content: {
            title: 'Vi phạm Nội dung',
            icon: Shield,
            color: 'blue',
            rules: [
                {
                    title: 'Nội dung 18+',
                    description:
                        'Không được đăng tải nội dung khiêu dâm, bạo lực tình dục, hoặc không phù hợp với cộng đồng.',
                    severity: 'critical',
                    actions: [
                        'Ẩn/gỡ chương/truyện vi phạm ngay lập tức',
                        'Cảnh cáo nghiêm trọng tác giả',
                        'Khóa tài khoản nếu tái phạm',
                    ],
                },
                {
                    title: 'Ngôn từ xúc phạm',
                    description:
                        'Cấm sử dụng ngôn từ thô tục, xúc phạm, kích động thù địch giữa các nhóm người.',
                    severity: 'high',
                    actions: [
                        'Yêu cầu chỉnh sửa nội dung',
                        'Gửi cảnh báo tới tác giả',
                        'Ẩn tạm thời nếu không tuân thủ',
                    ],
                },
                {
                    title: 'Bạo lực quá mức',
                    description:
                        'Không được mô tả chi tiết các cảnh bạo lực gây ảnh hưởng xấu đến người đọc.',
                    severity: 'medium',
                    actions: [
                        'Yêu cầu cảnh báo nội dung',
                        'Chỉnh sửa mô tả chi tiết',
                        'Ẩn nếu không tuân thủ',
                    ],
                },
            ],
        },
        copyright: {
            title: 'Vi phạm Bản quyền',
            icon: AlertTriangle,
            color: 'orange',
            rules: [
                {
                    title: 'Vi phạm bản quyền',
                    description:
                        'Cấm sao chép, đăng tải nội dung của người khác mà không có sự đồng ý hoặc trích dẫn nguồn.',
                    severity: 'critical',
                    actions: [
                        'Gỡ bỏ truyện ngay lập tức',
                        'Khóa tài khoản tác giả',
                        'Thông báo cho chủ bản quyền',
                        'Có thể xử lý pháp lý',
                    ],
                },
                {
                    title: 'Đạo văn',
                    description:
                        'Không được sao chép ý tưởng, cốt truyện, nhân vật từ tác phẩm khác mà không có sự cho phép.',
                    severity: 'high',
                    actions: [
                        'Yêu cầu chứng minh nguồn gốc',
                        'Gỡ bỏ nếu không hợp lệ',
                        'Cảnh cáo hoặc khóa tài khoản',
                    ],
                },
            ],
        },
        spam: {
            title: 'Spam & Quảng cáo',
            icon: Info,
            color: 'yellow',
            rules: [
                {
                    title: 'Spam quảng cáo',
                    description:
                        'Cấm chèn link quảng cáo, mã giới thiệu, hoặc nội dung không liên quan vào truyện.',
                    severity: 'high',
                    actions: [
                        'Xóa nội dung quảng cáo',
                        'Gửi cảnh báo lần đầu',
                        'Khóa tài khoản nếu tái phạm',
                    ],
                },
                {
                    title: 'Lừa đảo',
                    description:
                        'Cấm mọi hành vi lừa đảo, mạo danh, hoặc yêu cầu chuyển tiền trái phép.',
                    severity: 'critical',
                    actions: [
                        'Khóa tài khoản ngay lập tức',
                        'Báo cáo cơ quan chức năng',
                        'Không được khôi phục tài khoản',
                    ],
                },
            ],
        },
    };

    const getSeverityStyle = (severity) => {
        const map = {
            critical: { backgroundColor: '#fee2e2', color: '#b91c1c', borderColor: '#fca5a5' },
            high: { backgroundColor: '#ffedd5', color: '#c2410c', borderColor: '#fdba74' },
            medium: { backgroundColor: '#fef9c3', color: '#a16207', borderColor: '#fde047' },
            low: { backgroundColor: '#dbeafe', color: '#1d4ed8', borderColor: '#93c5fd' },
            info: { backgroundColor: '#dcfce7', color: '#15803d', borderColor: '#86efac' },
        };
        return map[severity] || { backgroundColor: '#f3f4f6', color: '#374151', borderColor: '#d1d5db' };
    };

    const getSeverityText = (severity) => {
        switch (severity) {
            case 'critical':
                return 'Rất nghiêm trọng';
            case 'high':
                return 'Nghiêm trọng';
            case 'medium':
                return 'Trung bình';
            case 'low':
                return 'Nhẹ';
            case 'info':
                return 'Quy trình';
            default:
                return severity;
        }
    };

    return (
        <div style={{ position: 'fixed', inset: 0, backgroundColor: 'rgba(0,0,0,0.5)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 50, padding: '1rem' }}>
            <div style={{ backgroundColor: '#fff', borderRadius: '0.75rem', boxShadow: '0 25px 50px -12px rgba(0,0,0,0.25)', width: '100%', maxWidth: '64rem', maxHeight: '90vh', overflow: 'hidden', display: 'flex', flexDirection: 'column' }}>
                {/* Header */}
                <div style={{ background: 'linear-gradient(to right, #13ec5b, #0ec252)', padding: '1.5rem' }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                            <div style={{ width: 48, height: 48, backgroundColor: 'rgba(255,255,255,0.2)', borderRadius: '50%', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                <Shield style={{ width: 24, height: 24, color: '#111827' }} />
                            </div>
                            <div>
                                <h2 style={{ fontSize: '1.5rem', fontWeight: 700, color: '#111827', margin: 0 }}>
                                    Chính sách & Quy định
                                </h2>
                                <p style={{ color: '#1f2937', fontSize: '0.875rem', marginTop: '0.25rem', margin: 0 }}>
                                    Hướng dẫn xử lý vi phạm và tiêu chuẩn nội dung
                                </p>
                            </div>
                        </div>
                        <button
                            type="button"
                            onClick={onClose}
                            style={{ padding: '0.5rem', border: 'none', background: 'transparent', color: '#111827', cursor: 'pointer', borderRadius: '0.5rem' }}
                            onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = 'rgba(255,255,255,0.2)'; }}
                            onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = 'transparent'; }}
                        >
                            <X style={{ width: 20, height: 20 }} />
                        </button>
                    </div>
                </div>

                {/* Content */}
                <div style={{ flex: 1, overflowY: 'auto', padding: '1.5rem' }}>
                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '0.5rem', marginBottom: '1.5rem' }}>
                        {Object.entries(policies).map(([key, policy]) => {
                            const Icon = policy.icon;
                            return (
                                <button
                                    key={key}
                                    type="button"
                                    onClick={() => setActiveTab(key)}
                                    style={{
                                        display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '0.5rem 1rem',
                                        border: 'none', borderRadius: '0.5rem', fontSize: '0.875rem', cursor: 'pointer',
                                        backgroundColor: activeTab === key ? 'rgba(19, 236, 91, 0.2)' : 'transparent',
                                        color: activeTab === key ? '#0ec252' : '#6b7280', fontWeight: 500,
                                    }}
                                >
                                    <Icon style={{ width: 16, height: 16 }} />
                                    <span>{policy.title}</span>
                                </button>
                            );
                        })}
                    </div>

                    {Object.entries(policies).map(([key, policy]) => (
                        activeTab === key && (
                            <div key={key} style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
                                {policy.rules.map((rule, index) => (
                                    <div
                                        key={index}
                                        style={{ backgroundColor: '#fff', border: '1px solid #e5e7eb', borderRadius: '0.5rem', padding: '1.5rem' }}
                                    >
                                        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', marginBottom: '1rem' }}>
                                            <div style={{ flex: 1, minWidth: 0 }}>
                                                <h3 style={{ fontSize: '1.125rem', fontWeight: 700, color: '#111827', marginBottom: '0.5rem', margin: 0 }}>
                                                    {rule.title}
                                                </h3>
                                                <p style={{ color: '#4b5563', fontSize: '0.875rem', lineHeight: 1.6, margin: 0 }}>
                                                    {rule.description}
                                                </p>
                                            </div>
                                            <span style={{ padding: '0.25rem 0.75rem', borderRadius: '9999px', fontSize: '0.75rem', fontWeight: 600, border: '1px solid', ...getSeverityStyle(rule.severity) }}>
                                                {getSeverityText(rule.severity)}
                                            </span>
                                        </div>
                                        <div style={{ backgroundColor: '#f9fafb', borderRadius: '0.5rem', padding: '1rem' }}>
                                            <h4 style={{ fontSize: '0.875rem', fontWeight: 600, color: '#374151', marginBottom: '0.75rem', display: 'flex', alignItems: 'center', gap: '0.5rem', margin: 0 }}>
                                                <AlertTriangle style={{ width: 16, height: 16, color: '#f97316' }} />
                                                Hành động xử lý:
                                            </h4>
                                            <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                                                {rule.actions.map((action, actionIndex) => (
                                                    <li key={actionIndex} style={{ display: 'flex', alignItems: 'flex-start', gap: '0.5rem', fontSize: '0.875rem', color: '#374151' }}>
                                                        <span style={{ width: 6, height: 6, backgroundColor: '#13ec5b', borderRadius: '50%', marginTop: 6, flexShrink: 0 }} />
                                                        <span>{action}</span>
                                                    </li>
                                                ))}
                                            </ul>
                                        </div>
                                    </div>
                                ))}
                            </div>
                        )
                    ))}

                    {/* General Guidelines */}
                    <div style={{ marginTop: '2rem', background: 'linear-gradient(to bottom right, #eff6ff, #f0fdf4)', border: '1px solid #bfdbfe', borderRadius: '0.5rem', padding: '1.5rem' }}>
                        <h3 style={{ fontSize: '1.125rem', fontWeight: 700, color: '#111827', marginBottom: '1rem', display: 'flex', alignItems: 'center', gap: '0.5rem', margin: 0 }}>
                            <Info style={{ width: 20, height: 20, color: '#2563eb' }} />
                            Nguyên tắc Xử lý Hậu kiểm
                        </h3>
                        <ul style={{ listStyle: 'none', padding: 0, margin: 0, display: 'flex', flexDirection: 'column', gap: '0.75rem', fontSize: '0.875rem', color: '#374151' }}>
                            {[
                                { n: 1, title: 'Xử lý dựa trên báo cáo:', text: 'Chỉ xử lý các nội dung đã bị báo cáo vi phạm từ cộng đồng. Không kiểm duyệt trước khi đăng.' },
                                { n: 2, title: 'Xem xét kỹ lưỡng:', text: 'Đọc toàn bộ báo cáo và nội dung trước khi đưa ra quyết định. Có thể có báo cáo sai hoặc không đủ căn cứ.' },
                                { n: 3, title: 'Thông báo minh bạch:', text: 'Luôn gửi thông báo kết quả xử lý cho cả tác giả và người báo cáo với lý do rõ ràng.' },
                                { n: 4, title: 'Ghi chú đầy đủ:', text: 'Mọi quyết định phải có ghi chú nội bộ chi tiết làm căn cứ cho các quyết định sau này.' },
                                { n: 5, title: 'Quyền khiếu nại:', text: 'Tác giả có quyền khiếu nại quyết định trong vòng 7 ngày. Compliance Officer cần xem xét lại nếu có khiếu nại hợp lý.' },
                            ].map((item) => (
                                <li key={item.n} style={{ display: 'flex', alignItems: 'flex-start', gap: '0.75rem' }}>
                                    <div style={{ width: 24, height: 24, backgroundColor: 'rgba(19, 236, 91, 0.2)', borderRadius: '50%', display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0 }}>
                                        <span style={{ fontSize: '0.75rem', fontWeight: 700, color: '#111827' }}>{item.n}</span>
                                    </div>
                                    <p style={{ margin: 0 }}><strong>{item.title}</strong> {item.text}</p>
                                </li>
                            ))}
                        </ul>
                    </div>
                </div>

                {/* Footer */}
                <div style={{ borderTop: '1px solid #e5e7eb', padding: '1.5rem', backgroundColor: '#f9fafb' }}>
                    <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                        <button
                            type="button"
                            onClick={onClose}
                            style={{
                                padding: '0.5rem 1rem', backgroundColor: '#13ec5b', color: '#111827',
                                border: 'none', borderRadius: '0.5rem', fontWeight: 600, cursor: 'pointer', fontSize: '0.875rem',
                            }}
                            onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = '#11d351'; }}
                            onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = '#13ec5b'; }}
                        >
                            Đã hiểu
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
};

export default PolicyModal;