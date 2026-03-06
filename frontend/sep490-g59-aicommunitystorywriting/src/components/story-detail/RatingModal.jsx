import { Star, X } from 'lucide-react';
import { useState } from 'react';

export function RatingModal({ isOpen, onClose, onSubmit }) {
    const [userRating, setUserRating] = useState(0);
    const [hoverRating, setHoverRating] = useState(0);
    const [reviewContent, setReviewContent] = useState('');

    if (!isOpen) return null;

    const handleSubmit = () => {
        if (userRating > 0) {
            onSubmit(userRating, reviewContent);
            setUserRating(0);
            setReviewContent('');
            onClose();
        }
    };

    return (
        <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
            <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-2xl max-w-md w-full border border-slate-200 dark:border-slate-800">
                <div className="flex items-center justify-between p-6 border-b border-slate-200 dark:border-slate-800">
                    <h3 className="text-xl font-bold text-slate-900 dark:text-white">Đánh giá truyện</h3>
                    <button
                        onClick={onClose}
                        className="p-2 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-full transition-colors"
                    >
                        <X className="w-5 h-5" />
                    </button>
                </div>

                <div className="p-6">
                    <div className="text-center mb-6">
                        <p className="text-sm text-slate-600 dark:text-slate-400 mb-4">
                            Đánh giá của bạn về truyện này
                        </p>
                        <div className="flex items-center justify-center gap-2">
                            {[1, 2, 3, 4, 5].map((star) => (
                                <button
                                    key={star}
                                    onClick={() => setUserRating(star)}
                                    onMouseEnter={() => setHoverRating(star)}
                                    onMouseLeave={() => setHoverRating(0)}
                                    className="transition-transform hover:scale-110"
                                >
                                    <Star
                                        className={`w-10 h-10 transition-colors ${star <= (hoverRating || userRating)
                                            ? 'fill-amber-400 text-amber-400'
                                            : 'text-slate-300 dark:text-slate-600'
                                            }`}
                                    />
                                </button>
                            ))}
                        </div>
                        {userRating > 0 && (
                            <p className="text-lg font-bold text-amber-600 dark:text-amber-400 mt-2">
                                {userRating} / 5 sao
                            </p>
                        )}
                    </div>

                    <div className="mb-6">
                        <label className="block text-sm font-semibold text-slate-900 dark:text-white mb-2">
                            Nội dung đánh giá (tùy chọn)
                        </label>
                        <textarea
                            value={reviewContent}
                            onChange={(e) => setReviewContent(e.target.value)}
                            placeholder="Chia sẻ cảm nhận của bạn về truyện..."
                            className="w-full p-3 bg-slate-50 dark:bg-slate-800 border border-slate-200 dark:border-slate-700 rounded-lg text-sm outline-none focus:ring-2 focus:ring-primary/50 resize-none"
                            rows={4}
                        />
                    </div>

                    <div className="flex gap-3">
                        <button
                            onClick={onClose}
                            className="flex-1 px-4 py-2.5 bg-slate-100 dark:bg-slate-800 text-slate-900 dark:text-white text-sm font-bold rounded-full hover:bg-slate-200 dark:hover:bg-slate-700 transition-all"
                        >
                            Hủy
                        </button>
                        <button
                            onClick={handleSubmit}
                            disabled={userRating === 0}
                            className="flex-1 px-4 py-2.5 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all disabled:opacity-50 disabled:cursor-not-allowed"
                        >
                            Gửi đánh giá
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}
