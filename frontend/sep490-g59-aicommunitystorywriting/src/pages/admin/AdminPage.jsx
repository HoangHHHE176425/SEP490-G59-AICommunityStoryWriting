import { useState } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { AdminLayout } from '../../components/admin/AdminLayout';
import { AdminDashboard } from '../../components/admin/AdminDashboard';
import { CategoryManagement } from './category/CategoryManagement';
import { PublicationManagement } from './publication/PublicationManagement';
import { UserManagement } from './user/UserManagement';
import { PolicyManagement } from './policy/PolicyManagement';

export function AdminPage() {
    const { role } = useAuth();
    const roleUpper = (role ?? '').toString().toUpperCase();
    const isModeratorOnly = roleUpper === 'MODERATOR';
    const [activePage, setActivePage] = useState(isModeratorOnly ? 'publication' : 'categories');

    const renderPage = () => {
        switch (activePage) {
            case 'dashboard':
                return <AdminDashboard />;
            case 'categories':
                return <CategoryManagement />;
            case 'publication':
                return <PublicationManagement />;
            case 'stories':
                return (
                    <div className="text-center py-12">
                        <h2 className="text-xl font-bold text-slate-900 mb-2">
                            Quản lý truyện
                        </h2>
                        <p className="text-slate-500">
                            Trang đang được phát triển...
                        </p>
                    </div>
                );
            case 'users':
                return <UserManagement />;
            case 'policies':
                return <PolicyManagement />;
            case 'comments':
                return (
                    <div className="text-center py-12">
                        <h2 className="text-xl font-bold text-slate-900 mb-2">
                            Quản lý bình luận
                        </h2>
                        <p className="text-slate-500">
                            Trang đang được phát triển...
                        </p>
                    </div>
                );
            case 'settings':
                return (
                    <div className="text-center py-12">
                        <h2 className="text-xl font-bold text-slate-900 mb-2">
                            Cài đặt
                        </h2>
                        <p className="text-slate-500">
                            Trang đang được phát triển...
                        </p>
                    </div>
                );
            default:
                return <CategoryManagement />;
        }
    };

    return (
        <AdminLayout activePage={activePage} onNavigate={setActivePage}>
            {renderPage()}
        </AdminLayout>
    );
}
