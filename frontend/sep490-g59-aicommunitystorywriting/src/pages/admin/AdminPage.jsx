import { useEffect, useMemo, useState } from 'react';
import { useAuth } from '../../contexts/AuthContext';
import { AdminLayout } from '../../components/admin/AdminLayout';
import { AdminDashboard } from '../../components/admin/AdminDashboard';
import { CategoryManagement } from './category/CategoryManagement';
import { PublicationManagement } from './publication/PublicationManagement';
import { UserManagement } from './user/UserManagement';
import { PolicyManagement } from './policy/PolicyManagement';
import { AiConfig } from './ai/AiConfig';
import ViolationManagement from './violation/ViolationManagement';
import { AdminTransactions } from './transactions/AdminTransactions';
import { AdminWalletDashboard } from './wallet/AdminWalletDashboard';
import { ReviewEscalationsManagement } from './moderation/ReviewEscalationsManagement';

export function AdminPage() {
    const { role } = useAuth();
    const roleUpper = (role ?? '').toString().toUpperCase();
    const hidePagesForAdmin = useMemo(() => new Set(['publication', 'stories', 'comments']), []);
    const allowedPages = useMemo(() => {
        if (roleUpper === 'MODERATOR') return new Set(['dashboard', 'publication']);
        if (roleUpper === 'COMPLIANCE') return new Set(['violations']);
        return null; // ADMIN: full
    }, [roleUpper]);

    const getDefaultPageByRole = () => {
        if (roleUpper === 'MODERATOR') return 'dashboard';
        if (roleUpper === 'COMPLIANCE') return 'violations';
        return 'dashboard';
    };

    const [activePage, setActivePage] = useState(getDefaultPageByRole());
    const [publicationInitialStatus, setPublicationInitialStatus] = useState('pending'); // pending | approved | rejected

    // Khi role thay đổi hoặc reload, luôn kéo về màn mặc định của role đó.
    useEffect(() => {
        setActivePage((prev) => {
            const nextDefault = getDefaultPageByRole();
            if (!allowedPages) return prev || nextDefault;
            return allowedPages.has(prev) ? prev : nextDefault;
        });
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [roleUpper]);

    // ADMIN: ẩn tab => chặn truy cập nội bộ nếu state còn giữ.
    useEffect(() => {
        if (roleUpper !== 'ADMIN') return;
        if (hidePagesForAdmin.has(activePage)) {
            setActivePage('dashboard');
        }
    }, [roleUpper, activePage, hidePagesForAdmin]);

    const renderPage = () => {
        switch (activePage) {
            case 'dashboard':
                return (
                    <AdminDashboard
                        onNavigatePublicationStatus={(status) => {
                            setPublicationInitialStatus(status);
                            setActivePage('publication');
                        }}
                    />
                );
            case 'categories':
                return <CategoryManagement />;
            case 'publication':
                return <PublicationManagement initialFilterStatus={publicationInitialStatus} />;
            case 'review-escalations':
                return <ReviewEscalationsManagement />;
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
            case 'violations':
                return <ViolationManagement />;
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
            case 'wallet-dashboard':
                return <AdminWalletDashboard />;
            case 'ai-config':
                return <AiConfig />;
            case 'transactions':
                return <AdminTransactions />;
            default:
                return <CategoryManagement />;
        }
    };

    const handleNavigate = (pageId) => {
        if (roleUpper === 'ADMIN' && hidePagesForAdmin.has(pageId)) {
            setActivePage('dashboard');
            return;
        }
        if (allowedPages && !allowedPages.has(pageId)) {
            setActivePage(getDefaultPageByRole());
            return;
        }
        setActivePage(pageId);
        if (pageId === 'publication') setPublicationInitialStatus('pending');
    };

    return (
        <AdminLayout activePage={activePage} onNavigate={handleNavigate}>
            {renderPage()}
        </AdminLayout>
    );
}
