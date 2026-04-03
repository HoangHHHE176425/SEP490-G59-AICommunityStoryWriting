import { Search, Bell, Edit, Menu, X, ChevronDown, Wallet, User, UserCircle, Library, LogOut, Book } from 'lucide-react';
import { useState, useEffect, useCallback, useRef } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { resolveBackendUrl } from '../../utils/resolveBackendUrl';
import { createInitialAvatarDataUrl } from '../../utils/avatarFallback';
import { getAllCategories } from '../../api/category/categoryApi';
import { getNotifications, getUnreadCount, markNotificationAsRead, markAllNotificationsAsRead } from '../../api/notification/notificationApi';
import * as coinApi from '../../api/coins/coinApi';
import { useToast } from '../author/story-editor/Toast';
import { isAuthorChapterListActive } from '../../utils/authorUiFlags';

/** Thông báo mới nhất trên cùng (theo createdAt). */
function sortNotificationsNewestFirst(list) {
    if (!Array.isArray(list)) return [];
    return [...list].sort((a, b) => {
        const ta = Date.parse(a.createdAt ?? a.CreatedAt ?? '') || 0;
        const tb = Date.parse(b.createdAt ?? b.CreatedAt ?? '') || 0;
        if (tb !== ta) return tb - ta;
        return String(b.id ?? b.Id ?? '').localeCompare(String(a.id ?? a.Id ?? ''));
    });
}

export function Header() {
    const navigate = useNavigate();
    const location = useLocation();
    const { user, logout, isAuthenticated, role } = useAuth();
    const { showToast, ToastContainer } = useToast();
    const showToastRef = useRef(showToast);
    showToastRef.current = showToast;
    const [isMenuOpen, setIsMenuOpen] = useState(false);
    const [isGenreOpen, setIsGenreOpen] = useState(false);
    const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
    const [isNotificationOpen, setIsNotificationOpen] = useState(false);
    const [categories, setCategories] = useState([]);
    const [categoriesLoading, setCategoriesLoading] = useState(true);
    const [notifications, setNotifications] = useState([]);
    const [unreadCount, setUnreadCount] = useState(0);
    const [notificationsLoading, setNotificationsLoading] = useState(false);
    const roleUpper = (role ?? '').toString().toUpperCase();
    const isAuthor = roleUpper === 'AUTHOR';

    const userCoinsFallback = user?.stats?.currentCoins ?? 0;
    const [walletBalanceCoin, setWalletBalanceCoin] = useState(null);
    const [walletIncomeBalance, setWalletIncomeBalance] = useState(null);

    const displayedCoins =
        walletBalanceCoin !== null
            ? isAuthor
                ? (walletBalanceCoin ?? 0) + (walletIncomeBalance ?? 0)
                : walletBalanceCoin
            : userCoinsFallback;

    const [searchKeyword, setSearchKeyword] = useState('');

    const fetchNotifications = useCallback(() => {
        if (!isAuthenticated) return;
        setNotificationsLoading(true);
        Promise.all([getNotifications({ limit: 30 }), getUnreadCount()])
            .then(([list, countRes]) => {
                setNotifications(sortNotificationsNewestFirst(Array.isArray(list) ? list : []));
                setUnreadCount(countRes?.count ?? 0);
            })
            .catch(() => {
                setNotifications([]);
                setUnreadCount(0);
            })
            .finally(() => setNotificationsLoading(false));
    }, [isAuthenticated]);

    useEffect(() => {
        if (!isAuthenticated) return;
        const t = setTimeout(() => fetchNotifications(), 0);
        return () => clearTimeout(t);
    }, [isAuthenticated, fetchNotifications]);

    useEffect(() => {
        if (!isAuthenticated) return;
        const handler = (e) => {
            const n = e?.detail;
            if (n && (n.id ?? n.Id)) {
                const title = n.title ?? n.Title ?? '';
                const content = n.content ?? n.Content ?? '';
                const type = (n.type ?? n.Type ?? '').toUpperCase();
                setNotifications((prev) => {
                    const id = n.id ?? n.Id;
                    if (prev.some((x) => (x.id ?? x.Id) === id)) return prev;
                    const item = {
                        id,
                        title,
                        content,
                        linkUrl: n.linkUrl ?? n.LinkUrl,
                        isRead: n.isRead ?? n.IsRead ?? false,
                        createdAt: n.createdAt ?? n.CreatedAt,
                        type: n.type ?? n.Type,
                    };
                    return sortNotificationsNewestFirst([item, ...prev]);
                });
                setUnreadCount((c) => c + 1);
                // Realtime toast: hiển thị popup khi có thông báo mới (vd: ủng hộ, duyệt truyện/chương)
                const toastMsg = content || title || 'Bạn có thông báo mới';
                const toastType = type === 'DONATION' || type === 'CHAPTER_UNLOCK' ? 'success' : 'info';
                const onAuthorChapterList =
                    isAuthor &&
                    location.pathname.replace(/\/$/, '') === '/author' &&
                    isAuthorChapterListActive();
                const chapterModerationToastTypes = new Set([
                    'CHAPTER_APPROVED',
                    'CHAPTER_REJECTED',
                    'CHAPTER_VERSION_APPROVED',
                    'CHAPTER_VERSION_REJECTED',
                ]);
                const skipChapterModerationToast =
                    onAuthorChapterList &&
                    (chapterModerationToastTypes.has(type) || /^CHAPTER_/.test(type));
                if (!skipChapterModerationToast) {
                    showToastRef.current(toastMsg, toastType, 5000);
                }
                // Khi có ủng hộ hoặc độc giả mở khóa chương (thu nhập tác giả), cập nhật ví ngay
                if (type === 'DONATION' || type === 'CHAPTER_UNLOCK') {
                    window.dispatchEvent(new CustomEvent('wallet:changed'));
                }
            }
            fetchNotifications();
        };
        window.addEventListener('app:notification', handler);
        return () => window.removeEventListener('app:notification', handler);
    }, [isAuthenticated, fetchNotifications, isAuthor, location.pathname]);

    const fetchWallet = useCallback(async () => {
        if (!isAuthenticated) {
            setWalletBalanceCoin(null);
            setWalletIncomeBalance(null);
            return;
        }
        const res = await coinApi.getMyWallet();
        if (res?.success) {
            setWalletBalanceCoin(res?.data?.balanceCoin ?? 0);
            setWalletIncomeBalance(Number(res?.data?.incomeBalance ?? 0) || 0);
        }
    }, [isAuthenticated]);

    useEffect(() => {
        fetchWallet().catch(() => {
            // ignore, keep fallback coins
        });
    }, [fetchWallet]);

    useEffect(() => {
        if (!isAuthenticated) return;
        const handler = () => fetchWallet().catch(() => { });
        window.addEventListener('wallet:changed', handler);
        return () => window.removeEventListener('wallet:changed', handler);
    }, [isAuthenticated, fetchWallet]);

    const handleLogout = async () => {
        await logout();
        setIsUserMenuOpen(false);
        navigate('/home');
    };

    const handleBecomeAuthor = () => {
        // Navigate to Policy page, show accept/decline buttons only for this entry.
        navigate('/policy?type=AUTHOR&from=become-author&next=/author');
    };

    const handleSearchSubmit = (e) => {
        e?.preventDefault?.();
        const q = (searchKeyword ?? '').trim();
        if (q) {
            navigate(`/story-list?search=${encodeURIComponent(q)}`);
        } else {
            navigate('/story-list');
        }
    };

    useEffect(() => {
        let cancelled = false;
        getAllCategories({ includeInactive: false })
            .then((data) => {
                if (cancelled) return;
                const items = Array.isArray(data) ? data : (data?.items ?? data?.Items ?? []);
                const categoryNames = items
                    .map((cat) => cat.name ?? cat.Name ?? '')
                    .filter((name) => name && name.trim())
                    .sort();
                setCategories(categoryNames);
            })
            .catch((err) => {
                if (!cancelled) {
                    console.error('Failed to load categories:', err);
                    setCategories([]);
                }
            })
            .finally(() => {
                if (!cancelled) setCategoriesLoading(false);
            });
        return () => {
            cancelled = true;
        };
    }, []);

    return (
        <>
            <header className="sticky top-0 z-50 w-full bg-slate-900/95 backdrop-blur-md border-b border-slate-700/50">
                <div className="max-w-[1280px] mx-auto px-4 h-16 flex items-center justify-between gap-8">
                    {/* Logo & Brand */}
                    <Link to="/home" className="flex items-center shrink-0 hover:opacity-90 transition-opacity" aria-label="CSW-AI - Trang chủ">
                        <img src="/logo.png" alt="CSW-AI" className="h-12 w-auto object-contain" />
                    </Link>

                    {/* Search Bar (Center) - Tìm kiếm truyện, tác giả, thể loại */}
                    <div className="flex-1 max-w-2xl hidden md:block">
                        <form onSubmit={handleSearchSubmit} className="relative group">
                            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400 group-focus-within:text-primary transition-colors">
                                <Search className="w-5 h-5" />
                            </div>
                            <input
                                className="block w-full pl-10 pr-4 py-2 bg-slate-800 border border-slate-700 rounded-full text-sm focus:ring-2 focus:ring-primary/50 transition-all placeholder:text-slate-500 outline-none text-white"
                                placeholder="Tìm kiếm truyện, tác giả, thể loại..."
                                type="text"
                                value={searchKeyword}
                                onChange={(e) => setSearchKeyword(e.target.value)}
                                aria-label="Tìm kiếm truyện, tác giả, thể loại"
                            />
                        </form>
                    </div>

                    {/* Main Nav & User Actions */}
                    <nav className="flex items-center gap-6">
                        <div className="hidden lg:flex items-center gap-6 text-sm font-semibold text-slate-300">
                            {isAuthor ? (
                                <>
                                    <Link to="/home" className="hover:text-primary transition-colors">Trang chủ</Link>
                                    <Link to="/about-us" className="hover:text-primary transition-colors">Về chúng tôi</Link>
                                    <Link to="/story-list" className="hover:text-primary transition-colors">Kho truyện tổng</Link>
                                </>
                            ) : (
                                <>
                                    <Link to="/home" className="hover:text-primary transition-colors">Trang chủ</Link>
                                    <Link to="/about-us" className="hover:text-primary transition-colors">Về chúng tôi</Link>
                                    <Link to="/story-list" className="hover:text-primary transition-colors">Khám phá truyện</Link>
                                </>
                            )}
                        </div>

                        <div className="h-6 w-px bg-slate-700 hidden lg:block"></div>

                        <div className="flex items-center gap-3">
                            {isAuthenticated ? (
                                <>
                                    {/* Wallet - click to go to wallet page */}
                                    <Link
                                        to="/wallet"
                                        className="hidden sm:flex items-center gap-1.5 px-3 py-1.5 bg-amber-950/40 border border-amber-700/50 rounded-full hover:bg-amber-950/60 transition-colors"
                                    >
                                        <Wallet className="w-4 h-4 text-amber-400" />
                                        <span className="text-sm font-bold text-amber-400">{displayedCoins.toLocaleString()}</span>
                                    </Link>

                                    <div className="relative">
                                        <button
                                            className="p-2 text-slate-300 hover:bg-slate-800 rounded-full transition-colors relative flex items-center justify-center"
                                            onClick={() => {
                                                const opening = !isNotificationOpen;
                                                setIsNotificationOpen((prev) => !prev);
                                                if (opening && unreadCount > 0) {
                                                    markAllNotificationsAsRead()
                                                        .then(() => {
                                                            setUnreadCount(0);
                                                            setNotifications((prev) => prev.map((n) => ({ ...n, isRead: true })));
                                                        })
                                                        .catch(() => { });
                                                }
                                            }}
                                            onBlur={() => setTimeout(() => setIsNotificationOpen(false), 200)}
                                        >
                                            <Bell className="w-5 h-5 shrink-0" />
                                            {unreadCount > 0 && (
                                                <span className="absolute -top-0.5 -right-0.5 min-w-[18px] h-[18px] px-1 flex items-center justify-center bg-primary text-white text-[10px] font-bold rounded-full border-2 border-slate-900 shadow-sm">
                                                    {unreadCount > 99 ? '99+' : unreadCount}
                                                </span>
                                            )}
                                        </button>
                                        {isNotificationOpen && (
                                            <div
                                                className="absolute top-full right-0 mt-2 w-80 bg-slate-800 border border-slate-700 rounded-lg shadow-xl overflow-hidden z-50 flex flex-col"
                                                onMouseDown={(e) => e.preventDefault()}
                                            >
                                                <div className="shrink-0 px-4 py-3 border-b border-slate-700 flex items-center justify-between">
                                                    <span className="font-semibold text-white">Thông báo</span>
                                                    {unreadCount > 0 && (
                                                        <span className="text-xs text-slate-400">{unreadCount} chưa đọc</span>
                                                    )}
                                                </div>
                                                <div className="flex-1 min-h-0 overflow-y-auto overscroll-y-contain max-h-[320px]">
                                                    {notificationsLoading ? (
                                                        <div className="px-4 py-6 text-center text-slate-400 text-sm">Đang tải...</div>
                                                    ) : notifications.length === 0 ? (
                                                        <div className="px-4 py-6 text-center text-slate-400 text-sm">Chưa có thông báo</div>
                                                    ) : (
                                                        notifications.map((n) => {
                                                            const typeUpper = (n.type ?? n.Type ?? '').toString().toUpperCase();
                                                            const isReportToVictim =
                                                                typeUpper === 'STORY_REPORTED_TO_AUTHOR' ||
                                                                typeUpper === 'COMMENT_REPORTED_TO_OWNER';
                                                            return (
                                                                <div
                                                                    key={n.id}
                                                                    className="block px-4 py-3 border-b border-slate-700/50"
                                                                >
                                                                    <p className={`text-sm font-medium ${n.isRead ? 'text-slate-400' : 'text-white'}`}>{n.title}</p>
                                                                    <p
                                                                        className={`text-xs text-slate-500 mt-0.5 ${isReportToVictim ? 'line-clamp-4' : 'line-clamp-2'}`}
                                                                    >
                                                                        {n.content}
                                                                    </p>
                                                                </div>
                                                            );
                                                        })
                                                    )}
                                                </div>
                                            </div>
                                        )}
                                    </div>

                                    {isAuthor ? (
                                        <Link
                                            to="/author"
                                            className="hidden sm:flex items-center gap-2 px-5 py-2.5 bg-primary text-white text-sm font-extrabold rounded-full shadow-lg shadow-primary/50 hover:bg-primary/90 hover:shadow-primary/60 transition-all"
                                        >
                                            <Edit className="w-4 h-4" />
                                            Viết truyện
                                        </Link>
                                    ) : (
                                        <button
                                            type="button"
                                            onClick={handleBecomeAuthor}
                                            className="hidden sm:flex items-center gap-2 px-4 py-2 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all"
                                        >
                                            <Edit className="w-4 h-4" />
                                            Trở thành tác giả
                                        </button>
                                    )}

                                    {/* User Avatar - click to Homepage; Chevron for user menu */}
                                    <div className="relative flex items-center gap-0.5">
                                        <Link
                                            to="/home"
                                            className="block size-9 rounded-full overflow-hidden border-2 border-slate-700 hover:border-primary transition-colors shrink-0"
                                        >
                                            <img
                                                alt="User Avatar"
                                                className="w-full h-full object-cover"
                                                src={
                                                    (user?.avatarUrl ? resolveBackendUrl(user.avatarUrl) : '') ||
                                                    createInitialAvatarDataUrl(user?.displayName ?? user?.email ?? 'U', 128)
                                                }
                                            />
                                        </Link>
                                        <button
                                            className="p-1 -ml-1 text-slate-400 hover:text-primary rounded transition-colors"
                                            onClick={() => setIsUserMenuOpen(!isUserMenuOpen)}
                                            onBlur={() => setTimeout(() => setIsUserMenuOpen(false), 200)}
                                        >
                                            <ChevronDown className={`w-4 h-4 transition-transform ${isUserMenuOpen ? 'rotate-180' : ''}`} />
                                        </button>
                                        {isUserMenuOpen && (
                                            <div
                                                className="absolute top-full right-0 mt-2 w-56 bg-slate-800 border border-slate-700 rounded-lg shadow-xl overflow-hidden z-50"
                                                onMouseDown={(e) => e.preventDefault()}
                                            >
                                                <div className="py-2">
                                                    <div className="px-4 py-3 border-b border-slate-700">
                                                        <p className="font-semibold text-white">{user?.displayName || 'Người dùng'}</p>
                                                        <p className="text-sm text-slate-400">{user?.email || ''}</p>
                                                    </div>
                                                    <Link
                                                        to="/profile"
                                                        className="flex items-center gap-3 px-4 py-2.5 text-sm text-slate-300 hover:bg-primary/10 hover:text-primary transition-colors"
                                                        onClick={() => setIsUserMenuOpen(false)}
                                                    >
                                                        <User className="w-4 h-4" />
                                                        Thông tin cá nhân
                                                    </Link>
                                                    <Link
                                                        to="/library"
                                                        className="flex items-center gap-3 px-4 py-2.5 text-sm text-slate-300 hover:bg-primary/10 hover:text-primary transition-colors"
                                                        onClick={() => setIsUserMenuOpen(false)}
                                                    >
                                                        <Library className="w-4 h-4" />
                                                        Tủ sách
                                                    </Link>
                                                    {isAuthor && (
                                                        <>
                                                            <Link
                                                                to="/author?view=stories"
                                                                className="flex items-center gap-3 px-4 py-2.5 text-sm text-slate-300 hover:bg-primary/10 hover:text-primary transition-colors"
                                                                onClick={() => setIsUserMenuOpen(false)}
                                                            >
                                                                <Book className="w-4 h-4" />
                                                                Truyện của tôi
                                                            </Link>
                                                            <Link
                                                                to="/author?view=profile"
                                                                className="flex items-center gap-3 px-4 py-2.5 text-sm text-slate-300 hover:bg-primary/10 hover:text-primary transition-colors"
                                                                onClick={() => setIsUserMenuOpen(false)}
                                                            >
                                                                <UserCircle className="w-4 h-4" />
                                                                Hồ sơ tác giả
                                                            </Link>
                                                        </>
                                                    )}
                                                    <div className="border-t border-slate-700 mt-1 pt-1">
                                                        <button
                                                            onClick={handleLogout}
                                                            className="w-full flex items-center gap-3 px-4 py-2.5 text-sm text-red-400 hover:bg-red-950/30 transition-colors"
                                                        >
                                                            <LogOut className="w-4 h-4" />
                                                            Đăng xuất
                                                        </button>
                                                    </div>
                                                </div>
                                            </div>
                                        )}
                                    </div>
                                </>
                            ) : (
                                <>
                                    <Link
                                        to="/login"
                                        className="hidden sm:flex items-center gap-2 px-4 py-2 text-slate-300 font-semibold hover:text-primary transition-colors"
                                    >
                                        Đăng nhập
                                    </Link>
                                    <Link
                                        to="/register"
                                        className="hidden sm:flex items-center gap-2 px-4 py-2 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all"
                                    >
                                        Đăng ký
                                    </Link>
                                </>
                            )}

                            <button
                                className="lg:hidden p-2 text-slate-300 hover:text-white"
                                onClick={() => setIsMenuOpen(!isMenuOpen)}
                            >
                                {isMenuOpen ? <X className="w-6 h-6" /> : <Menu className="w-6 h-6" />}
                            </button>
                        </div>
                    </nav>
                </div>

                {/* Mobile Menu */}
                {isMenuOpen && (
                    <div className="lg:hidden border-t border-slate-700 bg-slate-900">
                        <div className="max-w-[1280px] mx-auto px-4 py-4 flex flex-col gap-4">
                            {/* Wallet Mobile - link to wallet page */}
                            {isAuthenticated && (
                                <Link
                                    to="/wallet"
                                    onClick={() => setIsMenuOpen(false)}
                                    className="flex items-center justify-between p-3 bg-amber-950/40 border border-amber-700/50 rounded-lg hover:bg-amber-950/60 transition-colors"
                                >
                                    <div className="flex items-center gap-2">
                                        <Wallet className="w-5 h-5 text-amber-400" />
                                        <span className="font-semibold text-white">Ví</span>
                                    </div>
                                    <span className="text-lg font-bold text-amber-400">{displayedCoins.toLocaleString()}</span>
                                </Link>
                            )}

                            <form onSubmit={handleSearchSubmit} className="relative mb-2">
                                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400">
                                    <Search className="w-5 h-5" />
                                </div>
                                <input
                                    className="block w-full pl-10 pr-4 py-2 bg-slate-800 border border-slate-700 rounded-full text-sm outline-none text-white placeholder:text-slate-500"
                                    placeholder="Tìm kiếm truyện, tác giả, thể loại..."
                                    type="text"
                                    value={searchKeyword}
                                    onChange={(e) => setSearchKeyword(e.target.value)}
                                />
                            </form>

                            <div className="flex flex-col gap-3">
                                <Link to="/home" className="text-slate-300 hover:text-primary transition-colors font-semibold" onClick={() => setIsMenuOpen(false)}>Trang chủ</Link>
                                <details className="group">
                                    <summary className="flex items-center justify-between text-slate-300 hover:text-primary transition-colors font-semibold cursor-pointer list-none">
                                        Thể loại
                                        <ChevronDown className="w-4 h-4 group-open:rotate-180 transition-transform" />
                                    </summary>
                                    <div className="mt-2 ml-4 flex flex-col gap-2">
                                        {categoriesLoading ? (
                                            <div className="text-sm text-slate-400">Đang tải...</div>
                                        ) : categories.length === 0 ? (
                                            <div className="text-sm text-slate-400">Chưa có thể loại</div>
                                        ) : (
                                            categories.map((categoryName) => (
                                                <a
                                                    key={categoryName}
                                                    href="#"
                                                    className="text-sm text-slate-400 hover:text-primary transition-colors"
                                                >
                                                    {categoryName}
                                                </a>
                                            ))
                                        )}
                                    </div>
                                </details>

                                {isAuthor ? (
                                    <>
                                        <Link to="/home" className="text-slate-300 hover:text-primary transition-colors font-semibold" onClick={() => setIsMenuOpen(false)}>Trang chủ</Link>
                                        <Link to="/about-us" className="text-slate-300 hover:text-primary transition-colors font-semibold" onClick={() => setIsMenuOpen(false)}>Về chúng tôi</Link>
                                        <Link to="/story-list" className="text-slate-300 hover:text-primary transition-colors font-semibold" onClick={() => setIsMenuOpen(false)}>Kho truyện tổng</Link>
                                    </>
                                ) : (
                                    <>
                                        <Link to="/home" className="text-slate-300 hover:text-primary transition-colors font-semibold" onClick={() => setIsMenuOpen(false)}>Trang chủ</Link>
                                        <Link to="/about-us" className="text-slate-300 hover:text-primary transition-colors font-semibold" onClick={() => setIsMenuOpen(false)}>About us</Link>
                                        <Link to="/story-list" className="text-slate-300 hover:text-primary transition-colors font-semibold" onClick={() => setIsMenuOpen(false)}>Khám phá truyện</Link>
                                    </>
                                )}

                                {isAuthenticated ? (
                                    <>
                                        <div className="border-t border-slate-700 my-2"></div>
                                        {isAuthor ? (
                                            <Link
                                                to="/author"
                                                onClick={() => setIsMenuOpen(false)}
                                                className="flex items-center gap-3 px-4 py-2 bg-primary text-white rounded-full font-semibold justify-center hover:bg-primary/90 transition-colors"
                                            >
                                                <Edit className="w-4 h-4" />
                                                Viết truyện
                                            </Link>
                                        ) : (
                                            <button
                                                type="button"
                                                onClick={() => {
                                                    setIsMenuOpen(false);
                                                    handleBecomeAuthor();
                                                }}
                                                className="flex items-center gap-3 text-slate-300 hover:text-primary transition-colors font-semibold"
                                            >
                                                <Edit className="w-4 h-4" />
                                                Trở thành tác giả
                                            </button>
                                        )}
                                        {/* User Menu Mobile */}
                                        <Link
                                            to="/profile"
                                            className="flex items-center gap-3 text-slate-300 hover:text-primary transition-colors font-semibold"
                                        >
                                            <User className="w-4 h-4" />
                                            Thông tin cá nhân
                                        </Link>
                                        <Link
                                            to="/library"
                                            className="flex items-center gap-3 text-slate-300 hover:text-primary transition-colors font-semibold"
                                            onClick={() => setIsMenuOpen(false)}
                                        >
                                            <Library className="w-4 h-4" />
                                            Tủ sách
                                        </Link>
                                        {isAuthor && (
                                            <>
                                                <Link
                                                    to="/author?view=stories"
                                                    className="flex items-center gap-3 text-slate-300 hover:text-primary transition-colors font-semibold"
                                                    onClick={() => setIsMenuOpen(false)}
                                                >
                                                    <Book className="w-4 h-4" />
                                                    Truyện của tôi
                                                </Link>
                                                <Link
                                                    to="/author?view=profile"
                                                    className="flex items-center gap-3 text-slate-300 hover:text-primary transition-colors font-semibold"
                                                    onClick={() => setIsMenuOpen(false)}
                                                >
                                                    <UserCircle className="w-4 h-4" />
                                                    Hồ sơ tác giả
                                                </Link>
                                            </>
                                        )}
                                        <button
                                            onClick={handleLogout}
                                            className="flex items-center gap-3 text-red-600 dark:text-red-400 hover:text-red-700 transition-colors font-semibold"
                                        >
                                            <LogOut className="w-4 h-4" />
                                            Đăng xuất
                                        </button>
                                    </>
                                ) : (
                                    <>
                                        <div className="border-t border-slate-700 my-2"></div>
                                        <Link
                                            to="/login"
                                            className="text-slate-300 hover:text-primary transition-colors font-semibold"
                                        >
                                            Đăng nhập
                                        </Link>
                                        <Link
                                            to="/register"
                                            className="text-slate-300 hover:text-primary transition-colors font-semibold"
                                        >
                                            Đăng ký
                                        </Link>
                                    </>
                                )}
                            </div>
                        </div>
                    </div>
                )}

            </header>
            <ToastContainer />
        </>
    );
}