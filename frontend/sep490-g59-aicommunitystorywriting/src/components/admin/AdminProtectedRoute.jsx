import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';

/**
 * Bảo vệ route admin: chỉ cho phép user có role ADMIN.
 * Chưa đăng nhập -> redirect /login
 * Đã đăng nhập nhưng không phải ADMIN -> redirect /home
 */
export function AdminProtectedRoute({ children }) {
    const { user, loading, isAdmin } = useAuth();
    const location = useLocation();

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

    if (!isAdmin) {
        return <Navigate to="/home" replace />;
    }

    return children;
}
