import { ChevronRight, Star } from 'lucide-react';
import { useState, useEffect, useCallback, useMemo } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import StoryHeader from '../../components/story-detail/StoryHeader';
import { ChapterList } from '../../components/story-detail/ChapterList';
import { CommentSection } from '../../components/story-detail/CommentSection';
import { AuthorCard } from '../../components/story-detail/AuthorCard';
import { RelatedStories } from '../../components/story-detail/RelatedStories';
import { RatingModal } from '../../components/story-detail/RatingModal';
import { ReportModal } from '../../components/story-detail/ReportModal';
import { Footer } from '../../components/homepage/Footer';
import { Header } from '../../components/homepage/Header';
import {
    getStoryById,
    recordStoryView,
    getViewerKeyForViewCache,
    hasViewedStoryInCooldown,
    setStoryViewCache,
    rateStory,
    getStoryRatings,
    getStoryComments,
    addStoryComment,
    toggleCommentLike,
    followStory,
    unfollowStory,
} from '../../api/story/storyApi';
import { getChapters } from '../../api/chapter/chapterApi';
import { getProfileByUserId } from '../../api/account/accountApi';
import { getAuthorFollowersCount } from '../../api/author/authorApi';
import { resolveBackendUrl } from '../../utils/resolveBackendUrl';
import { useAuth } from '../../contexts/AuthContext';
import { useToast } from '../../components/author/story-editor/Toast';

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
    const location = useLocation();
    const { user } = useAuth();
    const viewerKey = getViewerKeyForViewCache(user?.id ?? null);
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
    const [ratingError, setRatingError] = useState(null);
    const [ratingSubmitting, setRatingSubmitting] = useState(false);
    const { showToast, ToastContainer } = useToast();
    const [comments, setComments] = useState([]);
    const [commentsLoading, setCommentsLoading] = useState(false);
    const [commentError, setCommentError] = useState(null);
    const [reviews, setReviews] = useState([]);
    const [reviewsLoading, setReviewsLoading] = useState(false);
    const [visibleReviewsCount, setVisibleReviewsCount] = useState(3);

    const svgAvatarDataUrl = (name) => {
        const initial = (String(name || 'T').trim()[0] || 'T').toUpperCase();
        const svg = `
          <svg xmlns="http://www.w3.org/2000/svg" width="256" height="256">
            <defs>
              <linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
                <stop offset="0" stop-color="#13EC5B"/>
                <stop offset="1" stop-color="#2B7FFF"/>
              </linearGradient>
            </defs>
            <rect width="256" height="256" rx="40" fill="url(#g)"/>
            <text x="50%" y="54%" dominant-baseline="middle" text-anchor="middle"
                  font-family="Arial, Helvetica, sans-serif" font-size="120" font-weight="800" fill="white">${initial}</text>
          </svg>
        `.trim();
        return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
    };

    const getStoryAuthorName = (storyRes) => {
        const name =
            storyRes?.authorName ??
            storyRes?.AuthorName ??
            storyRes?.author?.name ??
            storyRes?.author?.displayName ??
            storyRes?.authorDisplayName ??
            storyRes?.AuthorDisplayName ??
            storyRes?.createdByName ??
            storyRes?.CreatedByName ??
            null;

        return typeof name === 'string' && name.trim() ? name.trim() : 'Tác giả';
    };

    const getStoryAuthorAvatar = (storyRes, authorName) => {
        const avatar =
            storyRes?.authorAvatarUrl ??
            storyRes?.AuthorAvatarUrl ??
            storyRes?.authorAvatar ??
            storyRes?.AuthorAvatar ??
            storyRes?.author?.avatar ??
            storyRes?.author?.avatarUrl ??
            storyRes?.author?.AvatarUrl ??
            storyRes?.avatarUrl ??
            storyRes?.AvatarUrl ??
            null;

        if (avatar && typeof avatar === 'string' && avatar.trim()) return resolveBackendUrl(avatar.trim());
        return svgAvatarDataUrl(authorName);
    };

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
            const inCooldown = hasViewedStoryInCooldown(storyId, viewerKey);
            const loadData = () =>
                Promise.all([
                    getStoryById(storyId, { recordView: false }),
                    getChapters({ storyId, status: 'PUBLISHED', pageSize: 500 }),
                ]);
            (inCooldown ? loadData() : recordStoryView(storyId).then(() => { setStoryViewCache(storyId, viewerKey); return loadData(); }))
                .then(([storyRes, chaptersRes]) => {
                    if (cancelled) return;
                    const rawItems = Array.isArray(chaptersRes) ? chaptersRes : (chaptersRes?.items ?? chaptersRes?.Items ?? []);
                    const categoryNamesStr = storyRes?.categoryNames ?? storyRes?.CategoryNames ?? '';
                    const genreArr = categoryNamesStr
                        ? String(categoryNamesStr).split(',').map((s) => s.trim()).filter(Boolean)
                        : [];
                    const coverPath = storyRes?.coverImage ?? storyRes?.CoverImage;
                    const totalViews = Number(storyRes?.totalViews ?? storyRes?.TotalViews ?? 0);
                    const totalComments = Number(storyRes?.totalComments ?? storyRes?.TotalComments ?? 0);
                    const totalChapters = rawItems.length;
                    const authorId = storyRes?.authorId ?? storyRes?.AuthorId;
                    setIsFollowing(!!(storyRes?.userIsFollowing ?? storyRes?.UserIsFollowing));
                    const progressStatusRaw = (storyRes?.storyProgressStatus ?? storyRes?.StoryProgressStatus ?? 'ONGOING')?.toString?.() ?? 'ONGOING';
                    const progressUpper = String(progressStatusRaw).toUpperCase();
                    const progressLabel = progressUpper === 'COMPLETED' ? 'Hoàn thành' : progressUpper === 'HIATUS' ? 'Tạm dừng' : 'Đang ra';
                    const authorName = getStoryAuthorName(storyRes);
                    const storyPayload = {
                        id: storyRes?.id ?? storyRes?.Id,
                        title: storyRes?.title ?? storyRes?.Title ?? 'Không có tiêu đề',
                        author: {
                            id: authorId,
                            userId: authorId,
                            name: authorName,
                            avatar: getStoryAuthorAvatar(storyRes, authorName),
                            // null = đang chờ GET /authors/{id}/followers-count (tránh hiển thị 0 giả)
                            followers: authorId ? null : 0,
                        },
                        cover: coverPath ? resolveBackendUrl(coverPath) : '',
                        genre: genreArr.length ? genreArr : ['Chưa phân loại'],
                        // Trạng thái tiến độ truyện: Đang ra / Tạm dừng / Hoàn thành
                        storyProgressStatus: progressUpper,
                        storyProgressLabel: progressLabel,
                        // Nhãn cập nhật (UI): tách riêng khỏi trạng thái tiến độ
                        updateLabel: 'Đang cập nhật',
                        rating: Number(storyRes?.avgRating ?? storyRes?.AvgRating ?? 0) || 0,
                        totalRatings: Number(storyRes?.totalRatings ?? storyRes?.TotalRatings ?? 0) || 0,
                        views: totalViews,
                        totalViews,
                        comments: totalComments,
                        chapters: totalChapters,
                        words: 0,
                        lastUpdate: storyRes?.updatedAt ? formatTimeAgo(storyRes.updatedAt) : 'Chưa cập nhật',
                        description: storyRes?.summary ?? storyRes?.Summary ?? 'Chưa có giới thiệu.',
                        lastReadChapterId: storyRes?.lastReadChapterId ?? storyRes?.LastReadChapterId ?? null,
                        lastReadChapterTitle: storyRes?.lastReadChapterTitle ?? storyRes?.LastReadChapterTitle ?? null,
                        lastReadAt: (storyRes?.lastReadAt ?? storyRes?.LastReadAt) ? formatTimeAgo(storyRes?.lastReadAt ?? storyRes?.LastReadAt) : null,
                    };
                    const newCount = 3; // số chương mới nhất được gắn nhãn MỚI
                    setChapters(rawItems.map((ch, idx) => {
                        const orderIndex = ch.orderIndex ?? ch.OrderIndex ?? idx;
                        const num = orderIndex + 1;
                        const updatedAt = ch.updatedAt ?? ch.UpdatedAt ?? ch.publishedAt ?? ch.PublishedAt;
                        const accessType = (ch.accessType ?? ch.AccessType ?? 'FREE').toUpperCase();
                        const coinPrice = Number(ch.coinPrice ?? ch.CoinPrice ?? 0) || 0;
                        const isPaid = accessType === 'PAID' && coinPrice > 0;
                        return {
                            id: num,
                            chapterId: ch.id ?? ch.Id,
                            title: ch.title ?? ch.Title ?? `Chương ${num}`,
                            time: updatedAt ? formatTimeAgo(updatedAt) : '',
                            views: Number(ch.viewCount ?? ch.ViewCount ?? ch.views ?? 0) || 0,
                            isNew: idx >= rawItems.length - newCount,
                            isLocked: isPaid,
                            accessType,
                            coinPrice,
                        };
                    }));
                    if (!authorId) {
                        setStory(storyPayload);
                        return;
                    }
                    return Promise.all([
                        getProfileByUserId(authorId),
                        getAuthorFollowersCount(authorId).catch(() => 0),
                    ])
                        .then(([profile, followerCount]) => {
                            if (cancelled) return;
                            storyPayload.author = {
                                id: profile.id ?? authorId,
                                userId: profile.id ?? authorId,
                                name: profile.displayName ?? storyPayload.author.name,
                                avatar: profile.avatarUrl ? resolveBackendUrl(profile.avatarUrl) : storyPayload.author.avatar,
                                followers: typeof followerCount === 'number' ? followerCount : 0,
                            };
                            setStory(storyPayload);
                        })
                        .catch(() => {
                            if (cancelled) return;
                            // Profile lỗi vẫn cố lấy số follower (public API)
                            getAuthorFollowersCount(authorId)
                                .then((n) => {
                                    if (cancelled) return;
                                    storyPayload.author.followers = typeof n === 'number' ? n : 0;
                                    setStory({ ...storyPayload });
                                })
                                .catch(() => {
                                    if (!cancelled) {
                                        storyPayload.author.followers = 0;
                                        setStory({ ...storyPayload });
                                    }
                                });
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
    }, [storyId, viewerKey]);

    const loadComments = useCallback((options = {}) => {
        if (!storyId) return;
        const silent = options.silent === true;
        if (!silent) {
            setCommentsLoading(true);
            setCommentError(null);
        }
        getStoryComments(storyId)
            .then((list) => setComments(Array.isArray(list) ? list : []))
            .catch((err) => { if (!silent) setCommentError(err?.response?.data?.message ?? 'Không tải được bình luận.'); })
            .finally(() => { if (!silent) setCommentsLoading(false); });
    }, [storyId]);

    useEffect(() => {
        if (storyId && activeTab === 'comments') loadComments();
    }, [storyId, activeTab, loadComments]);

    // Mở tab bình luận khi vào từ thông báo (hash #comment-{guid})
    useEffect(() => {
        const h = location.hash || '';
        if (h && /^#comment-/i.test(h)) {
            setActiveTab('comments');
        }
    }, [location.hash]);

    useEffect(() => {
        const h = location.hash || '';
        if (!h || !/^#comment-/i.test(h)) return;
        if (activeTab !== 'comments' || commentsLoading) return;
        const elId = h.slice(1);
        const frame = requestAnimationFrame(() => {
            document.getElementById(elId)?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        });
        return () => cancelAnimationFrame(frame);
    }, [location.hash, activeTab, commentsLoading, comments.length]);

    // Load comment count sớm (silent) để tab hiển thị đúng số trước khi user click vào.
    useEffect(() => {
        if (storyId) loadComments({ silent: true });
    }, [storyId, loadComments]);

    const loadReviews = useCallback(() => {
        if (!storyId) return;
        setReviewsLoading(true);
        getStoryRatings(storyId)
            .then((list) => {
                const arr = Array.isArray(list) ? list : [];
                setReviews(arr);
                // Đồng bộ số đánh giá ở header (API story đôi khi trả TotalRatings không khớp)
                setStory((prev) => (prev ? { ...prev, totalRatings: arr.length } : prev));
            })
            .catch(() => {
                setReviews([]);
                setStory((prev) => (prev ? { ...prev, totalRatings: 0 } : prev));
            })
            .finally(() => setReviewsLoading(false));
    }, [storyId]);

    // Mỗi user chỉ được đánh giá 1 lần; BE cũng chặn. Dùng để ẩn nút "Đánh giá" và chặn mở modal.
    const { hasUserRated, userRatingStars } = useMemo(() => {
        const uid = user?.id;
        if (!uid) return { hasUserRated: false, userRatingStars: null };
        const mine = reviews.find((r) => String(r.userId ?? r.UserId ?? '') === String(uid));
        return {
            hasUserRated: !!mine,
            userRatingStars: mine != null ? Number(mine.starValue ?? mine.StarValue ?? 0) : null,
        };
    }, [user?.id, reviews]);

    // Load đánh giá ngay khi có storyId để tab hiển thị đúng số (0) trước khi user click tab
    useEffect(() => {
        if (storyId) loadReviews();
    }, [storyId, loadReviews]);

    useEffect(() => {
        if (activeTab === 'reviews') setVisibleReviewsCount(3);
    }, [activeTab]);

    const handleAddComment = async (content, parentId) => {
        if (!storyId) return;
        setCommentError(null);
        try {
            await addStoryComment(storyId, { content: content.trim(), parentId: parentId || undefined });
            loadComments();
            showToast('Đã gửi bình luận.', 'success');
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? 'Không thể gửi bình luận.';
            setCommentError(msg);
        }
    };

    const handleLikeComment = async (commentId) => {
        if (!storyId || !user?.id) return;
        try {
            const res = await toggleCommentLike(storyId, commentId);
            setComments((prev) =>
                prev.map((c) =>
                    (c.id === commentId || c.Id === commentId)
                        ? { ...c, userHasLiked: res.liked, likesCount: res.likesCount ?? res.likes_count ?? c.likesCount ?? c.likes_count ?? 0 }
                        : c
                )
            );
        } catch {
            // ignore
        }
    };

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

    const handleReportComment = (commentId) => {
        setReportingCommentId(commentId);
        setIsReportCommentModalOpen(true);
    };

    const handleSubmitRating = async (starValue, reviewText) => {
        if (!storyId) return;
        if (!user?.id) {
            setRatingError('Vui lòng đăng nhập để đánh giá.');
            return;
        }
        setRatingError(null);
        setRatingSubmitting(true);
        try {
            const data = await rateStory(storyId, { starValue, reviewText });
            setStory((prev) => (prev ? { ...prev, rating: data.avgRating ?? data.avg, totalRatings: data.ratingCount ?? data.count ?? 0 } : prev));
            setIsRatingModalOpen(false);
            loadReviews();
            showToast('Đánh giá thành công!', 'success');
        } catch (err) {
            const status = err?.response?.status;
            const msg = err?.response?.data?.message ?? err?.message ?? 'Không thể gửi đánh giá.';
            if (status === 401) {
                setRatingError('Vui lòng đăng nhập để đánh giá.');
            } else {
                setRatingError(msg);
                // BE trả lỗi "đã đánh giá" khi user đánh giá lần 2 → đồng bộ lại danh sách để UI hiển thị "Bạn đã đánh giá"
                if (msg && (msg.includes('đã đánh giá') || msg.includes('đánh giá lại'))) {
                    setIsRatingModalOpen(false);
                    loadReviews();
                }
            }
        } finally {
            setRatingSubmitting(false);
        }
    };

    const handleOpenRating = () => {
        if (hasUserRated) return; // Mỗi user chỉ được đánh giá 1 lần
        setRatingError(null);
        setIsRatingModalOpen(true);
    };

    const handleCloseRatingModal = () => {
        setIsRatingModalOpen(false);
        setRatingError(null);
    };

    const handleToggleFollow = async () => {
        if (!storyId) return;
        if (!user?.id) {
            showToast('Vui lòng đăng nhập để theo dõi truyện.', 'warning');
            return;
        }
        try {
            if (isFollowing) {
                await unfollowStory(storyId);
                setIsFollowing(false);
                showToast('Đã bỏ theo dõi.', 'success');
            } else {
                await followStory(storyId);
                setIsFollowing(true);
                showToast('Đã theo dõi truyện. Bạn sẽ nhận thông báo khi có chương mới.', 'success');
            }
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? (isFollowing ? 'Không thể bỏ theo dõi.' : 'Không thể theo dõi.');
            showToast(msg, 'error');
        }
    };

    const handleSubmitCommentReport = (payload) => {
        console.log('Comment report submitted:', { ...payload, targetId: reportingCommentId });
        showToast('Đã gửi báo cáo. Chúng tôi sẽ xem xét trong thời gian sớm nhất.', 'success');
    };

    const handleSubmitStoryReport = (payload) => {
        console.log('Story report submitted:', payload);
        showToast('Đã gửi báo cáo. Chúng tôi sẽ xem xét trong thời gian sớm nhất.', 'success');
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
                            onToggleFollow={handleToggleFollow}
                            onOpenRating={handleOpenRating}
                            hasUserRated={hasUserRated}
                            userRatingStars={userRatingStars}
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
                                        Bình luận ({comments.length.toLocaleString()})
                                    </button>
                                    <button
                                        onClick={() => setActiveTab('reviews')}
                                        className={`py-4 border-b-2 font-semibold text-sm transition-colors ${activeTab === 'reviews'
                                            ? 'border-primary text-primary'
                                            : 'border-transparent text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                                            }`}
                                    >
                                        Đánh giá ({(reviews.length > 0 ? reviews.length : (story.totalRatings ?? 0)).toLocaleString()})
                                    </button>
                                </div>
                            </div>

                            <div className="p-6">
                                {activeTab === 'chapters' && <ChapterList chapters={chapters} storyId={storyId} lastReadChapterId={story.lastReadChapterId} />}

                                {activeTab === 'comments' && (
                                    <CommentSection
                                        storyId={storyId}
                                        comments={comments}
                                        isLoggedIn={!!user?.id}
                                        commentError={commentError}
                                        commentsLoading={commentsLoading}
                                        onSubmitComment={handleAddComment}
                                        onLikeComment={handleLikeComment}
                                        onReportComment={handleReportComment}
                                        formatTimeAgo={formatTimeAgo}
                                    />
                                )}

                                {activeTab === 'reviews' && (
                                    <>
                                        {reviewsLoading ? (
                                            <p className="text-slate-500 dark:text-slate-400 text-sm py-4">Đang tải đánh giá...</p>
                                        ) : reviews.length === 0 ? (
                                            <div className="text-center py-12">
                                                <Star className="w-12 h-12 text-slate-300 dark:text-slate-700 mx-auto mb-4" />
                                                <p className="text-slate-500 dark:text-slate-400">Chưa có đánh giá nào</p>
                                            </div>
                                        ) : (
                                            <>
                                                <div className="space-y-4">
                                                    {reviews.slice(0, visibleReviewsCount).map((r) => {
                                                        const name = r.userDisplayName ?? r.UserDisplayName ?? 'Ẩn danh';
                                                        const stars = Number(r.starValue ?? r.StarValue ?? 0);
                                                        const text = r.reviewText ?? r.ReviewText ?? '';
                                                        const createdAt = r.createdAt ?? r.CreatedAt;
                                                        return (
                                                            <div key={r.id ?? r.Id} className="flex gap-3 p-4 bg-slate-50 dark:bg-slate-800 rounded-lg">
                                                                <div className="w-10 h-10 rounded-full bg-primary/20 shrink-0 flex items-center justify-center text-primary font-bold text-sm">
                                                                    {(name || '?').charAt(0).toUpperCase()}
                                                                </div>
                                                                <div className="flex-1 min-w-0">
                                                                    <div className="flex items-center gap-2 flex-wrap">
                                                                        <span className="font-semibold text-slate-900 dark:text-white text-sm">{name}</span>
                                                                        <span className="flex items-center gap-0.5">
                                                                            {[1, 2, 3, 4, 5].map((i) => (
                                                                                <Star key={i} className={`w-4 h-4 ${i <= stars ? 'fill-amber-400 text-amber-400' : 'text-slate-300 dark:text-slate-600'}`} />
                                                                            ))}
                                                                        </span>
                                                                        {createdAt && (
                                                                            <span className="text-xs text-slate-500 dark:text-slate-400">{formatTimeAgo(createdAt)}</span>
                                                                        )}
                                                                    </div>
                                                                    {text && <p className="text-slate-600 dark:text-slate-400 text-sm mt-1 whitespace-pre-wrap">{text}</p>}
                                                                </div>
                                                            </div>
                                                        );
                                                    })}
                                                </div>
                                                <div className="flex flex-wrap gap-3 mt-4">
                                                    {reviews.length > visibleReviewsCount && (
                                                        <button
                                                            type="button"
                                                            onClick={() => setVisibleReviewsCount((n) => n + 3)}
                                                            className="text-sm text-primary hover:underline"
                                                        >
                                                            Xem thêm đánh giá ({reviews.length - visibleReviewsCount})
                                                        </button>
                                                    )}
                                                    {visibleReviewsCount > 3 && (
                                                        <button
                                                            type="button"
                                                            onClick={() => setVisibleReviewsCount((n) => Math.max(3, n - 3))}
                                                            className="text-sm text-slate-500 dark:text-slate-400 hover:underline"
                                                        >
                                                            Ẩn bớt đánh giá
                                                        </button>
                                                    )}
                                                </div>
                                            </>
                                        )}
                                    </>
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
                onClose={handleCloseRatingModal}
                onSubmit={handleSubmitRating}
                errorMessage={ratingError}
                submitting={ratingSubmitting}
            />

            <ReportModal
                isOpen={isReportCommentModalOpen}
                onClose={() => { setIsReportCommentModalOpen(false); setReportingCommentId(null); }}
                onSubmit={handleSubmitCommentReport}
                title="Báo cáo bình luận"
                type="comment"
                targetId={reportingCommentId}
            />

            <ReportModal
                isOpen={isReportStoryModalOpen}
                onClose={() => setIsReportStoryModalOpen(false)}
                onSubmit={handleSubmitStoryReport}
                title="Báo cáo truyện"
                type="story"
                storyId={storyId}
                storyTitle={story?.title}
            />
            <ToastContainer />
            <Footer />
        </div>
    );
}
