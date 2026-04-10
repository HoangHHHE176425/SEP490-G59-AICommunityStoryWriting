/**
 * Parse DateTime từ API ASP.NET: thường là UTC trong DB nhưng JSON có thể thiếu hậu tố "Z",
 * khiến `new Date(str)` bị hiểu nhầm là giờ local → lệch múi giờ (vd. 7h30 VN hiện 00:30).
 */

export function parseApiDateTimeUtc(value) {
    if (value == null || value === '') return null;
    if (value instanceof Date) return Number.isNaN(value.getTime()) ? null : value;
    let s = String(value).trim();
    if (!s) return null;
    s = s.replace(' ', 'T');
    if (/Z$/i.test(s) || /[+-]\d{2}:\d{2}$/.test(s) || /[+-]\d{4}$/.test(s)) {
        const d = new Date(s);
        return Number.isNaN(d.getTime()) ? null : d;
    }
    if (/^\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}(\.\d+)?$/i.test(s)) {
        const d = new Date(`${s}Z`);
        return Number.isNaN(d.getTime()) ? null : d;
    }
    const d = new Date(s);
    return Number.isNaN(d.getTime()) ? null : d;
}

/** Hiển thị theo giờ địa phương trình duyệt, định dạng vi-VN. */
export function formatApiDateTimeLocalVi(value) {
    const d = parseApiDateTimeUtc(value);
    if (!d) return '—';
    return d.toLocaleString('vi-VN', {
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false,
    });
}

/** Luôn hiển thị theo giờ Việt Nam (UTC+7), không phụ thuộc múi giờ trình duyệt. */
export function formatApiDateTimeVietnamVi(value) {
    const d = parseApiDateTimeUtc(value);
    if (!d) return '—';
    return d.toLocaleString('vi-VN', {
        timeZone: 'Asia/Ho_Chi_Minh',
        day: '2-digit',
        month: '2-digit',
        year: 'numeric',
        hour: '2-digit',
        minute: '2-digit',
        second: '2-digit',
        hour12: false,
    });
}

/**
 * Nhật ký admin (moderation / compliance): JSON từ ASP.NET thường là DateTime Kind=Unspecified, không có "Z".
 * Nếu ép thành UTC rồi format Asia/Ho_Chi_Minh sẽ cộng thêm +7 so với giờ đã lưu trong DB (lệch giờ VN).
 * - Chuỗi có Z hoặc ±offset: coi là mốc UTC chuẩn → format giờ VN.
 * - Chuỗi naive `yyyy-MM-ddTHH:mm:ss`: hiển thị đúng theo các thành phần (wall clock), không đổi múi giờ.
 */
export function formatLogTimestampVi(value) {
    if (value == null || value === '') return '—';
    if (value instanceof Date) {
        return Number.isNaN(value.getTime()) ? '—' : formatApiDateTimeVietnamVi(value.toISOString());
    }
    let s = String(value).trim();
    if (!s) return '—';
    s = s.replace(' ', 'T');
    if (/Z$/i.test(s) || /[+-]\d{2}:\d{2}$/.test(s) || /[+-]\d{4}$/.test(s)) {
        return formatApiDateTimeVietnamVi(s);
    }
    const m = s.match(/^(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2})(?::(\d{2}))?/);
    if (m) {
        const [, y, mo, d, h, mi, secRaw] = m;
        const sec = (secRaw != null && secRaw !== '' ? secRaw : '00').padStart(2, '0').slice(0, 2);
        return `${h}:${mi}:${sec} ${d}/${mo}/${y}`;
    }
    return formatApiDateTimeVietnamVi(value);
}

/** Giá trị cho input datetime-local (giờ local trình duyệt). */
export function apiDateToDatetimeLocalValue(isoOrApi) {
    const d = parseApiDateTimeUtc(isoOrApi);
    if (!d) return '';
    const pad = (n) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
