import { useState, useEffect } from 'react';
import { Sparkles, Settings, X, Save, ArrowLeft, Lock, Unlock, Coins } from 'lucide-react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { useToast } from '../../components/author/story-editor/Toast';
import { indexRag, suggestNextChapter, coCreate, checkChapter, getAiUsageLimit } from '../../api/ai/aiApi';
import { getChapters, getChapterVersions } from '../../api/chapter/chapterApi';
import { refresh as refreshAuth } from '../../api/auth/authApi';

// Helper function to count words
const countWords = (text) => {
    if (!text || !text.trim()) return 0;
    return text.trim().split(/\s+/).filter(word => word.length > 0).length;
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
    const raw = outline.trim();
    const toParse = extractOutlineJson(raw);
    try {
        const parsed = JSON.parse(toParse);
        const scenes = parsed?.scenes ?? parsed?.Scenes;
        if (Array.isArray(scenes) && scenes.length > 0) {
            if (isExampleOutline(scenes)) return '';
            return scenes.map((s, i) => {
                const title = s?.title ?? s?.Title ?? '';
                const summary = s?.summary ?? s?.Summary ?? '';
                const characters = s?.characters ?? s?.Characters ?? '';
                const parts = [];
                if (title) parts.push(title);
                if (summary) parts.push(summary);
                if (Array.isArray(characters) && characters.length) parts.push(`Nhân vật: ${characters.join(', ')}`);
                else if (typeof characters === 'string' && characters.trim()) parts.push(`Nhân vật: ${characters}`);
                return parts.length ? `Bối cảnh ${i + 1}:\n${parts.join('\n')}` : `Bối cảnh ${i + 1}`;
            }).join('\n\n');
        }
    } catch {
        // Không phải JSON, xử lý plain text (chỉ phần không phải block hướng dẫn)
    }
    // Plain text: bỏ đoạn "Trả về JSON dàn ý" và block code mẫu, chỉ giữ nội dung thật
    let text = raw
        .replace(/\*\*Trả về JSON dàn ý\*\*[\s\S]*?^\{[\s\S]*?"scenes"\s*:[\s\S]*?\}\s*\}/im, '')
        .replace(/```(?:json)?[\s\S]*?```/g, '')
        .trim();
    if (text) return text.replace(/\bScene\s*(\d+)\b/gi, 'Bối cảnh $1');
    return raw.replace(/\bScene\s*(\d+)\b/gi, 'Bối cảnh $1');
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

export function ChapterEditorPage({ story, chapter, sourceChapterForVersion, editingVersion, readOnly = false, onSave, onCancel }) {
    const { showToast, ToastContainer } = useToast();
    const storyId = story?.id ?? story?.Id;
    const [chapterCheckModal, setChapterCheckModal] = useState({ open: false, loading: false, data: null, error: null });
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
    const [chapterNumberError, setChapterNumberError] = useState('');
    /** Danh sách version của chương (khi ở chế độ version) — dùng validate số version không trùng + trạng thái (pending_review) */
    const [existingVersionsForChapter, setExistingVersionsForChapter] = useState([]);
    const [versionNumberError, setVersionNumberError] = useState('');
    /** Điều kiện gửi xuất bản version: publishedOrderIndices, pendingOrderIndices, prevHasPendingVersion (chương trước có version chờ duyệt). */
    const [versionPublishEligibility, setVersionPublishEligibility] = useState({
        publishedOrderIndices: new Set(),
        pendingOrderIndices: new Set(),
        prevHasPendingVersion: false,
    });
    /** Đã load xong dữ liệu để tính canSubmitVersion (tránh nút Xuất bản bật sẵn rồi mới disable). */
    const [versionEligibilityLoaded, setVersionEligibilityLoaded] = useState(false);
    const [versionsForChapterLoaded, setVersionsForChapterLoaded] = useState(false);

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
    const [suggestError, setSuggestError] = useState(null);
    const [aiUsageLimit, setAiUsageLimit] = useState(null);

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
            // ignore (user có thể chưa đăng nhập / BE lỗi)
            setAiUsageLimit(null);
        }
    };

    // Popup đồng sáng tác (AI gợi ý chương): bước 1 = nhập ý tưởng, bước 2 = xem kết quả + đồng ý
    const [showCoCreateIdeaPopup, setShowCoCreateIdeaPopup] = useState(false);
    const [showCoCreateResultPopup, setShowCoCreateResultPopup] = useState(false);
    const [useCoCreatePrompt, setUseCoCreatePrompt] = useState(false);
    const [coCreateIdea, setCoCreateIdea] = useState('');
    const [coCreateLoading, setCoCreateLoading] = useState(false);
    const [coCreateResult, setCoCreateResult] = useState(null);

    const isNewChapter = !chapter;
    const isVersionMode = Boolean(sourceChapterForVersion);

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

    // Load điều kiện gửi xuất bản version (thứ tự 1,2,3... và không trùng phiên bản chờ duyệt) — dùng đúng API có status như ChapterListManager
    useEffect(() => {
        if (!isVersionMode || !storyId || !sourceChapterForVersion) {
            setVersionPublishEligibility({ publishedOrderIndices: new Set(), pendingOrderIndices: new Set(), prevHasPendingVersion: false });
            setVersionEligibilityLoaded(false);
            return;
        }
        setVersionEligibilityLoaded(false);
        const chapterNumber = Number(sourceChapterForVersion.number ?? 1);
        const prevOrderIndex = chapterNumber - 2; // 0-based index của chương trước
        const markLoaded = (publishedOrderIndices, pendingOrderIndices, prevHasPendingVersion) => {
            setVersionPublishEligibility({ publishedOrderIndices, pendingOrderIndices, prevHasPendingVersion });
            setVersionEligibilityLoaded(true);
        };
        // Gọi riêng PUBLISHED và PENDING_REVIEW giống ChapterListManager để đảm bảo đúng tập chương đã gửi/chờ duyệt
        Promise.all([
            getChapters({ storyId, status: 'PUBLISHED', pageSize: 500 }),
            getChapters({ storyId, status: 'PENDING_REVIEW', pageSize: 500 }),
            getChapters({ storyId, page: 1, pageSize: 500 }),
        ])
            .then(([publishedRes, pendingRes, allRes]) => {
                const publishedList = Array.isArray(publishedRes) ? publishedRes : (publishedRes?.items ?? publishedRes?.Items ?? []);
                const pendingList = Array.isArray(pendingRes) ? pendingRes : (pendingRes?.items ?? pendingRes?.Items ?? []);
                const publishedOrderIndices = new Set(
                    publishedList.map((c) => Number(c.orderIndex ?? c.OrderIndex ?? 0))
                );
                const pendingOrderIndices = new Set(
                    pendingList.map((c) => Number(c.orderIndex ?? c.OrderIndex ?? 0))
                );
                const allItems = allRes?.items ?? allRes?.Items ?? [];
                const arr = Array.isArray(allItems) ? allItems : [];
                const prevChapter = arr.find((c) => Number(c.orderIndex ?? c.OrderIndex ?? 0) === prevOrderIndex);
                if (!prevChapter) {
                    markLoaded(publishedOrderIndices, pendingOrderIndices, false);
                    return;
                }
                const prevChapterId = prevChapter.id ?? prevChapter.Id;
                getChapterVersions(prevChapterId)
                    .then((verList) => {
                        const vArr = Array.isArray(verList) ? verList : [];
                        const prevHasPendingVersion = vArr.some((v) => ((v.status ?? v.Status ?? '').toString().toLowerCase() === 'pending_review'));
                        markLoaded(publishedOrderIndices, pendingOrderIndices, prevHasPendingVersion);
                    })
                    .catch(() => markLoaded(publishedOrderIndices, pendingOrderIndices, false));
            })
            .catch(() => {
                setVersionPublishEligibility({ publishedOrderIndices: new Set(), pendingOrderIndices: new Set(), prevHasPendingVersion: false });
                setVersionEligibilityLoaded(true);
            });
    }, [isVersionMode, storyId, sourceChapterForVersion]);

    // Load danh sách chương để tính số chương tiếp theo (thêm mới) và validate trùng (số 1-based)
    useEffect(() => {
        if (!storyId) {
            setExistingChapterNumbers(new Set());
            return;
        }
        getChapters({ storyId, page: 1, pageSize: 500 })
            .then((res) => {
                const items = res?.items ?? res?.Items ?? [];
                const arr = Array.isArray(items) ? items : [];
                const numbers = new Set(arr.map((c) => Number((c.orderIndex ?? c.OrderIndex ?? 0) + 1)));
                setExistingChapterNumbers(numbers);
                if (isNewChapter && !sourceChapterForVersion) {
                    const nextNumber = arr.length > 0
                        ? Math.max(...arr.map((c) => Number(c.orderIndex ?? c.OrderIndex ?? 0) + 1)) + 1
                        : 1;
                    setChapterData((prev) => ({ ...prev, number: nextNumber }));
                }
            })
            .catch(() => setExistingChapterNumbers(new Set()));
    }, [storyId, isNewChapter, sourceChapterForVersion]);

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
        setShowSuggestPopup(true);
        try {
            // Gọi index-rag nền (không chờ). Gợi ý chạy ngay; BE dùng RAG nếu đã index, không thì dùng Story Context.
            indexRag(storyId);
            const afterChapterId = chapter?.id ?? chapter?.Id ?? null;
            const data = await suggestNextChapter(storyId, afterChapterId);
            const list = data?.suggestions ?? data?.Suggestions ?? [];
            setSuggestions(Array.isArray(list) ? list : []);
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
        setCoCreateLoading(true);
        try {
            const data = await coCreate(storyId, idea || null);
            setCoCreateResult(data);
            setShowCoCreateIdeaPopup(false);
            setShowCoCreateResultPopup(true);
        } catch (err) {
            const status = err?.response?.status;
            const msg = err?.response?.data?.message ?? err?.message ?? 'Lỗi khi đồng sáng tác với AI.';
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
        if (content) {
            setChapterData(prev => ({ ...prev, content, isAiClean: true }));
            showToast('Đã áp dụng nội dung. Bạn có thể chỉnh sửa và nhấn Lưu / Xuất bản.', 'success');
        } else {
            showToast('AI chưa trả về nội dung chương. Vui lòng thử lại với định hướng chi tiết hơn.', 'error');
            return;
        }
        setShowCoCreateResultPopup(false);
        setCoCreateResult(null);
    };

    const currentChapterNumber = chapter ? Number(chapter.number ?? chapter.chapterNumber ?? (chapter.orderIndex ?? chapter.OrderIndex ?? 0) + 1) : null;

    /** Điều kiện gửi xuất bản version — giống ChapterListManager: thứ tự 1,2,3...; chương gốc không chờ duyệt; chỉ một phiên bản chờ duyệt. */
    const chapterNumberForVersion = isVersionMode ? Number(sourceChapterForVersion?.number ?? 1) : 0;
    const prevOrderIndexVersion = chapterNumberForVersion - 2;
    const canSubmitForPublishVersion =
        chapterNumberForVersion === 1 ||
        versionPublishEligibility.publishedOrderIndices.has(prevOrderIndexVersion) ||
        versionPublishEligibility.pendingOrderIndices.has(prevOrderIndexVersion) ||
        versionPublishEligibility.prevHasPendingVersion;
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
                        ? `Phải gửi chương ${chapterNumberForVersion - 1} trước khi gửi chương ${chapterNumberForVersion}.`
                        : chapterIsPendingReviewVersion
                            ? 'Chương gốc đang chờ duyệt, không thể gửi phiên bản.'
                            : hasOtherPendingVersion
                                ? 'Chỉ được gửi một phiên bản tại một thời điểm. Hãy hủy phiên bản đang chờ duyệt trước.'
                                : 'Gửi phiên bản lên để duyệt xuất bản';

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
        if (!chapterData.content.trim()) {
            showToast('Vui lòng nhập nội dung chương', 'error');
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
        const wordCount = countWords(chapterData.content);
        if (wordCount < 500) {
            showToast(`Nội dung chương cần ít nhất 500 từ (Hiện tại: ${wordCount} từ)`, 'error');
            return;
        }
        if (!isVersionMode && chapterData.accessType === 'paid' && (!chapterData.price || chapterData.price <= 0)) {
            showToast('Vui lòng nhập giá cho chương trả phí', 'error');
            return;
        }

        // AI check: chính tả + từ cấm/chính sách (BE: POST /api/ai/check-chapter)
        try {
            setChapterCheckModal({ open: false, loading: true, data: null, error: null });
            const res = await checkChapter({
                content: chapterData.content,
                storyId: storyId ?? null,
                chapterTitle: chapterData.title ?? null,
            });
            const spellingIssues = res?.spellingIssues ?? res?.SpellingIssues ?? [];
            const policyViolations = res?.policyViolations ?? res?.PolicyViolations ?? [];
            const passed = Boolean(res?.passed ?? res?.Passed) &&
                Array.isArray(spellingIssues) && spellingIssues.length === 0 &&
                Array.isArray(policyViolations) && policyViolations.length === 0 &&
                !(res?.hasInappropriateContent ?? res?.HasInappropriateContent);

            if (!passed) {
                setChapterCheckModal({
                    open: true,
                    loading: false,
                    error: null,
                    data: {
                        passed: Boolean(res?.passed ?? res?.Passed),
                        summary: res?.summary ?? res?.Summary ?? null,
                        hasInappropriateContent: Boolean(res?.hasInappropriateContent ?? res?.HasInappropriateContent),
                        spellingIssues,
                        policyViolations,
                    },
                });
                showToast('Nội dung có lỗi chính tả / từ cấm. Vui lòng sửa theo gợi ý trước khi lưu/xuất bản.', 'error');
                return;
            }
            setChapterCheckModal({ open: false, loading: false, data: null, error: null });
        } catch (err) {
            const status = err?.response?.status;
            // Nếu 401: token hết hạn → refresh 1 lần rồi kiểm tra lại
            let retrySucceeded = false;

            if (status === 401) {
                try {
                    const refreshRes = await refreshAuth();
                    if (refreshRes?.success) {
                        const res2 = await checkChapter({
                            content: chapterData.content,
                            storyId: storyId ?? null,
                            chapterTitle: chapterData.title ?? null,
                        });
                        const spellingIssues2 = res2?.spellingIssues ?? res2?.SpellingIssues ?? [];
                        const policyViolations2 = res2?.policyViolations ?? res2?.PolicyViolations ?? [];
                        const passed2 = Boolean(res2?.passed ?? res2?.Passed) &&
                            Array.isArray(spellingIssues2) && spellingIssues2.length === 0 &&
                            Array.isArray(policyViolations2) && policyViolations2.length === 0 &&
                            !(res2?.hasInappropriateContent ?? res2?.HasInappropriateContent);

                        if (!passed2) {
                            setChapterCheckModal({
                                open: true,
                                loading: false,
                                error: null,
                                data: {
                                    passed: Boolean(res2?.passed ?? res2?.Passed),
                                    summary: res2?.summary ?? res2?.Summary ?? null,
                                    hasInappropriateContent: Boolean(res2?.hasInappropriateContent ?? res2?.HasInappropriateContent),
                                    spellingIssues: spellingIssues2,
                                    policyViolations: policyViolations2,
                                },
                            });
                            showToast('Nội dung có lỗi chính tả / từ cấm. Vui lòng sửa theo gợi ý trước khi lưu/xuất bản.', 'error');
                            return;
                        }

                        setChapterCheckModal({ open: false, loading: false, data: null, error: null });
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
                setChapterCheckModal({ open: true, loading: false, data: null, error: msg });
                showToast(msg, 'error');
                return;
            }
        }

        setIsSaving(true);
        try {
            const payload = {
                ...chapterData,
                status: saveStatus,
                updatedAt: new Date().toLocaleString('vi-VN'),
            };
            if (isVersionMode && sourceChapterForVersion?.id) {
                payload.sourceChapterId = sourceChapterForVersion.id;
                payload.versionNumber = chapterData.versionNumber ?? 1;
                if (editingVersion?.id) payload.editingVersionId = editingVersion.id;
            }
            await onSave(payload);
        } catch (error) {
            // Error handling is done in parent component
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

    return (
        <div>
            <Header />
            <ToastContainer />
            {/* Popup AI check-chapter: lỗi chính tả / từ cấm */}
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
                                <div style={{ fontSize: '1rem', fontWeight: 800, color: '#0f172a' }}>Kết quả kiểm tra nội dung</div>
                                <div style={{ fontSize: '0.8125rem', color: '#64748b', marginTop: '2px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>
                                    Vui lòng sửa các lỗi bên dưới trước khi lưu hoặc xuất bản.
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

                                    {chapterCheckModal.data?.hasInappropriateContent && (
                                        <div style={{ padding: '12px 14px', backgroundColor: '#fff7ed', border: '1px solid #fed7aa', borderRadius: '10px', color: '#9a3412', fontSize: '0.875rem', marginBottom: '12px' }}>
                                            Nội dung có dấu hiệu không phù hợp theo chính sách nền tảng. Vui lòng chỉnh sửa trước khi lưu/xuất bản.
                                        </div>
                                    )}

                                    <div style={{ display: 'grid', gridTemplateColumns: '1fr', gap: '12px' }}>
                                        <div style={{ border: '1px solid #e5e7eb', borderRadius: '12px', overflow: 'hidden' }}>
                                            <div style={{ padding: '10px 12px', backgroundColor: '#f9fafb', borderBottom: '1px solid #e5e7eb', fontWeight: 800, color: '#0f172a' }}>
                                                Lỗi chính tả ({(chapterCheckModal.data?.spellingIssues?.length ?? 0).toLocaleString()})
                                            </div>
                                            <div style={{ padding: '10px 12px' }}>
                                                {(chapterCheckModal.data?.spellingIssues ?? []).length === 0 ? (
                                                    <div style={{ color: '#64748b', fontSize: '0.875rem' }}>Không có lỗi chính tả.</div>
                                                ) : (
                                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '10px' }}>
                                                        {(chapterCheckModal.data?.spellingIssues ?? []).map((it, idx) => {
                                                            const word = it.wordOrPhrase ?? it.WordOrPhrase ?? '';
                                                            const sug = it.suggestion ?? it.Suggestion ?? '';
                                                            const pos = findIssuePosition(word);
                                                            return (
                                                                <div key={idx} style={{ padding: '10px 12px', border: '1px solid #e2e8f0', borderRadius: '10px' }}>
                                                                    <div style={{ fontSize: '0.875rem', color: '#0f172a' }}>
                                                                        <span style={{ fontWeight: 800 }}>Từ/Cụm</span>: <span style={{ fontWeight: 800, color: '#b91c1c' }}>{word || '—'}</span>
                                                                    </div>
                                                                    <div style={{ fontSize: '0.875rem', color: '#0f172a', marginTop: '4px' }}>
                                                                        <span style={{ fontWeight: 800 }}>Gợi ý</span>: <span style={{ color: '#15803d', fontWeight: 800 }}>{sug || '—'}</span>
                                                                    </div>
                                                                    {pos ? (
                                                                        <div style={{ marginTop: '8px', fontSize: '0.8125rem', color: '#475569' }}>
                                                                            <span style={{ fontWeight: 800 }}>Vị trí</span>:
                                                                            {pos.paraNo != null ? ` đoạn ${pos.paraNo}` : ''}
                                                                            {pos.lineNo != null ? `${pos.paraNo != null ? ',' : ''} dòng ${pos.lineNo}` : ''}
                                                                            {pos.charOffset != null ? `${(pos.paraNo != null || pos.lineNo != null) ? ',' : ''} ký tự ${pos.charOffset}` : ''}
                                                                        </div>
                                                                    ) : (
                                                                        <div style={{ marginTop: '8px', fontSize: '0.8125rem', color: '#94a3b8' }}>
                                                                            Không xác định được vị trí trong nội dung hiện tại.
                                                                        </div>
                                                                    )}
                                                                </div>
                                                            );
                                                        })}
                                                    </div>
                                                )}
                                            </div>
                                        </div>

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
                                                            const pos = findIssuePosition(quote);
                                                            return (
                                                                <div key={idx} style={{ padding: '10px 12px', border: '1px solid #fee2e2', borderRadius: '10px', backgroundColor: '#fff7ed' }}>
                                                                    <div style={{ fontSize: '0.875rem', color: '#9a3412' }}>
                                                                        <span style={{ fontWeight: 800 }}>Loại</span>: <span style={{ fontWeight: 800 }}>{type || '—'}</span>
                                                                    </div>
                                                                    <div style={{ fontSize: '0.875rem', color: '#9a3412', marginTop: '4px', whiteSpace: 'pre-wrap' }}>
                                                                        {desc || '—'}
                                                                    </div>
                                                                    {pos ? (
                                                                        <div style={{ marginTop: '8px', fontSize: '0.8125rem', color: '#7c2d12' }}>
                                                                            <span style={{ fontWeight: 800 }}>Vị trí</span>:
                                                                            {pos.paraNo != null ? ` đoạn ${pos.paraNo}` : ''}
                                                                            {pos.lineNo != null ? `${pos.paraNo != null ? ',' : ''} dòng ${pos.lineNo}` : ''}
                                                                            {pos.charOffset != null ? `${(pos.paraNo != null || pos.lineNo != null) ? ',' : ''} ký tự ${pos.charOffset}` : ''}
                                                                        </div>
                                                                    ) : null}
                                                                    {quote ? (
                                                                        <div style={{ marginTop: '8px', fontSize: '0.8125rem', color: '#7c2d12', backgroundColor: '#fffbeb', border: '1px dashed #fdba74', borderRadius: '10px', padding: '8px 10px', whiteSpace: 'pre-wrap' }}>
                                                                            {quote}
                                                                        </div>
                                                                    ) : null}
                                                                </div>
                                                            );
                                                        })}
                                                    </div>
                                                )}
                                            </div>
                                        </div>
                                    </div>
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
                                Gợi ý chương tiếp theo
                            </h3>
                        </div>
                        <div style={{ padding: '1.25rem 1.5rem', overflowY: 'auto', flex: 1 }}>
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
                            {coCreateResult.ideaContradictionFeedback ?? coCreateResult.IdeaContradictionFeedback ? (
                                <button
                                    type="button"
                                    onClick={() => { setShowCoCreateResultPopup(false); setCoCreateResult(null); }}
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
                                        onClick={() => { setShowCoCreateResultPopup(false); setCoCreateResult(null); }}
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
                                        {readOnly ? 'Xem chi tiết chương' : chapter ? 'Chỉnh sửa chương' : isVersionMode ? (editingVersion ? 'Chỉnh sửa phiên bản' : 'Tạo phiên bản chương') : 'Thêm chương mới'}
                                    </h2>
                                    <p style={{ fontSize: '0.875rem', color: '#6b7280', margin: '0.25rem 0 0 0' }}>
                                        {story?.title}
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
                                        onClick={() => handleSave('draft')}
                                        disabled={isSaving}
                                        className="flex items-center gap-2 px-6 py-2.5 bg-primary/10 text-primary text-sm font-bold rounded-full hover:bg-primary/20 transition-all disabled:opacity-60 disabled:cursor-not-allowed"
                                    >
                                        <Save style={{ width: '16px', height: '16px' }} />
                                        Lưu nháp
                                    </button>
                                    <button
                                        onClick={() => handleSave('published')}
                                        disabled={isSaving || (isVersionMode && !canSubmitVersion)}
                                        title={isVersionMode ? versionPublishTooltip : undefined}
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
                                            Số chương <span style={{ color: '#ef4444' }}>*</span>
                                        </label>
                                        <input
                                            type="number"
                                            value={chapterData.number}
                                            readOnly={readOnly}
                                            disabled={readOnly}
                                            onChange={(e) => {
                                                if (readOnly) return;
                                                const v = e.target.value === '' ? '' : Number(e.target.value);
                                                setChapterData({ ...chapterData, number: v === '' ? '' : v });
                                                const err = v === '' ? 'Vui lòng nhập số chương.' : validateChapterNumber(v);
                                                setChapterNumberError(err);
                                            }}
                                            onBlur={() => {
                                                const err = validateChapterNumber(chapterData.number);
                                                setChapterNumberError(err);
                                            }}
                                            min="1"
                                            style={{
                                                width: '100%',
                                                padding: '0.75rem',
                                                backgroundColor: readOnly ? '#f1f5f9' : '#f9fafb',
                                                border: `1px solid ${chapterNumberError ? '#ef4444' : '#e5e7eb'}`,
                                                borderRadius: '8px',
                                                fontSize: '0.875rem',
                                                outline: 'none',
                                                cursor: readOnly ? 'default' : undefined
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

                            {/* Khi tạo/sửa version: hiển thị Số chương (read-only) + Tiêu đề chương gốc, rồi Số phiên bản + Tiêu đề phiên bản */}
                            {isVersionMode && (
                                <>
                                    <div style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: '1rem' }}>
                                        <div>
                                            <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                                Số chương
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
                                                onClick={() => setChapterData({ ...chapterData, accessType: 'paid' })}
                                                className={`flex items-center gap-3 p-4 border-2 rounded-xl transition-all ${chapterData.accessType === 'paid' ? 'border-amber-500 bg-amber-50' : 'border-slate-200 hover:border-slate-300'}`}
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
                                </div>
                            )}

                            {/* Mô tả thay đổi (version) - chỉ hiện khi chỉnh sửa chương, ẩn khi xem chi tiết */}
                            {chapter && !readOnly && (
                                <div>
                                    <label style={{ display: 'block', fontSize: '0.875rem', fontWeight: 500, color: '#6b7280', marginBottom: '0.5rem' }}>
                                        Mô tả thay đổi (ghi chú phiên bản)
                                    </label>
                                    <input
                                        type="text"
                                        value={chapterData.changeSummary ?? ''}
                                        onChange={(e) => setChapterData({ ...chapterData, changeSummary: e.target.value })}
                                        placeholder="Ví dụ: Sửa lỗi chính tả, bổ sung đoạn mới..."
                                        maxLength={500}
                                        style={{
                                            width: '100%',
                                            padding: '0.75rem',
                                            backgroundColor: '#f9fafb',
                                            border: '1px solid #e5e7eb',
                                            borderRadius: '8px',
                                            fontSize: '0.875rem',
                                            outline: 'none'
                                        }}
                                    />
                                    <p style={{ fontSize: '0.75rem', color: '#9ca3af', marginTop: '0.25rem' }}>
                                        Tùy chọn. Khi lưu, hệ thống sẽ tạo phiên bản nội dung cho chương này.
                                    </p>
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
                                                AI gợi ý ý tưởng{aiUsageLimit ? ` (${aiUsageLimit.remaining}/${aiUsageLimit.limitPerDay})` : ''}
                                            </span>
                                            <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.5rem', padding: '0.5rem 1rem', backgroundColor: '#e2e8f0', color: '#64748b', fontSize: '0.875rem', fontWeight: 600, borderRadius: '9999px' }}>
                                                <Sparkles style={{ width: '14px', height: '14px' }} />
                                                AI gợi ý chương{aiUsageLimit ? ` (${aiUsageLimit.remaining}/${aiUsageLimit.limitPerDay})` : ''}
                                            </span>
                                        </>
                                    ) : (
                                        <>
                                            <button type="button" onClick={() => handleAISuggestion('paragraph')} className="flex items-center gap-2 px-4 py-2 bg-primary/10 text-primary text-sm font-bold rounded-full hover:bg-primary/20 transition-all">
                                                <Sparkles style={{ width: '14px', height: '14px' }} />
                                                AI gợi ý ý tưởng{aiUsageLimit ? ` (${aiUsageLimit.remaining}/${aiUsageLimit.limitPerDay})` : ''}
                                            </button>
                                            <button type="button" onClick={() => handleAISuggestion('chapter')} className="flex items-center gap-2 px-4 py-2 bg-primary/10 text-primary text-sm font-bold rounded-full hover:bg-primary/20 transition-all">
                                                <Sparkles style={{ width: '14px', height: '14px' }} />
                                                AI gợi ý chương{aiUsageLimit ? ` (${aiUsageLimit.remaining}/${aiUsageLimit.limitPerDay})` : ''}
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
                                <textarea
                                    value={chapterData.content}
                                    readOnly={readOnly}
                                    disabled={readOnly}
                                    onChange={(e) => !readOnly && setChapterData({ ...chapterData, content: e.target.value })}
                                    placeholder="Nhập nội dung chương của bạn...&#10;&#10;Bạn có thể sử dụng AI để gợi ý nội dung bằng cách click vào các nút phía trên."
                                    rows={25}
                                    style={{
                                        width: '100%',
                                        padding: '1rem',
                                        backgroundColor: readOnly ? '#f1f5f9' : editorSettings.backgroundColor,
                                        border: '1px solid #e5e7eb',
                                        borderRadius: '8px',
                                        fontSize: `${editorSettings.fontSize}px`,
                                        fontFamily: editorSettings.fontFamily,
                                        outline: 'none',
                                        cursor: readOnly ? 'default' : undefined,
                                        resize: 'vertical',
                                        lineHeight: '1.8'
                                    }}
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
