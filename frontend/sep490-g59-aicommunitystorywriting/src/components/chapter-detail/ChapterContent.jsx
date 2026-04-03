import { Lock } from 'lucide-react';
import { sanitizeRichTextHtml } from '../../utils/richText';

export function ChapterContent({
    chapter,
    fontSize,
    fontFamily,
    backgroundColor,
    textColor,
    lineHeight,
    onPayClick,
    isUnlocking = false,
}) {
    const isPaidLocked = chapter?.isPaidLocked === true;
    const coinPrice = Number(chapter?.coinPrice ?? 0) || 0;
    const isRichText = /<[a-z][\s\S]*>/i.test(String(chapter.content || ''));

    return (
        <div style={{ maxWidth: '1600px', margin: '0 auto', padding: '2rem 1.5rem' }}>
            {/* Chapter Header */}
            <div style={{ textAlign: 'center', marginBottom: '3rem' }}>
                <h2 style={{ fontSize: '1.875rem', fontWeight: 'bold', color: '#1e293b', margin: 0 }}>
                    Chương {chapter.number}: {chapter.title}
                </h2>
                {!isPaidLocked && (
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '1rem', marginTop: '1rem', fontSize: '0.875rem', color: '#64748b' }}>
                        <span>{chapter.publishedAt}</span>
                        <span>•</span>
                        <span>{(Number(chapter.commentCount ?? 0) || 0).toLocaleString()} bình luận</span>
                        <span>•</span>
                        <span>{chapter.words.toLocaleString()} từ</span>
                    </div>
                )}
            </div>

            {/* Paid chapter: show payment section instead of content */}
            {isPaidLocked ? (
                <div
                    style={{
                        maxWidth: '420px',
                        margin: '0 auto',
                        backgroundColor: '#fffbeb',
                        border: '2px solid #f59e0b',
                        borderRadius: '1rem',
                        padding: '3rem 2rem',
                        marginBottom: '3rem',
                        display: 'flex',
                        flexDirection: 'column',
                        alignItems: 'center',
                        textAlign: 'center'
                    }}
                >
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', width: '64px', height: '64px', borderRadius: '50%', backgroundColor: 'rgba(245, 158, 11, 0.2)', marginBottom: '1.5rem' }}>
                        <Lock style={{ width: '32px', height: '32px', color: '#d97706' }} />
                    </div>
                    <h3 style={{ fontSize: '1.25rem', fontWeight: 700, color: '#1e293b', margin: '0 0 0.5rem 0' }}>
                        Chương trả phí
                    </h3>
                    <p style={{ fontSize: '0.9375rem', color: '#64748b', margin: '0 0 1.5rem 0', lineHeight: 1.5 }}>
                        Chương này do tác giả đặt giá. Thanh toán để đọc nội dung.
                    </p>
                    <div
                        style={{
                            width: '100%',
                            display: 'flex',
                            flexDirection: 'row',
                            alignItems: 'center',
                            justifyContent: 'center',
                            gap: '1.5rem',
                            flexWrap: 'wrap'
                        }}
                    >
                        <div
                            style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.5rem',
                                padding: '1rem 1.5rem',
                                backgroundColor: '#ffffff',
                                borderRadius: '0.75rem',
                                border: '1px solid #e2e8f0',
                                boxShadow: '0 1px 2px rgba(0,0,0,0.05)'
                            }}
                        >
                            <span style={{ fontWeight: 600, color: '#64748b' }}>Giá:</span>
                            <span style={{ fontSize: '1.5rem', fontWeight: 700, color: '#d97706' }}>
                                {coinPrice} xu
                            </span>
                        </div>
                        <button
                            type="button"
                            onClick={onPayClick || (() => { })}
                            disabled={isUnlocking}
                            style={{
                                padding: '1rem 1.75rem',
                                fontSize: '1rem',
                                fontWeight: 600,
                                color: '#ffffff',
                                backgroundColor: '#f59e0b',
                                border: 'none',
                                borderRadius: '0.5rem',
                                cursor: isUnlocking ? 'not-allowed' : 'pointer',
                                boxShadow: '0 1px 3px rgba(0,0,0,0.1)'
                            }}
                            onMouseEnter={(e) => {
                                if (isUnlocking) return;
                                e.currentTarget.style.backgroundColor = '#d97706';
                            }}
                            onMouseLeave={(e) => {
                                if (isUnlocking) return;
                                e.currentTarget.style.backgroundColor = '#f59e0b';
                            }}
                        >
                            {isUnlocking ? 'Đang mở khóa...' : `Thanh toán ${coinPrice} xu`}
                        </button>
                    </div>
                </div>
            ) : (
                /* Chapter Content */
                <div
                    style={{
                        backgroundColor: backgroundColor,
                        color: textColor,
                        padding: '3rem 2rem',
                        borderRadius: '1rem',
                        marginBottom: '3rem',
                        boxShadow: '0 1px 3px rgba(0, 0, 0, 0.1)',
                        fontSize: `${fontSize}px`,
                        fontFamily: fontFamily,
                        lineHeight: lineHeight,
                        textAlign: 'justify',
                        maxHeight: '80vh',
                        overflowY: 'auto',
                        overscrollBehavior: 'contain',
                        WebkitOverflowScrolling: 'touch'
                    }}
                >
                    {isRichText ? (
                        <>
                            <style>{`
                                .chapter-reader-richtext {
                                    word-break: break-word;
                                }
                                .chapter-reader-richtext p {
                                    margin: 0 0 1.5em 0;
                                    text-indent: 2em;
                                }
                                .chapter-reader-richtext p:last-child {
                                    margin-bottom: 0;
                                }
                                .chapter-reader-richtext h1,
                                .chapter-reader-richtext h2,
                                .chapter-reader-richtext h3,
                                .chapter-reader-richtext h4,
                                .chapter-reader-richtext h5,
                                .chapter-reader-richtext h6 {
                                    margin: 0 0 1rem 0;
                                    line-height: 1.4;
                                    text-indent: 0;
                                }
                                .chapter-reader-richtext ul,
                                .chapter-reader-richtext ol {
                                    margin: 0 0 1.2em 1.8em;
                                    padding: 0;
                                }
                                .chapter-reader-richtext li {
                                    margin-bottom: 0.4em;
                                }
                                .chapter-reader-richtext blockquote {
                                    margin: 0 0 1.2em 0;
                                    padding-left: 1em;
                                    border-left: 3px solid #cbd5e1;
                                    color: #475569;
                                }
                            `}</style>
                            <div
                                className="chapter-reader-richtext"
                                dangerouslySetInnerHTML={{
                                    __html: sanitizeRichTextHtml(chapter.content || '').trim() || '<p></p>',
                                }}
                            />
                        </>
                    ) : (
                        (chapter.content || '')
                            .split('\n\n')
                            .filter(Boolean)
                            .map((paragraph, index) => (
                                <p key={index} style={{ marginBottom: '1.5em', textIndent: '2em' }}>
                                    {paragraph}
                                </p>
                            ))
                    )}
                </div>
            )}
        </div>
    );
}
