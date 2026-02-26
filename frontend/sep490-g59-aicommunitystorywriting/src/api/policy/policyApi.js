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

