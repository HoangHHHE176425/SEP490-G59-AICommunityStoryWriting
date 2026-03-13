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

