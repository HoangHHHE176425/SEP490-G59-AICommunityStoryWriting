import { useState } from 'react';
import { X, Plus, ChevronDown } from 'lucide-react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';

export function StoryInfoEditor({ story, onSave, onCancel }) {
    const [formData, setFormData] = useState({
        title: story?.title || '',
        author: 'Quyền Đình',
        storyType: story?.storyType || 'long',
        status: story?.publishStatus || 'Đang ra',
        ageRating: 'Phù hợp mọi lứa tuổi',
        categories: story?.categories || [],
        tags: [],
        note: '',
        cover: story?.cover || '',
    });

    const [tagInput, setTagInput] = useState('');

    const statusOptions = ['Đang ra', 'Hoàn thành', 'Tạm dừng'];
    const ageRatings = ['Phù hợp mọi lứa tuổi', 'Từ 13 tuổi', 'Từ 16 tuổi', 'Từ 18 tuổi'];

    const storyTypes = [
        { value: 'long', label: 'Truyện dài' },
        { value: 'short', label: 'Truyện ngắn' }
    ];

    const categoriesByType = {
        long: [
            'Tiên Hiệp', 'Kiếm Hiệp', 'Huyền Huyễn', 'Võng Du',
            'Khoa Huyễn', 'Hệ Thống', 'Dị Giới', 'Dị Năng',
            'Quân Sự', 'Lịch Sử', 'Cạnh Kỹ', 'Đô Thị'
        ],
        short: [
            'Ngôn Tình', 'Đam Mỹ', 'Đồng Nhân', 'Nguyên Sang',
            'Kinh Dị', 'Trinh Thám', 'Học Đường', 'Gia Đấu'
        ]
    };

    const availableCategories = categoriesByType[formData.storyType] || [];

    const handleInputChange = (field, value) => {
        setFormData({ ...formData, [field]: value });
    };

    const handleCategoryToggle = (category) => {
        const newCategories = formData.categories.includes(category)
            ? formData.categories.filter(c => c !== category)
            : [...formData.categories, category];
        handleInputChange('categories', newCategories);
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

    const handleStoryTypeChange = (newType) => {
        handleInputChange('storyType', newType);
        handleInputChange('categories', []);
    };

    const handleAddTag = () => {
        if (tagInput.trim() && !formData.tags.includes(tagInput.trim())) {
            handleInputChange('tags', [...formData.tags, tagInput.trim()]);
            setTagInput('');
        }
    };

    const handleRemoveTag = (tag) => {
        handleInputChange('tags', formData.tags.filter(t => t !== tag));
    };

    const handleSubmit = () => {
        onSave(formData);
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

                    {/* Content */}
                    <div style={{ display: 'grid', gridTemplateColumns: '300px 1fr', gap: '2rem' }}>
                        {/* Left: Cover Upload */}
                        <div>
                            <div style={{ position: 'sticky', top: '2rem' }}>
                                {formData.cover ? (
                                    <div style={{ position: 'relative' }}>
                                        <img
                                            src={formData.cover}
                                            alt="Cover"
                                            style={{
                                                width: '100%',
                                                aspectRatio: '2/3',
                                                objectFit: 'cover',
                                                borderRadius: '8px',
                                                border: '1px solid #e0e0e0'
                                            }}
                                        />
                                        <button
                                            onClick={() => handleInputChange('cover', '')}
                                            style={{
                                                position: 'absolute',
                                                top: '0.5rem',
                                                right: '0.5rem',
                                                width: '32px',
                                                height: '32px',
                                                borderRadius: '50%',
                                                backgroundColor: 'rgba(0, 0, 0, 0.6)',
                                                border: 'none',
                                                color: '#ffffff',
                                                cursor: 'pointer',
                                                display: 'flex',
                                                alignItems: 'center',
                                                justifyContent: 'center'
                                            }}
                                        >
                                            <X style={{ width: '16px', height: '16px' }} />
                                        </button>
                                    </div>
                                ) : (
                                    <label
                                        htmlFor="cover-upload"
                                        style={{
                                            display: 'flex',
                                            flexDirection: 'column',
                                            alignItems: 'center',
                                            justifyContent: 'center',
                                            aspectRatio: '2/3',
                                            border: '2px dashed #d1d5db',
                                            borderRadius: '8px',
                                            cursor: 'pointer',
                                            backgroundColor: '#fafafa',
                                            transition: 'all 0.2s'
                                        }}
                                        onMouseEnter={(e) => {
                                            e.currentTarget.style.borderColor = '#13ec5b';
                                        }}
                                        onMouseLeave={(e) => {
                                            e.currentTarget.style.borderColor = '#d1d5db';
                                        }}
                                    >
                                        <Plus style={{ width: '40px', height: '40px', color: '#13ec5b', marginBottom: '0.5rem' }} />
                                        <span style={{ fontSize: '0.875rem', fontWeight: 600, color: '#13ec5b' }}>
                                            CHỌN ẢNH BÌA
                                        </span>
                                    </label>
                                )}
                                <input
                                    type="file"
                                    accept="image/*"
                                    onChange={handleImageUpload}
                                    style={{ display: 'none' }}
                                    id="cover-upload"
                                />
                                <p style={{ fontSize: '0.75rem', color: '#6b7280', textAlign: 'center', marginTop: '0.75rem' }}>
                                    Kích thước yêu cầu: 800x1170
                                </p>
                            </div>
                        </div>

                        {/* Right: Form */}
                        <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '2rem', border: '1px solid #e0e0e0' }}>
                            <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
                                {/* Title */}
                                <div>
                                    <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                        Tên truyện <span style={{ color: '#ef4444' }}>*</span>
                                    </label>
                                    <input
                                        type="text"
                                        value={formData.title}
                                        onChange={(e) => handleInputChange('title', e.target.value)}
                                        placeholder="Nhập tên truyện"
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

                                {/* Author */}
                                <div>
                                    <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                        Tác giả
                                    </label>
                                    <div style={{
                                        display: 'flex',
                                        alignItems: 'center',
                                        gap: '0.5rem',
                                        padding: '0.75rem',
                                        backgroundColor: '#f9fafb',
                                        border: '1px solid #e5e7eb',
                                        borderRadius: '4px'
                                    }}>
                                        <span style={{ fontSize: '0.875rem', color: '#333333' }}>{formData.author}</span>
                                    </div>
                                </div>

                                {/* Status and Story Type */}
                                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1rem' }}>
                                    <div>
                                        <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                            Trạng thái
                                        </label>
                                        <div style={{ position: 'relative' }}>
                                            <select
                                                value={formData.status}
                                                onChange={(e) => handleInputChange('status', e.target.value)}
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
                                                {statusOptions.map(opt => (
                                                    <option key={opt} value={opt}>{opt}</option>
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

                                    <div>
                                        <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                            Thể loại <span style={{ color: '#ef4444' }}>*</span>
                                        </label>
                                        <div style={{ position: 'relative' }}>
                                            <select
                                                value={formData.storyType}
                                                onChange={(e) => handleStoryTypeChange(e.target.value)}
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
                                                {storyTypes.map(type => (
                                                    <option key={type.value} value={type.value}>{type.label}</option>
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
                                </div>

                                {/* Age Rating */}
                                <div>
                                    <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                        Giới hạn độ tuổi
                                    </label>
                                    <div style={{ position: 'relative' }}>
                                        <select
                                            value={formData.ageRating}
                                            onChange={(e) => handleInputChange('ageRating', e.target.value)}
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
                                            {ageRatings.map(opt => (
                                                <option key={opt} value={opt}>{opt}</option>
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

                                {/* Categories */}
                                <div>
                                    <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                        Thể loại chi tiết (Chọn tối đa 3)
                                    </label>
                                    <div style={{
                                        padding: '0.75rem',
                                        backgroundColor: '#e6fff0',
                                        border: '1px solid #13ec5b',
                                        borderRadius: '4px',
                                        fontSize: '0.75rem',
                                        color: '#065f46',
                                        marginBottom: '0.75rem'
                                    }}>
                                        💡 Đang hiển thị thể loại cho <strong>{formData.storyType === 'long' ? 'Truyện dài' : 'Truyện ngắn'}</strong>
                                    </div>
                                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem' }}>
                                        {availableCategories.map((cat) => (
                                            <button
                                                key={cat}
                                                type="button"
                                                onClick={() => {
                                                    if (formData.categories.length < 3 || formData.categories.includes(cat)) {
                                                        handleCategoryToggle(cat);
                                                    }
                                                }}
                                                disabled={formData.categories.length >= 3 && !formData.categories.includes(cat)}
                                                style={{
                                                    padding: '0.5rem 1rem',
                                                    backgroundColor: formData.categories.includes(cat) ? '#13ec5b' : '#ffffff',
                                                    border: '1px solid #e5e7eb',
                                                    borderRadius: '4px',
                                                    fontSize: '0.875rem',
                                                    color: formData.categories.includes(cat) ? '#ffffff' : '#333333',
                                                    cursor: formData.categories.length >= 3 && !formData.categories.includes(cat) ? 'not-allowed' : 'pointer',
                                                    opacity: formData.categories.length >= 3 && !formData.categories.includes(cat) ? 0.5 : 1,
                                                    transition: 'all 0.2s'
                                                }}
                                            >
                                                {cat}
                                            </button>
                                        ))}
                                    </div>
                                </div>

                                {/* Tags */}
                                <div>
                                    <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                        Tag
                                    </label>
                                    <div style={{ display: 'flex', gap: '0.5rem', marginBottom: '0.75rem' }}>
                                        <input
                                            type="text"
                                            value={tagInput}
                                            onChange={(e) => setTagInput(e.target.value)}
                                            onKeyPress={(e) => {
                                                if (e.key === 'Enter') {
                                                    e.preventDefault();
                                                    handleAddTag();
                                                }
                                            }}
                                            placeholder="Nhập tag và nhấn Enter"
                                            style={{
                                                flex: 1,
                                                padding: '0.5rem 0.75rem',
                                                backgroundColor: '#f9fafb',
                                                border: '1px solid #e5e7eb',
                                                borderRadius: '4px',
                                                fontSize: '0.875rem',
                                                outline: 'none'
                                            }}
                                        />
                                        <button
                                            onClick={handleAddTag}
                                            type="button"
                                            style={{
                                                padding: '0.5rem 1rem',
                                                backgroundColor: 'transparent',
                                                border: '1px solid #13ec5b',
                                                borderRadius: '4px',
                                                fontSize: '0.875rem',
                                                color: '#13ec5b',
                                                cursor: 'pointer',
                                                display: 'inline-flex',
                                                alignItems: 'center',
                                                gap: '0.25rem'
                                            }}
                                        >
                                            <Plus style={{ width: '14px', height: '14px' }} /> Thêm
                                        </button>
                                    </div>
                                    {formData.tags.length > 0 && (
                                        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem' }}>
                                            {formData.tags.map((tag) => (
                                                <div
                                                    key={tag}
                                                    style={{
                                                        padding: '0.25rem 0.75rem',
                                                        backgroundColor: '#f3f4f6',
                                                        borderRadius: '4px',
                                                        fontSize: '0.875rem',
                                                        display: 'flex',
                                                        alignItems: 'center',
                                                        gap: '0.5rem'
                                                    }}
                                                >
                                                    {tag}
                                                    <button
                                                        onClick={() => handleRemoveTag(tag)}
                                                        type="button"
                                                        style={{
                                                            border: 'none',
                                                            background: 'none',
                                                            cursor: 'pointer',
                                                            padding: 0,
                                                            color: '#6b7280',
                                                            display: 'flex',
                                                            alignItems: 'center'
                                                        }}
                                                    >
                                                        <X style={{ width: '14px', height: '14px' }} />
                                                    </button>
                                                </div>
                                            ))}
                                        </div>
                                    )}
                                </div>

                                {/* Note */}
                                <div>
                                    <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                        Ghi chú tác giả
                                    </label>
                                    <textarea
                                        value={formData.note}
                                        onChange={(e) => handleInputChange('note', e.target.value)}
                                        placeholder="Nhập ghi chú"
                                        rows={4}
                                        style={{
                                            width: '100%',
                                            padding: '0.75rem',
                                            backgroundColor: '#f9fafb',
                                            border: '1px solid #e5e7eb',
                                            borderRadius: '4px',
                                            fontSize: '0.875rem',
                                            outline: 'none',
                                            resize: 'vertical',
                                            fontFamily: 'inherit'
                                        }}
                                    />
                                </div>
                            </div>
                        </div>
                    </div>

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
                            className="px-6 py-2.5 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all"
                        >
                            Lưu thay đổi
                        </button>
                    </div>
                </div>
            </div>
            <Footer />
        </div>
    );
}