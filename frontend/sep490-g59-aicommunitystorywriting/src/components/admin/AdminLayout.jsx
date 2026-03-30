import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { resolveBackendUrl } from '../../utils/resolveBackendUrl';
import { createInitialAvatarDataUrl } from '../../utils/avatarFallback';
import { getSystemWalletBalance } from '../../api/admin/walletApi';
import { getAdminTransactions } from '../../api/admin/transactionsApi';
import {
    LayoutDashboard,
    Bookmark,
    Users,
    FileText,
    MessageSquare,
    Menu,
    LogOut,
    Bell,
    X,
    CheckSquare,
    Shield,
    Brain,
    AlertTriangle,
    Wallet,
    Landmark,
    Flag,
} from 'lucide-react';

const ROLE_LABELS = {
    USER: 'Người dùng',
    AUTHOR: 'Tác giả',
    MODERATOR: 'Kiểm duyệt',
    ADMIN: 'Quản trị',
    COMPLIANCE: 'Compliance',
};

const ALL_MENU_ITEMS = [
    { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
    { id: 'wallet-dashboard', label: 'Ví hệ thống', icon: Wallet },
    { id: 'categories', label: 'Quản lý thể loại', icon: Bookmark },
    { id: 'publication', label: 'Quản lý xuất bản', icon: CheckSquare },
    { id: 'moderator-logs', label: 'Nhật ký kiểm duyệt', icon: FileText },
    { id: 'review-escalations', label: 'Quản lý đơn', icon: Flag },
    { id: 'stories', label: 'Quản lý truyện', icon: FileText },
    { id: 'violations', label: 'Quản lý vi phạm', icon: AlertTriangle },
    { id: 'users', label: 'Quản lý người dùng', icon: Users },
    { id: 'comments', label: 'Quản lý bình luận', icon: MessageSquare },
    { id: 'policies', label: 'Quản lý Policy', icon: Shield },
    { id: 'ai-config', label: 'Cấu hình AI', icon: Brain },
];

/** Yêu cầu UI: ẩn một số tab ở màn Admin. */
const HIDE_MENU_IDS_FOR_ADMIN = new Set(['publication', 'stories', 'comments']);

/** Menu theo role để tách rõ màn Admin / Moderator / Compliance. */
const ROLE_MENU_IDS = {
    ADMIN: null, // null = full menu
    MODERATOR: new Set(['dashboard', 'publication']),
    COMPLIANCE: new Set(['violations']),
};

export function AdminLayout({ children, activePage = 'dashboard', onNavigate }) {
    const navigate = useNavigate();
    const { user, logout, role } = useAuth();
    const roleUpper = (role ?? user?.role ?? user?.Role ?? '').toString().toUpperCase();
    const roleMenuIds = ROLE_MENU_IDS[roleUpper] ?? ROLE_MENU_IDS.ADMIN;
    const hasLimitedMenu = !!roleMenuIds;
    const menuItems = (() => {
        const base = roleMenuIds ? ALL_MENU_ITEMS.filter((item) => roleMenuIds.has(item.id)) : ALL_MENU_ITEMS;
        if (roleUpper === 'ADMIN') return base.filter((item) => !HIDE_MENU_IDS_FOR_ADMIN.has(item.id));
        return base;
    })();

    const displayName = user?.displayName ?? user?.DisplayName ?? user?.email ?? 'Admin';
    const roleLabel = ROLE_LABELS[roleUpper] ?? 'Quản trị';
    const [isSidebarOpen, setIsSidebarOpen] = useState(true);
    const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false);
    // Số dư ví hệ thống (API: GET /api/admin/wallet/balance)
    // MODERATOR / COMPLIANCE: không xem ví hệ thống -> không gọi API.
    const [systemWalletBalance, setSystemWalletBalance] = useState(null);
    const [notiOpen, setNotiOpen] = useState(false);
    const [pendingWithdrawCount, setPendingWithdrawCount] = useState(0);
    const [notiError, setNotiError] = useState('');
    useEffect(() => {
        if (hasLimitedMenu) return;
        let cancelled = false;
        (async () => {
            try {
                const data = await getSystemWalletBalance();
                const balance = data?.balanceCoin ?? data?.balance_coin ?? data?.systemWalletBalanceCoins;
                if (!cancelled && typeof balance === 'number') setSystemWalletBalance(balance);
            } catch {
                // Best-effort: keep null if API fails
            }
        })();
        return () => {
            cancelled = true;
        };
    }, [hasLimitedMenu]);
    useEffect(() => {
        if (hasLimitedMenu) return;
        const handler = (evt) => {
            const next = evt?.detail?.balance;
            if (typeof next === 'number' && Number.isFinite(next)) setSystemWalletBalance(next);
        };
        window.addEventListener('system-wallet:balance', handler);
        return () => window.removeEventListener('system-wallet:balance', handler);
    }, [hasLimitedMenu]);

    useEffect(() => {
        setNotiOpen(false);
    }, [activePage]);

    // Admin notifications: pending withdraw requests.
    useEffect(() => {
        if (hasLimitedMenu) return;
        if (roleUpper !== 'ADMIN') return;

        let cancelled = false;
        const fetchCount = async () => {
            try {
                const [a, b] = await Promise.all([
                    getAdminTransactions({ type: 'WITHDRAW', status: 'PENDING', page: 1, pageSize: 1 }).catch(() => ({ totalCount: 0 })),
                    getAdminTransactions({ type: 'WITHDRAW', status: 'PENDING_REVIEW', page: 1, pageSize: 1 }).catch(() => ({ totalCount: 0 })),
                ]);
                if (cancelled) return;
                setPendingWithdrawCount(Number(a?.totalCount ?? 0) + Number(b?.totalCount ?? 0));
                setNotiError('');
            } catch {
                if (!cancelled) setNotiError('Không tải được thông báo.');
            }
        };

        fetchCount();
        const id = setInterval(fetchCount, 15000);
        return () => {
            cancelled = true;
            clearInterval(id);
        };
    }, [hasLimitedMenu, roleUpper]);

    const handleLogout = async () => {
        try {
            await logout();
        } finally {
            setIsMobileSidebarOpen(false);
            navigate('/admin/login');
        }
    };

    const sidebarWidth = isSidebarOpen ? 256 : 80;

    return (
        <div
            style={{
                minHeight: '100vh',
                backgroundColor: '#f8fafc',
                // Admin theme tokens (đồng nhất màu chủ đạo)
                '--admin-primary': '#13ec5b',
                '--admin-primary-soft': 'rgba(19, 236, 91, 0.12)',
                '--admin-primary-ink': '#166534',
                '--admin-border': '#e2e8f0',
                '--admin-surface': '#ffffff',
                '--admin-muted': '#64748b',
                '--admin-text': '#1e293b',
            }}
        >
            {/* Header */}
            <header
                style={{
                    position: 'fixed',
                    top: 0,
                    left: 0,
                    right: 0,
                    height: '64px',
                    backgroundColor: 'var(--admin-surface)',
                    borderBottom: '1px solid var(--admin-border)',
                    zIndex: 50,
                    display: 'flex',
                    alignItems: 'center',
                    padding: '0 1rem'
                }}
            >
                <div style={{ width: '100%', display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                        <button
                            onClick={() => setIsSidebarOpen(!isSidebarOpen)}
                            style={{
                                display: window.innerWidth >= 1024 ? 'block' : 'none',
                                padding: '0.5rem',
                                border: 'none',
                                background: 'transparent',
                                borderRadius: '0.5rem',
                                cursor: 'pointer'
                            }}
                            onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#f1f5f9'}
                            onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                        >
                            <Menu style={{ width: '20px', height: '20px', color: '#1e293b' }} />
                        </button>
                        <button
                            onClick={() => setIsMobileSidebarOpen(!isMobileSidebarOpen)}
                            style={{
                                display: window.innerWidth < 1024 ? 'block' : 'none',
                                padding: '0.5rem',
                                border: 'none',
                                background: 'transparent',
                                borderRadius: '0.5rem',
                                cursor: 'pointer'
                            }}
                            className="lg:hidden"
                        >
                            <Menu style={{ width: '20px', height: '20px', color: '#1e293b' }} />
                        </button>
                        <h1 style={{ fontSize: '1.25rem', fontWeight: 'bold', color: 'var(--admin-text)', margin: 0 }}>
                            {roleUpper === 'MODERATOR' ? 'Moderator' : roleUpper === 'COMPLIANCE' ? 'Compliance' : 'Admin'}{' '}
                            <span style={{ color: 'var(--admin-primary)' }}>Panel</span>
                        </h1>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                        {/* Icon ví hệ thống + số dư (không hiển thị cho MODERATOR/COMPLIANCE) */}
                        {!hasLimitedMenu && (
                            <button
                                onClick={() => onNavigate('wallet-dashboard')}
                                title="Ví hệ thống"
                                style={{
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: '0.5rem',
                                    padding: '0.5rem 0.75rem',
                                    border: '1px solid var(--admin-border)',
                                    borderRadius: '0.5rem',
                                    backgroundColor: '#f0fdf4',
                                    cursor: 'pointer',
                                    transition: 'background-color 0.2s'
                                }}
                                onMouseEnter={(e) => {
                                    e.currentTarget.style.backgroundColor = '#dcfce7';
                                }}
                                onMouseLeave={(e) => {
                                    e.currentTarget.style.backgroundColor = '#f0fdf4';
                                }}
                            >
                                <Wallet style={{ width: '18px', height: '18px', color: 'var(--admin-primary-ink)' }} />
                                <span style={{ fontSize: '0.8125rem', fontWeight: 600, color: 'var(--admin-primary-ink)' }}>
                                    {systemWalletBalance != null
                                        ? `${Number(systemWalletBalance).toLocaleString('vi-VN')} Coin`
                                        : '...'}
                                </span>
                            </button>
                        )}

                        <button
                            onClick={() => setNotiOpen((v) => !v)}
                            style={{
                                position: 'relative',
                                padding: '0.5rem',
                                border: 'none',
                                background: 'transparent',
                                borderRadius: '0.5rem',
                                cursor: 'pointer'
                            }}
                        >
                            <Bell style={{ width: '20px', height: '20px', color: '#1e293b' }} />
                            {pendingWithdrawCount > 0 ? (
                                <span
                                    style={{
                                        position: 'absolute',
                                        top: '2px',
                                        right: '2px',
                                        minWidth: '16px',
                                        height: '16px',
                                        padding: '0 5px',
                                        backgroundColor: '#ef4444',
                                        color: '#ffffff',
                                        borderRadius: '999px',
                                        fontSize: '10px',
                                        fontWeight: 800,
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        border: '2px solid var(--admin-surface)',
                                    }}
                                >
                                    {pendingWithdrawCount > 99 ? '99+' : pendingWithdrawCount}
                                </span>
                            ) : null}
                        </button>

                        {notiOpen && roleUpper === 'ADMIN' && !hasLimitedMenu ? (
                            <div style={{ position: 'relative' }}>
                                <div
                                    onClick={() => setNotiOpen(false)}
                                    style={{
                                        position: 'fixed',
                                        inset: 0,
                                        zIndex: 55,
                                        background: 'transparent',
                                    }}
                                />
                                <div
                                    style={{
                                        position: 'absolute',
                                        right: 0,
                                        top: 'calc(100% + 10px)',
                                        width: '340px',
                                        zIndex: 56,
                                        backgroundColor: 'var(--admin-surface)',
                                        border: '1px solid var(--admin-border)',
                                        borderRadius: '14px',
                                        boxShadow: '0 12px 30px rgba(15, 23, 42, 0.12)',
                                        overflow: 'hidden',
                                    }}
                                >
                                    <div style={{ padding: '12px 14px', borderBottom: '1px solid var(--admin-border)' }}>
                                        <p style={{ margin: 0, fontSize: '12px', fontWeight: 800, color: 'var(--admin-text)' }}>Thông báo</p>
                                        <p style={{ margin: '4px 0 0 0', fontSize: '11px', color: 'var(--admin-muted)' }}>
                                            {notiError ? notiError : 'Cập nhật tự động mỗi 15 giây'}
                                        </p>
                                    </div>

                                    <button
                                        type="button"
                                        onClick={() => {
                                            window.sessionStorage.setItem(
                                                'admin_transactions_prefill',
                                                JSON.stringify({ type: 'WITHDRAW', status: 'PENDING', page: 1 })
                                            );
                                            setNotiOpen(false);
                                            onNavigate('transactions');
                                        }}
                                        style={{
                                            width: '100%',
                                            textAlign: 'left',
                                            padding: '12px 14px',
                                            border: 'none',
                                            background: 'transparent',
                                            cursor: 'pointer',
                                            display: 'flex',
                                            alignItems: 'flex-start',
                                            gap: '10px',
                                        }}
                                        onMouseEnter={(e) => (e.currentTarget.style.backgroundColor = '#f8fafc')}
                                        onMouseLeave={(e) => (e.currentTarget.style.backgroundColor = 'transparent')}
                                        title="Mở danh sách giao dịch và lọc rút tiền chờ duyệt"
                                    >
                                        <div
                                            style={{
                                                width: '34px',
                                                height: '34px',
                                                borderRadius: '10px',
                                                backgroundColor: 'rgba(239, 68, 68, 0.10)',
                                                display: 'flex',
                                                alignItems: 'center',
                                                justifyContent: 'center',
                                                flexShrink: 0,
                                            }}
                                        >
                                            <Landmark style={{ width: '18px', height: '18px', color: '#ef4444' }} />
                                        </div>
                                        <div style={{ minWidth: 0 }}>
                                            <p style={{ margin: 0, fontSize: '12px', fontWeight: 800, color: 'var(--admin-text)' }}>
                                                Yêu cầu rút tiền chờ duyệt
                                            </p>
                                            <p style={{ margin: '4px 0 0 0', fontSize: '11px', color: 'var(--admin-muted)' }}>
                                                Hiện có <span style={{ fontWeight: 900, color: pendingWithdrawCount ? '#ef4444' : 'var(--admin-text)' }}>{pendingWithdrawCount}</span> yêu cầu cần xử lý
                                            </p>
                                        </div>
                                    </button>

                                    {pendingWithdrawCount === 0 ? (
                                        <div style={{ padding: '12px 14px', borderTop: '1px solid var(--admin-border)' }}>
                                            <p style={{ margin: 0, fontSize: '11px', color: 'var(--admin-muted)' }}>
                                                Không có yêu cầu mới.
                                            </p>
                                        </div>
                                    ) : null}
                                </div>
                            </div>
                        ) : null}

                        <div
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.75rem',
                                paddingLeft: '0.75rem',
                                borderLeft: '1px solid var(--admin-border)'
                            }}
                        >
                            <img
                                src={
                                    user?.avatarUrl
                                        ? resolveBackendUrl(user.avatarUrl)
                                        : createInitialAvatarDataUrl(displayName, 96)
                                }
                                alt="Admin"
                                style={{ width: '32px', height: '32px', borderRadius: '50%' }}
                                onError={(e) => { e.target.src = createInitialAvatarDataUrl(displayName, 96); }}
                            />
                            <div style={{ display: window.innerWidth >= 640 ? 'block' : 'none' }} className="hidden sm:block">
                                <p style={{ fontSize: '0.875rem', fontWeight: 600, color: '#1e293b', margin: 0 }}>{displayName}</p>
                                <p style={{ fontSize: '0.75rem', color: '#64748b', margin: 0 }}>{roleLabel}</p>
                            </div>
                        </div>
                    </div>
                </div>
            </header>

            {/* Sidebar Desktop */}
            <aside
                style={{
                    display: window.innerWidth >= 1024 ? 'block' : 'none',
                    position: 'fixed',
                    top: '64px',
                    left: 0,
                    bottom: 0,
                    width: `${sidebarWidth}px`,
                    backgroundColor: 'var(--admin-surface)',
                    borderRight: '1px solid var(--admin-border)',
                    transition: 'width 0.3s ease',
                    zIndex: 40,
                    overflowY: 'auto'
                }}
                className="hidden lg:block"
            >
                <nav style={{ padding: '1rem', display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                    {menuItems.map((item) => {
                        const Icon = item.icon;
                        const isActive = activePage === item.id;
                        const label =
                            item.id === 'dashboard' && roleUpper === 'MODERATOR'
                                ? 'Tổng quan kiểm duyệt'
                                : item.label;
                        return (
                            <button
                                key={item.id}
                                onClick={() => onNavigate(item.id)}
                                style={{
                                    width: '100%',
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: '0.75rem',
                                    padding: '0.75rem 1rem',
                                    border: 'none',
                                    borderRadius: '0.5rem',
                                    backgroundColor: isActive ? 'var(--admin-primary-soft)' : 'transparent',
                                    color: isActive ? 'var(--admin-primary)' : 'var(--admin-muted)',
                                    fontSize: '0.875rem',
                                    fontWeight: 500,
                                    cursor: 'pointer',
                                    transition: 'all 0.2s',
                                    textAlign: 'left'
                                }}
                                onMouseEnter={(e) => {
                                    if (!isActive) {
                                        e.currentTarget.style.backgroundColor = '#f1f5f9';
                                        e.currentTarget.style.color = 'var(--admin-text)';
                                    }
                                }}
                                onMouseLeave={(e) => {
                                    if (!isActive) {
                                        e.currentTarget.style.backgroundColor = 'transparent';
                                        e.currentTarget.style.color = 'var(--admin-muted)';
                                    }
                                }}
                            >
                                <Icon style={{ width: '20px', height: '20px', flexShrink: 0 }} />
                                {isSidebarOpen && <span>{label}</span>}
                            </button>
                        );
                    })}
                </nav>

                <div style={{ position: 'absolute', bottom: '1rem', left: '1rem', right: '1rem' }}>
                    <button
                        onClick={handleLogout}
                        style={{
                            width: '100%',
                            display: 'flex',
                            alignItems: 'center',
                            gap: '0.75rem',
                            padding: '0.75rem 1rem',
                            border: 'none',
                            borderRadius: '0.5rem',
                            backgroundColor: 'transparent',
                            color: '#ef4444',
                            fontSize: '0.875rem',
                            fontWeight: 500,
                            cursor: 'pointer',
                            transition: 'all 0.2s'
                        }}
                        onMouseEnter={(e) => e.currentTarget.style.backgroundColor = 'rgba(239, 68, 68, 0.1)'}
                        onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                    >
                        <LogOut style={{ width: '20px', height: '20px', flexShrink: 0 }} />
                        {isSidebarOpen && <span>Đăng xuất</span>}
                    </button>
                </div>
            </aside>

            {/* Mobile Sidebar Backdrop */}
            {isMobileSidebarOpen && (
                <div
                    onClick={() => setIsMobileSidebarOpen(false)}
                    style={{
                        display: window.innerWidth < 1024 ? 'block' : 'none',
                        position: 'fixed',
                        top: 0,
                        left: 0,
                        right: 0,
                        bottom: 0,
                        backgroundColor: 'rgba(0, 0, 0, 0.5)',
                        zIndex: 40
                    }}
                    className="lg:hidden"
                ></div>
            )}

            {/* Sidebar Mobile */}
            {isMobileSidebarOpen && (
                <aside
                    style={{
                        display: window.innerWidth < 1024 ? 'flex' : 'none',
                        position: 'fixed',
                        top: 0,
                        left: 0,
                        bottom: 0,
                        width: '256px',
                        backgroundColor: '#ffffff',
                        borderRight: '1px solid #e2e8f0',
                        zIndex: 50,
                        flexDirection: 'column'
                    }}
                    className="lg:hidden"
                >
                    <div
                        style={{
                            height: '64px',
                            padding: '0 1rem',
                            display: 'flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            borderBottom: '1px solid #e2e8f0'
                        }}
                    >
                        <h2 style={{ fontSize: '1.125rem', fontWeight: 'bold', color: '#1e293b', margin: 0 }}>Menu</h2>
                        <button
                            onClick={() => setIsMobileSidebarOpen(false)}
                            style={{
                                padding: '0.5rem',
                                border: 'none',
                                background: 'transparent',
                                borderRadius: '0.5rem',
                                cursor: 'pointer'
                            }}
                        >
                            <X style={{ width: '20px', height: '20px' }} />
                        </button>
                    </div>

                    <nav
                        style={{
                            flex: 1,
                            padding: '1rem',
                            display: 'flex',
                            flexDirection: 'column',
                            gap: '0.5rem',
                            overflowY: 'auto'
                        }}
                    >
                        {menuItems.map((item) => {
                            const Icon = item.icon;
                            const isActive = activePage === item.id;
                            const label =
                                item.id === 'dashboard' && roleUpper === 'MODERATOR'
                                    ? 'Tổng quan kiểm duyệt'
                                    : item.label;
                            return (
                                <button
                                    key={item.id}
                                    onClick={() => {
                                        setIsMobileSidebarOpen(false);
                                        onNavigate(item.id);
                                    }}
                                    style={{
                                        width: '100%',
                                        display: 'flex',
                                        alignItems: 'center',
                                        gap: '0.75rem',
                                        padding: '0.75rem 1rem',
                                        border: 'none',
                                        borderRadius: '0.5rem',
                                    backgroundColor: isActive ? 'var(--admin-primary-soft)' : 'transparent',
                                    color: isActive ? 'var(--admin-primary)' : 'var(--admin-muted)',
                                        fontSize: '0.875rem',
                                        fontWeight: 500,
                                        cursor: 'pointer',
                                        transition: 'all 0.2s',
                                        textAlign: 'left'
                                    }}
                                >
                                    <Icon style={{ width: '20px', height: '20px' }} />
                                    <span>{label}</span>
                                </button>
                            );
                        })}
                    </nav>

                    <div style={{ padding: '1rem', borderTop: '1px solid #e2e8f0' }}>
                        <button
                            onClick={handleLogout}
                            style={{
                                width: '100%',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.75rem',
                                padding: '0.75rem 1rem',
                                border: 'none',
                                borderRadius: '0.5rem',
                                backgroundColor: 'transparent',
                                color: '#ef4444',
                                fontSize: '0.875rem',
                                fontWeight: 500,
                                cursor: 'pointer'
                            }}
                        >
                            <LogOut style={{ width: '20px', height: '20px' }} />
                            <span>Đăng xuất</span>
                        </button>
                    </div>
                </aside>
            )}

            {/* Main Content */}
            <main
                style={{
                    marginTop: '64px',
                    marginLeft: window.innerWidth >= 1024 ? `${sidebarWidth}px` : 0,
                    transition: 'margin-left 0.3s ease',
                    minHeight: 'calc(100vh - 64px)',
                    backgroundColor: '#f8fafc'
                }}
                className={isSidebarOpen ? 'lg:ml-64' : 'lg:ml-20'}
            >
                <div style={{ padding: '1.5rem' }}>
                    {children}
                </div>
            </main>
        </div>
    );
}