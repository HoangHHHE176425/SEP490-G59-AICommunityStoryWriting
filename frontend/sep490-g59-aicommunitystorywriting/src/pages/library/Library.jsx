import { useMemo, useState, useEffect } from 'react';
import { Link } from 'react-router-dom';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { ImageWithFallback } from '../../components/figma/ImageWithFallback';
import { Library as LibraryIcon, BookOpen, Clock, ChevronRight, Filter, List, Grid3X3, Star } from 'lucide-react';
import { resolveBackendUrl } from '../../utils/resolveBackendUrl';
import { useAuth } from '../../contexts/AuthContext';
import { getMyLibrary } from '../../api/library/libraryApi';
import { getStoryById } from '../../api/story/storyApi';
import { getProfileByUserId } from '../../api/account/accountApi';

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

export default function Library() {
    const { user } = useAuth();
    const [activeTab, setActiveTab] = useState('reading'); // 'reading' | 'saved' | 'authors'
    const [statusFilter, setStatusFilter] = useState('all'); // all | ongoing | completed
    const [sortBy, setSortBy] = useState('recent'); // recent | title | author | lastRead
    const [search, setSearch] = useState('');
    const [viewMode, setViewMode] = useState('grid'); // grid | list

    const [libraryLoading, setLibraryLoading] = useState(true);
    const [libraryError, setLibraryError] = useState(null);
    const [followedStories, setFollowedStories] = useState([]);
    const [followedAuthors, setFollowedAuthors] = useState([]);
    const [readingHistory, setReadingHistory] = useState([]);
    const [storyVisibilityMap, setStoryVisibilityMap] = useState({});
    const [authorVisibilityMap, setAuthorVisibilityMap] = useState({});

    useEffect(() => {
        if (!user?.id) {
            queueMicrotask(() => {
                setFollowedStories([]);
                setFollowedAuthors([]);
                setReadingHistory([]);
                setStoryVisibilityMap({});
                setAuthorVisibilityMap({});
                setLibraryLoading(false);
                setLibraryError(null);
            });
            return;
        }
        let cancelled = false;
        queueMicrotask(() => {
            if (!cancelled) {
                setLibraryLoading(true);
                setLibraryError(null);
            }
        });
        getMyLibrary()
            .then(({ followedStories: stories, followedAuthors: authors, readingHistory: history }) => {
                if (cancelled) return;
                setFollowedStories(stories ?? []);
                setFollowedAuthors(authors ?? []);
                setReadingHistory(history ?? []);
            })
            .catch((err) => {
                if (!cancelled) {
                    setLibraryError(err?.message ?? 'Không tải được thư viện.');
                    setFollowedStories([]);
                    setFollowedAuthors([]);
                    setReadingHistory([]);
                }
            })
            .finally(() => {
                if (!cancelled) setLibraryLoading(false);
            });
        return () => { cancelled = true; };
    }, [user?.id]);

    useEffect(() => {
        if (!user?.id) {
            setStoryVisibilityMap({});
            setAuthorVisibilityMap({});
            return;
        }
        let cancelled = false;
        const loadVisibility = async () => {
            const storyIds = [...new Set([
                ...followedStories.map((s) => s?.id),
                ...readingHistory.map((h) => h?.storyId),
            ].filter(Boolean))];
            const authorIds = [...new Set(followedAuthors.map((a) => a?.authorId).filter(Boolean))];

            const storyPairs = await Promise.all(storyIds.map(async (storyId) => {
                try {
                    const story = await getStoryById(storyId);
                    const status = String(story?.status ?? story?.Status ?? '').toUpperCase();
                    const complianceHidden = Boolean(
                        story?.complianceHidden
                        ?? story?.ComplianceHidden
                        ?? story?.compliance_hidden
                        ?? false
                    );
                    const visible = status === 'PUBLISHED' && !complianceHidden;
                    return [storyId, visible];
                } catch {
                    // Không đọc được truyện => coi như không nên hiển thị trong tủ sách.
                    return [storyId, false];
                }
            }));

            const authorPairs = await Promise.all(authorIds.map(async (authorId) => {
                try {
                    const profile = await getProfileByUserId(authorId);
                    const status = String(profile?.status ?? '').toUpperCase();
                    const isBanned = Boolean(profile?.isBanned) || status === 'BANNED';
                    return [authorId, !isBanned];
                } catch {
                    // Không tải được profile thì giữ hiển thị để tránh ẩn nhầm.
                    return [authorId, true];
                }
            }));

            if (cancelled) return;
            setStoryVisibilityMap(Object.fromEntries(storyPairs));
            setAuthorVisibilityMap(Object.fromEntries(authorPairs));
        };
        loadVisibility();
        return () => { cancelled = true; };
    }, [user?.id, followedStories, followedAuthors, readingHistory]);

    const visibleFollowedStories = useMemo(
        () => followedStories.filter((s) => storyVisibilityMap[s.id] !== false),
        [followedStories, storyVisibilityMap],
    );
    const visibleReadingHistory = useMemo(
        () => readingHistory.filter((h) => storyVisibilityMap[h.storyId] !== false),
        [readingHistory, storyVisibilityMap],
    );
    const visibleFollowedAuthors = useMemo(
        () => followedAuthors.filter((a) => authorVisibilityMap[a.authorId] !== false),
        [followedAuthors, authorVisibilityMap],
    );

    const totalBooks = visibleFollowedStories.length;
    const readingCount = visibleReadingHistory.length;
    const followedAuthorsCount = visibleFollowedAuthors.length;

    const filteredBooks = useMemo(() => {
        let items = visibleFollowedStories.map((s) => ({
            id: s.id,
            title: s.title,
            author: s.authorName ?? '',
            coverImage: s.coverImage,
            totalChapters: s.publishedChaptersCount ?? 0,
            status: (s.status || '').toLowerCase() === 'completed' ? 'completed' : 'ongoing',
            latestUpdatedAt: s.latestUpdatedAt,
        }));

        if (statusFilter !== 'all') {
            items = items.filter((b) => b.status === statusFilter);
        }

        if (search.trim()) {
            const q = search.trim().toLowerCase();
            items = items.filter(
                (b) =>
                    (b.title || '').toLowerCase().includes(q) ||
                    (b.author || '').toLowerCase().includes(q)
            );
        }

        const parseDate = (s) => (s ? new Date(s).getTime() : 0);
        items.sort((a, b) => {
            if (sortBy === 'title') return (a.title || '').localeCompare(b.title || '', 'vi');
            if (sortBy === 'author') return (a.author || '').localeCompare(b.author || '', 'vi');
            if (sortBy === 'lastRead') return parseDate(b.latestUpdatedAt) - parseDate(a.latestUpdatedAt);
            return parseDate(b.latestUpdatedAt) - parseDate(a.latestUpdatedAt);
        });

        return items;
    }, [visibleFollowedStories, statusFilter, sortBy, search]);

    if (!user?.id) {
        return (
            <div className="min-h-screen bg-background-light dark:bg-background-dark flex flex-col">
                <Header />
                <div className="flex-1 flex items-center justify-center px-4">
                    <div className="text-center py-12">
                        <LibraryIcon className="w-16 h-16 text-slate-300 dark:text-slate-600 mx-auto mb-4" />
                        <h2 className="text-xl font-bold text-slate-800 dark:text-white mb-2">Đăng nhập để xem tủ sách</h2>
                        <p className="text-slate-500 dark:text-slate-400 mb-6 max-w-md mx-auto">
                            Truyện theo dõi, tác giả theo dõi và lịch sử đọc của bạn sẽ hiển thị tại đây.
                        </p>
                        <Link
                            to="/login"
                            className="inline-flex items-center gap-2 px-6 py-3 bg-primary text-white font-semibold rounded-lg hover:bg-primary/90"
                        >
                            Đăng nhập
                            <ChevronRight className="w-4 h-4" />
                        </Link>
                    </div>
                </div>
                <Footer />
            </div>
        );
    }

    if (libraryLoading) {
        return (
            <div className="min-h-screen bg-background-light dark:bg-background-dark flex flex-col">
                <Header />
                <div className="flex-1 flex items-center justify-center px-4">
                    <p className="text-slate-500 dark:text-slate-400">Đang tải thư viện...</p>
                </div>
                <Footer />
            </div>
        );
    }

    if (libraryError) {
        return (
            <div className="min-h-screen bg-background-light dark:bg-background-dark flex flex-col">
                <Header />
                <div className="flex-1 flex items-center justify-center px-4">
                    <div className="text-center py-12">
                        <p className="text-red-500 dark:text-red-400 mb-4">{libraryError}</p>
                        <button
                            type="button"
                            onClick={() => window.location.reload()}
                            className="px-4 py-2 bg-primary text-white font-semibold rounded-lg hover:bg-primary/90"
                        >
                            Thử lại
                        </button>
                    </div>
                </div>
                <Footer />
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-background-light dark:bg-background-dark flex flex-col">
            <Header />
            <div className="flex-1">
                <div className="max-w-[1280px] mx-auto px-4 py-8">
                    {/* Header */}
                    <div className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-6 mb-6 border border-slate-200 dark:border-slate-700">
                        <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between">
                            <div className="flex items-center gap-3">
                                <div className="size-12 rounded-full bg-primary/10 flex items-center justify-center">
                                    <LibraryIcon className="w-6 h-6 text-primary" />
                                </div>
                                <div>
                                    <h1 className="text-xl font-bold text-slate-900 dark:text-white">Tủ sách</h1>
                                    <p className="text-sm text-slate-500 dark:text-slate-400">
                                        Truyện đang đọc và truyện bạn đã lưu
                                    </p>
                                </div>
                            </div>
                            <div className="flex flex-wrap gap-3 text-xs text-slate-600 dark:text-slate-300">
                                <div className="px-3 py-1 rounded-full bg-slate-100 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-700">
                                    <span className="font-semibold">{totalBooks}</span> truyện theo dõi
                                </div>
                                <div className="px-3 py-1 rounded-full bg-amber-50 dark:bg-amber-900/30 border border-amber-200 dark:border-amber-800 text-amber-800 dark:text-amber-200">
                                    <span className="font-semibold">{readingCount}</span> đang đọc
                                </div>
                                <div className="px-3 py-1 rounded-full bg-emerald-50 dark:bg-emerald-900/30 border border-emerald-200 dark:border-emerald-800 text-emerald-800 dark:text-emerald-200">
                                    <span className="font-semibold">{visibleFollowedStories.filter((b) => (b.status || '').toLowerCase() === 'completed').length}</span> đã hoàn thành
                                </div>
                                <div className="px-3 py-1 rounded-full bg-blue-50 dark:bg-blue-900/30 border border-blue-200 dark:border-blue-800 text-blue-800 dark:text-blue-200">
                                    <span className="font-semibold">{followedAuthorsCount}</span> tác giả đã theo dõi
                                </div>
                            </div>
                        </div>
                    </div>

                    {/* Tabs */}
                    <div className="mb-6 border-b border-slate-200 dark:border-slate-700">
                        <div className="flex gap-6">
                            <button
                                type="button"
                                onClick={() => setActiveTab('reading')}
                                className={`flex items-center gap-2 px-4 py-3 font-semibold text-sm border-b-2 transition-colors ${activeTab === 'reading'
                                    ? 'text-primary border-primary'
                                    : 'text-slate-500 dark:text-slate-400 border-transparent hover:text-primary'
                                    }`}
                            >
                                <Clock className="w-5 h-5" />
                                Tiếp tục đọc
                            </button>
                            <button
                                type="button"
                                onClick={() => setActiveTab('saved')}
                                className={`flex items-center gap-2 px-4 py-3 font-semibold text-sm border-b-2 transition-colors ${activeTab === 'saved'
                                    ? 'text-primary border-primary'
                                    : 'text-slate-500 dark:text-slate-400 border-transparent hover:text-primary'
                                    }`}
                            >
                                <BookOpen className="w-5 h-5" />
                                Truyện theo dõi
                            </button>
                            <button
                                type="button"
                                onClick={() => setActiveTab('authors')}
                                className={`flex items-center gap-2 px-4 py-3 font-semibold text-sm border-b-2 transition-colors ${activeTab === 'authors'
                                    ? 'text-primary border-primary'
                                    : 'text-slate-500 dark:text-slate-400 border-transparent hover:text-primary'
                                    }`}
                            >
                                <Star className="w-5 h-5" />
                                Tác giả theo dõi
                            </button>
                        </div>
                    </div>

                    {/* Nội dung theo tab */}
                    {activeTab === 'reading' && (
                        <section className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-6 border border-slate-200 dark:border-slate-700">
                            <h2 className="text-lg font-bold text-slate-900 dark:text-white mb-4">
                                Tiếp tục đọc
                            </h2>
                            <p className="text-sm text-slate-500 dark:text-slate-400 mb-6">
                                Các truyện bạn đang đọc dở, nhấn vào để đọc tiếp chương mới nhất.
                            </p>
                            {visibleReadingHistory.length === 0 ? (
                                <div className="text-center py-12 text-slate-500 dark:text-slate-400">
                                    <BookOpen className="w-12 h-12 mx-auto mb-3 opacity-50" />
                                    <p>Bạn chưa đọc truyện nào. Khám phá truyện hay và bắt đầu đọc nhé.</p>
                                    <Link
                                        to="/home"
                                        className="inline-flex items-center gap-2 mt-4 px-4 py-2 bg-primary text-white font-semibold rounded-lg hover:bg-primary/90"
                                    >
                                        Khám phá truyện
                                        <ChevronRight className="w-4 h-4" />
                                    </Link>
                                </div>
                            ) : (
                                <div className="space-y-4">
                                    {visibleReadingHistory.map((item) => (
                                        <Link
                                            key={`${item.storyId}-${item.lastReadChapterId}`}
                                            to={`/chapter?storyId=${encodeURIComponent(item.storyId)}&chapterId=${encodeURIComponent(item.lastReadChapterId)}`}
                                            className="flex gap-4 p-4 rounded-xl border border-slate-200 dark:border-slate-700 hover:border-primary hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-all"
                                        >
                                            <div className="w-20 h-28 rounded-lg overflow-hidden bg-slate-200 dark:bg-slate-700 flex-shrink-0">
                                                <ImageWithFallback
                                                    src={resolveBackendUrl(item.coverImage) || ''}
                                                    alt={item.storyTitle}
                                                    className="w-full h-full object-cover"
                                                />
                                            </div>
                                            <div className="flex-1 min-w-0">
                                                <h3 className="font-semibold text-slate-900 dark:text-white truncate">
                                                    {item.storyTitle}
                                                </h3>
                                                <p className="text-sm text-slate-600 dark:text-slate-300 mt-1">
                                                    {item.lastReadChapterTitle ? `Chương: ${item.lastReadChapterTitle}` : 'Đọc tiếp'}
                                                </p>
                                                <p className="text-xs text-slate-400 mt-1">
                                                    Đọc {formatTimeAgo(item.lastReadAt)}
                                                </p>
                                            </div>
                                            <ChevronRight className="w-5 h-5 text-slate-400 flex-shrink-0 self-center" />
                                        </Link>
                                    ))}
                                </div>
                            )}
                        </section>
                    )}

                    {activeTab === 'saved' && (
                        <section className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-6 border border-slate-200 dark:border-slate-700">
                            <div className="flex flex-col gap-4 md:flex-row md:items-center md:justify-between mb-4">
                                <div>
                                    <h2 className="text-lg font-bold text-slate-900 dark:text-white">
                                        Truyện theo dõi
                                    </h2>
                                    <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
                                        Truyện bạn đang theo dõi, truy cập nhanh để đọc tiếp.
                                    </p>
                                </div>
                                {/* View mode toggle */}
                                <div className="inline-flex items-center rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900/40 overflow-hidden text-xs">
                                    <button
                                        type="button"
                                        onClick={() => setViewMode('grid')}
                                        className={`flex items-center gap-1 px-3 py-1.5 transition-colors ${viewMode === 'grid'
                                            ? 'bg-primary text-white'
                                            : 'text-slate-600 dark:text-slate-300'
                                            }`}
                                    >
                                        <Grid3X3 className="w-4 h-4" />
                                        Lưới
                                    </button>
                                    <button
                                        type="button"
                                        onClick={() => setViewMode('list')}
                                        className={`flex items-center gap-1 px-3 py-1.5 transition-colors ${viewMode === 'list'
                                            ? 'bg-primary text-white'
                                            : 'text-slate-600 dark:text-slate-300'
                                            }`}
                                    >
                                        <List className="w-4 h-4" />
                                        Danh sách
                                    </button>
                                </div>
                            </div>

                            {/* Bộ lọc & sắp xếp */}
                            <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between mb-6">
                                <div className="flex flex-wrap items-center gap-2 text-xs">
                                    <span className="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-slate-100 dark:bg-slate-900/60 text-slate-600 dark:text-slate-300">
                                        <Filter className="w-3 h-3" />
                                        Bộ lọc
                                    </span>
                                    <select
                                        value={statusFilter}
                                        onChange={(e) => setStatusFilter(e.target.value)}
                                        className="rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 px-3 py-1.5 text-xs text-slate-700 dark:text-slate-200 focus:ring-2 focus:ring-primary/40 focus:border-primary outline-none"
                                    >
                                        <option value="all">Tất cả trạng thái</option>
                                        <option value="ongoing">Đang đọc / Đang ra</option>
                                        <option value="completed">Đã hoàn thành</option>
                                    </select>
                                </div>
                                <div className="flex flex-col gap-2 md:flex-row md:items-center md:gap-3 w-full md:w-auto">
                                    <div className="relative flex-1 md:w-56">
                                        <input
                                            type="text"
                                            value={search}
                                            onChange={(e) => setSearch(e.target.value)}
                                            placeholder="Tìm theo tên truyện, tác giả..."
                                            className="w-full rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900 px-3 py-2 text-xs text-slate-700 dark:text-slate-200 focus:ring-2 focus:ring-primary/40 focus:border-primary outline-none"
                                        />
                                        {search && (
                                            <button
                                                type="button"
                                                onClick={() => setSearch('')}
                                                className="absolute inset-y-0 right-0 px-3 text-slate-400 hover:text-slate-200 text-xs"
                                            >
                                                Xóa
                                            </button>
                                        )}
                                    </div>
                                    <select
                                        value={sortBy}
                                        onChange={(e) => setSortBy(e.target.value)}
                                        className="rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 px-3 py-1.5 text-xs text-slate-700 dark:text-slate-200 focus:ring-2 focus:ring-primary/40 focus:border-primary outline-none"
                                    >
                                        <option value="recent">Mới thêm gần đây</option>
                                        <option value="lastRead">Đọc gần đây</option>
                                        <option value="title">Tên A–Z</option>
                                        <option value="author">Tác giả A–Z</option>
                                    </select>
                                </div>
                            </div>

                            {/* Nội dung tủ sách */}
                            {filteredBooks.length === 0 ? (
                                <div className="text-center py-12 text-slate-500 dark:text-slate-400">
                                    <BookOpen className="w-12 h-12 mx-auto mb-3 opacity-50" />
                                    <p>Không tìm thấy truyện nào khớp với bộ lọc hiện tại.</p>
                                    <Link
                                        to="/home"
                                        className="inline-flex items-center gap-2 mt-4 px-4 py-2 bg-primary text-white font-semibold rounded-lg hover:bg-primary/90"
                                    >
                                        Khám phá truyện
                                        <ChevronRight className="w-4 h-4" />
                                    </Link>
                                </div>
                            ) : viewMode === 'grid' ? (
                                <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 lg:grid-cols-5 gap-4 mb-8">
                                    {filteredBooks.map((book) => (
                                        <div
                                            key={book.id}
                                            className="group rounded-xl border border-slate-200 dark:border-slate-700 overflow-hidden hover:border-primary hover:shadow-lg transition-all flex flex-col"
                                        >
                                            <Link
                                                to={`/story/${book.id}`}
                                                className="block aspect-[3/4] bg-slate-200 dark:bg-slate-700 relative"
                                            >
                                                <ImageWithFallback
                                                    src={resolveBackendUrl(book.coverImage) || ''}
                                                    alt={book.title}
                                                    className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                                                />
                                                <span
                                                    className={`absolute top-2 right-2 px-2 py-0.5 text-xs font-semibold rounded ${book.status === 'completed'
                                                        ? 'bg-emerald-500/90 text-white'
                                                        : 'bg-amber-500/90 text-white'
                                                        }`}
                                                >
                                                    {book.status === 'completed' ? 'Full' : 'Đang ra'}
                                                </span>
                                            </Link>
                                            <div className="p-3 flex-1 flex flex-col gap-1">
                                                <h3 className="font-semibold text-slate-900 dark:text-white text-sm line-clamp-2">
                                                    {book.title}
                                                </h3>
                                                <p className="text-xs text-slate-500 dark:text-slate-400">
                                                    {book.author}
                                                </p>
                                                <p className="text-xs text-slate-400">
                                                    {book.totalChapters} chương
                                                </p>
                                                {/* Hành động nhanh */}
                                                <div className="mt-2 flex items-center justify-between text-[11px] text-slate-500 dark:text-slate-400">
                                                    <Link
                                                        to={`/story/${book.id}`}
                                                        className="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-primary/10 text-primary hover:bg-primary/20"
                                                    >
                                                        <BookOpen className="w-3 h-3" />
                                                        Đọc truyện
                                                    </Link>
                                                </div>
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            ) : (
                                <div className="space-y-4 mb-8">
                                    {filteredBooks.map((book) => (
                                        <div
                                            key={book.id}
                                            className="flex gap-4 rounded-xl border border-slate-200 dark:border-slate-700 p-4 hover:border-primary hover:bg-slate-50 dark:hover:bg-slate-800/60 transition-all"
                                        >
                                            <Link
                                                to={`/story/${book.id}`}
                                                className="w-20 h-28 rounded-lg overflow-hidden bg-slate-200 dark:bg-slate-700 flex-shrink-0"
                                            >
                                                <ImageWithFallback
                                                    src={resolveBackendUrl(book.coverImage) || ''}
                                                    alt={book.title}
                                                    className="w-full h-full object-cover"
                                                />
                                            </Link>
                                            <div className="flex-1 min-w-0">
                                                <div className="flex items-start justify-between gap-3">
                                                    <div>
                                                        <h3 className="font-semibold text-slate-900 dark:text-white text-sm">
                                                            {book.title}
                                                        </h3>
                                                        <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5">
                                                            {book.author}
                                                        </p>
                                                    </div>
                                                    <span
                                                        className={`px-2 py-0.5 text-xs font-semibold rounded-full ${book.status === 'completed'
                                                            ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-200'
                                                            : 'bg-amber-50 text-amber-700 dark:bg-amber-900/30 dark:text-amber-200'
                                                            }`}
                                                    >
                                                        {book.status === 'completed' ? 'Hoàn thành' : 'Đang ra'}
                                                    </span>
                                                </div>
                                                <p className="text-xs text-slate-400 mt-1">
                                                    {book.totalChapters} chương
                                                    {book.latestUpdatedAt && ` · Cập nhật ${formatTimeAgo(book.latestUpdatedAt)}`}
                                                </p>
                                                <div className="mt-3 flex flex-wrap items-center gap-2 text-[11px] text-slate-500 dark:text-slate-400">
                                                    <Link
                                                        to={`/story/${book.id}`}
                                                        className="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-primary/10 text-primary hover:bg-primary/20"
                                                    >
                                                        <BookOpen className="w-3 h-3" />
                                                        Đọc truyện
                                                    </Link>
                                                </div>
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            )}

                        </section>
                    )}

                    {activeTab === 'authors' && (
                        <section className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-6 border border-slate-200 dark:border-slate-700">
                            <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between mb-6">
                                <div>
                                    <h2 className="text-lg font-bold text-slate-900 dark:text-white">
                                        Tác giả bạn đã theo dõi
                                    </h2>
                                    <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
                                        Danh sách các tác giả bạn quan tâm. Sau này sẽ được cập nhật tự động từ hệ thống follow.
                                    </p>
                                </div>
                            </div>

                            {visibleFollowedAuthors.length === 0 ? (
                                <div className="text-center py-12 text-slate-500 dark:text-slate-400">
                                    <Star className="w-12 h-12 mx-auto mb-3 opacity-50" />
                                    <p>Bạn chưa theo dõi tác giả nào. Hãy vào trang truyện hoặc trang tác giả để nhấn Theo dõi.</p>
                                    <Link
                                        to="/home"
                                        className="inline-flex items-center gap-2 mt-4 px-4 py-2 bg-primary text-white font-semibold rounded-lg hover:bg-primary/90"
                                    >
                                        Khám phá tác giả
                                        <ChevronRight className="w-4 h-4" />
                                    </Link>
                                </div>
                            ) : (
                                <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
                                    {visibleFollowedAuthors.map((author) => (
                                        <div
                                            key={author.authorId}
                                            className="flex gap-4 rounded-xl border border-slate-200 dark:border-slate-700 p-4 hover:border-primary hover:bg-slate-50 dark:hover:bg-slate-800/60 transition-all"
                                        >
                                            <Link
                                                to={`/authors/${author.authorId}`}
                                                className="w-14 h-14 rounded-full overflow-hidden bg-slate-200 dark:bg-slate-700 flex-shrink-0 flex items-center justify-center text-lg font-bold text-slate-600 dark:text-slate-200 hover:ring-2 hover:ring-primary/40"
                                            >
                                                {(author.authorName || 'A').charAt(0).toUpperCase()}
                                            </Link>
                                            <div className="flex-1 min-w-0">
                                                <div className="flex items-start justify-between gap-2">
                                                    <div>
                                                        <Link
                                                            to={`/authors/${author.authorId}`}
                                                            className="font-semibold text-slate-900 dark:text-white text-sm line-clamp-1 hover:text-primary"
                                                        >
                                                            {author.authorName}
                                                        </Link>
                                                    </div>
                                                </div>
                                                <div className="mt-3 flex flex-wrap gap-2 text-xs">
                                                    <span className="inline-flex items-center gap-1 px-3 py-1.5 rounded-full bg-primary/10 text-primary font-semibold">
                                                        <Star className="w-3 h-3" />
                                                        Đang theo dõi
                                                    </span>
                                                    <Link
                                                        to={`/authors/${author.authorId}`}
                                                        className="inline-flex items-center gap-1 px-3 py-1.5 rounded-full border border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800"
                                                    >
                                                        Xem trang tác giả
                                                    </Link>
                                                </div>
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            )}
                        </section>
                    )}
                </div>
            </div>
            <Footer />
        </div>
    );
}
