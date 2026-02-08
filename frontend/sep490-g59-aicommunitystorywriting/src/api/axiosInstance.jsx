const apiUrl = import.meta.env.VITE_API_URL || "http://localhost:5000/api";

function joinUrl(baseURL, url) {
    if (/^https?:\/\//i.test(url)) return url;
    const base = String(baseURL || "").replace(/\/+$/, "");
    const path = String(url || "").replace(/^\/+/, "");
    return `${base}/${path}`;
}

async function parseResponseBody(res) {
    const contentType = res.headers.get("content-type") || "";
    if (contentType.includes("application/json")) {
        return await res.json();
    }
    const text = await res.text();
    // Try to parse JSON even if content-type is wrong
    try {
        return JSON.parse(text);
    } catch {
        return text;
    }
}

function buildError(message, res, data) {
    const err = new Error(message || "Request failed");
    err.response = {
        status: res?.status,
        data,
    };
    return err;
}

function createAxiosLikeClient({ baseURL, headers, withCredentials } = {}) {
    const defaultHeaders = { ...(headers || {}) };

    const requestInterceptors = [];
    const client = {
        defaults: { baseURL, headers: defaultHeaders, withCredentials: !!withCredentials },
        interceptors: {
            request: {
                use(onFulfilled, onRejected) {
                    requestInterceptors.push({ onFulfilled, onRejected });
                    return requestInterceptors.length - 1;
                },
            },
        },
    };

    async function request(method, url, data, config = {}) {
        let cfg = {
            method,
            url,
            baseURL: config.baseURL ?? client.defaults.baseURL,
            headers: { ...client.defaults.headers, ...(config.headers || {}) },
            withCredentials: config.withCredentials ?? client.defaults.withCredentials,
            data,
        };

        // Apply request interceptors (axios-like)
        for (const itc of requestInterceptors) {
            try {
                if (typeof itc.onFulfilled === "function") {
                    // allow interceptor to mutate/replace config
                    cfg = (await itc.onFulfilled(cfg)) || cfg;
                }
            } catch (e) {
                if (typeof itc.onRejected === "function") {
                    await itc.onRejected(e);
                }
                throw e;
            }
        }

        const fullUrl = joinUrl(cfg.baseURL, cfg.url);

        const fetchInit = {
            method: cfg.method,
            headers: { ...(cfg.headers || {}) },
            credentials: cfg.withCredentials ? "include" : "same-origin",
        };

        // Body handling
        if (cfg.data !== undefined) {
            if (cfg.data instanceof FormData) {
                fetchInit.body = cfg.data;
                // Let browser set boundary; don't force Content-Type
                for (const key of Object.keys(fetchInit.headers)) {
                    if (key.toLowerCase() === "content-type") delete fetchInit.headers[key];
                }
            } else if (
                typeof cfg.data === "string" ||
                cfg.data instanceof Blob ||
                cfg.data instanceof ArrayBuffer
            ) {
                fetchInit.body = cfg.data;
            } else {
                fetchInit.body = JSON.stringify(cfg.data);
                if (!Object.keys(fetchInit.headers).some((k) => k.toLowerCase() === "content-type")) {
                    fetchInit.headers["Content-Type"] = "application/json";
                }
            }
        }

        const res = await fetch(fullUrl, fetchInit);
        const body = await parseResponseBody(res);

        if (!res.ok) {
            const message = body?.message || body?.title || `HTTP ${res.status}`;
            throw buildError(message, res, body);
        }

        return {
            data: body,
            status: res.status,
            headers: res.headers,
        };
    }

    client.get = (url, config) => request("GET", url, undefined, config);
    client.delete = (url, config) => request("DELETE", url, undefined, config);
    client.post = (url, data, config) => request("POST", url, data, config);
    client.put = (url, data, config) => request("PUT", url, data, config);
    client.patch = (url, data, config) => request("PATCH", url, data, config);

    return client;
}

const axiosInstance = createAxiosLikeClient({
    baseURL: apiUrl,
    headers: {
        "Content-Type": "application/json",
    },
    withCredentials: true,
});

axiosInstance.interceptors.request.use(
    (config) => {
        const token = localStorage.getItem("accessToken");
        if (token) {
            config.headers = config.headers || {};
            config.headers.Authorization = `Bearer ${token}`;
        }
        return config;
    },
    (error) => Promise.reject(error)
);

export default axiosInstance;