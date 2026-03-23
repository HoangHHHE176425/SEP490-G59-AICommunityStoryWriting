import { useState, useEffect, useCallback } from 'react';
import { useSearchParams, useNavigate, useLocation } from 'react-router-dom';
import { ChapterNavBar } from '../../components/chapter-detail/ChapterNavBar';
import { ChapterSettings } from '../../components/chapter-detail/ChapterSettings';
import { ChapterSidebar } from '../../components/chapter-detail/ChapterSidebar';
import { ChapterContent } from '../../components/chapter-detail/ChapterContent';
import { ChapterNavigation } from '../../components/chapter-detail/ChapterNavigation';
import { ChapterComments } from '../../components/chapter-detail/ChapterComments';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { getStoryById, saveReadingProgress } from '../../api/story/storyApi';
import { getChapterById, getChapters, getChapterComments, addChapterComment, setChapterCommentReaction, unlockPaidChapter } from '../../api/chapter/chapterApi';
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

export function ChapterReader({ onBack, onNavigateToStory }) {
    const [searchParams] = useSearchParams();
    const navigate = useNavigate();
    const { user, loading: authLoading } = useAuth();
    const { showToast, ToastContainer } = useToast();
    const location = useLocation();
    const urlStoryId = searchParams.get('storyId');
    const urlChapterId = searchParams.get('chapterId');

    // Chưa đăng nhập thì không gọi các API có `[Authorize]` (tránh 401 + trang lỗi).
    useEffect(() => {
        if (!urlStoryId || !urlChapterId) return;
        if (authLoading) return;
        if (user?.id) return;
        const redirectTarget = `${location.pathname}${location.search}`;
        navigate(`/login?redirect=${encodeURIComponent(redirectTarget)}`, { replace: true });
    }, [urlStoryId, urlChapterId, authLoading, user?.id, location.pathname, location.search, navigate]);

    const [fontSize, setFontSize] = useState(18);
    const [fontFamily, setFontFamily] = useState('serif');
    const [backgroundColor, setBackgroundColor] = useState('#ffffff');
    const [textColor, setTextColor] = useState('#1e293b');
    const [lineHeight, setLineHeight] = useState(1.8);
    const [showSettings, setShowSettings] = useState(false);
    const [showChapterList, setShowChapterList] = useState(false);
    const [isBookmarked, setIsBookmarked] = useState(false);

    const [story, setStory] = useState(null);
    const [chapter, setChapter] = useState(null);
    const [allChapters, setAllChapters] = useState([]);
    const [loading, setLoading] = useState(!!(urlStoryId && urlChapterId));
    const [error, setError] = useState(null);
    const [unlocking, setUnlocking] = useState(false);

    const [comments, setComments] = useState([]);
    const [commentsLoading, setCommentsLoading] = useState(false);
    const [commentError, setCommentError] = useState(null);

    useEffect(() => {
        let cancelled = false;
        const id = setTimeout(() => {
            if (!urlStoryId || !urlChapterId) {
                setStory(null);
                setChapter(null);
                setAllChapters([]);
                setLoading(false);
                return;
            }
            if (!user?.id) {
                // Redirect effect sẽ xử lý; tạm giữ `loading` để UI không nhảy layout.
                return;
            }
            setLoading(true);
            setError(null);
            Promise.all([
                getStoryById(urlStoryId),
                getChapterById(urlChapterId),
                getChapters({ storyId: urlStoryId, status: 'PUBLISHED', pageSize: 500 })
            ])
                .then(([storyRes, chapterRes, chaptersRes]) => {
                    if (cancelled) return;
                    const rawChapters = Array.isArray(chaptersRes) ? chaptersRes : (chaptersRes?.items ?? chaptersRes?.Items ?? []);
                    setStory({
                        title: storyRes?.title ?? storyRes?.Title ?? '',
                        author: storyRes?.authorName ?? storyRes?.AuthorName ?? 'Ẩn danh',
                    });
                    const orderIndex = chapterRes?.orderIndex ?? chapterRes?.OrderIndex ?? 0;
                    const accessType = (chapterRes?.accessType ?? chapterRes?.AccessType ?? 'FREE').toUpperCase();
                    const coinPrice = Number(chapterRes?.coinPrice ?? chapterRes?.CoinPrice ?? 0) || 0;
                    const isUnlocked = Boolean(
                        chapterRes?.isUnlocked ??
                            chapterRes?.IsUnlocked ??
                            chapterRes?.unlocked ??
                            chapterRes?.Unlocked ??
                            false
                    );
                    const isPaidLocked = accessType === 'PAID' && coinPrice > 0 && !isUnlocked;
                    const contentRaw = chapterRes?.content ?? chapterRes?.Content ?? '';
                    const content = isPaidLocked ? '' : (contentRaw || 'Chưa có nội dung.');
                    const wordCount = (content.trim().split(/\s+/).filter(Boolean).length) || 0;
                    setChapter({
                        number: orderIndex + 1,
                        title: chapterRes?.title ?? chapterRes?.Title ?? 'Không có tiêu đề',
                        content,
                        publishedAt: chapterRes?.publishedAt ?? chapterRes?.PublishedAt ?? chapterRes?.updatedAt ? formatTimeAgo(chapterRes.updatedAt ?? chapterRes.UpdatedAt) : '',
                        views: Number(chapterRes?.viewCount ?? chapterRes?.ViewCount ?? 0) || 0,
                        words: wordCount,
                        isPaidLocked,
                        coinPrice,
                    });
                    setAllChapters(rawChapters.map((ch, idx) => {
                        const chAccess = (ch.accessType ?? ch.AccessType ?? 'FREE').toUpperCase();
                        const chPrice = Number(ch.coinPrice ?? ch.CoinPrice ?? 0) || 0;
                        const chUnlocked = Boolean(
                            ch.isUnlocked ??
                                ch.IsUnlocked ??
                                ch.unlocked ??
                                ch.Unlocked ??
                                false
                        );
                        return {
                            number: (ch.orderIndex ?? ch.OrderIndex ?? idx) + 1,
                            title: ch.title ?? ch.Title ?? `Chương ${idx + 1}`,
                            chapterId: ch.id ?? ch.Id,
                            isLocked: chAccess === 'PAID' && chPrice > 0 && !chUnlocked,
                            coinPrice: chPrice,
                        };
                    }));
                    if (user?.id && urlStoryId && urlChapterId) {
                        saveReadingProgress(urlStoryId, urlChapterId).catch(() => { });
                    }
                })
                .catch((err) => {
                    if (!cancelled) {
                        setError(err?.message ?? 'Không tải được chương');
                        setStory(null);
                        setChapter(null);
                        setAllChapters([]);
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
    }, [urlStoryId, urlChapterId, user?.id, authLoading]);

    const loadComments = useCallback(() => {
        if (!urlChapterId) return;
        setCommentsLoading(true);
        setCommentError(null);
        getChapterComments(urlChapterId)
            .then((data) => setComments(Array.isArray(data) ? data : []))
            .catch(() => setComments([]))
            .finally(() => setCommentsLoading(false));
    }, [urlChapterId]);

    useEffect(() => {
        if (urlChapterId) loadComments();
    }, [urlChapterId, loadComments]);

    const handleSubmitComment = useCallback(async (content, parentId) => {
        if (!urlChapterId) return;
        if (chapter?.isPaidLocked) {
            setCommentError('Bạn cần mở khóa chương để bình luận.');
            return;
        }
        setCommentError(null);
        try {
            await addChapterComment(urlChapterId, { content: content.trim(), parentId: parentId || null });
            loadComments();
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? 'Không gửi được bình luận.';
            setCommentError(msg);
        }
    }, [urlChapterId, chapter?.isPaidLocked, loadComments]);

    const handleLikeComment = useCallback(async (commentId) => {
        if (!urlChapterId || !user?.id) return;
        if (chapter?.isPaidLocked) return;
        try {
            await setChapterCommentReaction(urlChapterId, commentId, 'LIKE');
            loadComments();
        } catch {
            setCommentError('Không cập nhật được reaction.');
        }
    }, [urlChapterId, user?.id, chapter?.isPaidLocked, loadComments]);

    const refreshChapterUnlockedState = useCallback(async () => {
        if (!urlChapterId) return;
        try {
            const chapterRes = await getChapterById(urlChapterId);
            const orderIndex = chapterRes?.orderIndex ?? chapterRes?.OrderIndex ?? 0;
            const accessType = (chapterRes?.accessType ?? chapterRes?.AccessType ?? 'FREE').toUpperCase();
            const coinPrice = Number(chapterRes?.coinPrice ?? chapterRes?.CoinPrice ?? 0) || 0;
            const isUnlocked = Boolean(
                chapterRes?.isUnlocked ??
                    chapterRes?.IsUnlocked ??
                    chapterRes?.unlocked ??
                    chapterRes?.Unlocked ??
                    false
            );
            const isPaidLocked = accessType === 'PAID' && coinPrice > 0 && !isUnlocked;
            const contentRaw = chapterRes?.content ?? chapterRes?.Content ?? '';
            const content = isPaidLocked ? '' : (contentRaw || 'Chưa có nội dung.');
            const wordCount = (content.trim().split(/\s+/).filter(Boolean).length) || 0;
            setChapter((prev) => ({
                ...(prev ?? {}),
                number: orderIndex + 1,
                title: chapterRes?.title ?? chapterRes?.Title ?? prev?.title ?? '',
                content,
                publishedAt: chapterRes?.publishedAt ?? chapterRes?.PublishedAt ?? chapterRes?.updatedAt ? formatTimeAgo(chapterRes.updatedAt ?? chapterRes.UpdatedAt) : prev?.publishedAt ?? '',
                views: Number(chapterRes?.viewCount ?? chapterRes?.ViewCount ?? prev?.views ?? 0) || 0,
                words: wordCount,
                isPaidLocked,
                coinPrice,
            }));
            setAllChapters((prev) =>
                prev.map((ch) => (ch.chapterId === urlChapterId ? { ...ch, isLocked: isPaidLocked, coinPrice } : ch))
            );
            loadComments();
        } catch {
            // ignore, UI không cần hard fail
        }
    }, [urlChapterId, loadComments]);

    const handleUnlockChapter = useCallback(async () => {
        if (!urlChapterId) return;
        if (!user?.id) {
            showToast('Vui lòng đăng nhập để mở khóa chương.', 'warning');
            return;
        }
        if (unlocking) return;

        const isLocked = chapter?.isPaidLocked === true;
        if (!isLocked) return; // đã mở

        setUnlocking(true);
        try {
            await unlockPaidChapter(urlChapterId);
            showToast('Mở khóa chương thành công!', 'success');
            await refreshChapterUnlockedState();
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? 'Không thể mở khóa chương.';
            showToast(msg, 'error');
        } finally {
            setUnlocking(false);
        }
    }, [urlChapterId, user?.id, unlocking, chapter?.isPaidLocked, refreshChapterUnlockedState, showToast]);

    const storyForNav = story || { title: '', author: '' };
    const chapterForNav = chapter || {
        number: 0,
        title: '',
        content: '',
        publishedAt: '',
        views: 0,
        words: 0,
        isPaidLocked: false,
        coinPrice: 0,
    };

    const chapterForContent = chapter || {
        number: 0,
        title: 'Không có tiêu đề',
        content: 'Chưa có nội dung.',
        publishedAt: '',
        views: 0,
        words: 0,
        isPaidLocked: false,
        coinPrice: 0,
    };

    const handleBackClick = () => {
        if (onBack) {
            onBack();
        } else {
            window.history.back();
        }
    };

    const handleHomeClick = () => {
        if (onNavigateToStory) {
            onNavigateToStory();
        } else if (urlStoryId) {
            navigate(`/story/${urlStoryId}`);
        } else {
            navigate('/home');
        }
    };

    const currentIndex = allChapters.findIndex((ch) => ch.chapterId === urlChapterId);
    const prevChapter = currentIndex > 0 ? allChapters[currentIndex - 1] : null;
    const nextChapter = currentIndex >= 0 && currentIndex < allChapters.length - 1 ? allChapters[currentIndex + 1] : null;

    const handlePrevChapter = () => {
        if (prevChapter && urlStoryId) {
            navigate(`/chapter?storyId=${encodeURIComponent(urlStoryId)}&chapterId=${encodeURIComponent(prevChapter.chapterId)}`);
        }
    };

    const handleNextChapter = () => {
        if (nextChapter && urlStoryId) {
            navigate(`/chapter?storyId=${encodeURIComponent(urlStoryId)}&chapterId=${encodeURIComponent(nextChapter.chapterId)}`);
        }
    };

    const handleShare = () => {
        if (navigator.share) {
            navigator.share({
                title: `${storyForNav.title} - Chương ${chapterForNav.number}`,
                text: chapterForNav.title,
                url: window.location.href,
            });
        }
    };

    const handleChapterSelect = (ch) => {
        setShowChapterList(false);
        if (ch.chapterId && urlStoryId) {
            navigate(`/chapter?storyId=${encodeURIComponent(urlStoryId)}&chapterId=${encodeURIComponent(ch.chapterId)}`);
        }
    };

    const handleThemeChange = (bg, text) => {
        setBackgroundColor(bg);
        setTextColor(text);
    };

    if (urlStoryId && urlChapterId && loading) {
        return (
            <div style={{ minHeight: '100vh', backgroundColor: '#f8fafc' }}>
                <Header />
                <div className="max-w-3xl mx-auto px-4 py-12 text-center text-slate-500">Đang tải nội dung chương...</div>
                <Footer />
            </div>
        );
    }
    if (urlStoryId && urlChapterId && error) {
        return (
            <div style={{ minHeight: '100vh', backgroundColor: '#f8fafc' }}>
                <Header />
                <div className="max-w-3xl mx-auto px-4 py-12 text-center text-red-500">{error}</div>
                <Footer />
            </div>
        );
    }
    if (!urlStoryId || !urlChapterId) {
        return (
            <div style={{ minHeight: '100vh', backgroundColor: '#f8fafc' }}>
                <Header />
                <div className="max-w-3xl mx-auto px-4 py-12 text-center text-slate-500">
                    Vui lòng chọn chương từ trang chi tiết truyện.
                </div>
                <Footer />
            </div>
        );
    }

    return (
        <div style={{ minHeight: '100vh', backgroundColor: '#f8fafc' }}>
            {/* Header */}
            <Header />
            <ToastContainer />

            {/* Top Navigation Bar */}
            <ChapterNavBar
                story={storyForNav}
                chapter={chapterForNav}
                isBookmarked={isBookmarked}
                onBack={handleBackClick}
                onHome={handleHomeClick}
                onToggleChapterList={() => setShowChapterList(!showChapterList)}
                onToggleSettings={() => setShowSettings(!showSettings)}
                onToggleBookmark={() => setIsBookmarked(!isBookmarked)}
                onShare={handleShare}
            />

            {/* Settings Panel */}
            <ChapterSettings
                show={showSettings}
                fontSize={fontSize}
                fontFamily={fontFamily}
                backgroundColor={backgroundColor}
                textColor={textColor}
                lineHeight={lineHeight}
                onFontSizeChange={setFontSize}
                onFontFamilyChange={setFontFamily}
                onThemeChange={handleThemeChange}
                onLineHeightChange={setLineHeight}
            />

            {/* Chapter List Sidebar */}
            <ChapterSidebar
                show={showChapterList}
                chapters={allChapters}
                currentChapter={chapterForNav.number}
                onClose={() => setShowChapterList(false)}
                onChapterSelect={handleChapterSelect}
            />

            {/* Chapter Content */}
            <ChapterContent
                chapter={chapterForContent}
                fontSize={fontSize}
                fontFamily={fontFamily}
                backgroundColor={backgroundColor}
                textColor={textColor}
                lineHeight={lineHeight}
                onPayClick={handleUnlockChapter}
                isUnlocking={unlocking}
            />

            {/* Navigation Buttons */}
            <ChapterNavigation
                currentChapter={chapterForNav.number}
                totalChapters={allChapters.length}
                onPrevChapter={handlePrevChapter}
                onNextChapter={handleNextChapter}
            />

            {/* Comments Section */}
            <ChapterComments
                comments={comments}
                commentsLoading={commentsLoading}
                commentError={commentError}
                isLoggedIn={!!user?.id && !chapter?.isPaidLocked}
                onSubmitComment={handleSubmitComment}
                onLikeComment={handleLikeComment}
                onReportComment={(id) => console.log('Report comment:', id)}
                formatTimeAgo={formatTimeAgo}
            />

            {/* Footer */}
            <Footer />
        </div>
    );
}
