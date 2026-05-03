import axiosInstance from "../axiosInstance";
import { translateBackendMessage } from "../../utils/translateBackendMessage";

function extractAccessToken(payload) {
    return payload?.accessToken ?? payload?.AccessToken ?? "";
}

function getErrorMessage(err) {
    const payload = err?.response?.data;
    const validationErrors =
        payload?.errors && typeof payload.errors === "object"
            ? Object.values(payload.errors).flat().filter(Boolean)
            : [];
    const payloadText =
        typeof payload === "string"
            ? payload
            : typeof payload?.error === "string"
                ? payload.error
                : typeof payload?.detail === "string"
                    ? payload.detail
                    : "";
    const raw =
        payload?.message ||
        payload?.title ||
        payloadText ||
        validationErrors[0] ||
        err?.message ||
        "Đã xảy ra lỗi. Vui lòng thử lại.";
    const normalized = typeof raw === "string" ? raw.trim() : String(raw);
    // BR-02: email trùng (BE thường trả "Email already exists.")
    if (/email.*already.*exist/i.test(normalized)) {
        return "Email này đã được đăng ký. Vui lòng dùng email khác hoặc đăng nhập.";
    }
    // Login: tránh lộ email có tồn tại hay không, nhưng hiển thị message thân thiện cho user.
    if (/invalid\s+email\s+or\s+password/i.test(normalized)) {
        return "Email hoặc mật khẩu không đúng. Nếu chưa có tài khoản, vui lòng đăng ký.";
    }
    return translateBackendMessage(normalized);
}

export async function register({ email, password, confirmPassword, fullName }) {
    try {
        const res = await axiosInstance.post("/Auth/register", {
            email,
            password,
            confirmPassword,
            fullName,
        });
        return { success: true, data: res.data };
    } catch (err) {
        return { success: false, message: getErrorMessage(err) };
    }
}

export async function verifyOtp({ email, otpCode }) {
    try {
        const res = await axiosInstance.post("/Auth/verify-otp", {
            email,
            otpCode,
        });
        return { success: true, data: res.data };
    } catch (err) {
        return { success: false, message: getErrorMessage(err) };
    }
}

export async function resendOtp({ email }) {
    try {
        const res = await axiosInstance.post("/Auth/resend-otp", { email });
        return { success: true, data: res.data };
    } catch (err) {
        return { success: false, message: getErrorMessage(err) };
    }
}

export async function login({ email, password }) {
    try {
        const res = await axiosInstance.post("/Auth/login", { email, password });
        const accessToken = extractAccessToken(res?.data);
        if (accessToken) {
            localStorage.setItem("accessToken", accessToken);
        }
        return { success: true, accessToken };
    } catch (err) {
        return { success: false, message: getErrorMessage(err) };
    }
}

export async function refresh() {
    // Used when you want to refresh proactively (interceptor handles most cases).
    try {
        const res = await axiosInstance.post("/Auth/refresh");
        const accessToken = extractAccessToken(res?.data);
        if (accessToken) {
            localStorage.setItem("accessToken", accessToken);
        }
        return { success: true, accessToken };
    } catch (err) {
        return { success: false, message: getErrorMessage(err) };
    }
}

export async function logout() {
    try {
        await axiosInstance.post("/Auth/logout");
    } catch {
        // ignore
    } finally {
        localStorage.removeItem("accessToken");
        localStorage.removeItem("user");
    }
    return { success: true };
}

export async function forgotPassword({ email }) {
    try {
        const res = await axiosInstance.post("/Auth/forgot-password", { email });
        return { success: true, data: res.data };
    } catch (err) {
        return { success: false, message: getErrorMessage(err) };
    }
}

export async function resetPassword({ email, otpCode, newPassword, confirmPassword }) {
    try {
        const res = await axiosInstance.post("/Auth/reset-password", {
            email,
            otpCode,
            newPassword,
            confirmPassword,
        });
        return { success: true, data: res.data };
    } catch (err) {
        return { success: false, message: getErrorMessage(err) };
    }
}

