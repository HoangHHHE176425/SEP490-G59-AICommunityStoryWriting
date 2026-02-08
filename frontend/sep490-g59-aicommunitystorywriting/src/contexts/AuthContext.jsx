import { createContext, useContext, useState, useEffect } from 'react';
import * as authApi from '../api/auth/authApi';
import * as accountApi from '../api/account/accountApi';

const AuthContext = createContext(null);

const ACCESS_TOKEN_KEY = 'accessToken';
const USER_KEY = 'user';

const API_BASE = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';
const BACKEND_BASE = API_BASE.replace(/\/api\/?$/, '');

function toAbsoluteAssetUrl(url) {
    if (!url) return url;
    if (/^https?:\/\//i.test(url)) return url;
    if (url.startsWith('/')) return `${BACKEND_BASE}${url}`;
    return url;
}

function parseJwt(token) {
    try {
        const base64Url = token.split('.')[1];
        if (!base64Url) return null;
        const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
        const jsonPayload = decodeURIComponent(
            atob(base64)
                .split('')
                .map((c) => `%${`00${c.charCodeAt(0).toString(16)}`.slice(-2)}`)
                .join('')
        );
        return JSON.parse(jsonPayload);
    } catch {
        return null;
    }
}

function buildUserFromAccessToken(accessToken) {
    const payload = parseJwt(accessToken);
    if (!payload) return null;

    const id = payload.sub || payload.nameid || payload.userId;
    const email = payload.email || payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'];
    const role = payload.role;

    return {
        id: id || null,
        email: email || null,
        role: role || null,
        // UI expects these fields; backend profile can replace later
        name: email || 'Người dùng',
        avatar: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=100&h=100&fit=crop',
        coins: 0,
    };
}

function normalizeUserFromProfile(profile, fallbackUser) {
    if (!profile) return fallbackUser || null;
    return {
        id: profile.id ?? fallbackUser?.id ?? null,
        email: profile.email ?? fallbackUser?.email ?? null,
        role: fallbackUser?.role ?? null,
        name: profile.displayName ?? fallbackUser?.name ?? 'Người dùng',
        avatar:
            toAbsoluteAssetUrl(profile.avatarUrl) ||
            fallbackUser?.avatar ||
            'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=100&h=100&fit=crop',
        coins: profile?.stats?.currentCoins ?? fallbackUser?.coins ?? 0,
        // Keep full profile for profile screens
        profile,
    };
}

export function AuthProvider({ children }) {
    const [user, setUser] = useState(null);
    const [loading, setLoading] = useState(true);

    // Load user from localStorage on mount
    useEffect(() => {
        const savedUser = localStorage.getItem(USER_KEY);
        const accessToken = localStorage.getItem(ACCESS_TOKEN_KEY);

        if (savedUser) {
            try {
                setUser(JSON.parse(savedUser));
            } catch (error) {
                console.error('Error parsing saved user:', error);
                localStorage.removeItem(USER_KEY);
            }
        } else if (accessToken) {
            const tokenUser = buildUserFromAccessToken(accessToken);
            if (tokenUser) {
                setUser(tokenUser);
                localStorage.setItem(USER_KEY, JSON.stringify(tokenUser));
            }
        }

        setLoading(false);
    }, []);

    // Load profile from API once we have token
    useEffect(() => {
        const accessToken = localStorage.getItem(ACCESS_TOKEN_KEY);
        if (!accessToken) return;

        let cancelled = false;
        (async () => {
            try {
                const profile = await accountApi.getMyProfile();
                if (cancelled) return;
                const updated = normalizeUserFromProfile(profile, user);
                setUser(updated);
                localStorage.setItem(USER_KEY, JSON.stringify(updated));
            } catch {
                // ignore: token might be invalid/expired; pages can still work with decoded token
            }
        })();

        return () => {
            cancelled = true;
        };
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const login = async (email, password) => {
        try {
            const data = await authApi.login({ email, password });
            const accessToken = data?.accessToken || data?.AccessToken;

            if (!accessToken) {
                return { success: false, message: 'Dữ liệu đăng nhập không hợp lệ' };
            }

            localStorage.setItem(ACCESS_TOKEN_KEY, accessToken);

            const tokenUser = buildUserFromAccessToken(accessToken) || { email };

            // Try load full profile
            let finalUser = tokenUser;
            try {
                const profile = await accountApi.getMyProfile();
                finalUser = normalizeUserFromProfile(profile, tokenUser);
            } catch {
                // ignore, keep token user
            }

            setUser(finalUser);
            localStorage.setItem(USER_KEY, JSON.stringify(finalUser));

            return { success: true, user: finalUser };
        } catch (e) {
            return { success: false, message: e?.message || 'Đăng nhập thất bại' };
        }
    };

    const register = async (email, password, name) => {
        try {
            const data = await authApi.register({ email, password, fullName: name });
            return { success: true, message: data?.message };
        } catch (e) {
            return { success: false, message: e?.message || 'Đăng ký thất bại' };
        }
    };

    const loginWithGoogle = async () => {
        // Mock Google login - replace with actual OAuth implementation
        const mockUser = {
            id: '1',
            email: 'user@gmail.com',
            name: 'Google User',
            avatar: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=100&h=100&fit=crop',
            coins: 1250,
            provider: 'google',
        };
        setUser(mockUser);
        localStorage.setItem('user', JSON.stringify(mockUser));
        return { success: true, user: mockUser };
    };

    const loginWithFacebook = async () => {
        // Mock Facebook login - replace with actual OAuth implementation
        const mockUser = {
            id: '1',
            email: 'user@facebook.com',
            name: 'Facebook User',
            avatar: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=100&h=100&fit=crop',
            coins: 1250,
            provider: 'facebook',
        };
        setUser(mockUser);
        localStorage.setItem('user', JSON.stringify(mockUser));
        return { success: true, user: mockUser };
    };

    const forgotPassword = async (email) => {
        try {
            const data = await authApi.forgotPassword({ email });
            return { success: true, message: data?.message };
        } catch (e) {
            return { success: false, message: e?.message || 'Gửi yêu cầu thất bại' };
        }
    };

    const verifyOtp = async (email, otpCode) => {
        try {
            const data = await authApi.verifyOtp({ email, otpCode });
            return { success: true, message: data?.message };
        } catch (e) {
            return { success: false, message: e?.message || 'Xác thực OTP thất bại' };
        }
    };

    const resetPassword = async (email, otpCode, newPassword, confirmPassword) => {
        try {
            const data = await authApi.resetPassword({ email, otpCode, newPassword, confirmPassword });
            return { success: true, message: data?.message };
        } catch (e) {
            return { success: false, message: e?.message || 'Đặt lại mật khẩu thất bại' };
        }
    };

    const logout = () => {
        setUser(null);
        localStorage.removeItem(USER_KEY);
        localStorage.removeItem(ACCESS_TOKEN_KEY);
    };

    const logoutServer = async () => {
        try {
            await authApi.logout();
        } catch {
            // ignore
        } finally {
            logout();
        }
    };

    const refreshProfile = async () => {
        try {
            const profile = await accountApi.getMyProfile();
            const updated = normalizeUserFromProfile(profile, user);
            setUser(updated);
            localStorage.setItem(USER_KEY, JSON.stringify(updated));
            return { success: true, profile };
        } catch (e) {
            return { success: false, message: e?.message || 'Không thể tải profile' };
        }
    };

    const updateProfile = async (payload) => {
        try {
            const data = await accountApi.updateProfile(payload);
            await refreshProfile();
            return { success: true, message: data?.message };
        } catch (e) {
            return { success: false, message: e?.message || 'Cập nhật profile thất bại' };
        }
    };

    const changePassword = async (currentPassword, newPassword, confirmPassword) => {
        try {
            const data = await accountApi.changePassword({
                currentPassword,
                newPassword,
                confirmPassword,
            });
            return { success: true, message: data?.message };
        } catch (e) {
            return { success: false, message: e?.message || 'Đổi mật khẩu thất bại' };
        }
    };

    const deleteMyAccount = async () => {
        try {
            const data = await accountApi.deleteAccount();
            logout();
            return { success: true, message: data?.message };
        } catch (e) {
            return { success: false, message: e?.message || 'Xóa tài khoản thất bại' };
        }
    };

    const uploadAvatar = async (file) => {
        try {
            const data = await accountApi.uploadAvatar(file);
            await refreshProfile();
            return { success: true, message: data?.message, avatarUrl: data?.avatarUrl };
        } catch (e) {
            return { success: false, message: e?.message || 'Upload avatar thất bại' };
        }
    };

    const value = {
        user,
        loading,
        login,
        register,
        verifyOtp,
        loginWithGoogle,
        loginWithFacebook,
        forgotPassword,
        resetPassword,
        refreshProfile,
        updateProfile,
        changePassword,
        deleteMyAccount,
        uploadAvatar,
        logout: logoutServer,
        isAuthenticated: !!user,
    };

    return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

export function useAuth() {
    const context = useContext(AuthContext);
    if (!context) {
        throw new Error('useAuth must be used within AuthProvider');
    }
    return context;
}
