import { resolveBackendUrl } from './resolveBackendUrl';

export function getInitialFromName(name, fallback = 'U') {
    const s = String(name ?? '').trim();
    if (!s) return String(fallback).toUpperCase();
    return (s[0] || fallback).toUpperCase();
}

/**
 * Avatar fallback đồng nhất: nền gradient + chữ cái đầu.
 * Dùng data URL để có thể truyền trực tiếp vào <img src>.
 */
export function createInitialAvatarDataUrl(name, size = 256) {
    const n = Math.max(64, Number(size) || 256);
    const initial = getInitialFromName(name, 'U');
    const r = Math.round(n * 0.16);
    const font = Math.round(n * 0.48);
    const svg = `
<svg xmlns="http://www.w3.org/2000/svg" width="${n}" height="${n}" viewBox="0 0 ${n} ${n}">
  <defs>
    <linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#22c55e"/>
      <stop offset="1" stop-color="#3b82f6"/>
    </linearGradient>
  </defs>
  <rect width="${n}" height="${n}" rx="${r}" fill="url(#g)"/>
  <text x="50%" y="54%" dominant-baseline="middle" text-anchor="middle"
    font-family="Arial, Helvetica, sans-serif" font-size="${font}" font-weight="800" fill="#ffffff">${initial}</text>
</svg>`.trim();
    return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
}

/**
 * Ưu tiên avatar thật; nếu thiếu thì fallback chữ cái đầu.
 */
export function resolveAvatarWithFallback(avatarPathOrUrl, displayName, size = 256) {
    if (avatarPathOrUrl && String(avatarPathOrUrl).trim()) {
        return resolveBackendUrl(String(avatarPathOrUrl).trim());
    }
    return createInitialAvatarDataUrl(displayName, size);
}

