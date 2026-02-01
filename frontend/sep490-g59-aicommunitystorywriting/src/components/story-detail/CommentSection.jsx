import { ThumbsUp, Flag } from 'lucide-react';
import { useState } from 'react';

export function CommentSection({ comments, onReportComment }) {
    const [visibleComments, setVisibleComments] = useState(5);
    const [likedComments, setLikedComments] = useState(new Set());
    const [replyingTo, setReplyingTo] = useState(null);
    const [replyText, setReplyText] = useState('');

    const handleToggleLike = (commentId) => {
        setLikedComments((prev) => {
            const newSet = new Set(prev);
            if (newSet.has(commentId)) {
                newSet.delete(commentId);
            } else {
                newSet.add(commentId);
            }
            return newSet;
        });
    };

    const handleSubmitReply = () => {
        // Handle submit reply
        setReplyingTo(null);
        setReplyText('');
    };

    return (
        <div className="space-y-4">
            {/* Comment Form */}
            <div className="bg-slate-50 dark:bg-slate-800 rounded-lg p-4">
                <textarea
                    placeholder="Viết bình luận của bạn..."
                    className="w-full p-3 bg-white dark:bg-slate-900 border border-slate-200 dark:border-slate-700 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary/50 resize-none"
                    rows={3}
                />
                <div className="flex justify-end mt-2">
                    <button className="px-4 py-2 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all">
                        Gửi bình luận
                    </button>
                </div>
            </div>

            {/* Comments List */}
            {comments.slice(0, visibleComments).map((comment) => (
                <div key={comment.id} className="flex gap-3">
                    <img
                        src={comment.user.avatar}
                        alt={comment.user.name}
                        className="w-10 h-10 rounded-full shrink-0"
                    />
                    <div className="flex-1">
                        <div className="bg-slate-50 dark:bg-slate-800 rounded-lg p-4">
                            <p className="font-semibold text-slate-900 dark:text-white text-sm mb-1">
                                {comment.user.name}
                            </p>
                            <p className="text-slate-600 dark:text-slate-400 text-sm">
                                {comment.content}
                            </p>
                        </div>
                        <div className="flex items-center gap-4 mt-2 text-xs text-slate-500 dark:text-slate-400">
                            <span>{comment.time}</span>
                            <button
                                onClick={() => handleToggleLike(comment.id)}
                                className={`flex items-center gap-1 hover:text-primary transition-colors ${likedComments.has(comment.id) ? 'text-primary' : ''
                                    }`}
                            >
                                <ThumbsUp
                                    className={`w-3 h-3 ${likedComments.has(comment.id) ? 'fill-primary' : ''
                                        }`}
                                />
                                {comment.likes}
                            </button>
                            <button
                                onClick={() => setReplyingTo(comment.id)}
                                className="hover:text-primary transition-colors"
                            >
                                Trả lời
                            </button>
                            <button
                                onClick={() => onReportComment(comment.id)}
                                className="hover:text-red-500 transition-colors"
                            >
                                <Flag className="w-3 h-3" />
                            </button>
                        </div>
                        {replyingTo === comment.id && (
                            <div className="mt-2">
                                <textarea
                                    value={replyText}
                                    onChange={(e) => setReplyText(e.target.value)}
                                    placeholder="Viết trả lời của bạn..."
                                    className="w-full p-3 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary/50 resize-none"
                                    rows={2}
                                />
                                <div className="flex justify-end gap-2 mt-2">
                                    <button
                                        onClick={() => {
                                            setReplyingTo(null);
                                            setReplyText('');
                                        }}
                                        className="px-4 py-2 bg-slate-100 dark:bg-slate-800 text-slate-900 dark:text-white text-sm font-bold rounded-full hover:bg-slate-200 dark:hover:bg-slate-700 transition-all"
                                    >
                                        Hủy
                                    </button>
                                    <button
                                        onClick={handleSubmitReply}
                                        className="px-4 py-2 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all"
                                    >
                                        Gửi trả lời
                                    </button>
                                </div>
                            </div>
                        )}
                    </div>
                </div>
            ))}

            {visibleComments < comments.length && (
                <button
                    onClick={() => setVisibleComments(visibleComments + 5)}
                    className="text-sm text-primary hover:underline transition-colors"
                >
                    Xem thêm bình luận ({comments.length - visibleComments} bình luận)
                </button>
            )}
        </div>
    );
}
