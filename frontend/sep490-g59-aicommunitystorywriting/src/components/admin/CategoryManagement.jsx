import { useState } from 'react';
import {
    Plus,
    Search,
    Filter,
    Edit2,
    Trash2,
    Eye,
    EyeOff,
    MoreVertical,
    Download,
    Upload
} from 'lucide-react';
import { CategoryModal } from './CategoryModal';

export function CategoryManagement() {
    const [searchTerm, setSearchTerm] = useState('');
    const [filterStatus, setFilterStatus] = useState('all');
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [editingCategory, setEditingCategory] = useState(null);
    const [selectedCategories, setSelectedCategories] = useState([]);

    // Mock data - Replace with API call
    const [categories, setCategories] = useState([
        {
            id: 1,
            name: 'Tiên hiệp',
            slug: 'tien-hiep',
            description: 'Thể loại truyện tu tiên, tu luyện, thăng cấp',
            icon_url: 'https://images.unsplash.com/photo-1589519160732-57fc498494f8?w=100&h=100&fit=crop',
            story_type: 'long',
            is_active: true,
            created_at: '2024-01-15T10:30:00'
        },
        {
            id: 2,
            name: 'Huyền huyễn',
            slug: 'huyen-huyen',
            description: 'Thể loại truyện huyền ảo, phép thuật',
            icon_url: 'https://images.unsplash.com/photo-1518640467707-6811f4a6ab73?w=100&h=100&fit=crop',
            story_type: 'long',
            is_active: true,
            created_at: '2024-01-15T10:31:00'
        },
        {
            id: 3,
            name: 'Kiếm hiệp',
            slug: 'kiem-hiep',
            description: 'Thể loại võ hiệp cổ điển',
            icon_url: 'https://images.unsplash.com/photo-1555685812-4b943f1cb0eb?w=100&h=100&fit=crop',
            story_type: 'long',
            is_active: true,
            created_at: '2024-01-15T10:32:00'
        },
        {
            id: 4,
            name: 'Ngôn tình',
            slug: 'ngon-tinh',
            description: 'Thể loại tình cảm lãng mạn',
            icon_url: 'https://images.unsplash.com/photo-1518199266791-5375a83190b7?w=100&h=100&fit=crop',
            story_type: 'long',
            is_active: true,
            created_at: '2024-01-15T10:33:00'
        },
        {
            id: 5,
            name: 'Đô thị',
            slug: 'do-thi',
            description: 'Thể loại hiện đại, đời thường',
            icon_url: 'https://images.unsplash.com/photo-1480714378408-67cf0d13bc1b?w=100&h=100&fit=crop',
            story_type: 'short',
            is_active: false,
            created_at: '2024-01-15T10:34:00'
        },
        {
            id: 6,
            name: 'Khoa huyễn',
            slug: 'khoa-huyen',
            description: 'Thể loại khoa học viễn tưởng',
            icon_url: 'https://images.unsplash.com/photo-1451187580459-43490279c0fa?w=100&h=100&fit=crop',
            story_type: 'long',
            is_active: true,
            created_at: '2024-01-15T10:35:00'
        },
        {
            id: 7,
            name: 'Đam mỹ',
            slug: 'dam-my',
            description: 'Thể loại tình cảm nam - nam',
            icon_url: 'https://images.unsplash.com/photo-1529626455594-4ff0802cfb7e?w=100&h=100&fit=crop',
            story_type: 'short',
            is_active: true,
            created_at: '2024-01-15T10:36:00'
        },
        {
            id: 8,
            name: 'Trọng sinh',
            slug: 'trong-sinh',
            description: 'Thể loại tái sinh, hồi quy',
            icon_url: 'https://images.unsplash.com/photo-1506905925346-21bda4d32df4?w=100&h=100&fit=crop',
            story_type: 'long',
            is_active: true,
            created_at: '2024-01-15T10:37:00'
        },
    ]);

    const filteredCategories = categories.filter(cat => {
        const matchesSearch = cat.name.toLowerCase().includes(searchTerm.toLowerCase()) ||
            cat.slug.toLowerCase().includes(searchTerm.toLowerCase());
        const matchesFilter = filterStatus === 'all' ||
            (filterStatus === 'active' && cat.is_active) ||
            (filterStatus === 'inactive' && !cat.is_active);
        return matchesSearch && matchesFilter;
    });

    const handleAddCategory = () => {
        setEditingCategory(null);
        setIsModalOpen(true);
    };

    const handleEditCategory = (category) => {
        setEditingCategory(category);
        setIsModalOpen(true);
    };

    const handleDeleteCategory = (id) => {
        if (confirm('Bạn có chắc chắn muốn xóa thể loại này?')) {
            setCategories(categories.filter(cat => cat.id !== id));
        }
    };

    const handleToggleStatus = (id) => {
        setCategories(categories.map(cat =>
            cat.id === id ? { ...cat, is_active: !cat.is_active } : cat
        ));
    };

    const handleSaveCategory = (categoryData) => {
        if (editingCategory) {
            // Update existing category
            setCategories(categories.map(cat =>
                cat.id === editingCategory.id ? { ...cat, ...categoryData } : cat
            ));
        } else {
            // Add new category
            const newCategory = {
                ...categoryData,
                id: Math.max(...categories.map(c => c.id)) + 1,
                created_at: new Date().toISOString()
            };
            setCategories([...categories, newCategory]);
        }
        setIsModalOpen(false);
    };

    const handleSelectCategory = (id) => {
        setSelectedCategories(prev =>
            prev.includes(id) ? prev.filter(cId => cId !== id) : [...prev, id]
        );
    };

    const handleSelectAll = () => {
        if (selectedCategories.length === filteredCategories.length) {
            setSelectedCategories([]);
        } else {
            setSelectedCategories(filteredCategories.map(cat => cat.id));
        }
    };

    const formatDate = (dateString) => {
        const date = new Date(dateString);
        return date.toLocaleDateString('vi-VN', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit'
        });
    };

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
            {/* Page Header */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '1rem' }}>
                    <div>
                        <h1 style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#1e293b', margin: 0 }}>
                            Quản lý thể loại truyện
                        </h1>
                        <p style={{ fontSize: '0.875rem', color: '#64748b', margin: '0.25rem 0 0 0' }}>
                            Quản lý các thể loại truyện trên hệ thống
                        </p>
                    </div>
                    <button
                        onClick={handleAddCategory}
                        style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '0.5rem',
                            padding: '0.625rem 1rem',
                            backgroundColor: 'var(--admin-primary, #10b981)',
                            color: '#ffffff',
                            fontSize: '0.875rem',
                            fontWeight: 'bold',
                            border: 'none',
                            borderRadius: '0.5rem',
                            cursor: 'pointer',
                            transition: 'background-color 0.2s'
                        }}
                        onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'var(--admin-primary-ink, #047857)'}
                        onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'var(--admin-primary, #10b981)'}
                    >
                        <Plus style={{ width: '16px', height: '16px' }} />
                        Thêm thể loại
                    </button>
                </div>
            </div>

            {/* Stats Cards */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(250px, 1fr))', gap: '1rem' }}>
                <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', padding: '1.25rem', border: '1px solid #e2e8f0' }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <div>
                            <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>Tổng thể loại</p>
                            <p style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#1e293b', margin: '0.25rem 0 0 0' }}>
                                {categories.length}
                            </p>
                        </div>
                        <div style={{ width: '48px', height: '48px', backgroundColor: 'var(--admin-primary-soft, rgba(16, 185, 129, 0.14))', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                            <span style={{ fontSize: '1.5rem' }}>📚</span>
                        </div>
                    </div>
                </div>

                <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', padding: '1.25rem', border: '1px solid #e2e8f0' }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <div>
                            <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>Đang hoạt động</p>
                            <p style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#16a34a', margin: '0.25rem 0 0 0' }}>
                                {categories.filter(c => c.is_active).length}
                            </p>
                        </div>
                        <div style={{ width: '48px', height: '48px', backgroundColor: 'rgba(22, 163, 74, 0.1)', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                            <Eye style={{ width: '24px', height: '24px', color: '#16a34a' }} />
                        </div>
                    </div>
                </div>

                <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', padding: '1.25rem', border: '1px solid #e2e8f0' }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <div>
                            <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>Đã tắt</p>
                            <p style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#dc2626', margin: '0.25rem 0 0 0' }}>
                                {categories.filter(c => !c.is_active).length}
                            </p>
                        </div>
                        <div style={{ width: '48px', height: '48px', backgroundColor: 'rgba(220, 38, 38, 0.1)', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                            <EyeOff style={{ width: '24px', height: '24px', color: '#dc2626' }} />
                        </div>
                    </div>
                </div>

                <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', padding: '1.25rem', border: '1px solid #e2e8f0' }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <div>
                            <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>Mới trong tuần</p>
                            <p style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#2563eb', margin: '0.25rem 0 0 0' }}>
                                3
                            </p>
                        </div>
                        <div style={{ width: '48px', height: '48px', backgroundColor: 'rgba(37, 99, 235, 0.1)', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                            <Plus style={{ width: '24px', height: '24px', color: '#2563eb' }} />
                        </div>
                    </div>
                </div>
            </div>

            {/* Filters & Table */}
            <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', border: '1px solid #e2e8f0', overflow: 'hidden' }}>
                {/* Filters */}
                <div style={{ padding: '1rem', borderBottom: '1px solid #e2e8f0' }}>
                    <div style={{ display: 'flex', flexWrap: 'wrap', gap: '1rem', alignItems: 'center' }}>
                        {/* Search */}
                        <div style={{ flex: '1 1 300px', display: 'flex', alignItems: 'center', gap: '0.5rem', padding: '0.5rem 1rem', backgroundColor: '#f8fafc', borderRadius: '0.5rem' }}>
                            <Search style={{ width: '16px', height: '16px', color: '#94a3b8' }} />
                            <input
                                type="text"
                                placeholder="Tìm kiếm theo tên hoặc slug..."
                                value={searchTerm}
                                onChange={(e) => setSearchTerm(e.target.value)}
                                style={{
                                    flex: 1,
                                    backgroundColor: 'transparent',
                                    border: 'none',
                                    outline: 'none',
                                    fontSize: '0.875rem',
                                    color: '#1e293b'
                                }}
                            />
                        </div>

                        {/* Filter Status */}
                        <select
                            value={filterStatus}
                            onChange={(e) => setFilterStatus(e.target.value)}
                            style={{
                                padding: '0.5rem 1rem',
                                backgroundColor: '#f8fafc',
                                border: '1px solid #e2e8f0',
                                borderRadius: '0.5rem',
                                fontSize: '0.875rem',
                                color: '#1e293b',
                                outline: 'none',
                                cursor: 'pointer'
                            }}
                        >
                            <option value="all">Tất cả trạng thái</option>
                            <option value="active">Đang hoạt động</option>
                            <option value="inactive">Đã tắt</option>
                        </select>

                        {/* Action Buttons */}
                        <div style={{ display: 'flex', gap: '0.5rem' }}>
                            <button
                                style={{
                                    padding: '0.5rem',
                                    border: 'none',
                                    background: 'transparent',
                                    borderRadius: '0.5rem',
                                    cursor: 'pointer',
                                    transition: 'background-color 0.2s'
                                }}
                                onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#f1f5f9'}
                                onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                            >
                                <Upload style={{ width: '20px', height: '20px', color: '#64748b' }} />
                            </button>
                            <button
                                style={{
                                    padding: '0.5rem',
                                    border: 'none',
                                    background: 'transparent',
                                    borderRadius: '0.5rem',
                                    cursor: 'pointer',
                                    transition: 'background-color 0.2s'
                                }}
                                onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#f1f5f9'}
                                onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                            >
                                <Download style={{ width: '20px', height: '20px', color: '#64748b' }} />
                            </button>
                            <button
                                style={{
                                    padding: '0.5rem',
                                    border: 'none',
                                    background: 'transparent',
                                    borderRadius: '0.5rem',
                                    cursor: 'pointer',
                                    transition: 'background-color 0.2s'
                                }}
                                onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#f1f5f9'}
                                onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                            >
                                <Filter style={{ width: '20px', height: '20px', color: '#64748b' }} />
                            </button>
                        </div>
                    </div>
                </div>

                {/* Table */}
                <div style={{ overflowX: 'auto' }}>
                    <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                        <thead>
                            <tr style={{ borderBottom: '1px solid #e2e8f0' }}>
                                <th style={{ textAlign: 'left', padding: '1rem' }}>
                                    <input
                                        type="checkbox"
                                        checked={selectedCategories.length === filteredCategories.length && filteredCategories.length > 0}
                                        onChange={handleSelectAll}
                                        style={{ width: '16px', height: '16px', cursor: 'pointer' }}
                                    />
                                </th>
                                <th style={{ textAlign: 'left', padding: '1rem', fontSize: '0.75rem', fontWeight: 600, color: '#64748b', textTransform: 'uppercase' }}>
                                    Icon
                                </th>
                                <th style={{ textAlign: 'left', padding: '1rem', fontSize: '0.75rem', fontWeight: 600, color: '#64748b', textTransform: 'uppercase' }}>
                                    Tên thể loại
                                </th>
                                <th style={{ textAlign: 'left', padding: '1rem', fontSize: '0.75rem', fontWeight: 600, color: '#64748b', textTransform: 'uppercase' }}>
                                    Slug
                                </th>
                                <th style={{ textAlign: 'left', padding: '1rem', fontSize: '0.75rem', fontWeight: 600, color: '#64748b', textTransform: 'uppercase' }}>
                                    Loại truyện
                                </th>
                                <th style={{ textAlign: 'left', padding: '1rem', fontSize: '0.75rem', fontWeight: 600, color: '#64748b', textTransform: 'uppercase' }}>
                                    Mô tả
                                </th>
                                <th style={{ textAlign: 'left', padding: '1rem', fontSize: '0.75rem', fontWeight: 600, color: '#64748b', textTransform: 'uppercase' }}>
                                    Trạng thái
                                </th>
                                <th style={{ textAlign: 'left', padding: '1rem', fontSize: '0.75rem', fontWeight: 600, color: '#64748b', textTransform: 'uppercase' }}>
                                    Ngày tạo
                                </th>
                                <th style={{ textAlign: 'right', padding: '1rem', fontSize: '0.75rem', fontWeight: 600, color: '#64748b', textTransform: 'uppercase' }}>
                                    Thao tác
                                </th>
                            </tr>
                        </thead>
                        <tbody>
                            {filteredCategories.map((category) => (
                                <tr
                                    key={category.id}
                                    style={{
                                        borderBottom: '1px solid #e2e8f0',
                                        transition: 'background-color 0.2s'
                                    }}
                                    onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#f8fafc'}
                                    onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                                >
                                    <td style={{ padding: '1rem' }}>
                                        <input
                                            type="checkbox"
                                            checked={selectedCategories.includes(category.id)}
                                            onChange={() => handleSelectCategory(category.id)}
                                            style={{ width: '16px', height: '16px', cursor: 'pointer' }}
                                        />
                                    </td>
                                    <td style={{ padding: '1rem' }}>
                                        <img
                                            src={category.icon_url}
                                            alt={category.name}
                                            style={{
                                                width: '40px',
                                                height: '40px',
                                                objectFit: 'cover',
                                                borderRadius: '0.5rem',
                                                border: '1px solid #e2e8f0'
                                            }}
                                        />
                                    </td>
                                    <td style={{ padding: '1rem' }}>
                                        <p style={{ fontWeight: 600, color: '#1e293b', margin: 0 }}>
                                            {category.name}
                                        </p>
                                    </td>
                                    <td style={{ padding: '1rem' }}>
                                        <code style={{ padding: '0.25rem 0.5rem', backgroundColor: '#f1f5f9', fontSize: '0.75rem', borderRadius: '0.25rem', color: '#1e293b' }}>
                                            {category.slug}
                                        </code>
                                    </td>
                                    <td style={{ padding: '1rem' }}>
                                        <span
                                            style={{
                                                display: 'inline-flex',
                                                alignItems: 'center',
                                                gap: '0.25rem',
                                                padding: '0.25rem 0.75rem',
                                                borderRadius: '9999px',
                                                fontSize: '0.75rem',
                                                fontWeight: 600,
                                                backgroundColor: category.story_type === 'long' ? 'rgba(37, 99, 235, 0.1)' : 'rgba(168, 85, 247, 0.1)',
                                                color: category.story_type === 'long' ? '#1d4ed8' : '#7c3aed'
                                            }}
                                        >
                                            {category.story_type === 'long' ? '📖 Truyện dài' : '📄 Truyện ngắn'}
                                        </span>
                                    </td>
                                    <td style={{ padding: '1rem' }}>
                                        <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0, maxWidth: '300px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                            {category.description}
                                        </p>
                                    </td>
                                    <td style={{ padding: '1rem' }}>
                                        <button
                                            onClick={() => handleToggleStatus(category.id)}
                                            style={{
                                                display: 'inline-flex',
                                                alignItems: 'center',
                                                gap: '0.375rem',
                                                padding: '0.375rem 0.75rem',
                                                borderRadius: '9999px',
                                                fontSize: '0.75rem',
                                                fontWeight: 600,
                                                border: 'none',
                                                cursor: 'pointer',
                                                transition: 'background-color 0.2s',
                                                backgroundColor: category.is_active ? 'rgba(22, 163, 74, 0.1)' : 'rgba(220, 38, 38, 0.1)',
                                                color: category.is_active ? '#15803d' : '#b91c1c'
                                            }}
                                            onMouseEnter={(e) => {
                                                if (category.is_active) {
                                                    e.currentTarget.style.backgroundColor = 'rgba(22, 163, 74, 0.2)';
                                                } else {
                                                    e.currentTarget.style.backgroundColor = 'rgba(220, 38, 38, 0.2)';
                                                }
                                            }}
                                            onMouseLeave={(e) => {
                                                if (category.is_active) {
                                                    e.currentTarget.style.backgroundColor = 'rgba(22, 163, 74, 0.1)';
                                                } else {
                                                    e.currentTarget.style.backgroundColor = 'rgba(220, 38, 38, 0.1)';
                                                }
                                            }}
                                        >
                                            {category.is_active ? (
                                                <>
                                                    <Eye style={{ width: '12px', height: '12px' }} />
                                                    Hoạt động
                                                </>
                                            ) : (
                                                <>
                                                    <EyeOff style={{ width: '12px', height: '12px' }} />
                                                    Đã tắt
                                                </>
                                            )}
                                        </button>
                                    </td>
                                    <td style={{ padding: '1rem' }}>
                                        <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>
                                            {formatDate(category.created_at)}
                                        </p>
                                    </td>
                                    <td style={{ padding: '1rem' }}>
                                        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'flex-end', gap: '0.5rem' }}>
                                            <button
                                                onClick={() => handleEditCategory(category)}
                                                style={{
                                                    padding: '0.5rem',
                                                    border: 'none',
                                                    background: 'transparent',
                                                    borderRadius: '0.5rem',
                                                    cursor: 'pointer',
                                                    color: '#2563eb',
                                                    transition: 'background-color 0.2s'
                                                }}
                                                onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'rgba(37, 99, 235, 0.1)'}
                                                onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                                                title="Chỉnh sửa"
                                            >
                                                <Edit2 style={{ width: '16px', height: '16px' }} />
                                            </button>
                                            <button
                                                onClick={() => handleDeleteCategory(category.id)}
                                                style={{
                                                    padding: '0.5rem',
                                                    border: 'none',
                                                    background: 'transparent',
                                                    borderRadius: '0.5rem',
                                                    cursor: 'pointer',
                                                    color: '#dc2626',
                                                    transition: 'background-color 0.2s'
                                                }}
                                                onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'rgba(220, 38, 38, 0.1)'}
                                                onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                                                title="Xóa"
                                            >
                                                <Trash2 style={{ width: '16px', height: '16px' }} />
                                            </button>
                                        </div>
                                    </td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                </div>

                {filteredCategories.length === 0 && (
                    <div style={{ padding: '3rem', textAlign: 'center' }}>
                        <p style={{ color: '#64748b', margin: 0 }}>
                            Không tìm thấy thể loại nào
                        </p>
                    </div>
                )}

                {/* Pagination */}
                <div style={{ padding: '1rem', borderTop: '1px solid #e2e8f0', display: 'flex', alignItems: 'center', justifyContent: 'space-between', flexWrap: 'wrap', gap: '1rem' }}>
                    <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>
                        Hiển thị <span style={{ fontWeight: 600 }}>{filteredCategories.length}</span> / {categories.length} thể loại
                    </p>
                    <div style={{ display: 'flex', gap: '0.5rem' }}>
                        <button style={{ padding: '0.375rem 0.75rem', border: '1px solid #e2e8f0', borderRadius: '0.5rem', fontSize: '0.875rem', backgroundColor: '#ffffff', color: '#1e293b', cursor: 'pointer', transition: 'background-color 0.2s' }}
                            onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#f8fafc'}
                            onMouseLeave={(e) => e.currentTarget.style.backgroundColor = '#ffffff'}
                        >
                            Trước
                        </button>
                        <button style={{ padding: '0.375rem 0.75rem', border: 'none', borderRadius: '0.5rem', fontSize: '0.875rem', backgroundColor: 'var(--admin-primary, #10b981)', color: '#ffffff', cursor: 'pointer' }}>
                            1
                        </button>
                        <button style={{ padding: '0.375rem 0.75rem', border: '1px solid #e2e8f0', borderRadius: '0.5rem', fontSize: '0.875rem', backgroundColor: '#ffffff', color: '#1e293b', cursor: 'pointer', transition: 'background-color 0.2s' }}
                            onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#f8fafc'}
                            onMouseLeave={(e) => e.currentTarget.style.backgroundColor = '#ffffff'}
                        >
                            2
                        </button>
                        <button style={{ padding: '0.375rem 0.75rem', border: '1px solid #e2e8f0', borderRadius: '0.5rem', fontSize: '0.875rem', backgroundColor: '#ffffff', color: '#1e293b', cursor: 'pointer', transition: 'background-color 0.2s' }}
                            onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#f8fafc'}
                            onMouseLeave={(e) => e.currentTarget.style.backgroundColor = '#ffffff'}
                        >
                            Sau
                        </button>
                    </div>
                </div>
            </div>

            {/* Category Modal */}
            <CategoryModal
                isOpen={isModalOpen}
                onClose={() => setIsModalOpen(false)}
                onSave={handleSaveCategory}
                category={editingCategory}
            />
        </div>
    );
}