import { useCallback, useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { X } from 'lucide-react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { useAuth } from '../../contexts/AuthContext';
import {
    getNotifications,
    markAllNotificationsAsRead,
    markNotificationAsRead,
} from '../../api/notification/notificationApi';
import { normalizeNotificationTo } from '../../utils/notificationLink';

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
    const t = String(type || '').toUpperCase();
    return VIOLATION_NOTIFICATION_TYPES.has(t) || t.startsWith('COMPLIANCE_');
}

function parseNotificationContent(content) {
    const normalized = String(content || '')
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

export default function NotificationListPage() {
    const navigate = useNavigate();
    const { isAuthenticated } = useAuth();
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const [onlyUnread, setOnlyUnread] = useState(false);
    const [notifications, setNotifications] = useState([]);
    const [selectedNotification, setSelectedNotification] = useState(null);

    const loadNotifications = useCallback(async (unreadOnly = onlyUnread) => {
        setLoading(true);
        setError('');
        try {
            const list = await getNotifications({ limit: 200, onlyUnread: unreadOnly });
            setNotifications(Array.isArray(list) ? list : []);
        } catch (e) {
            setError(e?.response?.data?.message ?? e?.message ?? 'Không tải được danh sách thông báo.');
            setNotifications([]);
        } finally {
            setLoading(false);
        }
    }, [onlyUnread]);

    useEffect(() => {
        if (!isAuthenticated) return;
        loadNotifications();
    }, [isAuthenticated, loadNotifications]);

    const unreadCount = useMemo(
        () => notifications.filter((n) => !(n?.isRead ?? false)).length,
        [notifications]
    );

    const handleOpenNotification = async (n) => {
        const id = n?.id;
        const isRead = n?.isRead ?? false;
        const target = normalizeNotificationTo(n?.linkUrl);
        if (id && !isRead) {
            try {
                await markNotificationAsRead(id);
                setNotifications((prev) =>
                    prev.map((item) => (item.id === id ? { ...item, isRead: true } : item))
                );
            } catch {
                // best-effort
            }
        }
        setSelectedNotification({
            ...n,
            isRead: true,
            _target: target,
        });
    };

    const handleNavigateFromPopup = () => {
        const target = selectedNotification?._target;
        setSelectedNotification(null);
        if (target) navigate(target);
    };

    const handleMarkAllRead = async () => {
        try {
            await markAllNotificationsAsRead();
            setNotifications((prev) => prev.map((item) => ({ ...item, isRead: true })));
        } catch (e) {
            setError(e?.response?.data?.message ?? e?.message ?? 'Không thể đánh dấu đã đọc.');
        }
    };

    if (!isAuthenticated) {
        return (
            <div className="min-h-screen bg-slate-50 dark:bg-background-dark">
                <Header />
                <main className="max-w-[960px] mx-auto px-4 py-8">
                    <h1 className="text-2xl font-bold text-slate-900 dark:text-white">Danh sách thông báo</h1>
                    <p className="mt-3 text-slate-600 dark:text-slate-300">
                        Vui lòng đăng nhập để xem thông báo của bạn.
                    </p>
                </main>
                <Footer />
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-slate-50 dark:bg-background-dark">
            <Header />
            <main className="max-w-[960px] mx-auto px-4 py-8">
                <div className="flex items-center justify-between gap-3 flex-wrap">
                    <div>
                        <h1 className="text-2xl font-bold text-slate-900 dark:text-white">Danh sách thông báo</h1>
                        <p className="text-sm text-slate-600 dark:text-slate-300 mt-1">
                            {unreadCount > 0 ? `Bạn có ${unreadCount} thông báo chưa đọc` : 'Tất cả thông báo đã được đọc'}
                        </p>
                    </div>
                    <div className="flex items-center gap-2">
                        <button
                            type="button"
                            className="px-3 py-1.5 text-sm rounded-full border border-slate-300 text-slate-700 hover:bg-slate-100"
                            onClick={() => {
                                const next = !onlyUnread;
                                setOnlyUnread(next);
                                loadNotifications(next);
                            }}
                        >
                            {onlyUnread ? 'Hiển thị tất cả' : 'Chỉ chưa đọc'}
                        </button>
                        <button
                            type="button"
                            className="px-3 py-1.5 text-sm rounded-full bg-primary text-white hover:opacity-90 disabled:opacity-50"
                            onClick={handleMarkAllRead}
                            disabled={notifications.length === 0}
                        >
                            Đánh dấu tất cả đã đọc
                        </button>
                    </div>
                </div>

                {error && (
                    <div className="mt-4 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700">
                        {error}
                    </div>
                )}

                <section className="mt-4 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-xl overflow-hidden">
                    {loading ? (
                        <div className="px-4 py-6 text-sm text-slate-500">Đang tải thông báo...</div>
                    ) : notifications.length === 0 ? (
                        <div className="px-4 py-6 text-sm text-slate-500">Không có thông báo nào.</div>
                    ) : (
                        notifications.map((n) => {
                            const parsed = parseNotificationContent(n.content);
                            const isViolation = isViolationNotification(n.type);
                            const summaryText = buildViolationSummary(n, parsed);
                            return (
                                <button
                                    type="button"
                                    key={n.id}
                                    className={`w-full text-left px-4 py-3 border-b last:border-b-0 border-slate-100 dark:border-slate-800 transition-colors ${
                                        n.isRead
                                            ? 'hover:bg-slate-50 dark:hover:bg-slate-800/40'
                                            : 'bg-green-50/80 dark:bg-green-900/15 hover:bg-green-100/80 dark:hover:bg-green-900/25'
                                    }`}
                                    onClick={() => handleOpenNotification(n)}
                                >
                                    <div className="flex items-center gap-2 flex-wrap">
                                        <p className={`text-sm font-semibold ${n.isRead ? 'text-slate-500' : 'text-slate-900 dark:text-white'}`}>
                                            {n.title || 'Thông báo'}
                                        </p>
                                        {isViolation && (
                                            <span className="inline-flex rounded-full border border-amber-300 bg-amber-50 px-2 py-0.5 text-[10px] font-semibold text-amber-700">
                                                Liên quan vi phạm
                                            </span>
                                        )}
                                    </div>
                                    <p className="text-xs text-slate-600 mt-1 line-clamp-2">{summaryText}</p>
                                    <p className="text-[11px] text-slate-400 mt-1">
                                        {n.createdAt ? new Date(n.createdAt).toLocaleString('vi-VN') : ''}
                                    </p>
                                </button>
                            );
                        })
                    )}
                </section>
            </main>
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
                                onClick={handleNavigateFromPopup}
                                className="rounded-full bg-primary px-4 py-1.5 text-sm font-semibold text-white hover:bg-primary/90 transition-colors"
                            >
                                Mở trang liên quan
                            </button>
                        </div>
                    </div>
                </div>
            )}
            <Footer />
        </div>
    );
}
