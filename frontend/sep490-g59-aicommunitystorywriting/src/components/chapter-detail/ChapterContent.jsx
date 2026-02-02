export function ChapterContent({
    chapter,
    fontSize,
    fontFamily,
    backgroundColor,
    textColor,
    lineHeight
}) {
    return (
        <div style={{ maxWidth: '800px', margin: '0 auto', padding: '2rem 1.5rem' }}>
            {/* Chapter Header */}
            <div style={{ textAlign: 'center', marginBottom: '3rem' }}>
                <h2 style={{ fontSize: '1.875rem', fontWeight: 'bold', color: '#1e293b', margin: 0 }}>
                    Chương {chapter.number}: {chapter.title}
                </h2>
                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'center', gap: '1rem', marginTop: '1rem', fontSize: '0.875rem', color: '#64748b' }}>
                    <span>{chapter.publishedAt}</span>
                    <span>•</span>
                    <span>{chapter.views.toLocaleString()} lượt đọc</span>
                    <span>•</span>
                    <span>{chapter.words.toLocaleString()} từ</span>
                </div>
            </div>

            {/* Chapter Content */}
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
                {chapter.content.split('\n\n').map((paragraph, index) => (
                    <p key={index} style={{ marginBottom: '1.5em', textIndent: '2em' }}>
                        {paragraph}
                    </p>
                ))}
            </div>
        </div>
    );
}
