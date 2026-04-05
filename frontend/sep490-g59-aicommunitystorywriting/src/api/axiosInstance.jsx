import axios from "axios";
import { translateBackendMessage } from "../utils/translateBackendMessage";

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

function translateAxiosErrorMessage(err) {
    const originalMsg =
        err?.response?.data?.message ??
        err?.response?.data?.Message ??
        err?.message;
    const translated = translateBackendMessage(originalMsg);
    if (translated && translated !== originalMsg) {
        // Mutate in-place so callers using `err.response.data.message` get translated text.
        if (err?.response?.data) {
            if ('message' in err.response.data) err.response.data.message = translated;
            if ('Message' in err.response.data) err.response.data.Message = translated;
        }
        err.message = translated;
    }
    return err;
}

axiosInstance.interceptors.response.use(
    (response) => {
        // Một số API trả HTTP 200 nhưng success=false và message EN.
        // Chỉ dịch khi rõ ràng là lỗi (success === false).
        const data = response?.data;
        if (data && data.success === false && typeof data.message === "string") {
            const translated = translateBackendMessage(data.message);
            response.data.message = translated;
        }
        return response;
    },
    async (error) => {
        const originalRequest = error?.config;
        const status = error?.response?.status;

        if (!originalRequest || status !== 401) {
            return Promise.reject(translateAxiosErrorMessage(error));
        }

        // Chưa đăng nhập: không có accessToken → không gọi refresh (tránh lỗi "Missing refresh token" / vòng 401).
        if (!getAccessToken()) {
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
            return Promise.reject(translateAxiosErrorMessage(error));
        }

        originalRequest._retry = true;

        try {
            const refreshRes = await refreshClient.post("/Auth/refresh");
            const newToken = refreshRes?.data?.accessToken;
            if (!newToken) {
                clearAccessToken();
                return Promise.reject(translateAxiosErrorMessage(error));
            }

            setAccessToken(newToken);
            originalRequest.headers = originalRequest.headers ?? {};
            originalRequest.headers.Authorization = `Bearer ${newToken}`;

            return axiosInstance(originalRequest);
        } catch (refreshErr) {
            clearAccessToken();
            return Promise.reject(translateAxiosErrorMessage(refreshErr));
        }
    }
);

export default axiosInstance;