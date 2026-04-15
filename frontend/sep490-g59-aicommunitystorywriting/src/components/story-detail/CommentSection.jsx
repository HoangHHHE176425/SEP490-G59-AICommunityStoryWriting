import { ThumbsUp, Flag } from 'lucide-react';
import { useState, useEffect } from 'react';
import { getCommentRoleBadge } from '../../utils/commentRoleBadge';

/** Chuẩn hóa 1 comment từ API (id, content, userDisplayName, userRole, userCreatedAt, …). */
function norm(c) {
    return {
        id: c.id ?? c.Id,
        parentId: c.parentId ?? c.ParentId ?? null,
        userDisplayName: c.userDisplayName ?? c.UserDisplayName ?? 'Ẩn danh',
        userRole: c.userRole ?? c.UserRole ?? null,
        userCreatedAt: c.userCreatedAt ?? c.UserCreatedAt ?? null,
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
    roots.forEach((r) => r.replies.sort((a, b) => new Date(b.createdAt || 0) - new Date(a.createdAt || 0)));
    return roots;
}

const INITIAL_COMMENTS = 3;
const INITIAL_REPLIES = 3;
const LOAD_MORE_STEP = 3;

/** Block 1 comment + form trả lời. Định nghĩa ngoài CommentSection để không bị tạo lại mỗi lần render → tránh textarea reply mất focus khi gõ. */
function CommentBlock({
    node,
    isReply = false,
    replyingTo,
    onSetReplyingTo,
    replyText,
    onReplyTextChange,
    onSubmitReply,
    submitting,
    onLike,
    isLoggedIn,
    canPost,
    onReportComment,
    formatTimeAgo,
    visibleRepliesCount,
    onShowMoreReplies,
    onHideReplies,
}) {
    const timeStr = node.createdAt ? (formatTimeAgo ? formatTimeAgo(node.createdAt) : new Date(node.createdAt).toLocaleString()) : '';
    const roleBadge = getCommentRoleBadge(node.userRole, node.userCreatedAt);
    return (
        <div id={node.id ? `comment-${node.id}` : undefined} className={isReply ? 'ml-10 mt-2' : ''}>
            <div className="flex gap-3">
                <div className="w-10 h-10 rounded-full bg-primary/20 shrink-0 flex items-center justify-center text-primary font-bold text-sm">
                    {(node.userDisplayName || '?').charAt(0).toUpperCase()}
                </div>
                <div className="flex-1 min-w-0">
                    <div className="bg-slate-50 dark:bg-slate-800 rounded-lg p-4">
                        <div className="flex flex-wrap items-center gap-2 mb-1">
                            <span className="font-semibold text-slate-900 dark:text-white text-sm">
                                {node.userDisplayName}
                            </span>
                            {roleBadge ? (
                                <span
                                    className={`text-[10px] sm:text-xs font-bold px-2 py-0.5 rounded-full shrink-0 ${roleBadge.className}`}
                                >
                                    {roleBadge.label}
                                </span>
                            ) : null}
                        </div>
                        <p className="text-slate-600 dark:text-slate-400 text-sm whitespace-pre-wrap">
                            {node.content}
                        </p>
                    </div>
                    <div className="flex items-center gap-4 mt-2 text-xs text-slate-500 dark:text-slate-400">
                        <span>{timeStr}</span>
                        <button
                            type="button"
                            onClick={() => onLike(node.id)}
                            className={`flex items-center gap-1 transition-colors ${node.userHasLiked ? 'text-primary' : 'hover:text-primary'}`}
                        >
                            <ThumbsUp className={`w-3.5 h-3.5 ${node.userHasLiked ? 'fill-primary' : ''}`} />
                            {node.likesCount}
                        </button>
                        {canPost && (
                            <button
                                type="button"
                                onClick={() => { onSetReplyingTo(node.id); onReplyTextChange(''); }}
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
                    {replyingTo === node.id && canPost && (
                        <div className="mt-2">
                            <textarea
                                value={replyText}
                                onChange={(e) => onReplyTextChange(e.target.value)}
                                placeholder="Viết trả lời..."
                                className="w-full p-3 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary/50 resize-none"
                                rows={2}
                            />
                            <div className="flex justify-end gap-2 mt-2">
                                <button
                                    type="button"
                                    onClick={() => { onSetReplyingTo(null); onReplyTextChange(''); }}
                                    className="px-4 py-2 bg-slate-100 dark:bg-slate-800 text-slate-900 dark:text-white text-sm font-bold rounded-full hover:bg-slate-200 dark:hover:bg-slate-700"
                                >
                                    Hủy
                                </button>
                                <button
                                    type="button"
                                    onClick={() => onSubmitReply(node.id)}
                                    disabled={!replyText.trim() || submitting}
                                    className="px-4 py-2 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 disabled:opacity-50"
                                >
                                    Gửi trả lời
                                </button>
                            </div>
                        </div>
                    )}
                    {node.replies?.length > 0 && (() => {
                        const limit = visibleRepliesCount ?? node.replies.length;
                        const visibleReplies = node.replies.slice(0, limit);
                        const remaining = node.replies.length - limit;
                        return (
                            <div className="mt-2 space-y-2">
                                {visibleReplies.map((r) => (
                                    <CommentBlock
                                        key={r.id}
                                        node={r}
                                        isReply
                                        replyingTo={replyingTo}
                                        onSetReplyingTo={onSetReplyingTo}
                                        replyText={replyText}
                                        onReplyTextChange={onReplyTextChange}
                                        onSubmitReply={onSubmitReply}
                                        submitting={submitting}
                                        onLike={onLike}
                                        isLoggedIn={isLoggedIn}
                                        canPost={canPost}
                                        onReportComment={onReportComment}
                                        formatTimeAgo={formatTimeAgo}
                                    />
                                ))}
                                {remaining > 0 && onShowMoreReplies && (
                                    <button
                                        type="button"
                                        onClick={() => onShowMoreReplies(node.id)}
                                        className="text-sm text-primary hover:underline ml-10"
                                    >
                                        Xem thêm trả lời ({remaining})
                                    </button>
                                )}
                                {limit > INITIAL_REPLIES && onHideReplies && (
                                    <button
                                        type="button"
                                        onClick={() => onHideReplies(node.id)}
                                        className="text-sm text-slate-500 dark:text-slate-400 hover:underline ml-10"
                                    >
                                        Ẩn bớt trả lời
                                    </button>
                                )}
                            </div>
                        );
                    })()}
                </div>
            </div>
        </div>
    );
}

export function CommentSection({
    comments,
    isLoggedIn,
    commentsDisabled = false,
    commentError,
    commentsLoading,
    onSubmitComment,
    onLikeComment,
    onReportComment,
    formatTimeAgo,
}) {
    const [visibleCount, setVisibleCount] = useState(INITIAL_COMMENTS);
    const [visibleRepliesByParent, setVisibleRepliesByParent] = useState({});
    const [newCommentText, setNewCommentText] = useState('');
    const [submitting, setSubmitting] = useState(false);
    const [replyingTo, setReplyingTo] = useState(null);
    const [replyText, setReplyText] = useState('');

    useEffect(() => {
        if (!commentsDisabled) return;
        setReplyingTo(null);
        setReplyText('');
    }, [commentsDisabled]);

    const tree = buildCommentTree(comments);
    const visibleTree = tree.slice(0, visibleCount);
    const canPost = Boolean(isLoggedIn) && !commentsDisabled;

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

    const showMoreReplies = (parentId) => {
        setVisibleRepliesByParent((prev) => ({ ...prev, [parentId]: (prev[parentId] ?? INITIAL_REPLIES) + LOAD_MORE_STEP }));
    };

    const hideReplies = (parentId) => {
        setVisibleRepliesByParent((prev) => ({ ...prev, [parentId]: Math.max(INITIAL_REPLIES, (prev[parentId] ?? INITIAL_REPLIES) - LOAD_MORE_STEP) }));
    };

    const commentBlockProps = {
        replyingTo,
        onSetReplyingTo: setReplyingTo,
        replyText,
        onReplyTextChange: setReplyText,
        onSubmitReply: handleSubmitReply,
        submitting,
        onLike: handleLike,
        isLoggedIn,
        canPost,
        onReportComment,
        formatTimeAgo,
        onShowMoreReplies: showMoreReplies,
        onHideReplies: hideReplies,
    };

    const mainPlaceholder = !isLoggedIn
        ? 'Vui lòng đăng nhập để bình luận.'
        : commentsDisabled
            ? 'Bình luận đang bị khóa cho truyện này.'
            : 'Viết bình luận của bạn...';

    return (
        <div className="space-y-4">
            {/* Form gửi bình luận: disable nút khi chưa đăng nhập */}
            <div className="bg-slate-50 dark:bg-slate-800 rounded-lg p-4">
                {commentsDisabled ? (
                    <p className="text-sm text-amber-800 dark:text-amber-200 bg-amber-50 dark:bg-amber-950/40 border border-amber-200 dark:border-amber-800 rounded-lg px-3 py-2 mb-3">
                        Truyện này đang trong quá trình xử lý vi phạm nên hiện không thể bình luận.
                    </p>
                ) : null}
                <textarea
                    value={newCommentText}
                    onChange={(e) => setNewCommentText(e.target.value)}
                    placeholder={mainPlaceholder}
                    disabled={!canPost}
                    className="w-full p-3 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary/50 resize-none disabled:opacity-70 disabled:cursor-not-allowed"
                    rows={3}
                />
                <div className="flex justify-end mt-2">
                    <button
                        type="button"
                        onClick={handleSubmitMain}
                        disabled={!canPost || !newCommentText.trim() || submitting}
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
                            <CommentBlock
                                key={node.id}
                                node={node}
                                visibleRepliesCount={visibleRepliesByParent[node.id] ?? INITIAL_REPLIES}
                                {...commentBlockProps}
                            />
                        ))}
                    </div>
                    <div className="flex flex-wrap gap-3 mt-2">
                        {tree.length > visibleCount && (
                            <button
                                type="button"
                                onClick={() => setVisibleCount((n) => n + LOAD_MORE_STEP)}
                                className="text-sm text-primary hover:underline"
                            >
                                Xem thêm bình luận ({tree.length - visibleCount})
                            </button>
                        )}
                        {visibleCount > INITIAL_COMMENTS && (
                            <button
                                type="button"
                                onClick={() => setVisibleCount((n) => Math.max(INITIAL_COMMENTS, n - LOAD_MORE_STEP))}
                                className="text-sm text-slate-500 dark:text-slate-400 hover:underline"
                            >
                                Ẩn bớt bình luận
                            </button>
                        )}
                    </div>
                </>
            )}
        </div>
    );
}
