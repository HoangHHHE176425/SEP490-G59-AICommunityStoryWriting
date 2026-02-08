import axiosInstance from "../axiosInstance";

function getAxiosErrorMessage(error) {
    const msg =
        error?.response?.data?.message ||
        error?.response?.data?.title ||
        error?.message;
    return msg || "Đã xảy ra lỗi. Vui lòng thử lại.";
}

export async function getMyProfile() {
    try {
        const res = await axiosInstance.get("/account/profile");
        return res.data;
    } catch (e) {
        throw new Error(getAxiosErrorMessage(e));
    }
}

export async function updateProfile(payload) {
    try {
        const res = await axiosInstance.put("/account/profile", payload);
        return res.data; // { message }
    } catch (e) {
        throw new Error(getAxiosErrorMessage(e));
    }
}

export async function changePassword(payload) {
    try {
        const res = await axiosInstance.put("/account/change-password", payload);
        return res.data; // { message }
    } catch (e) {
        throw new Error(getAxiosErrorMessage(e));
    }
}

export async function deleteAccount() {
    try {
        const res = await axiosInstance.delete("/account/delete");
        return res.data; // { message }
    } catch (e) {
        throw new Error(getAxiosErrorMessage(e));
    }
}

export async function uploadAvatar(file) {
    try {
        const formData = new FormData();
        formData.append("avatar", file);
        const res = await axiosInstance.post("/account/avatar", formData, {
            headers: { "Content-Type": "multipart/form-data" },
        });
        return res.data; // { message, avatarUrl }
    } catch (e) {
        throw new Error(getAxiosErrorMessage(e));
    }
}

