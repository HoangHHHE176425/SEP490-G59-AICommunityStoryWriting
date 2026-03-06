import { Lock } from 'lucide-react';

export function ChapterContent({
    chapter,
    fontSize,
    fontFamily,
    backgroundColor,
    textColor,
    lineHeight,
    onPayClick
}) {
    const isPaidLocked = chapter?.isPaidLocked === true;
    const coinPrice = Number(chapter?.coinPrice ?? 0) || 0;

    return (
        <div style={{ maxWidth: '800px', margin: '0 auto', padding: '2rem 1.5rem' }}>
            {/* Chapter Header */}
            <div style={{ textAlign: 'center', marginBottom: '3rem' }}>
                <h2 style={{ fontSize: '1.875rem', fontWeight: 'bold', color: '#1e293b', margin: 0 }}>
                    Chương {chapter.number}: {chapter.title}
                </h2>
                {!isPaidLocked && (
                    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '1rem', marginTop: '1rem', fontSize: '0.875rem', color: '#64748b' }}>
                        <span>{chapter.publishedAt}</span>
                        <span>•</span>
                        <span>{chapter.views.toLocaleString()} lượt đọc</span>
                        <span>•</span>
                        <span>{chapter.words.toLocaleString()} từ</span>
                    </div>
                )}
            </div>

            {/* Paid chapter: show payment section instead of content */}
            {isPaidLocked ? (
                <div
                    style={{
                        backgroundColor: '#fffbeb',
                        border: '2px solid #f59e0b',
                        borderRadius: '1rem',
                        padding: '3rem 2rem',
                        marginBottom: '3rem',
                        textAlign: 'center'
                    }}
                >
                    <div style={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'center', width: '64px', height: '64px', borderRadius: '50%', backgroundColor: 'rgba(245, 158, 11, 0.2)', marginBottom: '1.5rem' }}>
                        <Lock style={{ width: '32px', height: '32px', color: '#d97706' }} />
                    </div>
                    <h3 style={{ fontSize: '1.25rem', fontWeight: 700, color: '#1e293b', margin: '0 0 0.5rem 0' }}>
                        Chương trả phí
                    </h3>
                    <p style={{ fontSize: '0.9375rem', color: '#64748b', margin: '0 0 1.5rem 0' }}>
                        Chương này do tác giả đặt giá. Thanh toán để đọc nội dung.
                    </p>
                    <div
                        style={{
                            display: 'inline-flex',
                            alignItems: 'center',
                            justifyContent: 'space-between',
                            minWidth: '220px',
                            padding: '1rem 1.5rem',
                            backgroundColor: '#ffffff',
                            borderRadius: '0.75rem',
                            border: '1px solid #e2e8f0',
                            marginBottom: '1.5rem'
                        }}
                    >
                        <span style={{ fontWeight: 600, color: '#64748b' }}>Giá:</span>
                        <span style={{ fontSize: '1.5rem', fontWeight: 700, color: '#d97706' }}>
                            {coinPrice} xu
                        </span>
                    </div>
                    <button
                        type="button"
                        onClick={onPayClick || (() => {})}
                        style={{
                            padding: '0.75rem 2rem',
                            fontSize: '1rem',
                            fontWeight: 600,
                            color: '#ffffff',
                            backgroundColor: '#f59e0b',
                            border: 'none',
                            borderRadius: '0.5rem',
                            cursor: 'pointer',
                            boxShadow: '0 1px 3px rgba(0,0,0,0.1)'
                        }}
                        onMouseEnter={(e) => {
                            e.currentTarget.style.backgroundColor = '#d97706';
                        }}
                        onMouseLeave={(e) => {
                            e.currentTarget.style.backgroundColor = '#f59e0b';
                        }}
                    >
                        Thanh toán {coinPrice} xu
                    </button>
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
                        textAlign: 'justify'
                    }}
                >
                    {(chapter.content || '')
                        .split('\n\n')
                        .filter(Boolean)
                        .map((paragraph, index) => (
                            <p key={index} style={{ marginBottom: '1.5em', textIndent: '2em' }}>
                                {paragraph}
                            </p>
                        ))}
                </div>
            )}
        </div>
    );
}
