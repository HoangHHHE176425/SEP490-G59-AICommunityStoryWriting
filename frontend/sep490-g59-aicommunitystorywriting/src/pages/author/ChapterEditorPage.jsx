import { useState, useEffect, useRef } from 'react';
import { Sparkles, Settings, X, Save, ArrowLeft, Lock, Unlock, Coins, Copy, Check } from 'lucide-react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { useToast } from '../../components/author/story-editor/Toast';
import { RichTextEditor } from '../../components/common/RichTextEditor';
import { indexRag, suggestNextChapter, coCreate, checkBannedWords, checkChapterSpelling, compareChapterPreview, getAiUsageLimit, pickAiContextWarning } from '../../api/ai/aiApi';
import { getChapters, getChapterVersions } from '../../api/chapter/chapterApi';
import { refresh as refreshAuth } from '../../api/auth/authApi';
import { translateCoCreateOutlineLabels } from '../../utils/coCreateOutlineLabelsVi';
import { stripHtmlToText } from '../../utils/richText';

// Helper function to count words
const countWords = (text) => {
    const plain = stripHtmlToText(text);
    if (!plain) return 0;
    return plain.split(/\s+/).filter(word => word.length > 0).length;
};

/** Lấy chuỗi JSON dàn ý thực từ outline (bỏ hướng dẫn + ví dụ mẫu). Ưu tiên khối có "scenes" ở cuối chuỗi. */
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
    // Nếu có nhiều khối (ví dụ + thật), lấy khối cuối (thường là dàn ý thật)
    if (candidates.length > 0) return candidates[candidates.length - 1];
    // Có block markdown ```json ... ```
    const codeBlock = /```(?:json)?\s*([\s\S]*?)```/.exec(s);
    if (codeBlock) {
        const inner = codeBlock[1].trim();
        const firstBrace = inner.indexOf('{');
        if (firstBrace >= 0) return inner.slice(firstBrace);
        return inner;
    }
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

// Copy helper for AI suggestion cards (no toast; caller shows inline feedback).
async function copyTextToClipboard(text) {
    const s = typeof text === 'string' ? text : '';
    if (!s) return false;
    try {
        if (navigator?.clipboard?.writeText) {
            await navigator.clipboard.writeText(s);
            return true;
        }
    } catch {
        // fallback below
    }
    try {
        const ta = document.createElement('textarea');
        ta.value = s;
        ta.setAttribute('readonly', 'true');
        ta.style.position = 'fixed';
        ta.style.left = '-9999px';
        document.body.appendChild(ta);
        ta.select();
        const ok = document.execCommand('copy');
        document.body.removeChild(ta);
        return ok;
    } catch {
        return false;
    }
}

/** Kiểm tra có phải dàn ý ví dụ mẫu (Holmes, Tới hiện trường...) thì không hiển thị */
function isExampleOutline(scenes) {
    if (!Array.isArray(scenes) || scenes.length === 0) return false;
    const first = scenes[0];
    const title = (first?.title ?? first?.Title ?? '').toString();
    const summary = (first?.summary ?? first?.Summary ?? '').toString();
    const hasExampleTitle = /Tới hiện trường|Khám phá căn phòng|Phỏng vấn nhân chứng/i.test(title) || /Holmes và Watson tới căn nhà riêng/i.test(summary);
    const hasHolmes = JSON.stringify(scenes).includes('Holmes');
    return hasExampleTitle && hasHolmes;
}

/** Dàn ý: đổi "Scene 1/2/3" thành "Bối cảnh 1/2/3"; nếu là JSON scenes thì render Bối cảnh 1, 2, 3... (bỏ hướng dẫn/ví dụ) */
function formatOutlineForDisplay(outline) {
    if (!outline || !outline.trim()) return '';
    const normalizeOutlineText = (s) =>
        (s || '')
            .replace(/\\r\\n/g, '\n')
            .replace(/\\n/g, '\n')
            .replace(/\\t/g, ' ')
            .replace(/(^|\n)(\d+)\.(\S)/g, '$1$2. $3')
            .replace(/\n{3,}/g, '\n\n')
            .trim();
    const raw = normalizeOutlineText(outline.trim());
    const toParse = extractOutlineJson(raw);
    try {
        const parsed = JSON.parse(toParse);
        const scenes = parsed?.scenes ?? parsed?.Scenes;
        if (Array.isArray(scenes) && scenes.length > 0) {
            if (isExampleOutline(scenes)) return '';
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
        // Không phải JSON, xử lý plain text (chỉ phần không phải block hướng dẫn)
    }
    // Plain text: bỏ đoạn "Trả về JSON dàn ý" và block code mẫu, chỉ giữ nội dung thật
    let text = raw
        .replace(/\*\*Trả về JSON dàn ý\*\*[\s\S]*?^\{[\s\S]*?"scenes"\s*:[\s\S]*?\}\s*\}/im, '')
        .replace(/```(?:json)?[\s\S]*?```/g, '')
        .trim();
    if (text) {
        return translateCoCreateOutlineLabels(text.replace(/\bScene\s*(\d+)\b/gi, 'Bối cảnh $1'));
    }
    return translateCoCreateOutlineLabels(raw.replace(/\bScene\s*(\d+)\b/gi, 'Bối cảnh $1'));
}

/** Nội dung: bỏ nhãn "Scene 1:", "Scene 2:"... để ghép thành một khối văn hoàn chỉnh */
function mergeContentRemoveScenes(content) {
    if (!content || !content.trim()) return '';
    return content
        .replace(/\s*Scene\s*\d+\s*[-:]?\s*/gi, '\n\n')
        .replace(/\n{3,}/g, '\n\n')
        .trim();
}

/** Chỉ lấy phần nội dung văn bản chương để đưa vào ô nội dung (bỏ hết dàn ý, Bối cảnh N:, JSON, v.v.) */
function contentOnlyForChapter(raw) {
    if (!raw || !raw.trim()) return '';
    let s = raw.trim();
    // Bỏ block markdown/JSON (dàn ý mẫu hoặc JSON)
    s = s.replace(/```[\s\S]*?```/g, '');
    s = s.replace(/\{\s*["']?(?:scenes|Scenes)["']?\s*:[\s\S]*?\}\s*\}/g, '');
    // Bỏ đoạn bắt đầu bằng **Dàn ý** hoặc Dàn ý: đến hết đoạn (đến \n\n hoặc hết)
    s = s.replace(/(?:\*\*)?\s*Dàn ý\s*\*\*[^\n]*(?:\n(?![ \t]*\n)[^\n]*)*/gi, '');
    s = s.replace(/\n?\s*Dàn ý\s*:?[^\n]*(?:\n(?![ \t]*\n)[^\n]*)*/gi, '');
    // Bỏ dòng chỉ có "Bối cảnh N:" (tiêu đề dàn ý)
    s = s.replace(/^\s*Bối cảnh\s*\d+\s*:\s*$/gm, '');
    s = s.replace(/^\s*Bối cảnh\s*\d+\s*:\s*\n/gm, '');
    // Bỏ nhãn Scene / Bối cảnh trong nội dung, gộp đoạn
    s = mergeContentRemoveScenes(s);
    s = s.replace(/\bBối cảnh\s*\d+\s*:\s*/gi, '\n\n');
    return s.replace(/\n{3,}/g, '\n\n').trim();
}

export function ChapterEditorPage({ story, chapter, isCreateMode = false, sourceChapterForVersion, editingVersion, readOnly = false, onSave, onNavigateAfterSave, onCancel }) {
    const { showToast, ToastContainer } = useToast();
    const storyId = story?.id ?? story?.Id;
    const [chapterCheckModal, setChapterCheckModal] = useState({ open: false, loading: false, data: null, error: null, mode: 'banned' });
    /** Preview % AI trước khi lưu; chỉ khi bấm Xác nhận mới gọi onSave + ghi ai_similarity_percent */
    const [aiCompareModal, setAiCompareModal] = useState({
        open: false,
        loading: false,
        data: null,
        error: null,
        pendingSaveStatus: null,
    });

    const [chapterData, setChapterData] = useState(() => {
        if (editingVersion) {
            return {
                number: sourceChapterForVersion?.number ?? 1,
                title: editingVersion.titleSnapshot != null ? String(editingVersion.titleSnapshot) : '',
                content: editingVersion.contentSnapshot != null ? String(editingVersion.contentSnapshot) : '',
                status: sourceChapterForVersion?.status || 'draft',
                accessType: sourceChapterForVersion?.accessType || 'public',
                price: sourceChapterForVersion?.price ?? 0,
                changeSummary: '',
                versionNumber: Number(editingVersion.versionNumber) || 1,
            };
        }
        if (chapter) {
            return {
                number: chapter?.number ?? 1,
                title: chapter?.title || '',
                content: chapter?.content || '',
                status: chapter?.status || 'draft',
                accessType: chapter?.accessType || 'public',
                price: chapter?.price || 0,
                changeSummary: chapter?.changeSummary ?? '',
                versionNumber: 1,
            };
        }
        if (sourceChapterForVersion) {
            return {
                number: sourceChapterForVersion.number ?? 1,
                title: '',
                content: '',
                status: sourceChapterForVersion.status || 'draft',
                accessType: sourceChapterForVersion.accessType || 'public',
                price: sourceChapterForVersion.price ?? 0,
                changeSummary: '',
                versionNumber: 1,
            };
        }
        return {
            number: 1,
            title: '',
            content: '',
            status: 'draft',
            accessType: 'public',
            price: 0,
            changeSummary: '',
            versionNumber: 1,
        };
    });
    /** Số chương (1-based) đã tồn tại — dùng để gợi ý số tiếp theo và validate không nhập trùng */
    const [existingChapterNumbers, setExistingChapterNumbers] = useState(new Set());
    const [existingChapterTitles, setExistingChapterTitles] = useState(new Set());
    const [chapterNumberError, setChapterNumberError] = useState('');
    /** Danh sách version của chương (khi ở chế độ version) — dùng validate số version không trùng + trạng thái (pending_review) */
    const [existingVersionsForChapter, setExistingVersionsForChapter] = useState([]);
    const [versionNumberError, setVersionNumberError] = useState('');
    /** Điều kiện gửi xuất bản version: chương trước đã duyệt/từ chối + không có version chờ duyệt ở chương trước (đồng bộ ChapterListManager). */
    const [versionPublishEligibility, setVersionPublishEligibility] = useState({
        prevSequentialOk: false,
        prevHasPendingVersion: false,
    });
    /** Đã load xong dữ liệu để tính canSubmitVersion (tránh nút Xuất bản bật sẵn rồi mới disable). */
    const [versionEligibilityLoaded, setVersionEligibilityLoaded] = useState(false);
    const [versionsForChapterLoaded, setVersionsForChapterLoaded] = useState(false);
    /** Chương thường (tạo mới / chỉnh sửa): điều kiện gửi xuất bản theo thứ tự + không có phiên bản chờ duyệt ở chương hiện tại. */
    const [normalPublishEligibility, setNormalPublishEligibility] = useState({
        loaded: false,
        prevSequentialOk: true,
        selfHasPendingVersion: false,
    });

    const [showSettings, setShowSettings] = useState(false);
    const [editorSettings, setEditorSettings] = useState({
        fontSize: 16,
        fontFamily: 'Arial, sans-serif',
        backgroundColor: '#ffffff',
    });

    const [isSaving, setIsSaving] = useState(false);

    // Popup gợi ý chương tiếp theo (AI)
    const [showSuggestPopup, setShowSuggestPopup] = useState(false);
    const [suggestLoading, setSuggestLoading] = useState(false);
    const [suggestions, setSuggestions] = useState([]);
    const [suggestionsCache, setSuggestionsCache] = useState([]);
    const [suggestWarning, setSuggestWarning] = useState(null);
    const [suggestWarningCache, setSuggestWarningCache] = useState(null);
    const [suggestError, setSuggestError] = useState(null);
    const [aiUsageLimit, setAiUsageLimit] = useState(null);
    const [copiedSuggestionIndex, setCopiedSuggestionIndex] = useState(null);
    const copySuggestionFeedbackRef = useRef(null);

    useEffect(() => {
        if (!showSuggestPopup) {
            setCopiedSuggestionIndex(null);
            if (copySuggestionFeedbackRef.current) {
                clearTimeout(copySuggestionFeedbackRef.current);
                copySuggestionFeedbackRef.current = null;
            }
        }
    }, [showSuggestPopup]);

    const loadAiUsageLimit = async () => {
        try {
            const data = await getAiUsageLimit();
            setAiUsageLimit({
                suggestNextChapter: {
                    limitPerDay: Number(data?.suggestNextChapter?.limitPerDay ?? 0) || 0,
                    usedInWindow: Number(data?.suggestNextChapter?.usedInWindow ?? 0) || 0,
                    remaining: Number(data?.suggestNextChapter?.remaining ?? 0) || 0,
                    resetsAtUtc: data?.suggestNextChapter?.resetsAtUtc ?? null,
                },
                coCreate: {
                    limitPerDay: Number(data?.coCreate?.limitPerDay ?? 0) || 0,
                    usedInWindow: Number(data?.coCreate?.usedInWindow ?? 0) || 0,
                    remaining: Number(data?.coCreate?.remaining ?? 0) || 0,
                    resetsAtUtc: data?.coCreate?.resetsAtUtc ?? null,
                },
                coCreateAvailable: Boolean(data?.coCreateAvailable),
            });
        } catch {
            // ignore (user có thể chưa đăng nhập / BE lỗi)
            setAiUsageLimit(null);
        }
    };

    const decrementCoCreateUsageOptimistic = () => {
        setAiUsageLimit((prev) => {
            if (!prev || !prev.coCreate) return prev;
            const currentRemaining = Number(prev.coCreate.remaining ?? 0) || 0;
            const currentUsed = Number(prev.coCreate.usedInWindow ?? 0) || 0;
            return {
                ...prev,
                coCreate: {
                    ...prev.coCreate,
                    remaining: Math.max(0, currentRemaining - 1),
                    usedInWindow: currentUsed + 1,
                },
            };
        });
    };

    // Popup đồng sáng tác (AI gợi ý chương): bước 1 = nhập ý tưởng, bước 2 = xem kết quả + đồng ý
    const [showCoCreateIdeaPopup, setShowCoCreateIdeaPopup] = useState(false);
    const [showCoCreateResultPopup, setShowCoCreateResultPopup] = useState(false);
    const [useCoCreatePrompt, setUseCoCreatePrompt] = useState(false);
    const [coCreateIdea, setCoCreateIdea] = useState('');
    const [coCreateLoading, setCoCreateLoading] = useState(false);
    const [coCreateResult, setCoCreateResult] = useState(null);
    const [coCreateContextWarning, setCoCreateContextWarning] = useState(null);
    const [manualSpellingCheckLoading, setManualSpellingCheckLoading] = useState(false);

    const isNewChapter = !chapter;
    const isVersionMode = Boolean(sourceChapterForVersion);
    const isEditingChapterMode = !isVersionMode && !isCreateMode && Boolean(chapter);
    const storyTotalViews = Number(story?.totalViews ?? story?.TotalViews ?? 0) || 0;
    const canEnablePaidMode = storyTotalViews >= 500;
    const normalizeChapterTitle = (value) => (value ?? '').toString().trim().replace(/\s+/g, ' ').toLowerCase();

    // Pre-fill khi ở chế độ version: chỉnh sửa version thì lấy dữ liệu version; tạo version mới thì để trống (chỉ giữ number, versionNumber)
    useEffect(() => {
        if (!sourceChapterForVersion) return;
        if (editingVersion) {
            setChapterData((prev) => ({
                ...prev,
                number: sourceChapterForVersion.number ?? prev.number,
                title: editingVersion.titleSnapshot != null ? String(editingVersion.titleSnapshot) : '',
                content: editingVersion.contentSnapshot != null ? String(editingVersion.contentSnapshot) : '',
                status: sourceChapterForVersion.status || 'draft',
                accessType: sourceChapterForVersion.accessType || 'public',
                price: sourceChapterForVersion.price ?? 0,
                versionNumber: Number(editingVersion.versionNumber) || 1,
            }));
        } else {
            setChapterData((prev) => ({
                ...prev,
                number: sourceChapterForVersion.number ?? prev.number,
                title: '',
                content: '',
                status: sourceChapterForVersion.status || 'draft',
                accessType: sourceChapterForVersion.accessType || 'public',
                price: sourceChapterForVersion.price ?? 0,
                versionNumber: prev.versionNumber ?? 1,
            }));
        }
    }, [sourceChapterForVersion, editingVersion]);

    // Load danh sách version của chương khi ở chế độ version (để validate số version không trùng + tự tăng số phiên bản)
    useEffect(() => {
        const chapterId = sourceChapterForVersion?.id ?? sourceChapterForVersion?.Id;
        if (!isVersionMode || !chapterId) {
            setExistingVersionsForChapter([]);
            setVersionsForChapterLoaded(false);
            return;
        }
        setVersionsForChapterLoaded(false);
        getChapterVersions(chapterId)
            .then((list) => {
                const arr = Array.isArray(list) ? list : [];
                const mapped = arr.map((v) => ({
                    id: v.id ?? v.Id,
                    versionNumber: Number(v.versionNumber ?? v.VersionNumber ?? v.version_number ?? 0) || 0,
                    status: (v.status ?? v.Status ?? '').toString(),
                }));
                setExistingVersionsForChapter(mapped);
                // Khi tạo version mới: tự gán số phiên bản = max(đã có) + 1 để khớp với list bên ngoài (BE cũng gán next number)
                if (!editingVersion && mapped.length > 0) {
                    const nextNum = Math.max(...mapped.map((x) => x.versionNumber), 0) + 1;
                    setChapterData((prev) => ({ ...prev, versionNumber: nextNum }));
                } else if (!editingVersion) {
                    setChapterData((prev) => ({ ...prev, versionNumber: 1 }));
                }
            })
            .catch(() => setExistingVersionsForChapter([]))
            .finally(() => setVersionsForChapterLoaded(true));
    }, [isVersionMode, sourceChapterForVersion?.id, sourceChapterForVersion?.Id, editingVersion]);

    // Load điều kiện gửi xuất bản version — đồng bộ ChapterListManager (chương trước đã published/rejected, không có version pending ở chương trước)
    useEffect(() => {
        if (!isVersionMode || !storyId || !sourceChapterForVersion) {
            setVersionPublishEligibility({ prevSequentialOk: false, prevHasPendingVersion: false });
            setVersionEligibilityLoaded(false);
            return;
        }
        setVersionEligibilityLoaded(false);
        const chapterNumber = Number(sourceChapterForVersion.number ?? 1);
        const prevOrderIndex = chapterNumber - 2;
        getChapters({ storyId, page: 1, pageSize: 500 })
            .then((allRes) => {
                const allItems = allRes?.items ?? allRes?.Items ?? [];
                const arr = Array.isArray(allItems) ? allItems : [];
                if (chapterNumber <= 1) {
                    setVersionPublishEligibility({ prevSequentialOk: true, prevHasPendingVersion: false });
                    setVersionEligibilityLoaded(true);
                    return;
                }
                const prevChapter = arr.find((c) => Number(c.orderIndex ?? c.OrderIndex ?? 0) === prevOrderIndex);
                if (!prevChapter) {
                    setVersionPublishEligibility({ prevSequentialOk: false, prevHasPendingVersion: false });
                    setVersionEligibilityLoaded(true);
                    return;
                }
                const prevChapterId = prevChapter.id ?? prevChapter.Id;
                const prevSt = String(prevChapter.status ?? prevChapter.Status ?? '').toLowerCase();
                const prevProcessed = prevSt === 'published' || prevSt === 'rejected';
                getChapterVersions(prevChapterId)
                    .then((verList) => {
                        const vArr = Array.isArray(verList) ? verList : [];
                        const prevHasPendingVersion = vArr.some((v) => ((v.status ?? v.Status ?? '').toString().toLowerCase() === 'pending_review'));
                        const prevSequentialOk = prevProcessed && !prevHasPendingVersion;
                        setVersionPublishEligibility({ prevSequentialOk, prevHasPendingVersion });
                        setVersionEligibilityLoaded(true);
                    })
                    .catch(() => {
                        setVersionPublishEligibility({ prevSequentialOk: false, prevHasPendingVersion: false });
                        setVersionEligibilityLoaded(true);
                    });
            })
            .catch(() => {
                setVersionPublishEligibility({ prevSequentialOk: false, prevHasPendingVersion: false });
                setVersionEligibilityLoaded(true);
            });
    }, [isVersionMode, storyId, sourceChapterForVersion]);

    // Điều kiện Xuất bản chương thường (tạo mới / sửa) — cùng quy tắc tuần tự với ChapterListManager
    useEffect(() => {
        if (isVersionMode || !storyId) {
            setNormalPublishEligibility({ loaded: false, prevSequentialOk: true, selfHasPendingVersion: false });
            return;
        }
        const num = Number(chapterData.number);
        if (!Number.isInteger(num) || num < 1) {
            setNormalPublishEligibility({ loaded: true, prevSequentialOk: false, selfHasPendingVersion: false });
            return;
        }
        setNormalPublishEligibility((p) => ({ ...p, loaded: false }));
        const currentChapterId = chapter?.id ?? chapter?.Id ?? null;
        getChapters({ storyId, page: 1, pageSize: 500 })
            .then(async (allRes) => {
                const allItems = allRes?.items ?? allRes?.Items ?? [];
                const arr = Array.isArray(allItems) ? allItems : [];
                let selfHasPendingVersion = false;
                if (currentChapterId) {
                    try {
                        const selfVers = await getChapterVersions(currentChapterId);
                        const sv = Array.isArray(selfVers) ? selfVers : [];
                        selfHasPendingVersion = sv.some((v) => String(v.status ?? v.Status ?? '').toLowerCase() === 'pending_review');
                    } catch {
                        selfHasPendingVersion = false;
                    }
                }
                if (num === 1) {
                    setNormalPublishEligibility({ loaded: true, prevSequentialOk: true, selfHasPendingVersion });
                    return;
                }
                const prevOrderIndex = num - 2;
                const prevChapter = arr.find((c) => Number(c.orderIndex ?? c.OrderIndex ?? 0) === prevOrderIndex);
                if (!prevChapter) {
                    setNormalPublishEligibility({ loaded: true, prevSequentialOk: false, selfHasPendingVersion });
                    return;
                }
                const prevChapterId = prevChapter.id ?? prevChapter.Id;
                const prevSt = String(prevChapter.status ?? prevChapter.Status ?? '').toLowerCase();
                const prevProcessed = prevSt === 'published' || prevSt === 'rejected';
                try {
                    const prevVers = await getChapterVersions(prevChapterId);
                    const pv = Array.isArray(prevVers) ? prevVers : [];
                    const prevHasPendingVersion = pv.some((v) => String(v.status ?? v.Status ?? '').toLowerCase() === 'pending_review');
                    const prevSequentialOk = prevProcessed && !prevHasPendingVersion;
                    setNormalPublishEligibility({ loaded: true, prevSequentialOk, selfHasPendingVersion });
                } catch {
                    setNormalPublishEligibility({ loaded: true, prevSequentialOk: false, selfHasPendingVersion });
                }
            })
            .catch(() => setNormalPublishEligibility({ loaded: true, prevSequentialOk: false, selfHasPendingVersion: false }));
    }, [isVersionMode, storyId, chapterData.number, chapter?.id, chapter?.Id]);

    // Load danh sách chương để tính số chương tiếp theo (thêm mới) và validate trùng (số 1-based)
    useEffect(() => {
        if (!storyId) {
            setExistingChapterNumbers(new Set());
            setExistingChapterTitles(new Set());
            return;
        }
        getChapters({ storyId, page: 1, pageSize: 500 })
            .then((res) => {
                const items = res?.items ?? res?.Items ?? [];
                const arr = Array.isArray(items) ? items : [];
                const numbers = new Set(arr.map((c) => Number((c.orderIndex ?? c.OrderIndex ?? 0) + 1)));
                const currentChapterId = chapter?.id ?? chapter?.Id ?? null;
                const titles = new Set(
                    arr
                        .filter((c) => (c?.id ?? c?.Id ?? null) !== currentChapterId)
                        .map((c) => normalizeChapterTitle(c?.title ?? c?.Title ?? ''))
                        .filter(Boolean),
                );
                setExistingChapterNumbers(numbers);
                setExistingChapterTitles(titles);
                if (isNewChapter && !sourceChapterForVersion) {
                    const nextNumber = arr.length > 0
                        ? Math.max(...arr.map((c) => Number(c.orderIndex ?? c.OrderIndex ?? 0) + 1)) + 1
                        : 1;
                    setChapterData((prev) => ({ ...prev, number: nextNumber }));
                }
            })
            .catch(() => {
                setExistingChapterNumbers(new Set());
                setExistingChapterTitles(new Set());
            });
    }, [storyId, isNewChapter, sourceChapterForVersion, chapter?.id, chapter?.Id]);

    // Reload chapter data when chapter prop changes (chỉnh sửa chương). Khi chapter=null mà đang ở chế độ version (sourceChapterForVersion) thì không xóa form.
    useEffect(() => {
        if (chapter) {
            setChapterData({
                number: chapter.number ?? 1,
                title: chapter.title || '',
                content: chapter.content || '',
                status: chapter.status || 'draft',
                accessType: chapter.accessType || 'public',
                price: chapter.price || 0,
                changeSummary: chapter.changeSummary ?? '',
            });
        } else if (!sourceChapterForVersion) {
            setChapterData((prev) => ({
                ...prev,
                title: '',
                content: '',
                status: 'draft',
                accessType: 'public',
                price: 0,
                changeSummary: '',
            }));
        }
    }, [chapter, sourceChapterForVersion]);

    const fontFamilies = [
        { name: 'Arial', value: 'Arial, sans-serif' },
        { name: 'Times New Roman', value: 'Times New Roman, serif' },
        { name: 'Georgia', value: 'Georgia, serif' },
        { name: 'Courier New', value: 'Courier New, monospace' },
        { name: 'Verdana', value: 'Verdana, sans-serif' },
    ];

    const backgroundColors = [
        { name: 'Trắng', value: '#ffffff' },
        { name: 'Kem', value: '#fef6e4' },
        { name: 'Xanh nhạt', value: '#e8f5e9' },
        { name: 'Xám nhạt', value: '#f5f5f5' },
        { name: 'Be', value: '#f5f5dc' },
    ];

    const runSuggestIdeas = async () => {
        const storyId = story?.id ?? story?.Id;
        if (!storyId) {
            showToast('Không xác định được truyện. Vui lòng thử lại.', 'error');
            return;
        }

        setSuggestLoading(true);
        setSuggestions([]);
        setSuggestError(null);
        setSuggestWarning(null);
        setShowSuggestPopup(true);
        try {
            // Gọi index-rag nền (không chờ). Gợi ý chạy ngay; BE dùng RAG nếu đã index, không thì dùng Story Context.
            indexRag(storyId);
            const orderIdx = (Number(chapterData.number) || 1) - 1;
            let afterChapterId = null;
            if (orderIdx > 0) {
                try {
                    const chRes = await getChapters({ storyId, page: 1, pageSize: 500 });
                    const items = chRes?.items ?? chRes?.Items ?? [];
                    const arr = Array.isArray(items) ? items : [];
                    const prev = arr.find((c) => Number(c.orderIndex ?? c.OrderIndex ?? 0) === orderIdx - 1);
                    afterChapterId = prev?.id ?? prev?.Id ?? null;
                } catch {
                    afterChapterId = null;
                }
            }
            const chapterIdForAi = chapter?.id ?? chapter?.Id ?? null;
            const data = await suggestNextChapter(storyId, afterChapterId, null, chapterIdForAi);
            const list = data?.suggestions ?? data?.Suggestions ?? [];
            const normalized = Array.isArray(list) ? list : [];
            setSuggestions(normalized);
            setSuggestionsCache(normalized);
            const ctxWarn = pickAiContextWarning(data);
            const draftContextWarning = ctxWarn
                ? 'Lưu ý: Chương liền trước hiện vẫn ở trạng thái bản nháp. AI có thể chưa bám sát đầy đủ mạch mới nhất, bạn nên dùng gợi ý này để tham khảo và chỉnh sửa thêm.'
                : null;
            setSuggestWarning(draftContextWarning);
            setSuggestWarningCache(draftContextWarning);
            // Cập nhật số lượt còn lại sau khi gọi AI thành công
            loadAiUsageLimit();
        } catch (err) {
            const status = err?.response?.status;
            const msg = err?.response?.data?.message ?? err?.message ?? 'Lỗi khi gọi gợi ý AI.';
            if (status === 429) {
                showToast('Bạn đã gọi gợi ý quá nhiều lần. Vui lòng thử lại sau.', 'error');
                setSuggestError('Bạn đã gọi gợi ý quá nhiều lần. Vui lòng thử lại sau.');
            } else if (status === 403) {
                showToast(msg || 'Chỉ tác giả của truyện mới được sử dụng tính năng này.', 'error');
                setSuggestError(msg || 'Chỉ tác giả của truyện mới được sử dụng tính năng này.');
            } else {
                showToast(msg, 'error');
                setSuggestError(msg);
            }
            setSuggestions([]);
        } finally {
            setSuggestLoading(false);
        }
    };

    const openCachedSuggestions = () => {
        if (!Array.isArray(suggestionsCache) || suggestionsCache.length === 0) {
            showToast('Chưa có gợi ý nào trong phiên làm việc hiện tại.', 'info');
            return;
        }
        setSuggestError(null);
        setSuggestLoading(false);
        setSuggestWarning(suggestWarningCache);
        setSuggestions(suggestionsCache);
        setShowSuggestPopup(true);
    };

    const handleAISuggestion = async (type) => {
        if (type === 'paragraph') {
            await runSuggestIdeas();
        } else {
            // AI gợi ý chương (đồng sáng tác): mở popup nhập ý tưởng
            const storyId = story?.id ?? story?.Id;
            if (!storyId) {
                showToast('Không xác định được truyện. Vui lòng thử lại.', 'error');
                return;
            }
            setUseCoCreatePrompt(false);
            setCoCreateIdea('');
            setCoCreateResult(null);
            setShowCoCreateResultPopup(false);
            setShowCoCreateIdeaPopup(true);
        }
    };

    // Load số lượt AI khi vào trang (nếu đã đăng nhập)
    useEffect(() => {
        loadAiUsageLimit();
    }, []);

    const handleCoCreateSubmit = async () => {
        const storyId = story?.id ?? story?.Id;
        if (!storyId) return;
        const idea = useCoCreatePrompt ? (coCreateIdea || '').trim() : '';
        setCoCreateContextWarning(null);
        setCoCreateLoading(true);
        try {
            const chapterOrderIndex = (Number(chapterData.number) || 1) - 1;
            const chapterIdForAi = chapter?.id ?? chapter?.Id ?? null;
            const data = await coCreate(storyId, idea || null, { chapterOrderIndex, chapterId: chapterIdForAi });
            // Trừ ngay trên UI để người dùng thấy số lượt giảm tức thì.
            decrementCoCreateUsageOptimistic();
            // Đồng bộ lại với BE (không chặn UI).
            loadAiUsageLimit();
            const ctxWarnCo = pickAiContextWarning(data);
            setCoCreateContextWarning(
                ctxWarnCo
                    ? 'Lưu ý: Chương liền trước hiện vẫn ở trạng thái bản nháp. Nội dung AI có thể chưa bám sát hoàn toàn mạch mới nhất, bạn nên rà soát và chỉnh sửa lại trước khi lưu.'
                    : null
            );
            setCoCreateResult(data);
            setShowCoCreateIdeaPopup(false);
            setShowCoCreateResultPopup(true);
        } catch (err) {
            const status = err?.response?.status;
            const msg = err?.response?.data?.message ?? err?.message ?? 'Lỗi khi gọi AI hỗ trợ.';
            if (status === 429) showToast('Bạn đã gọi AI quá nhiều lần. Vui lòng thử lại sau.', 'error');
            else if (status === 403) showToast(msg || 'Chỉ tác giả của truyện mới được sử dụng.', 'error');
            else showToast(msg, 'error');
        } finally {
            setCoCreateLoading(false);
        }
    };

    const handleCoCreateApply = () => {
        const raw = coCreateResult?.finalContent ?? coCreateResult?.FinalContent ?? '';
        const content = contentOnlyForChapter(raw) || raw.trim();
        const suggestedTitle = (coCreateResult?.suggestedChapterTitle ?? coCreateResult?.SuggestedChapterTitle ?? '')
            .toString()
            .trim();
        if (content) {
            setChapterData((prev) => ({
                ...prev,
                content,
                isAiClean: true,
                ...(suggestedTitle ? { title: suggestedTitle } : {}),
            }));
            showToast(
                suggestedTitle
                    ? 'Đã áp dụng tên chương và nội dung. Bạn có thể chỉnh sửa và nhấn Lưu / Xuất bản.'
                    : 'Đã áp dụng nội dung. Bạn có thể chỉnh sửa và nhấn Lưu / Xuất bản.',
                'success'
            );
        } else {
            showToast('AI chưa trả về nội dung chương. Vui lòng thử lại với định hướng chi tiết hơn.', 'error');
            return;
        }
        setShowCoCreateResultPopup(false);
        setCoCreateResult(null);
    };

    const currentChapterNumber = chapter ? Number(chapter.number ?? chapter.chapterNumber ?? (chapter.orderIndex ?? chapter.OrderIndex ?? 0) + 1) : null;

    /** Điều kiện gửi xuất bản version — giống ChapterListManager: thứ tự + chương trước đã duyệt/từ chối; chương gốc không chờ duyệt; chỉ một phiên bản chờ duyệt. */
    const chapterNumberForVersion = isVersionMode ? Number(sourceChapterForVersion?.number ?? 1) : 0;
    const canSubmitForPublishVersion = versionPublishEligibility.prevSequentialOk;
    const hasOtherPendingVersion =
        existingVersionsForChapter.some(
            (v) => (v.status ?? '').toString().toLowerCase() === 'pending_review' && (v.id ?? '') !== (editingVersion?.id ?? editingVersion?.Id ?? '')
        );
    const chapterIsPendingReviewVersion = (sourceChapterForVersion?.status ?? '').toString().toLowerCase() === 'pending_review';
    const chapterIsPublishedVersion = (sourceChapterForVersion?.status ?? '').toString().toLowerCase() === 'published';
    /** Phiên bản đang chỉnh sửa đã ở trạng thái chờ duyệt thì không cho gửi lại. */
    const editingVersionIsPendingReview = (editingVersion?.status ?? '').toString().toLowerCase() === 'pending_review';
    const canSubmitVersion =
        versionEligibilityLoaded &&
        versionsForChapterLoaded &&
        canSubmitForPublishVersion &&
        !hasOtherPendingVersion &&
        !chapterIsPendingReviewVersion &&
        !editingVersionIsPendingReview &&
        !chapterIsPublishedVersion;
    const versionPublishTooltip =
        !versionEligibilityLoaded || !versionsForChapterLoaded
            ? 'Đang kiểm tra điều kiện gửi xuất bản...'
            : chapterIsPublishedVersion
                ? 'Chương đã xuất bản, không thể gửi duyệt phiên bản chỉnh sửa.'
                : editingVersionIsPendingReview
                    ? 'Phiên bản này đang chờ duyệt, không thể gửi lại.'
                    : !canSubmitForPublishVersion
                        ? `Chỉ được gửi khi chương ${chapterNumberForVersion - 1} đã có kết quả duyệt hoặc từ chối duyệt.`
                        : chapterIsPendingReviewVersion
                            ? 'Chương gốc đang chờ duyệt, không thể gửi phiên bản.'
                            : hasOtherPendingVersion
                                ? 'Chỉ được gửi một phiên bản tại một thời điểm. Hãy hủy phiên bản đang chờ duyệt trước.'
                                : 'Gửi phiên bản lên để duyệt xuất bản';

    const chapterIsPendingReviewEdit = (chapter?.status ?? '').toString().toLowerCase() === 'pending_review';
    const chapterIsPublishedEdit = (chapter?.status ?? '').toString().toLowerCase() === 'published';
    const canSubmitNormalChapterPublish =
        normalPublishEligibility.loaded &&
        normalPublishEligibility.prevSequentialOk &&
        !normalPublishEligibility.selfHasPendingVersion &&
        !chapterIsPendingReviewEdit &&
        !chapterIsPublishedEdit;
    const normalChapterPublishTooltip = !normalPublishEligibility.loaded
        ? 'Đang kiểm tra điều kiện gửi xuất bản...'
        : chapterIsPublishedEdit
            ? 'Chương đã xuất bản.'
            : chapterIsPendingReviewEdit
                ? 'Chương đang chờ duyệt.'
                : normalPublishEligibility.selfHasPendingVersion
                    ? 'Đã có phiên bản đang chờ duyệt, không thể gửi chương gốc.'
                    : !normalPublishEligibility.prevSequentialOk
                        ? `Chỉ được gửi khi chương ${(Number(chapterData.number) || 1) - 1} đã có kết quả duyệt hoặc từ chối duyệt.`
                        : 'Gửi chương lên để duyệt xuất bản';

    const validateChapterNumber = (num) => {
        const n = Number(num);
        if (n < 1 || !Number.isInteger(n)) return 'Số chương phải là số nguyên từ 1 trở lên.';
        if (existingChapterNumbers.has(n) && (currentChapterNumber == null || n !== currentChapterNumber)) {
            return `Chương ${n} đã tồn tại. Vui lòng chọn số khác.`;
        }
        return '';
    };

    const handleSave = async (saveStatus) => {
        if (!chapterData.title.trim()) {
            showToast(isVersionMode ? 'Vui lòng nhập tiêu đề phiên bản' : 'Vui lòng nhập tên chương', 'error');
            return;
        }
        // FE validation: tên chương không vượt quá 50 ký tự.
        if (!isVersionMode && chapterData.title.trim().length > 50) {
            showToast('Tên chương không được vượt quá 50 ký tự', 'error');
            return;
        }
        if (!stripHtmlToText(chapterData.content)) {
            showToast('Vui lòng nhập nội dung chương', 'error');
            return;
        }
        if (!isVersionMode && existingChapterTitles.has(normalizeChapterTitle(chapterData.title))) {
            showToast('Tên chương đã tồn tại trong truyện này. Vui lòng đặt tên khác.', 'error');
            return;
        }
        if (isVersionMode) {
            if (saveStatus === 'published' && chapterIsPublishedVersion) {
                showToast('Chương đã xuất bản không còn được gửi duyệt phiên bản chỉnh sửa.', 'error');
                return;
            }
            const vNum = Number(chapterData.versionNumber ?? 1);
            if (!Number.isInteger(vNum) || vNum < 1) {
                setVersionNumberError('Số phiên bản phải là số nguyên từ 1 trở lên');
                showToast('Số phiên bản phải là số nguyên từ 1 trở lên', 'error');
                return;
            }
            const takenNumbers = existingVersionsForChapter
                .filter((v) => v.id !== (editingVersion?.id ?? editingVersion?.Id))
                .map((v) => v.versionNumber);
            if (takenNumbers.includes(vNum)) {
                setVersionNumberError(`Số phiên bản ${vNum} đã tồn tại, vui lòng chọn số khác`);
                showToast(`Số phiên bản ${vNum} đã tồn tại`, 'error');
                return;
            }
            setVersionNumberError('');
            if (saveStatus === 'published' && !canSubmitVersion) {
                showToast(versionPublishTooltip, 'error');
                return;
            }
        } else {
            const num = Number(chapterData.number);
            const numError = validateChapterNumber(isNaN(num) ? 0 : num);
            if (numError) {
                setChapterNumberError(numError);
                showToast(numError, 'error');
                return;
            }
            setChapterNumberError('');
        }
        if (!isVersionMode && saveStatus === 'published' && (!normalPublishEligibility.loaded || !canSubmitNormalChapterPublish)) {
            showToast(normalChapterPublishTooltip, 'error');
            return;
        }
        const wordCount = countWords(chapterData.content);
        if (wordCount < 500) {
            showToast(`Nội dung chương cần ít nhất 500 từ (Hiện tại: ${wordCount} từ)`, 'error');
            return;
        }
        if (!isVersionMode && chapterData.accessType === 'paid' && !canEnablePaidMode) {
            showToast('Truyện cần tối thiểu 500 lượt xem mới được bật chế độ trả phí cho chương.', 'error');
            return;
        }
        if (!isVersionMode && chapterData.accessType === 'paid' && (!chapterData.price || chapterData.price <= 0)) {
            showToast('Vui lòng nhập giá cho chương trả phí', 'error');
            return;
        }

        // AI check khi lưu: chỉ kiểm tra từ cấm/chính sách.
        try {
            setChapterCheckModal({ open: false, loading: true, data: null, error: null, mode: 'banned' });
            const res = await checkBannedWords({
                content: stripHtmlToText(chapterData.content),
                storyId: storyId ?? null,
                chapterTitle: chapterData.title ?? null,
            });
            const policyViolations = res?.policyViolations ?? res?.PolicyViolations ?? [];
            const passed = Boolean(res?.passed ?? res?.Passed) &&
                Array.isArray(policyViolations) && policyViolations.length === 0 &&
                !(res?.hasInappropriateContent ?? res?.HasInappropriateContent);

            if (!passed) {
                const hasInappropriateContent = Boolean(res?.hasInappropriateContent ?? res?.HasInappropriateContent);
                setChapterCheckModal({
                    open: true,
                    loading: false,
                    error: null,
                    mode: 'banned',
                    data: {
                        passed: Boolean(res?.passed ?? res?.Passed),
                        summary: buildBannedWordsSummary(res?.summary ?? res?.Summary ?? null, policyViolations, hasInappropriateContent),
                        hasInappropriateContent,
                        policyViolations,
                    },
                });
                showToast('Nội dung có từ cấm/vi phạm chính sách. Vui lòng sửa trước khi lưu/xuất bản.', 'error');
                return;
            }
            setChapterCheckModal({ open: false, loading: false, data: null, error: null, mode: 'banned' });
        } catch (err) {
            const status = err?.response?.status;
            // Nếu 401: token hết hạn → refresh 1 lần rồi kiểm tra lại
            let retrySucceeded = false;

            if (status === 401) {
                try {
                    const refreshRes = await refreshAuth();
                    if (refreshRes?.success) {
                        const res2 = await checkBannedWords({
                            content: stripHtmlToText(chapterData.content),
                            storyId: storyId ?? null,
                            chapterTitle: chapterData.title ?? null,
                        });
                        const policyViolations2 = res2?.policyViolations ?? res2?.PolicyViolations ?? [];
                        const passed2 = Boolean(res2?.passed ?? res2?.Passed) &&
                            Array.isArray(policyViolations2) && policyViolations2.length === 0 &&
                            !(res2?.hasInappropriateContent ?? res2?.HasInappropriateContent);

                        if (!passed2) {
                            const hasInappropriateContent2 = Boolean(res2?.hasInappropriateContent ?? res2?.HasInappropriateContent);
                            setChapterCheckModal({
                                open: true,
                                loading: false,
                                error: null,
                                mode: 'banned',
                                data: {
                                    passed: Boolean(res2?.passed ?? res2?.Passed),
                                    summary: buildBannedWordsSummary(res2?.summary ?? res2?.Summary ?? null, policyViolations2, hasInappropriateContent2),
                                    hasInappropriateContent: hasInappropriateContent2,
                                    policyViolations: policyViolations2,
                                },
                            });
                            showToast('Nội dung có từ cấm/vi phạm chính sách. Vui lòng sửa trước khi lưu/xuất bản.', 'error');
                            return;
                        }

                        setChapterCheckModal({ open: false, loading: false, data: null, error: null, mode: 'banned' });
                        retrySucceeded = true;
                    }
                } catch {
                    // ignore, fallthrough below
                }
            }

            if (!retrySucceeded) {
                const data = err?.response?.data;
                const baseMsg = err?.response?.data?.message ?? err?.response?.data?.detail ?? err?.message ?? 'Không thể kiểm tra nội dung chương.';
                const dataJson = data
                    ? (typeof data === 'string' ? data : (() => { try { return JSON.stringify(data); } catch { return String(data); } })())
                    : '';
                const msg = dataJson && dataJson !== baseMsg ? `${baseMsg}\n${dataJson}` : baseMsg;
                setChapterCheckModal({ open: true, loading: false, data: null, error: msg, mode: 'banned' });
                showToast(msg, 'error');
                return;
            }
        }

        if (!isVersionMode && !storyId) {
            showToast('Không tìm thấy truyện', 'error');
            return;
        }

        // Chế độ phiên bản: lưu ngay (không so sánh AI trên chapter gốc)
        if (isVersionMode) {
            setIsSaving(true);
            try {
                const payload = {
                    ...chapterData,
                    status: saveStatus,
                    updatedAt: new Date().toLocaleString('vi-VN'),
                };
                if (sourceChapterForVersion?.id) {
                    payload.sourceChapterId = sourceChapterForVersion.id;
                    payload.versionNumber = chapterData.versionNumber ?? 1;
                    if (editingVersion?.id) payload.editingVersionId = editingVersion.id;
                }
                await onSave(payload);
                onNavigateAfterSave?.();
            } catch (error) {
                console.error('Error saving chapter:', error);
            } finally {
                setIsSaving(false);
            }
            return;
        }

        // Chương thường: luôn gọi preview — BE tra `ai_generated_content` theo story + thứ tự chương (kể cả khi user chỉ copy–paste bản AI, không bấm «đồng ý dùng» trong UI). Không có bản ghi → lưu thẳng, không popup.
        setIsSaving(true);
        try {
            const orderIndex = (Number(chapterData.number) || 1) - 1;
            const cid = chapter?.id ?? chapter?.Id ?? null;
            const cmp = await compareChapterPreview({
                ...(cid ? { chapterId: cid } : { storyId, orderIndex }),
                content: stripHtmlToText(chapterData.content),
            });
            const hasBoth = Boolean(cmp?.hasBothContents ?? cmp?.HasBothContents);
            const score = cmp?.similarityScore ?? cmp?.SimilarityScore;
            const msg = (cmp?.message ?? cmp?.Message ?? '').toString();
            // Khớp message từ ChapterCompareService khi không có ai_records cho chapter_index
            const noAiBaselineToCompare =
                !hasBoth && /Chưa có bản nội dung AI/i.test(msg);
            if (noAiBaselineToCompare) {
                const payload = {
                    ...chapterData,
                    status: saveStatus,
                    updatedAt: new Date().toLocaleString('vi-VN'),
                };
                await onSave(payload);
                onNavigateAfterSave?.();
                return;
            }
            const scoreNum = typeof score === 'number' ? score : Number(score);
            const pct = hasBoth && Number.isFinite(scoreNum) ? scoreNum : undefined;
            const shouldSkipAiComparePopup =
                // Không tính ra được AI similarity (hoặc không có đủ dữ liệu so sánh)
                !hasBoth ||
                pct == null ||
                // Hoặc similarity < 40%
                (pct != null && pct < 40);

            // Rule: nếu không có / không tính được AI similarity hoặc pct < 40%
            // => không hiển thị popup xác nhận, mà lưu nháp/xuất bản luôn (không cập nhật ai_similarity_percent).
            if (shouldSkipAiComparePopup) {
                if (!isVersionMode && saveStatus === 'published' && (!normalPublishEligibility.loaded || !canSubmitNormalChapterPublish)) {
                    showToast(normalChapterPublishTooltip, 'error');
                    return;
                }
                const payload = {
                    ...chapterData,
                    status: saveStatus,
                    updatedAt: new Date().toLocaleString('vi-VN'),
                    aiSimilarityPercent: undefined,
                };
                try {
                    await onSave(payload);
                    onNavigateAfterSave?.();
                } catch (error) {
                    showToast(error?.message || 'Không thể lưu chương', 'error');
                }
                return;
            }
            setAiCompareModal({
                open: true,
                loading: false,
                data: {
                    hasBothContents: hasBoth,
                    similarityScore: typeof score === 'number' ? score : Number(score),
                    message: msg,
                },
                error: null,
                pendingSaveStatus: saveStatus,
            });
        } catch (cmpErr) {
            // Nếu BE fail / không tính được AI similarity => không mở popup nữa, vẫn cho lưu/xuất bản.
            const msg =
                cmpErr?.response?.data?.message ??
                cmpErr?.response?.data?.Message ??
                cmpErr?.message ??
                'Không thể tính độ tương đồng với bản AI.';
            if (!isVersionMode && saveStatus === 'published' && (!normalPublishEligibility.loaded || !canSubmitNormalChapterPublish)) {
                showToast(normalChapterPublishTooltip, 'error');
                return;
            }
            try {
                const payload = {
                    ...chapterData,
                    status: saveStatus,
                    updatedAt: new Date().toLocaleString('vi-VN'),
                    aiSimilarityPercent: undefined,
                };
                await onSave(payload);
                onNavigateAfterSave?.();
            } catch {
                showToast(String(msg), 'error');
            }
        } finally {
            setIsSaving(false);
        }
    };

    const handleManualBannedWordsCheck = async () => {
        if (!stripHtmlToText(chapterData.content)) {
            showToast('Vui lòng nhập nội dung chương trước khi kiểm tra.', 'error');
            return;
        }
        setManualSpellingCheckLoading(true);
        setChapterCheckModal({ open: true, loading: true, data: null, error: null, mode: 'spelling-support' });
        try {
            const res = await checkChapterSpelling({
                content: stripHtmlToText(chapterData.content),
                storyId: storyId ?? null,
                chapterTitle: chapterData.title ?? null,
            });
            const spellingIssues = res?.spellingIssues ?? res?.SpellingIssues ?? [];
            setChapterCheckModal({
                open: true,
                loading: false,
                error: null,
                mode: 'spelling-support',
                data: {
                    passed: Boolean(res?.passed ?? res?.Passed),
                    summary: buildSpellingSummary(res?.summary ?? res?.Summary ?? null, spellingIssues),
                    spellingIssues,
                },
            });
            const normalizedSummary = buildSpellingSummary(res?.summary ?? res?.Summary ?? null, spellingIssues);
            if (Array.isArray(spellingIssues) && spellingIssues.length === 0 && !summaryImpliesSpellingIssue(normalizedSummary)) {
                showToast('Hỗ trợ kiểm tra chính tả: không phát hiện lỗi chính tả.', 'success');
            }
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.response?.data?.detail ?? err?.message ?? 'Không thể kiểm tra chính tả.';
            setChapterCheckModal({ open: true, loading: false, data: null, error: msg, mode: 'spelling-support' });
            showToast(`Hỗ trợ kiểm tra chính tả: ${msg}`, 'error');
        } finally {
            setManualSpellingCheckLoading(false);
        }
    };

    const closeAiCompareModalOnly = () => {
        setAiCompareModal({
            open: false,
            loading: false,
            data: null,
            error: null,
            pendingSaveStatus: null,
        });
    };

    const confirmAiCompareSave = async () => {
        const status = aiCompareModal.pendingSaveStatus;
        if (!status) return;
        if (!isVersionMode && status === 'published' && (!normalPublishEligibility.loaded || !canSubmitNormalChapterPublish)) {
            showToast(normalChapterPublishTooltip, 'error');
            closeAiCompareModalOnly();
            return;
        }
        const hasPct =
            aiCompareModal.data?.hasBothContents &&
            Number.isFinite(Number(aiCompareModal.data?.similarityScore));
        const pct = hasPct ? Number(aiCompareModal.data.similarityScore) : undefined;
        closeAiCompareModalOnly();
        setIsSaving(true);
        try {
            const shouldSendAiSimilarity = pct != null && pct >= 40;
            const payload = {
                ...chapterData,
                status,
                updatedAt: new Date().toLocaleString('vi-VN'),
                // Bỏ cập nhật ai_similarity_percent khi pct < 40.
                aiSimilarityPercent: shouldSendAiSimilarity ? pct : undefined,
            };
            await onSave(payload);
            onNavigateAfterSave?.();
        } catch (error) {
            console.error('Error saving chapter:', error);
        } finally {
            setIsSaving(false);
        }
    };

    // Tìm vị trí (đoạn + dòng) của từ/cụm trong nội dung hiện tại để hiển thị "vị trí phạm lỗi"
    const findIssuePosition = (needleRaw) => {
        const needle = (needleRaw ?? '').toString().trim();
        const content = (chapterData?.content ?? '').toString();
        if (!needle || !content) return null;

        const lowerNeedle = needle.toLowerCase();
        const lines = content.split(/\r?\n/);

        // Line index (1-based)
        const lineIndex = lines.findIndex((ln) => ln.toLowerCase().includes(lowerNeedle));
        const lineNo = lineIndex >= 0 ? lineIndex + 1 : null;

        // Paragraph index (1-based) - paragraph = block separated by blank lines
        const paragraphs = content.split(/\r?\n\s*\r?\n/);
        const paraIndex = paragraphs.findIndex((p) => p.toLowerCase().includes(lowerNeedle));
        const paraNo = paraIndex >= 0 ? paraIndex + 1 : null;

        // Character offset (1-based) - first occurrence in full content
        const idx = content.toLowerCase().indexOf(lowerNeedle);
        const charOffset = idx >= 0 ? idx + 1 : null;

        if (lineNo == null && paraNo == null && charOffset == null) return null;
        return { lineNo, paraNo, charOffset };
    };

    const policyTypeVi = (typeRaw) => {
        const t = String(typeRaw ?? '').trim().toUpperCase();
        if (t === 'BANNEDWORD') return 'Từ cấm';
        return typeRaw || '—';
    };

    const buildBannedWordsSummary = (rawSummary, policyViolations, hasInappropriateContent) => {
        const count = Array.isArray(policyViolations) ? policyViolations.length : 0;
        if (count > 0) return `Phát hiện ${count} vi phạm từ cấm/chính sách.`;
        if (hasInappropriateContent) return 'Nội dung có dấu hiệu không phù hợp theo chính sách.';
        const s = String(rawSummary ?? '').trim();
        if (!s) return 'Không phát hiện vi phạm từ cấm/chính sách.';
        if (/ch[ií]nh t[ảa]/i.test(s)) return 'Không phát hiện vi phạm từ cấm/chính sách.';
        return s;
    };

    const buildSpellingSummary = (rawSummary, spellingIssues) => {
        const count = Array.isArray(spellingIssues) ? spellingIssues.length : 0;
        if (count > 0) return `Phát hiện ${count} lỗi chính tả.`;
        const s = String(rawSummary ?? '').trim();
        if (!s) return 'Không phát hiện lỗi chính tả.';
        if (/t[ừu]\s*c[ấa]m|ch[ií]nh\s*s[áa]ch/i.test(s)) return 'Không phát hiện lỗi chính tả.';
        return s;
    };

    /** Khớp ý backend SummaryIndicatesSpellingIssue: tránh báo nhầm khi tóm tắt là «Không phát hiện lỗi chính tả» (vẫn chứa cụm «lỗi chính tả»). */
    const summaryImpliesSpellingIssue = (summaryText) => {
        const s = String(summaryText ?? '').trim().toLowerCase();
        if (!s) return false;
        if (s.includes('không phát hiện')) return false;
        if (/không\s+có\s+lỗi\s+chính\s+tả/i.test(s)) return false;
        if (/không\s+còn\s+lỗi\s+chính\s+tả/i.test(s)) return false;
        return s.includes('lỗi chính tả') || s.includes('dấu câu');
    };

    const chapterCheckDialogTitle = chapterCheckModal.mode === 'spelling-support'
        ? 'Kết quả hỗ trợ kiểm tra chính tả'
        : 'Kết quả kiểm tra từ cấm/chính sách';
    const chapterCheckDialogSubtitle = chapterCheckModal.mode === 'spelling-support'
        ? 'Hiển thị lỗi chính tả và gợi ý chỉnh sửa nội dung.'
        : 'Vui lòng sửa các vi phạm bên dưới trước khi lưu hoặc xuất bản.';

    return (
        <div>
            <Header />
            <ToastContainer />
            {/* Popup AI check từ cấm/chính sách */}
            {chapterCheckModal.open && (
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
                    onClick={() => !chapterCheckModal.loading && setChapterCheckModal((p) => ({ ...p, open: false }))}
                >
                    <div
                        style={{
                            backgroundColor: '#ffffff',
                            borderRadius: '12px',
                            maxWidth: '820px',
                            width: '92%',
                            maxHeight: '85vh',
                            overflow: 'hidden',
                            display: 'flex',
                            flexDirection: 'column',
                            boxShadow: '0 20px 25px -5px rgba(0,0,0,0.1), 0 8px 10px -6px rgba(0,0,0,0.1)',
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div style={{ padding: '16px 18px', borderBottom: '1px solid #e5e7eb', display: 'flex', justifyContent: 'space-between', alignItems: 'center', gap: '12px' }}>
                            <div style={{ minWidth: 0 }}>
                                <div style={{ fontSize: '1rem', fontWeight: 800, color: '#0f172a' }}>{chapterCheckDialogTitle}</div>
                                <div style={{ fontSize: '0.8125rem', color: '#64748b', marginTop: '2px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                    {chapterCheckDialogSubtitle}
                                </div>
                            </div>
                            <button
                                type="button"
                                disabled={chapterCheckModal.loading}
                                onClick={() => setChapterCheckModal((p) => ({ ...p, open: false }))}
                                style={{ border: 'none', background: 'transparent', cursor: chapterCheckModal.loading ? 'not-allowed' : 'pointer', padding: '6px', borderRadius: '8px' }}
                                title="Đóng"
                            >
                                <X size={18} />
                            </button>
                        </div>

                        <div style={{ padding: '16px 18px', overflowY: 'auto' }}>
                            {chapterCheckModal.loading ? (
                                <div style={{ padding: '24px', textAlign: 'center', color: '#64748b' }}>Đang kiểm tra...</div>
                            ) : chapterCheckModal.error ? (
                                <div style={{ padding: '12px 14px', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '10px', color: '#b91c1c', fontSize: '0.875rem' }}>
                                    {chapterCheckModal.error}
                                </div>
                            ) : (
                                <>
                                    {chapterCheckModal.data?.summary && (
                                        <div style={{ padding: '12px 14px', backgroundColor: '#f8fafc', border: '1px solid #e2e8f0', borderRadius: '10px', color: '#0f172a', fontSize: '0.875rem', marginBottom: '12px' }}>
                                            <div style={{ fontWeight: 800, marginBottom: '6px' }}>Tóm tắt</div>
                                            <div style={{ color: '#334155', whiteSpace: 'pre-wrap' }}>{chapterCheckModal.data.summary}</div>
                                        </div>
                                    )}

                                    {chapterCheckModal.mode !== 'spelling-support' && chapterCheckModal.data?.hasInappropriateContent && (
                                        <div style={{ padding: '12px 14px', backgroundColor: '#fff7ed', border: '1px solid #fed7aa', borderRadius: '10px', color: '#9a3412', fontSize: '0.875rem', marginBottom: '12px' }}>
                                            Nội dung có dấu hiệu không phù hợp theo chính sách nền tảng. Vui lòng chỉnh sửa trước khi lưu/xuất bản.
                                        </div>
                                    )}

                                    {chapterCheckModal.mode === 'spelling-support' ? (
                                        <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '12px' }}>
                                            <div style={{ border: '1px solid #e5e7eb', borderRadius: '12px', overflow: 'hidden' }}>
                                                <div style={{ padding: '10px 12px', backgroundColor: '#f9fafb', borderBottom: '1px solid #e5e7eb', fontWeight: 800, color: '#0f172a' }}>
                                                    Lỗi chính tả ({(chapterCheckModal.data?.spellingIssues?.length ?? 0).toLocaleString()})
                                                </div>
                                                <div style={{ padding: '10px 12px' }}>
                                                    {(chapterCheckModal.data?.spellingIssues ?? []).length === 0 ? (
                                                        summaryImpliesSpellingIssue(chapterCheckModal.data?.summary) ? (
                                                            <div style={{ color: '#9a3412', fontSize: '0.875rem', backgroundColor: '#fff7ed', border: '1px solid #fed7aa', borderRadius: '10px', padding: '10px 12px' }}>
                                                                Phần tóm tắt cho biết có thể còn lỗi chính tả, nhưng không trích được câu/dòng cụ thể từ nội dung. Hãy đọc tóm tắt và rà lại bài.
                                                            </div>
                                                        ) : (
                                                            <div style={{ color: '#64748b', fontSize: '0.875rem' }}>Không phát hiện lỗi chính tả.</div>
                                                        )
                                                    ) : (
                                                        <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                                                            {(chapterCheckModal.data?.spellingIssues ?? []).map((it, idx) => {
                                                                const word = it.wordOrPhrase ?? it.WordOrPhrase ?? '';
                                                                const sug = it.suggestion ?? it.Suggestion ?? '';
                                                                const ctx = it.context ?? it.Context ?? '';
                                                                return (
                                                                    <div key={idx} style={{ padding: '10px 12px', border: '1px solid #e2e8f0', borderRadius: '10px' }}>
                                                                        <div style={{ fontSize: '0.875rem', color: '#0f172a' }}>
                                                                            <span style={{ fontWeight: 800 }}>Từ/Cụm</span>: <span style={{ fontWeight: 800, color: '#b91c1c' }}>{word || '—'}</span>
                                                                        </div>
                                                                        <div style={{ fontSize: '0.875rem', color: '#0f172a', marginTop: '4px' }}>
                                                                            <span style={{ fontWeight: 800 }}>Gợi ý</span>: <span style={{ color: '#15803d', fontWeight: 800 }}>{sug || '—'}</span>
                                                                        </div>
                                                                        {String(ctx).trim() ? (
                                                                            <div
                                                                                style={{
                                                                                    marginTop: '8px',
                                                                                    fontSize: '0.8125rem',
                                                                                    color: '#334155',
                                                                                    backgroundColor: '#f8fafc',
                                                                                    border: '1px solid #e2e8f0',
                                                                                    borderRadius: '10px',
                                                                                    padding: '8px 10px',
                                                                                    whiteSpace: 'pre-wrap',
                                                                                }}
                                                                            >
                                                                                <span style={{ fontWeight: 800 }}>Câu/dòng chứa lỗi</span>
                                                                                <div style={{ marginTop: '4px' }}>{ctx}</div>
                                                                            </div>
                                                                        ) : (
                                                                            <div style={{ marginTop: '8px', fontSize: '0.8125rem', color: '#94a3b8' }}>
                                                                                Không có đoạn trích chứa từ sai cho mục này.
                                                                            </div>
                                                                        )}
                                                                    </div>
                                                                );
                                                            })}
                                                        </div>
                                                    )}
                                                </div>
                                            </div>
                                        </div>
                                    ) : (
                                        <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '12px' }}>
                                            <div style={{ border: '1px solid #e5e7eb', borderRadius: '12px', overflow: 'hidden' }}>
                                                <div style={{ padding: '10px 12px', backgroundColor: '#f9fafb', borderBottom: '1px solid #e5e7eb', fontWeight: 800, color: '#0f172a' }}>
                                                    Từ cấm / vi phạm chính sách ({(chapterCheckModal.data?.policyViolations?.length ?? 0).toLocaleString()})
                                                </div>
                                                <div style={{ padding: '10px 12px' }}>
                                                    {(chapterCheckModal.data?.policyViolations ?? []).length === 0 ? (
                                                        <div style={{ color: '#64748b', fontSize: '0.875rem' }}>Không phát hiện vi phạm.</div>
                                                    ) : (
                                                        <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                                                            {(chapterCheckModal.data?.policyViolations ?? []).map((it, idx) => {
                                                                const type = it.type ?? it.Type ?? '';
                                                                const desc = it.description ?? it.Description ?? '';
                                                                const quote = it.quote ?? it.Quote ?? '';
                                                                return (
                                                                    <div key={idx} style={{ padding: '10px 12px', border: '1px solid #fee2e2', borderRadius: '10px', backgroundColor: '#fff7ed' }}>
                                                                        <div style={{ fontSize: '0.875rem', color: '#9a3412' }}>
                                                                            <span style={{ fontWeight: 800 }}>Loại</span>: <span style={{ fontWeight: 800 }}>{policyTypeVi(type)}</span>
                                                                        </div>
                                                                        <div style={{ fontSize: '0.875rem', color: '#9a3412', marginTop: '4px', whiteSpace: 'pre-wrap' }}>
                                                                            {desc || '—'}
                                                                        </div>
                                                                        {quote ? (
                                                                            <div style={{ marginTop: '8px', fontSize: '0.8125rem', color: '#7c2d12', backgroundColor: '#fffbeb', border: '1px dashed #fdba74', borderRadius: '10px', padding: '8px 10px', whiteSpace: 'pre-wrap' }}>
                                                                                {quote}
                                                                            </div>
                                                                        ) : (
                                                                            <div style={{ marginTop: '8px', fontSize: '0.8125rem', color: '#7c2d12' }}>
                                                                                Không có đoạn trích chứa từ cấm cho mục này.
                                                                            </div>
                                                                        )}
                                                                    </div>
                                                                );
                                                            })}
                                                        </div>
                                                    )}
                                                </div>
                                            </div>
                                        </div>
                                    )}
                                </>
                            )}
                        </div>

                        <div style={{ padding: '12px 18px', borderTop: '1px solid #e5e7eb', display: 'flex', justifyContent: 'flex-end', gap: '10px' }}>
                            <button
                                type="button"
                                disabled={chapterCheckModal.loading}
                                onClick={() => setChapterCheckModal((p) => ({ ...p, open: false }))}
                                style={{
                                    padding: '10px 14px',
                                    borderRadius: '10px',
                                    border: '1px solid #e2e8f0',
                                    backgroundColor: '#fff',
                                    fontWeight: 800,
                                    cursor: chapterCheckModal.loading ? 'not-allowed' : 'pointer',
                                }}
                            >
                                Đóng
                            </button>
                        </div>
                    </div>
                </div>
            )}
            {/* Popup preview % AI — chỉ khi Xác nhận mới lưu DB + ai_similarity_percent */}
            {aiCompareModal.open && (
                <div
                    className="fixed inset-0 z-[10000] flex items-center justify-center bg-black/50 p-4"
                    onClick={() => !aiCompareModal.loading && closeAiCompareModalOnly()}
                    role="presentation"
                >
                    <div
                        className="w-full max-w-md overflow-hidden rounded-xl border border-slate-200 bg-white shadow-xl"
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div className="border-b border-slate-200 bg-slate-50/80 px-5 py-4">
                            <h3 className="m-0 text-lg font-bold text-slate-900">
                                Độ tương đồng với nội dung AI
                            </h3>
                            <p className="mt-1.5 text-sm leading-relaxed text-slate-600">
                                Xem trước mức giống giữa bản bạn viết và bản đồng sáng tác AI cho thứ tự chương này.{' '}
                                <span className="font-semibold text-primary">
                                    Chương chỉ được lưu / gửi xuất bản khi bạn bấm xác nhận bên dưới.
                                </span>
                            </p>
                        </div>
                        <div className="px-5 py-5">
                            {aiCompareModal.loading ? (
                                <p className="m-0 text-center text-sm text-slate-600">Đang tính toán độ tương đồng...</p>
                            ) : aiCompareModal.error ? (
                                <div className="rounded-lg border border-red-200 bg-red-50 px-3.5 py-3 text-sm text-red-800 whitespace-pre-wrap">
                                    {aiCompareModal.error}
                                    <p className="mt-2 mb-0 text-xs text-red-700/90">
                                        Bạn vẫn có thể xác nhận để lưu chương; phần trăm AI có thể để trống nếu không tính được.
                                    </p>
                                </div>
                            ) : aiCompareModal.data?.hasBothContents ? (
                                <div className="text-center">
                                    <div className="text-[2.65rem] font-extrabold leading-tight text-primary tabular-nums">
                                        {Number.isFinite(Number(aiCompareModal.data?.similarityScore))
                                            ? `${Number(aiCompareModal.data.similarityScore).toFixed(2)}%`
                                            : '—'}
                                    </div>
                                    {aiCompareModal.data?.message ? (
                                        <p className="mt-3 text-left text-sm text-slate-600">{aiCompareModal.data.message}</p>
                                    ) : null}
                                </div>
                            ) : (
                                <p className="m-0 text-sm leading-relaxed text-slate-700 whitespace-pre-wrap">
                                    {aiCompareModal.data?.message ||
                                        'Chưa có đủ dữ liệu để tính % (cần ít nhất một bản AI co-create cho đúng thứ tự chương). Bạn vẫn có thể xác nhận để lưu chương.'}
                                </p>
                            )}
                        </div>
                        <div className="flex flex-wrap items-center justify-end gap-2 border-t border-slate-200 bg-slate-50/50 px-4 py-3">
                            <button
                                type="button"
                                disabled={aiCompareModal.loading}
                                onClick={closeAiCompareModalOnly}
                                className="rounded-full border border-slate-300 bg-white px-4 py-2.5 text-sm font-bold text-slate-700 shadow-sm transition hover:bg-slate-50 disabled:cursor-not-allowed disabled:opacity-60"
                            >
                                Hủy
                            </button>
                            <button
                                type="button"
                                disabled={aiCompareModal.loading}
                                onClick={confirmAiCompareSave}
                                className="rounded-full bg-primary px-5 py-2.5 text-sm font-bold text-primary-foreground shadow-sm transition hover:bg-primary/90 disabled:cursor-not-allowed disabled:opacity-60"
                            >
                                {aiCompareModal.pendingSaveStatus === 'published'
                                    ? 'Xác nhận xuất bản'
                                    : 'Xác nhận lưu nháp'}
                            </button>
                        </div>
                    </div>
                </div>
            )}
            {/* Popup gợi ý chương tiếp theo (AI) */}
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
                                AI gợi ý ý tưởng
                            </h3>
                        </div>
                        <div style={{ padding: '1.25rem 1.5rem', overflowY: 'auto', flex: 1 }}>
                            {!suggestLoading && suggestWarning ? (
                                <div style={{ marginBottom: '0.75rem', padding: '12px 14px', backgroundColor: '#fffbeb', border: '1px solid #fde68a', borderRadius: '10px', color: '#92400e', fontSize: '0.875rem', lineHeight: 1.5 }}>
                                    {suggestWarning}
                                </div>
                            ) : null}
                            {suggestLoading ? (
                                <p style={{ margin: 0, color: '#6b7280', textAlign: 'center' }}>Đang tải gợi ý...</p>
                            ) : suggestError ? (
                                <div style={{ padding: '12px 14px', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '10px', color: '#b91c1c', fontSize: '0.875rem' }}>
                                    {suggestError}
                                </div>
                            ) : suggestions.length === 0 ? (
                                <p style={{ margin: 0, color: '#6b7280', textAlign: 'center' }}>Không có gợi ý.</p>
                            ) : (
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                                    {suggestions.map((item, index) => {
                                        const title = item?.title ?? item?.Title ?? '';
                                        const summary = item?.summary ?? item?.Summary ?? '';
                                        const direction = item?.direction ?? item?.Direction ?? '';
                                        const copyPayload = [title, summary, direction].filter(Boolean).join('\n');
                                        return (
                                            <div
                                                key={index}
                                                style={{
                                                    padding: '1rem',
                                                    backgroundColor: '#f9fafb',
                                                    borderRadius: '8px',
                                                    border: '1px solid #e5e7eb',
                                                    position: 'relative',
                                                }}
                                            >
                                                <button
                                                    type="button"
                                                    onClick={async () => {
                                                        const ok = await copyTextToClipboard(copyPayload);
                                                        if (!ok) return;
                                                        if (copySuggestionFeedbackRef.current) {
                                                            clearTimeout(copySuggestionFeedbackRef.current);
                                                        }
                                                        setCopiedSuggestionIndex(index);
                                                        copySuggestionFeedbackRef.current = setTimeout(() => {
                                                            setCopiedSuggestionIndex(null);
                                                            copySuggestionFeedbackRef.current = null;
                                                        }, 2000);
                                                    }}
                                                    title={copiedSuggestionIndex === index ? 'Đã copy' : 'Copy nhanh'}
                                                    className={`absolute top-2 right-2 p-1 rounded-lg transition-colors duration-200 ${
                                                        copiedSuggestionIndex === index
                                                            ? 'bg-emerald-100 text-emerald-700'
                                                            : 'text-slate-600 hover:bg-slate-200'
                                                    }`}
                                                >
                                                    {copiedSuggestionIndex === index ? (
                                                        <Check size={16} strokeWidth={2.5} />
                                                    ) : (
                                                        <Copy size={16} />
                                                    )}
                                                </button>
                                                <div style={{ marginBottom: '0.5rem', paddingRight: '1.75rem' }}>
                                                    {title ? (
                                                        <>
                                                            <div style={{ fontSize: '0.6875rem', fontWeight: 600, color: '#6b7280', textTransform: 'uppercase', letterSpacing: '0.04em', marginBottom: '0.25rem' }}>
                                                                Tên chương gợi ý
                                                            </div>
                                                            <div style={{ fontSize: '0.875rem', fontWeight: 600, color: '#111827' }}>{title}</div>
                                                        </>
                                                    ) : (
                                                        <div style={{ fontSize: '0.875rem', fontWeight: 600, color: '#111827' }}>{`Gợi ý ${index + 1}`}</div>
                                                    )}
                                                </div>
                                                {summary && (
                                                    <div style={{ fontSize: '0.8125rem', color: '#4b5563', marginBottom: '0.5rem' }}>
                                                        {summary}
                                                    </div>
                                                )}
                                                {direction && (
                                                    <div style={{ fontSize: '0.8125rem', color: '#6b7280', whiteSpace: 'pre-wrap' }}>
                                                        {direction}
                                                    </div>
                                                )}
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

            {/* Popup 1: Đồng sáng tác - Nhập ý tưởng */}
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
                                Bạn có thể chọn nhập nội dung định hướng hoặc để AI tự viết theo mạch truyện hiện tại.
                            </p>
                        </div>
                        <div style={{ padding: '1.25rem 1.5rem' }}>
                            <div style={{ marginBottom: '0.75rem', display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                                <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.875rem', color: '#111827' }}>
                                    <input
                                        type="radio"
                                        name="co_create_prompt_mode"
                                        checked={!useCoCreatePrompt}
                                        onChange={() => {
                                            setUseCoCreatePrompt(false);
                                            setCoCreateIdea('');
                                        }}
                                        disabled={coCreateLoading}
                                    />
                                    Không nhập định hướng (AI tự sinh theo mạch truyện)
                                </label>
                                <label style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', fontSize: '0.875rem', color: '#111827' }}>
                                    <input
                                        type="radio"
                                        name="co_create_prompt_mode"
                                        checked={useCoCreatePrompt}
                                        onChange={() => setUseCoCreatePrompt(true)}
                                        disabled={coCreateLoading}
                                    />
                                    Nhập định hướng tùy chỉnh
                                </label>
                            </div>
                            <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#374151', marginBottom: '0.5rem' }}>
                                NỘI DUNG ĐỊNH HƯỚNG CỦA BẠN
                            </label>
                            {useCoCreatePrompt ? (
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
                            ) : (
                                <div style={{ padding: '0.75rem', border: '1px dashed #cbd5e1', borderRadius: '8px', fontSize: '0.8125rem', color: '#64748b' }}>
                                    Bạn đang chọn chế độ không nhập định hướng. AI sẽ tự sinh nội dung theo ngữ cảnh hiện tại của truyện.
                                </div>
                            )}
                            <div style={{ marginTop: '0.75rem', padding: '10px 12px', borderRadius: '8px', backgroundColor: '#fff7ed', border: '1px solid #fed7aa', fontSize: '0.8125rem', color: '#9a3412' }}>
                                Khi nhập định hướng tùy chỉnh, tác giả phải chịu trách nhiệm với nội dung định hướng đã nhập và nội dung AI sinh ra theo định hướng đó.
                            </div>
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
                                disabled={coCreateLoading}
                                style={{
                                    padding: '0.5rem 1.25rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    color: '#ffffff',
                                    backgroundColor: coCreateLoading ? '#9ca3af' : '#13ec5b',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: coCreateLoading ? 'not-allowed' : 'pointer',
                                }}
                            >
                                {coCreateLoading ? 'Đang tạo...' : 'Tạo nội dung'}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Popup 2: Kết quả đồng sáng tác - Nội dung AI + feedback + nút Đồng ý sử dụng */}
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
                            <h3 style={{ margin: 0, fontSize: '1.125rem', fontWeight: 600, color: '#111827' }}>
                                Nội dung AI đã tạo
                            </h3>
                        </div>
                        <div style={{ padding: '1.25rem 1.5rem', overflowY: 'auto', flex: 1 }}>
                            {(() => {
                                const ideaBlock = (coCreateResult.ideaContradictionFeedback ?? coCreateResult.IdeaContradictionFeedback ?? '').toString().trim();
                                const hasContent = ((coCreateResult.finalContent ?? coCreateResult.FinalContent ?? '').toString().trim().length > 0)
                                    || (((coCreateResult.outline ?? coCreateResult.Outline) || '').toString().trim().length > 0);
                                const hardStop = ideaBlock.length > 0 && !hasContent;
                                return hardStop;
                            })() ? (
                                <div style={{ padding: '1rem', backgroundColor: '#fef2f2', border: '1px solid #fecaca', borderRadius: '8px', color: '#991b1b' }}>
                                    {coCreateResult.ideaContradictionFeedback ?? coCreateResult.IdeaContradictionFeedback}
                                </div>
                            ) : (
                                <>
                                    {((coCreateResult.ideaConflictWarning ?? coCreateResult.IdeaConflictWarning ?? coCreateResult.ideaContradictionFeedback ?? coCreateResult.IdeaContradictionFeedback) || '').toString().trim() ? (
                                        <div style={{ marginBottom: '1rem', padding: '0.75rem', backgroundColor: '#fffbeb', border: '1px solid #fde68a', borderRadius: '8px', color: '#92400e', fontSize: '0.875rem', lineHeight: 1.5 }}>
                                            {coCreateResult.ideaConflictWarning ?? coCreateResult.IdeaConflictWarning ?? coCreateResult.ideaContradictionFeedback ?? coCreateResult.IdeaContradictionFeedback}
                                        </div>
                                    ) : null}
                                    {coCreateContextWarning ? (
                                        <div style={{ marginBottom: '1rem', padding: '0.75rem', backgroundColor: '#fffbeb', border: '1px solid #fde68a', borderRadius: '8px', color: '#92400e', fontSize: '0.875rem', lineHeight: 1.5 }}>
                                            {coCreateContextWarning}
                                        </div>
                                    ) : null}
                                    {((coCreateResult.outline ?? coCreateResult.Outline) || '').trim() && (
                                        <div style={{ marginBottom: '1rem' }}>
                                            <div style={{ fontSize: '0.75rem', fontWeight: 600, color: '#6b7280', marginBottom: '0.25rem' }}>Dàn ý</div>
                                            <div style={{ fontSize: '0.875rem', color: '#374151', whiteSpace: 'pre-wrap' }}>
                                                {formatOutlineForDisplay(coCreateResult.outline ?? coCreateResult.Outline)}
                                            </div>
                                            {(() => {
                                                const chars = coCreateResult.charactersInvolved ?? coCreateResult.CharactersInvolved ?? [];
                                                if (!Array.isArray(chars) || chars.length === 0) return null;
                                                return (
                                                    <div style={{ marginTop: '0.75rem' }}>
                                                        <div style={{ fontSize: '0.75rem', fontWeight: 600, color: '#6b7280', marginBottom: '0.25rem' }}>Nhân vật tham gia</div>
                                                        <div style={{ fontSize: '0.875rem', color: '#374151' }}>{chars.join(', ')}</div>
                                                    </div>
                                                );
                                            })()}
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
                                    {((coCreateResult.suggestedChapterTitle ?? coCreateResult.SuggestedChapterTitle) || '').toString().trim() ? (
                                        <div style={{ marginBottom: '1rem' }}>
                                            <div style={{ fontSize: '0.75rem', fontWeight: 600, color: '#6b7280', marginBottom: '0.25rem' }}>Tên chương gợi ý</div>
                                            <div
                                                style={{
                                                    fontSize: '0.875rem',
                                                    fontWeight: 600,
                                                    color: '#111827',
                                                    padding: '0.75rem',
                                                    border: '1px solid #bbf7d0',
                                                    borderRadius: '8px',
                                                    backgroundColor: '#f0fdf4',
                                                }}
                                            >
                                                {(coCreateResult.suggestedChapterTitle ?? coCreateResult.SuggestedChapterTitle ?? '').toString().trim()}
                                            </div>
                                            <p style={{ margin: '0.5rem 0 0', fontSize: '0.75rem', color: '#6b7280' }}>
                                                Khi bạn đồng ý sử dụng, tên này sẽ được điền vào ô Tên chương.
                                            </p>
                                        </div>
                                    ) : null}
                                    <div>
                                        <div style={{ fontSize: '0.75rem', fontWeight: 600, color: '#6b7280', marginBottom: '0.25rem' }}>Nội dung chương</div>
                                        {(() => {
                                            const raw = (coCreateResult.finalContent ?? coCreateResult.FinalContent ?? '').toString();
                                            const normalized = mergeContentRemoveScenes(raw);
                                            const displayContent = normalized || raw.trim();
                                            return (
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
                                                    {displayContent || 'AI chưa trả về nội dung chương. Bạn hãy thử lại với định hướng cụ thể hơn.'}
                                                </div>
                                            );
                                        })()}
                                    </div>
                                </>
                            )}
                        </div>
                        <div style={{ padding: '1rem 1.5rem', borderTop: '1px solid #e5e7eb', display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                            {(() => {
                                const ideaBlock = (coCreateResult.ideaContradictionFeedback ?? coCreateResult.IdeaContradictionFeedback ?? '').toString().trim();
                                const hasContent = ((coCreateResult.finalContent ?? coCreateResult.FinalContent ?? '').toString().trim().length > 0)
                                    || (((coCreateResult.outline ?? coCreateResult.Outline) || '').toString().trim().length > 0);
                                return ideaBlock.length > 0 && !hasContent;
                            })() ? (
                                <button
                                    type="button"
                                    onClick={() => { setShowCoCreateResultPopup(false); setCoCreateResult(null); setCoCreateContextWarning(null); }}
                                    style={{
                                        padding: '0.5rem 1.25rem',
                                        fontSize: '0.875rem',
                                        fontWeight: 600,
                                        color: '#ffffff',
                                        backgroundColor: '#13ec5b',
                                        border: 'none',
                                        borderRadius: '8px',
                                        cursor: 'pointer',
                                    }}
                                >
                                    Đóng
                                </button>
                            ) : (
                                <>
                                    <button
                                        type="button"
                                        onClick={() => { setShowCoCreateResultPopup(false); setCoCreateResult(null); setCoCreateContextWarning(null); }}
                                        style={{
                                            padding: '0.5rem 1rem',
                                            fontSize: '0.875rem',
                                            fontWeight: 500,
                                            color: '#6b7280',
                                            backgroundColor: '#f3f4f6',
                                            border: 'none',
                                            borderRadius: '8px',
                                            cursor: 'pointer',
                                        }}
                                    >
                                        Đóng
                                    </button>
                                    <button
                                        type="button"
                                        onClick={handleCoCreateApply}
                                        style={{
                                            padding: '0.5rem 1.25rem',
                                            fontSize: '0.875rem',
                                            fontWeight: 600,
                                            color: '#ffffff',
                                            backgroundColor: '#13ec5b',
                                            border: 'none',
                                            borderRadius: '8px',
                                            cursor: 'pointer',
                                        }}
                                    >
                                        ĐỒNG Ý SỬ DỤNG NỘI DUNG NÀY
                                    </button>
                                </>
                            )}
                        </div>
                    </div>
                </div>
            )}

            <div style={{ minHeight: '100vh', backgroundColor: '#f5f5f5' }}>
                {/* Header */}
                <div style={{
                    backgroundColor: '#ffffff',
                    borderBottom: '1px solid #e0e0e0',
                    position: 'sticky',
                    top: 0,
                    zIndex: 100
                }}>
                    <div style={{ maxWidth: '1200px', margin: '0 auto', padding: '1rem 2rem' }}>
                        <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between' }}>
                            {/* Left: Back button and title */}
                            <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                                <button
                                    onClick={onCancel}
                                    className="flex items-center gap-2 px-4 py-2 bg-slate-100 text-slate-900 text-sm font-semibold rounded-full hover:bg-slate-200 transition-all"
                                >
                                    <ArrowLeft style={{ width: '16px', height: '16px' }} />
                                    Quay lại
                                </button>
                                <div>
                                    <h2 style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#333333', margin: 0 }}>
                                        {readOnly ? 'Xem chi tiết chương' : isVersionMode ? (editingVersion ? 'Chỉnh sửa phiên bản' : 'Tạo phiên bản chương') : (isCreateMode ? 'Thêm chương mới' : (isEditingChapterMode ? 'Chỉnh sửa chương' : 'Thêm chương mới'))}
                                    </h2>
                                    <p style={{ fontSize: '0.875rem', color: '#6b7280', margin: '0.25rem 0 0 0' }}>
                                        {`Truyện: ${story?.title ?? ''}`}
                                    </p>
                                    {isVersionMode && sourceChapterForVersion && (
                                        <p style={{ fontSize: '0.8125rem', color: '#6366f1', margin: '0.375rem 0 0 0', fontWeight: 600 }}>
                                            {editingVersion
                                                ? `Đang chỉnh sửa phiên bản #${editingVersion.versionNumber ?? 1} — Chương ${sourceChapterForVersion.number}: ${sourceChapterForVersion.title || '(Không có tiêu đề)'}`
                                                : `Đang tạo phiên bản cho: Chương ${sourceChapterForVersion.number} — ${sourceChapterForVersion.title || '(Không có tiêu đề)'}`}
                                        </p>
                                    )}
                                </div>
                            </div>

                            {/* Right: Save buttons — ẩn khi readOnly (xem chi tiết) */}
                            {!readOnly && (
                                <div style={{ display: 'flex', gap: '0.75rem' }}>
                                    <button
                                        onClick={handleManualBannedWordsCheck}
                                        disabled={isSaving || manualSpellingCheckLoading}
                                        className="flex items-center gap-2 px-6 py-2.5 bg-amber-50 text-amber-700 text-sm font-bold rounded-full hover:bg-amber-100 transition-all disabled:opacity-60 disabled:cursor-not-allowed"
                                    >
                                        <Sparkles style={{ width: '16px', height: '16px' }} />
                                        {manualSpellingCheckLoading ? 'Đang kiểm tra...' : 'Hỗ trợ kiểm tra chính tả'}
                                    </button>
                                    <button
                                        onClick={() => handleSave('draft')}
                                        disabled={isSaving || aiCompareModal.open}
                                        className="flex items-center gap-2 px-6 py-2.5 bg-primary/10 text-primary text-sm font-bold rounded-full hover:bg-primary/20 transition-all disabled:opacity-60 disabled:cursor-not-allowed"
                                    >
                                        <Save style={{ width: '16px', height: '16px' }} />
                                        {isSaving ? 'Đang lưu...' : 'Lưu nháp'}
                                    </button>
                                    <button
                                        onClick={() => handleSave('published')}
                                        disabled={
                                            isSaving ||
                                            aiCompareModal.open ||
                                            (isVersionMode && !canSubmitVersion) ||
                                            (!isVersionMode && (!normalPublishEligibility.loaded || !canSubmitNormalChapterPublish))
                                        }
                                        title={isVersionMode ? versionPublishTooltip : normalChapterPublishTooltip}
                                        className="flex items-center gap-2 px-6 py-2.5 bg-primary text-white text-sm font-bold rounded-full hover:bg-primary/90 transition-all disabled:opacity-60 disabled:cursor-not-allowed"
                                    >
                                        <Save style={{ width: '16px', height: '16px' }} />
                                        {isSaving ? 'Đang lưu...' : 'Xuất bản'}
                                    </button>
                                </div>
                            )}
                        </div>
                    </div>
                </div>

                {/* Content */}
                <div style={{ maxWidth: '1200px', margin: '0 auto', padding: '2rem' }}>
                    <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '2rem', border: '1px solid #e0e0e0' }}>
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
                            {/* Khi tạo/sửa version: không hiển thị Số version, Chương gốc, Chế độ sáng tác. Chỉ hiển thị khi tạo/sửa chương thường. */}
                            {!isVersionMode && (
                                <div style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: '1rem' }}>
                                        <div>
                                            <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                                Chương số <span style={{ color: '#ef4444' }}>*</span>
                                            </label>
                                        <input
                                            type="number"
                                            value={chapterData.number}
                                            readOnly
                                            disabled
                                            min="1"
                                            style={{
                                                width: '100%',
                                                padding: '0.75rem',
                                                backgroundColor: '#f1f5f9',
                                                border: `1px solid ${chapterNumberError ? '#ef4444' : '#e5e7eb'}`,
                                                borderRadius: '8px',
                                                fontSize: '0.875rem',
                                                outline: 'none',
                                                cursor: 'default'
                                            }}
                                        />
                                        {chapterNumberError && (
                                            <div style={{ fontSize: '0.75rem', color: '#ef4444', marginTop: '0.25rem' }}>
                                                {chapterNumberError}
                                            </div>
                                        )}
                                    </div>
                                    <div>
                                        <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                            Tên chương <span style={{ color: '#ef4444' }}>*</span>
                                        </label>
                                        <input
                                            type="text"
                                            value={chapterData.title}
                                            readOnly={readOnly}
                                            disabled={readOnly}
                                            onChange={(e) => !readOnly && setChapterData({ ...chapterData, title: e.target.value })}
                                            placeholder="Nhập tên chương"
                                            style={{
                                                width: '100%',
                                                padding: '0.75rem',
                                                backgroundColor: readOnly ? '#f1f5f9' : '#f9fafb',
                                                border: '1px solid #e5e7eb',
                                                borderRadius: '8px',
                                                fontSize: '0.875rem',
                                                outline: 'none',
                                                cursor: readOnly ? 'default' : undefined
                                            }}
                                        />
                                    </div>
                                </div>
                            )}

                            {/* Khi tạo/sửa version: hiển thị Chương số (read-only) + Tiêu đề chương gốc, rồi Số phiên bản + Tiêu đề phiên bản */}
                            {isVersionMode && (
                                <>
                                    <div style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: '1rem' }}>
                                        <div>
                                            <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                                Chương số
                                            </label>
                                            <input
                                                type="text"
                                                readOnly
                                                disabled
                                                value={sourceChapterForVersion?.number ?? (chapterData.number ?? 1)}
                                                style={{
                                                    width: '100%',
                                                    padding: '0.75rem',
                                                    backgroundColor: '#f1f5f9',
                                                    border: '1px solid #e5e7eb',
                                                    borderRadius: '8px',
                                                    fontSize: '0.875rem',
                                                    color: '#475569',
                                                    cursor: 'default',
                                                }}
                                            />
                                        </div>
                                        <div>
                                            <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                                Tiêu đề chương gốc
                                            </label>
                                            <input
                                                type="text"
                                                readOnly
                                                disabled
                                                value={sourceChapterForVersion?.title ?? ''}
                                                style={{
                                                    width: '100%',
                                                    padding: '0.75rem',
                                                    backgroundColor: '#f1f5f9',
                                                    border: '1px solid #e5e7eb',
                                                    borderRadius: '8px',
                                                    fontSize: '0.875rem',
                                                    color: '#475569',
                                                    cursor: 'default',
                                                }}
                                            />
                                        </div>
                                    </div>
                                    <div style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: '1rem' }}>
                                        <div>
                                            <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                                Số phiên bản <span style={{ color: '#ef4444' }}>*</span>
                                            </label>
                                            <input
                                                type="number"
                                                min="1"
                                                value={chapterData.versionNumber ?? 1}
                                                readOnly={readOnly || (isVersionMode && !editingVersion)}
                                                disabled={readOnly || (isVersionMode && !editingVersion)}
                                                onChange={(e) => {
                                                    if (readOnly || (isVersionMode && !editingVersion)) return;
                                                    const v = e.target.value === '' ? 1 : Math.max(1, Number(e.target.value) || 1);
                                                    setChapterData((prev) => ({ ...prev, versionNumber: v }));
                                                    setVersionNumberError('');
                                                }}
                                                title={isVersionMode && !editingVersion ? 'Số phiên bản tự tăng theo danh sách phiên bản hiện có' : undefined}
                                                style={{
                                                    width: '100%',
                                                    padding: '0.75rem',
                                                    backgroundColor: readOnly || (isVersionMode && !editingVersion) ? '#f1f5f9' : '#f9fafb',
                                                    border: versionNumberError ? '1px solid #ef4444' : '1px solid #e5e7eb',
                                                    borderRadius: '8px',
                                                    fontSize: '0.875rem',
                                                    outline: 'none',
                                                    cursor: readOnly || (isVersionMode && !editingVersion) ? 'default' : undefined,
                                                }}
                                            />
                                            {versionNumberError && (
                                                <p style={{ fontSize: '0.75rem', color: '#ef4444', margin: '0.25rem 0 0 0' }}>{versionNumberError}</p>
                                            )}
                                        </div>
                                        <div>
                                            <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                                Tiêu đề phiên bản <span style={{ color: '#ef4444' }}>*</span>
                                            </label>
                                            <input
                                                type="text"
                                                value={chapterData.title}
                                                readOnly={readOnly}
                                                disabled={readOnly}
                                                onChange={(e) => !readOnly && setChapterData({ ...chapterData, title: e.target.value })}
                                                placeholder="Nhập tiêu đề phiên bản (vd: Bản chỉnh sửa lỗi chính tả)"
                                                style={{
                                                    width: '100%',
                                                    padding: '0.75rem',
                                                    backgroundColor: readOnly ? '#f1f5f9' : '#f9fafb',
                                                    border: '1px solid #e5e7eb',
                                                    borderRadius: '8px',
                                                    fontSize: '0.875rem',
                                                    outline: 'none',
                                                    cursor: readOnly ? 'default' : undefined,
                                                }}
                                            />
                                        </div>
                                    </div>
                                </>
                            )}

                            {/* Chế độ sáng tác — không hiển thị khi tạo/sửa version. Khi readOnly hiển thị dạng chỉ đọc. */}
                            {!isVersionMode && (
                                <div>
                                    <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.75rem' }}>
                                        Chế độ sáng tác {!readOnly && <span style={{ color: '#ef4444' }}>*</span>}
                                    </label>

                                    <div style={{ display: 'grid', gridTemplateColumns: chapterData.accessType === 'paid' ? '1fr 1fr 200px' : '1fr 1fr', gap: '1rem' }}>
                                        {/* Public Option */}
                                        {readOnly ? (
                                            <div
                                                style={{
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    gap: '0.75rem',
                                                    padding: '1rem',
                                                    border: `2px solid ${chapterData.accessType === 'public' ? '#13ec5b' : '#e2e8f0'}`,
                                                    borderRadius: '12px',
                                                    backgroundColor: chapterData.accessType === 'public' ? 'rgba(19, 236, 91, 0.08)' : '#f8fafc',
                                                    opacity: chapterData.accessType === 'public' ? 1 : 0.7
                                                }}
                                            >
                                                <div style={{ width: '40px', height: '40px', borderRadius: '50%', backgroundColor: chapterData.accessType === 'public' ? '#13ec5b' : '#e2e8f0', color: chapterData.accessType === 'public' ? '#fff' : '#94a3b8', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                                    <Unlock style={{ width: '20px', height: '20px' }} />
                                                </div>
                                                <div style={{ flex: 1 }}>
                                                    <div style={{ fontSize: '0.875rem', fontWeight: 'bold', color: chapterData.accessType === 'public' ? '#13ec5b' : '#64748b' }}>Miễn phí (Public)</div>
                                                    <div style={{ fontSize: '0.75rem', color: '#64748b' }}>Người đọc có thể đọc miễn phí</div>
                                                </div>
                                            </div>
                                        ) : (
                                            <button
                                                type="button"
                                                onClick={() => setChapterData({ ...chapterData, accessType: 'public', price: 0 })}
                                                className={`flex items-center gap-3 p-4 border-2 rounded-xl transition-all ${chapterData.accessType === 'public' ? 'border-primary bg-primary/5' : 'border-slate-200 hover:border-slate-300'}`}
                                            >
                                                <div className={`flex items-center justify-center w-10 h-10 rounded-full ${chapterData.accessType === 'public' ? 'bg-primary text-white' : 'bg-slate-100 text-slate-600'}`}>
                                                    <Unlock style={{ width: '20px', height: '20px' }} />
                                                </div>
                                                <div style={{ textAlign: 'left', flex: 1 }}>
                                                    <div style={{ fontSize: '0.875rem', fontWeight: 'bold', color: chapterData.accessType === 'public' ? '#13ec5b' : '#333333' }}>Miễn phí (Public)</div>
                                                    <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>Người đọc có thể đọc miễn phí</div>
                                                </div>
                                                {chapterData.accessType === 'public' && (
                                                    <div style={{ width: '20px', height: '20px', borderRadius: '50%', backgroundColor: '#13ec5b', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                                        <div style={{ width: '8px', height: '8px', borderRadius: '50%', backgroundColor: '#ffffff' }} />
                                                    </div>
                                                )}
                                            </button>
                                        )}

                                        {/* Paid Option */}
                                        {readOnly ? (
                                            <div
                                                style={{
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    gap: '0.75rem',
                                                    padding: '1rem',
                                                    border: `2px solid ${chapterData.accessType === 'paid' ? '#f59e0b' : '#e2e8f0'}`,
                                                    borderRadius: '12px',
                                                    backgroundColor: chapterData.accessType === 'paid' ? '#fffbeb' : '#f8fafc',
                                                    opacity: chapterData.accessType === 'paid' ? 1 : 0.7
                                                }}
                                            >
                                                <div style={{ width: '40px', height: '40px', borderRadius: '50%', backgroundColor: chapterData.accessType === 'paid' ? '#f59e0b' : '#e2e8f0', color: chapterData.accessType === 'paid' ? '#fff' : '#94a3b8', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                                    <Lock style={{ width: '20px', height: '20px' }} />
                                                </div>
                                                <div style={{ flex: 1 }}>
                                                    <div style={{ fontSize: '0.875rem', fontWeight: 'bold', color: chapterData.accessType === 'paid' ? '#f59e0b' : '#64748b' }}>Trả phí (Paid)</div>
                                                    <div style={{ fontSize: '0.75rem', color: '#64748b' }}>Yêu cầu người đọc trả phí</div>
                                                </div>
                                            </div>
                                        ) : (
                                            <button
                                                type="button"
                                                onClick={() => {
                                                    if (!canEnablePaidMode) {
                                                        showToast('Truyện cần tối thiểu 500 lượt xem mới được bật chế độ trả phí cho chương.', 'error');
                                                        return;
                                                    }
                                                    setChapterData({ ...chapterData, accessType: 'paid' });
                                                }}
                                                disabled={!canEnablePaidMode && chapterData.accessType !== 'paid'}
                                                title={!canEnablePaidMode && chapterData.accessType !== 'paid' ? 'Truyện cần tối thiểu 500 lượt xem để bật trả phí.' : undefined}
                                                className={`flex items-center gap-3 p-4 border-2 rounded-xl transition-all disabled:opacity-60 disabled:cursor-not-allowed ${chapterData.accessType === 'paid' ? 'border-amber-500 bg-amber-50' : 'border-slate-200 hover:border-slate-300'}`}
                                            >
                                                <div className={`flex items-center justify-center w-10 h-10 rounded-full ${chapterData.accessType === 'paid' ? 'bg-amber-500 text-white' : 'bg-slate-100 text-slate-600'}`}>
                                                    <Lock style={{ width: '20px', height: '20px' }} />
                                                </div>
                                                <div style={{ textAlign: 'left', flex: 1 }}>
                                                    <div style={{ fontSize: '0.875rem', fontWeight: 'bold', color: chapterData.accessType === 'paid' ? '#f59e0b' : '#333333' }}>Trả phí (Paid)</div>
                                                    <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>Yêu cầu người đọc trả phí</div>
                                                </div>
                                                {chapterData.accessType === 'paid' && (
                                                    <div style={{ width: '20px', height: '20px', borderRadius: '50%', backgroundColor: '#f59e0b', display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
                                                        <div style={{ width: '8px', height: '8px', borderRadius: '50%', backgroundColor: '#ffffff' }} />
                                                    </div>
                                                )}
                                            </button>
                                        )}

                                        {/* Price — khi paid: input chỉnh sửa hoặc hiển thị readOnly */}
                                        {chapterData.accessType === 'paid' && (
                                            <div>
                                                <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                                    Giá {!readOnly && <span style={{ color: '#ef4444' }}>*</span>}
                                                </label>
                                                <div style={{ position: 'relative' }}>
                                                    {readOnly ? (
                                                        <div style={{ padding: '0.75rem', backgroundColor: '#fffbeb', border: '1px solid #fbbf24', borderRadius: '8px', fontSize: '0.875rem', fontWeight: 'bold', color: '#92400e' }}>
                                                            {chapterData.price ?? 0} xu
                                                        </div>
                                                    ) : (
                                                        <input
                                                            type="number"
                                                            value={chapterData.price}
                                                            onChange={(e) => setChapterData({ ...chapterData, price: Number(e.target.value) })}
                                                            min="1"
                                                            placeholder="0"
                                                            style={{
                                                                width: '100%',
                                                                padding: '0.75rem 0.75rem 0.75rem 2.5rem',
                                                                backgroundColor: '#fffbeb',
                                                                border: '1px solid #fbbf24',
                                                                borderRadius: '8px',
                                                                fontSize: '0.875rem',
                                                                fontWeight: 'bold',
                                                                color: '#92400e',
                                                                outline: 'none'
                                                            }}
                                                        />
                                                    )}
                                                    {!readOnly && <Coins style={{
                                                        position: 'absolute',
                                                        left: '0.75rem',
                                                        top: '50%',
                                                        transform: 'translateY(-50%)',
                                                        width: '16px',
                                                        height: '16px',
                                                        color: '#f59e0b'
                                                    }} />}
                                                </div>
                                                <p style={{ fontSize: '0.625rem', color: '#92400e', marginTop: '0.25rem' }}>
                                                    Đơn vị: Xu
                                                </p>
                                            </div>
                                        )}
                                    </div>

                                    {/* Info Box */}
                                    {chapterData.accessType === 'paid' && (
                                        <div style={{
                                            marginTop: '1rem',
                                            padding: '0.75rem 1rem',
                                            backgroundColor: '#fffbeb',
                                            border: '1px solid #fcd34d',
                                            borderRadius: '8px',
                                            fontSize: '0.75rem',
                                            color: '#92400e',
                                            display: 'flex',
                                            alignItems: 'flex-start',
                                            gap: '0.5rem'
                                        }}>
                                            <span style={{ fontSize: '1rem' }}>💰</span>
                                            <div>
                                                <strong>Lưu ý về chương trả phí:</strong>
                                                <ul style={{ margin: '0.25rem 0 0 1rem', paddingLeft: 0 }}>
                                                    <li>Người đọc cần có đủ xu để mở khóa chương</li>
                                                    <li>Sau khi mua, chương sẽ được lưu vĩnh viễn trong tài khoản</li>
                                                    <li>Bạn sẽ nhận 70% số xu, nền tảng giữ lại 30%</li>
                                                </ul>
                                            </div>
                                        </div>
                                    )}
                                    {!canEnablePaidMode && chapterData.accessType !== 'paid' && (
                                        <div style={{
                                            marginTop: '1rem',
                                            padding: '0.75rem 1rem',
                                            backgroundColor: '#fff7ed',
                                            border: '1px solid #fdba74',
                                            borderRadius: '8px',
                                            fontSize: '0.75rem',
                                            color: '#9a3412',
                                        }}>
                                            Truyện hiện có {storyTotalViews.toLocaleString('vi-VN')} lượt xem. Cần tối thiểu 500 lượt xem để bật chế độ trả phí cho chương.
                                        </div>
                                    )}
                                </div>
                            )}

                            {/* Toolbar — khi readOnly hiển thị dạng chỉ đọc (disabled) */}
                            <div style={{
                                display: 'flex',
                                justifyContent: 'space-between',
                                alignItems: 'center',
                                padding: '0.75rem 1rem',
                                backgroundColor: '#f9fafb',
                                borderRadius: '8px',
                                border: '1px solid #e5e7eb',
                                opacity: readOnly ? 0.85 : 1
                            }}>
                                <div style={{ display: 'flex', gap: '0.5rem' }}>
                                    {readOnly ? (
                                        <>
                                            <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.5rem', padding: '0.5rem 1rem', backgroundColor: '#e2e8f0', color: '#64748b', fontSize: '0.875rem', fontWeight: 600, borderRadius: '9999px' }}>
                                                <Sparkles style={{ width: '14px', height: '14px' }} />
                                                AI gợi ý ý tưởng{aiUsageLimit ? ` (${aiUsageLimit.suggestNextChapter?.remaining ?? 0}/${aiUsageLimit.suggestNextChapter?.limitPerDay ?? 0})` : ''}
                                            </span>
                                            <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.5rem', padding: '0.5rem 1rem', backgroundColor: '#e2e8f0', color: '#64748b', fontSize: '0.875rem', fontWeight: 600, borderRadius: '9999px' }}>
                                                <Sparkles style={{ width: '14px', height: '14px' }} />
                                                AI gợi ý chương{aiUsageLimit
                                                    ? (aiUsageLimit.coCreateAvailable
                                                        ? ` (${aiUsageLimit.coCreate?.remaining ?? 0}/${aiUsageLimit.coCreate?.limitPerDay ?? 0})`
                                                        : ' (—/—)')
                                                    : ''}
                                            </span>
                                        </>
                                    ) : (
                                        <>
                                            <button type="button" onClick={() => handleAISuggestion('paragraph')} className="flex items-center gap-2 px-4 py-2 bg-primary/10 text-primary text-sm font-bold rounded-full hover:bg-primary/20 transition-all">
                                                <Sparkles style={{ width: '14px', height: '14px' }} />
                                                AI gợi ý ý tưởng{aiUsageLimit ? ` (${aiUsageLimit.suggestNextChapter?.remaining ?? 0}/${aiUsageLimit.suggestNextChapter?.limitPerDay ?? 0})` : ''}
                                            </button>
                                            <button
                                                type="button"
                                                onClick={openCachedSuggestions}
                                                disabled={!Array.isArray(suggestionsCache) || suggestionsCache.length === 0}
                                                className={`flex items-center gap-2 px-4 py-2 text-sm font-bold rounded-full transition-all disabled:opacity-60 disabled:cursor-not-allowed ${Array.isArray(suggestionsCache) && suggestionsCache.length > 0
                                                    ? 'bg-amber-100 text-amber-800 ring-2 ring-amber-300 hover:bg-amber-200 shadow-sm'
                                                    : 'bg-slate-100 text-slate-700 hover:bg-slate-200'
                                                    }`}
                                            >
                                                <Sparkles style={{ width: '14px', height: '14px' }} />
                                                Xem lại gợi ý gần nhất
                                            </button>
                                            <button type="button" onClick={() => handleAISuggestion('chapter')} className="flex items-center gap-2 px-4 py-2 bg-primary/10 text-primary text-sm font-bold rounded-full hover:bg-primary/20 transition-all">
                                                <Sparkles style={{ width: '14px', height: '14px' }} />
                                                AI gợi ý chương{aiUsageLimit
                                                    ? (aiUsageLimit.coCreateAvailable
                                                        ? ` (${aiUsageLimit.coCreate?.remaining ?? 0}/${aiUsageLimit.coCreate?.limitPerDay ?? 0})`
                                                        : ' (—/—)')
                                                    : ''}
                                            </button>
                                        </>
                                    )}
                                </div>
                                {readOnly ? (
                                    <span
                                        style={{
                                            display: 'inline-flex',
                                            alignItems: 'center',
                                            gap: '0.5rem',
                                            padding: '0.5rem 1rem',
                                            backgroundColor: '#e2e8f0',
                                            color: '#64748b',
                                            fontSize: '0.875rem',
                                            fontWeight: 600,
                                            borderRadius: '9999px'
                                        }}
                                    >
                                        <Settings style={{ width: '14px', height: '14px' }} />
                                        Tùy chỉnh hiển thị
                                    </span>
                                ) : (
                                    <button
                                        type="button"
                                        onClick={() => setShowSettings(!showSettings)}
                                        className={`flex items-center gap-2 px-4 py-2 text-sm font-bold rounded-full transition-all ${showSettings ? 'bg-primary text-white' : 'bg-slate-100 text-slate-700 hover:bg-slate-200'}`}
                                    >
                                        <Settings style={{ width: '14px', height: '14px' }} />
                                        Tùy chỉnh hiển thị
                                    </button>
                                )}
                            </div>
                            {readOnly && (
                                <div style={{ padding: '0.75rem 1rem', backgroundColor: '#f8fafc', borderRadius: '8px', border: '1px solid #e2e8f0', fontSize: '0.8125rem', color: '#64748b' }}>
                                    Cỡ chữ: {editorSettings.fontSize}px · Font: {fontFamilies.find(f => f.value === editorSettings.fontFamily)?.name ?? editorSettings.fontFamily} · Nền: {backgroundColors.find(b => b.value === editorSettings.backgroundColor)?.name ?? editorSettings.backgroundColor}
                                </div>
                            )}

                            {/* Settings Panel — ẩn khi readOnly (đã hiển thị dạng text phía trên) */}
                            {showSettings && !readOnly && (
                                <div style={{
                                    padding: '1.5rem',
                                    backgroundColor: '#f9fafb',
                                    borderRadius: '8px',
                                    border: '1px solid #e5e7eb'
                                }}>
                                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
                                        <h4 style={{ fontSize: '0.875rem', fontWeight: 600, color: '#333333', margin: 0 }}>
                                            Cài đặt hiển thị
                                        </h4>
                                        <button
                                            type="button"
                                            onClick={() => setShowSettings(false)}
                                            className="p-1 hover:bg-slate-200 rounded-full transition-colors"
                                        >
                                            <X style={{ width: '16px', height: '16px', color: '#6b7280' }} />
                                        </button>
                                    </div>

                                    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1.5rem' }}>
                                        {/* Font Size */}
                                        <div>
                                            <label style={{ display: 'block', fontSize: '0.75rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                                Cỡ chữ: {editorSettings.fontSize}px
                                            </label>
                                            <input
                                                type="range"
                                                min="12"
                                                max="28"
                                                value={editorSettings.fontSize}
                                                onChange={(e) => setEditorSettings({ ...editorSettings, fontSize: Number(e.target.value) })}
                                                style={{ width: '100%', cursor: 'pointer', accentColor: '#13ec5b' }}
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
                                                    padding: '0.5rem',
                                                    backgroundColor: '#ffffff',
                                                    border: '1px solid #e5e7eb',
                                                    borderRadius: '8px',
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
                                            <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                                                {backgroundColors.map((bg) => (
                                                    <button
                                                        key={bg.value}
                                                        type="button"
                                                        onClick={() => setEditorSettings({ ...editorSettings, backgroundColor: bg.value })}
                                                        title={bg.name}
                                                        style={{
                                                            width: '40px',
                                                            height: '40px',
                                                            backgroundColor: bg.value,
                                                            border: editorSettings.backgroundColor === bg.value ? '3px solid #13ec5b' : '1px solid #e5e7eb',
                                                            borderRadius: '8px',
                                                            cursor: 'pointer',
                                                            transition: 'all 0.2s'
                                                        }}
                                                        onMouseEnter={(e) => {
                                                            e.currentTarget.style.transform = 'scale(1.1)';
                                                        }}
                                                        onMouseLeave={(e) => {
                                                            e.currentTarget.style.transform = 'scale(1)';
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
                                    Nội dung chương <span style={{ color: '#ef4444' }}>*</span>
                                </label>
                                <RichTextEditor
                                    value={chapterData.content || ''}
                                    readOnly={readOnly}
                                    onChange={(html) => !readOnly && setChapterData({ ...chapterData, content: html })}
                                    placeholder="Nhập nội dung chương của bạn... Bạn có thể bôi đen một đoạn rồi dùng toolbar để in đậm/in nghiêng/font."
                                    minHeight={520}
                                    backgroundColor={readOnly ? '#f1f5f9' : editorSettings.backgroundColor}
                                    borderRadius="8px"
                                    fontSize={editorSettings.fontSize}
                                    fontFamily={editorSettings.fontFamily}
                                />
                                <div style={{ display: 'flex', justifyContent: 'space-between', marginTop: '0.5rem' }}>
                                    <p style={{ fontSize: '0.75rem', color: countWords(chapterData.content) < 500 ? '#ef4444' : '#9ca3af', margin: 0 }}>
                                        Tối thiểu 500 từ
                                    </p>
                                    <p style={{ fontSize: '0.75rem', color: '#9ca3af', margin: 0 }}>
                                        {countWords(chapterData.content).toLocaleString()} từ
                                    </p>
                                </div>
                            </div>

                            {/* Additional Info */}
                            <div style={{
                                padding: '1rem',
                                backgroundColor: '#dbeafe',
                                border: '1px solid #93c5fd',
                                borderRadius: '8px',
                                fontSize: '0.875rem',
                                color: '#1e40af'
                            }}>
                                <strong>💡 Mẹo viết chương hay:</strong>
                                <ul style={{ margin: '0.5rem 0 0 1.5rem', paddingLeft: 0 }}>
                                    <li>Bắt đầu bằng một hook hấp dẫn để thu hút người đọc</li>
                                    <li>Sử dụng AI để gợi ý khi gặp khó khăn</li>
                                    <li>Chia nhỏ đoạn văn để dễ đọc hơn</li>
                                    <li>Kết thúc chương với một twist hoặc cliffhanger</li>
                                </ul>
                            </div>
                        </div>
                    </div>
                </div>
            </div>
            <Footer />
        </div>
    );
}
