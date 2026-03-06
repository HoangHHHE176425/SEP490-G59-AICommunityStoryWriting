import { useState } from 'react';
import { Plus, Eye, MessageSquare, Heart, Book, Trash2, Edit } from 'lucide-react';

export function ChapterList({ story, onBack, onAddChapter, onEditChapter }) {
    const [chapters, setChapters] = useState([
        {
            id: 1,
            number: 1,
            title: 'Khởi đầu của hành trình',
            content: 'Nội dung chương 1...',
            status: 'published',
            accessType: 'public',
            price: 0,
            views: 2001,
            comments: 0,
            likes: 0,
            updatedAt: '15:13:31 25/01/2026'
        },
        {
            id: 2,
            number: 2,
            title: 'Gặp gỡ người bạn đồng hành',
            content: 'Nội dung chương 2...',
            status: 'published',
            accessType: 'paid',
            price: 50,
            views: 1850,
            comments: 5,
            likes: 12,
            updatedAt: '16:20:15 25/01/2026'
        },
        {
            id: 3,
            number: 3,
            title: 'Thử thách đầu tiên',
            content: 'Nội dung chương 3...',
            status: 'draft',
            accessType: 'public',
            price: 0,
            views: 0,
            comments: 0,
            likes: 0,
            updatedAt: '18:45:00 25/01/2026'
        },
    ]);

    const handleDeleteChapter = (chapterId) => {
        if (window.confirm('Bạn có chắc chắn muốn xóa chương này?')) {
            setChapters(chapters.filter(ch => ch.id !== chapterId));
        }
    };

    const publishedChapters = chapters.filter(ch => ch.status === 'published');
    const draftChapters = chapters.filter(ch => ch.status === 'draft');

    return (
        <div style={{ minHeight: '100vh', backgroundColor: '#f5f5f5' }}>
            {/* Header */}
            <div style={{ backgroundColor: '#ffffff', borderBottom: '1px solid #e0e0e0', padding: '1.5rem 0' }}>
                <div style={{ maxWidth: '1200px', margin: '0 auto', padding: '0 2rem' }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                        <div>
                            <h2 style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#333333', margin: '0 0 0.5rem 0' }}>
                                Quản lý chương
                            </h2>
                            <p style={{ fontSize: '0.875rem', color: '#6b7280', margin: 0 }}>
                                {story?.title}
                            </p>
                        </div>
                        <div style={{ display: 'flex', gap: '0.75rem' }}>
                            <button
                                onClick={onBack}
                                className="px-6 py-2.5 bg-slate-100 text-slate-900 text-sm font-bold rounded-full hover:bg-slate-200 transition-all"
                            >
                                Quay lại
                            </button>
                            <button
                                onClick={onAddChapter}
                                className="flex items-center gap-2 px-6 py-2.5 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all"
                            >
                                <Plus style={{ width: '16px', height: '16px' }} />
                                Thêm chương mới
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            {/* Content */}
            <div style={{ maxWidth: '1200px', margin: '0 auto', padding: '2rem' }}>
                {/* Stats Cards */}
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '1rem', marginBottom: '2rem' }}>
                    <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '1.25rem', border: '1px solid #e0e0e0' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '0.5rem' }}>
                            <div style={{ width: '40px', height: '40px', borderRadius: '50%', backgroundColor: '#d4fce3', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                <Book style={{ width: '20px', height: '20px', color: '#13ec5b' }} />
                            </div>
                            <div>
                                <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>Tổng chương</div>
                                <div style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#333333' }}>{chapters.length}</div>
                            </div>
                        </div>
                    </div>

                    <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '1.25rem', border: '1px solid #e0e0e0' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '0.5rem' }}>
                            <div style={{ width: '40px', height: '40px', borderRadius: '50%', backgroundColor: '#dbeafe', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                <Eye style={{ width: '20px', height: '20px', color: '#3b82f6' }} />
                            </div>
                            <div>
                                <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>Lượt xem</div>
                                <div style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#333333' }}>
                                    {chapters.reduce((sum, ch) => sum + ch.views, 0).toLocaleString()}
                                </div>
                            </div>
                        </div>
                    </div>

                    <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '1.25rem', border: '1px solid #e0e0e0' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '0.5rem' }}>
                            <div style={{ width: '40px', height: '40px', borderRadius: '50%', backgroundColor: '#fef3c7', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                <MessageSquare style={{ width: '20px', height: '20px', color: '#f59e0b' }} />
                            </div>
                            <div>
                                <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>Bình luận</div>
                                <div style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#333333' }}>
                                    {chapters.reduce((sum, ch) => sum + ch.comments, 0)}
                                </div>
                            </div>
                        </div>
                    </div>

                    <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '1.25rem', border: '1px solid #e0e0e0' }}>
                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '0.5rem' }}>
                            <div style={{ width: '40px', height: '40px', borderRadius: '50%', backgroundColor: '#fce7f3', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                <Heart style={{ width: '20px', height: '20px', color: '#ec4899' }} />
                            </div>
                            <div>
                                <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>Lượt thích</div>
                                <div style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#333333' }}>
                                    {chapters.reduce((sum, ch) => sum + ch.likes, 0)}
                                </div>
                            </div>
                        </div>
                    </div>
                </div>

                {/* Empty State */}
                {chapters.length === 0 && (
                    <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '3rem', textAlign: 'center', border: '1px solid #e0e0e0' }}>
                        <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>📄</div>
                        <h3 style={{ fontSize: '1.125rem', color: '#6b7280', marginBottom: '0.5rem' }}>
                            Chưa có chương nào
                        </h3>
                        <p style={{ fontSize: '0.875rem', color: '#9ca3af', marginBottom: '1.5rem' }}>
                            Thêm chương đầu tiên cho truyện của bạn
                        </p>
                        <button
                            onClick={onAddChapter}
                            className="px-6 py-2.5 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all"
                        >
                            Thêm chương mới
                        </button>
                    </div>
                )}

                {/* Chapter Table */}
                {chapters.length > 0 && (
                    <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', border: '1px solid #e0e0e0', overflow: 'hidden' }}>
                        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                            <thead>
                                <tr style={{ backgroundColor: '#f9fafb', borderBottom: '1px solid #e5e7eb' }}>
                                    <th style={{ padding: '1rem', textAlign: 'left', fontSize: '0.75rem', fontWeight: 600, color: '#6b7280', textTransform: 'uppercase' }}>
                                        Số
                                    </th>
                                    <th style={{ padding: '1rem', textAlign: 'left', fontSize: '0.75rem', fontWeight: 600, color: '#6b7280', textTransform: 'uppercase' }}>
                                        Tên chương
                                    </th>
                                    <th style={{ padding: '1rem', textAlign: 'left', fontSize: '0.75rem', fontWeight: 600, color: '#6b7280', textTransform: 'uppercase' }}>
                                        Trạng thái
                                    </th>
                                    <th style={{ padding: '1rem', textAlign: 'left', fontSize: '0.75rem', fontWeight: 600, color: '#6b7280', textTransform: 'uppercase' }}>
                                        Loại
                                    </th>
                                    <th style={{ padding: '1rem', textAlign: 'center', fontSize: '0.75rem', fontWeight: 600, color: '#6b7280', textTransform: 'uppercase' }}>
                                        <Eye style={{ width: '14px', height: '14px', margin: '0 auto' }} />
                                    </th>
                                    <th style={{ padding: '1rem', textAlign: 'center', fontSize: '0.75rem', fontWeight: 600, color: '#6b7280', textTransform: 'uppercase' }}>
                                        <MessageSquare style={{ width: '14px', height: '14px', margin: '0 auto' }} />
                                    </th>
                                    <th style={{ padding: '1rem', textAlign: 'center', fontSize: '0.75rem', fontWeight: 600, color: '#6b7280', textTransform: 'uppercase' }}>
                                        <Heart style={{ width: '14px', height: '14px', margin: '0 auto' }} />
                                    </th>
                                    <th style={{ padding: '1rem', textAlign: 'left', fontSize: '0.75rem', fontWeight: 600, color: '#6b7280', textTransform: 'uppercase' }}>
                                        Cập nhật
                                    </th>
                                    <th style={{ padding: '1rem', textAlign: 'right', fontSize: '0.75rem', fontWeight: 600, color: '#6b7280', textTransform: 'uppercase' }}>
                                        Thao tác
                                    </th>
                                </tr>
                            </thead>
                            <tbody>
                                {chapters.map((chapter, index) => (
                                    <tr
                                        key={chapter.id}
                                        style={{
                                            borderBottom: index < chapters.length - 1 ? '1px solid #f3f4f6' : 'none',
                                            transition: 'background-color 0.2s'
                                        }}
                                        onMouseEnter={(e) => {
                                            e.currentTarget.style.backgroundColor = '#f9fafb';
                                        }}
                                        onMouseLeave={(e) => {
                                            e.currentTarget.style.backgroundColor = 'transparent';
                                        }}
                                    >
                                        <td style={{ padding: '1rem', fontSize: '0.875rem', fontWeight: 600, color: '#333333' }}>
                                            {chapter.number}
                                        </td>
                                        <td style={{ padding: '1rem', fontSize: '0.875rem', color: '#333333', maxWidth: '300px' }}>
                                            <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                                {chapter.title}
                                            </div>
                                        </td>
                                        <td style={{ padding: '1rem' }}>
                                            <span style={{
                                                padding: '0.25rem 0.75rem',
                                                backgroundColor: chapter.status === 'published' ? '#d1fae5' : '#fef3c7',
                                                borderRadius: '9999px',
                                                fontSize: '0.75rem',
                                                fontWeight: 600,
                                                color: chapter.status === 'published' ? '#065f46' : '#92400e'
                                            }}>
                                                {chapter.status === 'published' ? 'Đã xuất bản' : 'Nháp'}
                                            </span>
                                        </td>
                                        <td style={{ padding: '1rem' }}>
                                            <span style={{
                                                padding: '0.25rem 0.75rem',
                                                backgroundColor: chapter.accessType === 'paid' ? '#fffbeb' : '#f0fdf4',
                                                borderRadius: '9999px',
                                                fontSize: '0.75rem',
                                                fontWeight: 600,
                                                color: chapter.accessType === 'paid' ? '#92400e' : '#166534'
                                            }}>
                                                {chapter.accessType === 'paid' ? `${chapter.price} xu` : 'Miễn phí'}
                                            </span>
                                        </td>
                                        <td style={{ padding: '1rem', fontSize: '0.875rem', color: '#6b7280', textAlign: 'center' }}>
                                            {chapter.views}
                                        </td>
                                        <td style={{ padding: '1rem', fontSize: '0.875rem', color: '#6b7280', textAlign: 'center' }}>
                                            {chapter.comments}
                                        </td>
                                        <td style={{ padding: '1rem', fontSize: '0.875rem', color: '#6b7280', textAlign: 'center' }}>
                                            {chapter.likes}
                                        </td>
                                        <td style={{ padding: '1rem', fontSize: '0.75rem', color: '#9ca3af' }}>
                                            {chapter.updatedAt}
                                        </td>
                                        <td style={{ padding: '1rem' }}>
                                            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
                                                <button
                                                    onClick={() => onEditChapter(chapter)}
                                                    className="px-4 py-1.5 bg-primary/10 text-primary text-xs font-semibold rounded-full hover:bg-primary/20 transition-all"
                                                >
                                                    Sửa
                                                </button>
                                                <button
                                                    onClick={() => handleDeleteChapter(chapter.id)}
                                                    className="px-4 py-1.5 bg-red-50 text-red-600 text-xs font-semibold rounded-full hover:bg-red-100 transition-all"
                                                >
                                                    Xóa
                                                </button>
                                            </div>
                                        </td>
                                    </tr>
                                ))}
                            </tbody>
                        </table>
                    </div>
                )}
            </div>
        </div>
    );
}
