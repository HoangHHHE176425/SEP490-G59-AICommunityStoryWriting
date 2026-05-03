export const MAX_CO_CREATE_PROMPT_CHARS = 6000;

export const countCoCreatePromptDisplayLength = (text) => {
    if (!text) return 0;
    return text.normalize('NFD').length;
};

export const clampTextToCoCreatePromptLimit = (text, maxChars = MAX_CO_CREATE_PROMPT_CHARS) => {
    if (!text) return '';
    let result = '';
    for (const ch of text) {
        const candidate = result + ch;
        if (countCoCreatePromptDisplayLength(candidate) > maxChars) break;
        result = candidate;
    }
    return result;
};
