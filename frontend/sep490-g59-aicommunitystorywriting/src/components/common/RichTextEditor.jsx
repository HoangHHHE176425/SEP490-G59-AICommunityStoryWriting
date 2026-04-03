import { useEffect, useRef } from 'react';
import { Bold, Italic } from 'lucide-react';
import { sanitizeRichTextHtml } from '../../utils/richText';

const FONT_FAMILIES = [
    { label: 'Arial', value: 'Arial, sans-serif' },
    { label: 'Times New Roman', value: 'Times New Roman, serif' },
    { label: 'Georgia', value: 'Georgia, serif' },
    { label: 'Courier New', value: 'Courier New, monospace' },
];

const FONT_SIZES = ['14px', '16px', '18px', '20px', '22px'];

export function RichTextEditor({
    value,
    onChange,
    readOnly = false,
    placeholder = '',
    minHeight = 320,
    backgroundColor = '#ffffff',
    borderRadius = '8px',
    fontSize = 16,
    fontFamily = 'Arial, sans-serif',
}) {
    const editorRef = useRef(null);

    useEffect(() => {
        const el = editorRef.current;
        if (!el) return;
        const normalized = sanitizeRichTextHtml(value || '');
        if (el.innerHTML !== normalized) {
            el.innerHTML = normalized;
        }
    }, [value]);

    const emitChange = () => {
        const el = editorRef.current;
        if (!el || !onChange) return;
        onChange(sanitizeRichTextHtml(el.innerHTML));
    };

    const applyCommand = (command) => {
        const el = editorRef.current;
        if (!el || readOnly) return;
        el.focus();
        document.execCommand(command, false);
        emitChange();
    };

    const applyInlineStyle = (styleKey, styleValue) => {
        const el = editorRef.current;
        if (!el || readOnly) return;
        el.focus();
        const sel = window.getSelection();
        if (!sel || sel.rangeCount === 0) return;
        const range = sel.getRangeAt(0);
        if (range.collapsed) return;

        const span = document.createElement('span');
        span.style[styleKey] = styleValue;
        try {
            range.surroundContents(span);
        } catch {
            const extracted = range.extractContents();
            span.appendChild(extracted);
            range.insertNode(span);
        }

        sel.removeAllRanges();
        const newRange = document.createRange();
        newRange.selectNodeContents(span);
        sel.addRange(newRange);
        emitChange();
    };

    return (
        <div>
            {!readOnly && (
                <div className="mb-2 flex flex-wrap items-center gap-2 rounded-lg border border-slate-200 bg-slate-50 p-2">
                    <button type="button" onClick={() => applyCommand('bold')} className="rounded-md border border-slate-300 bg-white p-2 hover:bg-slate-100" title="In đậm">
                        <Bold size={14} />
                    </button>
                    <button type="button" onClick={() => applyCommand('italic')} className="rounded-md border border-slate-300 bg-white p-2 hover:bg-slate-100" title="In nghiêng">
                        <Italic size={14} />
                    </button>
                    <select
                        defaultValue=""
                        onChange={(e) => {
                            if (!e.target.value) return;
                            applyInlineStyle('fontFamily', e.target.value);
                            e.target.value = '';
                        }}
                        className="rounded-md border border-slate-300 bg-white px-2 py-1.5 text-xs"
                    >
                        <option value="">Chọn font</option>
                        {FONT_FAMILIES.map((f) => (
                            <option key={f.value} value={f.value}>{f.label}</option>
                        ))}
                    </select>
                    <select
                        defaultValue=""
                        onChange={(e) => {
                            if (!e.target.value) return;
                            applyInlineStyle('fontSize', e.target.value);
                            e.target.value = '';
                        }}
                        className="rounded-md border border-slate-300 bg-white px-2 py-1.5 text-xs"
                    >
                        <option value="">Chọn cỡ chữ</option>
                        {FONT_SIZES.map((s) => (
                            <option key={s} value={s}>{s}</option>
                        ))}
                    </select>
                </div>
            )}
            <div
                ref={editorRef}
                contentEditable={!readOnly}
                suppressContentEditableWarning
                onInput={emitChange}
                data-placeholder={placeholder}
                style={{
                    width: '100%',
                    minHeight,
                    padding: '1rem',
                    backgroundColor,
                    border: '1px solid #e5e7eb',
                    borderRadius,
                    outline: 'none',
                    fontSize: `${fontSize}px`,
                    fontFamily,
                    lineHeight: 1.8,
                    overflowY: 'auto',
                    whiteSpace: 'pre-wrap',
                    cursor: readOnly ? 'default' : 'text',
                }}
            />
        </div>
    );
}

