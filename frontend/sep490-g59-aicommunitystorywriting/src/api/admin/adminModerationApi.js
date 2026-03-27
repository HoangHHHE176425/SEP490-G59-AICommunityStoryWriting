import axiosInstance from '../axiosInstance';

/**
 * @param {string} [urgencyTier] - CRITICAL | HIGH | STANDARD
 * @returns {Promise<{ items, counts: { critical, high, standard } }>}
 */
export async function getPendingReviewEscalations(urgencyTier) {
    const q = new URLSearchParams();
    if (urgencyTier) q.append('urgencyTier', urgencyTier);
    const url = `/admin/moderation/review-escalations/pending${q.toString() ? `?${q}` : ''}`;
    const res = await axiosInstance.get(url);
    return res.data;
}

/**
 * @param {string} id - escalation guid
 * @param {{ approve: boolean, adminNote?: string, confirmedDeadlineAt?: string|null, reassignToUserId?: string|null }} body
 */
export async function resolveReviewEscalation(id, body) {
    await axiosInstance.post(`/admin/moderation/review-escalations/${id}/resolve`, body);
}

/**
 * @returns {Promise<{ items: Array<{ id: string, displayName: string, claimedAssignmentCount: number }> }>}
 */
export async function getModeratorsForAssignment() {
    const res = await axiosInstance.get('/admin/moderation/moderators-for-assignment');
    return res.data;
}

/**
 * @param {number} [skip]
 * @param {number} [take]
 */
export async function getReviewEscalationHistory(skip = 0, take = 200) {
    const q = new URLSearchParams();
    q.append('skip', String(skip));
    q.append('take', String(take));
    const res = await axiosInstance.get(`/admin/moderation/review-escalations/history?${q}`);
    return res.data;
}

/**
 * @param {Record<string, string|number|undefined>} params — page, pageSize, search, status, requestKind, targetType, senderId, resolverId, createdFrom, createdTo, resolvedFrom, resolvedTo, sortBy, sortOrder
 */
export async function getReviewEscalationLog(params = {}) {
    const q = new URLSearchParams();
    Object.entries(params).forEach(([k, v]) => {
        if (v !== undefined && v !== null && v !== '') q.append(k, String(v));
    });
    const res = await axiosInstance.get(`/admin/moderation/review-escalations/log?${q}`);
    return res.data;
}

/**
 * Log duyệt/từ chối của moderator.
 * @param {Record<string, string|number|undefined>} params — page, pageSize, search, moderatorId, dateFrom, dateTo, action, targetType, targetId, processingTimeMinMs, processingTimeMaxMs, sortBy, sortOrder
 */
export async function getModerationLogs(params = {}) {
    const q = new URLSearchParams();
    Object.entries(params).forEach(([k, v]) => {
        if (v !== undefined && v !== null && v !== '') q.append(k, String(v));
    });
    const res = await axiosInstance.get(`/admin/moderation/logs?${q}`);
    return res.data;
}
