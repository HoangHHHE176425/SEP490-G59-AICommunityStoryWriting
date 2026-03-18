import { useEffect, useMemo, useState } from 'react';
import { Brain, Sparkles } from 'lucide-react';
import {
    addAdminBannedWord,
    deleteAdminBannedWord,
    getAdminAiUsageLimit,
    getAdminBannedWords,
    setAdminAiUsageLimit,
} from '../../../api/admin/aiConfigApi';

export function AiConfig() {
    // Feature toggles for APIs
    const [featureSuggestNext, setFeatureSuggestNext] = useState(true);
    const [featureCoCreate, setFeatureCoCreate] = useState(true);
    const [featureCheckConsistency, setFeatureCheckConsistency] = useState(true);
    const [featureCheckChapter, setFeatureCheckChapter] = useState(true);
    const [featureCompareChapter, setFeatureCompareChapter] = useState(true);

    // Limits & banned words (mock only – chưa gọi API thật)
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
                            Cấu hình AI cho nền tảng
                        </h1>
                        <p className="text-xs md:text-sm text-slate-500">
                            Nền tảng đang dùng Ollama. Thiết lập trí nhớ truyện, tìm kiếm theo ngữ cảnh, viết chung với AI và giới hạn sử dụng. Giao diện mẫu, chưa kết nối hệ thống.
                        </p>
                    </div>
                </div>
            </div>

            <div className="space-y-5">
                    {/* Các dịch vụ AI cho tác giả */}
                    <section className="bg-white rounded-xl border border-slate-200 shadow-sm p-5 space-y-4">
                        <div className="flex items-center gap-2 mb-1">
                            <Sparkles className="w-4 h-4 text-primary" />
                            <h2 className="text-sm font-semibold text-slate-900">
                                Các dịch vụ AI cho tác giả
                            </h2>
                        </div>
                        <p className="text-[11px] text-slate-500">
                            Bật hoặc tắt từng tính năng. Tắt thì tác giả sẽ không dùng được tính năng đó trên trang viết truyện.
                        </p>

                        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-xs">
                            <button
                                type="button"
                                onClick={() => setFeatureSuggestNext((v) => !v)}
                                className={`flex flex-col items-start gap-1 rounded-xl border px-3 py-3 text-left transition-all ${
                                    featureSuggestNext
                                        ? 'border-primary bg-primary/5 shadow-sm'
                                        : 'border-slate-200 hover:border-primary/50'
                                }`}
                            >
                                <span className="text-[11px] font-semibold text-slate-800">
                                    Gợi ý viết tiếp
                                </span>
                                <p className="text-[11px] text-slate-500">
                                    AI đề xuất các hướng phát triển tiếp theo dựa trên chương gần nhất và ý tưởng hiện tại.
                                </p>
                            </button>

                            <button
                                type="button"
                                onClick={() => setFeatureCoCreate((v) => !v)}
                                className={`flex flex-col items-start gap-1 rounded-xl border px-3 py-3 text-left transition-all ${
                                    featureCoCreate
                                        ? 'border-primary bg-primary/5 shadow-sm'
                                        : 'border-slate-200 hover:border-primary/50'
                                }`}
                            >
                                <span className="text-[11px] font-semibold text-slate-800">
                                    Viết chung với AI (co-create)
                                </span>
                                <p className="text-[11px] text-slate-500">
                                    AI hỗ trợ lên dàn ý → viết bản nháp → kiểm tra nhất quán theo ngữ cảnh truyện.
                                </p>
                            </button>

                            <button
                                type="button"
                                onClick={() => setFeatureCheckConsistency((v) => !v)}
                                className={`flex flex-col items-start gap-1 rounded-xl border px-3 py-3 text-left transition-all ${
                                    featureCheckConsistency
                                        ? 'border-primary bg-primary/5 shadow-sm'
                                        : 'border-slate-200 hover:border-primary/50'
                                }`}
                            >
                                <span className="text-[11px] font-semibold text-slate-800">
                                    Kiểm tra mâu thuẫn nội dung
                                </span>
                                <p className="text-[11px] text-slate-500">
                                    Phát hiện chi tiết bất hợp lý (timeline, trạng thái nhân vật, sự kiện) trong bản nháp chương.
                                </p>
                            </button>
                        </div>

                        <div className="mt-4 border-t border-slate-200 pt-3 grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
                            <div className="space-y-2">
                                <p className="text-[11px] font-semibold text-slate-700">
                                    Khi tác giả lưu chương (hỗ trợ tự động)
                                </p>
                                <div className="grid grid-cols-1 gap-3">
                                    <button
                                        type="button"
                                        onClick={() => setFeatureCheckChapter((v) => !v)}
                                        className={`flex flex-col items-start gap-1 rounded-xl border px-3 py-3 text-left transition-all ${
                                            featureCheckChapter
                                                ? 'border-primary bg-primary/5 shadow-sm'
                                                : 'border-slate-200 hover:border-primary/50'
                                        }`}
                                    >
                                        <span className="text-[11px] font-semibold text-slate-800">
                                            Kiểm tra chính tả và từ cấm
                                        </span>
                                        <p className="text-[11px] text-slate-500">
                                            Rà soát lỗi cơ bản và cảnh báo nội dung có từ/cụm từ không phù hợp.
                                        </p>
                                    </button>
                                    <button
                                        type="button"
                                        onClick={() => setFeatureCompareChapter((v) => !v)}
                                        className={`flex flex-col items-start gap-1 rounded-xl border px-3 py-3 text-left transition-all ${
                                            featureCompareChapter
                                                ? 'border-primary bg-primary/5 shadow-sm'
                                                : 'border-slate-200 hover:border-primary/50'
                                        }`}
                                    >
                                        <span className="text-[11px] font-semibold text-slate-800">
                                            Ước lượng tỷ lệ nội dung do AI viết
                                        </span>
                                        <p className="text-[11px] text-slate-500">
                                            So sánh mức độ tương đồng giữa chương tác giả và bản AI đã sinh trước đó.
                                        </p>
                                    </button>
                                </div>
                            </div>
                        </div>

                        {/* Quy trình viết chung với AI */}
                        <div className="mt-4 border-t border-slate-200 pt-3 space-y-2 text-xs">
                            <p className="text-[11px] font-semibold text-slate-700">
                                Quy trình viết chung với AI (co-create)
                            </p>
                            <ol className="list-decimal list-inside space-y-1 text-[11px] text-slate-600">
                                <li>Bước 1 – Lên ý tưởng: AI gợi ý dàn ý chương.</li>
                                <li>Bước 2 – Viết: AI viết nháp theo dàn ý.</li>
                                <li>Bước 3 – Kiểm tra: AI đọc lại và báo mâu thuẫn (nếu có).</li>
                            </ol>
                        </div>
                    </section>

                    {/* Giới hạn sử dụng AI & Danh sách từ cấm — đặt dưới Các dịch vụ AI cho tác giả */}
                    <section className="bg-white rounded-xl border border-slate-200 shadow-sm p-5 space-y-4">
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
                            <div className="rounded-xl border border-slate-200 bg-white p-4">
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
                            <div className="rounded-xl border border-slate-200 bg-white p-4">
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
                                        className={`mt-3 rounded-lg border px-3 py-2 text-[11px] ${
                                            error
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

                                <div className="mt-3 overflow-hidden rounded-lg border border-slate-200">
                                    {bannedWords.length === 0 ? (
                                        <div className="px-4 py-6">
                                            <p className="text-[12px] font-semibold text-slate-800">
                                                Chưa có từ cấm nào
                                            </p>
                                            <p className="mt-1 text-[11px] text-slate-500">
                                                Thêm từ/cụm từ để hệ thống cảnh báo khi phát hiện trong nội dung.
                                            </p>
                                        </div>
                                    ) : (
                                        <table className="w-full text-[11px]">
                                            <thead className="bg-slate-50">
                                                <tr className="text-left text-slate-500">
                                                    <th className="px-3 py-2 font-medium">Từ/cụm từ</th>
                                                    <th className="px-3 py-2 font-medium">Nhóm</th>
                                                    <th className="px-3 py-2 font-medium text-right">Xóa</th>
                                                </tr>
                                            </thead>
                                            <tbody className="bg-white">
                                                {bannedWords.map((bw, idx) => {
                                                    const isSensitive = String(bw.category || '')
                                                        .toLowerCase()
                                                        .includes('sensitive');
                                                    return (
                                                        <tr
                                                            key={bw.id}
                                                            className={`border-t border-slate-100 ${
                                                                idx % 2 === 1 ? 'bg-slate-50/40' : ''
                                                            }`}
                                                        >
                                                            <td className="px-3 py-2 font-medium text-slate-800">
                                                                {bw.word}
                                                            </td>
                                                            <td className="px-3 py-2">
                                                                <span
                                                                    className={`inline-flex items-center rounded-full px-2 py-0.5 text-[10px] font-semibold ring-1 ${
                                                                        isSensitive
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
                                    )}
                                </div>
                            </div>
                        </div>
                    </section>
            </div>
        </div>
    );
}

