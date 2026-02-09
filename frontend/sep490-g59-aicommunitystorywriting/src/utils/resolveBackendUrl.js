const apiUrl = import.meta.env.VITE_API_URL || "https://localhost:7117/api";

// If VITE_API_URL ends with /api, media is typically served from the same origin without /api.
const backendOrigin = apiUrl.replace(/\/api\/?$/i, "");

export function resolveBackendUrl(path) {
    if (!path) return "";
    if (/^https?:\/\//i.test(path)) return path;
    if (path.startsWith("/")) return `${backendOrigin}${path}`;
    return `${backendOrigin}/${path}`;
}

