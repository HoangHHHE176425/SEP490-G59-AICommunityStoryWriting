import { ChevronDown, Plus } from 'lucide-react';

export function ChapterList({
    chapters,
    currentChapterIndex,
    onChapterSelect,
    onAddChapter,
    minChapters
}) {
    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            {/* Warning if insufficient chapters */}
            {chapters.length < minChapters && (
                <div style={{
                    padding: '1rem',
                    backgroundColor: '#fef2f2',
                    border: '1px solid #fecaca',
                    borderRadius: '4px',
                    fontSize: '0.875rem',
                    color: '#ef4444',
                    display: 'flex',
                    alignItems: 'center',
                    gap: '0.5rem'
                }}>
                    <span>⚠️</span>
                    <span>Cần thêm ít nhất {minChapters} chương để có thể đăng bài (Hiện có: {chapters.length})</span>
                </div>
            )}

            {/* Chapter Selector and Add Button */}
            <div style={{ display: 'flex', gap: '1rem', alignItems: 'flex-start' }}>
                <div style={{ flex: 1 }}>
                    <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                        Chọn chương để chỉnh sửa <span style={{ color: '#ef4444' }}>*</span>
                    </label>
                    <div style={{ position: 'relative' }}>
                        <select
                            value={currentChapterIndex}
                            onChange={(e) => onChapterSelect(Number(e.target.value))}
                            style={{
                                width: '100%',
                                padding: '0.75rem',
                                backgroundColor: '#f9fafb',
                                border: '1px solid #e5e7eb',
                                borderRadius: '4px',
                                fontSize: '0.875rem',
                                outline: 'none',
                                appearance: 'none',
                                cursor: 'pointer'
                            }}
                        >
                            {chapters.map((ch, idx) => (
                                <option key={ch.id} value={idx}>
                                    Chương {ch.number}{ch.title ? ` - ${ch.title}` : ' (Chưa có tiêu đề)'}
                                </option>
                            ))}
                        </select>
                        <ChevronDown style={{
                            position: 'absolute',
                            right: '0.75rem',
                            top: '50%',
                            transform: 'translateY(-50%)',
                            width: '16px',
                            height: '16px',
                            pointerEvents: 'none',
                            color: '#6b7280'
                        }} />
                    </div>
                </div>

                <div style={{ paddingTop: '1.65rem' }}>
                    <button
                        onClick={onAddChapter}
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '0.5rem',
                            padding: '0.75rem 1.5rem',
                            backgroundColor: '#13ec5b',
                            border: 'none',
                            borderRadius: '8px',
                            fontSize: '0.875rem',
                            fontWeight: 700,
                            color: '#ffffff',
                            cursor: 'pointer',
                            transition: 'all 0.2s',
                            whiteSpace: 'nowrap'
                        }}
                        onMouseEnter={(e) => {
                            e.currentTarget.style.backgroundColor = '#10d452';
                            e.currentTarget.style.transform = 'scale(1.05)';
                        }}
                        onMouseLeave={(e) => {
                            e.currentTarget.style.backgroundColor = '#13ec5b';
                            e.currentTarget.style.transform = 'scale(1)';
                        }}
                    >
                        <Plus style={{ width: '16px', height: '16px' }} />
                        Thêm chương mới
                    </button>
                </div>
            </div>

            {/* Chapter List Overview */}
            <div style={{
                backgroundColor: '#f9fafb',
                border: '1px solid #e5e7eb',
                borderRadius: '4px',
                padding: '1rem'
            }}>
                <h4 style={{ fontSize: '0.875rem', fontWeight: 600, color: '#333333', marginBottom: '0.75rem' }}>
                    Tổng quan ({chapters.length} chương)
                </h4>
                <div style={{
                    display: 'grid',
                    gap: '0.5rem',
                    maxHeight: '200px',
                    overflowY: 'auto'
                }}>
                    {chapters.map((ch, idx) => (
                        <div
                            key={ch.id}
                            onClick={() => onChapterSelect(idx)}
                            style={{
                                padding: '0.5rem 0.75rem',
                                backgroundColor: idx === currentChapterIndex ? '#f0fdf4' : '#ffffff',
                                border: `1px solid ${idx === currentChapterIndex ? '#13ec5b' : '#e5e7eb'}`,
                                borderRadius: '4px',
                                cursor: 'pointer',
                                transition: 'all 0.2s',
                                display: 'flex',
                                justifyContent: 'space-between',
                                alignItems: 'center'
                            }}
                            onMouseEnter={(e) => {
                                if (idx !== currentChapterIndex) {
                                    e.currentTarget.style.backgroundColor = '#f9fafb';
                                }
                            }}
                            onMouseLeave={(e) => {
                                if (idx !== currentChapterIndex) {
                                    e.currentTarget.style.backgroundColor = '#ffffff';
                                }
                            }}
                        >
                            <div>
                                <span style={{ fontSize: '0.875rem', fontWeight: 600, color: '#333333' }}>
                                    Chương {ch.number}
                                </span>
                                {ch.title && (
                                    <span style={{ fontSize: '0.875rem', color: '#6b7280', marginLeft: '0.5rem' }}>
                                        - {ch.title}
                                    </span>
                                )}
                            </div>
                            <div style={{ fontSize: '0.75rem', color: ch.content.length > 0 ? '#10b981' : '#ef4444' }}>
                                {ch.content.length > 0 ? `${ch.content.length} ký tự` : 'Chưa có nội dung'}
                            </div>
                        </div>
                    ))}
                </div>
            </div>
        </div>
    );
}
