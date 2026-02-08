import { useState, useEffect } from 'react';
import {
    Plus,
    Search,
    Edit2,
    Trash2,
    Eye,
    EyeOff,
    Info,
    Loader2
} from 'lucide-react';
import { CategoryModal } from '../../../components/admin/category/CategoryModal';
import { Pagination } from '../../../components/pagination/Pagination';
import { createCategory, getCategoriesWithPagination, updateCategory } from '../../../api/category/categoryApi';
import { useToast } from '../../../components/author/story-editor/Toast';

export function CategoryManagement() {
    const { showToast, ToastContainer } = useToast();
    const [searchTerm, setSearchTerm] = useState('');
    const [filterStatus, setFilterStatus] = useState('all');
    const [isModalOpen, setIsModalOpen] = useState(false);
    const [editingCategory, setEditingCategory] = useState(null);
    const [viewingCategory, setViewingCategory] = useState(null);
    const [categories, setCategories] = useState([]);
    const [allCategoriesForStats, setAllCategoriesForStats] = useState([]); // All categories for stats (not filtered by status)
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);

    // Pagination state
    const [currentPage, setCurrentPage] = useState(1);
    const [pageSize] = useState(10);
    const [totalCount, setTotalCount] = useState(0);
    const [totalPages, setTotalPages] = useState(1);

    // Load categories for stats (all categories, not filtered by status)
    const loadCategoriesForStats = async () => {
        try {
            const queryParams = {
                page: 1,
                pageSize: 1000, // Get all categories for stats
                includeInactive: true, // Include all statuses for stats
            };

            const result = await getCategoriesWithPagination(queryParams);
            const allCategories = result.items || [];
            setAllCategoriesForStats(allCategories);
        } catch (err) {
            console.error('Error loading categories for stats:', err);
            // Don't show error to user, just log it
        }
    };

    // Load categories from API with pagination
    useEffect(() => {
        loadCategories();
        loadCategoriesForStats(); // Also load all categories for stats
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [currentPage]);

    // Handle search with debounce - reset to page 1
    useEffect(() => {
        const timer = setTimeout(() => {
            if (currentPage !== 1) {
                setCurrentPage(1);
            } else {
                loadCategories(1);
            }
            // Stats don't need to reload on search, they show all categories
        }, 500);

        return () => clearTimeout(timer);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [searchTerm]);

    // Handle filter change - reset to page 1
    useEffect(() => {
        if (currentPage !== 1) {
            setCurrentPage(1);
        } else {
            loadCategories(1);
        }
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [filterStatus]);

    const loadCategories = async (page = currentPage) => {
        try {
            setLoading(true);
            setError(null);
            const queryParams = {
                page: page,
                pageSize: pageSize,
                search: searchTerm || null,
            };

            if (filterStatus === 'active') {
                queryParams.isActive = true;
                queryParams.includeInactive = false; // Only active
            } else if (filterStatus === 'inactive') {
                queryParams.isActive = false;
                queryParams.includeInactive = true; // Include inactive to show inactive
            } else {
                // filterStatus === 'all' - show all statuses
                queryParams.includeInactive = true; // Include inactive categories
                // Don't set isActive, let backend handle it
            }

            const result = await getCategoriesWithPagination(queryParams);

            // Use data directly from API
            const categoriesData = result.items || [];
            const backendTotalCount = result.totalCount || 0;
            const backendTotalPages = result.totalPages || 1;

            setCategories(categoriesData);
            setTotalCount(backendTotalCount);
            setTotalPages(backendTotalPages);
            setCurrentPage(result.page || page);
        } catch (err) {
            console.error('Error loading categories:', err);
            setError('Không thể tải danh sách thể loại. Vui lòng thử lại sau.');
        } finally {
            setLoading(false);
        }
    };

    const handlePageChange = (page) => {
        setCurrentPage(page);
        loadCategories(page);
    };

    const filteredCategories = categories; // Already filtered from API

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
            cat.id === id ? { ...cat, isActive: !cat.isActive } : cat
        ));
    };

    // Get full icon URL (handle relative paths)
    const getIconUrl = (iconUrl) => {
        if (!iconUrl) return '';

        // Check if URL is a valid absolute URL with domain (not just http://uploads/...)
        const isValidAbsoluteUrl = (url) => {
            try {
                const urlObj = new URL(url);
                // Check if it has a valid hostname (not empty and not just 'uploads')
                return urlObj.hostname && urlObj.hostname !== 'uploads' && urlObj.hostname.includes('.');
            } catch {
                return false;
            }
        };

        // If it's a valid absolute URL, return as is
        if (isValidAbsoluteUrl(iconUrl)) {
            return iconUrl;
        }

        // Handle URLs that start with http://uploads/ or https://uploads/ (invalid absolute URLs)
        // or relative paths like /uploads/icons/... or uploads/icons/...
        let path = iconUrl;
        if (iconUrl.startsWith('http://uploads/') || iconUrl.startsWith('https://uploads/')) {
            // Extract path part after http://uploads/ or https://uploads/
            path = iconUrl.replace(/^https?:\/\/uploads\//, '/uploads/');
        } else if (iconUrl.startsWith('http://') || iconUrl.startsWith('https://')) {
            // Other invalid http:// URLs, try to extract path
            path = iconUrl.replace(/^https?:\/\//, '/');
        }

        // Ensure path starts with /
        if (!path.startsWith('/')) {
            path = '/' + path;
        }

        // Get base URL (remove /api if present, as static files are served from root)
        let baseUrl = import.meta.env.VITE_API_URL || 'https://localhost:7117/api';
        // Remove /api suffix if present, as static files are served from root
        baseUrl = baseUrl.replace(/\/api\/?$/, '');
        // Ensure baseUrl doesn't end with /
        baseUrl = baseUrl.replace(/\/$/, '');

        return `${baseUrl}${path}`;
    };

    const handleSaveCategory = async (categoryData) => {
        try {
            if (editingCategory) {
                // Call API to update category
                await updateCategory(editingCategory.id, {
                    name: categoryData.name,
                    description: categoryData.description || '',
                    isActive: categoryData.is_active !== false,
                    iconImage: categoryData.iconFile || null
                });

                // Reload categories from API to get updated list
                await loadCategories(currentPage);
                await loadCategoriesForStats(); // Reload stats

                // Show success message
                showToast('Cập nhật thể loại thành công!', 'success');
            } else {
                // Call API to create category
                // eslint-disable-next-line no-unused-vars
                const newCategory = await createCategory({
                    name: categoryData.name,
                    description: categoryData.description || '',
                    isActive: categoryData.is_active !== false,
                    iconImage: categoryData.iconFile || null
                });

                // Reload categories from API to get updated list
                // Reload current page or go to first page if new category might be on different page
                await loadCategories(1);
                await loadCategoriesForStats(); // Reload stats

                // Show success message
                showToast('Tạo thể loại thành công!', 'success');
            }
            setIsModalOpen(false);
        } catch (error) {
            console.error('Error saving category:', error);
            // Handle validation errors from frontend (thrown by createCategory)
            // or API errors from backend
            const errorMessage = error.message
                || error.response?.data?.message
                || error.response?.data?.title
                || 'Có lỗi xảy ra khi lưu thể loại';
            alert(`Lỗi: ${errorMessage}`);
            // Log full error for debugging
            if (error.response?.data) {
                console.error('Full error response:', error.response.data);
            }
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

    const handleViewCategory = (category) => {
        setViewingCategory(category);
        setIsModalOpen(true);
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
                                {allCategoriesForStats.length}
                            </p>
                        </div>
                        <div style={{ width: '48px', height: '48px', backgroundColor: 'rgba(19, 236, 91, 0.1)', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                            <span style={{ fontSize: '1.5rem' }}>📚</span>
                        </div>
                    </div>
                </div>

                <div style={{ backgroundColor: '#ffffff', borderRadius: '0.75rem', padding: '1.25rem', border: '1px solid #e2e8f0' }}>
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                        <div>
                            <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>Đang hoạt động</p>
                            <p style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#16a34a', margin: '0.25rem 0 0 0' }}>
                                {allCategoriesForStats.filter(c => c.isActive !== false).length}
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
                                {allCategoriesForStats.filter(c => c.isActive === false).length}
                            </p>
                        </div>
                        <div style={{ width: '48px', height: '48px', backgroundColor: 'rgba(220, 38, 38, 0.1)', borderRadius: '0.5rem', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                            <EyeOff style={{ width: '24px', height: '24px', color: '#dc2626' }} />
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
                    </div>
                </div>

                {/* Loading State */}
                {loading && (
                    <div style={{ padding: '3rem', textAlign: 'center' }}>
                        <Loader2 style={{ width: '48px', height: '48px', color: '#13ec5b', margin: '0 auto', animation: 'spin 1s linear infinite' }} />
                        <p style={{ color: '#64748b', fontSize: '0.875rem', marginTop: '1rem' }}>
                            Đang tải danh sách thể loại...
                        </p>
                        <style>{`
                            @keyframes spin {
                                from { transform: rotate(0deg); }
                                to { transform: rotate(360deg); }
                            }
                        `}</style>
                    </div>
                )}

                {/* Error State */}
                {error && !loading && (
                    <div style={{ padding: '3rem', textAlign: 'center' }}>
                        <p style={{ color: '#dc2626', fontSize: '0.875rem', marginBottom: '1rem' }}>
                            {error}
                        </p>
                        <button
                            onClick={loadCategories}
                            style={{
                                padding: '0.625rem 1rem',
                                backgroundColor: '#13ec5b',
                                color: '#ffffff',
                                fontSize: '0.875rem',
                                fontWeight: 'bold',
                                border: 'none',
                                borderRadius: '0.5rem',
                                cursor: 'pointer'
                            }}
                        >
                            Thử lại
                        </button>
                    </div>
                )}

                {/* Table */}
                {!loading && !error && (
                    <div style={{ overflowX: 'auto' }}>
                        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                            <thead>
                                <tr style={{ borderBottom: '1px solid #e2e8f0' }}>
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
                                            <div style={{ position: 'relative', width: '40px', height: '40px' }}>
                                                {category.iconUrl ? (
                                                    <img
                                                        src={getIconUrl(category.iconUrl)}
                                                        alt={category.name}
                                                        style={{
                                                            width: '40px',
                                                            height: '40px',
                                                            objectFit: 'cover',
                                                            borderRadius: '0.5rem',
                                                            border: '1px solid #e2e8f0',
                                                            position: 'absolute',
                                                            top: 0,
                                                            left: 0
                                                        }}
                                                        onError={(e) => {
                                                            e.target.style.display = 'none';
                                                        }}
                                                    />
                                                ) : null}
                                                <div
                                                    style={{
                                                        width: '40px',
                                                        height: '40px',
                                                        backgroundColor: '#e2e8f0',
                                                        borderRadius: '0.5rem',
                                                        border: '1px solid #e2e8f0',
                                                        display: 'flex',
                                                        alignItems: 'center',
                                                        justifyContent: 'center',
                                                        fontSize: '8px',
                                                        color: '#94a3b8',
                                                        textAlign: 'center',
                                                        position: category.iconUrl ? 'absolute' : 'relative',
                                                        top: category.iconUrl ? 0 : undefined,
                                                        left: category.iconUrl ? 0 : undefined,
                                                        zIndex: category.iconUrl ? -1 : 0
                                                    }}
                                                >
                                                    No Image
                                                </div>
                                            </div>
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
                                                    backgroundColor: category.isActive !== false ? 'rgba(22, 163, 74, 0.1)' : 'rgba(220, 38, 38, 0.1)',
                                                    color: category.isActive !== false ? '#15803d' : '#b91c1c'
                                                }}
                                                onMouseEnter={(e) => {
                                                    if (category.isActive !== false) {
                                                        e.currentTarget.style.backgroundColor = 'rgba(22, 163, 74, 0.2)';
                                                    } else {
                                                        e.currentTarget.style.backgroundColor = 'rgba(220, 38, 38, 0.2)';
                                                    }
                                                }}
                                                onMouseLeave={(e) => {
                                                    if (category.isActive !== false) {
                                                        e.currentTarget.style.backgroundColor = 'rgba(22, 163, 74, 0.1)';
                                                    } else {
                                                        e.currentTarget.style.backgroundColor = 'rgba(220, 38, 38, 0.1)';
                                                    }
                                                }}
                                            >
                                                {category.isActive !== false ? (
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
                                                {category.createdAt ? formatDate(category.createdAt) : '-'}
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
                                                <button
                                                    onClick={() => handleViewCategory(category)}
                                                    style={{
                                                        padding: '0.5rem',
                                                        border: 'none',
                                                        background: 'transparent',
                                                        borderRadius: '0.5rem',
                                                        cursor: 'pointer',
                                                        color: '#64748b',
                                                        transition: 'background-color 0.2s'
                                                    }}
                                                    onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'rgba(100, 116, 139, 0.1)'}
                                                    onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                                                    title="Xem chi tiết"
                                                >
                                                    <Info style={{ width: '16px', height: '16px' }} />
                                                </button>
                                            </div>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}

                {/* Empty State */}
                {!loading && !error && filteredCategories.length === 0 && (
                    <div style={{ padding: '3rem', textAlign: 'center' }}>
                        <p style={{ color: '#94a3b8', fontSize: '0.875rem' }}>
                            Không tìm thấy thể loại nào
                        </p>
                    </div>
                )}

                {/* Pagination */}
                {!loading && !error && totalPages > 1 && (
                    <Pagination
                        currentPage={currentPage}
                        totalPages={totalPages}
                        totalItems={totalCount}
                        itemsPerPage={pageSize}
                        onPageChange={handlePageChange}
                        itemLabel="thể loại"
                    />
                )}
            </div>

            {/* Category Modal */}
            <CategoryModal
                isOpen={isModalOpen}
                onClose={() => {
                    setIsModalOpen(false);
                    setViewingCategory(null);
                }}
                onSave={handleSaveCategory}
                category={viewingCategory || editingCategory}
                isViewOnly={!!viewingCategory}
            />

            {/* Toast Container */}
            <ToastContainer />
        </div>
    );
}