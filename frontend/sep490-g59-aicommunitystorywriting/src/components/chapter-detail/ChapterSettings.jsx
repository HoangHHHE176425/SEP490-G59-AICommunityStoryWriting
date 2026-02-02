export function ChapterSettings({
    show,
    fontSize,
    fontFamily,
    backgroundColor,
    // eslint-disable-next-line no-unused-vars
    textColor,
    lineHeight,
    onFontSizeChange,
    onFontFamilyChange,
    onThemeChange,
    onLineHeightChange
}) {
    const themes = [
        { name: 'Sáng', bg: '#ffffff', text: '#1e293b' },
        { name: 'Kem', bg: '#fef6e4', text: '#3d2914' },
        { name: 'Xanh nhạt', bg: '#e8f5e9', text: '#1b5e20' },
        { name: 'Tối', bg: '#1e293b', text: '#f1f5f9' },
        { name: 'Đen', bg: '#0f172a', text: '#e2e8f0' },
    ];

    const fontFamilies = [
        { name: 'Serif', value: 'serif' },
        { name: 'Sans-serif', value: 'sans-serif' },
        { name: 'Plus Jakarta Sans', value: '"Plus Jakarta Sans", sans-serif' },
        { name: 'Georgia', value: 'Georgia, serif' },
    ];

    if (!show) return null;

    return (
        <div
            style={{
                position: 'sticky',
                top: '73px',
                backgroundColor: '#ffffff',
                borderBottom: '1px solid #e2e8f0',
                zIndex: 90,
                boxShadow: '0 4px 6px rgba(0, 0, 0, 0.05)'
            }}
        >
            <div style={{ maxWidth: '1200px', margin: '0 auto', padding: '1.5rem' }}>
                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '1.5rem' }}>
                    {/* Font Size */}
                    <div>
                        <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 600, color: '#64748b', marginBottom: '0.5rem', textTransform: 'uppercase' }}>
                            Cỡ chữ: {fontSize}px
                        </label>
                        <input
                            type="range"
                            min="14"
                            max="28"
                            value={fontSize}
                            onChange={(e) => onFontSizeChange(Number(e.target.value))}
                            style={{ width: '100%', cursor: 'pointer' }}
                        />
                    </div>

                    {/* Font Family */}
                    <div>
                        <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 600, color: '#64748b', marginBottom: '0.5rem', textTransform: 'uppercase' }}>
                            Font chữ
                        </label>
                        <select
                            value={fontFamily}
                            onChange={(e) => onFontFamilyChange(e.target.value)}
                            style={{
                                width: '100%',
                                padding: '0.5rem',
                                backgroundColor: '#f8fafc',
                                border: '1px solid #e2e8f0',
                                borderRadius: '0.375rem',
                                fontSize: '0.875rem',
                                cursor: 'pointer'
                            }}
                        >
                            {fontFamilies.map((font) => (
                                <option key={font.value} value={font.value}>
                                    {font.name}
                                </option>
                            ))}
                        </select>
                    </div>

                    {/* Line Height */}
                    <div>
                        <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 600, color: '#64748b', marginBottom: '0.5rem', textTransform: 'uppercase' }}>
                            Giãn dòng: {lineHeight}
                        </label>
                        <input
                            type="range"
                            min="1.4"
                            max="2.5"
                            step="0.1"
                            value={lineHeight}
                            onChange={(e) => onLineHeightChange(Number(e.target.value))}
                            style={{ width: '100%', cursor: 'pointer' }}
                        />
                    </div>

                    {/* Theme Colors */}
                    <div>
                        <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 600, color: '#64748b', marginBottom: '0.5rem', textTransform: 'uppercase' }}>
                            Màu nền
                        </label>
                        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                            {themes.map((theme) => (
                                <button
                                    key={theme.name}
                                    onClick={() => onThemeChange(theme.bg, theme.text)}
                                    style={{
                                        width: '40px',
                                        height: '40px',
                                        backgroundColor: theme.bg,
                                        border: backgroundColor === theme.bg ? '3px solid #13ec5b' : '2px solid #e2e8f0',
                                        borderRadius: '0.375rem',
                                        cursor: 'pointer',
                                        transition: 'all 0.2s'
                                    }}
                                    title={theme.name}
                                />
                            ))}
                        </div>
                    </div>
                </div>
            </div>
        </div>
    );
}
