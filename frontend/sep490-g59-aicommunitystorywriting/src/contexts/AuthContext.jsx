import { createContext, useContext, useState, useEffect } from 'react';
import {
    clearStoredAccessToken,
    getStoredAccessToken,
    setStoredAccessToken,
} from '../api/axiosInstance';
import * as authApi from '../api/auth/authApi';
import * as accountApi from '../api/account/accountApi';

const AuthContext = createContext(null);

function getApiOrigin() {
    const base = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';
    // remove trailing "/api" (or "/api/") to get server origin
    return String(base).replace(/\/api\/?$/i, '');
}

function resolveAvatarUrl(avatarUrl) {
    if (!avatarUrl) return null;
    const url = String(avatarUrl);
    if (url.startsWith('http://') || url.startsWith('https://')) return url;
    // backend returns relative path like "/uploads/avatars/..."
    return `${getApiOrigin()}${url.startsWith('/') ? '' : '/'}${url}`;
}

function mapProfileToUser(profile) {
    return {
        id: profile?.id,
        email: profile?.email,
        name: profile?.displayName || 'User',
        avatar: resolveAvatarUrl(profile?.avatarUrl),
        coins: profile?.stats?.currentCoins ?? 0,
        profile, // keep raw profile for profile pages
    };
}

export function AuthProvider({ children }) {
    const [user, setUser] = useState(null);
    const [loading, setLoading] = useState(true);

    async function loadProfile() {
        const profile = await accountApi.getMyProfile();
        const mapped = mapProfileToUser(profile);
        setUser(mapped);
        localStorage.setItem('user', JSON.stringify(mapped));
        return mapped;
    }

    // Load user/token from localStorage on mount
    useEffect(() => {
        const init = async () => {
            const token = getStoredAccessToken();
            const savedUser = localStorage.getItem('user');

            if (savedUser) {
                try {
                    setUser(JSON.parse(savedUser));
                } catch {
                    localStorage.removeItem('user');
                }
            }

            if (!token) {
                // no token => treat as logged out
                clearStoredAccessToken();
                localStorage.removeItem('user');
                setUser(null);
                setLoading(false);
                return;
            }

            try {
                await loadProfile();
            } catch {
                // token invalid/expired and refresh failed
                clearStoredAccessToken();
                localStorage.removeItem('user');
                setUser(null);
            } finally {
                setLoading(false);
            }
        };

        init();
    }, []);

    const login = async (email, password) => {
        try {
            const data = await authApi.login({ email, password }); // { accessToken }
            const accessToken = data?.accessToken || data?.AccessToken;
            if (!accessToken) {
                return { success: false, message: 'Thiếu access token từ server.' };
            }
            setStoredAccessToken(accessToken);

            const mapped = await loadProfile();
            return { success: true, user: mapped };
        } catch (e) {
            return { success: false, message: e?.message || 'Đăng nhập thất bại' };
        }
    };

    const register = async (email, password, name) => {
        try {
            const res = await authApi.register({ email, password, fullName: name });
            return { success: true, message: res?.message, email };
        } catch (e) {
            return { success: false, message: e?.message || 'Đăng ký thất bại' };
        }
    };

    const loginWithGoogle = async () => {
        return { success: false, message: 'Hiện chưa hỗ trợ đăng nhập Google.' };
    };

    const loginWithFacebook = async () => {
        return { success: false, message: 'Hiện chưa hỗ trợ đăng nhập Facebook.' };
    };

    const forgotPassword = async (email) => {
        try {
            const res = await authApi.forgotPassword({ email });
            return { success: true, message: res?.message };
        } catch (e) {
            return { success: false, message: e?.message || 'Gửi OTP thất bại' };
        }
    };

    const resetPassword = async ({ email, otpCode, newPassword, confirmPassword }) => {
        try {
            const res = await authApi.resetPassword({ email, otpCode, newPassword, confirmPassword });
            return { success: true, message: res?.message };
        } catch (e) {
            return { success: false, message: e?.message || 'Đặt lại mật khẩu thất bại' };
        }
    };

    const verifyOtp = async (email, otpCode) => {
        try {
            const res = await authApi.verifyOtp({ email, otpCode });
            return { success: true, message: res?.message };
        } catch (e) {
            return { success: false, message: e?.message || 'Xác thực OTP thất bại' };
        }
    };

    const logout = () => {
        // best-effort server logout (clear refresh cookie)
        authApi.logout().catch(() => {});
        clearStoredAccessToken();
        localStorage.removeItem('user');
        setUser(null);
    };

    // Account APIs
    const refreshProfile = async () => {
        try {
            const mapped = await loadProfile();
            return { success: true, user: mapped };
        } catch (e) {
            return { success: false, message: e?.message || 'Không lấy được profile' };
        }
    };

    const updateProfile = async (payload) => {
        try {
            const res = await accountApi.updateProfile(payload);
            await loadProfile();
            return { success: true, message: res?.message };
        } catch (e) {
            return { success: false, message: e?.message || 'Cập nhật profile thất bại' };
        }
    };

    const changePassword = async (payload) => {
        try {
            const res = await accountApi.changePassword(payload);
            return { success: true, message: res?.message };
        } catch (e) {
            return { success: false, message: e?.message || 'Đổi mật khẩu thất bại' };
        }
    };

    const uploadAvatar = async (file) => {
        try {
            const res = await accountApi.uploadAvatar(file);
            await loadProfile();
            return { success: true, message: res?.message, avatarUrl: resolveAvatarUrl(res?.avatarUrl) };
        } catch (e) {
            return { success: false, message: e?.message || 'Upload avatar thất bại' };
        }
    };

    const deleteAccount = async () => {
        try {
            const res = await accountApi.deleteAccount();
            logout();
            return { success: true, message: res?.message };
        } catch (e) {
            return { success: false, message: e?.message || 'Xóa tài khoản thất bại' };
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
        logout,
        refreshProfile,
        updateProfile,
        changePassword,
        uploadAvatar,
        deleteAccount,
        isAuthenticated: !!user && !!getStoredAccessToken(),
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
