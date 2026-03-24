import axiosInstance from '../axiosInstance';

function toQuery(params = {}) {
    const q = new URLSearchParams();
    Object.entries(params).forEach(([k, v]) => {
        if (v !== undefined && v !== null && v !== '') q.append(k, String(v));
    });
    return q.toString();
}

export async function getStoryReportingReasons() {
    const res = await axiosInstance.get('/story-reporting/reasons');
    return res.data;
}

export async function getCommentReportingReasons() {
    const res = await axiosInstance.get('/comment-reporting/reasons');
    return res.data;
}

export async function getComplianceStoryReports(params = {}) {
    const query = toQuery(params);
    const res = await axiosInstance.get(`/compliance/story-reports${query ? `?${query}` : ''}`);
    return res.data;
}

export async function claimComplianceStoryReports(storyId, body) {
    const payload = body && Object.keys(body).length > 0 ? body : undefined;
    const res = await axiosInstance.post(`/compliance/story-reports/stories/${storyId}/claim`, payload);
    return res.data;
}

export async function releaseComplianceStoryClaim(storyId) {
    const res = await axiosInstance.post(`/compliance/story-reports/stories/${storyId}/release-claim`);
    return res.data;
}

export async function requestComplianceStoryRelease(storyId, body) {
    const res = await axiosInstance.post(`/compliance/story-reports/stories/${storyId}/request-release`, body);
    return res.data;
}

export async function resolveComplianceStoryReport(reportId, body) {
    const res = await axiosInstance.post(`/compliance/story-reports/${reportId}/resolve`, body);
    return res.data;
}

export async function resolveAllOpenComplianceStoryReports(storyId, body) {
    const res = await axiosInstance.post(`/compliance/story-reports/stories/${storyId}/resolve-all-open`, body);
    return res.data;
}

export async function setComplianceStoryFlag(storyId, body) {
    const res = await axiosInstance.post(`/compliance/story-reports/stories/${storyId}/flag`, body);
    return res.data;
}

export async function setComplianceStoryCommentsDisabled(storyId, body) {
    const res = await axiosInstance.post(`/compliance/story-reports/stories/${storyId}/comments-disabled`, body);
    return res.data;
}

export async function setComplianceStoryHidden(storyId, body) {
    const res = await axiosInstance.post(`/compliance/story-reports/stories/${storyId}/compliance-hidden`, body);
    return res.data;
}

export async function requestComplianceStoryAdminAction(storyId, body) {
    const res = await axiosInstance.post(`/compliance/story-reports/stories/${storyId}/admin-action-requests`, body);
    return res.data;
}

export async function getAdminComplianceLockRequests(params = {}) {
    const query = toQuery(params);
    const res = await axiosInstance.get(`/admin/compliance-story-reports/lock-requests${query ? `?${query}` : ''}`);
    return res.data;
}

export async function getAdminComplianceOfficers() {
    const res = await axiosInstance.get('/admin/compliance-story-reports/compliance-officers');
    return res.data;
}

export async function resolveAdminComplianceLockRequest(requestId, body) {
    const res = await axiosInstance.post(`/admin/compliance-story-reports/lock-requests/${requestId}/resolve`, body);
    return res.data;
}

export async function adminReleaseComplianceStoryClaim(storyId) {
    const res = await axiosInstance.post(`/admin/compliance-story-reports/stories/${storyId}/release-claim`);
    return res.data;
}

export async function getAdminComplianceAdminActionRequests(params = {}) {
    const query = toQuery(params);
    const res = await axiosInstance.get(`/admin/compliance-story-reports/admin-action-requests${query ? `?${query}` : ''}`);
    return res.data;
}

export async function resolveAdminComplianceAdminActionRequest(requestId, body) {
    const res = await axiosInstance.post(`/admin/compliance-story-reports/admin-action-requests/${requestId}/resolve`, body);
    return res.data;
}

export async function getComplianceCommentReports(params = {}) {
    const query = toQuery(params);
    const res = await axiosInstance.get(`/compliance/comment-reports${query ? `?${query}` : ''}`);
    return res.data;
}

export async function claimComplianceCommentReports(commentId) {
    const res = await axiosInstance.post(`/compliance/comment-reports/comments/${commentId}/claim`);
    return res.data;
}

export async function resolveComplianceCommentReport(reportId, body) {
    const res = await axiosInstance.post(`/compliance/comment-reports/${reportId}/resolve`, body);
    return res.data;
}

export async function resolveAllOpenComplianceCommentReports(commentId, body) {
    const res = await axiosInstance.post(`/compliance/comment-reports/comments/${commentId}/resolve-all-open`, body);
    return res.data;
}

export async function setComplianceCommentThreadHidden(commentId, body) {
    const res = await axiosInstance.post(`/compliance/comment-reports/comments/${commentId}/hidden`, body);
    return res.data;
}

export async function requestComplianceCommentAdminAction(commentId, body) {
    const res = await axiosInstance.post(`/compliance/comment-reports/comments/${commentId}/admin-action-requests`, body);
    return res.data;
}

export async function adminReleaseComplianceCommentClaim(commentId) {
    const res = await axiosInstance.post(`/admin/compliance-comment-reports/comments/${commentId}/release-claim`);
    return res.data;
}

export async function getAdminComplianceLogs(params = {}) {
    const query = toQuery(params);
    const res = await axiosInstance.get(`/admin/compliance-story-reports/compliance-logs${query ? `?${query}` : ''}`);
    return res.data;
}

export async function getAdminCompliancePerformance(params = {}) {
    const query = toQuery(params);
    const res = await axiosInstance.get(`/admin/compliance-story-reports/compliance-performance${query ? `?${query}` : ''}`);
    return res.data;
}
