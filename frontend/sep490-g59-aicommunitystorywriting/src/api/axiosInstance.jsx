import axios from "axios";

// Use HTTPS by default to avoid preflight redirect (backend uses UseHttpsRedirection)
const apiUrl = import.meta.env.VITE_API_URL || "https://localhost:7117/api";

const axiosInstance = axios.create({
    baseURL: apiUrl,
    headers: {
        "Content-Type": "application/json",
    },
    withCredentials: true,
});

const ACCESS_TOKEN_KEY = "accessToken";

export function getStoredAccessToken() {
    try {
        return localStorage.getItem(ACCESS_TOKEN_KEY);
    } catch {
        return null;
    }
}

export function setStoredAccessToken(token) {
    try {
        if (token) localStorage.setItem(ACCESS_TOKEN_KEY, token);
    } catch {
        // ignore
    }
}

export function clearStoredAccessToken() {
    try {
        localStorage.removeItem(ACCESS_TOKEN_KEY);
    } catch {
        // ignore
    }
}

// Attach access token (Bearer) on each request
axiosInstance.interceptors.request.use(
    (config) => {
        const token = getStoredAccessToken();
        if (token) {
            config.headers = config.headers || {};
            if (!config.headers.Authorization) {
                config.headers.Authorization = `Bearer ${token}`;
            }
        }
        return config;
    },
    (error) => Promise.reject(error)
);

// Refresh access token on 401 (refresh token is HttpOnly cookie)
const refreshClient = axios.create({
    baseURL: apiUrl,
    headers: { "Content-Type": "application/json" },
    withCredentials: true,
});

let refreshPromise = null;

function isAuthRoute(url = "") {
    const u = String(url).toLowerCase();
    return (
        u.includes("/auth/login") ||
        u.includes("/auth/register") ||
        u.includes("/auth/verify-otp") ||
        u.includes("/auth/refresh") ||
        u.includes("/auth/logout") ||
        u.includes("/auth/forgot-password") ||
        u.includes("/auth/reset-password")
    );
}

axiosInstance.interceptors.response.use(
    (response) => response,
    async (error) => {
        const status = error?.response?.status;
        const originalConfig = error?.config;

        if (!originalConfig || status !== 401) {
            return Promise.reject(error);
        }

        // Don't attempt refresh for auth endpoints
        if (isAuthRoute(originalConfig.url)) {
            return Promise.reject(error);
        }

        if (originalConfig.__isRetryRequest) {
            return Promise.reject(error);
        }
        originalConfig.__isRetryRequest = true;

        try {
            if (!refreshPromise) {
                refreshPromise = refreshClient
                    .post("/auth/refresh")
                    .then((r) => r.data)
                    .finally(() => {
                        refreshPromise = null;
                    });
            }

            const data = await refreshPromise;
            const newToken = data?.accessToken || data?.AccessToken;
            if (!newToken) {
                clearStoredAccessToken();
                return Promise.reject(error);
            }

            setStoredAccessToken(newToken);
            originalConfig.headers = originalConfig.headers || {};
            originalConfig.headers.Authorization = `Bearer ${newToken}`;
            return axiosInstance(originalConfig);
        } catch (refreshErr) {
            clearStoredAccessToken();
            return Promise.reject(error);
        }
    }
);

export default axiosInstance;