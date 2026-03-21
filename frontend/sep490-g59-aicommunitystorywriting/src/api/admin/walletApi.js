import axiosInstance from '../axiosInstance';

export async function getSystemWalletBalance() {
    const res = await axiosInstance.get('/admin/wallet/balance');
    return res.data;
}

export async function getAdminWalletSummary() {
    const res = await axiosInstance.get('/admin/wallet/summary');
    return res.data;
}

export async function getTopAuthorsByIncome(params = {}) {
    const res = await axiosInstance.get('/admin/wallet/top-authors', { params });
    return res.data;
}

export async function getTopSpenders(params = {}) {
    const res = await axiosInstance.get('/admin/wallet/top-spenders', { params });
    return res.data;
}

export async function getSystemCoinLedger(params = {}) {
    const res = await axiosInstance.get('/admin/wallet/system-coin-ledger', { params });
    return res.data;
}

