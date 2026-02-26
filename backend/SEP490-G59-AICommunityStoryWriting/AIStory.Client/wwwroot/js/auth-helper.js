// Auth Helper - Quản lý authentication và user session
const AuthHelper = {
    TOKEN_KEY: 'auth_token',
    USER_KEY: 'user_info',

    // Lưu token vào localStorage
    setToken(token) {
        if (token) {
            localStorage.setItem(this.TOKEN_KEY, token);
            // Cập nhật header cho các request tiếp theo
            this.updateApiHeaders();
        }
    },

    // Lấy token từ localStorage
    getToken() {
        return localStorage.getItem(this.TOKEN_KEY);
    },

    // Xóa token
    removeToken() {
        localStorage.removeItem(this.TOKEN_KEY);
        localStorage.removeItem(this.USER_KEY);
    },

    // Kiểm tra đã đăng nhập chưa
    isAuthenticated() {
        return !!this.getToken();
    },

    // Lưu thông tin user
    setUser(user) {
        if (user) {
            localStorage.setItem(this.USER_KEY, JSON.stringify(user));
        }
    },

    // Lấy thông tin user
    getUser() {
        const userStr = localStorage.getItem(this.USER_KEY);
        return userStr ? JSON.parse(userStr) : null;
    },

    // Tải thông tin user từ token
    async loadUserInfo() {
        try {
            const token = this.getToken();
            if (token) {
                const decoded = this.decodeToken(token);
                if (decoded) {
                    // Lưu thông tin user từ token
                    const userInfo = {
                        id: decoded.sub || decoded.nameid || decoded.userId || decoded.id,
                        email: decoded.email || decoded.sub || '',
                        role: decoded.role || decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || 'USER'
                    };
                    this.setUser(userInfo);
                    return Promise.resolve(userInfo);
                }
            }
            return Promise.resolve(null);
        } catch (error) {
            console.error('Failed to load user info:', error);
            return Promise.reject(error);
        }
    },

    // Cập nhật header Authorization cho ApiService (không cần override nữa vì ApiService đã tự động thêm)
    updateApiHeaders() {
        // ApiService.request đã tự động thêm token từ AuthHelper.getToken()
        // Không cần làm gì thêm
    },

    // Đăng xuất
    async logout() {
        try {
            await ApiService.logout();
        } catch (error) {
            console.error('Logout error:', error);
        } finally {
            this.removeToken();
            window.location.href = '/Auth/Login';
        }
    },

    // Decode JWT token (đơn giản, không verify)
    decodeToken(token) {
        try {
            const base64Url = token.split('.')[1];
            const base64 = base64Url.replace(/-/g, '+').replace(/_/g, '/');
            const jsonPayload = decodeURIComponent(atob(base64).split('').map(function (c) {
                return '%' + ('00' + c.charCodeAt(0).toString(16)).slice(-2);
            }).join(''));
            return JSON.parse(jsonPayload);
        } catch (error) {
            return null;
        }
    },

    // Lấy user ID từ token (Author ID)
    getUserId() {
        const token = this.getToken();
        if (token) {
            const decoded = this.decodeToken(token);
            // JWT thường dùng 'sub' (subject) hoặc 'nameid' cho user ID
            return decoded?.sub || decoded?.nameid || decoded?.userId || decoded?.id || null;
        }
        return null;
    },

    // Lấy Author ID (alias của getUserId)
    getAuthorId() {
        return this.getUserId();
    },

    // Lấy role của user hiện tại
    getUserRole() {
        const user = this.getUser();
        if (user && user.role) {
            return user.role.toUpperCase();
        }
        // Fallback: decode từ token
        const token = this.getToken();
        if (token) {
            const decoded = this.decodeToken(token);
            if (decoded) {
                return (decoded.role || decoded['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || 'USER').toUpperCase();
            }
        }
        return 'USER';
    },

    // Kiểm tra user có role cụ thể không
    hasRole(role) {
        const userRole = this.getUserRole();
        return userRole === role.toUpperCase();
    },

    // Kiểm tra user có một trong các roles không
    hasAnyRole(...roles) {
        const userRole = this.getUserRole();
        return roles.some(role => userRole === role.toUpperCase());
    },

    // Kiểm tra là ADMIN
    isAdmin() {
        return this.hasRole('ADMIN');
    },

    // Kiểm tra là AUTHOR
    isAuthor() {
        return this.hasRole('AUTHOR');
    },

    // Kiểm tra là MODERATOR hoặc ADMIN (quyền kiểm duyệt)
    isModerator() {
        return this.hasAnyRole('MODERATOR', 'ADMIN');
    },

    // Kiểm tra token có hết hạn không
    isTokenExpired() {
        const token = this.getToken();
        if (!token) return true;

        const decoded = this.decodeToken(token);
        if (!decoded || !decoded.exp) return true;

        const currentTime = Date.now() / 1000;
        return decoded.exp < currentTime;
    },

    // Refresh token nếu hết hạn
    async ensureValidToken() {
        if (this.isTokenExpired()) {
            try {
                const response = await ApiService.refreshToken();
                if (response?.accessToken) {
                    this.setToken(response.accessToken);
                    return true;
                }
            } catch (error) {
                console.error('Token refresh failed:', error);
                this.logout();
                return false;
            }
        }
        return true;
    }
};

// Khởi tạo khi load trang
document.addEventListener('DOMContentLoaded', function () {
    // Cập nhật headers nếu đã có token
    if (AuthHelper.isAuthenticated()) {
        AuthHelper.updateApiHeaders();
    }
});
