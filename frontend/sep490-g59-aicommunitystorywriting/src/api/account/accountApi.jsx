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

/**
 * Lấy thông tin hồ sơ người dùng theo userId (dùng để hiển thị tác giả, v.v.).
 * Backend: GET /Account/profile/{userId}
 * @param {string} userId - Guid
 * @returns {Promise<{ id, displayName, email, avatarUrl, bio, stats, ... }>}
 */
export async function getProfileByUserId(userId) {
    if (!userId) throw new Error("userId là bắt buộc");
    const res = await axiosInstance.get(`/Account/profile/${userId}`);
    const d = res.data;
    return {
        id: d.id ?? d.Id,
        displayName: d.displayName ?? d.DisplayName ?? d.email?.split?.('@')?.[0] ?? 'Ẩn danh',
        email: d.email ?? d.Email ?? '',
        phone: d.phone ?? d.Phone ?? '',
        avatarUrl: d.avatarUrl ?? d.AvatarUrl ?? '',
        bio: d.bio ?? d.Bio ?? '',
        description: d.description ?? d.Description ?? '',
        joinDate: d.joinDate ?? d.JoinDate ?? '',
        isVerified: d.isVerified ?? d.IsVerified ?? false,
        tags: d.tags ?? d.Tags ?? [],
        stats: (() => {
            const s = d.stats ?? d.Stats ?? {};
            return {
                storiesWritten: s.storiesWritten ?? s.StoriesWritten ?? 0,
                totalReads: s.totalReads ?? s.TotalReads ?? 0,
                likes: s.likes ?? s.Likes ?? 0,
                currentCoins: s.currentCoins ?? s.CurrentCoins ?? 0,
            };
        })(),
    };
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

function normalizeAuthorOnboardingStatus(data) {
    if (!data) return null;
    return {
        currentRole: (data.currentRole ?? data.CurrentRole ?? "USER").toString().trim().toUpperCase(),
        isAuthor: data.isAuthor ?? data.IsAuthor ?? false,
        hasActiveAuthorPolicy: data.hasActiveAuthorPolicy ?? data.HasActiveAuthorPolicy ?? false,
        activeAuthorPolicyId: data.activeAuthorPolicyId ?? data.ActiveAuthorPolicyId ?? null,
        activeAuthorPolicyVersion: data.activeAuthorPolicyVersion ?? data.ActiveAuthorPolicyVersion ?? null,
        hasAcceptedActivePolicy: data.hasAcceptedActivePolicy ?? data.HasAcceptedActivePolicy ?? false,
        acceptedAt: data.acceptedAt ?? data.AcceptedAt ?? null,
        canBecomeAuthor: data.canBecomeAuthor ?? data.CanBecomeAuthor ?? false,
        missingRequirements: data.missingRequirements ?? data.MissingRequirements ?? [],
    };
}

export async function getAuthorOnboardingStatus() {
    const res = await axiosInstance.get("/Account/author-onboarding");
    return normalizeAuthorOnboardingStatus(res.data);
}

export async function becomeAuthor() {
    try {
        const res = await axiosInstance.post("/Account/become-author");
        return {
            success: true,
            data: {
                accessToken: res?.data?.accessToken ?? res?.data?.AccessToken ?? "",
                role: res?.data?.role ?? res?.data?.Role ?? "AUTHOR",
                policyId: res?.data?.policyId ?? res?.data?.PolicyId ?? null,
                acceptedPolicyNow: res?.data?.acceptedPolicyNow ?? res?.data?.AcceptedPolicyNow ?? false,
                acceptedAt: res?.data?.acceptedAt ?? res?.data?.AcceptedAt ?? null,
            },
        };
    } catch (err) {
        return { success: false, message: getErrorMessage(err) };
    }
}

