import { createContext, useContext, useState, useEffect } from 'react';
import * as authApi from '../api/auth/authApi';
import * as accountApi from '../api/account/accountApi';

// Preserve context identity across Vite HMR to avoid "useAuth must be used within AuthProvider"
// when modules reload and recreate a new Context instance.
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

    const register = async (email, password, name) => {
        return await authApi.register({ email, password, fullName: name });
    };

    const verifyOtp = async (email, otpCode) => {
        return await authApi.verifyOtp({ email, otpCode });
    };

    const loginWithGoogle = async () => {
        return { success: false, message: 'Google login chưa được tích hợp ở backend.' };
    };

    const loginWithFacebook = async () => {
        return { success: false, message: 'Facebook login chưa được tích hợp ở backend.' };
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
        fetchProfile,
        updateMyProfile,
        changeMyPassword,
        deleteMyAccount,
        uploadMyAvatar,
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
