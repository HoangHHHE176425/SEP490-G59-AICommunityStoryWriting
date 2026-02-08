import axios from "axios";

const apiUrl = import.meta.env.VITE_API_URL || "https://localhost:7117/api";

const axiosInstance = axios.create({
    baseURL: apiUrl,
    headers: {
        "Content-Type": "application/json",
    },
    withCredentials: true,
});

function getAccessToken() {
    return localStorage.getItem("accessToken");
}

function setAccessToken(token) {
    if (token) localStorage.setItem("accessToken", token);
}

function clearAccessToken() {
    localStorage.removeItem("accessToken");
}

// A separate client without interceptors to avoid infinite loops when refreshing.
const refreshClient = axios.create({
    baseURL: apiUrl,
    withCredentials: true,
    headers: { "Content-Type": "application/json" },
});

axiosInstance.interceptors.request.use(
    (config) => {
        const token = getAccessToken();
        if (token) {
            config.headers = config.headers ?? {};
            config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
    },
    (error) => Promise.reject(error)
);

axiosInstance.interceptors.response.use(
    (response) => response,
    async (error) => {
        const originalRequest = error?.config;
        const status = error?.response?.status;

        if (!originalRequest || status !== 401) {
            return Promise.reject(error);
        }

        // Avoid retry loops.
        if (originalRequest._retry) {
            return Promise.reject(error);
        }

        // Don't try refresh for auth endpoints.
        const url = String(originalRequest.url || "");
        const isAuthEndpoint =
            url.includes("/Auth/login") ||
            url.includes("/Auth/register") ||
            url.includes("/Auth/verify-otp") ||
            url.includes("/Auth/forgot-password") ||
            url.includes("/Auth/reset-password") ||
            url.includes("/Auth/refresh");

        if (isAuthEndpoint) {
            return Promise.reject(error);
        }

        originalRequest._retry = true;

        try {
            const refreshRes = await refreshClient.post("/Auth/refresh");
            const newToken = refreshRes?.data?.accessToken;
            if (!newToken) {
                clearAccessToken();
                return Promise.reject(error);
            }

            setAccessToken(newToken);
            originalRequest.headers = originalRequest.headers ?? {};
            originalRequest.headers.Authorization = `Bearer ${newToken}`;

            return axiosInstance(originalRequest);
        } catch (refreshErr) {
            clearAccessToken();
            return Promise.reject(refreshErr);
        }
    }
);

export default axiosInstance;