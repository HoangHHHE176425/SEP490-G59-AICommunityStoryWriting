import { useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { ImageWithFallback } from '../../components/figma/ImageWithFallback';
import { Library as LibraryIcon, BookOpen, Clock, ChevronRight, Filter, List, Grid3X3, Star, Trash2 } from 'lucide-react';
import { resolveBackendUrl } from '../../utils/resolveBackendUrl';

// Mock: danh sách "tiếp tục đọc" – sau này lấy từ API (reading history)
const MOCK_CONTINUE_READING = [
    {
        storyId: '1',
        chapterId: 'c1',
        title: 'Kiếm Thánh',
        author: 'Tác giả A',
        coverImage: '',
        lastChapterTitle: 'Chương 5: Khai mạc',
        lastReadAt: '2026-02-10',
        progressPercent: 45,
    },
    {
        storyId: '2',
        chapterId: 'c2',
        title: 'Tu Tiên Ký',
        author: 'Tác giả B',
        coverImage: '',
        lastChapterTitle: 'Chương 12: Đột phá',
        lastReadAt: '2026-02-09',
        progressPercent: 20,
    },
];

// Mock: tủ sách (truyện đã lưu / đã mua) – sau này lấy từ API
const MOCK_MY_BOOKS = [
    {
        id: '1',
        title: 'Kiếm Thánh',
        author: 'Tác giả A',
        coverImage: '',
        totalChapters: 120,
        status: 'ongoing', // ongoing | completed
        tags: ['Đang đọc', 'Yêu thích'],
        addedAt: '2026-02-01',
        lastReadAt: '2026-02-10',
    },
    {
        id: '2',
        title: 'Tu Tiên Ký',
        author: 'Tác giả B',
        coverImage: '',
        totalChapters: 80,
        status: 'ongoing',
        tags: ['Đọc sau'],
        addedAt: '2026-02-05',
        lastReadAt: '2026-02-08',
    },
    {
        id: '3',
        title: 'Đấu Phá Thương Khung',
        author: 'Tác giả C',
        coverImage: '',
        totalChapters: 500,
        status: 'completed',
        tags: ['Hoàn thành'],
        addedAt: '2026-01-20',
        lastReadAt: '2026-02-01',
    },
];

// Mock: danh sách tác giả đã follow – sau này lấy từ API
const MOCK_FOLLOWED_AUTHORS = [
    {
        id: 'author-1',
        name: 'Tác giả A',
        avatarUrl: '',
        bio: 'Chuyên viết kiếm hiệp, tu tiên với phong cách hiện đại.',
        totalStories: 5,
        totalFollowers: 12340,
    },
    {
        id: 'author-2',
        name: 'Tác giả B',
        avatarUrl: '',
        bio: 'Tác giả truyện ngôn tình nhẹ nhàng, healing.',
        totalStories: 8,
        totalFollowers: 8520,
    },
    {
        id: 'author-3',
        name: 'Tác giả C',
        avatarUrl: '',
        bio: 'Tác giả fantasy dài tập, thế giới đồ sộ.',
        totalStories: 3,
        totalFollowers: 4321,
    },
];

export default function Library() {
    const [activeTab, setActiveTab] = useState('reading'); // 'reading' | 'saved' | 'authors'
    const [statusFilter, setStatusFilter] = useState('all'); // all | ongoing | completed
    const [tagFilter, setTagFilter] = useState('all'); // all | favorite | reading | later
    const [sortBy, setSortBy] = useState('recent'); // recent | title | author | lastRead
    const [search, setSearch] = useState('');
    const [viewMode, setViewMode] = useState('grid'); // grid | list

    // Mock stats – sau này lấy từ API / BE
    const totalBooks = MOCK_MY_BOOKS.length;
    const readingCount = MOCK_MY_BOOKS.filter((b) => b.status === 'ongoing').length;
    const completedCount = MOCK_MY_BOOKS.filter((b) => b.status === 'completed').length;
    const followedAuthorsCount = MOCK_FOLLOWED_AUTHORS.length;

    const filteredBooks = useMemo(() => {
        let items = [...MOCK_MY_BOOKS];

        // Lọc trạng thái
        if (statusFilter !== 'all') {
            items = items.filter((b) => b.status === statusFilter);
        }

        // Lọc theo tag logic đơn giản (map từ UI -> tags)
        if (tagFilter !== 'all') {
            const map = {
                favorite: 'Yêu thích',
                reading: 'Đang đọc',
                later: 'Đọc sau',
            };
            const tagLabel = map[tagFilter];
            items = items.filter((b) => (b.tags || []).includes(tagLabel));
        }

        // Tìm kiếm theo tên truyện / tác giả
        if (search.trim()) {
            const q = search.trim().toLowerCase();
            items = items.filter(
                (b) =>
                    b.title.toLowerCase().includes(q) ||
                    b.author.toLowerCase().includes(q)
            );
        }

        // Sắp xếp
        const parseDate = (s) => (s ? new Date(s).getTime() : 0);
        items.sort((a, b) => {
            if (sortBy === 'title') {
                return a.title.localeCompare(b.title, 'vi');
            }
            if (sortBy === 'author') {
                return a.author.localeCompare(b.author, 'vi');
            }
            if (sortBy === 'lastRead') {
                return parseDate(b.lastReadAt) - parseDate(a.lastReadAt);
            }
            // recent = theo ngày thêm
            return parseDate(b.addedAt) - parseDate(a.addedAt);
        });

        return items;
    }, [statusFilter, tagFilter, sortBy, search]);

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
                                    <span className="font-semibold">{totalBooks}</span> truyện trong tủ sách
                                </div>
                                <div className="px-3 py-1 rounded-full bg-amber-50 dark:bg-amber-900/30 border border-amber-200 dark:border-amber-800 text-amber-800 dark:text-amber-200">
                                    <span className="font-semibold">{readingCount}</span> đang đọc
                                </div>
                                <div className="px-3 py-1 rounded-full bg-emerald-50 dark:bg-emerald-900/30 border border-emerald-200 dark:border-emerald-800 text-emerald-800 dark:text-emerald-200">
                                    <span className="font-semibold">{completedCount}</span> đã hoàn thành
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
                                className={`flex items-center gap-2 px-4 py-3 font-semibold text-sm border-b-2 transition-colors ${
                                    activeTab === 'reading'
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
                                className={`flex items-center gap-2 px-4 py-3 font-semibold text-sm border-b-2 transition-colors ${
                                    activeTab === 'saved'
                                        ? 'text-primary border-primary'
                                        : 'text-slate-500 dark:text-slate-400 border-transparent hover:text-primary'
                                }`}
                            >
                                <BookOpen className="w-5 h-5" />
                                Tủ sách của tôi
                            </button>
                            <button
                                type="button"
                                onClick={() => setActiveTab('authors')}
                                className={`flex items-center gap-2 px-4 py-3 font-semibold text-sm border-b-2 transition-colors ${
                                    activeTab === 'authors'
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
                            {MOCK_CONTINUE_READING.length === 0 ? (
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
                                    {MOCK_CONTINUE_READING.map((item) => (
                                        <Link
                                            key={`${item.storyId}-${item.chapterId}`}
                                            to={`/chapter?storyId=${encodeURIComponent(item.storyId)}&chapterId=${encodeURIComponent(item.chapterId)}`}
                                            className="flex gap-4 p-4 rounded-xl border border-slate-200 dark:border-slate-700 hover:border-primary hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-all"
                                        >
                                            <div className="w-20 h-28 rounded-lg overflow-hidden bg-slate-200 dark:bg-slate-700 flex-shrink-0">
                                                <ImageWithFallback
                                                    src={resolveBackendUrl(item.coverImage) || ''}
                                                    alt={item.title}
                                                    className="w-full h-full object-cover"
                                                />
                                            </div>
                                            <div className="flex-1 min-w-0">
                                                <h3 className="font-semibold text-slate-900 dark:text-white truncate">
                                                    {item.title}
                                                </h3>
                                                <p className="text-sm text-slate-500 dark:text-slate-400">
                                                    {item.author}
                                                </p>
                                                <p className="text-sm text-slate-600 dark:text-slate-300 mt-1">
                                                    {item.lastChapterTitle}
                                                </p>
                                                <p className="text-xs text-slate-400 mt-1">
                                                    Đọc lúc {item.lastReadAt}
                                                </p>
                                                <div className="mt-2 h-1.5 w-full max-w-[200px] rounded-full bg-slate-200 dark:bg-slate-600 overflow-hidden">
                                                    <div
                                                        className="h-full bg-primary rounded-full transition-all"
                                                        style={{ width: `${item.progressPercent}%` }}
                                                    />
                                                </div>
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
                                        Tủ sách của tôi
                                    </h2>
                                    <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
                                        Truyện bạn đã lưu hoặc đã mua, truy cập nhanh để đọc tiếp.
                                    </p>
                                </div>
                                {/* View mode toggle */}
                                <div className="inline-flex items-center rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900/40 overflow-hidden text-xs">
                                    <button
                                        type="button"
                                        onClick={() => setViewMode('grid')}
                                        className={`flex items-center gap-1 px-3 py-1.5 transition-colors ${
                                            viewMode === 'grid'
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
                                        className={`flex items-center gap-1 px-3 py-1.5 transition-colors ${
                                            viewMode === 'list'
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
                                    <select
                                        value={tagFilter}
                                        onChange={(e) => setTagFilter(e.target.value)}
                                        className="rounded-lg border border-slate-200 dark:border-slate-700 bg-white dark:bg-slate-900 px-3 py-1.5 text-xs text-slate-700 dark:text-slate-200 focus:ring-2 focus:ring-primary/40 focus:border-primary outline-none"
                                    >
                                        <option value="all">Tất cả nhãn</option>
                                        <option value="favorite">Yêu thích</option>
                                        <option value="reading">Đang đọc</option>
                                        <option value="later">Đọc sau</option>
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
                                                    className={`absolute top-2 right-2 px-2 py-0.5 text-xs font-semibold rounded ${
                                                        book.status === 'completed'
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
                                                <div className="mt-1 flex flex-wrap gap-1 text-[10px] text-slate-500 dark:text-slate-400">
                                                    {(book.tags || []).map((tag) => (
                                                        <span
                                                            key={tag}
                                                            className="px-1.5 py-0.5 rounded-full bg-slate-100 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-700"
                                                        >
                                                            {tag}
                                                        </span>
                                                    ))}
                                                </div>
                                                {/* Hành động nhanh */}
                                                <div className="mt-2 flex items-center justify-between text-[11px] text-slate-500 dark:text-slate-400">
                                                    <button
                                                        type="button"
                                                        className="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-primary/10 text-primary hover:bg-primary/20"
                                                    >
                                                        <BookOpen className="w-3 h-3" />
                                                        Đọc tiếp
                                                    </button>
                                                    <button
                                                        type="button"
                                                        className="inline-flex items-center gap-1 px-2 py-1 rounded-full hover:bg-slate-100 dark:hover:bg-slate-800"
                                                    >
                                                        <Star className="w-3 h-3" />
                                                        Yêu thích
                                                    </button>
                                                    <button
                                                        type="button"
                                                        className="inline-flex items-center gap-1 px-2 py-1 rounded-full hover:bg-red-50 dark:hover:bg-red-900/30 text-red-500"
                                                    >
                                                        <Trash2 className="w-3 h-3" />
                                                        Xóa
                                                    </button>
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
                                                        className={`px-2 py-0.5 text-xs font-semibold rounded-full ${
                                                            book.status === 'completed'
                                                                ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-200'
                                                                : 'bg-amber-50 text-amber-700 dark:bg-amber-900/30 dark:text-amber-200'
                                                        }`}
                                                    >
                                                        {book.status === 'completed' ? 'Hoàn thành' : 'Đang ra'}
                                                    </span>
                                                </div>
                                                <p className="text-xs text-slate-400 mt-1">
                                                    {book.totalChapters} chương · Thêm vào ngày {book.addedAt}
                                                </p>
                                                <div className="mt-1 flex flex-wrap gap-1 text-[10px] text-slate-500 dark:text-slate-400">
                                                    {(book.tags || []).map((tag) => (
                                                        <span
                                                            key={tag}
                                                            className="px-1.5 py-0.5 rounded-full bg-slate-100 dark:bg-slate-900/60 border border-slate-200 dark:border-slate-700"
                                                        >
                                                            {tag}
                                                        </span>
                                                    ))}
                                                </div>
                                                <div className="mt-3 flex flex-wrap items-center gap-2 text-[11px] text-slate-500 dark:text-slate-400">
                                                    <Link
                                                        to={`/story/${book.id}`}
                                                        className="inline-flex items-center gap-1 px-2 py-1 rounded-full bg-primary/10 text-primary hover:bg-primary/20"
                                                    >
                                                        <BookOpen className="w-3 h-3" />
                                                        Đọc tiếp
                                                    </Link>
                                                    <button
                                                        type="button"
                                                        className="inline-flex items-center gap-1 px-2 py-1 rounded-full hover:bg-slate-100 dark:hover:bg-slate-800"
                                                    >
                                                        <Star className="w-3 h-3" />
                                                        Yêu thích
                                                    </button>
                                                    <button
                                                        type="button"
                                                        className="inline-flex items-center gap-1 px-2 py-1 rounded-full hover:bg-red-50 dark:hover:bg-red-900/30 text-red-500"
                                                    >
                                                        <Trash2 className="w-3 h-3" />
                                                        Xóa khỏi tủ sách
                                                    </button>
                                                </div>
                                            </div>
                                        </div>
                                    ))}
                                </div>
                            )}

                            {/* Gợi ý dựa trên tủ sách */}
                            <div className="mt-2 border-t border-slate-200 dark:border-slate-700 pt-5">
                                <h3 className="text-sm font-semibold text-slate-900 dark:text-white mb-2">
                                    Gợi ý cho bạn
                                </h3>
                                <p className="text-xs text-slate-500 dark:text-slate-400 mb-3">
                                    Dựa trên thể loại bạn đang đọc nhiều, chúng tôi gợi ý một số truyện có thể bạn sẽ thích
                                    (demo UI, chưa nối dữ liệu thật).
                                </p>
                                <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-4 gap-3">
                                    {MOCK_MY_BOOKS.slice(0, 3).map((book) => (
                                        <Link
                                            key={`suggest-${book.id}`}
                                            to={`/story/${book.id}`}
                                            className="group rounded-lg border border-slate-200 dark:border-slate-700 overflow-hidden hover:border-primary hover:shadow-md transition-all bg-slate-50 dark:bg-slate-900/40"
                                        >
                                            <div className="aspect-[3/4] bg-slate-200 dark:bg-slate-700">
                                                <ImageWithFallback
                                                    src={resolveBackendUrl(book.coverImage) || ''}
                                                    alt={book.title}
                                                    className="w-full h-full object-cover group-hover:scale-105 transition-transform duration-300"
                                                />
                                            </div>
                                            <div className="p-2">
                                                <p className="text-xs font-semibold text-slate-900 dark:text-white line-clamp-2">
                                                    {book.title}
                                                </p>
                                                <p className="text-[11px] text-slate-500 dark:text-slate-400 mt-0.5 line-clamp-1">
                                                    {book.author}
                                                </p>
                                            </div>
                                        </Link>
                                    ))}
                                </div>
                            </div>
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

                            {MOCK_FOLLOWED_AUTHORS.length === 0 ? (
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
                                    {MOCK_FOLLOWED_AUTHORS.map((author) => (
                                        <div
                                            key={author.id}
                                            className="flex gap-4 rounded-xl border border-slate-200 dark:border-slate-700 p-4 hover:border-primary hover:bg-slate-50 dark:hover:bg-slate-800/60 transition-all"
                                        >
                                            <div className="w-14 h-14 rounded-full overflow-hidden bg-slate-200 dark:bg-slate-700 flex-shrink-0 flex items-center justify-center text-lg font-bold text-slate-600 dark:text-slate-200">
                                                {author.avatarUrl ? (
                                                    <ImageWithFallback
                                                        src={resolveBackendUrl(author.avatarUrl)}
                                                        alt={author.name}
                                                        className="w-full h-full object-cover"
                                                    />
                                                ) : (
                                                    (author.name || 'A').charAt(0).toUpperCase()
                                                )}
                                            </div>
                                            <div className="flex-1 min-w-0">
                                                <div className="flex items-start justify-between gap-2">
                                                    <div>
                                                        <h3 className="font-semibold text-slate-900 dark:text-white text-sm line-clamp-1">
                                                            {author.name}
                                                        </h3>
                                                        <p className="text-xs text-slate-500 dark:text-slate-400 mt-0.5 line-clamp-2">
                                                            {author.bio}
                                                        </p>
                                                    </div>
                                                </div>
                                                <div className="mt-2 flex flex-wrap gap-3 text-xs text-slate-500 dark:text-slate-400">
                                                    <span>{author.totalStories} truyện</span>
                                                    <span>
                                                        {author.totalFollowers.toLocaleString('vi-VN')} người theo dõi
                                                    </span>
                                                </div>
                                                <div className="mt-3 flex flex-wrap gap-2 text-xs">
                                                    <button
                                                        type="button"
                                                        className="inline-flex items-center gap-1 px-3 py-1.5 rounded-full bg-primary text-white font-semibold hover:bg-primary/90"
                                                    >
                                                        <Star className="w-3 h-3" />
                                                        Đang theo dõi
                                                    </button>
                                                    <button
                                                        type="button"
                                                        className="inline-flex items-center gap-1 px-3 py-1.5 rounded-full border border-slate-200 dark:border-slate-700 text-slate-600 dark:text-slate-200 hover:bg-slate-100 dark:hover:bg-slate-800"
                                                    >
                                                        Xem truyện
                                                    </button>
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
