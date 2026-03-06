import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';

/**
 * Bảo vệ route admin: cho phép user có role ADMIN hoặc MODERATOR.
 * Chưa đăng nhập -> redirect /login
 * Đã đăng nhập nhưng không phải ADMIN/MODERATOR -> redirect /home
 */
export function AdminProtectedRoute({ children }) {
    const { user, loading, isAdmin, role } = useAuth();
    const location = useLocation();
    const roleUpper = (role ?? '').toString().toUpperCase();
    const canAccessAdmin = isAdmin || roleUpper === 'MODERATOR';

    if (loading) {
        return (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '60vh' }}>
                <span>Đang tải...</span>
            </div>
        );
    }

    if (!user) {
        return <Navigate to="/login" state={{ from: location }} replace />;
    }

    if (!canAccessAdmin) {
        return <Navigate to="/home" replace />;
    }

    return children;
}
