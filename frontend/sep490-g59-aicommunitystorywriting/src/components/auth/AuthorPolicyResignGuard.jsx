import { useCallback, useEffect, useRef } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { getMyAuthorPolicyStatus } from '../../api/policy/policyApi';
import { useAuth } from '../../contexts/AuthContext';

function isAuthorRole(user) {
    const role = (user?.role ?? user?.Role ?? '').toString().trim().toUpperCase();
    return role === 'AUTHOR';
}

function isExemptPath(pathname) {
    return (
        pathname.startsWith('/login') ||
        pathname.startsWith('/register') ||
        pathname.startsWith('/forgot-password') ||
        pathname.startsWith('/verify-otp') ||
        pathname.startsWith('/auth/google/callback') ||
        pathname.startsWith('/admin') ||
        pathname.startsWith('/staff')
    );
}

export function AuthorPolicyResignGuard() {
    const { user, loading, isAuthenticated } = useAuth();
    const location = useLocation();
    const navigate = useNavigate();
    const checkingRef = useRef(false);

    const checkAndRedirect = useCallback(async () => {
        if (checkingRef.current) return;
        if (loading || !isAuthenticated || !isAuthorRole(user)) return;

        checkingRef.current = true;
        try {
            const status = await getMyAuthorPolicyStatus('AUTHOR');
            const mustResignNow = Boolean(status?.policy?.requireResign) && !Boolean(status?.hasAccepted);
            if (!mustResignNow) return;

            const currentPath = `${location.pathname}${location.search}${location.hash}`;
            const isPolicyResignPage =
                location.pathname.startsWith('/policy') &&
                new URLSearchParams(location.search).get('from')?.toLowerCase() === 'resign';
            if (isPolicyResignPage) return;

            const target = `/policy?type=AUTHOR&from=resign&next=${encodeURIComponent(currentPath)}`;
            navigate(target, { replace: true });
        } catch {
            // Silently ignore to avoid breaking navigation on transient API failures.
        } finally {
            checkingRef.current = false;
        }
    }, [isAuthenticated, loading, location.hash, location.pathname, location.search, navigate, user]);

    useEffect(() => {
        if (isExemptPath(location.pathname)) return;
        void checkAndRedirect();
    }, [checkAndRedirect, location.pathname, location.search]);

    useEffect(() => {
        if (loading || !isAuthenticated || !isAuthorRole(user)) return undefined;
        const id = window.setInterval(() => {
            if (isExemptPath(window.location.pathname)) return;
            void checkAndRedirect();
        }, 15000);
        return () => window.clearInterval(id);
    }, [checkAndRedirect, isAuthenticated, loading, user]);

    return null;
}
