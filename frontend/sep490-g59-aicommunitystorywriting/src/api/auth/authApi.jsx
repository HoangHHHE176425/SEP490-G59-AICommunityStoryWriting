import axiosInstance from "../axiosInstance";

function getAxiosErrorMessage(error) {
    // Axios error shape: error.response?.data?.message or error.message
    const msg =
        error?.response?.data?.message ||
        error?.response?.data?.title || // sometimes ASP.NET returns { title: ... }
        error?.message;
    return msg || "Đã xảy ra lỗi. Vui lòng thử lại.";
}

export async function register({ email, password, fullName }) {
    try {
        const res = await axiosInstance.post("/auth/register", {
            email,
            password,
            fullName,
        });
        return res.data;
    } catch (e) {
        throw new Error(getAxiosErrorMessage(e));
    }
}

export async function verifyOtp({ email, otpCode }) {
    try {
        const res = await axiosInstance.post("/auth/verify-otp", {
            email,
            otpCode,
        });
        return res.data;
    } catch (e) {
        throw new Error(getAxiosErrorMessage(e));
    }
}

export async function login({ email, password }) {
    try {
        const res = await axiosInstance.post("/auth/login", {
            email,
            password,
        });
        return res.data; // { accessToken }
    } catch (e) {
        throw new Error(getAxiosErrorMessage(e));
    }
}

export async function refresh() {
    try {
        // refreshToken is stored in HttpOnly cookie, sent automatically (credentials include)
        const res = await axiosInstance.post("/auth/refresh");
        return res.data; // { accessToken }
    } catch (e) {
        throw new Error(getAxiosErrorMessage(e));
    }
}

export async function logout() {
    try {
        const res = await axiosInstance.post("/auth/logout");
        return res.data;
    } catch (e) {
        // logout should be best-effort; still surface message if needed
        throw new Error(getAxiosErrorMessage(e));
    }
}

export async function forgotPassword({ email }) {
    try {
        const res = await axiosInstance.post("/auth/forgot-password", { email });
        return res.data;
    } catch (e) {
        throw new Error(getAxiosErrorMessage(e));
    }
}

export async function resetPassword({ email, otpCode, newPassword, confirmPassword }) {
    try {
        const res = await axiosInstance.post("/auth/reset-password", {
            email,
            otpCode,
            newPassword,
            confirmPassword,
        });
        return res.data;
    } catch (e) {
        throw new Error(getAxiosErrorMessage(e));
    }
}

