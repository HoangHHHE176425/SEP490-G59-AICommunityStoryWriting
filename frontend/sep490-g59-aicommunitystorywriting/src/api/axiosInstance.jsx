import axios from "axios";
import { translateBackendMessage } from "../utils/translateBackendMessage";

const apiUrl = import.meta.env.VITE_API_URL || "http://localhost:5000/api";

const axiosInstance = axios.create({
    baseURL: apiUrl,
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

function extractAccessToken(payload) {
    return payload?.accessToken ?? payload?.AccessToken ?? "";
}

function notifySessionEnded(message) {
    if (typeof window === "undefined") return;
    window.dispatchEvent(new CustomEvent("app:auth:session-ended", {
        detail: {
            message: message || "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
        }
    }));
}

// A separate client without interceptors to avoid infinite loops when refreshing.
const refreshClient = axios.create({
    baseURL: apiUrl,
    withCredentials: true,
    headers: { "Content-Type": "application/json" },
});

axiosInstance.interceptors.request.use(
    (config) => {
        const h = config.headers;

        if (config.data instanceof FormData) {
            // Không gửi Content-Type — trình duyệt thêm multipart/form-data; boundary=...
            if (h && typeof h.setContentType === "function") {
                h.setContentType(false);
            } else if (h && typeof h.delete === "function") {
                h.delete("Content-Type");
            }
        } else if (
            config.data != null &&
            typeof config.data === "object" &&
            !(config.data instanceof URLSearchParams) &&
            !(config.data instanceof Blob)
        ) {
            const hasCt =
                (typeof h?.get === "function" && h.get("Content-Type")) ||
                (h && (h["Content-Type"] || h["content-type"]));
            if (!hasCt && h && typeof h.setContentType === "function") {
                h.setContentType("application/json");
            } else if (!hasCt && h) {
                h["Content-Type"] = "application/json";
            }
        }

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
        const data = err?.response?.data;
        if (data && typeof data === "object" && !Array.isArray(data)) {
            if ("message" in data) data.message = translated;
            if ("Message" in data) data.Message = translated;
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
            const newToken = extractAccessToken(refreshRes?.data);
            if (!newToken) {
                clearAccessToken();
                const translatedError = translateAxiosErrorMessage(error);
                notifySessionEnded(translatedError?.response?.data?.message ?? translatedError?.message);
                return Promise.reject(translatedError);
            }

            setAccessToken(newToken);
            originalRequest.headers = originalRequest.headers ?? {};
            originalRequest.headers.Authorization = `Bearer ${newToken}`;

            return axiosInstance(originalRequest);
        } catch (refreshErr) {
            clearAccessToken();
            const translatedRefreshErr = translateAxiosErrorMessage(refreshErr);
            notifySessionEnded(translatedRefreshErr?.response?.data?.message ?? translatedRefreshErr?.message);
            return Promise.reject(translatedRefreshErr);
        }
    }
);

export default axiosInstance;