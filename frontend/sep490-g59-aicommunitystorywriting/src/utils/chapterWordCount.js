function htmlDecodeBasic(input) {
    if (input == null || input === '') return '';
    if (typeof document !== 'undefined' && typeof document.createElement === 'function') {
        const ta = document.createElement('textarea');
        ta.innerHTML = input;
        return ta.value;
    }
    return String(input)
        .replace(/&nbsp;/gi, ' ')
        .replace(/&amp;/g, '&')
        .replace(/&lt;/g, '<')
        .replace(/&gt;/g, '>')
        .replace(/&quot;/g, '"')
        .replace(/&#(\d+);/g, (_, n) => String.fromCharCode(Number(n)))
        .replace(/&#x([0-9a-f]+);/gi, (_, h) => String.fromCharCode(parseInt(h, 16)));
}

export function plainTextForWordCount(rawHtmlOrText) {
    if (rawHtmlOrText == null || !String(rawHtmlOrText).trim()) return '';
    let s = String(rawHtmlOrText).trim();

    s = s.replace(/<script\b[^>]*>[\s\S]*?<\/script>/gi, ' ');
    s = s.replace(/<style\b[^>]*>[\s\S]*?<\/style>/gi, ' ');

    s = s.replace(/<\s*br\s*\/?\s*>/gi, ' ');
    s = s.replace(/<\s*\/\s*p\s*>/gi, ' ');
    s = s.replace(/<\s*p\b[^>]*>/gi, ' ');
    s = s.replace(/<\s*\/\s*div\s*>/gi, ' ');
    s = s.replace(/<\s*div\b[^>]*>/gi, ' ');
    s = s.replace(/<\s*\/\s*li\s*>/gi, ' ');
    s = s.replace(/<\s*li\b[^>]*>/gi, ' ');

    s = s.replace(/<[^>]+>/g, ' ');

    s = htmlDecodeBasic(s);
    s = s.replace(/\u00a0/g, ' ');

    s = s.replace(/\s+/g, ' ').trim();
    return s;
}

export const countChapterWords = (rawContent) => {
    const plain = plainTextForWordCount(rawContent == null ? '' : String(rawContent));
    if (!plain.trim()) return 0;
    return plain.split(/[ \t\n\r]+/).filter(Boolean).length;
};
