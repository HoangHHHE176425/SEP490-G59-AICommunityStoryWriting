import { useState } from 'react';
import { Book, User, LogOut, ChevronRight, Heart, Star } from 'lucide-react';

// Stories components
import { StoryList } from '../../components/author/stories/StoryList';
import { StoryEditor } from '../../components/author/stories/StoryEditor';
import { StoryInfoEditor } from '../../components/author/stories/StoryInfoEditor';

// Chapters components
import { ChapterList } from '../../components/author/chapters/ChapterList';
import { ChapterEditor } from '../../components/author/chapters/ChapterEditor';

// Shared components
import { StoryCommentsViewer } from '../../components/author/shared/StoryCommentsViewer';

/**
 * AuthorDashboard - Component chính quản lý toàn bộ hệ thống tác giả
 * 
 * Views:
 * - profile: Hồ sơ tác giả
 * - stories: Danh sách truyện
 * - createStory: Tạo truyện mới
 * - editInfo: Chỉnh sửa thông tin truyện
 * - chapterList: Danh sách chương
 * - addChapter: Thêm chương mới
 * - editChapter: Chỉnh sửa chương
 * - comments: Xem bình luận
 */
export function AuthorDashboard({ onBack }) {
    const [activeView, setActiveView] = useState('stories');
    const [activeMenu, setActiveMenu] = useState('stories');
    const [currentStory, setCurrentStory] = useState(null);
    const [currentChapter, setCurrentChapter] = useState(null);

    const [stories, setStories] = useState([
        {
            id: 1,
            title: 'Tu Tiên Chi Lộ: Hành Trình Vạn Năm',
            cover: 'https://images.unsplash.com/photo-1589998059171-988d887df646?w=200&h=300&fit=crop',
            storyType: 'long',
            categories: ['Tiên hiệp', 'Huyền huyễn'],
            status: 'published',
            chapters: 450,
            totalViews: 5200000,
            follows: 8900,
            rating: 4.8,
            lastUpdate: 'Cập nhật 21:07 25/01/2026',
            publishStatus: 'Đang ra',
        },
        {
            id: 2,
            title: 'Kiếm Đạo Độc Tôn',
            cover: 'https://images.unsplash.com/photo-1612036801632-8e4cf4e2e1b7?w=200&h=300&fit=crop',
            storyType: 'long',
            categories: ['Kiếm hiệp'],
            status: 'draft',
            chapters: 25,
            totalViews: 125000,
            follows: 340,
            rating: 4.5,
            lastUpdate: 'Cập nhật 15:13 25/01/2026',
            publishStatus: 'Lưu tạm',
        },
    ]);

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

    // === STORY HANDLERS ===
    const handleCreateStory = () => {
        setCurrentStory(null);
        setActiveView('createStory');
    };

    const handleEditStory = (story) => {
        setCurrentStory(story);
        setActiveView('editInfo');
    };

    const handleDeleteStory = (storyId) => {
        if (window.confirm('Bạn có chắc chắn muốn xóa truyện này?')) {
            setStories(stories.filter(s => s.id !== storyId));
        }
    };

    const handleSaveStory = (storyData) => {
        if (currentStory) {
            setStories(stories.map(s => s.id === currentStory.id ? { ...s, ...storyData } : s));
        } else {
            const newStory = {
                ...storyData,
                id: Date.now(),
                totalViews: 0,
                follows: 0,
                rating: 0,
                lastUpdate: 'Vừa xong',
            };
            setStories([newStory, ...stories]);
        }
        setActiveView('stories');
        setCurrentStory(null);
    };

    const handleSaveInfo = (infoData) => {
        setStories(stories.map(s => s.id === currentStory.id ? { ...s, ...infoData } : s));
        setActiveView('stories');
        setCurrentStory(null);
    };

    const handleViewComments = (story) => {
        setCurrentStory(story);
        setActiveView('comments');
    };

    // === CHAPTER HANDLERS ===
    const handleViewChapters = (story) => {
        setCurrentStory(story);
        setActiveView('chapterList');
    };

    const handleAddChapter = () => {
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

    // === VIEW RENDERING ===
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
            <ChapterList
                story={currentStory}
                onBack={() => {
                    setActiveView('stories');
                    setCurrentStory(null);
                }}
                onAddChapter={handleAddChapter}
                onEditChapter={handleEditChapter}
            />
        );
    }

    if (activeView === 'addChapter' || activeView === 'editChapter') {
        return (
            <ChapterEditor
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

    // === MAIN LAYOUT ===
    return (
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
                                <button className="px-5 py-2 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all">
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
                    <StoryList
                        stories={stories}
                        onCreateStory={handleCreateStory}
                        onEditStory={handleEditStory}
                        onDeleteStory={handleDeleteStory}
                        onViewChapters={handleViewChapters}
                        onViewComments={handleViewComments}
                    />
                )}
            </div>
        </div>
    );
}
