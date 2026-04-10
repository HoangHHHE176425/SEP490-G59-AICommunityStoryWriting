import { useEffect } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';

function buildLoginPath(pathname) {
    if (pathname.startsWith('/admin')) return '/admin/login';
    if (pathname.startsWith('/staff')) return '/staff/login';
    return '/login';
}

export function AuthSessionHandler() {
    const location = useLocation();
    const navigate = useNavigate();
    const { logout } = useAuth();

    useEffect(() => {
        let handling = false;

        const onSessionEnded = async (event) => {
            if (handling) return;
            handling = true;

            const message =
                event?.detail?.message ||
                'Phiên đăng nhập của bạn đã hết hạn. Vui lòng đăng nhập lại.';

            const loginPath = buildLoginPath(location.pathname);
            const currentPath = `${location.pathname}${location.search}${location.hash}`;
            const shouldKeepRedirect =
                currentPath &&
                currentPath !== loginPath &&
                !location.pathname.startsWith('/auth/google/callback');

            try {
                await logout();
            } catch {
                // logout already clears local storage in the auth API layer.
            }

            const target = shouldKeepRedirect
                ? `${loginPath}?redirect=${encodeURIComponent(currentPath)}`
                : loginPath;

            navigate(target, {
                replace: true,
                state: { forcedLogoutMessage: message }
            });
        };

        window.addEventListener('app:auth:session-ended', onSessionEnded);
        return () => window.removeEventListener('app:auth:session-ended', onSessionEnded);
    }, [location.hash, location.pathname, location.search, logout, navigate]);

    return null;
}
