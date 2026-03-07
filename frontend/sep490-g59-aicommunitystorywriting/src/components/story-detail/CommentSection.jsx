import { ThumbsUp, Flag } from 'lucide-react';
import { useState } from 'react';

/** Chuẩn hóa 1 comment từ API (id, content, userDisplayName, likesCount, userHasLiked, createdAt, parentId). */
function norm(c) {
    return {
        id: c.id ?? c.Id,
        parentId: c.parentId ?? c.ParentId ?? null,
        userDisplayName: c.userDisplayName ?? c.UserDisplayName ?? 'Ẩn danh',
        content: c.content ?? c.Content ?? '',
        likesCount: c.likesCount ?? c.LikesCount ?? 0,
        userHasLiked: c.userHasLiked ?? c.UserHasLiked ?? false,
        createdAt: c.createdAt ?? c.CreatedAt ?? null,
    };
}

/** Chuyển danh sách phẳng thành cây: top-level (parentId null), mỗi item có replies[]. */
function buildCommentTree(flatList) {
    const list = (flatList ?? []).map(norm);
    const byId = new Map();
    list.forEach((c) => byId.set(c.id, { ...c, replies: [] }));
    const roots = [];
    list.forEach((c) => {
        const node = byId.get(c.id);
        if (!c.parentId) {
            roots.push(node);
        } else {
            const parent = byId.get(c.parentId);
            if (parent) parent.replies.push(node);
            else roots.push(node);
        }
    });
    roots.sort((a, b) => new Date(b.createdAt || 0) - new Date(a.createdAt || 0));
    roots.forEach((r) => r.replies.sort((a, b) => new Date(a.createdAt || 0) - new Date(b.createdAt || 0)));
    return roots;
}

export function CommentSection({
    comments,
    isLoggedIn,
    commentError,
    commentsLoading,
    onSubmitComment,
    onLikeComment,
    onReportComment,
    formatTimeAgo,
}) {
    const [visibleCount, setVisibleCount] = useState(10);
    const [newCommentText, setNewCommentText] = useState('');
    const [submitting, setSubmitting] = useState(false);
    const [replyingTo, setReplyingTo] = useState(null);
    const [replyText, setReplyText] = useState('');

    const tree = buildCommentTree(comments);
    const flatCount = (comments ?? []).length;
    const visibleTree = tree.slice(0, visibleCount);

    const handleSubmitMain = async () => {
        const text = newCommentText.trim();
        if (!text || submitting || !onSubmitComment) return;
        setSubmitting(true);
        try {
            await onSubmitComment(text, null);
            setNewCommentText('');
        } finally {
            setSubmitting(false);
        }
    };

    const handleSubmitReply = async (parentId) => {
        const text = replyText.trim();
        if (!text || submitting || !onSubmitComment) return;
        setSubmitting(true);
        try {
            await onSubmitComment(text, parentId);
            setReplyingTo(null);
            setReplyText('');
        } finally {
            setSubmitting(false);
        }
    };

    const handleLike = (commentId) => {
        if (isLoggedIn && onLikeComment) onLikeComment(commentId);
    };

    function CommentBlock({ node, isReply = false }) {
        const timeStr = node.createdAt ? (formatTimeAgo ? formatTimeAgo(node.createdAt) : new Date(node.createdAt).toLocaleString()) : '';
        return (
            <div className={isReply ? 'ml-10 mt-2' : ''}>
                <div className="flex gap-3">
                    <div className="w-10 h-10 rounded-full bg-primary/20 shrink-0 flex items-center justify-center text-primary font-bold text-sm">
                        {(node.userDisplayName || '?').charAt(0).toUpperCase()}
                    </div>
                    <div className="flex-1 min-w-0">
                        <div className="bg-slate-50 dark:bg-slate-800 rounded-lg p-4">
                            <p className="font-semibold text-slate-900 dark:text-white text-sm mb-1">
                                {node.userDisplayName}
                            </p>
                            <p className="text-slate-600 dark:text-slate-400 text-sm whitespace-pre-wrap">
                                {node.content}
                            </p>
                        </div>
                        <div className="flex items-center gap-4 mt-2 text-xs text-slate-500 dark:text-slate-400">
                            <span>{timeStr}</span>
                            <button
                                type="button"
                                onClick={() => handleLike(node.id)}
                                className={`flex items-center gap-1 transition-colors ${node.userHasLiked ? 'text-primary' : 'hover:text-primary'}`}
                            >
                                <ThumbsUp className={`w-3.5 h-3.5 ${node.userHasLiked ? 'fill-primary' : ''}`} />
                                {node.likesCount}
                            </button>
                            {isLoggedIn && (
                                <button
                                    type="button"
                                    onClick={() => { setReplyingTo(node.id); setReplyText(''); }}
                                    className="hover:text-primary transition-colors"
                                >
                                    Trả lời
                                </button>
                            )}
                            <button
                                type="button"
                                onClick={() => onReportComment?.(node.id)}
                                className="hover:text-red-500 transition-colors"
                            >
                                <Flag className="w-3.5 h-3.5 inline" />
                            </button>
                        </div>
                        {replyingTo === node.id && (
                            <div className="mt-2">
                                <textarea
                                    value={replyText}
                                    onChange={(e) => setReplyText(e.target.value)}
                                    placeholder="Viết trả lời..."
                                    className="w-full p-3 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary/50 resize-none"
                                    rows={2}
                                />
                                <div className="flex justify-end gap-2 mt-2">
                                    <button
                                        type="button"
                                        onClick={() => { setReplyingTo(null); setReplyText(''); }}
                                        className="px-4 py-2 bg-slate-100 dark:bg-slate-800 text-slate-900 dark:text-white text-sm font-bold rounded-full hover:bg-slate-200 dark:hover:bg-slate-700"
                                    >
                                        Hủy
                                    </button>
                                    <button
                                        type="button"
                                        onClick={() => handleSubmitReply(node.id)}
                                        disabled={!replyText.trim() || submitting}
                                        className="px-4 py-2 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 disabled:opacity-50"
                                    >
                                        Gửi trả lời
                                    </button>
                                </div>
                            </div>
                        )}
                        {node.replies?.length > 0 && (
                            <div className="mt-2 space-y-2">
                                {node.replies.map((r) => (
                                    <CommentBlock key={r.id} node={r} isReply />
                                ))}
                            </div>
                        )}
                    </div>
                </div>
            </div>
        );
    }

    return (
        <div className="space-y-4">
            {/* Form gửi bình luận: disable nút khi chưa đăng nhập */}
            <div className="bg-slate-50 dark:bg-slate-800 rounded-lg p-4">
                <textarea
                    value={newCommentText}
                    onChange={(e) => setNewCommentText(e.target.value)}
                    placeholder={isLoggedIn ? 'Viết bình luận của bạn...' : 'Vui lòng đăng nhập để bình luận.'}
                    disabled={!isLoggedIn}
                    className="w-full p-3 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary/50 resize-none disabled:opacity-70 disabled:cursor-not-allowed"
                    rows={3}
                />
                <div className="flex justify-end mt-2">
                    <button
                        type="button"
                        onClick={handleSubmitMain}
                        disabled={!isLoggedIn || !newCommentText.trim() || submitting}
                        className="px-4 py-2 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                    >
                        {submitting ? 'Đang gửi...' : 'Gửi bình luận'}
                    </button>
                </div>
            </div>

            {commentError && (
                <div className="p-3 rounded-lg bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 text-red-700 dark:text-red-300 text-sm">
                    {commentError}
                </div>
            )}

            {/* Danh sách comment */}
            {commentsLoading ? (
                <p className="text-slate-500 dark:text-slate-400 text-sm py-4">Đang tải bình luận...</p>
            ) : visibleTree.length === 0 ? (
                <p className="text-slate-500 dark:text-slate-400 text-sm py-4">Chưa có bình luận nào.</p>
            ) : (
                <>
                    <div className="space-y-4">
                        {visibleTree.map((node) => (
                            <CommentBlock key={node.id} node={node} />
                        ))}
                    </div>
                    {flatCount > visibleCount && (
                        <button
                            type="button"
                            onClick={() => setVisibleCount((n) => n + 10)}
                            className="text-sm text-primary hover:underline"
                        >
                            Xem thêm bình luận ({flatCount - visibleCount})
                        </button>
                    )}
                </>
            )}
        </div>
    );
}
