/**
 * API quản lý người dùng (Admin).
 * Hiện dùng dữ liệu mock; khi ghép BE chỉ cần thay nội dung từng hàm bằng gọi axiosInstance.
 *
 * Chuẩn params/response để dễ ghép:
 * - getUsers({ page, pageSize, search, status, role }) => { items, totalCount, page, totalPages }
 * - getStats() => { total, active, inactive, banned, authors }
 * - updateUserStatus(userId, status) => { success }
 * - updateUserRole(userId, role) => { success } (nếu BE hỗ trợ)
 */

import axiosInstance from '../axiosInstance';

/**
 * Chuẩn hóa 1 user từ API (users + user_profiles).
 * BE có thể trả user kèm profile hoặc tách; đều map về format thống nhất.
 */
function normalizeUser(item) {
    const profile = item.profile ?? item.Profile ?? item.user_profile ?? {};
    return {
        id: item.id ?? item.Id ?? profile.user_id ?? profile.userId,
        email: item.email ?? item.Email ?? '',
        role: (item.role ?? item.Role ?? 'USER').toUpperCase(),
        status: (item.status ?? item.Status ?? 'ACTIVE').toUpperCase(),
        emailVerifiedAt: item.email_verified_at ?? item.emailVerifiedAt ?? item.EmailVerifiedAt ?? null,
        mustResignPolicy: item.must_resign_policy ?? item.mustResignPolicy ?? 0,
        deletionRequestedAt: item.deletion_requested_at ?? item.deletionRequestedAt ?? null,
        createdAt: item.createdAt ?? item.CreatedAt ?? item.created_at ?? null,
        updatedAt: item.updatedAt ?? item.UpdatedAt ?? item.updated_at ?? null,
        lastLoginAt: item.lastLoginAt ?? item.LastLoginAt ?? item.last_login_at ?? null,
        // user_profiles
        nickname: profile.nickname ?? profile.Nickname ?? item.nickname ?? null,
        phone: profile.phone ?? profile.Phone ?? item.phone ?? null,
        idNumber: profile.id_number ?? profile.idNumber ?? profile.IdNumber ?? item.id_number ?? null,
        avatarUrl: profile.avatar_url ?? profile.avatarUrl ?? profile.AvatarUrl ?? item.avatarUrl ?? item.avatar_url ?? null,
        bio: profile.bio ?? profile.Bio ?? item.bio ?? null,
        description: profile.description ?? profile.Description ?? item.description ?? null,
        socialLinks: profile.social_links ?? profile.socialLinks ?? null,
        settings: profile.settings ?? profile.Settings ?? null,
        profileUpdatedAt: profile.updated_at ?? profile.updatedAt ?? null,
        // moderator: thể loại được gán kiểm duyệt (khi role = MODERATOR)
        moderatorCategoryIds: item.moderatorCategoryIds ?? item.moderator_category_ids ?? profile.moderatorCategoryIds ?? [],
    };
}

/** Display name: nickname ưu tiên, fallback email */
export function getUserDisplayName(user) {
    if (!user) return '—';
    const name = user.nickname ?? user.fullName ?? user.email;
    return name && String(name).trim() ? name : '—';
}

// ========== API functions (thay body bằng axios khi ghép BE) ==========

/**
 * Lấy danh sách người dùng (phân trang, tìm kiếm, lọc)
 * @param {Object} params - { page, pageSize, search, status, role }
 * @returns {Promise<{ items: Array, totalCount: number, page: number, totalPages: number }>}
 */
export async function getUsers(params = {}) {
    const { page = 1, pageSize = 10, search = '', status = '', role = '' } = params;

    const res = await axiosInstance.get('/admin/users', {
        params: {
            page,
            pageSize,
            search: search || undefined,
            status: status || undefined,
            role: role || undefined,
        },
    });
    const data = res.data ?? {};
    const totalCount = data.totalCount ?? data.total ?? 0;
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
    return {
        items: (data.items ?? data.data ?? []).map(normalizeUser),
        totalCount,
        page: data.page ?? page,
        totalPages,
    };
}

/**
 * Thống kê nhanh (tổng user, active, inactive, banned, authors)
 * @returns {Promise<{ total: number, active: number, inactive: number, banned: number, authors: number }>}
 */
export async function getStats() {
    const res = await axiosInstance.get('/admin/users/stats');
    return res.data;
}

/**
 * Cập nhật trạng thái user (ACTIVE | INACTIVE | BANNED)
 * @param {string} userId
 * @param {string} status
 */
export async function updateUserStatus(userId, status) {
    const s = String(status ?? '').trim().toUpperCase();
    if (s === 'BANNED') {
        await axiosInstance.post(`/admin/users/${userId}/lock`);
        return { success: true };
    }
    if (s === 'ACTIVE') {
        await axiosInstance.post(`/admin/users/${userId}/unlock`);
        return { success: true };
    }
    throw new Error('Status not supported by backend endpoint. Use ACTIVE or BANNED.');
}

/**
 * Cập nhật role user (nếu backend hỗ trợ)
 * @param {string} userId
 * @param {string} role - USER | AUTHOR | ADMIN
 */
export async function updateUserRole(userId, role) {
    await axiosInstance.post(`/admin/users/${userId}/role`, { role });
    return { success: true };
}

/**
 * Gán user làm moderator (kiểm duyệt) theo các thể loại truyện.
 * @param {string} userId
 * @param {string[]} categoryIds - Mảng id thể loại (categories) user được quyền kiểm duyệt
 */
export async function assignModeratorCategories(userId, categoryIds) {
    await axiosInstance.put(`/admin/users/${userId}/moderator-categories`, { categoryIds });
    return { success: true };
}

/**
 * Lấy danh sách thể loại mà moderator được gán (khi BE có API).
 * @param {string} userId
 * @returns {Promise<{ categoryIds: string[] }>}
 */
export async function getModeratorCategories(userId) {
    const res = await axiosInstance.get(`/admin/users/${userId}/moderator-categories`);
    return { categoryIds: res.data?.categoryIds ?? res.data?.category_ids ?? [] };
}
