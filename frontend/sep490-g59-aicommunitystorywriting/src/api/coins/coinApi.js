import axiosInstance from "../axiosInstance";

function getErrorMessage(err) {
  return (
    err?.response?.data?.message ||
    err?.response?.data?.title ||
    err?.message ||
    "Đã xảy ra lỗi. Vui lòng thử lại."
  );
}

export async function getCoinPackages() {
  try {
    const res = await axiosInstance.get("/coins/packages");
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, message: getErrorMessage(err) };
  }
}

export async function getMyWallet() {
  try {
    const res = await axiosInstance.get("/coins/wallet");
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, message: getErrorMessage(err) };
  }
}

export async function getMyCoinOrders({ take = 20 } = {}) {
  try {
    const res = await axiosInstance.get("/coins/orders", { params: { take } });
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, message: getErrorMessage(err) };
  }
}

/**
 * Lịch sử mở khóa chương trả phí của user (trừ tiền).
 * @param {{ page?: number, pageSize?: number }} params
 */
export async function getMyChapterUnlockHistory({ page = 1, pageSize = 20 } = {}) {
  try {
    const res = await axiosInstance.get("/coins/wallet/unlock-history", {
      params: { page, pageSize },
    });
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, message: getErrorMessage(err) };
  }
}

/**
 * Lịch sử donate của user (sender_id = user, trừ tiền).
 * @param {{ page?: number, pageSize?: number }} params
 */
export async function getMyDonateHistory({ page = 1, pageSize = 20 } = {}) {
  try {
    const res = await axiosInstance.get("/coins/wallet/donate-history", {
      params: { page, pageSize },
    });
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, message: getErrorMessage(err) };
  }
}

export async function createPayOSPayment({ packageId, returnUrl, cancelUrl }) {
  try {
    const res = await axiosInstance.post("/coins/payos/create", {
      packageId,
      returnUrl,
      cancelUrl,
    });
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, message: getErrorMessage(err) };
  }
}

export async function syncMyPayOSOrder(orderId) {
  try {
    const res = await axiosInstance.post(`/coins/orders/${orderId}/sync`);
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, message: getErrorMessage(err) };
  }
}

/**
 * Ủng hộ coin cho tác giả.
 * @param {{ authorId: string, amount: number, message?: string }} payload
 */
export async function donateToAuthor({ authorId, amount, message }) {
  if (!authorId) {
    return { success: false, message: 'Thiếu thông tin tác giả để ủng hộ.' };
  }
  if (!amount || amount <= 0) {
    return { success: false, message: 'Số coin ủng hộ phải lớn hơn 0.' };
  }

  try {
    const res = await axiosInstance.post('/coins/donate', {
      authorId,
      amount,
      message,
    });
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, message: getErrorMessage(err) };
  }
}

/**
 * Lịch sử donate nhận + rút tiền của tác giả (author hiện tại).
 * @param {{ page?: number, pageSize?: number }} params
 * @returns {Promise<{ success: boolean, data?: { items, totalCount, page, pageSize }, message?: string }>}
 */
export async function getAuthorActivity({ page = 1, pageSize = 50 } = {}) {
  try {
    const res = await axiosInstance.get('/coins/author/activity', {
      params: { page, pageSize },
    });
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, message: getErrorMessage(err) };
  }
}

/**
 * Lịch sử thu nhập tác giả từ người đọc mở khóa chương trả phí.
 * @param {{ page?: number, pageSize?: number }} params
 */
export async function getAuthorUnlockChapterIncomeHistory({ page = 1, pageSize = 20 } = {}) {
  try {
    const res = await axiosInstance.get('/coins/author/unlock-chapter-income-history', {
      params: { page, pageSize },
    });
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, message: getErrorMessage(err) };
  }
}

/**
 * Tạo yêu cầu rút tiền (tác giả). Trừ coin từ ví khi tạo.
 * @param {{ amountCoins: number, bankInfo?: string }} payload
 */
export async function createWithdrawRequest({ amountCoins, bankInfo } = {}) {
  if (!amountCoins || amountCoins <= 0) {
    return { success: false, message: 'Số coin rút phải lớn hơn 0.' };
  }
  try {
    const res = await axiosInstance.post('/coins/author/withdraw-request', {
      amountCoins,
      bankInfo: bankInfo ?? null,
    });
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, message: getErrorMessage(err) };
  }
}

// ===== Author bank accounts (for payout to author) =====
export async function getAuthorBankAccounts() {
  try {
    const res = await axiosInstance.get('/coins/author/bank-accounts');
    return { success: true, data: res.data?.items ?? [] };
  } catch (err) {
    return { success: false, message: getErrorMessage(err) };
  }
}

export async function upsertAuthorBankAccount({ bankName, bankBin, accountNumber, accountHolderName, branchName, isVerified } = {}) {
  try {
    const res = await axiosInstance.post('/coins/author/bank-accounts', {
      bankName,
      bankBin: bankBin ?? null,
      accountNumber,
      accountHolderName,
      branchName: branchName ?? null,
      isVerified: !!isVerified,
    });
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, message: getErrorMessage(err) };
  }
}

export async function verifyAuthorBankAccount({ isVerified } = {}) {
  try {
    const res = await axiosInstance.post('/coins/author/bank-accounts/verify', { isVerified: !!isVerified });
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, message: getErrorMessage(err) };
  }
}

export async function deleteAuthorBankAccount() {
  try {
    const res = await axiosInstance.delete('/coins/author/bank-accounts');
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, message: getErrorMessage(err) };
  }
}

/**
 * Tác giả hủy yêu cầu rút (khi đang chờ admin xử lý).
 * @param {string} withdrawId
 */
export async function cancelWithdrawRequest(withdrawId) {
  if (!withdrawId) return { success: false, message: 'Thiếu withdrawId.' };
  try {
    const res = await axiosInstance.post(`/coins/author/withdraw/${withdrawId}/cancel`);
    return { success: true, data: res.data };
  } catch (err) {
    return { success: false, message: getErrorMessage(err) };
  }
}

