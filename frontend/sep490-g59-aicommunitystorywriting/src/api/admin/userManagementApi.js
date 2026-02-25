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

// ========== Mock data (xóa khi ghép API thật) ==========
const MOCK_USERS = [
    { id: '1', email: 'user1@example.com', role: 'USER', status: 'ACTIVE', email_verified_at: '2025-01-16T00:00:00Z', created_at: '2025-01-15T10:00:00Z', updated_at: '2025-02-20T08:30:00Z', profile: { nickname: 'Nguyễn Văn A', phone: '0901234567', id_number: null, avatar_url: null, bio: 'Xin chào', description: null } },
    { id: '2', email: 'author1@example.com', role: 'AUTHOR', status: 'ACTIVE', email_verified_at: '2025-01-11T00:00:00Z', created_at: '2025-01-10T12:00:00Z', updated_at: '2025-02-19T14:00:00Z', profile: { nickname: 'Trần Thị B', phone: null, id_number: '001234567890', avatar_url: 'uploads/avatar/2.jpg', bio: null, description: null } },
    { id: '3', email: 'mod@example.com', role: 'MODERATOR', status: 'ACTIVE', email_verified_at: '2025-02-01T00:00:00Z', created_at: '2025-02-01T09:00:00Z', updated_at: '2025-02-20T00:00:00Z', profile: { nickname: 'Mod Kiểm Duyệt', phone: '0904895575', id_number: null, avatar_url: null, bio: null, description: null }, moderatorCategoryIds: ['cat-1', 'cat-2'] },
    { id: '4', email: 'user3@example.com', role: 'USER', status: 'PENDING', email_verified_at: null, created_at: '2025-02-10T11:00:00Z', updated_at: '2025-02-10T11:00:00Z', profile: { nickname: 'Phạm Thị D', phone: null, id_number: null, avatar_url: null, bio: null, description: null } },
    { id: '5', email: 'banned@example.com', role: 'USER', status: 'BANNED', email_verified_at: '2024-12-02T00:00:00Z', created_at: '2024-12-01T00:00:00Z', updated_at: '2025-01-05T00:00:00Z', profile: { nickname: 'Hoàng Văn E', phone: null, id_number: null, avatar_url: null, bio: null, description: null } },
];

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

    // --- Khi ghép BE: bỏ comment block dưới, xóa phần mock ---
    /*
    const res = await axiosInstance.get('/Admin/users', {
        params: { page, pageSize, search: search || undefined, status: status || undefined, role: role || undefined },
    });
    const data = res.data;
    return {
        items: (data.items ?? data.data ?? []).map(normalizeUser),
        totalCount: data.totalCount ?? data.total ?? 0,
        page: data.page ?? page,
        totalPages: data.totalPages ?? 1,
    };
    */

    // Mock: lọc theo search/status/role rồi phân trang
    let list = [...MOCK_USERS].map(normalizeUser);
    if (search) {
        const q = search.toLowerCase();
        list = list.filter(u => (u.email + ' ' + (u.nickname || '')).toLowerCase().includes(q));
    }
    if (status) list = list.filter(u => u.status === status);
    if (role) list = list.filter(u => u.role === role);
    const totalCount = list.length;
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
    const start = (page - 1) * pageSize;
    const items = list.slice(start, start + pageSize);
    return { items, totalCount, page, totalPages };
}

/**
 * Thống kê nhanh (tổng user, active, inactive, banned, authors)
 * @returns {Promise<{ total: number, active: number, inactive: number, banned: number, authors: number }>}
 */
export async function getStats() {
    // --- Khi ghép BE: gọi GET /Admin/users/stats hoặc tương đương ---
    /*
    const res = await axiosInstance.get('/Admin/users/stats');
    return res.data;
    */

    const list = MOCK_USERS.map(normalizeUser);
    return {
        total: list.length,
        active: list.filter(u => u.status === 'ACTIVE').length,
        inactive: list.filter(u => u.status === 'INACTIVE').length,
        banned: list.filter(u => u.status === 'BANNED').length,
        pending: list.filter(u => u.status === 'PENDING').length,
        authors: list.filter(u => u.role === 'AUTHOR').length,
        moderators: list.filter(u => u.role === 'MODERATOR').length,
    };
}

/**
 * Cập nhật trạng thái user (ACTIVE | INACTIVE | BANNED)
 * @param {string} userId
 * @param {string} status
 */
export async function updateUserStatus(userId, status) {
    // --- Khi ghép BE: gọi PATCH/PUT /Admin/users/:id/status ---
    /*
    await axiosInstance.patch(`/Admin/users/${userId}/status`, { status });
    return { success: true };
    */

    console.log('updateUserStatus (mock):', userId, status);
    return { success: true };
}

/**
 * Cập nhật role user (nếu backend hỗ trợ)
 * @param {string} userId
 * @param {string} role - USER | AUTHOR | ADMIN
 */
export async function updateUserRole(userId, role) {
    /*
    await axiosInstance.patch(`/Admin/users/${userId}/role`, { role });
    return { success: true };
    */
    console.log('updateUserRole (mock):', userId, role);
    return { success: true };
}

/**
 * Gán user làm moderator (kiểm duyệt) theo các thể loại truyện.
 * @param {string} userId
 * @param {string[]} categoryIds - Mảng id thể loại (categories) user được quyền kiểm duyệt
 */
export async function assignModeratorCategories(userId, categoryIds) {
    /*
    await axiosInstance.put(`/Admin/users/${userId}/moderator-categories`, { categoryIds });
    return { success: true };
    */
    console.log('assignModeratorCategories (mock):', userId, categoryIds);
    return { success: true };
}

/**
 * Lấy danh sách thể loại mà moderator được gán (khi BE có API).
 * @param {string} userId
 * @returns {Promise<{ categoryIds: string[] }>}
 */
export async function getModeratorCategories(userId) {
    /*
    const res = await axiosInstance.get(`/Admin/users/${userId}/moderator-categories`);
    return { categoryIds: res.data?.categoryIds ?? res.data?.category_ids ?? [] };
    */
    const mockUser = MOCK_USERS.find(u => (u.id ?? u.Id) === userId);
    return { categoryIds: mockUser?.moderatorCategoryIds ?? [] };
}
