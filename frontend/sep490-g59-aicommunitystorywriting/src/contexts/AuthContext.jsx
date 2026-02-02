import { createContext, useContext, useState, useEffect } from 'react';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
    const [user, setUser] = useState(null);
    const [loading, setLoading] = useState(true);

    // Load user from localStorage on mount
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
        setLoading(false);
    }, []);

    const login = async (email, password) => {
        // Mock login - replace with actual API call
        const mockUser = {
            id: '1',
            email,
            name: 'Nguyễn Văn A',
            avatar: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=100&h=100&fit=crop',
            coins: 1250,
        };
        setUser(mockUser);
        localStorage.setItem('user', JSON.stringify(mockUser));
        return { success: true, user: mockUser };
    };

    const register = async (email, password, name) => {
        // Mock register - replace with actual API call
        const mockUser = {
            id: '1',
            email,
            name,
            avatar: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=100&h=100&fit=crop',
            coins: 0,
        };
        setUser(mockUser);
        localStorage.setItem('user', JSON.stringify(mockUser));
        return { success: true, user: mockUser };
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
        // Mock forgot password - replace with actual API call
        return { success: true, message: 'Email đặt lại mật khẩu đã được gửi!' };
    };

    const logout = () => {
        setUser(null);
        localStorage.removeItem('user');
    };

    const value = {
        user,
        loading,
        login,
        register,
        loginWithGoogle,
        loginWithFacebook,
        forgotPassword,
        logout,
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
