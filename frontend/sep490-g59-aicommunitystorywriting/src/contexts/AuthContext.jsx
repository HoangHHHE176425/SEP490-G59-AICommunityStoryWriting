import { createContext, useContext, useState, useEffect, useRef } from 'react';
import * as authApi from '../api/auth/authApi';
import * as accountApi from '../api/account/accountApi';
import * as policyApi from '../api/policy/policyApi';
import { createNotificationHubConnection } from '../api/notification/notificationHub';

// Giữ nguyên identity của context trong Vite HMR để tránh lỗi
// "useAuth phải được sử dụng bên trong AuthProvider" khi module reload và tạo lại Context mới.
// eslint-disable-next-line no-undef
const AuthContext =
    import.meta?.hot?.data?.AuthContext ?? createContext(null);
// eslint-disable-next-line no-undef
if (import.meta?.hot) {
    // eslint-disable-next-line no-undef
    import.meta.hot.data.AuthContext = AuthContext;
}

export function AuthProvider({ children }) {
    const [user, setUser] = useState(null);
    const [loading, setLoading] = useState(true);

    const saveUser = (u) => {
        setUser(u);
        if (u) localStorage.setItem('user', JSON.stringify(u));
        else localStorage.removeItem('user');
    };

    const fetchProfile = async () => {
        const profile = await accountApi.getMyProfile();
        saveUser(profile);
        return profile;
    };
    const fetchProfileRef = useRef(fetchProfile);
    fetchProfileRef.current = fetchProfile;

    // Load cached user + try restore session (refresh cookie -> access token -> profile)
    useEffect(() => {
        const savedUser = localStorage.getItem('user');
        if (savedUser) {
            try {
                setUser(JSON.parse(savedUser));
            } catch (error) {
                console.error('Error parsing saved user:', error);
                localStorage.removeItem('user');
            }
        }

        const bootstrap = async () => {
            try {
                const token = localStorage.getItem('accessToken');
                // Only try refresh if we have evidence of a previous session (cached user).
                // This avoids noisy 401s on first-time visits (especially with StrictMode double-invoking effects).
                if (!token && savedUser) {
                    await authApi.refresh();
                }
                const tokenNow = localStorage.getItem('accessToken');
                if (tokenNow) {
                    await fetchProfile();
                }
            } catch {
                localStorage.removeItem('accessToken');
                localStorage.removeItem('user');
                setUser(null);
            } finally {
                setLoading(false);
            }
        };

        bootstrap();
    }, []);

    /** Real-time notification: khi moderator duyệt/từ chối, backend push NewNotification tới author. Dispatch event để component có thể hiển thị toast hoặc refresh danh sách. */
    const notificationHubStopRef = useRef(null);
    const userId = user?.id ?? null;
    useEffect(() => {
        const token = localStorage.getItem('accessToken');
        if (!token || !userId) {
            if (notificationHubStopRef.current) {
                notificationHubStopRef.current();
                notificationHubStopRef.current = null;
            }
            return;
        }
        const { stop, startPromise } = createNotificationHubConnection(
            (notification) => {
                window.dispatchEvent(new CustomEvent('app:notification', { detail: notification }));
                const t = String(notification?.type ?? '').trim().toUpperCase();
                if (t === 'COMPLIANCE_STORY_MODERATION_ACTION') {
                    void fetchProfileRef.current?.();
                }
            },
            (payload) => {
                window.dispatchEvent(new CustomEvent('app:auth:session-ended', {
                    detail: {
                        message: payload?.message || 'Tài khoản của bạn đã bị khóa. Vui lòng đăng nhập lại.'
                    }
                }));
            }
        );
        notificationHubStopRef.current = stop;
        startPromise?.catch(() => { });
        return () => {
            if (notificationHubStopRef.current) {
                notificationHubStopRef.current();
                notificationHubStopRef.current = null;
            }
        };
    }, [userId]);

    const login = async (email, password) => {
        const result = await authApi.login({ email, password });
        if (!result.success) return result;

        try {
            const profile = await fetchProfile();
            return { success: true, user: profile };
        } catch (err) {
            // Token exists but cannot fetch profile
            return { success: false, message: err?.message || 'Đăng nhập thất bại' };
        }
    };

    const register = async (email, password, confirmPassword, name) => {
        return await authApi.register({ email, password, confirmPassword, fullName: name });
    };

    const verifyOtp = async (email, otpCode) => {
        return await authApi.verifyOtp({ email, otpCode });
    };

    const resendOtp = async (email) => {
        return await authApi.resendOtp({ email });
    };

    const loginWithGoogle = async (returnUrl = '/home') => {
        const apiBase = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';
        const ru = typeof returnUrl === 'string' && returnUrl.startsWith('/') ? returnUrl : '/home';
        // Redirect code flow: browser will go to backend -> Google -> backend callback -> frontend callback.
        window.location.href = `${apiBase}/Auth/google/login?returnUrl=${encodeURIComponent(ru)}`;
        // If redirect is blocked, return success so UI won't show an error.
        return { success: true };
    };

    const forgotPassword = async (email) => {
        return await authApi.forgotPassword({ email });
    };

    const resetPassword = async (email, otpCode, newPassword, confirmPassword) => {
        return await authApi.resetPassword({ email, otpCode, newPassword, confirmPassword });
    };

    const logout = async () => {
        await authApi.logout();
        setUser(null);
    };

    const updateMyProfile = async (payload) => {
        const res = await accountApi.updateProfile(payload);
        if (!res.success) return res;
        await fetchProfile();
        return { success: true };
    };

    const changeMyPassword = async (payload) => {
        return await accountApi.changePassword(payload);
    };

    const deleteMyAccount = async () => {
        const res = await accountApi.deleteAccount();
        if (!res.success) return res;
        await logout();
        return { success: true };
    };

    const uploadMyAvatar = async (file) => {
        const res = await accountApi.uploadAvatar(file);
        if (!res.success) return res;
        await fetchProfile();
        return res;
    };

    const becomeAuthor = async (policyId) => {
        if (policyId) {
            const acceptRes = await policyApi.acceptAuthorPolicy(policyId);
            if (!acceptRes?.success) return acceptRes;
        }

        const res = await accountApi.becomeAuthor();
        if (!res.success) return res;

        const accessToken = res?.data?.accessToken;
        if (accessToken) {
            localStorage.setItem('accessToken', accessToken);
        }

        const profile = await fetchProfile();
        return { success: true, user: profile, data: res.data };
    };

    const role = (user?.role ?? user?.Role ?? '').toString().trim().toUpperCase();
    const hasAdminTag = Array.isArray(user?.tags) && user.tags.includes('Quản trị viên');
    const isAdmin = role === 'ADMIN' || hasAdminTag;

    const value = {
        user,
        loading,
        login,
        register,
        verifyOtp,
        resendOtp,
        loginWithGoogle,
        forgotPassword,
        resetPassword,
        logout,
        fetchProfile,
        updateMyProfile,
        changeMyPassword,
        deleteMyAccount,
        uploadMyAvatar,
        becomeAuthor,
        isAuthenticated: !!user,
        isAdmin,
        role,
    };

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}
// eslint-disable-next-line react-refresh/only-export-components
export function useAuth() {
    const context = useContext(AuthContext);
    if (!context) {
        throw new Error('useAuth phải được sử dụng bên trong AuthProvider.');
    }
    return context;
}

