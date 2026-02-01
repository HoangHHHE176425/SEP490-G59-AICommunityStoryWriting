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
    Search
} from 'lucide-react';

export function AdminLayout({ children, activePage = 'dashboard' }) {
    const [isSidebarOpen, setIsSidebarOpen] = useState(true);
    const [isMobileSidebarOpen, setIsMobileSidebarOpen] = useState(false);

    const menuItems = [
        { id: 'dashboard', label: 'Dashboard', icon: LayoutDashboard },
        { id: 'categories', label: 'Quản lý thể loại', icon: Bookmark },
        { id: 'stories', label: 'Quản lý truyện', icon: FileText },
        { id: 'users', label: 'Quản lý người dùng', icon: Users },
        { id: 'comments', label: 'Quản lý bình luận', icon: MessageSquare },
        { id: 'settings', label: 'Cài đặt', icon: Settings },
    ];

    return (
        <>
            {/* Top Header - Fixed */}
            <div className="fixed top-0 left-0 right-0 h-16 bg-white border-b border-slate-200 z-50">
                <div className="h-full px-4 flex items-center justify-between">
                    <div className="flex items-center gap-4">
                        <button
                            onClick={() => setIsSidebarOpen(!isSidebarOpen)}
                            className="hidden lg:block p-2 hover:bg-slate-100 rounded-lg"
                        >
                            <Menu className="w-5 h-5 text-slate-900" />
                        </button>
                        <button
                            onClick={() => setIsMobileSidebarOpen(!isMobileSidebarOpen)}
                            className="lg:hidden p-2 hover:bg-slate-100 rounded-lg"
                        >
                            <Menu className="w-5 h-5 text-slate-900" />
                        </button>
                        <h1 className="text-xl font-bold text-slate-900">
                            Admin <span style={{ color: '#13ec5b' }}>Panel</span>
                        </h1>
                    </div>

                    <div className="flex items-center gap-3">
                        <div className="hidden md:flex items-center gap-2 px-4 py-2 bg-slate-100 rounded-lg">
                            <Search className="w-4 h-4 text-slate-400" />
                            <input
                                type="text"
                                placeholder="Tìm kiếm..."
                                className="bg-transparent border-none outline-none text-sm text-slate-900 w-64"
                            />
                        </div>

                        <button className="relative p-2 hover:bg-slate-100 rounded-lg">
                            <Bell className="w-5 h-5 text-slate-900" />
                            <span className="absolute top-1 right-1 w-2 h-2 bg-red-500 rounded-full"></span>
                        </button>

                        <div className="flex items-center gap-3 pl-3 border-l border-slate-200">
                            <img
                                src="https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?w=40&h=40&fit=crop"
                                alt="Admin"
                                className="w-8 h-8 rounded-full"
                            />
                            <div className="hidden sm:block">
                                <p className="text-sm font-semibold text-slate-900">Admin User</p>
                                <p className="text-xs text-slate-500">Administrator</p>
                            </div>
                        </div>
                    </div>
                </div>
            </div>

            {/* Sidebar - Desktop Fixed */}
            <div
                className={`hidden lg:block fixed top-16 left-0 bottom-0 bg-white border-r border-slate-200 z-40 transition-all duration-300 ${isSidebarOpen ? 'w-64' : 'w-20'
                    }`}
            >
                <nav className="p-4 space-y-2">
                    {menuItems.map((item) => {
                        const Icon = item.icon;
                        const isActive = activePage === item.id;
                        return (
                            <button
                                key={item.id}
                                className={`w-full flex items-center gap-3 px-4 py-3 rounded-lg transition-all ${isActive
                                    ? 'text-slate-900'
                                    : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
                                    }`}
                                style={isActive ? { backgroundColor: 'rgba(19, 236, 91, 0.1)', color: '#13ec5b' } : {}}
                            >
                                <Icon className="w-5 h-5 shrink-0" />
                                {isSidebarOpen && (
                                    <span className="text-sm font-medium">{item.label}</span>
                                )}
                            </button>
                        );
                    })}
                </nav>

                <div className="absolute bottom-4 left-4 right-4">
                    <button className="w-full flex items-center gap-3 px-4 py-3 text-red-600 hover:bg-red-50 rounded-lg transition-all">
                        <LogOut className="w-5 h-5 shrink-0" />
                        {isSidebarOpen && <span className="text-sm font-medium">Đăng xuất</span>}
                    </button>
                </div>
            </div>

            {/* Mobile Sidebar Overlay */}
            {isMobileSidebarOpen && (
                <div
                    className="lg:hidden fixed inset-0 bg-black/50 z-40"
                    style={{ top: '64px' }}
                    onClick={() => setIsMobileSidebarOpen(false)}
                ></div>
            )}

            {/* Sidebar - Mobile */}
            {isMobileSidebarOpen && (
                <div className="lg:hidden fixed left-0 bottom-0 w-64 bg-white border-r border-slate-200 z-50 flex flex-col" style={{ top: '64px' }}>
                    <nav className="flex-1 p-4 space-y-2 overflow-y-auto">
                        {menuItems.map((item) => {
                            const Icon = item.icon;
                            const isActive = activePage === item.id;
                            return (
                                <button
                                    key={item.id}
                                    onClick={() => setIsMobileSidebarOpen(false)}
                                    className={`w-full flex items-center gap-3 px-4 py-3 rounded-lg transition-all ${isActive
                                        ? 'text-slate-900'
                                        : 'text-slate-600 hover:bg-slate-100 hover:text-slate-900'
                                        }`}
                                    style={isActive ? { backgroundColor: 'rgba(19, 236, 91, 0.1)', color: '#13ec5b' } : {}}
                                >
                                    <Icon className="w-5 h-5" />
                                    <span className="text-sm font-medium">{item.label}</span>
                                </button>
                            );
                        })}
                    </nav>
                    <div className="p-4 border-t border-slate-200">
                        <button className="w-full flex items-center gap-3 px-4 py-3 text-red-600 hover:bg-red-50 rounded-lg transition-all">
                            <LogOut className="w-5 h-5" />
                            <span className="text-sm font-medium">Đăng xuất</span>
                        </button>
                    </div>
                </div>
            )}

            {/* Main Content Area */}
            <div
                className={`pt-16 min-h-screen bg-slate-50 transition-all duration-300 ${isSidebarOpen ? 'lg:pl-64' : 'lg:pl-20'
                    }`}
            >
                <div className="p-6">
                    {children}
                </div>
            </div>
        </>
    );
}
