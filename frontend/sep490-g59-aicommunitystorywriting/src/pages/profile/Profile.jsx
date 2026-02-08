import { useState } from 'react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { User, Edit, Coins, History, Ticket, Trash2, BookOpen } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import ViewProfile from '../../components/profile/ViewProfile';
import EditProfile from '../../components/profile/EditProfile';
import RechargeCoin from '../../components/profile/RechargeCoin';
import ActivityHistory from '../../components/profile/ActivityHistory';
import DeleteAccount from '../../components/profile/DeleteAccount';

export default function Profile() {
    const { user, isAuthenticated, loading } = useAuth();
    const [activeTab, setActiveTab] = useState('info');

    const profile = user?.profile || {};
    const profileData = {
        displayName: profile.displayName || user?.name || 'User',
        email: profile.email || user?.email || '',
        tags: Array.isArray(profile.tags) ? profile.tags : [],
        stats: {
            currentCoins: profile?.stats?.currentCoins ?? (user?.coins ?? 0),
            storiesWritten: profile?.stats?.storiesWritten ?? 0,
        },
    };

    const tabs = [
        { id: 'info', label: 'Thông tin', icon: User },
        { id: 'edit', label: 'Chỉnh sửa', icon: Edit },
        { id: 'recharge', label: 'Nạp Coin', icon: Coins },
        { id: 'history', label: 'Lịch sử', icon: History },
        { id: 'voucher', label: 'Voucher', icon: Ticket },
        { id: 'delete', label: 'Xóa tài khoản', icon: Trash2 },
    ];

    const renderContent = () => {
        switch (activeTab) {
            case 'info':
                return <ViewProfile />;
            case 'edit':
                return <EditProfile />;
            case 'recharge':
                return <RechargeCoin />;
            case 'history':
                return <ActivityHistory />;
            case 'voucher':
                return <div className="p-8 text-center text-slate-500">Tính năng Voucher đang được phát triển...</div>;
            case 'delete':
                return <DeleteAccount />;
            default:
                return <ViewProfile />;
        }
    };

    return (
        <div className="min-h-screen bg-background-light dark:bg-background-dark flex flex-col">
            <Header />
            <div className="flex-1">
                <div className="max-w-[1280px] mx-auto px-4 py-8">
                    {!loading && !isAuthenticated && (
                        <div className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-8 border border-slate-200 dark:border-slate-700 text-center text-slate-600 dark:text-slate-300">
                            Bạn cần đăng nhập để xem trang cá nhân.
                        </div>
                    )}

                    {isAuthenticated && (
                        <>
                            {/* Profile Summary Section */}
                            <div className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-6 mb-6 border border-slate-200 dark:border-slate-700">
                                <div className="flex items-start gap-6">
                                    <div className="size-20 bg-primary rounded-full flex items-center justify-center text-white text-3xl font-bold shadow-lg">
                                        {profileData.displayName.charAt(0).toUpperCase()}
                                    </div>
                                    <div className="flex-1">
                                        <h2 className="text-2xl font-bold text-slate-900 dark:text-white mb-1">
                                            {profileData.displayName}
                                        </h2>
                                        <p className="text-slate-600 dark:text-slate-400 mb-2">{profileData.email}</p>
                                        <p className="text-sm text-slate-500 dark:text-slate-500 mb-4">
                                            {profileData.tags.length > 0 ? profileData.tags.join(' | ') : 'Chưa có tag'}
                                        </p>
                                        <div className="flex gap-3">
                                            <div className="flex items-center gap-2 px-3 py-1.5 bg-amber-50 dark:bg-amber-950/30 border border-amber-200 dark:border-amber-800 rounded-full">
                                                <Coins className="w-4 h-4 text-amber-500" />
                                                <span className="text-sm font-bold text-amber-700 dark:text-amber-400">
                                                    {profileData.stats.currentCoins.toLocaleString()} Coins
                                                </span>
                                            </div>
                                            <div className="flex items-center gap-2 px-3 py-1.5 bg-green-50 dark:bg-green-950/30 border border-green-200 dark:border-green-800 rounded-full">
                                                <BookOpen className="w-4 h-4 text-green-500" />
                                                <span className="text-sm font-bold text-green-700 dark:text-green-400">
                                                    {profileData.stats.storiesWritten} Truyện
                                                </span>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            {/* Navigation Tabs */}
                            <div className="mb-8 border-b border-slate-200 dark:border-slate-700">
                                <div className="flex gap-8 overflow-x-auto">
                                    {tabs.map((tab) => {
                                        const Icon = tab.icon;
                                        const isActive = activeTab === tab.id;
                                        return (
                                            <button
                                                key={tab.id}
                                                onClick={() => setActiveTab(tab.id)}
                                                className={`flex items-center gap-2 px-4 py-4 font-semibold text-sm transition-colors border-b-2 ${
                                                    isActive
                                                        ? 'text-primary border-primary'
                                                        : 'text-slate-500 dark:text-slate-400 border-transparent hover:text-primary'
                                                }`}
                                            >
                                                <Icon className="w-5 h-5" />
                                                {tab.label}
                                            </button>
                                        );
                                    })}
                                </div>
                            </div>

                            {/* Content */}
                            {renderContent()}
                        </>
                    )}
                </div>
            </div>
            <Footer />
        </div>
    );
}
