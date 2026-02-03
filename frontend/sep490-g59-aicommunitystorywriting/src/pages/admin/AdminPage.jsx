import { useState } from 'react';
import { AdminLayout } from '../../components/admin/AdminLayout';
import { AdminDashboard } from '../../components/admin/AdminDashboard';
import { CategoryManagement } from './category/CategoryManagement';

export function AdminPage() {
    // eslint-disable-next-line no-unused-vars
    const [activePage, setActivePage] = useState('categories'); // Start with categories

    const renderPage = () => {
        switch (activePage) {
            case 'dashboard':
                return <AdminDashboard />;
            case 'categories':
                return <CategoryManagement />;
            case 'stories':
                return (
                    <div className="text-center py-12">
                        <h2 className="text-xl font-bold text-slate-900 dark:text-white mb-2">
                            Quản lý truyện
                        </h2>
                        <p className="text-slate-500 dark:text-slate-400">
                            Trang đang được phát triển...
                        </p>
                    </div>
                );
            case 'users':
                return (
                    <div className="text-center py-12">
                        <h2 className="text-xl font-bold text-slate-900 dark:text-white mb-2">
                            Quản lý người dùng
                        </h2>
                        <p className="text-slate-500 dark:text-slate-400">
                            Trang đang được phát triển...
                        </p>
                    </div>
                );
            case 'comments':
                return (
                    <div className="text-center py-12">
                        <h2 className="text-xl font-bold text-slate-900 dark:text-white mb-2">
                            Quản lý bình luận
                        </h2>
                        <p className="text-slate-500 dark:text-slate-400">
                            Trang đang được phát triển...
                        </p>
                    </div>
                );
            case 'settings':
                return (
                    <div className="text-center py-12">
                        <h2 className="text-xl font-bold text-slate-900 dark:text-white mb-2">
                            Cài đặt
                        </h2>
                        <p className="text-slate-500 dark:text-slate-400">
                            Trang đang được phát triển...
                        </p>
                    </div>
                );
            default:
                return <CategoryManagement />;
        }
    };

    return (
        <AdminLayout activePage={activePage}>
            {renderPage()}
        </AdminLayout>
    );
}
