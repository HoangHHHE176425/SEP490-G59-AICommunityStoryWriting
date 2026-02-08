import { Search, Bell, Edit, BookOpen, Menu, X, ChevronDown, Coins, User, Library, LogOut, FileText, List } from 'lucide-react';
import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';

export function Header() {
    const navigate = useNavigate();
    const { user, logout, isAuthenticated } = useAuth();
    const [isMenuOpen, setIsMenuOpen] = useState(false);
    const [isGenreOpen, setIsGenreOpen] = useState(false);
    const [isUserMenuOpen, setIsUserMenuOpen] = useState(false);

    const userCoins = user?.coins || 0;

    const handleLogout = () => {
        logout();
        setIsUserMenuOpen(false);
        navigate('/');
    };

    const genres = [
        'Tiên hiệp',
        'Kiếm hiệp',
        'Ngôn tình',
        'Đô thị',
        'Huyền huyễn',
        'Khoa học viễn tưởng',
        'Trinh thám',
        'Kinh dị',
        'Lịch sử',
        'Đam mỹ',
        'Trọng sinh',
        'Xuyên không',
    ];

    return (
        <header className="sticky top-0 z-50 w-full bg-slate-900/95 backdrop-blur-md border-b border-slate-700/50">
            <div className="max-w-[1280px] mx-auto px-4 h-16 flex items-center justify-between gap-8">
                {/* Logo & Brand */}
                <Link to="/home" className="flex items-center gap-2 shrink-0 hover:opacity-80 transition-opacity">
                    <div className="size-9 bg-primary rounded-lg flex items-center justify-center text-white">
                        <BookOpen className="w-5 h-5" />
                    </div>
                    <h1 className="text-xl font-bold tracking-tight text-white">CSW_AI</h1>
                </Link>

                {/* Search Bar (Center) */}
                <div className="flex-1 max-w-2xl hidden md:block">
                    <div className="relative group">
                        <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400 group-focus-within:text-primary transition-colors">
                            <Search className="w-5 h-5" />
                        </div>
                        <input
                            className="block w-full pl-10 pr-4 py-2 bg-slate-800 border border-slate-700 rounded-full text-sm focus:ring-2 focus:ring-primary/50 transition-all placeholder:text-slate-500 outline-none text-white"
                            placeholder="Tìm kiếm truyện, tác giả, thể loại..."
                            type="text"
                        />
                    </div>
                </div>

                {/* Main Nav & User Actions */}
                <nav className="flex items-center gap-6">
                    <div className="hidden lg:flex items-center gap-6 text-sm font-semibold text-slate-300">
                        <Link to="/home" className="hover:text-primary transition-colors">Trang chủ</Link>
                        {/* Thể loại dropdown */}
                        <div className="relative group">
                            <button
                                className="flex items-center gap-1 hover:text-primary transition-colors"
                                onClick={() => setIsGenreOpen(!isGenreOpen)}
                                onBlur={() => setTimeout(() => setIsGenreOpen(false), 200)}
                            >
                                Thể loại
                                <ChevronDown className={`w-4 h-4 transition-transform ${isGenreOpen ? 'rotate-180' : ''}`} />
                            </button>

                            {isGenreOpen && (
                                <div
                                    className="absolute top-full left-0 mt-2 w-56 bg-slate-800 border border-slate-700 rounded-lg shadow-xl overflow-hidden"
                                    onMouseDown={(e) => e.preventDefault()}
                                >
                                    <div className="grid grid-cols-1 py-2">
                                        {genres.map((genre) => (
                                            <a
                                                key={genre}
                                                href="#"
                                                className="px-4 py-2 text-sm text-slate-300 hover:bg-primary/10 hover:text-primary transition-colors"
                                            >
                                                {genre}
                                            </a>
                                        ))}
                                    </div>
                                </div>
                            )}
                        </div>

                        <a className="hover:text-primary transition-colors" href="#">Bài viết</a>
                        <Link to="/about-us" className="hover:text-primary transition-colors">Về chúng tôi</Link>
                        <a className="hover:text-primary transition-colors" href="#">Đăng bài</a>
                    </div>

                    <div className="h-6 w-px bg-slate-700 hidden lg:block"></div>

                    <div className="flex items-center gap-3">
                        {isAuthenticated ? (
                            <>
                                {/* Coin Balance */}
                                <div className="hidden sm:flex items-center gap-1.5 px-3 py-1.5 bg-amber-950/40 border border-amber-700/50 rounded-full">
                                    <Coins className="w-4 h-4 text-amber-400" />
                                    <span className="text-sm font-bold text-amber-400">{userCoins.toLocaleString()}</span>
                                </div>

                                <button className="p-2 text-slate-300 hover:bg-slate-800 rounded-full transition-colors relative">
                                    <Bell className="w-5 h-5" />
                                    <span className="absolute top-2 right-2 size-2 bg-primary border-2 border-slate-900 rounded-full"></span>
                                </button>

                                <button className="hidden sm:flex items-center gap-2 px-4 py-2 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all">
                                    <Edit className="w-4 h-4" />
                                    Viết truyện
                                </button>

                                {/* User Avatar - click to Homepage; Chevron for user menu */}
                                <div className="relative flex items-center gap-0.5">
                                    <Link
                                        to="/home"
                                        className="block size-9 rounded-full overflow-hidden border-2 border-slate-700 hover:border-primary transition-colors shrink-0"
                                    >
                                        <img
                                            alt="User Avatar"
                                            className="w-full h-full object-cover"
                                            src={user?.avatar || 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=100&h=100&fit=crop'}
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
                                                    <p className="font-semibold text-white">{user?.name || 'Người dùng'}</p>
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
                        {/* Coin Balance Mobile */}
                        {isAuthenticated && (
                            <div className="flex items-center justify-between p-3 bg-amber-950/40 border border-amber-700/50 rounded-lg">
                                <div className="flex items-center gap-2">
                                    <Coins className="w-5 h-5 text-amber-400" />
                                    <span className="font-semibold text-white">Số dư xu</span>
                                </div>
                                <span className="text-lg font-bold text-amber-400">{userCoins.toLocaleString()}</span>
                            </div>
                        )}

                        <div className="relative mb-2">
                            <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none text-slate-400">
                                <Search className="w-5 h-5" />
                            </div>
                            <input
                                className="block w-full pl-10 pr-4 py-2 bg-slate-800 border border-slate-700 rounded-full text-sm outline-none text-white placeholder:text-slate-500"
                                placeholder="Tìm kiếm..."
                                type="text"
                            />
                        </div>

                        <div className="flex flex-col gap-3">
                            <Link to="/home" className="text-slate-300 hover:text-primary transition-colors font-semibold" onClick={() => setIsMenuOpen(false)}>Trang chủ</Link>
                            <details className="group">
                                <summary className="flex items-center justify-between text-slate-300 hover:text-primary transition-colors font-semibold cursor-pointer list-none">
                                    Thể loại
                                    <ChevronDown className="w-4 h-4 group-open:rotate-180 transition-transform" />
                                </summary>
                                <div className="mt-2 ml-4 flex flex-col gap-2">
                                    {genres.map((genre) => (
                                        <a
                                            key={genre}
                                            href="#"
                                            className="text-sm text-slate-400 hover:text-primary transition-colors"
                                        >
                                            {genre}
                                        </a>
                                    ))}
                                </div>
                            </details>

                            <a className="text-slate-300 hover:text-primary transition-colors font-semibold" href="#">Bài viết</a>
                            <Link to="/about-us" className="text-slate-300 hover:text-primary transition-colors font-semibold" onClick={() => setIsMenuOpen(false)}>Về chúng tôi</Link>
                            <a className="text-slate-300 hover:text-primary transition-colors font-semibold" href="#">Đăng bài</a>

                            {isAuthenticated ? (
                                <>
                                    <div className="border-t border-slate-700 my-2"></div>

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
    );
}