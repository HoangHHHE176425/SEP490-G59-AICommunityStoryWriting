import { Book, Eye, Heart, Star, Edit, Trash2, MessageSquare, Plus } from 'lucide-react';

export function StoryList({ stories, onCreateStory, onEditStory, onDeleteStory, onViewChapters, onViewComments }) {
    return (
        <div style={{ maxWidth: '1200px' }}>
            {/* Header */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    <div style={{ width: '20px', height: '20px', color: '#6b7280' }}>📚</div>
                    <h2 style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#333333', margin: 0 }}>
                        Truyện của tôi
                    </h2>
                </div>
                <button
                    onClick={onCreateStory}
                    className="flex items-center gap-2 px-6 py-2.5 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all"
                >
                    <Plus style={{ width: '16px', height: '16px' }} />
                    THÊM TRUYỆN MỚI
                </button>
            </div>

            {/* Stories List */}
            {stories.length === 0 ? (
                <div style={{
                    backgroundColor: '#ffffff',
                    borderRadius: '8px',
                    padding: '3rem',
                    textAlign: 'center',
                    border: '1px solid #e0e0e0'
                }}>
                    <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>📖</div>
                    <h3 style={{ fontSize: '1.125rem', color: '#6b7280', marginBottom: '0.5rem' }}>
                        Chưa có truyện nào
                    </h3>
                    <p style={{ fontSize: '0.875rem', color: '#9ca3af', margin: 0 }}>
                        Bắt đầu sáng tác truyện đầu tiên của bạn.
                    </p>
                </div>
            ) : (
                <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                    {stories.map((story) => (
                        <div
                            key={story.id}
                            style={{
                                backgroundColor: '#ffffff',
                                borderRadius: '8px',
                                padding: '1.25rem',
                                border: '1px solid #e0e0e0',
                                display: 'flex',
                                gap: '1.25rem'
                            }}
                        >
                            {/* Cover */}
                            <img
                                src={story.cover}
                                alt={story.title}
                                style={{
                                    width: '80px',
                                    height: '107px',
                                    objectFit: 'cover',
                                    borderRadius: '4px',
                                    flexShrink: 0
                                }}
                            />

                            {/* Info */}
                            <div style={{ flex: 1, minWidth: 0 }}>
                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '0.75rem' }}>
                                    <div style={{ flex: 1, minWidth: 0 }}>
                                        <h3 style={{
                                            fontSize: '1rem',
                                            fontWeight: 'bold',
                                            color: '#333333',
                                            margin: '0 0 0.5rem 0',
                                            overflow: 'hidden',
                                            textOverflow: 'ellipsis',
                                            whiteSpace: 'nowrap'
                                        }}>
                                            {story.title}
                                        </h3>
                                        <div style={{ fontSize: '0.75rem', color: '#9ca3af' }}>
                                            {story.lastUpdate}
                                        </div>
                                    </div>
                                    <div style={{
                                        padding: '0.25rem 0.75rem',
                                        backgroundColor: story.status === 'published' ? '#d1fae5' : '#fef3c7',
                                        borderRadius: '4px',
                                        fontSize: '0.75rem',
                                        color: story.status === 'published' ? '#065f46' : '#92400e',
                                        marginLeft: '1rem',
                                        flexShrink: 0
                                    }}>
                                        {story.publishStatus}
                                    </div>
                                </div>

                                {/* Stats */}
                                <div style={{
                                    display: 'grid',
                                    gridTemplateColumns: 'repeat(4, 1fr)',
                                    gap: '1rem',
                                    padding: '0.75rem 0',
                                    borderTop: '1px solid #f3f4f6',
                                    borderBottom: '1px solid #f3f4f6',
                                    marginBottom: '1rem'
                                }}>
                                    <div>
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', marginBottom: '0.25rem' }}>
                                            <Book style={{ width: '14px', height: '14px', color: '#6b7280' }} />
                                            <span style={{ fontSize: '0.75rem', color: '#6b7280' }}>Chương</span>
                                        </div>
                                        <div style={{ fontSize: '1rem', fontWeight: 'bold', color: '#333333' }}>
                                            {story.chapters}
                                        </div>
                                    </div>

                                    <div>
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', marginBottom: '0.25rem' }}>
                                            <Eye style={{ width: '14px', height: '14px', color: '#6b7280' }} />
                                            <span style={{ fontSize: '0.75rem', color: '#6b7280' }}>Lượt đọc</span>
                                        </div>
                                        <div style={{ fontSize: '1rem', fontWeight: 'bold', color: '#333333' }}>
                                            {story.totalViews.toLocaleString()}
                                        </div>
                                    </div>

                                    <div>
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', marginBottom: '0.25rem' }}>
                                            <Heart style={{ width: '14px', height: '14px', color: '#6b7280' }} />
                                            <span style={{ fontSize: '0.75rem', color: '#6b7280' }}>Theo dõi</span>
                                        </div>
                                        <div style={{ fontSize: '1rem', fontWeight: 'bold', color: '#333333' }}>
                                            {story.follows}
                                        </div>
                                    </div>

                                    <div>
                                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', marginBottom: '0.25rem' }}>
                                            <Star style={{ width: '14px', height: '14px', color: '#6b7280' }} />
                                            <span style={{ fontSize: '0.75rem', color: '#6b7280' }}>Đề cử</span>
                                        </div>
                                        <div style={{ fontSize: '1rem', fontWeight: 'bold', color: '#333333' }}>
                                            0
                                        </div>
                                    </div>
                                </div>

                                {/* Status */}
                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                                    <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                                        Trạng thái xuất bản
                                    </div>
                                    <div style={{
                                        padding: '0.25rem 0.75rem',
                                        backgroundColor: story.status === 'draft' ? '#fef3c7' : '#d1fae5',
                                        borderRadius: '4px',
                                        fontSize: '0.75rem',
                                        color: story.status === 'draft' ? '#92400e' : '#065f46'
                                    }}>
                                        {story.status === 'draft' ? 'Lưu tạm' : 'Xuất bản'}
                                    </div>
                                    {story.status === 'draft' && (
                                        <div style={{ fontSize: '0.75rem', color: '#ef4444' }}>
                                            Cần thêm 1 chương để có thể xuất bản
                                        </div>
                                    )}
                                </div>
                            </div>

                            {/* Action Buttons */}
                            <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem', flexShrink: 0 }}>
                                <button
                                    onClick={() => onViewComments(story)}
                                    className="flex items-center justify-center gap-1.5 px-4 py-2 bg-slate-100 text-slate-700 text-xs font-semibold rounded-full hover:bg-slate-200 transition-all whitespace-nowrap"
                                >
                                    <MessageSquare style={{ width: '14px', height: '14px' }} />
                                    Bình luận
                                </button>
                                <button
                                    onClick={() => onEditStory(story)}
                                    className="flex items-center justify-center gap-1.5 px-4 py-2 bg-primary/10 text-primary text-xs font-semibold rounded-full hover:bg-primary/20 transition-all whitespace-nowrap"
                                >
                                    <Edit style={{ width: '14px', height: '14px' }} />
                                    Chỉnh sửa
                                </button>
                                <button
                                    onClick={() => onDeleteStory(story.id)}
                                    className="flex items-center justify-center gap-1.5 px-4 py-2 bg-red-50 text-red-600 text-xs font-semibold rounded-full hover:bg-red-100 transition-all whitespace-nowrap"
                                >
                                    <Trash2 style={{ width: '14px', height: '14px' }} />
                                    Xóa
                                </button>
                                <button
                                    onClick={() => onViewChapters(story)}
                                    className="flex items-center justify-center gap-1.5 px-4 py-2 bg-primary text-white text-xs font-bold rounded-full hover:bg-primary/90 transition-all whitespace-nowrap"
                                >
                                    <Plus style={{ width: '14px', height: '14px' }} />
                                    Thêm chương
                                </button>
                                <button
                                    onClick={() => onViewChapters(story)}
                                    className="flex items-center justify-center gap-1.5 px-4 py-2 bg-slate-100 text-slate-700 text-xs font-semibold rounded-full hover:bg-slate-200 transition-all whitespace-nowrap"
                                >
                                    <Book style={{ width: '14px', height: '14px' }} />
                                    DS chương
                                </button>
                            </div>
                        </div>
                    ))}
                </div>
            )}
        </div>
    );
}
