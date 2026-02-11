import { useState, useEffect, useCallback } from 'react';
import { Plus, Edit, Eye, Heart, MessageSquare, Star, ChevronRight, Book, User, LogOut } from 'lucide-react';
import { StoryEditor } from './StoryEditor';
import { StoryInfoEditor } from './StoryInfoEditor';
import { ChapterListManager } from '../author/ChapterListManager';
import { StoryCommentsViewer } from './StoryCommentsViewer';
import { ChapterEditorPage } from '../author/ChapterEditorPage';
import { Footer } from '../../components/homepage/Footer';
import { Header } from '../../components/homepage/Header';
import { createStory, updateStory, getStories } from '../../api/story/storyApi';
import { createChapter } from '../../api/chapter/chapterApi';
import { resolveBackendUrl } from '../../utils/resolveBackendUrl';
import { useAuth } from '../../contexts/AuthContext';

function mapStoryFromApi(item) {
    const status = item.status || item.Status || '';
    const publishStatusMap = {
        DRAFT: 'Lưu tạm',
        PENDING_REVIEW: 'Chờ duyệt',
        PUBLISHED: 'Đang ra',
        COMPLETED: 'Hoàn thành',
        HIATUS: 'Tạm dừng',
    };
    const publishStatus = publishStatusMap[status] || status;
    const categories = item.categoryNames || item.CategoryNames
        ? String(item.categoryNames || item.CategoryNames).split(',').map(s => s.trim()).filter(Boolean)
        : [];
    const updatedAt = item.updatedAt || item.UpdatedAt;
    const lastUpdate = updatedAt
        ? new Date(updatedAt).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
        : '';
    const coverPath = item.coverImage ?? item.CoverImage;
    return {
        id: item.id ?? item.Id,
        title: item.title ?? item.Title,
        cover: coverPath ? resolveBackendUrl(coverPath) : '',
        categories,
        status: status.toLowerCase(),
        chapters: item.totalChapters ?? item.TotalChapters ?? 0,
        totalViews: Number(item.totalViews ?? item.TotalViews ?? 0),
        follows: Number(item.totalFavorites ?? item.TotalFavorites ?? 0),
        rating: item.avgRating ?? item.AvgRating ?? 0,
        lastUpdate: lastUpdate || 'Chưa cập nhật',
        publishStatus,
    };
}

export function AuthorStoryManagement({ onBack }) {
    const { user } = useAuth();
    const [activeView, setActiveView] = useState('stories');
    const [activeMenu, setActiveMenu] = useState('stories');
    const [currentStory, setCurrentStory] = useState(null);
    const [currentChapter, setCurrentChapter] = useState(null);
    const [stories, setStories] = useState([]);
    const [storiesLoading, setStoriesLoading] = useState(true);
    const [storiesError, setStoriesError] = useState(null);

    const authorId = user?.id ?? user?.Id;

    const loadStories = useCallback(() => {
        if (!authorId) {
            setStories([]);
            setStoriesLoading(false);
            return;
        }
        setStoriesLoading(true);
        setStoriesError(null);
        getStories({ authorId, page: 1, pageSize: 100 })
            .then((res) => {
                const items = res?.items ?? res?.Items ?? [];
                setStories(items.map(mapStoryFromApi));
            })
            .catch((err) => {
                setStoriesError(err?.message ?? 'Không tải được danh sách truyện');
                setStories([]);
            })
            .finally(() => setStoriesLoading(false));
    }, [authorId]);

    useEffect(() => {
        queueMicrotask(() => loadStories());
    }, [loadStories]);

    // Mock comments data
    const mockComments = [
        {
            id: 1,
            userName: 'Nguyễn Văn A',
            userAvatar: 'https://api.dicebear.com/7.x/avataaars/svg?seed=user1',
            time: '2 giờ trước',
            content: 'Truyện hay quá! Mong tác giả cập nhật thêm nhiều chương.',
            likes: 15
        },
        {
            id: 2,
            userName: 'Trần Thị B',
            userAvatar: 'https://api.dicebear.com/7.x/avataaars/svg?seed=user2',
            time: '5 giờ trước',
            content: 'Nhân vật chính rất hay, tính cách rõ ràng.',
            likes: 8
        },
    ];

    const userStats = {
        published: stories.filter(s => s.status === 'published').length,
        totalChapters: stories.reduce((acc, s) => acc + s.chapters, 0),
        followers: 0,
        recommendations: 0,
    };

    const handleCreateStory = () => {
        setCurrentStory(null);
        setActiveView('createStory');
    };

    const handleEditStory = (story) => {
        setCurrentStory(story);
        setActiveView('editInfo');
    };

    const handleViewChapters = (story) => {
        setCurrentStory(story);
        setActiveView('chapterList');
    };

    const handleViewComments = (story) => {
        setCurrentStory(story);
        setActiveView('comments');
    };

    const handleAddChapter = (story) => {
        setCurrentStory(story);
        setCurrentChapter(null);
        setActiveView('addChapter');
    };

    const handleEditChapter = (chapter) => {
        setCurrentChapter(chapter);
        setActiveView('editChapter');
    };

    const handleSaveChapter = (chapterData) => {
        // TODO: Save chapter to backend
        console.log('Saving chapter:', chapterData);
        setActiveView('chapterList');
        setCurrentChapter(null);
    };

    const handleDeleteStory = (storyId) => {
        if (window.confirm('Bạn có chắc chắn muốn xóa truyện này?')) {
            setStories(stories.filter(s => s.id !== storyId));
        }
    };

    const handleSaveStory = async (storyData) => {
        if (currentStory) {
            setStories(stories.map(s => s.id === currentStory.id ? { ...s, ...storyData } : s));
            setActiveView('stories');
            setCurrentStory(null);
            return;
        }

        const payload = {
            title: storyData.title,
            summary: storyData.note || '',
            categoryIds: storyData.categoryIds || [],
            ageRating: storyData.ageRating,
            storyProgressStatus: storyData.storyProgressStatus || storyData.status,
            coverImage: storyData.cover,
        };
        const created = await createStory(payload);
        const storyId = created?.id ?? created?.Id;

        const chaptersData = storyData.chaptersData || [];
        for (let i = 0; i < chaptersData.length; i++) {
            const ch = chaptersData[i];
            await createChapter({
                storyId,
                title: ch.title,
                content: ch.content || '',
                orderIndex: i,
                status: 'DRAFT',
            });
        }

        if (!storyData.isDraft) {
            await updateStory(storyId, {
                title: storyData.title,
                summary: storyData.note || '',
                categoryIds: storyData.categoryIds || [],
                status: 'PENDING_REVIEW',
                ageRating: storyData.ageRating,
                storyProgressStatus: storyData.storyProgressStatus,
                coverImage: storyData.cover,
            });
        }

        loadStories();
        if (storyData.isDraft) {
            setActiveView('stories');
            setCurrentStory(null);
        }
    };

    const handleSaveInfo = (infoData) => {
        setStories(stories.map(s => s.id === currentStory.id ? { ...s, ...infoData } : s));
        setActiveView('stories');
        setCurrentStory(null);
    };

    // Render different views
    if (activeView === 'createStory') {
        return (
            <StoryEditor
                story={null}
                onSave={handleSaveStory}
                onCancel={() => {
                    setActiveView('stories');
                    setCurrentStory(null);
                }}
            />
        );
    }

    if (activeView === 'editInfo') {
        return (
            <StoryInfoEditor
                story={currentStory}
                onSave={handleSaveInfo}
                onCancel={() => {
                    setActiveView('stories');
                    setCurrentStory(null);
                }}
            />
        );
    }

    if (activeView === 'chapterList') {
        return (
            <ChapterListManager
                story={currentStory}
                onBack={() => {
                    setActiveView('stories');
                    setCurrentStory(null);
                }}
                onAddChapter={() => handleAddChapter(currentStory)}
                onEditChapter={(chapter) => handleEditChapter(chapter)}
            />
        );
    }

    if (activeView === 'addChapter' || activeView === 'editChapter') {
        return (
            <ChapterEditorPage
                story={currentStory}
                chapter={currentChapter}
                onSave={handleSaveChapter}
                onCancel={() => {
                    setActiveView('chapterList');
                    setCurrentChapter(null);
                }}
            />
        );
    }

    if (activeView === 'comments') {
        return (
            <StoryCommentsViewer
                story={currentStory}
                comments={mockComments}
                onBack={() => {
                    setActiveView('stories');
                    setCurrentStory(null);
                }}
            />
        );
    }

    return (
        <div>
            <Header />
            <div style={{ minHeight: '100vh', backgroundColor: '#f5f5f5', display: 'flex' }}>
                {/* Sidebar */}
                <div style={{ width: '250px', backgroundColor: '#ffffff', borderRight: '1px solid #e0e0e0', padding: '2rem 0' }}>
                    <div style={{ padding: '0 1.5rem 1.5rem', borderBottom: '1px solid #e0e0e0' }}>
                        <h2 style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#333333', margin: '0 0 1rem 0' }}>
                            Quyền Đình
                        </h2>
                    </div>

                    <nav style={{ marginTop: '1rem' }}>
                        <button
                            onClick={() => {
                                setActiveMenu('profile');
                                setActiveView('profile');
                            }}
                            style={{
                                width: '100%',
                                padding: '0.75rem 1.5rem',
                                backgroundColor: activeMenu === 'profile' ? '#f5f5f5' : 'transparent',
                                border: 'none',
                                textAlign: 'left',
                                fontSize: '0.875rem',
                                color: '#333333',
                                cursor: 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.75rem',
                                transition: 'background-color 0.2s'
                            }}
                        >
                            <User style={{ width: '18px', height: '18px' }} />
                            Hồ sơ tác giả
                            <ChevronRight style={{ width: '16px', height: '16px', marginLeft: 'auto' }} />
                        </button>

                        <button
                            onClick={() => {
                                setActiveMenu('stories');
                                setActiveView('stories');
                            }}
                            style={{
                                width: '100%',
                                padding: '0.75rem 1.5rem',
                                backgroundColor: activeMenu === 'stories' ? '#f5f5f5' : 'transparent',
                                border: 'none',
                                textAlign: 'left',
                                fontSize: '0.875rem',
                                color: '#333333',
                                cursor: 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.75rem',
                                transition: 'background-color 0.2s'
                            }}
                        >
                            <Book style={{ width: '18px', height: '18px' }} />
                            Truyện của tôi
                            <ChevronRight style={{ width: '16px', height: '16px', marginLeft: 'auto' }} />
                        </button>

                        <button
                            onClick={onBack}
                            style={{
                                width: '100%',
                                padding: '0.75rem 1.5rem',
                                backgroundColor: 'transparent',
                                border: 'none',
                                textAlign: 'left',
                                fontSize: '0.875rem',
                                color: '#333333',
                                cursor: 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.75rem',
                                transition: 'background-color 0.2s'
                            }}
                        >
                            <LogOut style={{ width: '18px', height: '18px' }} />
                            Đăng xuất
                            <ChevronRight style={{ width: '16px', height: '16px', marginLeft: 'auto' }} />
                        </button>
                    </nav>
                </div>

                {/* Main Content */}
                <div style={{ flex: 1, padding: '2rem' }}>
                    {activeView === 'profile' ? (
                        <div style={{ maxWidth: '900px' }}>
                            {/* Thành tích */}
                            <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '1.5rem', marginBottom: '1.5rem', border: '1px solid #e0e0e0' }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1.5rem' }}>
                                    <div style={{ width: '20px', height: '20px', color: '#6b7280' }}>🌱</div>
                                    <h3 style={{ fontSize: '1rem', fontWeight: 'bold', color: '#333333', margin: 0 }}>Thành tích</h3>
                                </div>

                                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '1rem' }}>
                                    <div style={{ textAlign: 'center' }}>
                                        <div style={{ width: '48px', height: '48px', borderRadius: '50%', backgroundColor: '#d4fce3', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 0.75rem' }}>
                                            <Book style={{ width: '24px', height: '24px', color: '#13ec5b' }} />
                                        </div>
                                        <div style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#333333', marginBottom: '0.25rem' }}>
                                            {userStats.published}
                                        </div>
                                        <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                                            Truyện đã đăng
                                        </div>
                                    </div>

                                    <div style={{ textAlign: 'center' }}>
                                        <div style={{ width: '48px', height: '48px', borderRadius: '50%', backgroundColor: '#d4fce3', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 0.75rem' }}>
                                            <div style={{ fontSize: '1.25rem' }}>📄</div>
                                        </div>
                                        <div style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#333333', marginBottom: '0.25rem' }}>
                                            {userStats.totalChapters}
                                        </div>
                                        <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                                            Chương đã đăng
                                        </div>
                                    </div>

                                    <div style={{ textAlign: 'center' }}>
                                        <div style={{ width: '48px', height: '48px', borderRadius: '50%', backgroundColor: '#d4fce3', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 0.75rem' }}>
                                            <Heart style={{ width: '24px', height: '24px', color: '#13ec5b' }} />
                                        </div>
                                        <div style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#333333', marginBottom: '0.25rem' }}>
                                            {userStats.followers}
                                        </div>
                                        <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                                            Người theo dõi
                                        </div>
                                    </div>

                                    <div style={{ textAlign: 'center' }}>
                                        <div style={{ width: '48px', height: '48px', borderRadius: '50%', backgroundColor: '#d4fce3', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 0.75rem' }}>
                                            <Star style={{ width: '24px', height: '24px', color: '#13ec5b' }} />
                                        </div>
                                        <div style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#333333', marginBottom: '0.25rem' }}>
                                            {userStats.recommendations}
                                        </div>
                                        <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                                            Đề cử
                                        </div>
                                    </div>
                                </div>
                            </div>

                            {/* Thông tin cá nhân */}
                            <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '1.5rem', border: '1px solid #e0e0e0' }}>
                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
                                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                        <div style={{ width: '20px', height: '20px', color: '#6b7280' }}>👤</div>
                                        <h3 style={{ fontSize: '1rem', fontWeight: 'bold', color: '#333333', margin: 0 }}>Thông tin cá nhân</h3>
                                    </div>
                                    <button
                                        style={{
                                            padding: '0.5rem 1.25rem',
                                            backgroundColor: '#13ec5b',
                                            border: 'none',
                                            borderRadius: '4px',
                                            fontSize: '0.875rem',
                                            fontWeight: 600,
                                            color: '#ffffff',
                                            cursor: 'pointer'
                                        }}
                                    >
                                        CẬP NHẬT
                                    </button>
                                </div>

                                <div style={{ display: 'grid', gap: '1rem' }}>
                                    <div style={{ display: 'grid', gridTemplateColumns: '120px 1fr', gap: '1rem', alignItems: 'center' }}>
                                        <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>Tên hiển thị</div>
                                        <div style={{ fontSize: '0.875rem', color: '#333333', fontWeight: 500 }}>Quyền Đình</div>
                                    </div>
                                    <div style={{ display: 'grid', gridTemplateColumns: '120px 1fr', gap: '1rem', alignItems: 'center' }}>
                                        <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>Giới thiệu</div>
                                        <div style={{ fontSize: '0.875rem', color: '#333333' }}>Đang cập nhật</div>
                                    </div>
                                </div>
                            </div>
                        </div>
                    ) : (
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
                                    onClick={handleCreateStory}
                                    style={{
                                        display: 'flex',
                                        alignItems: 'center',
                                        gap: '0.5rem',
                                        padding: '0.625rem 1.25rem',
                                        backgroundColor: '#13ec5b',
                                        border: 'none',
                                        borderRadius: '9999px',
                                        fontSize: '0.875rem',
                                        fontWeight: 700,
                                        color: '#ffffff',
                                        cursor: 'pointer',
                                        transition: 'background-color 0.2s'
                                    }}
                                    onMouseEnter={(e) => {
                                        e.currentTarget.style.backgroundColor = '#10d452';
                                    }}
                                    onMouseLeave={(e) => {
                                        e.currentTarget.style.backgroundColor = '#13ec5b';
                                    }}
                                >
                                    <Plus style={{ width: '16px', height: '16px' }} />
                                    THÊM TRUYỆN MỚI
                                </button>
                            </div>

                            {/* Stories List */}
                            {storiesLoading ? (
                                <div style={{
                                    backgroundColor: '#ffffff',
                                    borderRadius: '8px',
                                    padding: '3rem',
                                    textAlign: 'center',
                                    border: '1px solid #e0e0e0'
                                }}>
                                    <p style={{ fontSize: '0.875rem', color: '#6b7280' }}>Đang tải danh sách truyện...</p>
                                </div>
                            ) : storiesError ? (
                                <div style={{
                                    backgroundColor: '#ffffff',
                                    borderRadius: '8px',
                                    padding: '3rem',
                                    textAlign: 'center',
                                    border: '1px solid #e0e0e0'
                                }}>
                                    <p style={{ fontSize: '0.875rem', color: '#dc2626', marginBottom: '1rem' }}>{storiesError}</p>
                                    <button
                                        onClick={() => loadStories()}
                                        style={{ padding: '0.5rem 1rem', fontSize: '0.875rem', cursor: 'pointer' }}
                                    >
                                        Thử lại
                                    </button>
                                </div>
                            ) : stories.length === 0 ? (
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
                                    <p style={{ fontSize: '0.875rem', color: '#9ca3af', marginBottom: '1.5rem' }}>
                                        Bắt đầu sáng tác truyện đầu tiên của bạn
                                    </p>
                                    <button
                                        onClick={handleCreateStory}
                                        style={{
                                            padding: '0.75rem 1.5rem',
                                            backgroundColor: '#13ec5b',
                                            border: 'none',
                                            borderRadius: '9999px',
                                            fontSize: '0.875rem',
                                            fontWeight: 700,
                                            color: '#ffffff',
                                            cursor: 'pointer'
                                        }}
                                    >
                                        Tạo truyện mới
                                    </button>
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
                                                src={story.cover || 'https://via.placeholder.com/80x107?text=No+Cover'}
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
                                                        backgroundColor: (story.status === 'published' || story.status === 'pending_review') ? '#d1fae5' : '#fef3c7',
                                                        borderRadius: '4px',
                                                        fontSize: '0.75rem',
                                                        color: (story.status === 'published' || story.status === 'pending_review') ? '#065f46' : '#92400e',
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
                                                        backgroundColor: (story.status === 'draft' || story.status === 'pending_review') ? '#fef3c7' : '#d1fae5',
                                                        borderRadius: '4px',
                                                        fontSize: '0.75rem',
                                                        color: (story.status === 'draft' || story.status === 'pending_review') ? '#92400e' : '#065f46'
                                                    }}>
                                                        {story.status === 'draft' ? 'Lưu tạm' : story.status === 'pending_review' ? 'Chờ duyệt' : 'Xuất bản'}
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
                                                    onClick={() => handleViewComments(story)}
                                                    style={{
                                                        padding: '0.5rem 1rem',
                                                        backgroundColor: 'transparent',
                                                        border: '1px solid #e0e0e0',
                                                        borderRadius: '4px',
                                                        fontSize: '0.75rem',
                                                        color: '#333333',
                                                        cursor: 'pointer',
                                                        whiteSpace: 'nowrap'
                                                    }}
                                                >
                                                    Danh sách bình luận
                                                </button>
                                                <button
                                                    onClick={() => handleEditStory(story)}
                                                    style={{
                                                        padding: '0.5rem 1rem',
                                                        backgroundColor: 'transparent',
                                                        border: '1px solid #e0e0e0',
                                                        borderRadius: '4px',
                                                        fontSize: '0.75rem',
                                                        color: '#333333',
                                                        cursor: 'pointer',
                                                        whiteSpace: 'nowrap'
                                                    }}
                                                >
                                                    Chỉnh sửa
                                                </button>
                                                <button
                                                    onClick={() => handleDeleteStory(story.id)}
                                                    style={{
                                                        padding: '0.5rem 1rem',
                                                        backgroundColor: 'transparent',
                                                        border: '1px solid #e0e0e0',
                                                        borderRadius: '4px',
                                                        fontSize: '0.75rem',
                                                        color: '#333333',
                                                        cursor: 'pointer',
                                                        whiteSpace: 'nowrap'
                                                    }}
                                                >
                                                    Xóa
                                                </button>
                                                <button
                                                    onClick={() => handleViewChapters(story)}
                                                    style={{
                                                        padding: '0.5rem 1rem',
                                                        backgroundColor: '#13ec5b',
                                                        border: 'none',
                                                        borderRadius: '4px',
                                                        fontSize: '0.75rem',
                                                        fontWeight: 600,
                                                        color: '#ffffff',
                                                        cursor: 'pointer',
                                                        whiteSpace: 'nowrap'
                                                    }}
                                                >
                                                    + Thêm chương
                                                </button>
                                                <button
                                                    onClick={() => handleViewChapters(story)}
                                                    style={{
                                                        padding: '0.5rem 1rem',
                                                        backgroundColor: 'transparent',
                                                        border: '1px solid #e0e0e0',
                                                        borderRadius: '4px',
                                                        fontSize: '0.75rem',
                                                        color: '#333333',
                                                        cursor: 'pointer',
                                                        whiteSpace: 'nowrap'
                                                    }}
                                                >
                                                    Danh sách chương
                                                </button>
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            )}
                        </div>
                    )}
                </div>
            </div>
            <Footer />
        </div>
    );
}