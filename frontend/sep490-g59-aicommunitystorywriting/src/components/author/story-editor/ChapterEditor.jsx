import { useState } from 'react';
import { Sparkles, Settings, X } from 'lucide-react';

export function ChapterEditor({ chapter, onChange }) {
    const [showSettings, setShowSettings] = useState(false);
    const [editorSettings, setEditorSettings] = useState({
        fontSize: 16,
        fontFamily: 'Arial, sans-serif',
        backgroundColor: '#ffffff',
    });

    const fontFamilies = [
        { name: 'Arial', value: 'Arial, sans-serif' },
        { name: 'Times New Roman', value: 'Times New Roman, serif' },
        { name: 'Georgia', value: 'Georgia, serif' },
        { name: 'Courier New', value: 'Courier New, monospace' },
    ];

    const backgroundColors = [
        { name: 'Trắng', value: '#ffffff' },
        { name: 'Kem', value: '#fef6e4' },
        { name: 'Xanh nhạt', value: '#e8f5e9' },
        { name: 'Xám nhạt', value: '#f5f5f5' },
    ];

    const handleAISuggestion = (type) => {
        // Mock AI suggestion
        if (type === 'paragraph') {
            alert('AI đang gợi ý đoạn văn... (Tính năng đang phát triển)');
        } else {
            alert('AI đang gợi ý nội dung chương... (Tính năng đang phát triển)');
        }
    };

    return (
        <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '2rem', border: '1px solid #e0e0e0' }}>
            <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
                {/* Chapter Title */}
                <div>
                    <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                        Tên chương
                    </label>
                    <input
                        type="text"
                        value={chapter.title}
                        onChange={(e) => onChange('title', e.target.value)}
                        placeholder="Nhập tên chương"
                        style={{
                            width: '100%',
                            padding: '0.75rem',
                            backgroundColor: '#f9fafb',
                            border: '1px solid #e5e7eb',
                            borderRadius: '4px',
                            fontSize: '0.875rem',
                            outline: 'none'
                        }}
                    />
                </div>

                {/* Toolbar */}
                <div style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'center',
                    padding: '0.75rem 1rem',
                    backgroundColor: '#f9fafb',
                    borderRadius: '4px',
                    border: '1px solid #e5e7eb'
                }}>
                    <div style={{ display: 'flex', gap: '0.5rem' }}>
                        <button
                            onClick={() => handleAISuggestion('paragraph')}
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.375rem',
                                padding: '0.5rem 1rem',
                                backgroundColor: '#ffffff',
                                border: '1px solid #e5e7eb',
                                borderRadius: '4px',
                                fontSize: '0.75rem',
                                fontWeight: 600,
                                color: '#6ee7b7',
                                cursor: 'pointer',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                e.currentTarget.style.backgroundColor = '#f0fdf4';
                            }}
                            onMouseLeave={(e) => {
                                e.currentTarget.style.backgroundColor = '#ffffff';
                            }}
                        >
                            <Sparkles style={{ width: '14px', height: '14px' }} />
                            AI gợi ý đoạn văn
                        </button>

                        <button
                            onClick={() => handleAISuggestion('chapter')}
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.375rem',
                                padding: '0.5rem 1rem',
                                backgroundColor: '#ffffff',
                                border: '1px solid #e5e7eb',
                                borderRadius: '4px',
                                fontSize: '0.75rem',
                                fontWeight: 600,
                                color: '#6ee7b7',
                                cursor: 'pointer',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                e.currentTarget.style.backgroundColor = '#f0fdf4';
                            }}
                            onMouseLeave={(e) => {
                                e.currentTarget.style.backgroundColor = '#ffffff';
                            }}
                        >
                            <Sparkles style={{ width: '14px', height: '14px' }} />
                            AI gợi ý chương
                        </button>
                    </div>

                    <button
                        onClick={() => setShowSettings(!showSettings)}
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '0.375rem',
                            padding: '0.5rem 1rem',
                            backgroundColor: showSettings ? '#6ee7b7' : '#ffffff',
                            border: '1px solid #e5e7eb',
                            borderRadius: '4px',
                            fontSize: '0.75rem',
                            fontWeight: 600,
                            color: showSettings ? '#ffffff' : '#6b7280',
                            cursor: 'pointer',
                            transition: 'all 0.2s'
                        }}
                    >
                        <Settings style={{ width: '14px', height: '14px' }} />
                        Tùy chỉnh
                    </button>
                </div>

                {/* Settings Panel */}
                {showSettings && (
                    <div style={{
                        padding: '1rem',
                        backgroundColor: '#f9fafb',
                        borderRadius: '4px',
                        border: '1px solid #e5e7eb'
                    }}>
                        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
                            <h4 style={{ fontSize: '0.875rem', fontWeight: 600, color: '#333333', margin: 0 }}>
                                Cài đặt hiển thị
                            </h4>
                            <button
                                onClick={() => setShowSettings(false)}
                                style={{
                                    padding: '0.25rem',
                                    backgroundColor: 'transparent',
                                    border: 'none',
                                    cursor: 'pointer',
                                    color: '#6b7280'
                                }}
                            >
                                <X style={{ width: '16px', height: '16px' }} />
                            </button>
                        </div>

                        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1rem' }}>
                            {/* Font Size */}
                            <div>
                                <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                    Cỡ chữ: {editorSettings.fontSize}px
                                </label>
                                <input
                                    type="range"
                                    min="12"
                                    max="24"
                                    value={editorSettings.fontSize}
                                    onChange={(e) => setEditorSettings({ ...editorSettings, fontSize: Number(e.target.value) })}
                                    style={{ width: '100%', cursor: 'pointer' }}
                                />
                            </div>

                            {/* Font Family */}
                            <div>
                                <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                    Font chữ
                                </label>
                                <select
                                    value={editorSettings.fontFamily}
                                    onChange={(e) => setEditorSettings({ ...editorSettings, fontFamily: e.target.value })}
                                    style={{
                                        width: '100%',
                                        padding: '0.375rem 0.5rem',
                                        backgroundColor: '#ffffff',
                                        border: '1px solid #e5e7eb',
                                        borderRadius: '4px',
                                        fontSize: '0.75rem',
                                        outline: 'none',
                                        cursor: 'pointer'
                                    }}
                                >
                                    {fontFamilies.map((font) => (
                                        <option key={font.value} value={font.value}>
                                            {font.name}
                                        </option>
                                    ))}
                                </select>
                            </div>

                            {/* Background Color */}
                            <div>
                                <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                    Màu nền
                                </label>
                                <div style={{ display: 'flex', gap: '0.5rem' }}>
                                    {backgroundColors.map((bg) => (
                                        <button
                                            key={bg.value}
                                            onClick={() => setEditorSettings({ ...editorSettings, backgroundColor: bg.value })}
                                            title={bg.name}
                                            style={{
                                                width: '32px',
                                                height: '32px',
                                                backgroundColor: bg.value,
                                                border: editorSettings.backgroundColor === bg.value ? '2px solid #6ee7b7' : '1px solid #e5e7eb',
                                                borderRadius: '4px',
                                                cursor: 'pointer',
                                                transition: 'all 0.2s'
                                            }}
                                        />
                                    ))}
                                </div>
                            </div>
                        </div>
                    </div>
                )}

                {/* Chapter Content */}
                <div>
                    <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                        Nội dung <span style={{ color: '#ef4444' }}>*</span>
                    </label>
                    <textarea
                        value={chapter.content}
                        onChange={(e) => onChange('content', e.target.value)}
                        placeholder="Nhập nội dung chương của bạn..."
                        rows={20}
                        style={{
                            width: '100%',
                            padding: '1rem',
                            backgroundColor: editorSettings.backgroundColor,
                            border: '1px solid #e5e7eb',
                            borderRadius: '4px',
                            fontSize: `${editorSettings.fontSize}px`,
                            fontFamily: editorSettings.fontFamily,
                            outline: 'none',
                            resize: 'vertical',
                            lineHeight: '1.8'
                        }}
                    />
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: '0.5rem' }}>
                        <p style={{ fontSize: '0.75rem', color: '#9ca3af', margin: 0 }}>
                            Tối thiểu 500 ký tự
                        </p>
                        <p style={{ fontSize: '0.75rem', color: '#9ca3af', margin: 0 }}>
                            {chapter.content.length} ký tự
                        </p>
                    </div>
                </div>
            </div>
        </div>
    );
}
