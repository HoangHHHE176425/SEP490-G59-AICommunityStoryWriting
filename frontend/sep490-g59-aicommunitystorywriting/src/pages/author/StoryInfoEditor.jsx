import { useState, useEffect } from 'react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { useAuth } from '../../contexts/AuthContext';
import { StoryInfoForm } from '../../components/author/story-editor/StoryInfoForm';
import { useToast } from '../../components/author/story-editor/Toast';

const MIN_STORY_SUMMARY_WORDS = 50;
const countWords = (text) => String(text || '').trim().split(/\s+/).filter(Boolean).length;

export function StoryInfoEditor({ story, onSave, onCancel }) {
    const { user } = useAuth();
    const authorName = user?.displayName ?? user?.DisplayName ?? user?.fullName ?? user?.FullName ?? user?.nickname ?? user?.Nickname ?? '';

    const [saving, setSaving] = useState(false);
    const { showToast, ToastContainer } = useToast();
    const [formData, setFormData] = useState({
        title: '',
        author: authorName,
        status: 'Đang ra',
        ageRating: 'Phù hợp mọi lứa tuổi',
        categories: [],
        note: '',
        cover: '',
        coverFile: null,
    });

    useEffect(() => {
        if (story) {
            const cats = story.categories || [];
            const normalized = Array.isArray(cats)
                ? cats.map((c) => (typeof c === 'object' && c?.id ? { id: c.id, name: c.name || '' } : { id: c, name: String(c) }))
                : [];
            const data = {
                title: story.title || '',
                author: story.author ?? authorName,
                status: story.progressStatusDisplay ?? story.publishStatus ?? 'Đang ra',
                ageRating: story.ageRating ?? 'Phù hợp mọi lứa tuổi',
                categories: normalized,
                note: story.summary ?? story.note ?? '',
                cover: story.cover || '',
                coverFile: null,
            };
            queueMicrotask(() => setFormData(data));
        } else {
            queueMicrotask(() => setFormData((prev) => ({ ...prev, author: authorName })));
        }
    }, [story, authorName]);

    const handleInputChange = (field, value) => {
        setFormData((prev) => ({ ...prev, [field]: value }));
    };

    const handleImageUpload = (e) => {
        const file = e.target.files?.[0];
        if (file) {
            const allowedExtensions = ['.jpg', '.jpeg', '.png', '.gif', '.webp'];
            const lowerName = String(file.name || '').toLowerCase();
            const ext = lowerName.slice(lowerName.lastIndexOf('.'));
            if (!allowedExtensions.includes(ext)) {
                showToast(`Ảnh bìa chỉ chấp nhận ${allowedExtensions.join(', ').toUpperCase()}`, 'error');
                e.target.value = '';
                return;
            }
            if (file.size > 5 * 1024 * 1024) {
                showToast('Kích thước ảnh bìa không được vượt quá 5MB', 'error');
                e.target.value = '';
                return;
            }
            const previewUrl = URL.createObjectURL(file);
            setFormData((prev) => {
                if (prev.cover && String(prev.cover).startsWith('blob:')) {
                    URL.revokeObjectURL(prev.cover);
                }
                return { ...prev, cover: previewUrl, coverFile: file };
            });
            e.target.value = '';
        }
    };

    const handleSubmit = async () => {
        const hasPendingReviewChapter = Boolean(story?._hasPendingReviewChapter);
        const selectedProgressStatus = String(formData.status ?? '').trim();
        if (hasPendingReviewChapter && (selectedProgressStatus === 'Tạm dừng' || selectedProgressStatus === 'Hoàn thành')) {
            showToast('Truyện đang có chương chờ duyệt, vui lòng thử lại sau.', 'error');
            return;
        }
        const payload = {
            ...formData,
            author: formData.author || authorName,
            publishStatus: formData.status,
        };
        const summaryWordCount = countWords(payload.note);
        if (summaryWordCount < MIN_STORY_SUMMARY_WORDS) {
            showToast(`Mô tả truyện cần tối thiểu ${MIN_STORY_SUMMARY_WORDS} từ (hiện có ${summaryWordCount} từ).`, 'error');
            return;
        }
        try {
            setSaving(true);
            await onSave(payload);
        } finally {
            setSaving(false);
        }
    };

    const publishStatusUpper = String(story?.status ?? '').trim().toUpperCase();
    const isPublishedStory = publishStatusUpper === 'PUBLISHED';
    const progressStatusUpper = String(story?.storyProgressStatus ?? '').trim().toUpperCase();
    const hasPendingReviewChapter = Boolean(story?._hasPendingReviewChapter);
    const disabledProgressOptions = hasPendingReviewChapter ? ['Tạm dừng', 'Hoàn thành'] : [];
    const allowProgressOptions = progressStatusUpper === 'COMPLETED'
        ? (hasPendingReviewChapter ? ['Đang ra', 'Hoàn thành', 'Tạm dừng'] : ['Hoàn thành'])
        : (hasPendingReviewChapter ? ['Đang ra', 'Hoàn thành', 'Tạm dừng'] : ['Đang ra', 'Hoàn thành', 'Tạm dừng']);

    return (
        <div>
            <Header />
            <ToastContainer />
            <div style={{ minHeight: '100vh', backgroundColor: '#f5f5f5', padding: '2rem' }}>
                <div style={{ maxWidth: '1200px', margin: '0 auto' }}>
                    {/* Header */}
                    <div style={{ marginBottom: '2rem' }}>
                        <button
                            type="button"
                            onClick={onCancel}
                            style={{
                                display: 'inline-flex',
                                alignItems: 'center',
                                gap: '0.4rem',
                                marginBottom: '0.75rem',
                                padding: '0.45rem 0.85rem',
                                backgroundColor: '#ffffff',
                                border: '1px solid #e2e8f0',
                                borderRadius: '9999px',
                                fontSize: '0.8125rem',
                                fontWeight: 600,
                                color: '#475569',
                                cursor: 'pointer'
                            }}
                            onMouseEnter={(e) => {
                                e.currentTarget.style.backgroundColor = '#f8fafc';
                                e.currentTarget.style.borderColor = '#13ec5b';
                                e.currentTarget.style.color = '#13ec5b';
                            }}
                            onMouseLeave={(e) => {
                                e.currentTarget.style.backgroundColor = '#ffffff';
                                e.currentTarget.style.borderColor = '#e2e8f0';
                                e.currentTarget.style.color = '#475569';
                            }}
                        >
                            &larr; Quay lại
                        </button>
                        <h2 style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#333333', margin: 0 }}>
                            Chỉnh sửa thông tin truyện
                        </h2>
                    </div>

                    {/* Content - dùng StoryInfoForm giống màn tạo truyện */}
                    <StoryInfoForm
                        formData={formData}
                        onChange={handleInputChange}
                        onImageUpload={handleImageUpload}
                        readOnlyFields={isPublishedStory}
                        allowProgressStatusWhenReadOnly={isPublishedStory}
                        allowProgressOptions={allowProgressOptions}
                        disabledProgressOptions={disabledProgressOptions}
                    />
                    {hasPendingReviewChapter && (
                        <div style={{
                            marginTop: '1rem',
                            padding: '0.75rem 0.9rem',
                            borderRadius: '8px',
                            border: '1px solid #fcd34d',
                            backgroundColor: '#fffbeb',
                            color: '#92400e',
                            fontSize: '0.8125rem',
                            fontWeight: 600
                        }}>
                            Truyện hiện có ít nhất 1 chương đang chờ duyệt. Vì vậy bạn không thể cập nhật trạng thái tiến độ sang <b>Tạm dừng</b> hoặc <b>Hoàn thành</b>. Vui lòng thử lại sau khi chương được duyệt xong.
                        </div>
                    )}
                    {isPublishedStory && (
                        <div style={{
                            marginTop: '1rem',
                            padding: '0.75rem 0.9rem',
                            borderRadius: '8px',
                            border: '1px solid #bfdbfe',
                            backgroundColor: '#eff6ff',
                            color: '#1d4ed8',
                            fontSize: '0.8125rem',
                            fontWeight: 600
                        }}>
                            Truyện đã xuất bản: chỉ được cập nhật trạng thái tiến độ (Đang ra/Tạm dừng/Hoàn thành).
                        </div>
                    )}

                    {/* Action Buttons */}
                    <div style={{
                        display: 'flex',
                        justifyContent: 'flex-end',
                        gap: '1rem',
                        marginTop: '2rem',
                        paddingTop: '2rem',
                        borderTop: '1px solid #e0e0e0'
                    }}>
                        <button
                            onClick={onCancel}
                            style={{
                                padding: '0.75rem 2rem',
                                backgroundColor: '#ffffff',
                                border: '2px solid #13ec5b',
                                borderRadius: '9999px',
                                fontSize: '0.875rem',
                                fontWeight: 700,
                                color: '#13ec5b',
                                cursor: 'pointer',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                e.currentTarget.style.backgroundColor = '#f0fdf4';
                                e.currentTarget.style.borderColor = '#10d452';
                            }}
                            onMouseLeave={(e) => {
                                e.currentTarget.style.backgroundColor = '#ffffff';
                                e.currentTarget.style.borderColor = '#13ec5b';
                            }}
                        >
                            Hủy
                        </button>
                        <button
                            onClick={handleSubmit}
                            disabled={saving}
                            className="px-6 py-2.5 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all disabled:opacity-60 disabled:cursor-not-allowed"
                        >
                            {saving ? 'Đang lưu...' : 'Lưu thay đổi'}
                        </button>
                    </div>
                </div>
            </div>
            <Footer />
        </div>
    );
}
