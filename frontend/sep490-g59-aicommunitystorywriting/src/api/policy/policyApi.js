import axiosInstance from '../axiosInstance';

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

export async function getActivePolicy(type) {
  try {
    const res = await axiosInstance.get('/policies/active', {
      params: { type },
    });
    return normalizePolicy(res.data);
  } catch (err) {
    // If backend returns 404 (no active policy), treat as "no policy" instead of hard error.
    if (err?.response?.status === 404) return null;
    throw err;
  }
}

export async function getMyAuthorPolicyStatus(type = 'AUTHOR') {
  try {
    const res = await axiosInstance.get('/policies/me/author-status', {
      params: { type },
    });
    return {
      policy: normalizePolicy(res?.data?.policy ?? res?.data?.Policy),
      hasAccepted: res?.data?.hasAccepted ?? res?.data?.HasAccepted ?? false,
      acceptedAt: res?.data?.acceptedAt ?? res?.data?.AcceptedAt ?? null,
    };
  } catch (err) {
    if (err?.response?.status === 404 || err?.response?.status === 403) return null;
    throw err;
  }
}

export async function acceptAuthorPolicy(policyId) {
  if (!policyId) {
    return { success: false, message: 'policyId là bắt buộc.' };
  }

  try {
    const res = await axiosInstance.post(`/policies/${policyId}/accept-author`);
    return {
      success: true,
      data: {
        accepted: res?.data?.accepted ?? res?.data?.Accepted ?? false,
        alreadyAccepted: res?.data?.alreadyAccepted ?? res?.data?.AlreadyAccepted ?? false,
      },
    };
  } catch (err) {
    return {
      success: false,
      message: err?.response?.data?.message || err?.message || 'Không thể chấp nhận điều khoản tác giả.',
    };
  }
}

