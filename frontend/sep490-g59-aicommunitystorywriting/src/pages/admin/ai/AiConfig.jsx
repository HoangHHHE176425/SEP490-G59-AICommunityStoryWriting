import { useState } from 'react';
import { Brain, SlidersHorizontal, Sparkles, ShieldCheck } from 'lucide-react';

export function AiConfig() {
    const [model, setModel] = useState('llama3');
    const [maxTokens, setMaxTokens] = useState(2048);
    const [temperature, setTemperature] = useState(0.7);

    // Story Memory Engine & RAG
    const [memoryEnabled, setMemoryEnabled] = useState(true);
    const [chunkSize, setChunkSize] = useState(1200);
    const [recentChaptersFallback, setRecentChaptersFallback] = useState(5);
    const [embeddingModel, setEmbeddingModel] = useState('text-embedding-3-small');

    // Co-create pipeline
    const [coCreateSkipReview, setCoCreateSkipReview] = useState(false);
    const [coCreateMaxRevisions, setCoCreateMaxRevisions] = useState(2);

    // Feature toggles for APIs
    const [featureSuggestNext, setFeatureSuggestNext] = useState(true);
    const [featureCoCreate, setFeatureCoCreate] = useState(true);
    const [featureCheckConsistency, setFeatureCheckConsistency] = useState(true);
    const [featureCheckChapter, setFeatureCheckChapter] = useState(true);
    const [featureCompareChapter, setFeatureCompareChapter] = useState(true);

    // Generic AI features
    const [enableSuggestions, setEnableSuggestions] = useState(true);
    const [enableModeration, setEnableModeration] = useState(true);
    const [enableAutoSummary, setEnableAutoSummary] = useState(false);

    // Limits & banned words (mock only – chưa gọi API thật)
    const [dailyLimit, setDailyLimit] = useState(3);
    const [bannedWordInput, setBannedWordInput] = useState('');
    const [bannedWords, setBannedWords] = useState([
        { id: 1, word: 'spoiler', category: 'BannedWord' },
        { id: 2, word: 'nsfw', category: 'Sensitive' },
    ]);

    const configPreview = {
        model,
        maxTokens,
        temperature,
        memory: {
            enabled: memoryEnabled,
            chunkSize,
            recentChaptersFallback,
            embeddingModel,
        },
        coCreate: {
            skipReview: coCreateSkipReview,
            maxRevisions: coCreateMaxRevisions,
            agents: ['Planner(Ollama)', 'Writer(Ollama)', 'Consistency(Ollama)'],
        },
        limits: {
            perAuthorPerDay: dailyLimit,
        },
        features: {
            suggestNextChapter: featureSuggestNext,
            coCreate: featureCoCreate,
            checkConsistency: featureCheckConsistency,
            checkChapter: featureCheckChapter,
            compareChapter: featureCompareChapter,
            suggestions: enableSuggestions,
            moderation: enableModeration,
            autoSummary: enableAutoSummary,
        },
        bannedWordsCount: bannedWords.length,
    };

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

            <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
                {/* Cột trái: thông số kỹ thuật + nghiệp vụ AI */}
                <div className="lg:col-span-2 space-y-5">
                    {/* Mô hình & tham số chung */}
                    <section className="bg-white rounded-xl border border-slate-200 shadow-sm p-5 space-y-4">
                        <div className="flex items-center gap-2 mb-1">
                            <SlidersHorizontal className="w-4 h-4 text-slate-500" />
                            <h2 className="text-sm font-semibold text-slate-900">
                                Thông số mô hình
                            </h2>
                        </div>

                        <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                            <div>
                                <label className="block text-xs font-medium text-slate-600 mb-1">
                                    Mô hình Ollama sử dụng
                                </label>
                                <select
                                    value={model}
                                    onChange={(e) => setModel(e.target.value)}
                                    className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary"
                                >
                                    <option value="llama3">Llama 3 (khuyến nghị)</option>
                                    <option value="mistral">Mistral</option>
                                    <option value="qwen2.5">Qwen 2.5</option>
                                    <option value="codellama">Code Llama</option>
                                </select>
                                <p className="mt-1 text-[11px] text-slate-500">
                                    Mô hình chạy qua Ollama cho gợi ý nội dung và hỗ trợ tác giả.
                                </p>
                            </div>

                            <div>
                                <label className="block text-xs font-medium text-slate-600 mb-1">
                                    Số token tối đa mỗi lần gọi
                                </label>
                                <input
                                    type="number"
                                    min={256}
                                    max={8192}
                                    step={256}
                                    value={maxTokens}
                                    onChange={(e) => setMaxTokens(Number(e.target.value) || 0)}
                                    className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary"
                                />
                                <p className="mt-1 text-[11px] text-slate-500">
                                    Giới hạn độ dài phản hồi. Giá trị cao sẽ tốn nhiều token hơn.
                                </p>
                            </div>

                            <div className="md:col-span-2">
                                <label className="block text-xs font-medium text-slate-600 mb-1">
                                    Mức độ sáng tạo (temperature)
                                </label>
                                <div className="flex items-center gap-3">
                                    <input
                                        type="range"
                                        min={0}
                                        max={1}
                                        step={0.05}
                                        value={temperature}
                                        onChange={(e) => setTemperature(Number(e.target.value))}
                                        className="flex-1 accent-primary"
                                    />
                                    <span className="w-12 text-xs font-semibold text-slate-700 text-right">
                                        {temperature.toFixed(2)}
                                    </span>
                                </div>
                                <p className="mt-1 text-[11px] text-slate-500">
                                    Thấp: nội dung an toàn, ổn định. Cao: gợi ý sáng tạo hơn nhưng khó đoán hơn.
                                </p>
                            </div>
                        </div>
                    </section>

                    {/* Trí nhớ truyện & Tìm kiếm theo ngữ cảnh (RAG) */}
                    <section className="bg-white rounded-xl border border-emerald-200 shadow-sm p-5 space-y-4">
                        <div className="flex items-center justify-between gap-2 mb-1">
                            <div className="flex items-center gap-2">
                                <Sparkles className="w-4 h-4 text-emerald-500" />
                                <h2 className="text-sm font-semibold text-slate-900">
                                    Trí nhớ truyện &amp; Tìm kiếm theo ngữ cảnh
                                </h2>
                            </div>
                            <button
                                type="button"
                                onClick={() => setMemoryEnabled((v) => !v)}
                                className={`inline-flex items-center gap-2 rounded-full px-3 py-1 text-[11px] font-semibold ${
                                    memoryEnabled
                                        ? 'bg-emerald-100 text-emerald-700'
                                        : 'bg-slate-100 text-slate-500'
                                }`}
                            >
                                <span
                                    className={`inline-block h-2 w-2 rounded-full ${
                                        memoryEnabled ? 'bg-emerald-500' : 'bg-slate-400'
                                    }`}
                                />
                                {memoryEnabled ? 'Đang bật' : 'Đang tắt'}
                            </button>
                        </div>
                        <p className="text-[11px] text-slate-500 mb-3">
                            Hệ thống lưu nhân vật, sự kiện và tình tiết truyện để AI viết chung hoặc kiểm tra mâu thuẫn chính xác hơn.
                        </p>

                        <div className="grid grid-cols-1 md:grid-cols-3 gap-4 text-xs">
                            <div>
                                <label className="block text-[11px] font-medium text-slate-600 mb-1">
                                    Độ dài mỗi đoạn (ký tự)
                                </label>
                                <input
                                    type="number"
                                    min={400}
                                    max={4000}
                                    step={100}
                                    value={chunkSize}
                                    onChange={(e) => setChunkSize(Number(e.target.value) || 0)}
                                    className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary"
                                />
                                <p className="mt-1 text-[11px] text-slate-500">
                                    Mỗi đoạn truyện được cắt ra để lưu và tìm kiếm.
                                </p>
                            </div>
                            <div>
                                <label className="block text-[11px] font-medium text-slate-600 mb-1">
                                    Số chương gần nhất dùng tạm
                                </label>
                                <input
                                    type="number"
                                    min={1}
                                    max={20}
                                    value={recentChaptersFallback}
                                    onChange={(e) => setRecentChaptersFallback(Number(e.target.value) || 0)}
                                    className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary"
                                />
                                <p className="mt-1 text-[11px] text-slate-500">
                                    Khi truyện chưa lập chỉ mục, lấy bao nhiêu chương gần nhất để AI đọc.
                                </p>
                            </div>
                            <div>
                                <label className="block text-[11px] font-medium text-slate-600 mb-1">
                                    Mô hình tìm kiếm nội dung
                                </label>
                                <select
                                    value={embeddingModel}
                                    onChange={(e) => setEmbeddingModel(e.target.value)}
                                    className="w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-xs text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary"
                                >
                                    <option value="text-embedding-3-small">Nhẹ (nhanh, tiết kiệm)</option>
                                    <option value="text-embedding-3-large">Nặng (chính xác hơn)</option>
                                </select>
                                <p className="mt-1 text-[11px] text-slate-500">
                                    Dùng để so khớp nội dung khi tìm đoạn truyện liên quan.
                                </p>
                            </div>
                        </div>

                        <div className="border-t border-slate-200 pt-3 space-y-2">
                            <p className="text-[11px] font-semibold text-slate-700">
                                Trạng thái chỉ mục tìm kiếm
                            </p>
                            <p className="text-[11px] text-slate-500">
                                Cho biết truyện đã được lập chỉ mục chưa (để tìm theo ngữ cảnh). Có thể bấm chạy lập chỉ mục khi cần cập nhật.
                            </p>
                            <div className="flex flex-wrap gap-2">
                                <span className="inline-flex items-center rounded-lg bg-slate-100 px-2 py-1 text-[11px] text-slate-600">
                                    Trạng thái: Đang dùng mẫu
                                </span>
                                <button
                                    type="button"
                                    className="rounded-lg border border-slate-300 bg-white px-3 py-1.5 text-[11px] font-medium text-slate-700 hover:bg-slate-50"
                                >
                                    Chạy lập chỉ mục
                                </button>
                            </div>
                        </div>
                    </section>

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
                                onClick={() => setEnableSuggestions((v) => !v)}
                                className={`flex flex-col items-start gap-1 rounded-xl border px-3 py-3 text-left transition-all ${
                                    enableSuggestions
                                        ? 'border-primary bg-primary/5 shadow-sm'
                                        : 'border-slate-200 hover:border-primary/50'
                                }`}
                            >
                                <span className="text-[11px] font-semibold text-slate-800">
                                    Gợi ý viết tiếp
                                </span>
                                <p className="text-[11px] text-slate-500">
                                    AI đề xuất đoạn tiếp theo khi tác giả bí ý tưởng.
                                </p>
                            </button>

                            <button
                                type="button"
                                onClick={() => setEnableModeration((v) => !v)}
                                className={`flex flex-col items-start gap-1 rounded-xl border px-3 py-3 text-left transition-all ${
                                    enableModeration
                                        ? 'border-emerald-500 bg-emerald-50/60 shadow-sm'
                                        : 'border-slate-200 hover:border-emerald-400/60'
                                }`}
                            >
                                <span className="inline-flex items-center gap-1 text-[11px] font-semibold text-slate-800">
                                    <ShieldCheck className="w-3.5 h-3.5 text-emerald-600" />
                                    Kiểm duyệt nội dung
                                </span>
                                <p className="text-[11px] text-slate-500">
                                    Gợi ý đánh dấu nội dung nhạy cảm hoặc vi phạm quy định.
                                </p>
                            </button>

                            <button
                                type="button"
                                onClick={() => setEnableAutoSummary((v) => !v)}
                                className={`flex flex-col items-start gap-1 rounded-xl border px-3 py-3 text-left transition-all ${
                                    enableAutoSummary
                                        ? 'border-sky-500 bg-sky-50/60 shadow-sm'
                                        : 'border-slate-200 hover:border-sky-400/60'
                                }`}
                            >
                                <span className="text-[11px] font-semibold text-slate-800">
                                    Tự động tóm tắt chương
                                </span>
                                <p className="text-[11px] text-slate-500">
                                    Tạo đoạn tóm tắt ngắn cho mỗi chương để hiển thị trên trang truyện.
                                </p>
                            </button>
                        </div>

                        {/* Bật/tắt từng dịch vụ tương ứng backend */}
                        <div className="mt-4 border-t border-slate-200 pt-3 grid grid-cols-1 md:grid-cols-2 gap-3 text-xs">
                            <div className="space-y-2">
                                <p className="text-[11px] font-semibold text-slate-700">
                                    Dịch vụ chính
                                </p>
                                <label className="flex items-center gap-2 text-[11px] text-slate-600">
                                    <input
                                        type="checkbox"
                                        checked={featureSuggestNext}
                                        onChange={(e) => setFeatureSuggestNext(e.target.checked)}
                                    />
                                    <span>Gợi ý chương tiếp theo</span>
                                </label>
                                <label className="flex items-center gap-2 text-[11px] text-slate-600">
                                    <input
                                        type="checkbox"
                                        checked={featureCoCreate}
                                        onChange={(e) => setFeatureCoCreate(e.target.checked)}
                                    />
                                    <span>Viết chung với AI (co-create)</span>
                                </label>
                                <label className="flex items-center gap-2 text-[11px] text-slate-600">
                                    <input
                                        type="checkbox"
                                        checked={featureCheckConsistency}
                                        onChange={(e) => setFeatureCheckConsistency(e.target.checked)}
                                    />
                                    <span>Kiểm tra mâu thuẫn nội dung</span>
                                </label>
                            </div>
                            <div className="space-y-2">
                                <p className="text-[11px] font-semibold text-slate-700">
                                    Khi tác giả lưu chương
                                </p>
                                <label className="flex items-center gap-2 text-[11px] text-slate-600">
                                    <input
                                        type="checkbox"
                                        checked={featureCheckChapter}
                                        onChange={(e) => setFeatureCheckChapter(e.target.checked)}
                                    />
                                    <span>Kiểm tra chính tả và từ cấm</span>
                                </label>
                                <label className="flex items-center gap-2 text-[11px] text-slate-600">
                                    <input
                                        type="checkbox"
                                        checked={featureCompareChapter}
                                        onChange={(e) => setFeatureCompareChapter(e.target.checked)}
                                    />
                                    <span>Ước lượng tỷ lệ nội dung do AI viết</span>
                                </label>
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
                            <div className="flex flex-wrap gap-3 mt-2">
                                <label className="inline-flex items-center gap-2 text-[11px] text-slate-600">
                                    <input
                                        type="checkbox"
                                        checked={coCreateSkipReview}
                                        onChange={(e) => setCoCreateSkipReview(e.target.checked)}
                                    />
                                    <span>Bỏ qua bước kiểm tra mâu thuẫn khi viết chung</span>
                                </label>
                                <div className="flex items-center gap-2 text-[11px] text-slate-600">
                                    <span>Tối đa số lần AI chỉnh sửa lại:</span>
                                    <input
                                        type="number"
                                        min={0}
                                        max={5}
                                        value={coCreateMaxRevisions}
                                        onChange={(e) => setCoCreateMaxRevisions(Number(e.target.value) || 0)}
                                        className="w-14 rounded border border-slate-300 px-1 py-0.5 text-center text-[11px]"
                                    />
                                </div>
                            </div>
                        </div>
                    </section>

                    {/* Giới hạn sử dụng AI & Danh sách từ cấm — đặt dưới Các dịch vụ AI cho tác giả */}
                    <section className="bg-white rounded-xl border border-slate-200 shadow-sm p-5 space-y-4">
                        <h2 className="text-sm font-semibold text-slate-900">
                            Giới hạn sử dụng AI &amp; Danh sách từ cấm
                        </h2>
                        <div className="space-y-3 text-xs">
                            <div>
                                <label className="block text-[11px] font-medium text-slate-600 mb-1">
                                    Số lần dùng AI tối đa mỗi ngày (mỗi tác giả)
                                </label>
                                <input
                                    type="number"
                                    min={1}
                                    max={50}
                                    value={dailyLimit}
                                    onChange={(e) => setDailyLimit(Number(e.target.value) || 0)}
                                    className="w-24 rounded-lg border border-slate-300 bg-white px-2 py-1 text-xs text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary"
                                />
                                <p className="mt-1 text-[11px] text-slate-500">
                                    Vượt quá số này thì tác giả không dùng thêm được tính năng AI trong ngày.
                                </p>
                            </div>

                            <div className="border-t border-slate-200 pt-3">
                                <div className="flex items-center justify-between mb-2">
                                    <p className="text-[11px] font-medium text-slate-600">
                                        Từ cấm (không cho xuất hiện trong nội dung)
                                    </p>
                                    <span className="text-[10px] text-slate-500">
                                        Hiện có: {bannedWords.length} từ
                                    </span>
                                </div>
                                <div className="flex gap-2 mb-2">
                                    <input
                                        type="text"
                                        value={bannedWordInput}
                                        onChange={(e) => setBannedWordInput(e.target.value)}
                                        placeholder="Nhập từ cấm..."
                                        className="flex-1 rounded-lg border border-slate-300 bg-white px-2 py-1 text-xs text-slate-900 focus:outline-none focus:ring-2 focus:ring-primary/40 focus:border-primary"
                                    />
                                    <button
                                        type="button"
                                        onClick={() => {
                                            const word = bannedWordInput.trim();
                                            if (!word) return;
                                            setBannedWords((list) => [
                                                ...list,
                                                { id: Date.now(), word, category: 'BannedWord' },
                                            ]);
                                            setBannedWordInput('');
                                        }}
                                        className="px-3 py-1 rounded-lg bg-primary text-white text-[11px] font-semibold hover:bg-primary/90"
                                    >
                                        Thêm
                                    </button>
                                </div>
                                <div className="max-h-32 overflow-auto border border-slate-200 rounded-lg">
                                    {bannedWords.length === 0 ? (
                                        <div className="px-3 py-2 text-[11px] text-slate-400">
                                            Chưa có từ cấm nào được cấu hình.
                                        </div>
                                    ) : (
                                        <table className="w-full text-[11px]">
                                            <tbody>
                                                {bannedWords.map((bw) => (
                                                    <tr
                                                        key={bw.id}
                                                        className="border-b border-slate-100 last:border-b-0"
                                                    >
                                                        <td className="px-3 py-1 text-slate-700">{bw.word}</td>
                                                        <td className="px-3 py-1 text-slate-400">{bw.category}</td>
                                                        <td className="px-2 py-1 text-right">
                                                            <button
                                                                type="button"
                                                                onClick={() =>
                                                                    setBannedWords((list) =>
                                                                        list.filter((x) => x.id !== bw.id)
                                                                    )
                                                                }
                                                                className="text-[10px] text-red-500 hover:text-red-600"
                                                            >
                                                                Xóa
                                                            </button>
                                                        </td>
                                                    </tr>
                                                ))}
                                            </tbody>
                                        </table>
                                    )}
                                </div>
                            </div>
                        </div>
                    </section>
                </div>

                {/* Cột phải: chỉ xem trước cấu hình */}
                <aside className="lg:col-span-1 space-y-4">
                    <div className="bg-slate-900 rounded-xl text-slate-50 p-5 shadow-lg space-y-4">
                        <h2 className="text-sm font-semibold flex items-center gap-2">
                            <Brain className="w-4 h-4 text-emerald-300" />
                            Xem trước cấu hình
                        </h2>
                        <p className="text-[11px] text-slate-300">
                            Tóm tắt các thiết lập hiện tại. Khi kết nối hệ thống, các giá trị này sẽ được lưu lại.
                        </p>
                        <pre className="text-[11px] bg-slate-950/40 border border-slate-700/60 rounded-lg p-3 overflow-x-auto">
                            {JSON.stringify(configPreview, null, 2)}
                        </pre>
                    </div>
                </aside>
            </div>
        </div>
    );
}

