import axiosInstance from '../axiosInstance';

export async function getStoryReportReasons() {
    const res = await axiosInstance.get('/story-reporting/reasons');
    return res.data;
}

export async function getCommentReportReasons() {
    const res = await axiosInstance.get('/comment-reporting/reasons');
    return res.data;
}

export async function reportStory(storyId, body) {
    const res = await axiosInstance.post(`/stories/${storyId}/reports`, body);
    return res.data;
}

export async function reportStoryComment(storyId, commentId, body) {
    const res = await axiosInstance.post(`/stories/${storyId}/comments/${commentId}/reports`, body);
    return res.data;
}
