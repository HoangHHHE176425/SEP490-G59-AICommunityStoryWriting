import { useState, useEffect } from 'react';
import { X, Save, AlertCircle, Upload, Image as ImageIcon } from 'lucide-react';

export function CategoryModal({ isOpen, onClose, onSave, category, isViewOnly = false }) {
    const [formData, setFormData] = useState({
        name: '',
        slug: '',
        description: '',
        icon_url: '',
        iconFile: null, // File object for API
        is_active: true
    });

    const [errors, setErrors] = useState({});
    const [imagePreview, setImagePreview] = useState(null);

    // Get full icon URL for preview
    const getIconUrl = (iconUrl) => {
        if (!iconUrl) return '';
        if (iconUrl.startsWith('http://') || iconUrl.startsWith('https://')) {
            // Check if it's a valid absolute URL
            try {
                const urlObj = new URL(iconUrl);
                if (urlObj.hostname && urlObj.hostname !== 'uploads' && urlObj.hostname.includes('.')) {
                    return iconUrl;
                }
            } catch {
                // Invalid URL, treat as relative path
            }
        }

        // Handle relative paths
        let path = iconUrl;
        if (iconUrl.startsWith('http://uploads/') || iconUrl.startsWith('https://uploads/')) {
            path = iconUrl.replace(/^https?:\/\/uploads\//, '/uploads/');
        } else if (iconUrl.startsWith('http://') || iconUrl.startsWith('https://')) {
            path = iconUrl.replace(/^https?:\/\//, '/');
        }

        if (!path.startsWith('/')) {
            path = '/' + path;
        }

        const apiUrl = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';
        const baseUrl = apiUrl.replace(/\/api\/?$/, '').replace(/\/$/, '');
        return `${baseUrl}${path}`;
    };

    useEffect(() => {
        if (category) {
            // Map API response fields to formData
            // API returns: iconUrl (camelCase), isActive (camelCase)
            // Form expects: icon_url (snake_case), is_active (snake_case)
            const iconUrl = category.iconUrl || '';
            const fullIconUrl = getIconUrl(iconUrl);

            // eslint-disable-next-line react-hooks/set-state-in-effect
            setFormData({
                name: category.name || '',
                slug: category.slug || '',
                description: category.description || '',
                icon_url: iconUrl, // Keep original for API, use fullIconUrl for preview
                iconFile: null, // Reset file when editing (user can upload new one)
                is_active: category.isActive !== false // Handle both camelCase and snake_case
            });
            setImagePreview(fullIconUrl || null);
        } else {
            setFormData({
                name: '',
                slug: '',
                description: '',
                icon_url: '',
                iconFile: null,
                is_active: true
            });
            setImagePreview(null);
        }
        setErrors({});
    }, [category, isOpen]);

    const generateSlug = (name) => {
        return name
            .toLowerCase()
            .normalize('NFD')
            .replace(/[\u0300-\u036f]/g, '')
            .replace(/đ/g, 'd')
            .replace(/Đ/g, 'D')
            .replace(/[^a-z0-9\s-]/g, '')
            .replace(/\s+/g, '-')
            .replace(/-+/g, '-')
            .trim();
    };

    const handleNameChange = (e) => {
        const name = e.target.value;
        setFormData(prev => ({
            ...prev,
            name,
            slug: generateSlug(name)
        }));
    };

    const handleImageUpload = (e) => {
        const file = e.target.files[0];
        if (file) {
            // Validate file type - check MIME type
            if (!file.type.startsWith('image/')) {
                setErrors(prev => ({ ...prev, icon_url: 'Vui lòng chọn file ảnh hợp lệ' }));
                return;
            }

            // Validate file extension (backend requires: jpg, jpeg, png, gif, webp, svg)
            const allowedExtensions = ['.jpg', '.jpeg', '.png', '.gif', '.webp', '.svg'];
            const fileExtension = file.name.toLowerCase().substring(file.name.lastIndexOf('.'));
            if (!allowedExtensions.includes(fileExtension)) {
                setErrors(prev => ({
                    ...prev,
                    icon_url: 'Định dạng file không hợp lệ. Chỉ chấp nhận: JPG, JPEG, PNG, GIF, WEBP, SVG'
                }));
                return;
            }

            // Validate file size (max 2MB)
            if (file.size > 2 * 1024 * 1024) {
                setErrors(prev => ({ ...prev, icon_url: 'Kích thước ảnh không được vượt quá 2MB' }));
                return;
            }

            // Create preview and save File object
            const reader = new FileReader();
            reader.onloadend = () => {
                setImagePreview(reader.result);
                setFormData(prev => ({
                    ...prev,
                    icon_url: reader.result,
                    iconFile: file // Save File object for API
                }));
                setErrors(prev => ({ ...prev, icon_url: null }));
            };
            reader.readAsDataURL(file);
        }
    };

    const validateForm = () => {
        const newErrors = {};

        if (!formData.name.trim()) {
            newErrors.name = 'Tên thể loại không được để trống';
        }

        if (!formData.slug.trim()) {
            newErrors.slug = 'Slug không được để trống';
        }

        // Icon is required: either existing icon_url or new iconFile
        if (!formData.icon_url.trim() && !formData.iconFile) {
            newErrors.icon_url = 'Vui lòng tải lên ảnh icon';
        }

        setErrors(newErrors);
        return Object.keys(newErrors).length === 0;
    };

    const handleSubmit = (e) => {
        e.preventDefault();

        // Don't submit if in view-only mode
        if (isViewOnly) {
            return;
        }

        if (validateForm()) {
            onSave(formData);
            onClose();
        }
    };

    if (!isOpen) return null;

    return (
        <div
            style={{
                position: 'fixed',
                top: 0,
                left: 0,
                right: 0,
                bottom: 0,
                zIndex: 999,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'center',
                padding: '1rem',
                backgroundColor: 'rgba(0, 0, 0, 0.5)',
                backdropFilter: 'blur(4px)'
            }}
            onClick={onClose}
        >
            <div
                style={{
                    backgroundColor: '#ffffff',
                    borderRadius: '1rem',
                    boxShadow: '0 25px 50px -12px rgba(0, 0, 0, 0.25)',
                    maxWidth: '640px',
                    width: '100%',
                    maxHeight: '90vh',
                    overflowY: 'auto',
                    border: '1px solid #e2e8f0'
                }}
                onClick={(e) => e.stopPropagation()}
            >
                {/* Header */}
                <div
                    style={{
                        position: 'sticky',
                        top: 0,
                        backgroundColor: '#ffffff',
                        borderBottom: '1px solid #e2e8f0',
                        padding: '1.5rem',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'space-between',
                        zIndex: 10
                    }}
                >
                    <h2 style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#1e293b', margin: 0 }}>
                        {isViewOnly ? 'Chi tiết thể loại' : (category ? 'Chỉnh sửa thể loại' : 'Thêm thể loại mới')}
                    </h2>
                    <button
                        onClick={onClose}
                        style={{
                            padding: '0.5rem',
                            border: 'none',
                            background: 'transparent',
                            borderRadius: '9999px',
                            cursor: 'pointer',
                            transition: 'background-color 0.2s'
                        }}
                        onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#f1f5f9'}
                        onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                    >
                        <X style={{ width: '20px', height: '20px', color: '#64748b' }} />
                    </button>
                </div>

                {/* Form */}
                <form onSubmit={handleSubmit} style={{ padding: '1.5rem', display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
                    {/* Name */}
                    <div>
                        <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 600, color: '#1e293b', marginBottom: '0.5rem' }}>
                            Tên thể loại <span style={{ color: '#ef4444' }}>*</span>
                        </label>
                        <input
                            type="text"
                            value={formData.name}
                            onChange={handleNameChange}
                            readOnly={isViewOnly}
                            placeholder="Ví dụ: Tiên hiệp"
                            style={{
                                width: '100%',
                                padding: '0.625rem 1rem',
                                backgroundColor: isViewOnly ? '#f1f5f9' : '#f8fafc',
                                border: errors.name ? '1px solid #ef4444' : '1px solid #e2e8f0',
                                borderRadius: '0.5rem',
                                outline: 'none',
                                fontSize: '0.875rem',
                                color: '#1e293b',
                                cursor: isViewOnly ? 'not-allowed' : 'text',
                                transition: 'all 0.2s'
                            }}
                            onFocus={(e) => {
                                if (!errors.name) {
                                    e.currentTarget.style.borderColor = '#13ec5b';
                                    e.currentTarget.style.boxShadow = '0 0 0 3px rgba(19, 236, 91, 0.1)';
                                }
                            }}
                            onBlur={(e) => {
                                e.currentTarget.style.borderColor = errors.name ? '#ef4444' : '#e2e8f0';
                                e.currentTarget.style.boxShadow = 'none';
                            }}
                        />
                        {errors.name && (
                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem', marginTop: '0.25rem' }}>
                                <AlertCircle style={{ width: '12px', height: '12px', color: '#ef4444' }} />
                                <span style={{ fontSize: '0.75rem', color: '#ef4444' }}>{errors.name}</span>
                            </div>
                        )}
                    </div>

                    {/* Slug */}
                    <div>
                        <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 600, color: '#1e293b', marginBottom: '0.5rem' }}>
                            Slug <span style={{ color: '#ef4444' }}>*</span>
                        </label>
                        <input
                            type="text"
                            value={formData.slug}
                            onChange={(e) => setFormData(prev => ({ ...prev, slug: e.target.value }))}
                            readOnly={true}
                            placeholder="tien-hiep"
                            style={{
                                width: '100%',
                                padding: '0.625rem 1rem',
                                backgroundColor: '#f1f5f9',
                                border: errors.slug ? '1px solid #ef4444' : '1px solid #e2e8f0',
                                borderRadius: '0.5rem',
                                outline: 'none',
                                fontSize: '0.875rem',
                                color: '#1e293b',
                                cursor: 'not-allowed',
                                transition: 'all 0.2s'
                            }}
                            onFocus={(e) => {
                                if (!errors.slug) {
                                    e.currentTarget.style.borderColor = '#13ec5b';
                                    e.currentTarget.style.boxShadow = '0 0 0 3px rgba(19, 236, 91, 0.1)';
                                }
                            }}
                            onBlur={(e) => {
                                e.currentTarget.style.borderColor = errors.slug ? '#ef4444' : '#e2e8f0';
                                e.currentTarget.style.boxShadow = 'none';
                            }}
                        />
                        {errors.slug && (
                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem', marginTop: '0.25rem' }}>
                                <AlertCircle style={{ width: '12px', height: '12px', color: '#ef4444' }} />
                                <span style={{ fontSize: '0.75rem', color: '#ef4444' }}>{errors.slug}</span>
                            </div>
                        )}
                        <p style={{ marginTop: '0.25rem', fontSize: '0.75rem', color: '#64748b', margin: '0.25rem 0 0 0' }}>
                            Slug được tự động tạo từ tên thể loại
                        </p>
                    </div>

                    {/* Icon Upload */}
                    <div>
                        <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 600, color: '#1e293b', marginBottom: '0.5rem' }}>
                            Ảnh icon <span style={{ color: '#ef4444' }}>*</span>
                        </label>

                        <div style={{ display: 'flex', gap: '1rem', alignItems: 'flex-start' }}>
                            {/* Upload Button */}
                            {!isViewOnly && (
                                <label
                                    style={{
                                        flex: 1,
                                        display: 'flex',
                                        flexDirection: 'column',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        padding: '2rem 1rem',
                                        backgroundColor: '#f8fafc',
                                        border: errors.icon_url ? '2px dashed #ef4444' : '2px dashed #e2e8f0',
                                        borderRadius: '0.5rem',
                                        cursor: 'pointer',
                                        transition: 'all 0.2s'
                                    }}
                                    onMouseEnter={(e) => {
                                        if (!errors.icon_url) {
                                            e.currentTarget.style.borderColor = '#13ec5b';
                                            e.currentTarget.style.backgroundColor = 'rgba(19, 236, 91, 0.05)';
                                        }
                                    }}
                                    onMouseLeave={(e) => {
                                        e.currentTarget.style.borderColor = errors.icon_url ? '#ef4444' : '#e2e8f0';
                                        e.currentTarget.style.backgroundColor = '#f8fafc';
                                    }}
                                >
                                    <Upload style={{ width: '32px', height: '32px', color: '#64748b', marginBottom: '0.5rem' }} />
                                    <p style={{ fontSize: '0.875rem', fontWeight: 600, color: '#1e293b', margin: 0 }}>
                                        Tải ảnh lên
                                    </p>
                                    <p style={{ fontSize: '0.75rem', color: '#64748b', marginTop: '0.25rem' }}>
                                        JPG, JPEG, PNG, GIF, WEBP, SVG (Max 2MB)
                                    </p>
                                    <input
                                        type="file"
                                        accept=".jpg,.jpeg,.png,.gif,.webp,.svg,image/jpeg,image/png,image/gif,image/webp,image/svg+xml"
                                        onChange={handleImageUpload}
                                        style={{ display: 'none' }}
                                    />
                                </label>
                            )}

                            {/* Image Preview */}
                            {imagePreview && (
                                <div style={{ width: '120px', height: '120px', position: 'relative' }}>
                                    <img
                                        src={imagePreview}
                                        alt="Preview"
                                        style={{
                                            width: '100%',
                                            height: '100%',
                                            objectFit: 'cover',
                                            borderRadius: '0.5rem',
                                            border: '1px solid #e2e8f0'
                                        }}
                                    />
                                    {!isViewOnly && (
                                        <button
                                            type="button"
                                            onClick={() => {
                                                setImagePreview(null);
                                                setFormData(prev => ({ ...prev, icon_url: '', iconFile: null }));
                                            }}
                                            style={{
                                                position: 'absolute',
                                                top: '-8px',
                                                right: '-8px',
                                                width: '24px',
                                                height: '24px',
                                                backgroundColor: '#ef4444',
                                                color: '#ffffff',
                                                border: 'none',
                                                borderRadius: '9999px',
                                                cursor: 'pointer',
                                                display: 'flex',
                                                alignItems: 'center',
                                                justifyContent: 'center',
                                                fontSize: '12px',
                                                fontWeight: 'bold',
                                                boxShadow: '0 2px 4px rgba(0, 0, 0, 0.1)'
                                            }}
                                        >
                                            ×
                                        </button>
                                    )}
                                </div>
                            )}
                        </div>

                        {errors.icon_url && (
                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.25rem', marginTop: '0.5rem' }}>
                                <AlertCircle style={{ width: '12px', height: '12px', color: '#ef4444' }} />
                                <span style={{ fontSize: '0.75rem', color: '#ef4444' }}>{errors.icon_url}</span>
                            </div>
                        )}
                    </div>

                    {/* Description */}
                    <div>
                        <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 600, color: '#1e293b', marginBottom: '0.5rem' }}>
                            Mô tả
                        </label>
                        <textarea
                            value={formData.description}
                            onChange={(e) => setFormData(prev => ({ ...prev, description: e.target.value }))}
                            readOnly={isViewOnly}
                            placeholder="Nhập mô tả chi tiết về thể loại này..."
                            rows={4}
                            style={{
                                width: '100%',
                                padding: '0.625rem 1rem',
                                backgroundColor: isViewOnly ? '#f1f5f9' : '#f8fafc',
                                border: '1px solid #e2e8f0',
                                borderRadius: '0.5rem',
                                outline: 'none',
                                fontSize: '0.875rem',
                                color: '#1e293b',
                                resize: 'none',
                                cursor: isViewOnly ? 'not-allowed' : 'text',
                                transition: 'all 0.2s'
                            }}
                            onFocus={(e) => {
                                e.currentTarget.style.borderColor = '#13ec5b';
                                e.currentTarget.style.boxShadow = '0 0 0 3px rgba(19, 236, 91, 0.1)';
                            }}
                            onBlur={(e) => {
                                e.currentTarget.style.borderColor = '#e2e8f0';
                                e.currentTarget.style.boxShadow = 'none';
                            }}
                        />
                        <p style={{ marginTop: '0.25rem', fontSize: '0.75rem', color: '#64748b', margin: '0.25rem 0 0 0' }}>
                            {formData.description.length} / 500 ký tự
                        </p>
                    </div>

                    {/* Status */}
                    <div
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            padding: '1rem',
                            backgroundColor: '#f8fafc',
                            borderRadius: '0.5rem'
                        }}
                    >
                        <div>
                            <p style={{ fontWeight: 600, color: '#1e293b', margin: 0 }}>Trạng thái hoạt động</p>
                            <p style={{ fontSize: '0.75rem', color: '#64748b', marginTop: '0.125rem', margin: '0.125rem 0 0 0' }}>
                                Bật/tắt thể loại này trên trang web
                            </p>
                        </div>
                        <label style={{ position: 'relative', display: 'inline-flex', alignItems: 'center', cursor: isViewOnly ? 'not-allowed' : 'pointer' }}>
                            <input
                                type="checkbox"
                                checked={formData.is_active}
                                onChange={(e) => setFormData(prev => ({ ...prev, is_active: e.target.checked }))}
                                disabled={isViewOnly}
                                style={{ display: 'none' }}
                            />
                            <div
                                style={{
                                    width: '44px',
                                    height: '24px',
                                    backgroundColor: formData.is_active ? '#13ec5b' : '#cbd5e1',
                                    borderRadius: '9999px',
                                    position: 'relative',
                                    transition: 'background-color 0.2s'
                                }}
                            >
                                <div
                                    style={{
                                        position: 'absolute',
                                        top: '2px',
                                        left: formData.is_active ? '22px' : '2px',
                                        width: '20px',
                                        height: '20px',
                                        backgroundColor: '#ffffff',
                                        borderRadius: '9999px',
                                        transition: 'left 0.2s',
                                        boxShadow: '0 2px 4px rgba(0, 0, 0, 0.1)'
                                    }}
                                />
                            </div>
                        </label>
                    </div>

                    {/* Actions */}
                    {!isViewOnly && (
                        <div style={{ display: 'flex', gap: '0.75rem', paddingTop: '1rem', borderTop: '1px solid #e2e8f0' }}>
                            <button
                                type="button"
                                onClick={onClose}
                                style={{
                                    flex: 1,
                                    padding: '0.625rem 1rem',
                                    backgroundColor: '#f1f5f9',
                                    color: '#1e293b',
                                    fontSize: '0.875rem',
                                    fontWeight: 'bold',
                                    border: 'none',
                                    borderRadius: '0.5rem',
                                    cursor: 'pointer',
                                    transition: 'background-color 0.2s'
                                }}
                                onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#e2e8f0'}
                                onMouseLeave={(e) => e.currentTarget.style.backgroundColor = '#f1f5f9'}
                            >
                                Hủy
                            </button>
                            <button
                                type="submit"
                                style={{
                                    flex: 1,
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    gap: '0.5rem',
                                    padding: '0.625rem 1rem',
                                    backgroundColor: '#13ec5b',
                                    color: '#ffffff',
                                    fontSize: '0.875rem',
                                    fontWeight: 'bold',
                                    border: 'none',
                                    borderRadius: '0.5rem',
                                    cursor: 'pointer',
                                    transition: 'background-color 0.2s'
                                }}
                                onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#10d352'}
                                onMouseLeave={(e) => e.currentTarget.style.backgroundColor = '#13ec5b'}
                            >
                                <Save style={{ width: '16px', height: '16px' }} />
                                {category ? 'Cập nhật' : 'Thêm mới'}
                            </button>
                        </div>
                    )}
                    {isViewOnly && (
                        <div style={{ display: 'flex', gap: '0.75rem', paddingTop: '1rem', borderTop: '1px solid #e2e8f0' }}>
                            <button
                                type="button"
                                onClick={onClose}
                                style={{
                                    width: '100%',
                                    padding: '0.625rem 1rem',
                                    backgroundColor: '#13ec5b',
                                    color: '#ffffff',
                                    fontSize: '0.875rem',
                                    fontWeight: 'bold',
                                    border: 'none',
                                    borderRadius: '0.5rem',
                                    cursor: 'pointer',
                                    transition: 'background-color 0.2s'
                                }}
                                onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#10d352'}
                                onMouseLeave={(e) => e.currentTarget.style.backgroundColor = '#13ec5b'}
                            >
                                Đóng
                            </button>
                        </div>
                    )}
                </form>
            </div>
        </div>
    );
}
