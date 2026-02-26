import { ChevronRight, Star } from 'lucide-react';
import { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { StoryHeader } from '../../components/story-detail/StoryHeader';
import { ChapterList } from '../../components/story-detail/ChapterList';
import { CommentSection } from '../../components/story-detail/CommentSection';
import { AuthorCard } from '../../components/story-detail/AuthorCard';
import { RelatedStories } from '../../components/story-detail/RelatedStories';
import { RatingModal } from '../../components/story-detail/RatingModal';
import { ReportModal } from '../../components/story-detail/ReportModal';
import { Footer } from '../../components/homepage/Footer';
import { Header } from '../../components/homepage/Header';
import { getStoryById } from '../../api/story/storyApi';
import { getChapters } from '../../api/chapter/chapterApi';
import { getProfileByUserId } from '../../api/account/accountApi';
import { resolveBackendUrl } from '../../utils/resolveBackendUrl';

function formatTimeAgo(dateStr) {
    if (!dateStr) return '';
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now - date;
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);
    if (diffMins < 60) return `${diffMins} phút trước`;
    if (diffHours < 24) return `${diffHours} giờ trước`;
    if (diffDays < 7) return `${diffDays} ngày trước`;
    return date.toLocaleDateString('vi-VN');
}

export function StoryDetail() {
    const { storyId } = useParams();
    const navigate = useNavigate();
    const [story, setStory] = useState(null);
    const [chapters, setChapters] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [isFollowing, setIsFollowing] = useState(false);
    const [activeTab, setActiveTab] = useState('chapters');
    const [isRatingModalOpen, setIsRatingModalOpen] = useState(false);
    const [isReportCommentModalOpen, setIsReportCommentModalOpen] = useState(false);
    const [isReportStoryModalOpen, setIsReportStoryModalOpen] = useState(false);
    const [reportingCommentId, setReportingCommentId] = useState(null);

    useEffect(() => {
        let cancelled = false;
        const id = setTimeout(() => {
            if (!storyId) {
                setLoading(false);
                setError('Thiếu ID truyện');
                return;
            }
            setLoading(true);
            setError(null);
            Promise.all([
                getStoryById(storyId),
                getChapters({ storyId, status: 'PUBLISHED', pageSize: 500 })
            ])
                .then(([storyRes, chaptersRes]) => {
                    if (cancelled) return;
                    const rawItems = Array.isArray(chaptersRes) ? chaptersRes : (chaptersRes?.items ?? chaptersRes?.Items ?? []);
                    const categoryNamesStr = storyRes?.categoryNames ?? storyRes?.CategoryNames ?? '';
                    const genreArr = categoryNamesStr
                        ? String(categoryNamesStr).split(',').map((s) => s.trim()).filter(Boolean)
                        : [];
                    const coverPath = storyRes?.coverImage ?? storyRes?.CoverImage;
                    const totalViews = Number(storyRes?.totalViews ?? storyRes?.TotalViews ?? 0);
                    const totalChapters = rawItems.length;
                    const authorId = storyRes?.authorId ?? storyRes?.AuthorId;
                    const storyPayload = {
                        id: storyRes?.id ?? storyRes?.Id,
                        title: storyRes?.title ?? storyRes?.Title ?? 'Không có tiêu đề',
                        author: {
                            name: storyRes?.authorName ?? storyRes?.AuthorName ?? 'Ẩn danh',
                            avatar: '',
                            followers: 0
                        },
                        cover: coverPath ? resolveBackendUrl(coverPath) : '',
                        genre: genreArr.length ? genreArr : ['Chưa phân loại'],
                        status: 'Đang cập nhật',
                        rating: Number(storyRes?.avgRating ?? storyRes?.AvgRating ?? 0) || 0,
                        totalRatings: Number(storyRes?.totalRatings ?? 0) || 0,
                        views: totalViews,
                        totalViews,
                        comments: 0,
                        chapters: totalChapters,
                        words: 0,
                        lastUpdate: storyRes?.updatedAt ? formatTimeAgo(storyRes.updatedAt) : 'Chưa cập nhật',
                        description: storyRes?.summary ?? storyRes?.Summary ?? 'Chưa có giới thiệu.'
                    };
                    setChapters(rawItems.map((ch, idx) => {
                        const orderIndex = ch.orderIndex ?? ch.OrderIndex ?? idx;
                        const num = orderIndex + 1;
                        const updatedAt = ch.updatedAt ?? ch.UpdatedAt ?? ch.publishedAt ?? ch.PublishedAt;
                        return {
                            id: num,
                            chapterId: ch.id ?? ch.Id,
                            title: ch.title ?? ch.Title ?? `Chương ${num}`,
                            time: updatedAt ? formatTimeAgo(updatedAt) : '',
                            views: Number(ch.viewCount ?? ch.ViewCount ?? ch.views ?? 0) || 0,
                            isNew: idx < 3,
                            isLocked: false
                        };
                    }));
                    if (!authorId) {
                        setStory(storyPayload);
                        return;
                    }
                    return getProfileByUserId(authorId)
                        .then((profile) => {
                            if (cancelled) return;
                            storyPayload.author = {
                                name: profile.displayName ?? storyPayload.author.name,
                                avatar: profile.avatarUrl ? resolveBackendUrl(profile.avatarUrl) : '',
                                followers: Number(profile.stats?.totalReads ?? profile.stats?.TotalReads ?? 0) || 0
                            };
                            setStory(storyPayload);
                        })
                        .catch(() => {
                            if (!cancelled) setStory(storyPayload);
                        });
                })
                .catch((err) => {
                    if (!cancelled) {
                        setError(err?.message ?? 'Không tải được truyện');
                        setStory(null);
                        setChapters([]);
                    }
                })
                .finally(() => {
                    if (!cancelled) setLoading(false);
                });
        }, 0);
        return () => {
            cancelled = true;
            clearTimeout(id);
        };
    }, [storyId]);

    const relatedStories = Array.from({ length: 5 }, (_, i) => ({
        id: i + 2,
        title: ['Đấu Phá Thương Khung', 'Vũ Luyện Đỉnh Phong', 'Thần Ấn Vương Tọa', 'Tuyệt Thế Đường Môn', 'Đấu La Đại Lục'][i],
        cover: `https://images.unsplash.com/photo-${['1589998059171', '1612036801632', '1614729939124', '1589998059171', '1612036801632'][i]}-988d887df646?w=300&h=400&fit=crop`,
        author: ['Thiên Tằm Thổ Đậu', 'Ngã Cật Tây Hồng Thị', 'Đường Gia Tam Thiếu', 'Đường Gia Tam Thiếu', 'Đường Gia Tam Thiếu'][i],
        rating: 4.5 + (i * 0.1),
        // chapters: Math.floor(Math.random() * 500) + 100,
        chapters: 100,
    }));

    const moreRelatedStories = Array.from({ length: 8 }, (_, i) => ({
        id: i + 10,
        title: [
            'Tu Chân Phản Phái',
            'Ngã Dục Phong Thiên',
            'Thông Thiên Chi Lộ',
            'Tinh Thần Biến',
            'Bách Luyện Thành Thần',
            'Tam Bộ Thiên Môn',
            'Tương Thần',
            'Ngũ Hành Thiên'
        ][i],
        cover: `https://images.unsplash.com/photo-${['1589998059171', '1612036801632', '1614729939124', '1610926597998', '1598669266459', '1762554914464', '1764768306669', '1633901605644'][i]}-988d887df646?w=300&h=400&fit=crop`,
        author: ['Ngạo Vô Thường', 'Mộng Nhập Thần Cơ', 'Vô Tội', 'Hồ Thuyết Bát Đạo', 'Thập Lý Kiếm Thần', 'Hắc Tâm Bất Tử', 'Bạch Kim Hành', 'Phương Tưởng'][i],
        rating: 4.3 + (i * 0.05),
        // chapters: Math.floor(Math.random() * 800) + 200,
        chapters: 200,
        genre: ['Tiên hiệp', 'Huyền huyễn', 'Tu tiên'][i % 3],
    }));

    const comments = [
        {
            id: 1,
            user: { name: 'Độc Giả 123', avatar: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=50&h=50&fit=crop' },
            content: 'Truyện hay quá! Cốt truyện chặt chẽ, nhân vật phát triển hợp lý. Đọc mà không thể rời mắt!',
            time: '3 giờ trước',
            likes: 234,
        },
        {
            id: 2,
            user: { name: 'Fan Tiên Hiệp', avatar: 'https://images.unsplash.com/photo-1494790108377-be9c29b29330?w=50&h=50&fit=crop' },
            content: 'Chap mới ra nhanh quá, tác giả siêng cập nhật ghê! Hóng chap sau!',
            time: '5 giờ trước',
            likes: 156,
        },
        {
            id: 3,
            user: { name: 'Nguyễn Văn A', avatar: 'https://images.unsplash.com/photo-1599566150163-29194dcaad36?w=50&h=50&fit=crop' },
            content: 'Cảm ơn tác giả đã cho ra một tác phẩm tuyệt vời như vậy. Mong tác giả giữ vững phim độ cập nhật nhé!',
            time: '1 ngày trước',
            likes: 89,
        },
        {
            id: 4,
            user: { name: 'Phương Mai', avatar: 'https://images.unsplash.com/photo-1438761681033-6461ffad8d80?w=50&h=50&fit=crop' },
            content: 'Phần này hơi chậm rồi, mong tác giả đẩy nhanh nhịp độ hơn. Nhưng nhìn chung vẫn ổn!',
            time: '1 ngày trước',
            likes: 45,
        },
        {
            id: 5,
            user: { name: 'Trần Minh', avatar: 'https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?w=50&h=50&fit=crop' },
            content: 'Nhân vật chính quá bá đạo luôn! Thích quá đi mất!!!',
            time: '2 ngày trước',
            likes: 178,
        },
        {
            id: 6,
            user: { name: 'Linh Ngọc', avatar: 'https://images.unsplash.com/photo-1544005313-94ddf0286df2?w=50&h=50&fit=crop' },
            content: 'Có ai giống mình không? Đọc xong chap này mà muốn đọc luôn chap tiếp theo quá!',
            time: '2 ngày trước',
            likes: 201,
        },
        {
            id: 7,
            user: { name: 'Hoàng Long', avatar: 'https://images.unsplash.com/photo-1500648767791-00dcc994a43e?w=50&h=50&fit=crop' },
            content: 'Plot twist ở chương này quá hay! Không ngờ tác giả lại viết như vậy.',
            time: '3 ngày trước',
            likes: 67,
        },
        {
            id: 8,
            user: { name: 'Thu Hà', avatar: 'https://images.unsplash.com/photo-1487412720507-e7ab37603c6f?w=50&h=50&fit=crop' },
            content: 'Mình follow từ chap 1 đến giờ, chưa bao giờ thất vọng. Tác giả quá đỉnh!',
            time: '3 ngày trước',
            likes: 312,
        },
        {
            id: 9,
            user: { name: 'Quang Huy', avatar: 'https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?w=50&h=50&fit=crop' },
            content: 'Các nhân vật phụ cũng được phát triển rất tốt, không bị lãng quên như nhiều truyện khác.',
            time: '4 ngày trước',
            likes: 93,
        },
        {
            id: 10,
            user: { name: 'Bảo Trâm', avatar: 'https://images.unsplash.com/photo-1534528741775-53994a69daeb?w=50&h=50&fit=crop' },
            content: 'Đọc truyện này mà cảm giác thời gian trôi quá nhanh. Tác giả viết hay lắm!',
            time: '4 ngày trước',
            likes: 156,
        },
        {
            id: 11,
            user: { name: 'Đức Anh', avatar: 'https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?w=50&h=50&fit=crop' },
            content: 'Mong tác giả cập nhật nhiều hơn nữa. Đợi mỗi ngày một chap thật là khó chịu quá!',
            time: '5 ngày trước',
            likes: 128,
        },
        {
            id: 12,
            user: { name: 'Khánh Linh', avatar: 'https://images.unsplash.com/photo-1517841905240-472988babdf9?w=50&h=50&fit=crop' },
            content: 'Hệ thống tu luyện trong truyện rất logic và dễ hiểu. Thích lắm!',
            time: '5 ngày trước',
            likes: 74,
        },
    ];

    const handleReportComment = (commentId) => {
        setReportingCommentId(commentId);
        setIsReportCommentModalOpen(true);
    };

    const handleSubmitRating = (rating, review) => {
        console.log('Rating submitted:', rating, review);
        // Handle rating submission
    };

    const handleSubmitCommentReport = (reason, details) => {
        console.log('Comment report submitted:', reportingCommentId, reason, details);
        // Handle comment report submission
    };

    const handleSubmitStoryReport = (reason, details) => {
        console.log('Story report submitted:', reason, details);
        // Handle story report submission
    };

    if (loading) {
        return (
            <div className="min-h-screen bg-slate-50 dark:bg-background-dark">
                <Header />
                <div className="max-w-[1280px] mx-auto px-4 py-12 text-center text-slate-500">Đang tải...</div>
                <Footer />
            </div>
        );
    }
    if (error || !story) {
        return (
            <div className="min-h-screen bg-slate-50 dark:bg-background-dark">
                <Header />
                <div className="max-w-[1280px] mx-auto px-4 py-12 text-center text-red-500">{error || 'Không tìm thấy truyện'}</div>
                <Footer />
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-slate-50 dark:bg-background-dark">
            <Header />
            {/* Breadcrumb */}
            <div className="bg-white dark:bg-slate-900 border-b border-slate-200 dark:border-slate-800">
                <div className="max-w-[1280px] mx-auto px-4 py-3">
                    <div className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-400">
                        <a href="/home" className="hover:text-primary transition-colors">Trang chủ</a>
                        <ChevronRight className="w-4 h-4" />
                        <span className="text-slate-900 dark:text-white font-medium line-clamp-1">{story.title}</span>
                    </div>
                </div>
            </div>

            <div className="max-w-[1280px] mx-auto px-4 py-6">
                <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
                    {/* Main Content */}
                    <div className="lg:col-span-8">
                        {/* Story Header */}
                        <StoryHeader
                            story={story}
                            isFollowing={isFollowing}
                            onToggleFollow={() => setIsFollowing(!isFollowing)}
                            onOpenRating={() => setIsRatingModalOpen(true)}
                            onOpenReport={() => setIsReportStoryModalOpen(true)}
                            onReadStory={() => {
                                const first = chapters[0];
                                if (first?.chapterId && storyId) {
                                    navigate(`/chapter?storyId=${storyId}&chapterId=${first.chapterId}`);
                                } else if (storyId) {
                                    navigate(`/chapter?storyId=${storyId}`);
                                }
                            }}
                        />

                        {/* Tabs */}
                        <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 mt-6">
                            <div className="border-b border-slate-200 dark:border-slate-800">
                                <div className="flex gap-6 px-6">
                                    <button
                                        onClick={() => setActiveTab('chapters')}
                                        className={`py-4 border-b-2 font-semibold text-sm transition-colors ${activeTab === 'chapters'
                                            ? 'border-primary text-primary'
                                            : 'border-transparent text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                                            }`}
                                    >
                                        Danh sách chương ({story.chapters})
                                    </button>
                                    <button
                                        onClick={() => setActiveTab('comments')}
                                        className={`py-4 border-b-2 font-semibold text-sm transition-colors ${activeTab === 'comments'
                                            ? 'border-primary text-primary'
                                            : 'border-transparent text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                                            }`}
                                    >
                                        Bình luận ({story.comments.toLocaleString()})
                                    </button>
                                    <button
                                        onClick={() => setActiveTab('reviews')}
                                        className={`py-4 border-b-2 font-semibold text-sm transition-colors ${activeTab === 'reviews'
                                            ? 'border-primary text-primary'
                                            : 'border-transparent text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                                            }`}
                                    >
                                        Đánh giá ({story.totalRatings.toLocaleString()})
                                    </button>
                                </div>
                            </div>

                            <div className="p-6">
                                {activeTab === 'chapters' && <ChapterList chapters={chapters} storyId={storyId} />}

                                {activeTab === 'comments' && (
                                    <CommentSection comments={comments} onReportComment={handleReportComment} />
                                )}

                                {activeTab === 'reviews' && (
                                    <div className="text-center py-12">
                                        <Star className="w-12 h-12 text-slate-300 dark:text-slate-700 mx-auto mb-4" />
                                        <p className="text-slate-500 dark:text-slate-400">Chưa có đánh giá nào</p>
                                    </div>
                                )}
                            </div>
                        </div>
                    </div>

                    {/* Sidebar */}
                    <div className="lg:col-span-4">
                        <div className="sticky top-20 space-y-6">
                            <AuthorCard author={story.author} />
                        </div>
                    </div>
                </div>

                {/* Related Stories Section - Full Width */}
                <div className="mt-10">
                    <RelatedStories stories={[...relatedStories, ...moreRelatedStories]} />
                </div>
            </div>

            {/* Modals */}
            <RatingModal
                isOpen={isRatingModalOpen}
                onClose={() => setIsRatingModalOpen(false)}
                onSubmit={handleSubmitRating}
            />

            <ReportModal
                isOpen={isReportCommentModalOpen}
                onClose={() => setIsReportCommentModalOpen(false)}
                onSubmit={handleSubmitCommentReport}
                title="Báo cáo bình luận"
                type="comment"
            />

            <ReportModal
                isOpen={isReportStoryModalOpen}
                onClose={() => setIsReportStoryModalOpen(false)}
                onSubmit={handleSubmitStoryReport}
                title="Báo cáo truyện"
                type="story"
            />
            <Footer />
        </div>
    );
}
