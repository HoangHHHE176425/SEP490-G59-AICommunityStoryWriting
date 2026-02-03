import { useState } from 'react';
import {
    LayoutDashboard,
    Bookmark,
    Users,
    FileText,
    MessageSquare,
    Settings,
    Menu,
    LogOut,
    Bell,
    Search,
    X,
    CheckSquare
} from 'lucide-react';

export function AdminLayout({ children, activePage = 'dashboard', onNavigate }) {
    const [isSidebarOpen, setIsSidebarOpen] = useState(true);
    const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false);

    const menuItems = [
        { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
        { id: 'categories', label: 'Quản lý thể loại', icon: Bookmark },
        { id: 'publication', label: 'Quản lý xuất bản', icon: CheckSquare },
        { id: 'stories', label: 'Quản lý truyện', icon: FileText },
        { id: 'users', label: 'Quản lý người dùng', icon: Users },
        { id: 'comments', label: 'Quản lý bình luận', icon: MessageSquare },
        { id: 'settings', label: 'Cài đặt', icon: Settings },
    ];

    const sidebarWidth = isSidebarOpen ? 256 : 80;

    return (
        <div style={{ minHeight: '100vh', backgroundColor: '#f8fafc' }}>
            {/* Header */}
            <header
                style={{
                    position: 'fixed',
                    top: 0,
                    left: 0,
                    right: 0,
                    height: '64px',
                    backgroundColor: '#ffffff',
                    borderBottom: '1px solid #e2e8f0',
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
                        <h1 style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#1e293b', margin: 0 }}>
                            Admin <span style={{ color: '#13ec5b' }}>Panel</span>
                        </h1>
                    </div>

                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                        <div
                            style={{
                                display: window.innerWidth >= 768 ? 'flex' : 'none',
                                alignItems: 'center',
                                gap: '0.5rem',
                                padding: '0.5rem 1rem',
                                backgroundColor: '#f1f5f9',
                                borderRadius: '0.5rem'
                            }}
                            className="hidden md:flex"
                        >
                            <Search style={{ width: '16px', height: '16px', color: '#94a3b8' }} />
                            <input
                                type="text"
                                placeholder="Tìm kiếm..."
                                style={{
                                    background: 'transparent',
                                    border: 'none',
                                    outline: 'none',
                                    fontSize: '0.875rem',
                                    color: '#1e293b',
                                    width: '16rem'
                                }}
                            />
                        </div>

                        <button
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
                            <span
                                style={{
                                    position: 'absolute',
                                    top: '4px',
                                    right: '4px',
                                    width: '8px',
                                    height: '8px',
                                    backgroundColor: '#ef4444',
                                    borderRadius: '50%'
                                }}
                            ></span>
                        </button>

                        <div
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.75rem',
                                paddingLeft: '0.75rem',
                                borderLeft: '1px solid #e2e8f0'
                            }}
                        >
                            <img
                                src="https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?w=40&h=40&fit=crop"
                                alt="Admin"
                                style={{ width: '32px', height: '32px', borderRadius: '50%' }}
                            />
                            <div style={{ display: window.innerWidth >= 640 ? 'block' : 'none' }} className="hidden sm:block">
                                <p style={{ fontSize: '0.875rem', fontWeight: 600, color: '#1e293b', margin: 0 }}>Admin User</p>
                                <p style={{ fontSize: '0.75rem', color: '#64748b', margin: 0 }}>Administrator</p>
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
                    backgroundColor: '#ffffff',
                    borderRight: '1px solid #e2e8f0',
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
                                    backgroundColor: isActive ? 'rgba(19, 236, 91, 0.1)' : 'transparent',
                                    color: isActive ? '#13ec5b' : '#64748b',
                                    fontSize: '0.875rem',
                                    fontWeight: 500,
                                    cursor: 'pointer',
                                    transition: 'all 0.2s',
                                    textAlign: 'left'
                                }}
                                onMouseEnter={(e) => {
                                    if (!isActive) {
                                        e.currentTarget.style.backgroundColor = '#f1f5f9';
                                        e.currentTarget.style.color = '#1e293b';
                                    }
                                }}
                                onMouseLeave={(e) => {
                                    if (!isActive) {
                                        e.currentTarget.style.backgroundColor = 'transparent';
                                        e.currentTarget.style.color = '#64748b';
                                    }
                                }}
                            >
                                <Icon style={{ width: '20px', height: '20px', flexShrink: 0 }} />
                                {isSidebarOpen && <span>{item.label}</span>}
                            </button>
                        );
                    })}
                </nav>

                <div style={{ position: 'absolute', bottom: '1rem', left: '1rem', right: '1rem' }}>
                    <button
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
                                        backgroundColor: isActive ? 'rgba(19, 236, 91, 0.1)' : 'transparent',
                                        color: isActive ? '#13ec5b' : '#64748b',
                                        fontSize: '0.875rem',
                                        fontWeight: 500,
                                        cursor: 'pointer',
                                        transition: 'all 0.2s',
                                        textAlign: 'left'
                                    }}
                                >
                                    <Icon style={{ width: '20px', height: '20px' }} />
                                    <span>{item.label}</span>
                                </button>
                            );
                        })}
                    </nav>

                    <div style={{ padding: '1rem', borderTop: '1px solid #e2e8f0' }}>
                        <button
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