/* eslint-disable react-hooks/purity */
import { X, Tag, BookOpen, Calendar, ToggleLeft, ToggleRight } from 'lucide-react';

export function CategoryDetailModal({ category, onClose }) {
    const formatDate = (dateString) => {
        if (!dateString) return '-';
        const date = new Date(dateString);
        return date.toLocaleString('vi-VN', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit'
        });
    };

    // Mock statistics
    const stats = {
        totalStories: Math.floor(Math.random() * 1000) + 100,
        activeStories: Math.floor(Math.random() * 800) + 50,
        completedStories: Math.floor(Math.random() * 200) + 20,
        totalViews: Math.floor(Math.random() * 5000000) + 500000,
        totalFollows: Math.floor(Math.random() * 100000) + 10000,
        avgRating: (Math.random() * 1.5 + 3.5).toFixed(1)
    };

    return (
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
                zIndex: 9999,
                padding: '1rem'
            }}
            onClick={onClose}
        >
            <div
                style={{
                    backgroundColor: '#ffffff',
                    borderRadius: '16px',
                    maxWidth: '900px',
                    width: '100%',
                    maxHeight: '90vh',
                    display: 'flex',
                    flexDirection: 'column',
                    overflow: 'hidden',
                    boxShadow: '0 20px 60px rgba(0, 0, 0, 0.3)'
                }}
                onClick={(e) => e.stopPropagation()}
            >
                {/* Header */}
                <div style={{
                    padding: '2rem',
                    borderBottom: '1px solid #e2e8f0',
                    background: 'linear-gradient(135deg, #13ec5b 0%, #10d954 100%)'
                }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'start', marginBottom: '1rem' }}>
                        <div style={{ flex: 1 }}>
                            <div style={{
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: '0.5rem',
                                padding: '0.375rem 0.75rem',
                                backgroundColor: 'rgba(255, 255, 255, 0.2)',
                                borderRadius: '9999px',
                                marginBottom: '1rem'
                            }}>
                                <Tag style={{ width: '14px', height: '14px', color: '#ffffff' }} />
                                <span style={{ fontSize: '0.75rem', fontWeight: 600, color: '#ffffff', textTransform: 'uppercase' }}>
                                    Thể loại
                                </span>
                            </div>

                            <h2 style={{
                                fontSize: '2rem',
                                fontWeight: 700,
                                color: '#ffffff',
                                margin: 0,
                                marginBottom: '0.5rem',
                                textShadow: '0 2px 4px rgba(0, 0, 0, 0.1)'
                            }}>
                                {category.name}
                            </h2>

                            {category.description && (
                                <p style={{
                                    fontSize: '1rem',
                                    color: 'rgba(255, 255, 255, 0.9)',
                                    margin: 0,
                                    lineHeight: 1.6
                                }}>
                                    {category.description}
                                </p>
                            )}
                        </div>

                        <button
                            onClick={onClose}
                            style={{
                                padding: '0.5rem',
                                backgroundColor: 'rgba(255, 255, 255, 0.2)',
                                border: 'none',
                                cursor: 'pointer',
                                borderRadius: '0.5rem',
                                transition: 'background-color 0.2s',
                                flexShrink: 0,
                                marginLeft: '1rem'
                            }}
                            onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'rgba(255, 255, 255, 0.3)'}
                            onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'rgba(255, 255, 255, 0.2)'}
                        >
                            <X style={{ width: '24px', height: '24px', color: '#ffffff' }} />
                        </button>
                    </div>

                    {/* Status Badge */}
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                        {category.is_active ? (
                            <ToggleRight style={{ width: '20px', height: '20px', color: '#ffffff' }} />
                        ) : (
                            <ToggleLeft style={{ width: '20px', height: '20px', color: 'rgba(255, 255, 255, 0.6)' }} />
                        )}
                        <span style={{
                            fontSize: '0.875rem',
                            fontWeight: 600,
                            color: category.is_active ? '#ffffff' : 'rgba(255, 255, 255, 0.8)'
                        }}>
                            {category.is_active ? 'Đang hoạt động' : 'Đã vô hiệu hóa'}
                        </span>
                    </div>
                </div>

                {/* Body */}
                <div style={{ flex: 1, overflowY: 'auto', padding: '2rem' }}>
                    {/* Basic Info */}
                    <div style={{ marginBottom: '2rem' }}>
                        <h3 style={{
                            fontSize: '1.125rem',
                            fontWeight: 700,
                            color: '#1e293b',
                            margin: 0,
                            marginBottom: '1rem'
                        }}>
                            Thông tin cơ bản
                        </h3>

                        <div style={{
                            display: 'grid',
                            gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))',
                            gap: '1rem'
                        }}>
                            <div style={{
                                padding: '1rem',
                                backgroundColor: '#f8fafc',
                                borderRadius: '8px',
                                border: '1px solid #e2e8f0'
                            }}>
                                <div style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.25rem', textTransform: 'uppercase' }}>
                                    ID
                                </div>
                                <div style={{ fontSize: '1rem', fontWeight: 600, color: '#1e293b' }}>
                                    #{category.id}
                                </div>
                            </div>

                            <div style={{
                                padding: '1rem',
                                backgroundColor: '#f8fafc',
                                borderRadius: '8px',
                                border: '1px solid #e2e8f0'
                            }}>
                                <div style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.25rem', textTransform: 'uppercase' }}>
                                    Slug
                                </div>
                                <div style={{ fontSize: '1rem', fontWeight: 600, color: '#1e293b', fontFamily: 'monospace' }}>
                                    {category.slug}
                                </div>
                            </div>

                            <div style={{
                                padding: '1rem',
                                backgroundColor: '#f8fafc',
                                borderRadius: '8px',
                                border: '1px solid #e2e8f0'
                            }}>
                                <div style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.25rem', textTransform: 'uppercase', display: 'flex', alignItems: 'center', gap: '0.25rem' }}>
                                    <Calendar style={{ width: '12px', height: '12px' }} />
                                    Ngày tạo
                                </div>
                                <div style={{ fontSize: '0.875rem', fontWeight: 600, color: '#1e293b' }}>
                                    {formatDate(category.created_at)}
                                </div>
                            </div>

                            {category.story_type && (
                                <div style={{
                                    padding: '1rem',
                                    backgroundColor: '#f8fafc',
                                    borderRadius: '8px',
                                    border: '1px solid #e2e8f0'
                                }}>
                                    <div style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.25rem', textTransform: 'uppercase' }}>
                                        Loại truyện
                                    </div>
                                    <div style={{ fontSize: '1rem', fontWeight: 600, color: '#1e293b' }}>
                                        {category.story_type === 'long' ? 'Truyện dài' : category.story_type === 'short' ? 'Truyện ngắn' : 'Tất cả'}
                                    </div>
                                </div>
                            )}
                        </div>
                    </div>

                    {/* Statistics */}
                    <div style={{ marginBottom: '2rem' }}>
                        <h3 style={{
                            fontSize: '1.125rem',
                            fontWeight: 700,
                            color: '#1e293b',
                            margin: 0,
                            marginBottom: '1rem',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '0.5rem'
                        }}>
                            <BookOpen style={{ width: '20px', height: '20px', color: '#13ec5b' }} />
                            Thống kê
                        </h3>

                        <div style={{
                            display: 'grid',
                            gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))',
                            gap: '1rem'
                        }}>
                            <div style={{
                                padding: '1.5rem',
                                backgroundColor: '#eff6ff',
                                borderRadius: '12px',
                                border: '2px solid #3b82f6',
                                textAlign: 'center'
                            }}>
                                <div style={{ fontSize: '2rem', fontWeight: 700, color: '#1e40af', marginBottom: '0.25rem' }}>
                                    {stats.totalStories}
                                </div>
                                <div style={{ fontSize: '0.875rem', color: '#1e40af', fontWeight: 600 }}>
                                    Tổng truyện
                                </div>
                            </div>

                            <div style={{
                                padding: '1.5rem',
                                backgroundColor: '#d1fae5',
                                borderRadius: '12px',
                                border: '2px solid #10b981',
                                textAlign: 'center'
                            }}>
                                <div style={{ fontSize: '2rem', fontWeight: 700, color: '#065f46', marginBottom: '0.25rem' }}>
                                    {stats.activeStories}
                                </div>
                                <div style={{ fontSize: '0.875rem', color: '#065f46', fontWeight: 600 }}>
                                    Đang ra
                                </div>
                            </div>

                            <div style={{
                                padding: '1.5rem',
                                backgroundColor: '#fef3c7',
                                borderRadius: '12px',
                                border: '2px solid #f59e0b',
                                textAlign: 'center'
                            }}>
                                <div style={{ fontSize: '2rem', fontWeight: 700, color: '#92400e', marginBottom: '0.25rem' }}>
                                    {stats.completedStories}
                                </div>
                                <div style={{ fontSize: '0.875rem', color: '#92400e', fontWeight: 600 }}>
                                    Hoàn thành
                                </div>
                            </div>

                            <div style={{
                                padding: '1.5rem',
                                backgroundColor: '#f3e8ff',
                                borderRadius: '12px',
                                border: '2px solid #a855f7',
                                textAlign: 'center'
                            }}>
                                <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#6b21a8', marginBottom: '0.25rem' }}>
                                    {(stats.totalViews / 1000000).toFixed(1)}M
                                </div>
                                <div style={{ fontSize: '0.875rem', color: '#6b21a8', fontWeight: 600 }}>
                                    Lượt xem
                                </div>
                            </div>

                            <div style={{
                                padding: '1.5rem',
                                backgroundColor: '#fee2e2',
                                borderRadius: '12px',
                                border: '2px solid #ef4444',
                                textAlign: 'center'
                            }}>
                                <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#991b1b', marginBottom: '0.25rem' }}>
                                    {(stats.totalFollows / 1000).toFixed(1)}K
                                </div>
                                <div style={{ fontSize: '0.875rem', color: '#991b1b', fontWeight: 600 }}>
                                    Theo dõi
                                </div>
                            </div>

                            <div style={{
                                padding: '1.5rem',
                                backgroundColor: '#fef9c3',
                                borderRadius: '12px',
                                border: '2px solid #eab308',
                                textAlign: 'center'
                            }}>
                                <div style={{ fontSize: '2rem', fontWeight: 700, color: '#713f12', marginBottom: '0.25rem' }}>
                                    {stats.avgRating}
                                </div>
                                <div style={{ fontSize: '0.875rem', color: '#713f12', fontWeight: 600 }}>
                                    Đánh giá TB
                                </div>
                            </div>
                        </div>
                    </div>

                    {/* Icon Preview */}
                    {category.icon_url && (
                        <div style={{ marginBottom: '2rem' }}>
                            <h3 style={{
                                fontSize: '1.125rem',
                                fontWeight: 700,
                                color: '#1e293b',
                                margin: 0,
                                marginBottom: '1rem'
                            }}>
                                Icon preview
                            </h3>

                            <div style={{
                                padding: '2rem',
                                backgroundColor: '#f8fafc',
                                borderRadius: '12px',
                                border: '1px solid #e2e8f0',
                                textAlign: 'center'
                            }}>
                                <img
                                    src={category.icon_url}
                                    alt={category.name}
                                    style={{
                                        width: '64px',
                                        height: '64px',
                                        objectFit: 'contain',
                                        margin: '0 auto'
                                    }}
                                />
                                <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0, marginTop: '0.5rem' }}>
                                    {category.icon_url}
                                </p>
                            </div>
                        </div>
                    )}

                    {/* Additional Info */}
                    <div style={{
                        padding: '1.5rem',
                        backgroundColor: category.is_active ? '#f0fdf4' : '#fef2f2',
                        borderRadius: '12px',
                        border: `2px solid ${category.is_active ? '#10b981' : '#ef4444'}`
                    }}>
                        <div style={{
                            fontSize: '0.875rem',
                            fontWeight: 600,
                            color: category.is_active ? '#065f46' : '#991b1b',
                            marginBottom: '0.5rem'
                        }}>
                            Trạng thái hiện tại
                        </div>
                        <p style={{
                            fontSize: '0.875rem',
                            color: category.is_active ? '#065f46' : '#991b1b',
                            margin: 0,
                            lineHeight: 1.6
                        }}>
                            {category.is_active
                                ? 'Thể loại này đang được hiển thị trên website và tác giả có thể sử dụng khi tạo truyện mới.'
                                : 'Thể loại này đã bị vô hiệu hóa. Các truyện cũ vẫn giữ nguyên nhưng tác giả không thể chọn thể loại này cho truyện mới.'
                            }
                        </p>
                    </div>
                </div>

                {/* Footer */}
                <div style={{
                    padding: '1.5rem 2rem',
                    borderTop: '1px solid #e2e8f0',
                    backgroundColor: '#f8fafc',
                    display: 'flex',
                    justifyContent: 'flex-end'
                }}>
                    <button
                        onClick={onClose}
                        style={{
                            padding: '0.75rem 2rem',
                            backgroundColor: '#13ec5b',
                            color: '#ffffff',
                            fontSize: '0.875rem',
                            fontWeight: 700,
                            borderRadius: '8px',
                            border: 'none',
                            cursor: 'pointer',
                            transition: 'background-color 0.2s'
                        }}
                        onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#10d954'}
                        onMouseLeave={(e) => e.currentTarget.style.backgroundColor = '#13ec5b'}
                    >
                        Đóng
                    </button>
                </div>
            </div>
        </div>
    );
}
