import { useState, useEffect } from 'react';
import { X, Plus, ChevronDown } from 'lucide-react';
import { getCategoriesWithPagination } from '../../../api/category/categoryApi';

export function StoryInfoForm({
    formData,
    onChange,
    onImageUpload,
    readOnlyFields = false,
    allowProgressOptions = null,
    disabledProgressOptions = [],
    /** Truyện mới: khóa dropdown trạng thái tiến độ ở «Đang ra». */
    lockStoryProgressStatus = false,
}) {

    const statusOptions = Array.isArray(allowProgressOptions) && allowProgressOptions.length > 0
        ? allowProgressOptions
        : ['Đang ra', 'Hoàn thành', 'Tạm dừng'];
    const ageRatings = ['Phù hợp mọi lứa tuổi', 'Từ 13 tuổi', 'Từ 16 tuổi', 'Từ 18 tuổi'];

    const [categories, setCategories] = useState([]);
    const [categoriesLoading, setCategoriesLoading] = useState(true);
    const [categoriesError, setCategoriesError] = useState(null);

    useEffect(() => {
        let cancelled = false;
        async function load() {
            setCategoriesLoading(true);
            setCategoriesError(null);
            try {
                const res = await getCategoriesWithPagination({
                    page: 1,
                    pageSize: 500,
                    includeInactive: false
                });
                const items = (res.items || []).map((c) => ({ id: c.id, name: c.name || '' }));
                if (!cancelled) setCategories(items);
            } catch (e) {
                if (!cancelled) {
                    setCategoriesError(e.message || 'Không tải được thể loại');
                    setCategories([]);
                }
            } finally {
                if (!cancelled) setCategoriesLoading(false);
            }
        }
        load();
        return () => { cancelled = true; };
    }, []);

    const getCategoryId = (c) => (typeof c === 'object' && c?.id ? c.id : c);
    const handleCategoryToggle = (category) => {
        const id = typeof category === 'object' ? category.id : category;
        const current = (formData.categories || []).map(getCategoryId);
        const newCategories = current.includes(id)
            ? formData.categories.filter((c) => getCategoryId(c) !== id)
            : [...formData.categories, { id: category.id, name: category.name }];
        onChange('categories', newCategories);
    };

    return (
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
                                onClick={() => onChange('cover', '')}
                                disabled={readOnlyFields}
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
                                    cursor: readOnlyFields ? 'not-allowed' : 'pointer',
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
                                cursor: readOnlyFields ? 'not-allowed' : 'pointer',
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
                        onChange={onImageUpload}
                        disabled={readOnlyFields}
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
                            onChange={(e) => onChange('title', e.target.value)}
                            disabled={readOnlyFields}
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
                            <button style={{ marginLeft: 'auto', padding: '0.25rem', border: 'none', background: 'none', cursor: 'pointer' }}>
                                ✏️
                            </button>
                        </div>
                    </div>

                    {/* Status — truyện mới: cố định «Đang ra», không dùng dropdown để tránh nhầm là có thể đổi */}
                    <div>
                        <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                            Trạng thái{lockStoryProgressStatus ? <span style={{ color: '#dc2626', marginLeft: '2px' }}>*</span> : null}
                        </label>
                        {lockStoryProgressStatus ? (
                            <>
                                <div
                                    title="Truyện mới luôn ở trạng thái Đang ra"
                                    style={{
                                        width: '100%',
                                        padding: '0.75rem',
                                        backgroundColor: '#f3f4f6',
                                        border: '1px solid #e5e7eb',
                                        borderRadius: '4px',
                                        fontSize: '0.875rem',
                                        color: '#111827',
                                        cursor: 'default',
                                        userSelect: 'none',
                                    }}
                                >
                                    Đang ra
                                </div>
                                <p style={{ fontSize: '0.75rem', color: '#6b7280', marginTop: '0.375rem', marginBottom: 0 }}>
                                    Truyện mới bắt buộc ở trạng thái «Đang ra»; sau khi xuất bản bạn có thể đổi trong mục chỉnh sửa truyện.
                                </p>
                            </>
                        ) : (
                            <div style={{ position: 'relative' }}>
                                <select
                                    value={formData.status}
                                    onChange={(e) => onChange('status', e.target.value)}
                                    disabled={readOnlyFields}
                                    style={{
                                        width: '100%',
                                        padding: '0.75rem',
                                        backgroundColor: readOnlyFields ? '#f3f4f6' : '#f9fafb',
                                        border: '1px solid #e5e7eb',
                                        borderRadius: '4px',
                                        fontSize: '0.875rem',
                                        outline: 'none',
                                        appearance: 'none',
                                        cursor: readOnlyFields ? 'not-allowed' : 'pointer',
                                    }}
                                >
                                    {statusOptions.map((opt) => (
                                        <option key={opt} value={opt} disabled={Array.isArray(disabledProgressOptions) && disabledProgressOptions.includes(opt)}>
                                            {opt}
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
                        )}
                    </div>

                    {/* Age Rating */}
                    <div>
                        <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                            Giới hạn độ tuổi
                        </label>
                        <div style={{ position: 'relative' }}>
                            <select
                                value={formData.ageRating}
                                onChange={(e) => onChange('ageRating', e.target.value)}
                                disabled={readOnlyFields}
                                style={{
                                    width: '100%',
                                    padding: '0.75rem',
                                    backgroundColor: '#f9fafb',
                                    border: '1px solid #e5e7eb',
                                    borderRadius: '4px',
                                    fontSize: '0.875rem',
                                    outline: 'none',
                                    appearance: 'none',
                                    cursor: readOnlyFields ? 'not-allowed' : 'pointer'
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

                    {/* Thể loại chi tiết - từ API */}
                    <div>
                        <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                            Thể loại chi tiết <span style={{ color: '#ef4444' }}>*</span> (Chọn tối đa 3)
                        </label>
                        {categoriesLoading && (
                            <p style={{ fontSize: '0.875rem', color: '#6b7280', marginBottom: '0.75rem' }}>Đang tải thể loại...</p>
                        )}
                        {categoriesError && (
                            <p style={{ fontSize: '0.875rem', color: '#dc2626', marginBottom: '0.75rem' }}>{categoriesError}</p>
                        )}
                        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '0.5rem' }}>
                            {!categoriesLoading &&
                                categories.map((cat) => {
                                    const isSelected = (formData.categories || []).map(getCategoryId).includes(cat.id);
                                    const isDisabled = readOnlyFields || (formData.categories.length >= 3 && !isSelected);
                                    return (
                                        <button
                                            key={cat.id}
                                            type="button"
                                            onClick={() => {
                                                if (formData.categories.length < 3 || isSelected) {
                                                    handleCategoryToggle(cat);
                                                }
                                            }}
                                            disabled={isDisabled}
                                            style={{
                                                padding: '0.5rem 1rem',
                                                backgroundColor: isSelected ? '#13ec5b' : '#ffffff',
                                                border: '1px solid #e5e7eb',
                                                borderRadius: '4px',
                                                fontSize: '0.875rem',
                                                color: isSelected ? '#ffffff' : '#333333',
                                                cursor: isDisabled ? 'not-allowed' : 'pointer',
                                                opacity: isDisabled ? 0.5 : 1,
                                                transition: 'all 0.2s'
                                            }}
                                            onMouseEnter={(e) => {
                                                if (!isDisabled) e.currentTarget.style.transform = 'scale(1.05)';
                                            }}
                                            onMouseLeave={(e) => {
                                                e.currentTarget.style.transform = 'scale(1)';
                                            }}
                                        >
                                            {cat.name}
                                        </button>
                                    );
                                })}
                        </div>
                    </div>

                    {/* Mô tả truyện (summary) — bắt buộc ở StoryEditor.validateStep1 */}
                    <div>
                        <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                            Mô tả truyện <span style={{ color: '#ef4444' }}>*</span>
                        </label>
                        <textarea
                            value={formData.note}
                            onChange={(e) => onChange('note', e.target.value)}
                            disabled={readOnlyFields}
                            placeholder="Nhập mô tả truyện"
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
    );
}