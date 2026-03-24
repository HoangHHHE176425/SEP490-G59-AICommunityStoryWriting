import axiosInstance from '../axiosInstance';

export async function getAdminTransactions(params = {}) {
    const res = await axiosInstance.get('/admin/transactions', { params });
    const data = res.data ?? {};
    return {
        items: data.items ?? [],
        totalCount: data.totalCount ?? 0,
        page: data.page ?? 1,
        pageSize: data.pageSize ?? 20,
        totalPages: data.totalPages ?? 1,
    };
}

export async function approveWithdraw(withdrawId, adminNote) {
    const res = await axiosInstance.post(`/admin/transactions/withdraw/${withdrawId}/approve`, {
        adminNote: adminNote || null,
    });
    return res.data;
}

export async function rejectWithdraw(withdrawId, adminNote) {
    const res = await axiosInstance.post(`/admin/transactions/withdraw/${withdrawId}/reject`, {
        adminNote: adminNote || null,
    });
    return res.data;
}

