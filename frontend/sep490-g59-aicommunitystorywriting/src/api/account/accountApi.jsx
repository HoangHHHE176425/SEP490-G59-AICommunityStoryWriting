import axiosInstance from "../axiosInstance";

function getErrorMessage(err) {
    return (
        err?.response?.data?.message ||
        err?.response?.data?.title ||
        err?.message ||
        "Đã xảy ra lỗi. Vui lòng thử lại."
    );
}

export async function getMyProfile() {
    const res = await axiosInstance.get("/Account/profile");
    return res.data;
}

export async function updateProfile(payload) {
    try {
        const res = await axiosInstance.put("/Account/profile", payload);
        return { success: true, data: res.data };
    } catch (err) {
        return { success: false, message: getErrorMessage(err) };
    }
}

export async function changePassword(payload) {
    try {
        const res = await axiosInstance.put("/Account/change-password", payload);
        return { success: true, data: res.data };
    } catch (err) {
        return { success: false, message: getErrorMessage(err) };
    }
}

export async function deleteAccount() {
    try {
        const res = await axiosInstance.delete("/Account/delete");
        return { success: true, data: res.data };
    } catch (err) {
        return { success: false, message: getErrorMessage(err) };
    }
}

export async function uploadAvatar(file) {
    const formData = new FormData();
    formData.append("avatar", file);
    try {
        const res = await axiosInstance.post("/Account/avatar", formData, {
            headers: { "Content-Type": "multipart/form-data" },
        });
        return { success: true, data: res.data };
    } catch (err) {
        return { success: false, message: getErrorMessage(err) };
    }
}

