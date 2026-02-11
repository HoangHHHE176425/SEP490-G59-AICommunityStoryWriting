import { useState, useEffect } from 'react';
import { StoryInfoForm } from '../../components/author/story-editor/StoryInfoForm';
import { ChapterList } from '../../components/author/story-editor/ChapterList';
import { ChapterEditor } from '../../components/author/story-editor/ChapterEditor';
import { StepIndicator } from '../../components/author/story-editor/StepIndicator';
import { useToast } from '../../components/author/story-editor/Toast';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { useAuth } from '../../contexts/AuthContext';

export function StoryEditor({ story, onSave, onCancel }) {
    const { user } = useAuth();
    const authorName = user?.displayName ?? user?.DisplayName ?? user?.fullName ?? user?.FullName ?? user?.nickname ?? user?.Nickname ?? '';
    const [currentStep, setCurrentStep] = useState(1);
    const [saving, setSaving] = useState(false);
    const { showToast, ToastContainer } = useToast();

    const [formData, setFormData] = useState({
        title: '',
        author: authorName,
        status: 'Đang ra',
        ageRating: 'Phù hợp mọi lứa tuổi',
        categories: [],
        tags: [],
        note: '',
        cover: '',
    });

    const [chapters, setChapters] = useState([
        { id: 1, number: 1, title: '', content: '', accessType: 'public', price: 0 }
    ]);

    const [currentChapterIndex, setCurrentChapterIndex] = useState(0);

    const steps = [
        { number: 1, title: 'Thông tin truyện' },
        { number: 2, title: 'Nội dung' },
        { number: 3, title: 'Đăng truyện' },
        { number: 4, title: 'Hoàn Thành' },
    ];

    useEffect(() => {
        const name = user?.displayName ?? user?.DisplayName ?? user?.fullName ?? user?.FullName ?? user?.nickname ?? user?.Nickname ?? '';
        if (story) {
            const cats = story.categories || [];
            const normalized = Array.isArray(cats)
                ? cats.map((c) => (typeof c === 'object' && c?.id ? { id: c.id, name: c.name || '' } : { id: c, name: String(c) }))
                : [];
            setFormData({
                title: story.title || '',
                author: story.author ?? name,
                status: story.publishStatus || 'Đang ra',
                ageRating: story.ageRating ?? 'Phù hợp mọi lứa tuổi',
                categories: normalized,
                tags: [],
                note: story.summary ?? story.note ?? '',
                cover: story.cover || '',
            });
        } else {
            setFormData(prev => ({ ...prev, author: name }));
        }
    }, [story, user]);

    const minChapters = 1;

    const handleFormDataChange = (field, value) => {
        setFormData({ ...formData, [field]: value });
    };

    const handleImageUpload = (e) => {
        const file = e.target.files?.[0];
        if (file) {
            const reader = new FileReader();
            reader.onloadend = () => {
                handleFormDataChange('cover', reader.result);
                showToast('Ảnh bìa đã được tải lên thành công!', 'success');
            };
            reader.readAsDataURL(file);
        }
    };

    const handleChapterChange = (field, value) => {
        setChapters((prev) => {
            const updated = [...prev];
            updated[currentChapterIndex] = {
                ...updated[currentChapterIndex],
                [field]: value
            };
            return updated;
        });
    };

    const handleAddChapter = () => {
        const newChapter = {
            id: Date.now(),
            number: chapters.length + 1,
            title: '',
            content: '',
            accessType: 'public',
            price: 0
        };
        setChapters([...chapters, newChapter]);
        setCurrentChapterIndex(chapters.length);
        showToast(`Đã thêm chương ${newChapter.number}`, 'success');

        // Scroll to top
        window.scrollTo({ top: 0, behavior: 'smooth' });
    };

    const validateStep1 = () => {
        if (!formData.title.trim()) {
            showToast('Vui lòng nhập tên truyện', 'error');
            return false;
        }
        if (formData.categories.length === 0) {
            showToast('Vui lòng chọn ít nhất 1 thể loại', 'error');
            return false;
        }
        if (!formData.cover) {
            showToast('Vui lòng tải ảnh bìa lên', 'error');
            return false;
        }
        return true;
    };

    const validateStep2 = () => {
        if (chapters.length < minChapters) {
            showToast(`Cần ít nhất ${minChapters} chương để tiếp tục`, 'error');
            return false;
        }
        const invalidChapters = chapters.filter(ch => !ch.title.trim() || !ch.content.trim());
        if (invalidChapters.length > 0) {
            showToast(`Có ${invalidChapters.length} chương chưa hoàn thành`, 'error');
            return false;
        }
        return true;
    };

    const handleNextStep = () => {
        if (currentStep === 1 && !validateStep1()) return;
        if (currentStep === 2 && !validateStep2()) return;

        if (currentStep < 4) {
            setCurrentStep(currentStep + 1);
            window.scrollTo({ top: 0, behavior: 'smooth' });
            showToast('Đã chuyển sang bước tiếp theo', 'success');
        }
    };

    const handlePrevStep = () => {
        if (currentStep > 1) {
            setCurrentStep(currentStep - 1);
            window.scrollTo({ top: 0, behavior: 'smooth' });
        }
    };

    const handleSubmit = async (isDraft) => {
        if (!validateStep1() || !validateStep2()) {
            showToast('Vui lòng hoàn thành tất cả thông tin bắt buộc', 'error');
            return;
        }

        const getCategoryId = (c) => (typeof c === 'object' && c?.id ? c.id : c);
        const categoryIds = (formData.categories || []).map(getCategoryId).filter(Boolean);

        const storyData = {
            ...formData,
            categoryIds,
            isDraft,
            status: isDraft ? 'DRAFT' : 'PENDING_REVIEW',
            storyProgressStatus: formData.status,
            chaptersData: chapters.map((ch, i) => ({
                title: ch.title,
                content: ch.content || '',
                orderIndex: i,
                status: isDraft ? 'DRAFT' : 'PENDING_REVIEW',
                accessType: (ch.accessType === 'paid' ? 'PAID' : 'FREE'),
                coinPrice: ch.accessType === 'paid' ? Number(ch.price) || 0 : 0,
            })),
            chaptersCount: chapters.length,
            lastUpdate: 'Vừa xong',
            publishStatus: isDraft ? 'Lưu tạm' : 'Chờ duyệt',
        };

        setSaving(true);
        try {
            await onSave(storyData);
            showToast(isDraft ? 'Đã lưu bản nháp' : 'Đăng truyện thành công! Đang chờ duyệt.', 'success');
            if (!isDraft) setCurrentStep(4);
        } catch (err) {
            showToast(err?.message || 'Có lỗi xảy ra', 'error');
        } finally {
            setSaving(false);
        }
    };

    return (
        <div>
            <Header />
            <div style={{ minHeight: '100vh', backgroundColor: '#f5f5f5' }}>
                <ToastContainer />

                {/* Header with Stepper */}
                <div style={{ backgroundColor: '#ffffff', borderBottom: '1px solid #e0e0e0', padding: '2rem 0' }}>
                    <div style={{ maxWidth: '1000px', margin: '0 auto', padding: '0 2rem' }}>
                        <StepIndicator currentStep={currentStep} steps={steps} />
                    </div>
                </div>

                {/* Content */}
                <div style={{ maxWidth: '1000px', margin: '0 auto', padding: '2rem' }}>
                    {/* Step 1: Thông tin truyện */}
                    {currentStep === 1 && (
                        <StoryInfoForm
                            formData={formData}
                            onChange={handleFormDataChange}
                            onImageUpload={handleImageUpload}
                        />
                    )}

                    {/* Step 2: Nội dung */}
                    {currentStep === 2 && (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
                            <ChapterList
                                chapters={chapters}
                                currentChapterIndex={currentChapterIndex}
                                onChapterSelect={setCurrentChapterIndex}
                                onAddChapter={handleAddChapter}
                                minChapters={minChapters}
                            />

                            <ChapterEditor
                                chapter={chapters[currentChapterIndex]}
                                onChange={handleChapterChange}
                            />
                        </div>
                    )}

                    {/* Step 3: Review */}
                    {currentStep === 3 && (
                        <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '2rem', border: '1px solid #e0e0e0' }}>
                            <h3 style={{ fontSize: '1.125rem', fontWeight: 'bold', color: '#333333', marginBottom: '1.5rem' }}>
                                Xác nhận thông tin trước khi đăng
                            </h3>

                            <div style={{ display: 'flex', gap: '2rem', marginBottom: '2rem' }}>
                                {formData.cover && (
                                    <img
                                        src={formData.cover}
                                        alt="Cover"
                                        style={{
                                            width: '150px',
                                            height: '200px',
                                            objectFit: 'cover',
                                            borderRadius: '8px',
                                            border: '1px solid #e0e0e0'
                                        }}
                                    />
                                )}
                                <div style={{ flex: 1, display: 'grid', gap: '1rem' }}>
                                    <div style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: '1rem' }}>
                                        <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>Tên truyện:</div>
                                        <div style={{ fontSize: '0.875rem', color: '#333333', fontWeight: 500 }}>{formData.title}</div>
                                    </div>
                                    <div style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: '1rem' }}>
                                        <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>Thể loại:</div>
                                        <div style={{ fontSize: '0.875rem', color: '#333333' }}>
                                            {(formData.categories || []).map((c) => (typeof c === 'object' && c?.name ? c.name : String(c))).join(', ')}
                                        </div>
                                    </div>
                                    <div style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: '1rem' }}>
                                        <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>Số chương:</div>
                                        <div style={{ fontSize: '0.875rem', color: '#333333' }}>{chapters.length} chương</div>
                                    </div>
                                    <div style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: '1rem' }}>
                                        <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>Trạng thái:</div>
                                        <div style={{ fontSize: '0.875rem', color: '#333333' }}>{formData.status}</div>
                                    </div>
                                    <div style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: '1rem' }}>
                                        <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>Giới hạn độ tuổi:</div>
                                        <div style={{ fontSize: '0.875rem', color: '#333333' }}>{formData.ageRating}</div>
                                    </div>
                                </div>
                            </div>

                            {/* Chapter Preview */}
                            <div style={{
                                padding: '1rem',
                                backgroundColor: '#f9fafb',
                                borderRadius: '4px',
                                border: '1px solid #e5e7eb'
                            }}>
                                <h4 style={{ fontSize: '0.875rem', fontWeight: 600, color: '#333333', marginBottom: '0.75rem' }}>
                                    Danh sách chương ({chapters.length})
                                </h4>
                                <div style={{
                                    display: 'grid',
                                    gap: '0.5rem',
                                    maxHeight: '300px',
                                    overflowY: 'auto'
                                }}>
                                    {chapters.map((ch) => (
                                        <div
                                            key={ch.id}
                                            style={{
                                                padding: '0.5rem 0.75rem',
                                                backgroundColor: '#ffffff',
                                                borderRadius: '4px',
                                                display: 'flex',
                                                justifyContent: 'space-between',
                                                alignItems: 'center',
                                                fontSize: '0.875rem'
                                            }}
                                        >
                                            <div>
                                                <span style={{ fontWeight: 600, color: '#333333' }}>Chương {ch.number}:</span>
                                                {' '}
                                                <span style={{ color: '#6b7280' }}>{ch.title || '(Chưa có tiêu đề)'}</span>
                                            </div>
                                            <span style={{ fontSize: '0.75rem', color: '#9ca3af' }}>
                                                {ch.content.length} ký tự
                                            </span>
                                        </div>
                                    ))}
                                </div>
                            </div>
                        </div>
                    )}

                    {/* Step 4: Complete */}
                    {currentStep === 4 && (
                        <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '3rem', border: '1px solid #e0e0e0', textAlign: 'center' }}>
                            <div style={{ fontSize: '4rem', marginBottom: '1rem' }}>🎉</div>
                            <h3 style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#333333', marginBottom: '0.5rem' }}>
                                Đăng truyện thành công!
                            </h3>
                            <p style={{ fontSize: '0.875rem', color: '#6b7280', marginBottom: '2rem' }}>
                                Truyện "{formData.title}" đã được đăng tải với {chapters.length} chương
                            </p>
                            <button
                                onClick={onCancel}
                                style={{
                                    padding: '0.75rem 2rem',
                                    backgroundColor: '#13ec5b',
                                    border: 'none',
                                    borderRadius: '8px',
                                    fontSize: '0.875rem',
                                    fontWeight: 700,
                                    color: '#ffffff',
                                    cursor: 'pointer',
                                    transition: 'all 0.2s'
                                }}
                                onMouseEnter={(e) => {
                                    e.currentTarget.style.backgroundColor = '#10d452';
                                }}
                                onMouseLeave={(e) => {
                                    e.currentTarget.style.backgroundColor = '#13ec5b';
                                }}
                            >
                                Về trang quản lý
                            </button>
                        </div>
                    )}

                    {/* Navigation Buttons */}
                    {currentStep < 4 && (
                        <div style={{
                            display: 'flex',
                            justifyContent: 'space-between',
                            marginTop: '2rem',
                            paddingTop: '2rem',
                            borderTop: '1px solid #e0e0e0'
                        }}>
                            <button
                                onClick={currentStep === 1 ? onCancel : handlePrevStep}
                                style={{
                                    padding: '0.75rem 2rem',
                                    backgroundColor: '#ffffff',
                                    border: '2px solid #13ec5b',
                                    borderRadius: '8px',
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
                                {currentStep === 1 ? 'Hủy' : 'Quay lại'}
                            </button>

                            <div style={{ display: 'flex', gap: '1rem' }}>
                                {currentStep > 1 && currentStep !== 2 && (
                                    <button
                                        disabled={saving}
                                        onClick={() => handleSubmit(true)}
                                        style={{
                                            padding: '0.75rem 2rem',
                                            backgroundColor: '#ffffff',
                                            border: '2px solid #13ec5b',
                                            borderRadius: '8px',
                                            fontSize: '0.875rem',
                                            fontWeight: 700,
                                            color: '#13ec5b',
                                            cursor: saving ? 'not-allowed' : 'pointer',
                                            transition: 'all 0.2s',
                                            opacity: saving ? 0.6 : 1
                                        }}
                                        onMouseEnter={(e) => {
                                            if (!saving) {
                                                e.currentTarget.style.backgroundColor = '#f0fdf4';
                                                e.currentTarget.style.borderColor = '#10d452';
                                            }
                                        }}
                                        onMouseLeave={(e) => {
                                            if (!saving) {
                                                e.currentTarget.style.backgroundColor = '#ffffff';
                                                e.currentTarget.style.borderColor = '#13ec5b';
                                            }
                                        }}
                                    >
                                        {saving ? 'Đang lưu...' : 'Lưu nháp'}
                                    </button>
                                )}

                                {currentStep === 3 ? (
                                    <button
                                        disabled={saving}
                                        onClick={() => handleSubmit(false)}
                                        style={{
                                            padding: '0.75rem 2rem',
                                            backgroundColor: '#13ec5b',
                                            border: 'none',
                                            borderRadius: '8px',
                                            fontSize: '0.875rem',
                                            fontWeight: 700,
                                            color: '#ffffff',
                                            cursor: saving ? 'not-allowed' : 'pointer',
                                            transition: 'all 0.2s',
                                            opacity: saving ? 0.6 : 1
                                        }}
                                        onMouseEnter={(e) => {
                                            if (!saving) {
                                                e.currentTarget.style.backgroundColor = '#10d452';
                                            }
                                        }}
                                        onMouseLeave={(e) => {
                                            if (!saving) {
                                                e.currentTarget.style.backgroundColor = '#13ec5b';
                                            }
                                        }}
                                    >
                                        {saving ? 'Đang xuất bản...' : 'Xuất bản'}
                                    </button>
                                ) : (
                                    <button
                                        onClick={handleNextStep}
                                        style={{
                                            padding: '0.75rem 2rem',
                                            backgroundColor: '#13ec5b',
                                            border: 'none',
                                            borderRadius: '8px',
                                            fontSize: '0.875rem',
                                            fontWeight: 700,
                                            color: '#ffffff',
                                            cursor: 'pointer',
                                            transition: 'all 0.2s'
                                        }}
                                        onMouseEnter={(e) => {
                                            e.currentTarget.style.backgroundColor = '#10d452';
                                        }}
                                        onMouseLeave={(e) => {
                                            e.currentTarget.style.backgroundColor = '#13ec5b';
                                        }}
                                    >
                                        Tiếp theo
                                    </button>
                                )}
                            </div>
                        </div>
                    )}
                </div>
            </div>
            <Footer />
        </div>
    );
}
