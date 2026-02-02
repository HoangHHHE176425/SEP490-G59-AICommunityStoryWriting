import { CommentSection } from '../../components/story-detail/CommentSection';
import { MessageSquare } from 'lucide-react';

export function StoryCommentsViewer({ story, comments, onBack }) {
    return (
        <div style={{ minHeight: '100vh', backgroundColor: '#f5f5f5', padding: '2rem' }}>
            <div style={{ maxWidth: '1000px', margin: '0 auto' }}>
                {/* Header */}
                <div style={{ marginBottom: '2rem' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', marginBottom: '0.5rem' }}>
                        <MessageSquare style={{ width: '24px', height: '24px', color: '#13ec5b' }} />
                        <h2 style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#333333', margin: 0 }}>
                            Bình luận - Truyện "{story?.title || 'Untitled'}"
                        </h2>
                    </div>
                    <p style={{ fontSize: '0.875rem', color: '#6b7280', marginLeft: '2.5rem' }}>
                        Tổng số: {comments?.length || 0} bình luận
                    </p>
                </div>

                {/* Comments Section */}
                <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '2rem', border: '1px solid #e0e0e0' }}>
                    <CommentSection
                        comments={comments || []}
                        onReportComment={(id) => console.log('Report comment:', id)}
                    />
                </div>

                {/* Back Button */}
                <div style={{ marginTop: '2rem' }}>
                    <button
                        onClick={onBack}
                        className="px-6 py-2.5 bg-slate-100 text-slate-900 text-sm font-bold rounded-full hover:bg-slate-200 transition-all"
                    >
                        Quay lại
                    </button>
                </div>
            </div>
        </div>
    );
}