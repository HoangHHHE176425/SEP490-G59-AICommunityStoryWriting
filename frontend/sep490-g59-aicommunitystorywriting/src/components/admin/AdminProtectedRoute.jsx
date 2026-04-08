import { Navigate, useLocation } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';

const ADMIN_ONLY_ROLES = new Set(['ADMIN']);
const STAFF_ONLY_ROLES = new Set(['MODERATOR', 'COMPLIANCE']);

/**
 * Khu vực quản trị tách riêng:
 * - admin: chỉ ADMIN, login /admin/login
 * - staff: MODERATOR/COMPLIANCE, login /staff/login
 */
export function AdminProtectedRoute({ children, area = 'admin' }) {
    const { user, loading, role } = useAuth();
    const location = useLocation();
    const roleUpper = (role ?? '').toString().toUpperCase();
    const isStaffArea = area === 'staff';
    const loginPath = isStaffArea ? '/staff/login' : '/admin/login';
    const oppositeHomePath = isStaffArea ? '/admin' : '/staff';
    const canAccessOwnArea = isStaffArea
        ? STAFF_ONLY_ROLES.has(roleUpper)
        : ADMIN_ONLY_ROLES.has(roleUpper);
    const canAccessOppositeArea = isStaffArea
        ? ADMIN_ONLY_ROLES.has(roleUpper)
        : STAFF_ONLY_ROLES.has(roleUpper);

    if (loading) {
        return (
            <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'center', minHeight: '60vh' }}>
                <span>Đang tải...</span>
            </div>
        );
    }

    if (!user) {
        return <Navigate to={loginPath} state={{ from: location }} replace />;
    }

    if (canAccessOppositeArea) {
        return <Navigate to={oppositeHomePath} replace />;
    }

    if (!canAccessOwnArea) {
        return <Navigate to="/home" replace />;
    }

    return children;
}
