import { ChevronRight, Star } from 'lucide-react';
import { useState } from 'react';
import { StoryHeader } from '../../components/story-detail/StoryHeader';
import { ChapterList } from '../../components/story-detail/ChapterList';
import { CommentSection } from '../../components/story-detail/CommentSection';
import { AuthorCard } from '../../components/story-detail/AuthorCard';
import { RelatedStories } from '../../components/story-detail/RelatedStories';
import { RatingModal } from '../../components/story-detail/RatingModal';
import { ReportModal } from '../../components/story-detail/ReportModal';
import { Footer } from '../../components/homepage/Footer';
import { Header } from '../../components/homepage/Header';

export function StoryDetail() {
    const [isFollowing, setIsFollowing] = useState(false);
    const [activeTab, setActiveTab] = useState('chapters');
    const [isRatingModalOpen, setIsRatingModalOpen] = useState(false);
    const [isReportCommentModalOpen, setIsReportCommentModalOpen] = useState(false);
    const [isReportStoryModalOpen, setIsReportStoryModalOpen] = useState(false);
    const [reportingCommentId, setReportingCommentId] = useState(null);

    // Mock data
    const story = {
        id: '1',
        title: 'Tu Tiên Chi Lộ: Hành Trình Vạn Năm',
        author: {
            name: 'Thiên Tằm Thổ Đậu',
            avatar: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=100&h=100&fit=crop',
            followers: 125000
        },
        cover: 'https://images.unsplash.com/photo-1589998059171-988d887df646?w=800&h=1200&fit=crop',
        genre: ['Tiên hiệp', 'Huyền huyễn', 'Trọng sinh'],
        status: 'Đang cập nhật',
        rating: 4.8,
        totalRatings: 15420,
        views: 2450000,
        totalViews: 5200000,
        comments: 8900,
        chapters: 450,
        words: 2800000,
        lastUpdate: '2 giờ trước',
        description: `Tái sinh trở về thời kỳ mới bắt đầu tu tiên, Phương Viễn quyết tâm thay đổi vận mệnh.

Kiếp trước, hắn chỉ là một tản tu bình thường, sống trong nỗi khổ đau và oán hận, cuối cùng chết trong tay yêu ma.

Kiếp này, hắn biết trước mọi cơ duyên, biết rõ mọi nguy hiểm. Với kiến thức và kinh nghiệm từ tiền kiếp, hắn sẽ bước lên con đường mạnh nhất!

"Ta muốn trở thành người mạnh nhất trong vũ trụ này, không ai có thể ngăn cản ta!"

Một hành trình tu tiên đầy máu và lửa, một câu chuyện về ý chí và nghị lực. Hãy theo dõi Phương Viễn trên con đường chinh phục đỉnh cao tu tiên!`,
    };

    const chapters = Array.from({ length: 15 }, (_, i) => ({
        id: 450 - i,
        title: i === 0
            ? 'Đại chiến với Ma Đế'
            : i === 1
                ? 'Đột phá Nguyên Anh kỳ'
                : i === 2
                    ? 'Bí mật của Thái Cổ Thần Thạch'
                    : `${['Khám phá hang động bí mật', 'Gặp gỡ kỳ nhân', 'Đột phá cảnh giới', 'Đấu với cao thủ', 'Tìm kiếm linh dược'][i % 5]}`,
        time: i === 0 ? '2 giờ trước' : i === 1 ? '1 ngày trước' : `${i + 1} ngày trước`,
        // views: Math.floor(Math.random() * 50000) + 10000,
        views: 10000,
        isNew: i < 3,
        isLocked: i > 10,
    }));

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

    return (
        <div className="min-h-screen bg-slate-50 dark:bg-background-dark">
            <Header />
            {/* Breadcrumb */}
            <div className="bg-white dark:bg-slate-900 border-b border-slate-200 dark:border-slate-800">
                <div className="max-w-[1280px] mx-auto px-4 py-3">
                    <div className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-400">
                        <a href="#" className="hover:text-primary transition-colors">Trang chủ</a>
                        <ChevronRight className="w-4 h-4" />
                        <a href="#" className="hover:text-primary transition-colors">Truyện dài</a>
                        <ChevronRight className="w-4 h-4" />
                        <a href="#" className="hover:text-primary transition-colors">Tiên hiệp</a>
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
                                {activeTab === 'chapters' && <ChapterList chapters={chapters} />}

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
