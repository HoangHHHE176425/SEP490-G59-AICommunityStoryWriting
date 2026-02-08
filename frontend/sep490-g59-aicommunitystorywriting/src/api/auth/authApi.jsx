import axiosInstance from "../axiosInstance";

function getErrorMessage(err) {
    return (
        err?.response?.data?.message ||
        err?.response?.data?.title ||
        err?.message ||
        "Đã xảy ra lỗi. Vui lòng thử lại."
    );
}

export async function register({ email, password, fullName }) {
    try {
        const res = await axiosInstance.post("/Auth/register", {
            email,
            password,
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

export async function login({ email, password }) {
    try {
        const res = await axiosInstance.post("/Auth/login", { email, password });
        const accessToken = res?.data?.accessToken;
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
        const accessToken = res?.data?.accessToken;
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

