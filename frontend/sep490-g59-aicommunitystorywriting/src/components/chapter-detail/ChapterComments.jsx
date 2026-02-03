import { MessageSquare } from 'lucide-react';
import { CommentSection } from '../story-detail/CommentSection';

export function ChapterComments({ comments, onReportComment }) {
    return (
        <div style={{ maxWidth: '800px', margin: '0 auto', padding: '0 1.5rem 3rem' }}>
            <div style={{ backgroundColor: '#ffffff', borderRadius: '1rem', padding: '2rem', boxShadow: '0 1px 3px rgba(0, 0, 0, 0.1)' }}>
                <h3 style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#1e293b', marginBottom: '1.5rem', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                    <MessageSquare style={{ width: '24px', height: '24px', color: '#13ec5b' }} />
                    Bình luận ({comments.length})
                </h3>
                <CommentSection
                    comments={comments}
                    onReportComment={onReportComment}
                />
            </div>
        </div>
    );
}
