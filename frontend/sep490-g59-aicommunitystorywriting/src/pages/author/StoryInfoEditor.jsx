import { useState, useEffect } from 'react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { useAuth } from '../../contexts/AuthContext';
import { StoryInfoForm } from '../../components/author/story-editor/StoryInfoForm';

export function StoryInfoEditor({ story, onSave, onCancel }) {
    const { user } = useAuth();
    const authorName = user?.displayName ?? user?.DisplayName ?? user?.fullName ?? user?.FullName ?? user?.nickname ?? user?.Nickname ?? '';

    const [saving, setSaving] = useState(false);
    const [formData, setFormData] = useState({
        title: '',
        author: authorName,
        status: 'Đang ra',
        ageRating: 'Phù hợp mọi lứa tuổi',
        categories: [],
        note: '',
        cover: '',
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
            const reader = new FileReader();
            reader.onloadend = () => {
                handleInputChange('cover', reader.result);
            };
            reader.readAsDataURL(file);
        }
    };

    const handleSubmit = async () => {
        const payload = {
            ...formData,
            author: formData.author || authorName,
            publishStatus: formData.status,
        };
        try {
            setSaving(true);
            await onSave(payload);
        } finally {
            setSaving(false);
        }
    };

    return (
        <div>
            <Header />
            <div style={{ minHeight: '100vh', backgroundColor: '#f5f5f5', padding: '2rem' }}>
                <div style={{ maxWidth: '1200px', margin: '0 auto' }}>
                    {/* Header */}
                    <div style={{ marginBottom: '2rem' }}>
                        <h2 style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#333333', margin: 0 }}>
                            Chỉnh sửa thông tin truyện
                        </h2>
                    </div>

                    {/* Content - dùng StoryInfoForm giống màn tạo truyện */}
                    <StoryInfoForm
                        formData={formData}
                        onChange={handleInputChange}
                        onImageUpload={handleImageUpload}
                    />

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
                            className="px-6 py-2.5 bg-slate-100 text-slate-900 text-sm font-bold rounded-full hover:bg-slate-200 transition-all"
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
