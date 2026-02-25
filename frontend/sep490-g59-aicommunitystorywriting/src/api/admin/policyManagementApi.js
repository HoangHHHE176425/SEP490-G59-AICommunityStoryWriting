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
        is_active: isActive ?? false,
        require_resign: requireResign ?? false,
    };
}

// Mock data (xóa khi ghép BE)
const MOCK_POLICIES = [
    { id: '1', type: 'USER', version: '1.0', content: 'Điều khoản sử dụng cho người dùng...', is_active: true, require_resign: false, created_at: '2025-01-01T00:00:00Z', activated_at: '2025-01-01T00:00:00Z' },
    { id: '2', type: 'AUTHOR', version: '1.0', content: 'Chính sách dành cho tác giả...', is_active: true, require_resign: true, created_at: '2025-01-02T00:00:00Z', activated_at: '2025-01-02T00:00:00Z' },
    { id: '3', type: 'USER', version: '2.0', content: 'Phiên bản cập nhật điều khoản...', is_active: false, require_resign: false, created_at: '2025-02-01T00:00:00Z', activated_at: null },
];

/**
 * Lấy danh sách policy (phân trang, lọc theo type / isActive)
 */
export async function getPolicies(params = {}) {
    const { page = 1, pageSize = 10, type = '', isActive } = params;

    /*
    const res = await axiosInstance.get('/Admin/policies', {
        params: { page, pageSize, type: type || undefined, isActive },
    });
    const data = res.data;
    return {
        items: (data.items ?? data.data ?? []).map(normalizePolicy),
        totalCount: data.totalCount ?? data.total ?? 0,
        page: data.page ?? page,
        totalPages: data.totalPages ?? 1,
    };
    */

    let list = MOCK_POLICIES.map(normalizePolicy);
    if (type) list = list.filter((p) => p.type === type);
    if (isActive !== undefined && isActive !== null) list = list.filter((p) => p.isActive === isActive);
    const totalCount = list.length;
    const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
    const start = (page - 1) * pageSize;
    const items = list.slice(start, start + pageSize);
    return { items, totalCount, page, totalPages };
}

/**
 * Lấy một policy theo id
 */
export async function getPolicyById(id) {
    /*
    const res = await axiosInstance.get(`/Admin/policies/${id}`);
    return normalizePolicy(res.data);
    */
    const p = MOCK_POLICIES.find((x) => (x.id ?? x.Id) === id);
    return p ? normalizePolicy(p) : null;
}

/**
 * Thống kê (tổng, đang active, theo type)
 */
export async function getPolicyStats() {
    /*
    const res = await axiosInstance.get('/Admin/policies/stats');
    return res.data;
    */
    const list = MOCK_POLICIES.map(normalizePolicy);
    const active = list.filter((p) => p.isActive).length;
    const byType = {};
    list.forEach((p) => { byType[p.type] = (byType[p.type] || 0) + 1; });
    return { total: list.length, active, byType };
}

/**
 * Tạo policy mới
 */
export async function createPolicy(payload) {
    /*
    const body = toRequestPayload(payload);
    const res = await axiosInstance.post('/Admin/policies', body);
    return normalizePolicy(res.data);
    */
    const id = String(MOCK_POLICIES.length + 1);
    const now = new Date().toISOString();
    const row = {
        id,
        type: payload.type ?? '',
        version: payload.version ?? '',
        content: payload.content ?? '',
        is_active: payload.isActive ?? false,
        require_resign: payload.requireResign ?? false,
        created_at: now,
        activated_at: payload.isActive ? now : null,
    };
    MOCK_POLICIES.push(row);
    return normalizePolicy(row);
}

/**
 * Cập nhật policy
 */
export async function updatePolicy(id, payload) {
    /*
    const body = toRequestPayload(payload);
    const res = await axiosInstance.put(`/Admin/policies/${id}`, body);
    return normalizePolicy(res.data);
    */
    const idx = MOCK_POLICIES.findIndex((x) => (x.id ?? x.Id) === id);
    if (idx === -1) throw new Error('Policy not found');
    const p = MOCK_POLICIES[idx];
    p.type = payload.type ?? p.type;
    p.version = payload.version ?? p.version;
    p.content = payload.content ?? p.content;
    p.is_active = payload.isActive !== undefined ? payload.isActive : p.is_active;
    p.require_resign = payload.requireResign !== undefined ? payload.requireResign : p.require_resign;
    if (payload.isActive && !p.activated_at) p.activated_at = new Date().toISOString();
    return normalizePolicy(p);
}

/**
 * Bật/tắt trạng thái active
 */
export async function setPolicyActive(id, isActive) {
    /*
    const res = await axiosInstance.patch(`/Admin/policies/${id}/active`, { isActive });
    return normalizePolicy(res.data);
    */
    const policy = await getPolicyById(id);
    if (!policy) throw new Error('Policy not found');
    return updatePolicy(id, { ...policy, isActive });
}
