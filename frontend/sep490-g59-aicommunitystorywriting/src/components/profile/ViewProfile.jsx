import { useAuth } from '../../contexts/AuthContext';
import { User, Mail, Phone, CreditCard, Calendar, CheckCircle } from 'lucide-react';

export default function ViewProfile() {
    const { user } = useAuth();

    const profileData = user?.profile || {
        displayName: user?.name || 'Người dùng',
        email: user?.email || '',
        phone: '',
        idNumber: '',
        joinDate: '',
        isVerified: false,
        tags: ['Thành viên'],
        bio: '',
        stats: {
            storiesWritten: 0,
            totalReads: 0,
            currentCoins: user?.coins || 0,
            likes: 0,
        },
    };

    return (
        <div>
            {/* Main Content Card */}
            <div className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-8 border border-slate-200 dark:border-slate-700">
                {/* Personal Information Section */}
                <div className="mb-8">
                    <h3 className="text-xl font-bold text-slate-900 dark:text-white mb-6">
                        Thông tin cá nhân
                    </h3>

                    {/* Mini Profile Summary */}
                    <div className="flex items-start gap-4 mb-8 p-4 bg-slate-50 dark:bg-slate-700/50 rounded-lg">
                        <div className="size-12 bg-primary rounded-full flex items-center justify-center text-white text-lg font-bold">
                            {profileData.displayName.charAt(0).toUpperCase()}
                        </div>
                        <div className="flex-1">
                            <h4 className="font-bold text-slate-900 dark:text-white mb-1">
                                {profileData.displayName}
                            </h4>
                            <p className="text-sm text-slate-600 dark:text-slate-400 mb-2">
                                {profileData.tags.join(' | ')}
                            </p>
                            {profileData.isVerified && (
                                <div className="flex items-center gap-2">
                                    <CheckCircle className="w-4 h-4 text-green-500" />
                                    <span className="text-sm font-semibold text-green-600 dark:text-green-400">
                                        Tài khoản đã xác thực
                                    </span>
                                </div>
                            )}
                        </div>
                    </div>

                    {/* Personal Fields Grid */}
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-6 mb-8">
                        <div>
                            <label className="block text-xs font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider mb-2">
                                TÊN HIỂN THỊ
                            </label>
                            <div className="relative">
                                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                    <User className="w-5 h-5 text-slate-400" />
                                </div>
                                <div className="pl-10 pr-4 py-3 bg-slate-50 dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-900 dark:text-white">
                                    {profileData.displayName}
                                </div>
                            </div>
                        </div>

                        <div>
                            <label className="block text-xs font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider mb-2">
                                EMAIL
                            </label>
                            <div className="relative">
                                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                    <Mail className="w-5 h-5 text-slate-400" />
                                </div>
                                <div className="pl-10 pr-4 py-3 bg-slate-50 dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-900 dark:text-white">
                                    {profileData.email}
                                </div>
                            </div>
                        </div>

                        <div>
                            <label className="block text-xs font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider mb-2">
                                SỐ ĐIỆN THOẠI
                            </label>
                            <div className="relative">
                                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                    <Phone className="w-5 h-5 text-slate-400" />
                                </div>
                                <div className="pl-10 pr-4 py-3 bg-slate-50 dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-900 dark:text-white">
                                    {profileData.phone || ''}
                                </div>
                            </div>
                        </div>

                        <div>
                            <label className="block text-xs font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider mb-2">
                                SỐ CCCD/CMND
                            </label>
                            <div className="relative">
                                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                    <CreditCard className="w-5 h-5 text-slate-400" />
                                </div>
                                <div className="pl-10 pr-4 py-3 bg-slate-50 dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-900 dark:text-white">
                                    {profileData.idNumber || ''}
                                </div>
                            </div>
                        </div>

                        <div>
                            <label className="block text-xs font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider mb-2">
                                NGÀY THAM GIA
                            </label>
                            <div className="relative">
                                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                    <Calendar className="w-5 h-5 text-slate-400" />
                                </div>
                                <div className="pl-10 pr-4 py-3 bg-slate-50 dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-900 dark:text-white">
                                    {profileData.joinDate || ''}
                                </div>
                            </div>
                        </div>

                        <div>
                            <label className="block text-xs font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider mb-2">
                                TRẠNG THÁI TÀI KHOẢN
                            </label>
                            <div className="relative">
                                <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                                    <CheckCircle className="w-5 h-5 text-green-500" />
                                </div>
                                <div className="pl-10 pr-4 py-3 bg-slate-50 dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-900 dark:text-white">
                                    {profileData.isVerified ? 'Đã xác thực' : 'Chưa xác thực'}
                                </div>
                            </div>
                        </div>
                    </div>

                    {/* About Me Section */}
                    <div>
                        <label className="block text-xs font-bold text-slate-500 dark:text-slate-400 uppercase tracking-wider mb-2">
                            GIỚI THIỆU BẢN THÂN
                        </label>
                        <div className="p-4 bg-slate-50 dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-700 dark:text-slate-300 min-h-[100px]">
                            {profileData.bio || 'Chưa có giới thiệu'}
                        </div>
                    </div>
                </div>

                {/* Activity Statistics */}
                <div className="border-t border-slate-200 dark:border-slate-700 pt-8">
                    <h3 className="text-xl font-bold text-slate-900 dark:text-white mb-6">
                        Thống kê hoạt động
                    </h3>
                    <div className="grid grid-cols-2 md:grid-cols-4 gap-6">
                        <div className="text-center">
                            <div className="text-4xl font-bold text-blue-600 dark:text-blue-400 mb-2">
                                {profileData.stats?.storiesWritten ?? 0}
                            </div>
                            <div className="text-sm text-slate-600 dark:text-slate-400">
                                Truyện đã viết
                            </div>
                        </div>
                        <div className="text-center">
                            <div className="text-4xl font-bold text-blue-600 dark:text-blue-400 mb-2">
                                {profileData.stats?.totalReads ?? 0}
                            </div>
                            <div className="text-sm text-slate-600 dark:text-slate-400">
                                Lượt đọc
                            </div>
                        </div>
                        <div className="text-center">
                            <div className="text-4xl font-bold text-amber-600 dark:text-amber-400 mb-2">
                                {(profileData.stats?.currentCoins ?? 0).toLocaleString()}
                            </div>
                            <div className="text-sm text-slate-600 dark:text-slate-400">
                                Coins hiện có
                            </div>
                        </div>
                        <div className="text-center">
                            <div className="text-4xl font-bold text-red-600 dark:text-red-400 mb-2">
                                {profileData.stats?.likes ?? 0}
                            </div>
                            <div className="text-sm text-slate-600 dark:text-slate-400">
                                Yêu thích
                            </div>
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
