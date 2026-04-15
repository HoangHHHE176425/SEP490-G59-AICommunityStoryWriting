import { useEffect, useMemo, useState } from 'react';
import { Brain } from 'lucide-react';
import {
    addAdminBannedWord,
    deleteAdminBannedWord,
    getAdminAiUsageLimit,
    getAdminBannedWords,
    setAdminAiUsageLimit,
} from '../../../api/admin/aiConfigApi';

export function AiConfig() {
    // Limits & banned words
    const [dailyLimit, setDailyLimit] = useState(3);
    const [bannedWordInput, setBannedWordInput] = useState('');
    const [bannedWords, setBannedWords] = useState([]);

    const [loadingConfig, setLoadingConfig] = useState(true);
    const [savingLimit, setSavingLimit] = useState(false);
    const [savingWord, setSavingWord] = useState(false);
    const [deletingWordId, setDeletingWordId] = useState(null);
    const [error, setError] = useState('');
    const [success, setSuccess] = useState('');

    const limitValue = useMemo(() => {
        const n = Number(dailyLimit);
        if (!Number.isFinite(n)) return 0;
        return Math.max(1, Math.min(100, Math.trunc(n)));
    }, [dailyLimit]);

    useEffect(() => {
        let mounted = true;
        async function load() {
            try {
                setError('');
                setSuccess('');
                setLoadingConfig(true);

                const [limitRes, wordsRes] = await Promise.all([
                    getAdminAiUsageLimit(),
                    getAdminBannedWords('BannedWord'),
                ]);

                if (!mounted) return;
                const max = Number(limitRes?.maxRequestsPerDay);
                if (Number.isFinite(max) && max > 0) setDailyLimit(max);
                setBannedWords(Array.isArray(wordsRes) ? wordsRes : []);
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
                        <p className="text-xs md:text-sm text-slate-500">
                            Thiết lập các cấu hình hệ thống cho nền tảng.
                        </p>
                    </div>
                </div>
            </div>

            <div className="space-y-5">
                {/* Giới hạn sử dụng AI & Danh sách từ cấm */}
                <section className="bg-white rounded-xl border border-[#c9f0d8] shadow-sm p-5 space-y-4">
                    <div className="flex items-start justify-between gap-4">
                        <div>
                            <h2 className="text-sm font-semibold text-slate-900">
                                Giới hạn sử dụng AI &amp; Từ cấm
                            </h2>
                            <p className="mt-1 text-[11px] text-slate-500">
                                Thiết lập kiểm soát sử dụng AI và quy tắc nội dung để giảm spam và hạn chế từ nhạy cảm.
                            </p>
                        </div>
                    </div>

                    <div className="space-y-4">
                        {/* Daily limit */}
                        <div className="rounded-xl border border-[#c9f0d8] bg-white p-4">
                            <div className="flex items-start justify-between gap-3">
                                <div className="space-y-1">
                                    <p className="text-[12px] font-semibold text-slate-900">
                                        Giới hạn theo ngày
                                    </p>
                                    <p className="text-[11px] text-slate-500">
                                        Mỗi tác giả có tối đa một số lượt dùng AI trong 24h. Hết lượt sẽ tự mở lại vào ngày hôm sau.
                                    </p>
                                </div>
                                <div className="inline-flex items-center rounded-full bg-emerald-50 px-2 py-1 text-[10px] font-semibold text-emerald-700 ring-1 ring-emerald-200">
                                    {limitValue} lượt/ngày
                                </div>
                            </div>

                            <div className="mt-3 flex flex-wrap items-center gap-2">
                                <input
                                    type="number"
                                    min={1}
                                    max={100}
                                    value={dailyLimit}
                                    onChange={(e) => setDailyLimit(Number(e.target.value) || 0)}
                                    className="w-28 rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary"
                                />
                                <span className="text-[11px] text-slate-500">lượt / ngày</span>
                                <button
                                    type="button"
                                    disabled={savingLimit || loadingConfig}
                                    onClick={async () => {
                                        try {
                                            setError('');
                                            setSuccess('');
                                            setSavingLimit(true);
                                            const res = await setAdminAiUsageLimit(limitValue);
                                            const max = Number(res?.maxRequestsPerDay);
                                            if (Number.isFinite(max) && max > 0) setDailyLimit(max);
                                            setSuccess('Đã lưu giới hạn sử dụng AI.');
                                        } catch (e) {
                                            const msg =
                                                e?.response?.data?.message ||
                                                e?.message ||
                                                'Không lưu được giới hạn. Vui lòng thử lại.';
                                            setError(msg);
                                        } finally {
                                            setSavingLimit(false);
                                        }
                                    }}
                                    className="ml-auto rounded-lg border border-slate-300 bg-white px-3 py-2 text-[11px] font-semibold text-slate-700 hover:bg-slate-50 disabled:opacity-60"
                                >
                                    {savingLimit ? 'Đang lưu…' : 'Lưu'}
                                </button>
                            </div>
                        </div>

                        {/* Banned words */}
                        <div className="rounded-xl border border-[#c9f0d8] bg-white p-4">
                            <div className="flex items-start justify-between gap-3">
                                <div className="space-y-1">
                                    <p className="text-[12px] font-semibold text-slate-900">
                                        Danh sách từ cấm
                                    </p>
                                    <p className="text-[11px] text-slate-500">
                                        Khi tác giả lưu chương, hệ thống sẽ cảnh báo nếu phát hiện các từ/cụm từ trong danh sách này.
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

