import { useState, useEffect } from 'react';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { ChapterNavBar } from '../../components/chapter-detail/ChapterNavBar';
import { ChapterSettings } from '../../components/chapter-detail/ChapterSettings';
import { ChapterSidebar } from '../../components/chapter-detail/ChapterSidebar';
import { ChapterContent } from '../../components/chapter-detail/ChapterContent';
import { ChapterNavigation } from '../../components/chapter-detail/ChapterNavigation';
import { ChapterComments } from '../../components/chapter-detail/ChapterComments';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { getStoryById } from '../../api/story/storyApi';
import { getChapterById, getChapters } from '../../api/chapter/chapterApi';

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
    const urlStoryId = searchParams.get('storyId');
    const urlChapterId = searchParams.get('chapterId');

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
                    const content = chapterRes?.content ?? chapterRes?.Content ?? '';
                    const wordCount = (content.trim().split(/\s+/).filter(Boolean).length) || 0;
                    setChapter({
                        number: orderIndex + 1,
                        title: chapterRes?.title ?? chapterRes?.Title ?? 'Không có tiêu đề',
                        content: content || 'Chưa có nội dung.',
                        publishedAt: chapterRes?.publishedAt ?? chapterRes?.PublishedAt ?? chapterRes?.updatedAt ? formatTimeAgo(chapterRes.updatedAt ?? chapterRes.UpdatedAt) : '',
                        views: Number(chapterRes?.viewCount ?? chapterRes?.ViewCount ?? 0) || 0,
                        words: wordCount,
                    });
                    setAllChapters(rawChapters.map((ch, idx) => ({
                        number: (ch.orderIndex ?? ch.OrderIndex ?? idx) + 1,
                        title: ch.title ?? ch.Title ?? `Chương ${idx + 1}`,
                        chapterId: ch.id ?? ch.Id,
                        isLocked: false,
                    })));
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
    }, [urlStoryId, urlChapterId]);

    const storyForNav = story || { title: '', author: '' };
    const chapterForNav = chapter || {
        number: 0,
        title: '',
        content: '',
        publishedAt: '',
        views: 0,
        words: 0,
    };

    const chapterForContent = chapter || {
        number: 0,
        title: 'Không có tiêu đề',
        content: 'Chưa có nội dung.',
        publishedAt: '',
        views: 0,
        words: 0,
    };

    const comments = [
        {
            id: 1,
            user: { name: 'Độc Giả 123', avatar: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=50&h=50&fit=crop' },
            content: 'Chương này hay quá! Trận chiến với Ma Đế được miêu tả rất sống động và hấp dẫn!',
            time: '3 giờ trước',
            likes: 234,
        },
        {
            id: 2,
            user: { name: 'Phong Vân', avatar: 'https://images.unsplash.com/photo-1599566150163-29194dcaad36?w=50&h=50&fit=crop' },
            content: 'Tác giả viết văn rất hay, cảm xúc nhân vật được thể hiện rõ ràng. Mong chờ chương tiếp theo!',
            time: '5 giờ trước',
            likes: 189,
        },
        {
            id: 3,
            user: { name: 'Long Thiên', avatar: 'https://images.unsplash.com/photo-1527980965255-d3b416303d12?w=50&h=50&fit=crop' },
            content: 'Phần chiến đấu quá đỉnh! Đọc xong muốn xem tiếp luôn 🔥',
            time: '6 giờ trước',
            likes: 156,
        },
    ];

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
                onReportComment={(id) => console.log('Report comment:', id)}
            />

            {/* Footer */}
            <Footer />
        </div>
    );
}
