import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';

/** Các role được dùng trang quản trị và đăng nhập tại /admin/login. Actor: ADMIN, MODERATOR, COMPLIANCE, USER, AUTHOR */
const ADMIN_PANEL_ROLES = new Set(['ADMIN', 'MODERATOR', 'COMPLIANCE']);
export function AdminProtectedRoute({ children }) {
    const { user, loading, isAdmin, role } = useAuth();
    const location = useLocation();
    const roleUpper = (role ?? '').toString().toUpperCase();
    const canAccessAdmin = isAdmin || ADMIN_PANEL_ROLES.has(roleUpper);

    if (loading) {
        return (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '60vh' }}>
                <span>Đang tải...</span>
            </div>
        );
    }

    if (!user) {
        return <Navigate to="/admin/login" state={{ from: location }} replace />;
    }

    if (!canAccessAdmin) {
        return <Navigate to="/home" replace />;
    }

    return children;
}
