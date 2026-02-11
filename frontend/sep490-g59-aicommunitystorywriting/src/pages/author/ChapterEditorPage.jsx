import { useState, useEffect } from 'react';
import { Sparkles, Settings, X, Save, ArrowLeft, Lock, Unlock, Coins } from 'lucide-react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';

export function ChapterEditorPage({ story, chapter, onSave, onCancel }) {
    const [chapterData, setChapterData] = useState({
        number: chapter?.number || (story?.chapters || 0) + 1,
        title: chapter?.title || '',
        content: chapter?.content || '',
        status: chapter?.status || 'draft',
        accessType: chapter?.accessType || 'public', // 'public' | 'paid'
        price: chapter?.price || 0,
    });

    const [showSettings, setShowSettings] = useState(false);
    const [editorSettings, setEditorSettings] = useState({
        fontSize: 16,
        fontFamily: 'Arial, sans-serif',
        backgroundColor: '#ffffff',
    });

    const [isSaving, setIsSaving] = useState(false);

    // Reload chapter data when chapter prop changes
    useEffect(() => {
        if (chapter) {
            setChapterData({
                number: chapter.number || (story?.chapters || 0) + 1,
                title: chapter.title || '',
                content: chapter.content || '',
                status: chapter.status || 'draft',
                accessType: chapter.accessType || 'public',
                price: chapter.price || 0,
            });
        } else {
            // Reset to default for new chapter
            setChapterData({
                number: (story?.chapters || 0) + 1,
                title: '',
                content: '',
                status: 'draft',
                accessType: 'public',
                price: 0,
            });
        }
    }, [chapter, story?.chapters]);

    const fontFamilies = [
        { name: 'Arial', value: 'Arial, sans-serif' },
        { name: 'Times New Roman', value: 'Times New Roman, serif' },
        { name: 'Georgia', value: 'Georgia, serif' },
        { name: 'Courier New', value: 'Courier New, monospace' },
        { name: 'Verdana', value: 'Verdana, sans-serif' },
    ];

    const backgroundColors = [
        { name: 'Trắng', value: '#ffffff' },
        { name: 'Kem', value: '#fef6e4' },
        { name: 'Xanh nhạt', value: '#e8f5e9' },
        { name: 'Xám nhạt', value: '#f5f5f5' },
        { name: 'Be', value: '#f5f5dc' },
    ];

    const handleAISuggestion = (type) => {
        if (type === 'paragraph') {
            // Mock AI suggestion for paragraph
            const suggestions = [
                'Ánh nắng buổi sáng len lỏi qua những tán cây, rọi xuống con đường đất nhỏ hẹp. Không khí trong lành, mát mẻ khiến tâm trí anh trở nên thư thái.',
                'Tiếng gió rít qua khe cửa sổ, mang theo làn hương thơm ngát của hoa sen từ ao sen phía sau nhà. Cô ngồi bên bàn, tay cầm cây bút đang lặng lẽ viết nhật ký.',
                'Bầu trời đêm đầy sao, ánh trăng như dát bạc trải khắp mặt hồ. Tiếng ve kêu ran rát, xen lẫn tiếng hát của dân làng xa xa.',
            ];
            const randomSuggestion = suggestions[Math.floor(Math.random() * suggestions.length)];

            // Insert at cursor position or append
            setChapterData(prev => ({
                ...prev,
                content: prev.content + '\n\n' + randomSuggestion
            }));
        } else {
            // Mock AI suggestion for full chapter
            const chapterSuggestions = [
                `Chương ${chapterData.number} - ${chapterData.title || 'Tiếp theo'}\n\nDựa trên nội dung trước, câu chuyện tiếp tục...\n\n[AI sẽ gợi ý nội dung dựa trên ngữ cảnh truyện]`,
            ];
            setChapterData(prev => ({
                ...prev,
                content: prev.content + '\n\n' + chapterSuggestions[0]
            }));
        }
    };

    const handleSave = async (saveStatus) => {
        if (!chapterData.title.trim()) {
            alert('Vui lòng nhập tên chương');
            return;
        }
        if (!chapterData.content.trim()) {
            alert('Vui lòng nhập nội dung chương');
            return;
        }
        if (chapterData.content.length < 500) {
            alert('Nội dung chương cần ít nhất 500 ký tự');
            return;
        }
        if (chapterData.accessType === 'paid' && (!chapterData.price || chapterData.price <= 0)) {
            alert('Vui lòng nhập giá cho chương trả phí');
            return;
        }

        setIsSaving(true);
        try {
            await onSave({
                ...chapterData,
                status: saveStatus,
                updatedAt: new Date().toLocaleString('vi-VN'),
            });
        } catch (error) {
            // Error handling is done in parent component
            console.error('Error saving chapter:', error);
        } finally {
            setIsSaving(false);
        }
    };

    return (
        <div>
            <Header />
            <div style={{ minHeight: '100vh', backgroundColor: '#f5f5f5' }}>
                {/* Header */}
                <div style={{
                    backgroundColor: '#ffffff',
                    borderBottom: '1px solid #e0e0e0',
                    position: 'sticky',
                    top: 0,
                    zIndex: 100
                }}>
                    <div style={{ maxWidth: '1200px', margin: '0 auto', padding: '1rem 2rem' }}>
                        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                            {/* Left: Back button and title */}
                            <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                                <button
                                    onClick={onCancel}
                                    className="flex items-center gap-2 px-4 py-2 bg-slate-100 text-slate-900 text-sm font-semibold rounded-full hover:bg-slate-200 transition-all"
                                >
                                    <ArrowLeft style={{ width: '16px', height: '16px' }} />
                                    Quay lại
                                </button>
                                <div>
                                    <h2 style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#333333', margin: 0 }}>
                                        {chapter ? 'Chỉnh sửa chương' : 'Thêm chương mới'}
                                    </h2>
                                    <p style={{ fontSize: '0.875rem', color: '#6b7280', margin: '0.25rem 0 0 0' }}>
                                        {story?.title}
                                    </p>
                                </div>
                            </div>

                            {/* Right: Save buttons */}
                            <div style={{ display: 'flex', gap: '0.75rem' }}>
                                <button
                                    onClick={() => handleSave('draft')}
                                    disabled={isSaving}
                                    className="flex items-center gap-2 px-6 py-2.5 bg-primary/10 text-primary text-sm font-bold rounded-full hover:bg-primary/20 transition-all disabled:opacity-60 disabled:cursor-not-allowed"
                                >
                                    <Save style={{ width: '16px', height: '16px' }} />
                                    Lưu nháp
                                </button>
                                <button
                                    onClick={() => handleSave('published')}
                                    disabled={isSaving}
                                    className="flex items-center gap-2 px-6 py-2.5 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all disabled:opacity-60 disabled:cursor-not-allowed"
                                >
                                    <Save style={{ width: '16px', height: '16px' }} />
                                    {isSaving ? 'Đang lưu...' : 'Xuất bản'}
                                </button>
                            </div>
                        </div>
                    </div>
                </div>

                {/* Content */}
                <div style={{ maxWidth: '1200px', margin: '0 auto', padding: '2rem' }}>
                    <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '2rem', border: '1px solid #e0e0e0' }}>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
                            {/* Chapter Number and Title */}
                            <div style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: '1rem' }}>
                                <div>
                                    <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                        Số chương <span style={{ color: '#ef4444' }}>*</span>
                                    </label>
                                    <input
                                        type="number"
                                        value={chapterData.number}
                                        onChange={(e) => setChapterData({ ...chapterData, number: Number(e.target.value) })}
                                        min="1"
                                        style={{
                                            width: '100%',
                                            padding: '0.75rem',
                                            backgroundColor: '#f9fafb',
                                            border: '1px solid #e5e7eb',
                                            borderRadius: '8px',
                                            fontSize: '0.875rem',
                                            outline: 'none'
                                        }}
                                    />
                                </div>
                                <div>
                                    <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                        Tên chương <span style={{ color: '#ef4444' }}>*</span>
                                    </label>
                                    <input
                                        type="text"
                                        value={chapterData.title}
                                        onChange={(e) => setChapterData({ ...chapterData, title: e.target.value })}
                                        placeholder="Nhập tên chương"
                                        style={{
                                            width: '100%',
                                            padding: '0.75rem',
                                            backgroundColor: '#f9fafb',
                                            border: '1px solid #e5e7eb',
                                            borderRadius: '8px',
                                            fontSize: '0.875rem',
                                            outline: 'none'
                                        }}
                                    />
                                </div>
                            </div>

                            {/* Access Type and Price */}
                            <div>
                                <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.75rem' }}>
                                    Chế độ sáng tác <span style={{ color: '#ef4444' }}>*</span>
                                </label>

                                <div style={{ display: 'grid', gridTemplateColumns: chapterData.accessType === 'paid' ? '1fr 1fr 200px' : '1fr 1fr', gap: '1rem' }}>
                                    {/* Public Option */}
                                    <button
                                        type="button"
                                        onClick={() => setChapterData({ ...chapterData, accessType: 'public', price: 0 })}
                                        className={`flex items-center gap-3 p-4 border-2 rounded-xl transition-all ${chapterData.accessType === 'public'
                                            ? 'border-primary bg-primary/5'
                                            : 'border-slate-200 hover:border-slate-300'
                                            }`}
                                    >
                                        <div className={`flex items-center justify-center w-10 h-10 rounded-full ${chapterData.accessType === 'public' ? 'bg-primary text-white' : 'bg-slate-100 text-slate-600'
                                            }`}>
                                            <Unlock style={{ width: '20px', height: '20px' }} />
                                        </div>
                                        <div style={{ textAlign: 'left', flex: 1 }}>
                                            <div style={{ fontSize: '0.875rem', fontWeight: 'bold', color: chapterData.accessType === 'public' ? '#13ec5b' : '#333333' }}>
                                                Miễn phí (Public)
                                            </div>
                                            <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                                                Người đọc có thể đọc miễn phí
                                            </div>
                                        </div>
                                        {chapterData.accessType === 'public' && (
                                            <div style={{ width: '20px', height: '20px', borderRadius: '50%', backgroundColor: '#13ec5b', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                                <div style={{ width: '8px', height: '8px', borderRadius: '50%', backgroundColor: '#ffffff' }} />
                                            </div>
                                        )}
                                    </button>

                                    {/* Paid Option */}
                                    <button
                                        type="button"
                                        onClick={() => setChapterData({ ...chapterData, accessType: 'paid' })}
                                        className={`flex items-center gap-3 p-4 border-2 rounded-xl transition-all ${chapterData.accessType === 'paid'
                                            ? 'border-amber-500 bg-amber-50'
                                            : 'border-slate-200 hover:border-slate-300'
                                            }`}
                                    >
                                        <div className={`flex items-center justify-center w-10 h-10 rounded-full ${chapterData.accessType === 'paid' ? 'bg-amber-500 text-white' : 'bg-slate-100 text-slate-600'
                                            }`}>
                                            <Lock style={{ width: '20px', height: '20px' }} />
                                        </div>
                                        <div style={{ textAlign: 'left', flex: 1 }}>
                                            <div style={{ fontSize: '0.875rem', fontWeight: 'bold', color: chapterData.accessType === 'paid' ? '#f59e0b' : '#333333' }}>
                                                Trả phí (Paid)
                                            </div>
                                            <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                                                Yêu cầu người đọc trả phí
                                            </div>
                                        </div>
                                        {chapterData.accessType === 'paid' && (
                                            <div style={{ width: '20px', height: '20px', borderRadius: '50%', backgroundColor: '#f59e0b', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                                <div style={{ width: '8px', height: '8px', borderRadius: '50%', backgroundColor: '#ffffff' }} />
                                            </div>
                                        )}
                                    </button>

                                    {/* Price Input (show only when paid) */}
                                    {chapterData.accessType === 'paid' && (
                                        <div>
                                            <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                                Giá <span style={{ color: '#ef4444' }}>*</span>
                                            </label>
                                            <div style={{ position: 'relative' }}>
                                                <input
                                                    type="number"
                                                    value={chapterData.price}
                                                    onChange={(e) => setChapterData({ ...chapterData, price: Number(e.target.value) })}
                                                    min="1"
                                                    placeholder="0"
                                                    style={{
                                                        width: '100%',
                                                        padding: '0.75rem 0.75rem 0.75rem 2.5rem',
                                                        backgroundColor: '#fffbeb',
                                                        border: '1px solid #fbbf24',
                                                        borderRadius: '8px',
                                                        fontSize: '0.875rem',
                                                        fontWeight: 'bold',
                                                        color: '#92400e',
                                                        outline: 'none'
                                                    }}
                                                />
                                                <Coins style={{
                                                    position: 'absolute',
                                                    left: '0.75rem',
                                                    top: '50%',
                                                    transform: 'translateY(-50%)',
                                                    width: '16px',
                                                    height: '16px',
                                                    color: '#f59e0b'
                                                }} />
                                            </div>
                                            <p style={{ fontSize: '0.625rem', color: '#92400e', marginTop: '0.25rem' }}>
                                                Đơn vị: Xu
                                            </p>
                                        </div>
                                    )}
                                </div>

                                {/* Info Box */}
                                {chapterData.accessType === 'paid' && (
                                    <div style={{
                                        marginTop: '1rem',
                                        padding: '0.75rem 1rem',
                                        backgroundColor: '#fffbeb',
                                        border: '1px solid #fcd34d',
                                        borderRadius: '8px',
                                        fontSize: '0.75rem',
                                        color: '#92400e',
                                        display: 'flex',
                                        alignItems: 'flex-start',
                                        gap: '0.5rem'
                                    }}>
                                        <span style={{ fontSize: '1rem' }}>💰</span>
                                        <div>
                                            <strong>Lưu ý về chương trả phí:</strong>
                                            <ul style={{ margin: '0.25rem 0 0 1rem', paddingLeft: 0 }}>
                                                <li>Người đọc cần có đủ xu để mở khóa chương</li>
                                                <li>Sau khi mua, chương sẽ được lưu vĩnh viễn trong tài khoản</li>
                                                <li>Bạn sẽ nhận 70% số xu, nền tảng giữ lại 30%</li>
                                            </ul>
                                        </div>
                                    </div>
                                )}
                            </div>

                            {/* Toolbar */}
                            <div style={{
                                display: 'flex',
                                justifyContent: 'space-between',
                                alignItems: 'center',
                                padding: '0.75rem 1rem',
                                backgroundColor: '#f9fafb',
                                borderRadius: '8px',
                                border: '1px solid #e5e7eb'
                            }}>
                                <div style={{ display: 'flex', gap: '0.5rem' }}>
                                    <button
                                        type="button"
                                        onClick={() => handleAISuggestion('paragraph')}
                                        className="flex items-center gap-2 px-4 py-2 bg-primary/10 text-primary text-sm font-bold rounded-full hover:bg-primary/20 transition-all"
                                    >
                                        <Sparkles style={{ width: '14px', height: '14px' }} />
                                        AI gợi ý đoạn văn
                                    </button>

                                    <button
                                        type="button"
                                        onClick={() => handleAISuggestion('chapter')}
                                        className="flex items-center gap-2 px-4 py-2 bg-primary/10 text-primary text-sm font-bold rounded-full hover:bg-primary/20 transition-all"
                                    >
                                        <Sparkles style={{ width: '14px', height: '14px' }} />
                                        AI gợi ý chương
                                    </button>
                                </div>

                                <button
                                    type="button"
                                    onClick={() => setShowSettings(!showSettings)}
                                    className={`flex items-center gap-2 px-4 py-2 text-sm font-bold rounded-full transition-all ${showSettings
                                        ? 'bg-primary text-white'
                                        : 'bg-slate-100 text-slate-700 hover:bg-slate-200'
                                        }`}
                                >
                                    <Settings style={{ width: '14px', height: '14px' }} />
                                    Tùy chỉnh hiển thị
                                </button>
                            </div>

                            {/* Settings Panel */}
                            {showSettings && (
                                <div style={{
                                    padding: '1.5rem',
                                    backgroundColor: '#f9fafb',
                                    borderRadius: '8px',
                                    border: '1px solid #e5e7eb'
                                }}>
                                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
                                        <h4 style={{ fontSize: '0.875rem', fontWeight: 600, color: '#333333', margin: 0 }}>
                                            Cài đặt hiển thị
                                        </h4>
                                        <button
                                            type="button"
                                            onClick={() => setShowSettings(false)}
                                            className="p-1 hover:bg-slate-200 rounded-full transition-colors"
                                        >
                                            <X style={{ width: '16px', height: '16px', color: '#6b7280' }} />
                                        </button>
                                    </div>

                                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1.5rem' }}>
                                        {/* Font Size */}
                                        <div>
                                            <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                                Cỡ chữ: {editorSettings.fontSize}px
                                            </label>
                                            <input
                                                type="range"
                                                min="12"
                                                max="28"
                                                value={editorSettings.fontSize}
                                                onChange={(e) => setEditorSettings({ ...editorSettings, fontSize: Number(e.target.value) })}
                                                style={{ width: '100%', cursor: 'pointer', accentColor: '#13ec5b' }}
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
                                                    padding: '0.5rem',
                                                    backgroundColor: '#ffffff',
                                                    border: '1px solid #e5e7eb',
                                                    borderRadius: '8px',
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
                                            <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                                                {backgroundColors.map((bg) => (
                                                    <button
                                                        key={bg.value}
                                                        type="button"
                                                        onClick={() => setEditorSettings({ ...editorSettings, backgroundColor: bg.value })}
                                                        title={bg.name}
                                                        style={{
                                                            width: '40px',
                                                            height: '40px',
                                                            backgroundColor: bg.value,
                                                            border: editorSettings.backgroundColor === bg.value ? '3px solid #13ec5b' : '1px solid #e5e7eb',
                                                            borderRadius: '8px',
                                                            cursor: 'pointer',
                                                            transition: 'all 0.2s'
                                                        }}
                                                        onMouseEnter={(e) => {
                                                            e.currentTarget.style.transform = 'scale(1.1)';
                                                        }}
                                                        onMouseLeave={(e) => {
                                                            e.currentTarget.style.transform = 'scale(1)';
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
                                    Nội dung chương <span style={{ color: '#ef4444' }}>*</span>
                                </label>
                                <textarea
                                    value={chapterData.content}
                                    onChange={(e) => setChapterData({ ...chapterData, content: e.target.value })}
                                    placeholder="Nhập nội dung chương của bạn...&#10;&#10;Bạn có thể sử dụng AI để gợi ý nội dung bằng cách click vào các nút phía trên."
                                    rows={25}
                                    style={{
                                        width: '100%',
                                        padding: '1rem',
                                        backgroundColor: editorSettings.backgroundColor,
                                        border: '1px solid #e5e7eb',
                                        borderRadius: '8px',
                                        fontSize: `${editorSettings.fontSize}px`,
                                        fontFamily: editorSettings.fontFamily,
                                        outline: 'none',
                                        resize: 'vertical',
                                        lineHeight: '1.8'
                                    }}
                                />
                                <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: '0.5rem' }}>
                                    <p style={{ fontSize: '0.75rem', color: chapterData.content.length < 500 ? '#ef4444' : '#9ca3af', margin: 0 }}>
                                        Tối thiểu 500 ký tự
                                    </p>
                                    <p style={{ fontSize: '0.75rem', color: '#9ca3af', margin: 0 }}>
                                        {chapterData.content.length.toLocaleString()} ký tự
                                    </p>
                                </div>
                            </div>

                            {/* Additional Info */}
                            <div style={{
                                padding: '1rem',
                                backgroundColor: '#dbeafe',
                                border: '1px solid #93c5fd',
                                borderRadius: '8px',
                                fontSize: '0.875rem',
                                color: '#1e40af'
                            }}>
                                <strong>💡 Mẹo viết chương hay:</strong>
                                <ul style={{ margin: '0.5rem 0 0 1.5rem', paddingLeft: 0 }}>
                                    <li>Bắt đầu bằng một hook hấp dẫn để thu hút người đọc</li>
                                    <li>Sử dụng AI để gợi ý khi gặp khó khăn</li>
                                    <li>Chia nhỏ đoạn văn để dễ đọc hơn</li>
                                    <li>Kết thúc chương với một twist hoặc cliffhanger</li>
                                </ul>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
            <Footer />
        </div>
    );
}
