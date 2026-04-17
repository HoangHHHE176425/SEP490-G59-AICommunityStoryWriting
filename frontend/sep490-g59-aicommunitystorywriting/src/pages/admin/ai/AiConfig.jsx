import { useEffect, useMemo, useState } from 'react';
import { Brain } from 'lucide-react';
import {
    addAdminBannedWord,
    createAdminAuthorAiTokenAutoGrantRule,
    deleteAdminAuthorAiTokenAutoGrantRule,
    getAdminAuthorAiTokenAutoGrantRules,
    deleteAdminBannedWord,
    getAdminAiGenerationsDaily,
    getAdminAiOpenRouterGeneration,
    getAdminAiRequestLogs,
    getAdminBannedWords,
    runNowAdminAuthorAiTokenAutoGrantRule,
    updateAdminAuthorAiTokenAutoGrantRule,
} from '../../../api/admin/aiConfigApi';
import { getUsers } from '../../../api/admin/userManagementApi';

const toUtcInputString = (d) => {
    if (!(d instanceof Date)) return '';
    const dt = new Date(d.getTime() - d.getTimezoneOffset() * 60000);
    return dt.toISOString().slice(0, 16);
};

const defaultToUtc = toUtcInputString(new Date());
const defaultFromUtc = toUtcInputString(new Date(Date.now() - 7 * 24 * 60 * 60 * 1000));


const formatDateTimeVi = (value) => {
    if (!value) return '—';
    let d;
    if (typeof value === 'string') {
        const raw = value.trim();
        const hasTimezone = /(?:Z|[+-]\d{2}:\d{2})$/i.test(raw);
        // Backend có thể trả "CreatedAtUtc" nhưng thiếu hậu tố timezone.
        // Khi thiếu, ép parse như UTC để tránh bị hiểu nhầm là local time.
        d = new Date(hasTimezone ? raw : `${raw}Z`);
    } else {
        d = new Date(value);
    }
    if (Number.isNaN(d.getTime())) return String(value);
    return d.toLocaleString('vi-VN', {
        timeZone: 'Asia/Ho_Chi_Minh',
        hour12: false,
    });
};

const formatUsdNullable = (value) => {
    if (value == null || value === '') return '—';
    const n = Number(value);
    if (!Number.isFinite(n)) return '—';
    return n.toLocaleString('en-US', { maximumFractionDigits: 6 });
};

const normalizeGenerationDetailPayload = (payload) => {
    if (payload == null) return null;
    if (typeof payload === 'string') {
        try {
            return JSON.parse(payload);
        } catch {
            return { raw: payload };
        }
    }
    return payload;
};

const fmtIntOrDash = (v) => {
    if (v == null || v === '') return '—';
    const n = Number(v);
    if (!Number.isFinite(n)) return '—';
    return Math.trunc(n).toLocaleString('vi-VN');
};

const pickArrayLike = (...candidates) => {
    for (const c of candidates) {
        if (Array.isArray(c)) return c;
    }
    return [];
};

const autoGrantLimitFieldFromPeriodKind = (periodKind) => {
    const s = String(periodKind || '').trim().toLowerCase();
    if (s === 'daily_utc') return 'per_day';
    if (s === 'weekly_utc') return 'per_week';
    return 'per_month';
};

const autoGrantPeriodKindLabelVi = (periodKind) => {
    const s = String(periodKind || '').trim().toLowerCase();
    if (s === 'daily_utc') return 'Ngày (UTC)';
    if (s === 'weekly_utc') return 'Tuần (UTC)';
    if (s === 'monthly_utc') return 'Tháng (UTC)';
    return s || '—';
};

const autoGrantLimitFieldLabelVi = (field) => {
    const s = String(field || '').trim().toLowerCase();
    if (s === 'per_day') return 'Theo ngày';
    if (s === 'per_week') return 'Theo tuần';
    if (s === 'per_month') return 'Theo tháng';
    if (s === 'lifetime') return 'Tích lũy';
    return s || '—';
};

const normalizeAutoGrantRule = (x) => {
    const sel = x?.selectedUserIds ?? x?.SelectedUserIds ?? [];
    return {
        id: x?.id ?? x?.Id ?? '',
        isEnabled: !!(x?.isEnabled ?? x?.IsEnabled),
        displayName: x?.displayName ?? x?.DisplayName ?? '',
        periodKind: String(x?.periodKind ?? x?.PeriodKind ?? 'monthly_utc').toLowerCase(),
        grantLimitField: String(x?.grantLimitField ?? x?.GrantLimitField ?? ''),
        grantAmount: Number(x?.grantAmount ?? x?.GrantAmount ?? 0) || 0,
        applyToAllAuthors: !!(x?.applyToAllAuthors ?? x?.ApplyToAllAuthors),
        selectedUserIds: Array.isArray(sel) ? sel : [],
        lastRunAtUtc: x?.lastRunAtUtc ?? x?.LastRunAtUtc ?? null,
    };
};

export function AiConfig() {
    const [bannedWordInput, setBannedWordInput] = useState('');
    const [bannedWords, setBannedWords] = useState([]);
    const [authorAccounts, setAuthorAccounts] = useState([]);
    const [autoGrantRules, setAutoGrantRules] = useState([]);
    const [autoGrantLoading, setAutoGrantLoading] = useState(false);
    const [savingAutoGrant, setSavingAutoGrant] = useState(false);
    const [autoGrantBusyRuleId, setAutoGrantBusyRuleId] = useState(null);
    const [autoGrantEditingRuleId, setAutoGrantEditingRuleId] = useState('');
    const [autoGrantAuthorFilter, setAutoGrantAuthorFilter] = useState('');
    const [autoGrantForm, setAutoGrantForm] = useState({
        displayName: '',
        periodKind: 'monthly_utc',
        grantAmount: '100000',
        isEnabled: true,
        applyToAllAuthors: false,
        selectedUserIds: [],
    });

    const [genFilter, setGenFilter] = useState({
        fromUtc: defaultFromUtc,
        toUtc: defaultToUtc,
        modelName: '',
        status: '',
        actionType: '',
        page: 1,
        pageSize: 25,
    });
    const [generationRows, setGenerationRows] = useState([]);
    const [generationTotal, setGenerationTotal] = useState(0);
    const [dailyRows, setDailyRows] = useState([]);
    const [genLoading, setGenLoading] = useState(false);
    const [generationUserSearch, setGenerationUserSearch] = useState('');
    const [generationDetail, setGenerationDetail] = useState(null);
    const [generationDetailId, setGenerationDetailId] = useState('');
    const [detailLoadingId, setDetailLoadingId] = useState(null);
    const [selectedLogUserEmail, setSelectedLogUserEmail] = useState('');
    const [selectedUserLogPage, setSelectedUserLogPage] = useState(1);

    const [loadingConfig, setLoadingConfig] = useState(true);
    const [savingWord, setSavingWord] = useState(false);
    const [deletingWordId, setDeletingWordId] = useState(null);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');

    const dailyChartRows = useMemo(() => {
        const normalized = (dailyRows || []).map((d) => {
            const day = String(d?.dayUtc ?? d?.date ?? d?.Date ?? d?.day ?? '').slice(0, 10);
            const count = Number(d?.count ?? d?.Count ?? 0) || 0;
            return { day, count };
        }).filter((x) => x.day);
        const max = normalized.reduce((m, x) => Math.max(m, x.count), 0);
        return normalized.map((x) => ({
            ...x,
            pct: max > 0 ? Math.max(4, Math.round((x.count / max) * 100)) : 0,
        }));
    }, [dailyRows]);
    const generationUserRows = useMemo(() => {
        const map = new Map();
        (generationRows || []).forEach((r) => {
            const email = String(r?.userEmail ?? r?.UserEmail ?? '—').trim() || '—';
            const prompt = Number(r?.promptTokens ?? r?.PromptTokens ?? r?.inputTokens ?? r?.InputTokens ?? 0) || 0;
            const completion = Number(r?.completionTokens ?? r?.CompletionTokens ?? r?.outputTokens ?? r?.OutputTokens ?? 0) || 0;
            const costRaw = r?.costUsd ?? r?.CostUsd;
            const cost = Number(costRaw);
            const prev = map.get(email) ?? { userEmail: email, totalPromptTokens: 0, totalCompletionTokens: 0, totalCostUsd: 0, hasAnyCost: false, requestCount: 0 };
            prev.totalPromptTokens += prompt;
            prev.totalCompletionTokens += completion;
            if (Number.isFinite(cost)) {
                prev.totalCostUsd += cost;
                prev.hasAnyCost = true;
            }
            prev.requestCount += 1;
            map.set(email, prev);
        });
        return Array.from(map.values()).sort((a, b) => b.requestCount - a.requestCount);
    }, [generationRows]);
    const filteredGenerationUserRows = useMemo(() => {
        const q = String(generationUserSearch || '').trim().toLowerCase();
        if (!q) return generationUserRows;
        return generationUserRows.filter((r) => String(r.userEmail || '').toLowerCase().includes(q));
    }, [generationUserRows, generationUserSearch]);
    const generationUserTotalPages = Math.max(1, Math.ceil((filteredGenerationUserRows.length || 0) / (Number(genFilter.pageSize) || 25)));
    const generationUserPagedRows = useMemo(() => {
        const pageSize = Number(genFilter.pageSize) || 25;
        const safePage = Math.min(Math.max(1, Number(genFilter.page) || 1), generationUserTotalPages);
        const start = (safePage - 1) * pageSize;
        return filteredGenerationUserRows.slice(start, start + pageSize);
    }, [filteredGenerationUserRows, genFilter.page, genFilter.pageSize, generationUserTotalPages]);
    const selectedUserLogs = useMemo(() => {
        if (!selectedLogUserEmail) return [];
        return (generationRows || [])
            .filter((r) => String(r?.userEmail ?? r?.UserEmail ?? '—').trim() === selectedLogUserEmail)
            .sort((a, b) => {
                const da = new Date(a?.createdAtUtc ?? a?.CreatedAtUtc ?? a?.occurredAtUtc ?? a?.OccurredAtUtc ?? a?.createdAt ?? a?.CreatedAt ?? 0).getTime();
                const db = new Date(b?.createdAtUtc ?? b?.CreatedAtUtc ?? b?.occurredAtUtc ?? b?.OccurredAtUtc ?? b?.createdAt ?? b?.CreatedAt ?? 0).getTime();
                return db - da;
            });
    }, [generationRows, selectedLogUserEmail]);
    const selectedUserLogPageSize = 20;
    const selectedUserLogTotalPages = Math.max(1, Math.ceil(selectedUserLogs.length / selectedUserLogPageSize));
    const selectedUserLogPagedRows = useMemo(() => {
        const safePage = Math.min(Math.max(1, Number(selectedUserLogPage) || 1), selectedUserLogTotalPages);
        const start = (safePage - 1) * selectedUserLogPageSize;
        return selectedUserLogs.slice(start, start + selectedUserLogPageSize);
    }, [selectedUserLogs, selectedUserLogPage, selectedUserLogTotalPages]);

    useEffect(() => {
        setSelectedUserLogPage(1);
    }, [selectedLogUserEmail]);
    const autoGrantFilteredAuthors = useMemo(() => {
        const q = String(autoGrantAuthorFilter || '').trim().toLowerCase();
        if (!q) return authorAccounts || [];
        return (authorAccounts || []).filter((u) => {
            const hay = `${u?.id || ''} ${u?.email || ''} ${u?.nickname || ''}`.toLowerCase();
            return hay.includes(q);
        });
    }, [authorAccounts, autoGrantAuthorFilter]);
    const autoGrantFormError = useMemo(() => {
        const amount = Number(autoGrantForm.grantAmount);
        if (!Number.isInteger(amount) || amount <= 0) {
            return 'Số token cộng phải là số nguyên > 0.';
        }
        if (!autoGrantForm.applyToAllAuthors && (!autoGrantForm.selectedUserIds || autoGrantForm.selectedUserIds.length === 0)) {
            return 'Cần chọn ít nhất một tác giả hoặc bật "Tất cả tác giả".';
        }
        return '';
    }, [autoGrantForm]);

    useEffect(() => {
        let mounted = true;
        async function load() {
            try {
                setError('');
                setSuccess('');
                setLoadingConfig(true);

                const [wordsResult, authorsResult] = await Promise.allSettled([
                    getAdminBannedWords('BannedWord'),
                    getUsers({ page: 1, pageSize: 200, role: 'AUTHOR' }),
                ]);

                if (!mounted) return;

                if (wordsResult.status === 'fulfilled') {
                    setBannedWords(Array.isArray(wordsResult.value) ? wordsResult.value : []);
                } else {
                    setBannedWords([]);
                }

                const authors = authorsResult.status === 'fulfilled' && Array.isArray(authorsResult.value?.items)
                    ? authorsResult.value.items
                    : [];
                setAuthorAccounts(authors);
            } catch (e) {
                if (!mounted) return;
                const msg =
                    e?.response?.data?.message ||
                    e?.message ||
                    'Không tải được cấu hình AI. Vui lòng thử lại.';
                setError(msg);
            } finally {
                if (mounted) setLoadingConfig(false);
            }
        }
        load();
        return () => {
            mounted = false;
        };
    }, []);

    const resetAutoGrantForm = () => {
        setAutoGrantEditingRuleId('');
        setAutoGrantAuthorFilter('');
        setAutoGrantForm({
            displayName: '',
            periodKind: 'monthly_utc',
            grantAmount: '100000',
            isEnabled: true,
            applyToAllAuthors: false,
            selectedUserIds: [],
        });
    };

    const loadAutoGrantRules = async () => {
        try {
            setAutoGrantLoading(true);
            const data = await getAdminAuthorAiTokenAutoGrantRules();
            const rows = pickArrayLike(data, data?.items, data?.Items, data?.data?.items, data?.Data?.Items);
            setAutoGrantRules((rows || []).map(normalizeAutoGrantRule).filter((x) => x.id));
        } catch (e) {
            setAutoGrantRules([]);
            setError(e?.response?.data?.message || e?.message || 'Không tải được danh sách quy tắc tự gia hạn token.');
        } finally {
            setAutoGrantLoading(false);
        }
    };

    const onToggleAutoGrantAuthor = (id, checked) => {
        const normalizedId = String(id || '').trim();
        if (!normalizedId) return;
        setAutoGrantForm((prev) => {
            const cur = Array.isArray(prev.selectedUserIds) ? prev.selectedUserIds : [];
            const nextSet = new Set(cur.map((x) => String(x)));
            if (checked) nextSet.add(normalizedId);
            else nextSet.delete(normalizedId);
            return {
                ...prev,
                // Chọn tác giả cụ thể => tắt chế độ "Tất cả tác giả" để tránh xung đột.
                applyToAllAuthors: false,
                selectedUserIds: Array.from(nextSet),
            };
        });
    };

    const onToggleApplyAllAuthors = (checked) => {
        if (checked) {
            const allIds = (authorAccounts || [])
                .map((u) => String(u?.id || '').trim())
                .filter((id) => !!id);
            setAutoGrantForm((p) => ({
                ...p,
                applyToAllAuthors: true,
                selectedUserIds: Array.from(new Set(allIds)),
            }));
            return;
        }
        setAutoGrantForm((p) => ({
            ...p,
            applyToAllAuthors: false,
            selectedUserIds: [],
        }));
    };

    const onSubmitAutoGrantRule = async () => {
        if (autoGrantFormError) {
            setError(autoGrantFormError);
            setSuccess('');
            return;
        }
        try {
            setSavingAutoGrant(true);
            setError('');
            setSuccess('');
            const payload = {
                isEnabled: !!autoGrantForm.isEnabled,
                displayName: autoGrantForm.displayName?.trim() ? autoGrantForm.displayName.trim() : null,
                periodKind: String(autoGrantForm.periodKind || 'monthly_utc').toLowerCase(),
                grantLimitField: autoGrantLimitFieldFromPeriodKind(autoGrantForm.periodKind),
                grantAmount: Number(autoGrantForm.grantAmount),
                applyToAllAuthors: !!autoGrantForm.applyToAllAuthors,
                selectedUserIds: autoGrantForm.applyToAllAuthors ? [] : (autoGrantForm.selectedUserIds || []),
            };

            if (autoGrantEditingRuleId) {
                await updateAdminAuthorAiTokenAutoGrantRule(autoGrantEditingRuleId, payload);
                setSuccess('Đã cập nhật quy tắc tự gia hạn token.');
            } else {
                await createAdminAuthorAiTokenAutoGrantRule(payload);
                setSuccess('Đã tạo quy tắc tự gia hạn token.');
            }
            resetAutoGrantForm();
            await loadAutoGrantRules();
        } catch (e) {
            setError(e?.response?.data?.message || e?.message || 'Không lưu được quy tắc tự gia hạn token.');
        } finally {
            setSavingAutoGrant(false);
        }
    };

    const onEditAutoGrantRule = (rule) => {
        const allAuthorIds = (authorAccounts || [])
            .map((u) => String(u?.id || '').trim())
            .filter((id) => !!id);
        const selectedIdsFromRule = Array.isArray(rule.selectedUserIds) ? rule.selectedUserIds : [];
        setAutoGrantEditingRuleId(rule.id);
        setAutoGrantForm({
            displayName: rule.displayName || '',
            periodKind: String(rule.periodKind || 'monthly_utc').toLowerCase(),
            grantAmount: String(rule.grantAmount ?? 0),
            isEnabled: !!rule.isEnabled,
            applyToAllAuthors: !!rule.applyToAllAuthors,
            // Khi applyToAllAuthors = true, BE có thể trả selectedUserIds rỗng.
            // Fill đủ danh sách để UI tick hết ở panel bên dưới.
            selectedUserIds: rule.applyToAllAuthors ? Array.from(new Set(allAuthorIds)) : selectedIdsFromRule,
        });
    };

    const onDeleteAutoGrantRule = async (ruleId) => {
        if (!ruleId) return;
        if (typeof window !== 'undefined' && !window.confirm('Xóa quy tắc này?')) return;
        try {
            setAutoGrantBusyRuleId(ruleId);
            setError('');
            setSuccess('');
            await deleteAdminAuthorAiTokenAutoGrantRule(ruleId);
            setSuccess('Đã xóa quy tắc tự gia hạn token.');
            if (autoGrantEditingRuleId === ruleId) resetAutoGrantForm();
            await loadAutoGrantRules();
        } catch (e) {
            setError(e?.response?.data?.message || e?.message || 'Không xóa được quy tắc tự gia hạn token.');
        } finally {
            setAutoGrantBusyRuleId(null);
        }
    };

    const onRunNowAutoGrantRule = async (ruleId) => {
        if (!ruleId) return;
        try {
            setAutoGrantBusyRuleId(ruleId);
            setError('');
            setSuccess('');
            const res = await runNowAdminAuthorAiTokenAutoGrantRule(ruleId);
            const usersUpdated = res?.usersUpdated ?? res?.UsersUpdated ?? 0;
            setSuccess(`Đã chạy quy tắc. Số tài khoản đã cập nhật: ${usersUpdated}.`);
            await loadAutoGrantRules();
        } catch (e) {
            setError(e?.response?.data?.message || e?.message || 'Không chạy được quy tắc tự gia hạn token.');
        } finally {
            setAutoGrantBusyRuleId(null);
        }
    };

    const loadGenerationLogs = async (nextFilter = genFilter) => {
        try {
            setGenLoading(true);
            const baseQuery = {
                fromUtc: nextFilter.fromUtc ? new Date(nextFilter.fromUtc).toISOString() : undefined,
                toUtc: nextFilter.toUtc ? new Date(nextFilter.toUtc).toISOString() : undefined,
                actionType: nextFilter.actionType || undefined,
                modelName: nextFilter.modelName || undefined,
                status: nextFilter.status || undefined,
            };
            const [reqResult, dailyResult] = await Promise.allSettled([
                (async () => {
                    // Gộp theo tài khoản phải dựa trên toàn bộ request trong bộ lọc, không phải từng trang.
                    const collect = [];
                    const apiPageSize = 200;
                    let page = 1;
                    let totalCount = 0;
                    while (true) {
                        const reqRes = await getAdminAiRequestLogs({ ...baseQuery, page, pageSize: apiPageSize });
                        const rows = pickArrayLike(
                            reqRes?.items,
                            reqRes?.Items,
                            reqRes?.data?.items,
                            reqRes?.Data?.Items
                        );
                        const rowsArray = Array.isArray(rows) ? rows : [];
                        totalCount =
                            reqRes?.totalCount ??
                            reqRes?.TotalCount ??
                            reqRes?.data?.totalCount ??
                            reqRes?.Data?.TotalCount ??
                            rowsArray.length;
                        collect.push(...rowsArray);
                        if (rowsArray.length === 0 || collect.length >= Number(totalCount || 0)) break;
                        page += 1;
                        if (page > 200) break;
                    }
                    return { rows: collect, totalCount: Number(totalCount || collect.length) };
                })(),
                getAdminAiGenerationsDaily(baseQuery),
            ]);

            if (reqResult.status === 'fulfilled') {
                const reqRows = Array.isArray(reqResult.value?.rows) ? reqResult.value.rows : [];
                setGenerationRows(reqRows);
                setGenerationTotal(Number(reqResult.value?.totalCount) || reqRows.length);
            } else {
                setGenerationRows([]);
                setGenerationTotal(0);
                setError(reqResult.reason?.response?.data?.message || reqResult.reason?.message || 'Không tải được danh sách requests.');
            }

            if (dailyResult.status === 'fulfilled') {
                const dailyRes = dailyResult.value;
                const daily = pickArrayLike(
                    dailyRes,
                    dailyRes?.days,
                    dailyRes?.Days,
                    dailyRes?.items,
                    dailyRes?.Items,
                    dailyRes?.data?.days,
                    dailyRes?.Data?.Days
                );
                setDailyRows(Array.isArray(daily) ? daily : []);
            } else {
                // Không chặn bảng log nếu endpoint chart lỗi.
                setDailyRows([]);
            }
        } catch (e) {
            setError(e?.response?.data?.message || e?.message || 'Không tải được log Generations.');
        } finally {
            setGenLoading(false);
        }
    };

    useEffect(() => {
        loadGenerationLogs(genFilter);
        loadAutoGrantRules();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    return (
        <div className="p-6 space-y-6">
            <div className="flex items-center justify-between gap-3">
                <div className="flex items-center gap-3">
                    <div className="w-10 h-10 rounded-xl bg-emerald-100 flex items-center justify-center text-emerald-600">
                        <Brain className="w-5 h-5" />
                    </div>
                    <div>
                        <h1 className="text-lg md:text-xl font-bold text-slate-900">
                            Cấu hình hệ thống
                        </h1>
                    </div>
                </div>
            </div>

            <div className="space-y-5">
                {/* Giới hạn sử dụng AI & Danh sách từ cấm */}
                <section className="bg-white rounded-xl border border-[#c9f0d8] shadow-sm p-5 space-y-4">
                    <div className="flex items-start justify-between gap-4">
                        <div>
                            <h2 className="text-sm font-semibold text-slate-900">
                                Log AI Admin &amp; Từ cấm
                            </h2>
                        </div>
                    </div>

                    <div className="space-y-4">
                        {/* Auto grant rules */}
                        <div className="rounded-xl border border-[#c9f0d8] bg-white p-4 space-y-3">
                            <div className="flex flex-wrap items-center justify-between gap-2">
                                <div className="space-y-1">
                                    <p className="text-[12px] font-semibold text-slate-900">Gia hạn token AI tự động (tác giả)</p>
                                </div>
                                <button
                                    type="button"
                                    onClick={loadAutoGrantRules}
                                    disabled={autoGrantLoading}
                                    className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-[11px] font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-60"
                                >
                                    {autoGrantLoading ? 'Đang tải…' : 'Làm mới'}
                                </button>
                            </div>

                            <div className="rounded-lg border border-slate-200 bg-slate-50 p-3 space-y-2">
                                <div className="grid grid-cols-1 md:grid-cols-5 gap-2">
                                    <input
                                        type="text"
                                        value={autoGrantForm.displayName}
                                        onChange={(e) => setAutoGrantForm((p) => ({ ...p, displayName: e.target.value }))}
                                        placeholder="Tên quy tắc (tuỳ chọn)"
                                        className="md:col-span-2 rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs text-slate-900"
                                    />
                                    <select
                                        value={autoGrantForm.periodKind}
                                        onChange={(e) => setAutoGrantForm((p) => ({ ...p, periodKind: e.target.value }))}
                                        className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs text-slate-900"
                                    >
                                        <option value="daily_utc">Ngày (UTC)</option>
                                        <option value="weekly_utc">Tuần (UTC)</option>
                                        <option value="monthly_utc">Tháng (UTC)</option>
                                    </select>
                                    <input
                                        type="text"
                                        value={autoGrantLimitFieldLabelVi(autoGrantLimitFieldFromPeriodKind(autoGrantForm.periodKind))}
                                        readOnly
                                        className="rounded-lg border border-slate-300 bg-slate-100 px-3 py-2 text-xs text-slate-700"
                                    />
                                    <input
                                        type="number"
                                        min={1}
                                        step={1}
                                        value={autoGrantForm.grantAmount}
                                        onChange={(e) => setAutoGrantForm((p) => ({ ...p, grantAmount: e.target.value }))}
                                        placeholder="Số token cộng"
                                        className={`rounded-lg border bg-white px-3 py-2 text-xs text-slate-900 ${autoGrantFormError ? 'border-red-400' : 'border-slate-300'}`}
                                    />
                                </div>
                                <div className="flex flex-wrap items-center gap-4">
                                    <label className="inline-flex items-center gap-2 text-xs text-slate-700">
                                        <input
                                            type="checkbox"
                                            checked={autoGrantForm.isEnabled}
                                            onChange={(e) => setAutoGrantForm((p) => ({ ...p, isEnabled: e.target.checked }))}
                                        />
                                        Bật quy tắc
                                    </label>
                                    <label className="inline-flex items-center gap-2 text-xs text-slate-700">
                                        <input
                                            type="checkbox"
                                            checked={autoGrantForm.applyToAllAuthors}
                                            onChange={(e) => onToggleApplyAllAuthors(e.target.checked)}
                                        />
                                        Tất cả tác giả
                                    </label>
                                    <input
                                        type="search"
                                        value={autoGrantAuthorFilter}
                                        onChange={(e) => setAutoGrantAuthorFilter(e.target.value)}
                                        placeholder="Lọc tác giả (email / biệt danh / GUID)"
                                        className="w-full md:w-80 rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs text-slate-900"
                                    />
                                </div>

                                <div className={`max-h-44 overflow-y-auto rounded-lg border p-2 ${autoGrantForm.applyToAllAuthors ? 'border-slate-100 bg-slate-50' : 'border-slate-200 bg-white'}`}>
                                    {autoGrantFilteredAuthors.length === 0 ? (
                                        <p className="text-[11px] text-slate-500">Không có tác giả khớp bộ lọc.</p>
                                    ) : autoGrantFilteredAuthors.map((u) => {
                                        const id = String(u?.id || '');
                                        const selected = (autoGrantForm.selectedUserIds || []).some((x) => String(x) === id);
                                        return (
                                            <label key={id} className={`flex items-start gap-2 py-1 text-xs ${autoGrantForm.applyToAllAuthors ? 'text-slate-400' : 'text-slate-700'}`}>
                                                <input
                                                    type="checkbox"
                                                    checked={selected}
                                                    onChange={(e) => onToggleAutoGrantAuthor(id, e.target.checked)}
                                                />
                                                <span className="break-all">
                                                    {(u?.nickname || '').trim() ? `${u.nickname} · ${u?.email || '—'}` : (u?.email || id)}
                                                    <span className="block text-[10px] text-slate-400">{id}</span>
                                                </span>
                                            </label>
                                        );
                                    })}
                                </div>
                                {autoGrantForm.applyToAllAuthors && (
                                    <p className="text-[11px] text-slate-500">Đang bật "Tất cả tác giả" nên danh sách chỉ hiển thị để tham chiếu, không cần chọn.</p>
                                )}
                                {autoGrantFormError && (
                                    <p className="text-[11px] text-red-600">{autoGrantFormError}</p>
                                )}
                                <div className="flex flex-wrap items-center gap-2">
                                    <button
                                        type="button"
                                        onClick={onSubmitAutoGrantRule}
                                        disabled={savingAutoGrant || !!autoGrantFormError}
                                        className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-[11px] font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-60"
                                    >
                                        {savingAutoGrant ? 'Đang lưu…' : (autoGrantEditingRuleId ? 'Cập nhật quy tắc' : 'Tạo quy tắc')}
                                    </button>
                                    <button
                                        type="button"
                                        onClick={resetAutoGrantForm}
                                        className="rounded-lg border border-slate-300 bg-white px-3 py-2 text-[11px] font-semibold text-slate-700 hover:bg-slate-50"
                                    >
                                        Đặt lại biểu mẫu
                                    </button>
                                    {autoGrantEditingRuleId && (
                                        <span className="text-[11px] text-slate-500">Đang sửa quy tắc: `{autoGrantEditingRuleId}`</span>
                                    )}
                                </div>
                            </div>

                            <div className="overflow-x-auto rounded-lg border border-slate-200">
                                <table className="min-w-full text-xs">
                                    <thead className="bg-slate-50 text-slate-600">
                                        <tr>
                                            <th className="px-2 py-2 text-left">Tên</th>
                                            <th className="px-2 py-2 text-left">Bật</th>
                                            <th className="px-2 py-2 text-left">Chu kỳ</th>
                                            <th className="px-2 py-2 text-left">Cột</th>
                                            <th className="px-2 py-2 text-right">+ Token</th>
                                            <th className="px-2 py-2 text-left">Phạm vi</th>
                                            <th className="px-2 py-2 text-left">Đã chạy</th>
                                            <th className="px-2 py-2 text-right">Hành động</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {autoGrantLoading ? (
                                            <tr><td colSpan={8} className="px-2 py-4 text-center text-slate-500">Đang tải...</td></tr>
                                        ) : autoGrantRules.length === 0 ? (
                                            <tr><td colSpan={8} className="px-2 py-4 text-center text-slate-500">Chưa có rule nào.</td></tr>
                                        ) : autoGrantRules.map((r) => (
                                            <tr key={r.id} className="border-t border-slate-100">
                                                <td className="px-2 py-2 text-slate-900">
                                                    {r.displayName || '—'}
                                                    <div className="text-[10px] text-slate-400 break-all">{r.id}</div>
                                                </td>
                                                <td className="px-2 py-2">{r.isEnabled ? 'Bật' : 'Tắt'}</td>
                                                <td className="px-2 py-2"><code>{autoGrantPeriodKindLabelVi(r.periodKind)}</code></td>
                                                <td className="px-2 py-2"><code>{autoGrantLimitFieldLabelVi(r.grantLimitField || autoGrantLimitFieldFromPeriodKind(r.periodKind))}</code></td>
                                                <td className="px-2 py-2 text-right font-semibold">{fmtIntOrDash(r.grantAmount)}</td>
                                                <td className="px-2 py-2">
                                                    {r.applyToAllAuthors ? 'Tất cả tác giả' : `${(r.selectedUserIds || []).length} tác giả`}
                                                </td>
                                                <td className="px-2 py-2">{r.lastRunAtUtc ? formatDateTimeVi(r.lastRunAtUtc) : '—'}</td>
                                                <td className="px-2 py-2 text-right">
                                                    <div className="inline-flex items-center gap-1">
                                                        <button
                                                            type="button"
                                                            onClick={() => onEditAutoGrantRule(r)}
                                                            className="rounded border border-blue-300 bg-blue-50 px-2 py-1 text-[10px] font-semibold text-blue-700 hover:bg-blue-100"
                                                        >
                                                            Sửa
                                                        </button>
                                                        <button
                                                            type="button"
                                                            onClick={() => onRunNowAutoGrantRule(r.id)}
                                                            disabled={autoGrantBusyRuleId === r.id}
                                                            className="rounded border border-amber-300 bg-amber-50 px-2 py-1 text-[10px] font-semibold text-amber-700 hover:bg-amber-100 disabled:opacity-60"
                                                        >
                                                            Chạy ngay
                                                        </button>
                                                        <button
                                                            type="button"
                                                            onClick={() => onDeleteAutoGrantRule(r.id)}
                                                            disabled={autoGrantBusyRuleId === r.id}
                                                            className="rounded border border-red-300 bg-red-50 px-2 py-1 text-[10px] font-semibold text-red-700 hover:bg-red-100 disabled:opacity-60"
                                                        >
                                                            Xóa
                                                        </button>
                                                    </div>
                                                </td>
                                            </tr>
                                        ))}
                                    </tbody>
                                </table>
                            </div>
                        </div>

                        {/* Generations logs from DB */}
                        <div className="rounded-xl border border-[#c9f0d8] bg-white p-4 space-y-3">
                            <p className="text-[12px] font-semibold text-slate-900">Lượt gọi AI (nhật ký chi tiết từng lượt)</p>
                            <div className="grid grid-cols-1 gap-2">
                                <input
                                    type="text"
                                    value={generationUserSearch}
                                    onChange={(e) => {
                                        setGenerationUserSearch(e.target.value);
                                        setGenFilter((p) => ({ ...p, page: 1 }));
                                    }}
                                    placeholder="Tìm theo tài khoản (email)"
                                    className="rounded-lg border border-slate-300 bg-white px-2 py-2 text-xs"
                                />
                            </div>

                            <div className="rounded-lg border border-slate-200 bg-slate-50 p-3">
                                <p className="text-[11px] font-semibold text-slate-700 mb-1">Số request theo ngày (UTC)</p>
                                {dailyChartRows.length === 0 ? (
                                    <span className="text-[11px] text-slate-500">Không có dữ liệu</span>
                                ) : (
                                    <div className="space-y-2">
                                        {dailyChartRows.map((row) => (
                                            <div key={row.day} className="grid grid-cols-[90px_1fr_60px] items-center gap-2">
                                                <span className="text-[10px] text-slate-600">{row.day}</span>
                                                <div className="h-4 rounded bg-indigo-100 overflow-hidden">
                                                    <div
                                                        className="h-full bg-indigo-500"
                                                        style={{ width: `${row.pct}%` }}
                                                        title={`${row.day}: ${row.count.toLocaleString('vi-VN')} requests`}
                                                    />
                                                </div>
                                                <span className="text-[10px] font-semibold text-slate-700 text-right">
                                                    {row.count.toLocaleString('vi-VN')}
                                                </span>
                                            </div>
                                        ))}
                                    </div>
                                )}
                            </div>

                            <div className="overflow-auto rounded-lg border border-[#c9f0d8]">
                                <table className="w-full text-[11px]">
                                    <thead className="bg-[#f0faf5] text-[#047857] uppercase">
                                        <tr>
                                            <th className="px-2 py-2 text-left">Tài khoản</th>
                                            <th className="px-2 py-2 text-right">Tokens (in→out)</th>
                                            <th className="px-2 py-2 text-right">Chi phí</th>
                                            <th className="px-2 py-2 text-center">Số lượt</th>
                                            <th className="px-2 py-2 text-center">Chi tiết</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {generationUserPagedRows.length === 0 ? (
                                            <tr><td className="px-2 py-3 text-center text-slate-500" colSpan={5}>{genLoading ? 'Đang tải...' : 'Không có dữ liệu'}</td></tr>
                                        ) : generationUserPagedRows.map((r, idx) => {
                                            return (
                                                <tr key={`${r.userEmail}-${idx}`} className="border-t border-[#c9f0d8]">
                                                    <td className="px-2 py-2">{r.userEmail}</td>
                                                    <td className="px-2 py-2 text-right">{`${Number(r.totalPromptTokens || 0).toLocaleString('vi-VN')} → ${Number(r.totalCompletionTokens || 0).toLocaleString('vi-VN')}`}</td>
                                                    <td className="px-2 py-2 text-right">{r.hasAnyCost ? formatUsdNullable(r.totalCostUsd) : '—'}</td>
                                                    <td className="px-2 py-2 text-center">{Number(r.requestCount || 0).toLocaleString('vi-VN')}</td>
                                                    <td className="px-2 py-2 text-center">
                                                        <button
                                                            type="button"
                                                            className="rounded border border-slate-300 bg-white px-2 py-1 text-[10px] font-semibold hover:bg-slate-50"
                                                            onClick={() => setSelectedLogUserEmail(r.userEmail)}
                                                        >
                                                            Xem
                                                        </button>
                                                    </td>
                                                </tr>
                                            );
                                        })}
                                    </tbody>
                                </table>
                            </div>
                            <div className="flex items-center justify-between">
                                <p className="text-[11px] text-slate-500">Tổng: {Number(generationTotal).toLocaleString('vi-VN')} request</p>
                                <div className="flex items-center gap-2">
                                    <button
                                        type="button"
                                        className="rounded border border-slate-300 bg-white px-2 py-1 text-[11px] disabled:opacity-50"
                                        disabled={genFilter.page <= 1}
                                        onClick={() => {
                                            const next = { ...genFilter, page: Math.max(1, genFilter.page - 1) };
                                            setGenFilter(next);
                                        }}
                                    >
                                        Trước
                                    </button>
                                    <span className="text-[11px] text-slate-600">Trang {Math.min(genFilter.page, generationUserTotalPages)}/{generationUserTotalPages}</span>
                                    <button
                                        type="button"
                                        className="rounded border border-slate-300 bg-white px-2 py-1 text-[11px] disabled:opacity-50"
                                        disabled={genFilter.page >= generationUserTotalPages}
                                        onClick={() => {
                                            const next = { ...genFilter, page: Math.min(generationUserTotalPages, genFilter.page + 1) };
                                            setGenFilter(next);
                                        }}
                                    >
                                        Sau
                                    </button>
                                </div>
                            </div>
                            {generationDetail ? (
                                <div className="fixed inset-0 z-[1000] bg-black/40 flex items-center justify-center p-4">
                                    <div className="w-full max-w-3xl max-h-[85vh] overflow-hidden rounded-xl border border-slate-200 bg-white shadow-2xl flex flex-col">
                                        <div className="px-4 py-3 border-b border-slate-200 flex items-center justify-between">
                                            <p className="text-sm font-semibold text-slate-900">Chi tiết lượt gọi (OpenRouter): {generationDetailId || '—'}</p>
                                            <button
                                                type="button"
                                                className="rounded border border-slate-300 bg-white px-2 py-1 text-[11px] font-semibold hover:bg-slate-50"
                                                onClick={() => {
                                                    setGenerationDetail(null);
                                                    setGenerationDetailId('');
                                                }}
                                            >
                                                Đóng
                                            </button>
                                        </div>
                                        <div className="p-4 overflow-auto">
                                            <pre className="text-[10px] whitespace-pre-wrap break-all text-slate-700">
                                                {JSON.stringify(generationDetail, null, 2)}
                                            </pre>
                                        </div>
                                    </div>
                                </div>
                            ) : null}
                            {selectedLogUserEmail ? (
                                <div className="fixed inset-0 z-[1000] bg-black/40 flex items-center justify-center p-4">
                                    <div className="w-full max-w-6xl max-h-[85vh] overflow-hidden rounded-xl border border-slate-200 bg-white shadow-2xl flex flex-col">
                                        <div className="px-4 py-3 border-b border-slate-200 flex items-center justify-between">
                                            <p className="text-sm font-semibold text-slate-900">Chi tiết tài khoản: {selectedLogUserEmail}</p>
                                            <button
                                                type="button"
                                                className="rounded border border-slate-300 bg-white px-2 py-1 text-[11px] font-semibold hover:bg-slate-50"
                                                onClick={() => setSelectedLogUserEmail('')}
                                            >
                                                Đóng
                                            </button>
                                        </div>
                                        <div className="p-4 overflow-auto">
                                            <table className="w-full text-[11px]">
                                                <thead className="bg-[#f0faf5] text-[#047857] uppercase">
                                                    <tr>
                                                        <th className="px-2 py-2 text-left">Thời gian</th>
                                                        <th className="px-2 py-2 text-left">Model</th>
                                                        <th className="px-2 py-2 text-left">Loại hành động</th>
                                                        <th className="px-2 py-2 text-right">Tokens (in→out)</th>
                                                        <th className="px-2 py-2 text-right">Chi phí</th>
                                                        <th className="px-2 py-2 text-left">Trạng thái</th>
                                                    </tr>
                                                </thead>
                                                <tbody>
                                                    {selectedUserLogPagedRows.length === 0 ? (
                                                        <tr><td className="px-2 py-3 text-center text-slate-500" colSpan={6}>Không có dữ liệu</td></tr>
                                                    ) : selectedUserLogPagedRows.map((r, idx) => {
                                                        return (
                                                            <tr key={idx} className="border-t border-[#c9f0d8]">
                                                                <td className="px-2 py-2">{formatDateTimeVi(r?.createdAtUtc ?? r?.CreatedAtUtc ?? r?.occurredAtUtc ?? r?.OccurredAtUtc ?? r?.createdAt ?? r?.CreatedAt)}</td>
                                                                <td className="px-2 py-2">{r?.modelName ?? r?.ModelName ?? '—'}</td>
                                                                <td className="px-2 py-2">{r?.actionType ?? r?.ActionType ?? '—'}</td>
                                                                <td className="px-2 py-2 text-right">{`${Number(r?.promptTokens ?? r?.PromptTokens ?? r?.inputTokens ?? r?.InputTokens ?? 0).toLocaleString('vi-VN')} → ${Number(r?.completionTokens ?? r?.CompletionTokens ?? r?.outputTokens ?? r?.OutputTokens ?? 0).toLocaleString('vi-VN')}`}</td>
                                                                <td className="px-2 py-2 text-right">{formatUsdNullable(r?.costUsd ?? r?.CostUsd)}</td>
                                                                <td className="px-2 py-2">{r?.status ?? r?.Status ?? '—'}</td>
                                                            </tr>
                                                        );
                                                    })}
                                                </tbody>
                                            </table>
                                            <div className="mt-3 flex items-center justify-between">
                                                <p className="text-[11px] text-slate-500">Tổng: {selectedUserLogs.length.toLocaleString('vi-VN')} bản ghi</p>
                                                <div className="flex items-center gap-2">
                                                    <button
                                                        type="button"
                                                        className="rounded border border-slate-300 bg-white px-2 py-1 text-[11px] disabled:opacity-50"
                                                        disabled={selectedUserLogPage <= 1}
                                                        onClick={() => setSelectedUserLogPage((p) => Math.max(1, p - 1))}
                                                    >
                                                        Trước
                                                    </button>
                                                    <span className="text-[11px] text-slate-600">
                                                        Trang {Math.min(selectedUserLogPage, selectedUserLogTotalPages)}/{selectedUserLogTotalPages}
                                                    </span>
                                                    <button
                                                        type="button"
                                                        className="rounded border border-slate-300 bg-white px-2 py-1 text-[11px] disabled:opacity-50"
                                                        disabled={selectedUserLogPage >= selectedUserLogTotalPages}
                                                        onClick={() => setSelectedUserLogPage((p) => Math.min(selectedUserLogTotalPages, p + 1))}
                                                    >
                                                        Sau
                                                    </button>
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            ) : null}
                        </div>

                        {/* Banned words */}
                        <div className="rounded-xl border border-[#c9f0d8] bg-white p-4">
                            <div className="flex items-start justify-between gap-3">
                                <div className="space-y-1">
                                    <p className="text-[12px] font-semibold text-slate-900">
                                        Danh sách từ cấm
                                    </p>
                                </div>
                                <div className="inline-flex items-center rounded-full bg-slate-100 px-2 py-1 text-[10px] font-semibold text-slate-700">
                                    {bannedWords.length} mục
                                </div>
                            </div>

                            {(error || success) && (
                                <div
                                    className={`mt-3 rounded-lg border px-3 py-2 text-[11px] ${error
                                        ? 'border-red-200 bg-red-50 text-red-700'
                                        : 'border-emerald-200 bg-emerald-50 text-emerald-700'
                                        }`}
                                >
                                    {error || success}
                                </div>
                            )}

                            <div className="mt-3 flex gap-2">
                                <input
                                    type="text"
                                    value={bannedWordInput}
                                    onChange={(e) => setBannedWordInput(e.target.value)}
                                    placeholder="Nhập từ/cụm từ cần chặn…"
                                    className="flex-1 rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs text-slate-900 placeholder:text-slate-400 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary"
                                />
                                <button
                                    type="button"
                                    disabled={savingWord || loadingConfig}
                                    onClick={() => {
                                        (async () => {
                                            const word = bannedWordInput.trim();
                                            if (!word) return;
                                            try {
                                                setError('');
                                                setSuccess('');
                                                setSavingWord(true);
                                                const created = await addAdminBannedWord(word, 'BannedWord');
                                                setBannedWords((list) => [created, ...list]);
                                                setBannedWordInput('');
                                                setSuccess('Đã thêm từ cấm.');
                                            } catch (e) {
                                                const msg =
                                                    e?.response?.data?.message ||
                                                    e?.message ||
                                                    'Không thêm được từ cấm. Vui lòng thử lại.';
                                                setError(msg);
                                            } finally {
                                                setSavingWord(false);
                                            }
                                        })();
                                    }}
                                    className="px-4 py-2 rounded-lg bg-primary text-white text-[11px] font-semibold hover:bg-primary/90"
                                >
                                    {savingWord ? 'Đang thêm…' : 'Thêm'}
                                </button>
                            </div>

                            <div className="mt-3 overflow-hidden rounded-lg border border-[#c9f0d8]">
                                {bannedWords.length === 0 ? (
                                    <div className="bg-[#f8fdfb] px-4 py-6">
                                        <p className="text-[12px] font-semibold text-slate-800">
                                            Chưa có từ cấm nào
                                        </p>
                                        <p className="mt-1 text-[11px] text-slate-500">
                                            Thêm từ/cụm từ để hệ thống cảnh báo khi phát hiện trong nội dung.
                                        </p>
                                    </div>
                                ) : (
                                    <>
                                        <table className="w-full table-fixed border-collapse text-[11px] text-slate-800">
                                            <colgroup>
                                                <col className="w-1/2" />
                                                <col className="w-1/3" />
                                                <col className="w-1/6" />
                                            </colgroup>
                                            <thead>
                                                <tr className="border-b border-[#c9f0d8] bg-[#f0faf5]">
                                                    <th className="px-3 py-2 text-left text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">
                                                        Từ/cụm từ
                                                    </th>
                                                    <th className="px-3 py-2 text-left text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">
                                                        Nhóm
                                                    </th>
                                                    <th className="px-3 py-2 text-right text-[0.72rem] font-bold uppercase tracking-wide text-[#047857]">
                                                        Xóa
                                                    </th>
                                                </tr>
                                            </thead>
                                        </table>
                                        <div className="max-h-60 overflow-y-auto border-t border-[#c9f0d8]">
                                            <table className="w-full table-fixed border-collapse text-[11px]">
                                                <colgroup>
                                                    <col className="w-1/2" />
                                                    <col className="w-1/3" />
                                                    <col className="w-1/6" />
                                                </colgroup>
                                                <tbody>
                                                    {bannedWords.map((bw, idx) => {
                                                        const isSensitive = String(bw.category || '')
                                                            .toLowerCase()
                                                            .includes('sensitive');
                                                        return (
                                                            <tr
                                                                key={bw.id}
                                                                className="border-t border-[#c9f0d8] bg-white transition-colors first:border-t-0 hover:bg-[#f7fcf9]"
                                                            >
                                                                <td className="px-3 py-2 font-medium text-slate-800">
                                                                    {bw.word}
                                                                </td>
                                                                <td className="px-3 py-2">
                                                                    <span
                                                                        className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-semibold ring-1 ${isSensitive
                                                                            ? 'bg-amber-50 text-amber-700 ring-amber-200'
                                                                            : 'bg-sky-50 text-sky-700 ring-sky-200'
                                                                            }`}
                                                                    >
                                                                        {bw.category}
                                                                    </span>
                                                                </td>
                                                                <td className="px-3 py-2 text-right">
                                                                    <button
                                                                        type="button"
                                                                        disabled={deletingWordId === bw.id}
                                                                        onClick={() =>
                                                                            (async () => {
                                                                                try {
                                                                                    setError('');
                                                                                    setSuccess('');
                                                                                    setDeletingWordId(bw.id);
                                                                                    await deleteAdminBannedWord(bw.id);
                                                                                    setBannedWords((list) =>
                                                                                        list.filter((x) => x.id !== bw.id)
                                                                                    );
                                                                                    setSuccess('Đã xóa từ cấm.');
                                                                                } catch (e) {
                                                                                    const msg =
                                                                                        e?.response?.data?.message ||
                                                                                        e?.message ||
                                                                                        'Không xóa được từ cấm. Vui lòng thử lại.';
                                                                                    setError(msg);
                                                                                } finally {
                                                                                    setDeletingWordId(null);
                                                                                }
                                                                            })()
                                                                        }
                                                                        className="inline-flex items-center justify-center rounded-lg px-2 py-1 text-[10px] font-semibold text-red-600 hover:bg-red-50"
                                                                    >
                                                                        {deletingWordId === bw.id ? 'Đang xóa…' : 'Xóa'}
                                                                    </button>
                                                                </td>
                                                            </tr>
                                                        );
                                                    })}
                                                </tbody>
                                            </table>
                                        </div>
                                    </>
                                )}
                            </div>
                        </div>
                    </div>
                </section>
            </div>
        </div>
    );
}

