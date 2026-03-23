import { useState, useEffect } from 'react';
import { Sparkles, Settings, X, Lock, Unlock, Coins } from 'lucide-react';
import { useToast } from './Toast';
import { indexRag, suggestNextChapter, coCreate, getAiUsageLimit } from '../../../api/ai/aiApi';
import { translateCoCreateOutlineLabels } from '../../../utils/coCreateOutlineLabelsVi';

const countWords = (text) => {
    if (!text || !text.trim()) return 0;
    return text.trim().split(/\s+/).filter(word => word.length > 0).length;
};

function extractOutlineJson(raw) {
    if (!raw || !raw.trim()) return raw;
    const s = raw.trim();
    const regex = /\{\s*"(?:scenes|Scenes)"\s*:/g;
    const candidates = [];
    let m;
    while ((m = regex.exec(s)) !== null) {
        const start = m.index;
        let depth = 0;
        let i = start;
        while (i < s.length) {
            if (s[i] === '{') depth++;
            else if (s[i] === '}') { depth--; if (depth === 0) { candidates.push(s.slice(start, i + 1)); break; } }
            i++;
        }
    }
    if (candidates.length > 0) return candidates[candidates.length - 1];
    const firstBrace = s.indexOf('{');
    if (firstBrace >= 0) {
        let depth = 0;
        let i = firstBrace;
        while (i < s.length) {
            if (s[i] === '{') depth++;
            else if (s[i] === '}') { depth--; if (depth === 0) return s.slice(firstBrace, i + 1); }
            i++;
        }
    }
    return s;
}

function isExampleOutline(scenes) {
    if (!Array.isArray(scenes) || scenes.length === 0) return false;
    const first = scenes[0];
    const title = (first?.title ?? first?.Title ?? '').toString();
    const summary = (first?.summary ?? first?.Summary ?? '').toString();
    const hasExampleTitle = /Tới hiện trường|Khám phá căn phòng|Phỏng vấn nhân chứng/i.test(title) || /Holmes và Watson tới căn nhà riêng/i.test(summary);
    return hasExampleTitle && JSON.stringify(scenes).includes('Holmes');
}

function formatOutlineForDisplay(outline) {
    if (!outline || !outline.trim()) return '';
    const raw = outline.trim();
    const toParse = extractOutlineJson(raw);
    try {
        const parsed = JSON.parse(toParse);
        const scenes = parsed?.scenes ?? parsed?.Scenes;
        if (Array.isArray(scenes) && scenes.length > 0 && !isExampleOutline(scenes)) {
            const joined = scenes
                .map((s, i) => {
                    const title = s?.title ?? s?.Title ?? '';
                    const summary = s?.summary ?? s?.Summary ?? '';
                    const characters = s?.characters ?? s?.Characters ?? '';
                    const parts = [];
                    if (title) parts.push(title);
                    if (summary) parts.push(summary);
                    if (Array.isArray(characters) && characters.length) {
                        parts.push(`Nhân vật: ${characters.join(', ')}`);
                    } else if (typeof characters === 'string' && characters.trim()) {
                        parts.push(`Nhân vật: ${characters}`);
                    }
                    return parts.length ? `Bối cảnh ${i + 1}:\n${parts.join('\n')}` : `Bối cảnh ${i + 1}`;
                })
                .join('\n\n');
            return translateCoCreateOutlineLabels(joined);
        }
    } catch {
        // ignore
    }
    return translateCoCreateOutlineLabels(raw.replace(/\bScene\s*(\d+)\b/gi, 'Bối cảnh $1'));
}

function mergeContentRemoveScenes(content) {
    if (!content || !content.trim()) return '';
    return content.replace(/\s*Scene\s*\d+\s*[-:]?\s*/gi, '\n\n').replace(/\n{3,}/g, '\n\n').trim();
}

/** Chỉ lấy phần nội dung văn bản chương (bỏ dàn ý, Bối cảnh N:, JSON) để đưa vào ô nội dung */
function contentOnlyForChapter(raw) {
    if (!raw || !raw.trim()) return '';
    let s = raw.trim();
    s = s.replace(/```[\s\S]*?```/g, '');
    s = s.replace(/\{\s*["']?(?:scenes|Scenes)["']?\s*:[\s\S]*?\}\s*\}/g, '');
    s = s.replace(/(?:\*\*)?\s*Dàn ý\s*\*\*[^\n]*(?:\n(?![ \t]*\n)[^\n]*)*/gi, '');
    s = s.replace(/\n?\s*Dàn ý\s*:?[^\n]*(?:\n(?![ \t]*\n)[^\n]*)*/gi, '');
    s = s.replace(/^\s*Bối cảnh\s*\d+\s*:\s*$/gm, '');
    s = s.replace(/^\s*Bối cảnh\s*\d+\s*:\s*\n/gm, '');
    s = mergeContentRemoveScenes(s);
    s = s.replace(/\bBối cảnh\s*\d+\s*:\s*/gi, '\n\n');
    return s.replace(/\n{3,}/g, '\n\n').trim();
}

const NO_STORY_MESSAGE = 'Tính năng AI chỉ khả dụng sau khi tạo truyện. Hãy hoàn thành các bước và nhấn Lưu nháp hoặc Xuất bản, sau đó vào truyện → Thêm chương để dùng AI gợi ý.';

/** Hiển thị trong thanh công cụ bước 2 (tạo truyện) khi chưa có truyện đã lưu — đồng bộ UX với màn soạn chương. */
const AI_DISABLED_BEFORE_STORY_SAVED =
    'Bạn phải hoàn tất tạo truyện (lưu nháp hoặc gửi duyệt) trước. Sau đó mở truyện → Thêm/Sửa chương để dùng gợi ý AI.';

export function ChapterEditor({ chapter, onChange, story }) {
    const { showToast, ToastContainer } = useToast();
    const [showSettings, setShowSettings] = useState(false);
    const [editorSettings, setEditorSettings] = useState({
        fontSize: 16,
        fontFamily: 'Arial, sans-serif',
        backgroundColor: '#ffffff',
    });
    const [showSuggestPopup, setShowSuggestPopup] = useState(false);
    const [suggestLoading, setSuggestLoading] = useState(false);
    const [suggestions, setSuggestions] = useState([]);
    const [showCoCreateIdeaPopup, setShowCoCreateIdeaPopup] = useState(false);
    const [showCoCreateResultPopup, setShowCoCreateResultPopup] = useState(false);
    const [coCreateIdea, setCoCreateIdea] = useState('');
    const [coCreateLoading, setCoCreateLoading] = useState(false);
    const [coCreateResult, setCoCreateResult] = useState(null);
    const [aiUsageLimit, setAiUsageLimit] = useState(null);

    const storyId = story?.id ?? story?.Id ?? null;
    /** Bước 2 tạo truyện mới: chưa có storyId → không gọi API AI; hiển thị nút giống màn chương nhưng khóa. */
    const aiLocked = !storyId;

    const loadAiUsageLimit = async () => {
        try {
            const data = await getAiUsageLimit();
            setAiUsageLimit({
                limitPerDay: Number(data?.limitPerDay ?? data?.LimitPerDay ?? 0) || 0,
                usedInWindow: Number(data?.usedInWindow ?? data?.UsedInWindow ?? 0) || 0,
                remaining: Number(data?.remaining ?? data?.Remaining ?? 0) || 0,
                resetsAtUtc: data?.resetsAtUtc ?? data?.ResetsAtUtc ?? null,
            });
        } catch {
            setAiUsageLimit(null);
        }
    };

    useEffect(() => {
        if (storyId) loadAiUsageLimit();
    }, [storyId]);

    const fontFamilies = [
        { name: 'Arial', value: 'Arial, sans-serif' },
        { name: 'Times New Roman', value: 'Times New Roman, serif' },
        { name: 'Georgia', value: 'Georgia, serif' },
        { name: 'Courier New', value: 'Courier New, monospace' },
    ];

    const backgroundColors = [
        { name: 'Trắng', value: '#ffffff' },
        { name: 'Kem', value: '#fef6e4' },
        { name: 'Xanh nhạt', value: '#e8f5e9' },
        { name: 'Xám nhạt', value: '#f5f5f5' },
    ];

    const handleAISuggestion = async (type) => {
        if (aiLocked) {
            showToast(AI_DISABLED_BEFORE_STORY_SAVED, 'info');
            return;
        }
        if (type === 'paragraph') {
            setSuggestions([]);
            setShowSuggestPopup(true);
            setSuggestLoading(true);
            try {
                indexRag(storyId);
                const data = await suggestNextChapter(storyId, null);
                const list = data?.suggestions ?? data?.Suggestions ?? [];
                setSuggestions(Array.isArray(list) ? list : []);
                loadAiUsageLimit();
            } catch (err) {
                const status = err?.response?.status;
                const msg = err?.response?.data?.message ?? err?.message ?? 'Lỗi khi gọi gợi ý AI.';
                if (status === 429) showToast('Bạn đã gọi gợi ý quá nhiều lần. Vui lòng thử lại sau.', 'error');
                else if (status === 403) showToast(msg || 'Chỉ tác giả mới được sử dụng tính năng này.', 'error');
                else showToast(msg, 'error');
                setSuggestions([]);
            } finally {
                setSuggestLoading(false);
            }
        } else {
            setCoCreateIdea('');
            setCoCreateResult(null);
            setShowCoCreateResultPopup(false);
            setShowCoCreateIdeaPopup(true);
        }
    };

    const handleCoCreateSubmit = async () => {
        if (!storyId) {
            showToast(NO_STORY_MESSAGE, 'info');
            return;
        }
        const idea = (coCreateIdea || '').trim();
        if (!idea) {
            showToast('Vui lòng nhập ý tưởng của bạn.', 'error');
            return;
        }
        setCoCreateLoading(true);
        try {
            const chapterOrderIndex = (Number(chapter?.number) || 1) - 1;
            const data = await coCreate(storyId, idea, { chapterOrderIndex });
            setCoCreateResult(data);
            setShowCoCreateIdeaPopup(false);
            setShowCoCreateResultPopup(true);
            loadAiUsageLimit();
        } catch (err) {
            const status = err?.response?.status;
            const msg = err?.response?.data?.message ?? err?.message ?? 'Lỗi khi đồng sáng tác với AI.';
            if (status === 429) showToast('Bạn đã gọi AI quá nhiều lần. Vui lòng thử lại sau.', 'error');
            else if (status === 403) showToast(msg || 'Chỉ tác giả mới được sử dụng.', 'error');
            else showToast(msg, 'error');
        } finally {
            setCoCreateLoading(false);
        }
    };

    const handleCoCreateApply = () => {
        const raw = coCreateResult?.finalContent ?? coCreateResult?.FinalContent ?? '';
        const content = contentOnlyForChapter(raw);
        if (content) {
            onChange('content', content);
            showToast('Đã áp dụng nội dung. Bạn có thể chỉnh sửa và nhấn Tiếp theo khi xong.', 'success');
        }
        setShowCoCreateResultPopup(false);
        setCoCreateResult(null);
    };

    return (
        <>
            <ToastContainer />
            {/* Popup gợi ý chương tiếp theo - giống màn tạo chương */}
            {showSuggestPopup && (
                <div
                    style={{
                        position: 'fixed',
                        inset: 0,
                        zIndex: 9999,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        backgroundColor: 'rgba(0,0,0,0.5)',
                    }}
                    onClick={() => !suggestLoading && setShowSuggestPopup(false)}
                >
                    <div
                        style={{
                            backgroundColor: '#ffffff',
                            borderRadius: '12px',
                            maxWidth: '560px',
                            width: '90%',
                            maxHeight: '85vh',
                            overflow: 'hidden',
                            display: 'flex',
                            flexDirection: 'column',
                            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1), 0 8px 10px -6px rgba(0,0,0,0.1)',
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div style={{ padding: '1.25rem 1.5rem', borderBottom: '1px solid #e5e7eb' }}>
                            <h3 style={{ margin: 0, fontSize: '1.125rem', fontWeight: 600, color: '#111827' }}>
                                Gợi ý chương tiếp theo
                            </h3>
                        </div>
                        <div style={{ padding: '1.25rem 1.5rem', overflowY: 'auto', flex: 1 }}>
                            {!storyId ? (
                                <p style={{ margin: 0, color: '#6b7280', lineHeight: 1.6 }}>{NO_STORY_MESSAGE}</p>
                            ) : suggestLoading ? (
                                <p style={{ margin: 0, color: '#6b7280', textAlign: 'center' }}>Đang tải gợi ý...</p>
                            ) : suggestions.length === 0 ? (
                                <p style={{ margin: 0, color: '#6b7280', textAlign: 'center' }}>Không có gợi ý.</p>
                            ) : (
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                                    {suggestions.map((item, index) => {
                                        const title = item?.title ?? item?.Title ?? '';
                                        const summary = item?.summary ?? item?.Summary ?? '';
                                        const direction = item?.direction ?? item?.Direction ?? '';
                                        return (
                                            <div
                                                key={index}
                                                style={{
                                                    padding: '1rem',
                                                    backgroundColor: '#f9fafb',
                                                    borderRadius: '8px',
                                                    border: '1px solid #e5e7eb',
                                                }}
                                            >
                                                <div style={{ fontSize: '0.875rem', fontWeight: 600, color: '#111827', marginBottom: '0.5rem' }}>
                                                    {title || `Gợi ý ${index + 1}`}
                                                </div>
                                                {summary && <div style={{ fontSize: '0.8125rem', color: '#4b5563', marginBottom: '0.5rem' }}>{summary}</div>}
                                                {direction && <div style={{ fontSize: '0.8125rem', color: '#6b7280', whiteSpace: 'pre-wrap' }}>{direction}</div>}
                                            </div>
                                        );
                                    })}
                                </div>
                            )}
                        </div>
                        <div style={{ padding: '1rem 1.5rem', borderTop: '1px solid #e5e7eb' }}>
                            <button
                                type="button"
                                onClick={() => setShowSuggestPopup(false)}
                                style={{
                                    width: '100%',
                                    padding: '0.625rem 1rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    color: '#ffffff',
                                    backgroundColor: '#13ec5b',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: 'pointer',
                                }}
                            >
                                ĐÓNG
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Popup đồng sáng tác - nhập ý tưởng */}
            {showCoCreateIdeaPopup && (
                <div
                    style={{
                        position: 'fixed',
                        inset: 0,
                        zIndex: 9999,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        backgroundColor: 'rgba(0,0,0,0.5)',
                    }}
                    onClick={() => !coCreateLoading && setShowCoCreateIdeaPopup(false)}
                >
                    <div
                        style={{
                            backgroundColor: '#ffffff',
                            borderRadius: '12px',
                            maxWidth: '520px',
                            width: '90%',
                            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)',
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div style={{ padding: '1.25rem 1.5rem', borderBottom: '1px solid #e5e7eb' }}>
                            <h3 style={{ margin: 0, fontSize: '1.125rem', fontWeight: 600, color: '#111827' }}>
                                Đồng sáng tác với AI
                            </h3>
                            <p style={{ margin: '0.5rem 0 0', fontSize: '0.8125rem', color: '#6b7280' }}>
                                Nhập ý tưởng của bạn, AI sẽ tạo dàn ý và nội dung chương.
                            </p>
                            {!storyId && (
                                <p style={{ margin: '0.75rem 0 0', padding: '0.75rem', backgroundColor: '#fef3c7', borderRadius: '8px', fontSize: '0.8125rem', color: '#92400e' }}>
                                    {NO_STORY_MESSAGE}
                                </p>
                            )}
                        </div>
                        <div style={{ padding: '1.25rem 1.5rem' }}>
                            <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#374151', marginBottom: '0.5rem' }}>Ý TƯỞNG CỦA BẠN</label>
                            <textarea
                                value={coCreateIdea}
                                onChange={(e) => setCoCreateIdea(e.target.value)}
                                placeholder="Ví dụ: Nhân vật A gặp lại B sau 5 năm, xung đột nổ ra..."
                                rows={4}
                                style={{
                                    width: '100%',
                                    padding: '0.75rem',
                                    border: '1px solid #e5e7eb',
                                    borderRadius: '8px',
                                    fontSize: '0.875rem',
                                    outline: 'none',
                                    resize: 'vertical',
                                }}
                            />
                        </div>
                        <div style={{ padding: '1rem 1.5rem', borderTop: '1px solid #e5e7eb', display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                            <button
                                type="button"
                                onClick={() => !coCreateLoading && setShowCoCreateIdeaPopup(false)}
                                style={{
                                    padding: '0.5rem 1rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 500,
                                    color: '#6b7280',
                                    backgroundColor: '#f3f4f6',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: coCreateLoading ? 'not-allowed' : 'pointer',
                                }}
                            >
                                Hủy
                            </button>
                            <button
                                type="button"
                                onClick={handleCoCreateSubmit}
                                disabled={coCreateLoading || !(coCreateIdea || '').trim()}
                                style={{
                                    padding: '0.5rem 1.25rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    color: '#ffffff',
                                    backgroundColor: (coCreateLoading || !(coCreateIdea || '').trim()) ? '#9ca3af' : '#13ec5b',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: (coCreateLoading || !(coCreateIdea || '').trim()) ? 'not-allowed' : 'pointer',
                                }}
                            >
                                {coCreateLoading ? 'Đang tạo...' : 'Tạo nội dung'}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Popup kết quả đồng sáng tác - nội dung chương */}
            {showCoCreateResultPopup && coCreateResult && (
                <div
                    style={{
                        position: 'fixed',
                        inset: 0,
                        zIndex: 9999,
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        backgroundColor: 'rgba(0,0,0,0.5)',
                    }}
                >
                    <div
                        style={{
                            backgroundColor: '#ffffff',
                            borderRadius: '12px',
                            maxWidth: '720px',
                            width: '95%',
                            maxHeight: '90vh',
                            overflow: 'hidden',
                            display: 'flex',
                            flexDirection: 'column',
                            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1)',
                        }}
                    >
                        <div style={{ padding: '1.25rem 1.5rem', borderBottom: '1px solid #e5e7eb' }}>
                            <h3 style={{ margin: 0, fontSize: '1.125rem', fontWeight: 600, color: '#111827' }}>Nội dung AI đã tạo</h3>
                        </div>
                        <div style={{ padding: '1.25rem 1.5rem', overflowY: 'auto', flex: 1 }}>
                            {coCreateResult.ideaContradictionFeedback ?? coCreateResult.IdeaContradictionFeedback ? (
                                <div style={{ padding: '1rem', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '8px', color: '#991b1b' }}>
                                    {coCreateResult.ideaContradictionFeedback ?? coCreateResult.IdeaContradictionFeedback}
                                </div>
                            ) : (
                                <>
                                    {((coCreateResult.outline ?? coCreateResult.Outline) || '').trim() && (
                                        <div style={{ marginBottom: '1rem' }}>
                                            <div style={{ fontSize: '0.75rem', fontWeight: 600, color: '#6b7280', marginBottom: '0.25rem' }}>Dàn ý</div>
                                            <div style={{ fontSize: '0.875rem', color: '#374151', whiteSpace: 'pre-wrap' }}>
                                                {formatOutlineForDisplay(coCreateResult.outline ?? coCreateResult.Outline)}
                                            </div>
                                        </div>
                                    )}
                                    {(coCreateResult.reviewFeedback ?? coCreateResult.ReviewFeedback) && (
                                        <div style={{ marginBottom: '1rem', padding: '0.75rem', backgroundColor: '#eff6ff', border: '1px solid #bfdbfe', borderRadius: '8px' }}>
                                            <div style={{ fontSize: '0.75rem', fontWeight: 600, color: '#1e40af', marginBottom: '0.25rem' }}>Nhận xét của AI</div>
                                            <div style={{ fontSize: '0.8125rem', color: '#1e3a8a', whiteSpace: 'pre-wrap' }}>
                                                {coCreateResult.reviewFeedback ?? coCreateResult.ReviewFeedback}
                                            </div>
                                        </div>
                                    )}
                                    <div>
                                        <div style={{ fontSize: '0.75rem', fontWeight: 600, color: '#6b7280', marginBottom: '0.25rem' }}>Nội dung chương</div>
                                        <div
                                            style={{
                                                fontSize: '0.875rem',
                                                color: '#111827',
                                                whiteSpace: 'pre-wrap',
                                                maxHeight: '40vh',
                                                overflowY: 'auto',
                                                padding: '0.75rem',
                                                border: '1px solid #e5e7eb',
                                                borderRadius: '8px',
                                                backgroundColor: '#f9fafb',
                                            }}
                                        >
                                            {mergeContentRemoveScenes(coCreateResult.finalContent ?? coCreateResult.FinalContent ?? '')}
                                        </div>
                                    </div>
                                </>
                            )}
                        </div>
                        <div style={{ padding: '1rem 1.5rem', borderTop: '1px solid #e5e7eb', display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                            {coCreateResult.ideaContradictionFeedback ?? coCreateResult.IdeaContradictionFeedback ? (
                                <button
                                    type="button"
                                    onClick={() => { setShowCoCreateResultPopup(false); setCoCreateResult(null); }}
                                    style={{ padding: '0.5rem 1.25rem', fontSize: '0.875rem', fontWeight: 600, color: '#ffffff', backgroundColor: '#13ec5b', border: 'none', borderRadius: '8px', cursor: 'pointer' }}
                                >
                                    Đóng
                                </button>
                            ) : (
                                <>
                                    <button
                                        type="button"
                                        onClick={() => { setShowCoCreateResultPopup(false); setCoCreateResult(null); }}
                                        style={{ padding: '0.5rem 1rem', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', backgroundColor: '#f3f4f6', border: 'none', borderRadius: '8px', cursor: 'pointer' }}
                                    >
                                        Đóng
                                    </button>
                                    <button
                                        type="button"
                                        onClick={handleCoCreateApply}
                                        style={{ padding: '0.5rem 1.25rem', fontSize: '0.875rem', fontWeight: 600, color: '#ffffff', backgroundColor: '#13ec5b', border: 'none', borderRadius: '8px', cursor: 'pointer' }}
                                    >
                                        ĐỒNG Ý SỬ DỤNG NỘI DUNG NÀY
                                    </button>
                                </>
                            )}
                        </div>
                    </div>
                </div>
            )}

            <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '2rem', border: '1px solid #e0e0e0' }}>
                <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
                    {/* Chapter Title */}
                    <div>
                        <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                            Tên chương
                        </label>
                        <input
                            type="text"
                            value={chapter.title}
                            onChange={(e) => onChange('title', e.target.value)}
                            placeholder="Nhập tên chương"
                            style={{
                                width: '100%',
                                padding: '0.75rem',
                                backgroundColor: '#f9fafb',
                                border: '1px solid #e5e7eb',
                                borderRadius: '4px',
                                fontSize: '0.875rem',
                                outline: 'none'
                            }}
                        />
                    </div>

                    {/* Chế độ sáng tác (Public / Paid) */}
                    <div>
                        <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.75rem' }}>
                            Chế độ sáng tác <span style={{ color: '#ef4444' }}>*</span>
                        </label>
                        <div style={{ display: 'grid', gridTemplateColumns: (chapter.accessType === 'paid' ? '1fr 1fr 200px' : '1fr 1fr'), gap: '1rem' }}>
                            <button
                                type="button"
                                onClick={() => { onChange('accessType', 'public'); onChange('price', 0); }}
                                style={{
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: '0.75rem',
                                    padding: '1rem',
                                    border: `2px solid ${chapter.accessType === 'public' ? '#13ec5b' : '#e5e7eb'}`,
                                    borderRadius: '8px',
                                    backgroundColor: chapter.accessType === 'public' ? 'rgba(19, 236, 91, 0.05)' : '#ffffff',
                                    cursor: 'pointer',
                                    transition: 'all 0.2s',
                                    textAlign: 'left',
                                }}
                            >
                                <div style={{
                                    width: '40px',
                                    height: '40px',
                                    borderRadius: '50%',
                                    backgroundColor: chapter.accessType === 'public' ? '#13ec5b' : '#f3f4f6',
                                    color: chapter.accessType === 'public' ? '#ffffff' : '#6b7280',
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                }}>
                                    <Unlock style={{ width: '20px', height: '20px' }} />
                                </div>
                                <div style={{ flex: 1 }}>
                                    <div style={{ fontSize: '0.875rem', fontWeight: 600, color: chapter.accessType === 'public' ? '#13ec5b' : '#333333' }}>
                                        Miễn phí (Public)
                                    </div>
                                    <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                                        Người đọc có thể đọc miễn phí
                                    </div>
                                </div>
                            </button>

                            <button
                                type="button"
                                onClick={() => onChange('accessType', 'paid')}
                                style={{
                                    display: 'flex',
                                    alignItems: 'center',
                                    gap: '0.75rem',
                                    padding: '1rem',
                                    border: `2px solid ${chapter.accessType === 'paid' ? '#f59e0b' : '#e5e7eb'}`,
                                    borderRadius: '8px',
                                    backgroundColor: chapter.accessType === 'paid' ? '#fffbeb' : '#ffffff',
                                    cursor: 'pointer',
                                    transition: 'all 0.2s',
                                    textAlign: 'left',
                                }}
                            >
                                <div style={{
                                    width: '40px',
                                    height: '40px',
                                    borderRadius: '50%',
                                    backgroundColor: chapter.accessType === 'paid' ? '#f59e0b' : '#f3f4f6',
                                    color: chapter.accessType === 'paid' ? '#ffffff' : '#6b7280',
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                }}>
                                    <Lock style={{ width: '20px', height: '20px' }} />
                                </div>
                                <div style={{ flex: 1 }}>
                                    <div style={{ fontSize: '0.875rem', fontWeight: 600, color: chapter.accessType === 'paid' ? '#f59e0b' : '#333333' }}>
                                        Trả phí (Paid)
                                    </div>
                                    <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                                        Yêu cầu người đọc trả phí
                                    </div>
                                </div>
                            </button>

                            {chapter.accessType === 'paid' && (
                                <div>
                                    <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                        Giá <span style={{ color: '#ef4444' }}>*</span>
                                    </label>
                                    <div style={{ position: 'relative' }}>
                                        <input
                                            type="number"
                                            value={chapter.price || 0}
                                            onChange={(e) => onChange('price', Number(e.target.value) || 0)}
                                            min="1"
                                            placeholder="0"
                                            style={{
                                                width: '100%',
                                                padding: '0.75rem 0.75rem 0.75rem 2.5rem',
                                                backgroundColor: '#fffbeb',
                                                border: '1px solid #fbbf24',
                                                borderRadius: '8px',
                                                fontSize: '0.875rem',
                                                fontWeight: 600,
                                                color: '#92400e',
                                                outline: 'none',
                                            }}
                                        />
                                        <Coins style={{
                                            position: 'absolute',
                                            left: '0.75rem',
                                            top: '50%',
                                            transform: 'translateY(-50%)',
                                            width: '16px',
                                            height: '16px',
                                            color: '#f59e0b',
                                        }} />
                                    </div>
                                    <p style={{ fontSize: '0.625rem', color: '#92400e', marginTop: '0.25rem', margin: 0 }}>Đơn vị: Xu</p>
                                </div>
                            )}
                        </div>
                        {chapter.accessType === 'paid' && (
                            <div style={{
                                marginTop: '1rem',
                                padding: '0.75rem 1rem',
                                backgroundColor: '#fffbeb',
                                border: '1px solid #fcd34d',
                                borderRadius: '8px',
                                fontSize: '0.75rem',
                                color: '#92400e',
                            }}>
                                <strong>Lưu ý:</strong> Người đọc cần xu để mở khóa. Bạn nhận 70% số xu, nền tảng giữ 30%.
                            </div>
                        )}
                    </div>

                    {/* Toolbar — giống ChapterEditorPage: 2 nút AI + Tùy chỉnh; khi chưa có truyện đã lưu: nút khóa + thông báo trong khung */}
                    <div
                        className="rounded-lg border border-slate-200 bg-slate-50 p-3 sm:p-4"
                        style={{
                            display: 'flex',
                            flexDirection: 'column',
                            gap: aiLocked ? '0.75rem' : '0',
                        }}
                    >
                        <div
                            style={{
                                display: 'flex',
                                justifyContent: 'space-between',
                                alignItems: 'center',
                                flexWrap: 'wrap',
                                gap: '0.5rem',
                            }}
                        >
                            <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                                {aiLocked ? (
                                    <>
                                        <button
                                            type="button"
                                            disabled
                                            title={AI_DISABLED_BEFORE_STORY_SAVED}
                                            className="flex cursor-not-allowed items-center gap-2 rounded-full border-0 bg-primary/10 px-4 py-2 text-sm font-bold text-primary/50 opacity-70"
                                        >
                                            <Sparkles style={{ width: '14px', height: '14px' }} />
                                            AI gợi ý ý tưởng
                                        </button>
                                        <button
                                            type="button"
                                            disabled
                                            title={AI_DISABLED_BEFORE_STORY_SAVED}
                                            className="flex cursor-not-allowed items-center gap-2 rounded-full border-0 bg-primary/10 px-4 py-2 text-sm font-bold text-primary/50 opacity-70"
                                        >
                                            <Sparkles style={{ width: '14px', height: '14px' }} />
                                            AI gợi ý chương
                                        </button>
                                    </>
                                ) : (
                                    <>
                                        <button
                                            type="button"
                                            onClick={() => handleAISuggestion('paragraph')}
                                            className="flex items-center gap-2 rounded-full border-0 bg-primary/10 px-4 py-2 text-sm font-bold text-primary transition-all hover:bg-primary/20"
                                        >
                                            <Sparkles style={{ width: '14px', height: '14px' }} />
                                            AI gợi ý ý tưởng
                                            {aiUsageLimit ? ` (${aiUsageLimit.remaining}/${aiUsageLimit.limitPerDay})` : ''}
                                        </button>
                                        <button
                                            type="button"
                                            onClick={() => handleAISuggestion('chapter')}
                                            className="flex items-center gap-2 rounded-full border-0 bg-primary/10 px-4 py-2 text-sm font-bold text-primary transition-all hover:bg-primary/20"
                                        >
                                            <Sparkles style={{ width: '14px', height: '14px' }} />
                                            AI gợi ý chương
                                            {aiUsageLimit ? ` (${aiUsageLimit.remaining}/${aiUsageLimit.limitPerDay})` : ''}
                                        </button>
                                    </>
                                )}
                            </div>
                            <button
                                type="button"
                                onClick={() => setShowSettings(!showSettings)}
                                className={`flex items-center gap-2 rounded-full border-0 px-4 py-2 text-sm font-bold transition-all ${showSettings ? 'bg-primary text-white' : 'bg-slate-100 text-slate-700 hover:bg-slate-200'
                                    }`}
                            >
                                <Settings style={{ width: '14px', height: '14px' }} />
                                Tùy chỉnh hiển thị
                            </button>
                        </div>
                        {aiLocked && (
                            <p
                                className="m-0 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2.5 text-xs leading-relaxed text-amber-950"
                                role="status"
                            >
                                {AI_DISABLED_BEFORE_STORY_SAVED}
                            </p>
                        )}
                    </div>

                    {/* Settings Panel */}
                    {showSettings && (
                        <div style={{
                            padding: '1rem',
                            backgroundColor: '#f9fafb',
                            borderRadius: '4px',
                            border: '1px solid #e5e7eb'
                        }}>
                            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1rem' }}>
                                <h4 style={{ fontSize: '0.875rem', fontWeight: 600, color: '#333333', margin: 0 }}>
                                    Cài đặt hiển thị
                                </h4>
                                <button
                                    onClick={() => setShowSettings(false)}
                                    style={{
                                        padding: '0.25rem',
                                        backgroundColor: 'transparent',
                                        border: 'none',
                                        cursor: 'pointer',
                                        color: '#6b7280'
                                    }}
                                >
                                    <X style={{ width: '16px', height: '16px' }} />
                                </button>
                            </div>

                            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1rem' }}>
                                {/* Font Size */}
                                <div>
                                    <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                        Cỡ chữ: {editorSettings.fontSize}px
                                    </label>
                                    <input
                                        type="range"
                                        min="12"
                                        max="24"
                                        value={editorSettings.fontSize}
                                        onChange={(e) => setEditorSettings({ ...editorSettings, fontSize: Number(e.target.value) })}
                                        style={{ width: '100%', cursor: 'pointer' }}
                                    />
                                </div>

                                {/* Font Family */}
                                <div>
                                    <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                        Font chữ
                                    </label>
                                    <select
                                        value={editorSettings.fontFamily}
                                        onChange={(e) => setEditorSettings({ ...editorSettings, fontFamily: e.target.value })}
                                        style={{
                                            width: '100%',
                                            padding: '0.375rem 0.5rem',
                                            backgroundColor: '#ffffff',
                                            border: '1px solid #e5e7eb',
                                            borderRadius: '4px',
                                            fontSize: '0.75rem',
                                            outline: 'none',
                                            cursor: 'pointer'
                                        }}
                                    >
                                        {fontFamilies.map((font) => (
                                            <option key={font.value} value={font.value}>
                                                {font.name}
                                            </option>
                                        ))}
                                    </select>
                                </div>

                                {/* Background Color */}
                                <div>
                                    <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                        Màu nền
                                    </label>
                                    <div style={{ display: 'flex', gap: '0.5rem' }}>
                                        {backgroundColors.map((bg) => (
                                            <button
                                                key={bg.value}
                                                onClick={() => setEditorSettings({ ...editorSettings, backgroundColor: bg.value })}
                                                title={bg.name}
                                                style={{
                                                    width: '32px',
                                                    height: '32px',
                                                    backgroundColor: bg.value,
                                                    border: editorSettings.backgroundColor === bg.value ? '2px solid #6ee7b7' : '1px solid #e5e7eb',
                                                    borderRadius: '4px',
                                                    cursor: 'pointer',
                                                    transition: 'all 0.2s'
                                                }}
                                            />
                                        ))}
                                    </div>
                                </div>
                            </div>
                        </div>
                    )}

                    {/* Chapter Content */}
                    <div>
                        <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                            Nội dung <span style={{ color: '#ef4444' }}>*</span>
                        </label>
                        <textarea
                            value={chapter.content}
                            onChange={(e) => onChange('content', e.target.value)}
                            placeholder="Nhập nội dung chương của bạn..."
                            rows={20}
                            style={{
                                width: '100%',
                                padding: '1rem',
                                backgroundColor: editorSettings.backgroundColor,
                                border: '1px solid #e5e7eb',
                                borderRadius: '4px',
                                fontSize: `${editorSettings.fontSize}px`,
                                fontFamily: editorSettings.fontFamily,
                                outline: 'none',
                                resize: 'vertical',
                                lineHeight: '1.8'
                            }}
                        />
                        <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: '0.5rem' }}>
                            <p style={{ fontSize: '0.75rem', color: countWords(chapter.content) < 500 ? '#ef4444' : '#9ca3af', margin: 0 }}>
                                Tối thiểu 500 từ
                            </p>
                            <p style={{ fontSize: '0.75rem', color: '#9ca3af', margin: 0 }}>
                                {countWords(chapter.content).toLocaleString()} từ
                            </p>
                        </div>
                    </div>
                </div>
            </div>
        </>
    );
}
