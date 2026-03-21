/**
 * BR-01: Đăng ký — chuẩn kiểm tra đầu vào (FE).
 * Danh sách từ cấm có thể mở rộng; BE nên kiểm tra lại để đảm bảo an toàn.
 */

/** Độ dài email tối đa (RFC thực tế ~254; BR yêu cầu 255). */
export const EMAIL_MAX_LENGTH = 255;

/** Tên hiển thị: 3–50 ký tự (theo BR). */
export const DISPLAY_NAME_MIN = 3;
export const DISPLAY_NAME_MAX = 50;

/** Mật khẩu: tối thiểu 8 ký tự, có chữ và số (BR). */
export const PASSWORD_MIN_LENGTH = 8;

/**
 * Cụm từ cấm (khớp trong toàn bộ chuỗi, sau khi chuẩn hóa).
 */
const PROHIBITED_PHRASES = [
    'đồ chó',
    'mẹ mày',
    'con mẹ',
    'fuck you',
    'fuck off',
];

/**
 * Từ đơn cấm: khớp theo từ (token) để tránh dính nhầm (vd: "đụng" ≠ "đụ").
 */
const PROHIBITED_TOKENS = new Set([
    // Vietnamese
    'địt',
    'đụ',
    'lồn',
    'cặc',
    'đéo',
    // English
    'fuck',
    'shit',
    'bitch',
    'asshole',
    'dick',
    'cunt',
    'whore',
    'slut',
    'nazi',
    'rape',
    'piss',
]);

/**
 * Chuỗi con cấm (khớp nếu xuất hiện trong một token hoặc toàn chuỗi — dùng cho biến thể).
 */
const PROHIBITED_SUBSTRINGS_IN_TOKEN = [
    'fuck',
    'shit',
    'bitch',
    'asshole',
    'nazi',
    'rape',
    'faggot',
    'retard',
];

/**
 * Kiểm tra tên có chứa từ/khối từ cấm (không phân biệt hoa thường).
 */
export function containsProhibitedWords(displayName) {
    if (!displayName || typeof displayName !== 'string') return false;
    const lower = displayName.toLowerCase().normalize('NFC');
    for (const phrase of PROHIBITED_PHRASES) {
        if (lower.includes(phrase.toLowerCase())) return true;
    }
    const segments = lower.split(/[^\p{L}\p{N}]+/u).filter(Boolean);
    for (const seg of segments) {
        if (PROHIBITED_TOKENS.has(seg)) return true;
        for (const sub of PROHIBITED_SUBSTRINGS_IN_TOKEN) {
            if (seg.includes(sub)) return true;
        }
    }
    return false;
}

/** Email đúng định dạng cơ bản user@domain.tld */
const EMAIL_REGEX =
    /^[a-zA-Z0-9.!#$%&'*+/=?^_`{|}~-]+@[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?(?:\.[a-zA-Z0-9](?:[a-zA-Z0-9-]{0,61}[a-zA-Z0-9])?)+$/;

export function isValidEmailFormat(email) {
    if (!email || typeof email !== 'string') return false;
    const trimmed = email.trim();
    if (trimmed.length > EMAIL_MAX_LENGTH) return false;
    return EMAIL_REGEX.test(trimmed);
}

export function isPasswordStrongEnough(password) {
    if (!password || typeof password !== 'string') return false;
    if (password.length < PASSWORD_MIN_LENGTH) return false;
    const hasLetter = /[a-zA-ZÀ-ỹà-ỹ]/.test(password);
    const hasDigit = /\d/.test(password);
    return hasLetter && hasDigit;
}

/**
 * @returns {{ ok: true } | { ok: false, field: string, message: string }}
 */
export function validateRegisterFields({ name, email, password, confirmPassword }) {
    const displayName = typeof name === 'string' ? name.trim() : '';
    const emailTrimmed = typeof email === 'string' ? email.trim() : '';
    const pwd = password ?? '';
    const confirm = confirmPassword ?? '';

    if (!displayName || !emailTrimmed || !pwd || !confirm) {
        return { ok: false, field: 'general', message: 'Vui lòng điền đầy đủ thông tin.' };
    }

    if (displayName.length < DISPLAY_NAME_MIN || displayName.length > DISPLAY_NAME_MAX) {
        return {
            ok: false,
            field: 'name',
            message: `Họ và tên phải từ ${DISPLAY_NAME_MIN} đến ${DISPLAY_NAME_MAX} ký tự.`,
        };
    }

    if (containsProhibitedWords(displayName)) {
        return {
            ok: false,
            field: 'name',
            message: 'Họ và tên không được chứa từ ngữ không phù hợp.',
        };
    }

    if (emailTrimmed.length > EMAIL_MAX_LENGTH || !isValidEmailFormat(emailTrimmed)) {
        return {
            ok: false,
            field: 'email',
            message: `Email phải đúng định dạng (ví dụ: user@domain.com) và không quá ${EMAIL_MAX_LENGTH} ký tự.`,
        };
    }

    if (!isPasswordStrongEnough(pwd)) {
        return {
            ok: false,
            field: 'password',
            message: `Mật khẩu phải ít nhất ${PASSWORD_MIN_LENGTH} ký tự và gồm cả chữ cái và chữ số.`,
        };
    }

    if (pwd !== confirm) {
        return { ok: false, field: 'confirmPassword', message: 'Mật khẩu xác nhận không khớp.' };
    }

    return { ok: true };
}
