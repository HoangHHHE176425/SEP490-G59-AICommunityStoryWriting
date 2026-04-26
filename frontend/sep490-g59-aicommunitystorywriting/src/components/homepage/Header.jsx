import { Search, Bell, Edit, Menu, X, ChevronDown, Wallet, User, UserCircle, Library, LogOut, Book } from 'lucide-react';
import { useState, useEffect, useCallback, useRef } from 'react';
import { Link, useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { resolveBackendUrl } from '../../utils/resolveBackendUrl';
import { createInitialAvatarDataUrl } from '../../utils/avatarFallback';
import { getNotifications, getUnreadCount, markNotificationAsRead, markAllNotificationsAsRead } from '../../api/notification/notificationApi';
import * as coinApi from '../../api/coins/coinApi';
import { useToast } from '../author/story-editor/Toast';
import { isAuthorChapterListActive } from '../../utils/authorUiFlags';
import { normalizeNotificationTo } from '../../utils/notificationLink';

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

const VIOLATION_NOTIFICATION_TYPES = new Set([
    'STORY_REPORTED_TO_AUTHOR',
    'COMMENT_REPORTED_TO_OWNER',
    'COMPLIANCE_STORY_MODERATION_ACTION',
    'COMPLIANCE_COMMENT_MODERATION_ACTION',
    'COMPLIANCE_AUTHOR_WRITING_MODERATION',
    'COMPLIANCE_STORY_REPORT_BULK_RESOLVED',
    'COMPLIANCE_COMMENT_REPORT_BULK_RESOLVED',
    'COMPLIANCE_ADMIN_ACTION_APPROVED',
]);

function isViolationNotification(type) {
    const t = String(type ?? '').toUpperCase();
    return VIOLATION_NOTIFICATION_TYPES.has(t) || t.startsWith('COMPLIANCE_');
}

function parseNotificationContent(content) {
    const normalized = String(content ?? '')
        .replace(/\s+/g, ' ')
        .trim();
    if (!normalized) {
        return { summary: 'Không có nội dung chi tiết.', violationLine: '', verificationLine: '', detailLines: [] };
    }
    const lines = normalized.split(/(?<=[.!?])\s+/).filter(Boolean);
    const violationLine = lines.find((x) => /nội dung vi phạm|lý do đã xác minh|vi phạm:/i.test(x)) || '';
    const verificationLine = lines.find((x) =>
        /xác minh|đối soát|lịch sử xử lý/i.test(x) && x !== violationLine
    ) || '';
    const summary = lines[0] || normalized;
    const detailLines = lines
        .slice(1)
        .filter((x) => x !== verificationLine && x !== violationLine);
    return { summary, violationLine, verificationLine, detailLines };
}

function extractViolationReason(content) {
    const text = String(content || '');
    const patterns = [
        /nội dung vi phạm(?: đã xác minh)?\s*:\s*([^.]*)/i,
        /lý do đã xác minh\s*:\s*([^.]*)/i,
        /vi phạm:\s*([^.]*)/i,
    ];
    for (const p of patterns) {
        const m = text.match(p);
        if (m?.[1]?.trim()) return m[1].trim();
    }
    return '';
}

function inferViolationSubject(notification) {
    const title = String(notification?.title || '').toLowerCase();
    const type = String(notification?.type || '').toUpperCase();
    if (title.includes('bình luận') || type.includes('COMMENT')) return 'bình luận của bạn';
    if (title.includes('tài khoản') || type === 'COMPLIANCE_ADMIN_ACTION_APPROVED') return 'tài khoản của bạn';
    if (title.includes('quyền viết')) return 'quyền viết của bạn';
    return 'truyện của bạn';
}

function buildViolationSummary(notification, parsed) {
    const capitalizeFirstAfterColon = (text) =>
        String(text || '').replace(/:\s*([a-zA-ZÀ-ỹà-ỹ])/g, (_, ch) => `: ${ch.toUpperCase()}`);
    const normalizeViolationPhrase = (text) =>
        String(text || '').replace(/nội dung vi phạm\s+là\s*:/gi, 'nội dung vi phạm:');
    const base = capitalizeFirstAfterColon(normalizeViolationPhrase(String(parsed?.summary || '').trim()));
    if (!base) return 'Thông báo xử lý vi phạm.';
    if (!isViolationNotification(notification?.type)) return base;
    if (/vì có người báo cáo|nội dung vi phạm:/i.test(base)) return base;

    const reason = extractViolationReason(notification?.content) || 'đang trong quá trình xác minh xử lí vi phạm';
    const type = String(notification?.type || '').toUpperCase();
    const title = String(notification?.title || '').toLowerCase();

    if (type === 'COMPLIANCE_STORY_MODERATION_ACTION') {
        if (title.includes('khóa bình luận')) {
            return capitalizeFirstAfterColon(normalizeViolationPhrase(`Xử lý vi phạm viên đã tắt bình luận cho truyện của bạn vì có người báo cáo truyện của bạn với nội dung vi phạm: ${reason}.`));
        }
        if (title.includes('mở lại bình luận')) {
            return `Xử lý vi phạm viên đã bật lại bình luận cho truyện của bạn sau khi rà soát báo cáo vi phạm.`;
        }
        if (title.includes('ẩn khỏi công khai')) {
            return capitalizeFirstAfterColon(normalizeViolationPhrase(`Xử lý vi phạm viên đã ẩn truyện của bạn khỏi danh sách công khai vì có người báo cáo truyện của bạn với nội dung vi phạm: ${reason}.`));
        }
        if (title.includes('hiển thị lại')) {
            return `Xử lý vi phạm viên đã hiển thị lại truyện của bạn sau khi rà soát báo cáo vi phạm.`;
        }
        if (title.includes('tạm khóa quyền viết')) {
            return capitalizeFirstAfterColon(normalizeViolationPhrase(`Xử lý vi phạm viên đã tạm khóa quyền viết của bạn vì có người báo cáo truyện của bạn với nội dung vi phạm: ${reason}.`));
        }
        if (title.includes('mở lại quyền viết')) {
            return `Xử lý vi phạm viên đã mở lại quyền viết của bạn sau khi rà soát báo cáo vi phạm.`;
        }
    }

    if (type === 'COMPLIANCE_COMMENT_MODERATION_ACTION') {
        if (title.includes('bị ẩn')) {
            return capitalizeFirstAfterColon(normalizeViolationPhrase(`Xử lý vi phạm viên đã ẩn bình luận của bạn vì có người báo cáo bình luận của bạn với nội dung vi phạm: ${reason}.`));
        }
        if (title.includes('hiển thị lại')) {
            return `Xử lý vi phạm viên đã hiển thị lại bình luận của bạn sau khi rà soát báo cáo vi phạm.`;
        }
    }

    if (type === 'COMPLIANCE_AUTHOR_WRITING_MODERATION') {
        if (title.includes('tạm khóa')) {
            return capitalizeFirstAfterColon(normalizeViolationPhrase(`Xử lý vi phạm viên đã tạm khóa quyền viết của bạn vì có người báo cáo nội dung của bạn với nội dung vi phạm: ${reason}.`));
        }
        return `Xử lý vi phạm viên đã mở lại quyền viết của bạn sau khi rà soát báo cáo vi phạm.`;
    }

    if (type === 'COMPLIANCE_ADMIN_ACTION_APPROVED') {
        if (title.includes('tài khoản đã bị khóa')) {
            return `Admin đã duyệt khóa tài khoản của bạn vì có báo cáo vi phạm đã được xác minh với nội dung: ${reason}.`;
        }
        if (title.includes('đình chỉ quyền viết')) {
            return `Admin đã duyệt đình chỉ quyền viết của bạn vì có báo cáo vi phạm đã được xác minh với nội dung: ${reason}.`;
        }
    }

    if (type === 'STORY_REPORTED_TO_AUTHOR') {
        return capitalizeFirstAfterColon(normalizeViolationPhrase(`Truyện của bạn đã bị báo cáo với nội dung vi phạm: ${reason}.`));
    }
    if (type === 'COMMENT_REPORTED_TO_OWNER') {
        return capitalizeFirstAfterColon(normalizeViolationPhrase(`Bình luận của bạn đã bị báo cáo với nội dung vi phạm: ${reason}.`));
    }

    const subject = inferViolationSubject(notification);
    return capitalizeFirstAfterColon(normalizeViolationPhrase(`${base} vì có người báo cáo ${subject} với nội dung vi phạm: ${reason}.`));
}

export function Header() {
    const navigate = useNavigate();
    const location = useLocation();
    const { user, logout, isAuthenticated, role } = useAuth();
    const { showToast, ToastContainer } = useToast();
    const showToastRef = useRef(showToast);
    showToastRef.current = showToast;
    const [isMenuOpen, setIsMenuOpen] = useState(false);
    const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);
    const [isNotificationOpen, setIsNotificationOpen] = useState(false);
    const [selectedNotification, setSelectedNotification] = useState(null);
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
                const chapterModerationToastTypes = new Set([
                    'CHAPTER_APPROVED',
                    'CHAPTER_REJECTED',
                    'CHAPTER_VERSION_APPROVED',
                    'CHAPTER_VERSION_REJECTED',
                ]);
                // UX request: không hiển thị toast duyệt/từ chối chương vì dễ bị nháy lặp.
                const skipChapterModerationToast =
                    chapterModerationToastTypes.has(type) || /^CHAPTER_/.test(type);
                // Trên /author (Truyện của tôi, danh sách chương, soạn thảo): đã cập nhật UI qua fetchProfile — không toast khi compliance bật/tắt quyền viết.
                const skipComplianceAuthorWritingToast =
                    type === 'COMPLIANCE_STORY_MODERATION_ACTION' && location.pathname === '/author';
                // UX: màn tác giả tự đồng bộ dữ liệu bằng polling/state, nên tắt toàn bộ toast notification để tránh nhiễu.
                const skipAllAuthorScreenToasts = location.pathname.startsWith('/author');
                if (!skipChapterModerationToast && !skipComplianceAuthorWritingToast && !skipAllAuthorScreenToasts) {
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
    }, [isAuthenticated, fetchNotifications, location.pathname]);

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

    const resolveNotificationTarget = useCallback((notification) => {
        const linkUrl = notification?.linkUrl ?? notification?.LinkUrl;
        let target = normalizeNotificationTo(linkUrl);
        const typeUpper = String(notification?.type ?? notification?.Type ?? '').toUpperCase();
        if (typeUpper === 'STORY_REPORTED_TO_AUTHOR' || typeUpper === 'COMMENT_REPORTED_TO_OWNER') {
            const storyMatch = String(linkUrl ?? '').match(/\/story\/([0-9a-fA-F-]{36})/i);
            const storyId = storyMatch?.[1];
            target = storyId ? `/author?view=reports&storyId=${encodeURIComponent(storyId)}` : '/author?view=reports';
        }
        return target;
    }, []);

    const handleNotificationClick = async (notification) => {
        const notificationId = notification?.id ?? notification?.Id;
        const isRead = notification?.isRead ?? notification?.IsRead ?? false;
        const target = resolveNotificationTarget(notification);

        if (notificationId && !isRead) {
            try {
                await markNotificationAsRead(notificationId);
                setNotifications((prev) =>
                    prev.map((item) =>
                        (item.id ?? item.Id) === notificationId ? { ...item, isRead: true } : item
                    )
                );
                setUnreadCount((count) => Math.max(0, count - 1));
            } catch {
                // best-effort; vẫn cho phép điều hướng
            }
        }

        setIsNotificationOpen(false);
        setSelectedNotification({
            ...(notification ?? {}),
            isRead: true,
            _target: target,
        });
    };

    const handleOpenNotificationTarget = () => {
        if (!selectedNotification?._target) {
            setSelectedNotification(null);
            return;
        }
        const target = selectedNotification._target;
        setSelectedNotification(null);
        navigate(target);
    };

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
                                                                <button
                                                                    type="button"
                                                                    key={n.id}
                                                                    className="w-full text-left block px-4 py-3 border-b border-slate-700/50 hover:bg-slate-700/40 transition-colors"
                                                                    onClick={() => handleNotificationClick(n)}
                                                                >
                                                                    <p className={`text-sm font-medium ${n.isRead ? 'text-slate-400' : 'text-white'}`}>{n.title}</p>
                                                                    <p
                                                                        className={`text-xs text-slate-500 mt-0.5 ${isReportToVictim ? 'whitespace-pre-wrap' : 'line-clamp-2'}`}
                                                                    >
                                                                        {n.content}
                                                                    </p>
                                                                    {isReportToVictim && (
                                                                        <p className="text-[11px] text-amber-300 mt-1">
                                                                            Nhấn để mở chi tiết truyện liên quan báo cáo
                                                                        </p>
                                                                    )}
                                                                </button>
                                                            );
                                                        })
                                                    )}
                                                </div>
                                                <div className="shrink-0 border-t border-slate-700 px-3 py-2">
                                                    <Link
                                                        to="/notifications"
                                                        className="block w-full text-center text-sm font-semibold text-primary hover:text-primary/90 transition-colors"
                                                        onClick={() => setIsNotificationOpen(false)}
                                                    >
                                                        Xem tất cả thông báo
                                                    </Link>
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
                                        <Link
                                            to="/notifications"
                                            className="flex items-center gap-3 text-slate-300 hover:text-primary transition-colors font-semibold"
                                            onClick={() => setIsMenuOpen(false)}
                                        >
                                            <Bell className="w-4 h-4" />
                                            Danh sách thông báo
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
            {selectedNotification && (
                <div className="fixed inset-0 z-[70] flex items-center justify-center bg-black/60 p-4">
                    <div className="w-full max-w-lg rounded-xl border border-slate-700 bg-slate-900 shadow-2xl">
                        <div className="flex items-start justify-between gap-3 border-b border-slate-700 px-4 py-3">
                            <div>
                                <p className="text-base font-semibold text-white">Chi tiết thông báo</p>
                                <p className="text-xs text-slate-400 mt-0.5">
                                    {selectedNotification?.createdAt
                                        ? new Date(selectedNotification.createdAt).toLocaleString('vi-VN')
                                        : ''}
                                </p>
                            </div>
                            <button
                                type="button"
                                onClick={() => setSelectedNotification(null)}
                                className="rounded-md p-1 text-slate-400 hover:bg-slate-800 hover:text-white transition-colors"
                                aria-label="Đóng popup thông báo"
                            >
                                <X className="w-4 h-4" />
                            </button>
                        </div>

                        <div className="px-4 py-3">
                            {(() => {
                                const parsed = parseNotificationContent(selectedNotification?.content);
                                const isViolation = isViolationNotification(selectedNotification?.type);
                                const summaryText = buildViolationSummary(selectedNotification, parsed);
                                return (
                                    <>
                                        <p className="text-sm font-semibold text-white">
                                            {selectedNotification?.title ?? 'Thông báo'}
                                        </p>
                                        <p className="mt-2 whitespace-pre-wrap text-sm text-slate-200">
                                            {summaryText}
                                        </p>
                                        {parsed.detailLines.length > 0 && (
                                            <div className="mt-3 rounded-md border border-slate-700 bg-slate-800/70 px-3 py-2">
                                                <p className="text-xs font-semibold text-slate-200">Chi tiết bổ sung</p>
                                                <ul className="mt-1 space-y-1 text-xs text-slate-300">
                                                    {parsed.detailLines.map((line, idx) => (
                                                        <li key={`${idx}-${line.slice(0, 24)}`}>- {line}</li>
                                                    ))}
                                                </ul>
                                            </div>
                                        )}
                                    </>
                                );
                            })()}
                        </div>

                        <div className="flex items-center justify-end gap-2 border-t border-slate-700 px-4 py-3">
                            <button
                                type="button"
                                onClick={() => setSelectedNotification(null)}
                                className="rounded-full border border-slate-600 px-4 py-1.5 text-sm font-semibold text-slate-300 hover:bg-slate-800 transition-colors"
                            >
                                Đóng
                            </button>
                            <button
                                type="button"
                                onClick={handleOpenNotificationTarget}
                                className="rounded-full bg-primary px-4 py-1.5 text-sm font-semibold text-white hover:bg-primary/90 transition-colors"
                            >
                                Mở trang liên quan
                            </button>
                        </div>
                    </div>
                </div>
            )}
            <ToastContainer />
        </>
    );
}