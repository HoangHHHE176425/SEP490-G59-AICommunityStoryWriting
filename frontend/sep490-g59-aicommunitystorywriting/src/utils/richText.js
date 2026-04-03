export function stripHtmlToText(input) {
    if (input == null) return '';
    const raw = String(input);
    if (!raw.trim()) return '';
    if (typeof window === 'undefined' || typeof DOMParser === 'undefined') {
        return raw.replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim();
    }
    const doc = new DOMParser().parseFromString(raw, 'text/html');
    return (doc.body?.textContent || '').replace(/\s+/g, ' ').trim();
}

export function sanitizeRichTextHtml(input) {
    const raw = String(input ?? '');
    if (!raw.trim()) return '';
    if (typeof window === 'undefined' || typeof DOMParser === 'undefined') {
        return raw;
    }
    const doc = new DOMParser().parseFromString(raw, 'text/html');
    doc.querySelectorAll('script,style,iframe,object,embed,link,meta').forEach((el) => el.remove());
    doc.querySelectorAll('*').forEach((el) => {
        [...el.attributes].forEach((attr) => {
            const name = attr.name.toLowerCase();
            const value = String(attr.value || '').trim().toLowerCase();
            if (name.startsWith('on')) el.removeAttribute(attr.name);
            if ((name === 'href' || name === 'src') && value.startsWith('javascript:')) {
                el.removeAttribute(attr.name);
            }
        });
    });
    return doc.body?.innerHTML || '';
}

