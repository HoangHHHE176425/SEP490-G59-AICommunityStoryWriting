/**
 * API quản lý Policy (Admin).
 * Khớp bảng DB: system_policies (id, type, version, content, is_active, require_resign, created_at, activated_at).
 * Response từ BE có thể dùng snake_case hoặc camelCase; normalize về camelCase cho FE.
 */

import axiosInstance from '../axiosInstance';

/** Chuẩn hóa 1 bản ghi từ API/DB (nhận snake_case hoặc camelCase) → camelCase cho FE */
function normalizePolicy(item) {
    if (!item) return null;
    return {
        id: item.id ?? item.Id ?? '',
        type: item.type ?? item.Type ?? '',
        version: item.version ?? item.Version ?? '',
        content: item.content ?? item.Content ?? '',
        isActive: item.isActive ?? item.IsActive ?? (item.is_active ?? false),
        requireResign: item.requireResign ?? item.RequireResign ?? (item.require_resign ?? false),
        createdAt: item.createdAt ?? item.CreatedAt ?? item.created_at ?? null,
        activatedAt: item.activatedAt ?? item.ActivatedAt ?? item.activated_at ?? null,
    };
}

/** Payload gửi BE khi tạo/sửa — khớp DB (có thể gửi snake_case nếu BE nhận đúng từ DB) */
function toRequestPayload({ type, version, content, isActive, requireResign }) {
    return {
        type: type ?? '',
        version: version ?? '',
        content: content ?? '',
        isActive: isActive ?? false,
        requireResign: requireResign ?? false,
    };
}

/**
 * Lấy danh sách policy (phân trang, lọc theo type / isActive)
 */
export async function getPolicies(params = {}) {
    const { page = 1, pageSize = 10, type = '', isActive } = params;

    const res = await axiosInstance.get('/admin/policies', {
        params: { page, pageSize, type: type || undefined, isActive },
    });
    const data = res.data ?? {};
    const totalCount = data.totalCount ?? data.total ?? 0;
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
    return {
        items: (data.items ?? data.data ?? []).map(normalizePolicy),
        totalCount,
        page: data.page ?? page,
        totalPages,
    };
}

/**
 * Lấy một policy theo id
 */
export async function getPolicyById(id) {
    const res = await axiosInstance.get(`/admin/policies/${id}`);
    return normalizePolicy(res.data);
}

/**
 * Thống kê (tổng, đang active, theo type)
 */
export async function getPolicyStats() {
    const res = await axiosInstance.get('/admin/policies/stats');
    return res.data;
}

/**
 * Tạo policy mới
 */
export async function createPolicy(payload) {
    const body = toRequestPayload(payload);
    const res = await axiosInstance.post('/admin/policies', body);
    return normalizePolicy(res.data);
}

/**
 * Cập nhật policy
 */
export async function updatePolicy(id, payload) {
    const body = toRequestPayload(payload);
    await axiosInstance.put(`/admin/policies/${id}`, body);
    return true;
}

/**
 * Bật/tắt trạng thái active
 */
export async function setPolicyActive(id, isActive) {
    if (isActive) {
        await axiosInstance.post(`/admin/policies/${id}/activate`);
    } else {
        await axiosInstance.post(`/admin/policies/${id}/deactivate`);
    }
    return true;
}
