import { useState, useEffect, useCallback } from 'react';
import { Plus, Edit, Eye, Heart, MessageSquare, Star, ChevronRight, Book, User, LogOut } from 'lucide-react';
import { StoryEditor } from './StoryEditor';
import { StoryInfoEditor } from './StoryInfoEditor';
import { ChapterListManager } from '../author/ChapterListManager';
import { StoryCommentsViewer } from './StoryCommentsViewer';
import { ChapterEditorPage } from '../author/ChapterEditorPage';
import { Footer } from '../../components/homepage/Footer';
import { Header } from '../../components/homepage/Header';
import { createStory, updateStory, getStories, getStoryById } from '../../api/story/storyApi';
import { createChapter, updateChapter, getChapterById } from '../../api/chapter/chapterApi';
import { resolveBackendUrl } from '../../utils/resolveBackendUrl';
import { useAuth } from '../../contexts/AuthContext';
import { useToast } from '../../components/author/story-editor/Toast';

function mapStoryFromApi(item) {
    const status = item.status || item.Status || '';
    const storyProgressStatus = item.storyProgressStatus ?? item.StoryProgressStatus ?? '';
    const publishStatusMap = {
        DRAFT: 'Lưu nháp',
        PENDING_REVIEW: 'Chờ duyệt',
        REJECTED: 'Bị từ chối',
        PUBLISHED: 'Đã xuất bản',
        HIDDEN: 'Đã ẩn',
        COMPLETED: 'Hoàn thành',
        CANCELLED: 'Đã hủy',
    };
    const progressStatusMap = {
        ONGOING: 'Đang ra',
        COMPLETED: 'Hoàn thành',
        HIATUS: 'Tạm dừng',
    };
    const publishStatus = publishStatusMap[status.toUpperCase()] ?? status;
    const progressStatusDisplay = progressStatusMap[storyProgressStatus.toUpperCase()] ?? progressStatusMap.ONGOING;
    // Lấy thể loại từ story_categories (CategoryIds + CategoryNames)
    const categoryIds = item.categoryIds ?? item.CategoryIds ?? [];
    const categoryNamesStr = item.categoryNames ?? item.CategoryNames ?? '';
    const categoryNamesArr = categoryNamesStr
        ? String(categoryNamesStr).split(',').map((s) => s.trim()).filter(Boolean)
        : [];
    const categories = Array.isArray(categoryIds) && categoryIds.length > 0
        ? categoryIds.map((id, i) => ({ id, name: categoryNamesArr[i] ?? '' })).filter((c) => c.id)
        : categoryNamesArr.map((name) => ({ id: name, name })); // fallback: chỉ có tên
    const updatedAt = item.updatedAt || item.UpdatedAt;
    const lastUpdate = updatedAt
        ? new Date(updatedAt).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
        : '';
    const coverPath = item.coverImage ?? item.CoverImage;
    const summary = item.summary ?? item.Summary ?? '';
    const ageRatingMap = { ALL: 'Phù hợp mọi lứa tuổi', '13+': 'Từ 13 tuổi', '16+': 'Từ 16 tuổi', '18+': 'Từ 18 tuổi' };
    const rawAge = item.ageRating ?? item.AgeRating ?? 'ALL';
    const ageRating = ageRatingMap[rawAge] ?? ageRatingMap.ALL;
    return {
        id: item.id ?? item.Id,
        title: item.title ?? item.Title,
        cover: coverPath ? resolveBackendUrl(coverPath) : '',
        summary,
        ageRating,
        categories,
        status: status.toLowerCase(),
        chapters: item.totalChapters ?? item.TotalChapters ?? 0,
        totalViews: Number(item.totalViews ?? item.TotalViews ?? 0),
        follows: Number(item.totalFavorites ?? item.TotalFavorites ?? 0),
        rating: item.avgRating ?? item.AvgRating ?? 0,
        lastUpdate: lastUpdate || 'Chưa cập nhật',
        publishStatus,
        storyProgressStatus: storyProgressStatus || 'ONGOING',
        progressStatusDisplay,
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

    const handleEditStory = async (story) => {
        if (!story?.id) return;
        try {
            const fullStory = await getStoryById(story.id);
            const mapped = mapStoryFromApi(fullStory);
            setCurrentStory(mapped);
            setActiveView('editInfo');
        } catch (err) {
            showToast(err?.response?.data?.message ?? err?.message ?? 'Không tải được thông tin truyện', 'error');
        }
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

    const handleEditChapter = async (chapter) => {
        const chapterId = chapter?.id ?? chapter?.Id;
        if (!chapterId) {
            showToast('Không tìm thấy ID chương', 'error');
            return;
        }

        try {
            // Gọi API để lấy đầy đủ thông tin chương
            const fullChapter = await getChapterById(chapterId);

            // Map dữ liệu từ API về format UI
            const status = (fullChapter.status ?? fullChapter.Status ?? 'DRAFT').toUpperCase();
            const accessTypeApi = (fullChapter.accessType ?? fullChapter.AccessType ?? 'FREE').toUpperCase();

            const mappedChapter = {
                id: fullChapter.id ?? fullChapter.Id,
                number: (fullChapter.orderIndex ?? fullChapter.OrderIndex ?? 0) + 1,
                title: fullChapter.title ?? fullChapter.Title ?? '',
                content: fullChapter.content ?? fullChapter.Content ?? '',
                status: status.toLowerCase(),
                accessType: accessTypeApi === 'PAID' ? 'paid' : 'public',
                price: fullChapter.coinPrice ?? fullChapter.CoinPrice ?? 0,
            };

            setCurrentChapter(mappedChapter);
            setActiveView('editChapter');
        } catch (error) {
            const errorMessage = error?.response?.data?.message || error?.message || 'Không thể tải thông tin chương';
            showToast(errorMessage, 'error');
            console.error('Error loading chapter:', error);
        }
    };

    const handleSaveChapter = async (chapterData) => {
        const storyId = currentStory?.id ?? currentStory?.Id;
        if (!storyId) {
            showToast('Không tìm thấy truyện', 'error');
            return;
        }

        try {
            // Map status: 'draft' -> 'DRAFT', 'published' -> 'PENDING_REVIEW'
            const apiStatus = chapterData.status === 'published' ? 'PENDING_REVIEW' : 'DRAFT';

            // Map accessType: 'public' -> 'FREE', 'paid' -> 'PAID'
            const apiAccessType = chapterData.accessType === 'paid' ? 'PAID' : 'FREE';

            // Xác định là chỉnh sửa hay thêm mới dựa vào currentChapter hoặc chapterData.id
            const isEditMode = currentChapter && (currentChapter.id || currentChapter.Id);

            if (!isEditMode) {
                // Thêm chương mới
                const orderIndex = (chapterData.number || 1) - 1; // number bắt đầu từ 1, orderIndex từ 0

                await createChapter({
                    storyId,
                    title: chapterData.title,
                    content: chapterData.content || '',
                    orderIndex,
                    status: apiStatus,
                    accessType: apiAccessType,
                    coinPrice: apiAccessType === 'PAID' ? (chapterData.price || 0) : 0,
                });

                showToast(
                    apiStatus === 'DRAFT' ? 'Đã lưu nháp chương mới' : 'Đã xuất bản chương mới',
                    'success'
                );
            } else {
                // Cập nhật chương hiện có
                const chapterId = currentChapter.id ?? currentChapter.Id;
                if (!chapterId) {
                    showToast('Không tìm thấy ID chương', 'error');
                    return;
                }

                await updateChapter(chapterId, {
                    title: chapterData.title,
                    content: chapterData.content || '',
                    orderIndex: (chapterData.number || 1) - 1,
                    status: apiStatus,
                    accessType: apiAccessType,
                    coinPrice: apiAccessType === 'PAID' ? (chapterData.price || 0) : 0,
                });

                showToast(
                    apiStatus === 'DRAFT' ? 'Đã cập nhật chương (lưu nháp)' : 'Đã cập nhật chương (xuất bản)',
                    'success'
                );
            }

            // Quay về danh sách chương
            setActiveView('chapterList');
            setCurrentChapter(null);
        } catch (error) {
            const errorMessage = error?.response?.data?.message || error?.message || 'Không thể lưu chương';
            showToast(errorMessage, 'error');
            console.error('Error saving chapter:', error);
        }
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
                status: ch.status || 'DRAFT',
                accessType: ch.accessType || 'FREE',
                coinPrice: ch.coinPrice || 0,
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

    const { showToast, ToastContainer } = useToast();

    const getCategoryId = (c) => (typeof c === 'object' && c?.id ? c.id : c);

    const handleSaveInfo = async (infoData) => {
        if (!currentStory?.id) return;
        try {
            const rawIds = (infoData.categories || []).map(getCategoryId).filter(Boolean);
            const categoryIds = rawIds.filter((id) =>
                /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/i.test(String(id))
            );
            if (categoryIds.length === 0) {
                showToast('Vui lòng chọn ít nhất một thể loại', 'error');
                return;
            }
            const storyPublishStatus = (currentStory.status || 'draft').toUpperCase();
            await updateStory(currentStory.id, {
                title: infoData.title,
                summary: infoData.note ?? '',
                categoryIds,
                status: storyPublishStatus,
                storyProgressStatus: infoData.status || infoData.publishStatus,
                ageRating: infoData.ageRating,
                coverImage: infoData.cover,
            });
            setStories(stories.map(s => s.id === currentStory.id ? { ...s, ...infoData, summary: infoData.note } : s));
            setCurrentStory((prev) => prev ? { ...prev, ...infoData, summary: infoData.note } : null);
            showToast('Đã lưu thay đổi thông tin truyện', 'success');
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.response?.data?.error ?? err?.message ?? 'Không thể lưu thay đổi';
            showToast(msg, 'error');
            throw err;
        }
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
            <>
                <StoryInfoEditor
                    story={currentStory}
                    onSave={handleSaveInfo}
                    onCancel={() => {
                        setActiveView('stories');
                        setCurrentStory(null);
                    }}
                />
                <ToastContainer />
            </>
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
                chapter={activeView === 'editChapter' ? currentChapter : null}
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
                                                        backgroundColor: ['published', 'completed'].includes(story.status) ? '#d1fae5' : '#fef3c7',
                                                        borderRadius: '4px',
                                                        fontSize: '0.75rem',
                                                        color: ['published', 'completed'].includes(story.status) ? '#065f46' : '#92400e',
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
                                                        backgroundColor: (story.status === 'published' || story.status === 'completed') ? '#d1fae5' : '#fef3c7',
                                                        borderRadius: '4px',
                                                        fontSize: '0.75rem',
                                                        color: (story.status === 'published' || story.status === 'completed') ? '#065f46' : '#92400e'
                                                    }}>
                                                        {story.publishStatus}
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