import { useState, useEffect } from 'react';
import { X } from 'lucide-react';
import { StoryInfoForm } from '../../components/author/story-editor/StoryInfoForm';
import { ChapterList } from '../../components/author/story-editor/ChapterList';
import { ChapterEditor } from '../../components/author/story-editor/ChapterEditor';
import { StepIndicator } from '../../components/author/story-editor/StepIndicator';
import { useToast } from '../../components/author/story-editor/Toast';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { useAuth } from '../../contexts/AuthContext';
import { checkBannedWords, checkChapterSpelling } from '../../api/ai/aiApi';
import { stripHtmlToText } from '../../utils/richText';

// Helper function to count words
const countWords = (text) => {
    const plain = stripHtmlToText(text);
    if (!plain) return 0;
    return plain.split(/\s+/).filter(word => word.length > 0).length;
};
const MIN_STORY_SUMMARY_WORDS = 50;

export function StoryEditor({ story, onSave, onCancel }) {
    const { user } = useAuth();
    const authorName = user?.displayName ?? user?.DisplayName ?? user?.fullName ?? user?.FullName ?? user?.nickname ?? user?.Nickname ?? '';
    const [currentStep, setCurrentStep] = useState(1);
    const [saving, setSaving] = useState(false);
    const { showToast, ToastContainer } = useToast();
    const storyTotalViews = Number(story?.totalViews ?? story?.TotalViews ?? 0) || 0;
    const canEnablePaidMode = storyTotalViews >= 500;

    const [formData, setFormData] = useState({
        title: '',
        author: authorName,
        status: 'Đang ra',
        ageRating: 'Phù hợp mọi lứa tuổi',
        categories: [],
        tags: [],
        note: '',
        cover: '',
        coverFile: null,
    });

    const [chapters, setChapters] = useState([
        { id: 1, number: 1, title: '', content: '', accessType: 'public', price: 0 }
    ]);

    const [currentChapterIndex, setCurrentChapterIndex] = useState(0);

    const steps = [
        { number: 1, title: 'Thông tin truyện' },
        { number: 2, title: 'Nội dung' },
        { number: 3, title: 'Đăng truyện' },
        { number: 4, title: 'Hoàn Thành' },
    ];

    const [completionType, setCompletionType] = useState('publish'); // 'draft' | 'publish'

    const [manualSpellingCheckLoading, setManualSpellingCheckLoading] = useState(false);
    const [chapterCheckModal, setChapterCheckModal] = useState({
        open: false,
        loading: false,
        mode: null, // 'spelling-support' | 'banned'
        data: null,
        error: null,
    });

    useEffect(() => {
        const name = user?.displayName ?? user?.DisplayName ?? user?.fullName ?? user?.FullName ?? user?.nickname ?? user?.Nickname ?? '';
        if (story) {
            const cats = story.categories || [];
            const normalized = Array.isArray(cats)
                ? cats.map((c) => (typeof c === 'object' && c?.id ? { id: c.id, name: c.name || '' } : { id: c, name: String(c) }))
                : [];
            setFormData({
                title: story.title || '',
                author: story.author ?? name,
                status: story.publishStatus || 'Đang ra',
                ageRating: story.ageRating ?? 'Phù hợp mọi lứa tuổi',
                categories: normalized,
                tags: [],
                note: story.summary ?? story.note ?? '',
                cover: story.cover || '',
                coverFile: null,
            });
        } else {
            setFormData((prev) => ({ ...prev, author: name, status: 'Đang ra' }));
        }
    }, [story, user]);

    const minChapters = 1;

    const handleFormDataChange = (field, value) => {
        if (!story && field === 'status' && value !== 'Đang ra') return;
        setFormData((prev) => ({ ...prev, [field]: value }));
    };

    const handleImageUpload = (e) => {
        const file = e.target.files?.[0];
        if (file) {
            const allowedExtensions = ['.jpg', '.jpeg', '.png', '.gif', '.webp'];
            const lowerName = String(file.name || '').toLowerCase();
            const ext = lowerName.slice(lowerName.lastIndexOf('.'));
            if (!allowedExtensions.includes(ext)) {
                showToast(`Ảnh bìa chỉ chấp nhận ${allowedExtensions.join(', ').toUpperCase()}`, 'error');
                e.target.value = '';
                return;
            }
            if (file.size > 5 * 1024 * 1024) {
                showToast('Kích thước ảnh bìa không được vượt quá 5MB', 'error');
                e.target.value = '';
                return;
            }
            const previewUrl = URL.createObjectURL(file);
            setFormData((prev) => {
                if (prev.cover && String(prev.cover).startsWith('blob:')) {
                    URL.revokeObjectURL(prev.cover);
                }
                return { ...prev, cover: previewUrl, coverFile: file };
            });
            e.target.value = '';
        }
    };

    const handleChapterChange = (field, value) => {
        setChapters((prev) => {
            const updated = [...prev];
            updated[currentChapterIndex] = {
                ...updated[currentChapterIndex],
                [field]: value
            };
            return updated;
        });
    };

    const handleAddChapter = () => {
        const newChapter = {
            id: Date.now(),
            number: chapters.length + 1,
            title: '',
            content: '',
            accessType: 'public',
            price: 0
        };
        setChapters([...chapters, newChapter]);
        setCurrentChapterIndex(chapters.length);
        showToast(`Đã thêm chương ${newChapter.number}`, 'success');

        // Scroll to top
        window.scrollTo({ top: 0, behavior: 'smooth' });
    };

    const validateStep1 = () => {
        if (!formData.title.trim()) {
            showToast('Vui lòng nhập tên truyện', 'error');
            return false;
        }
        if (formData.title.trim().length > 50) {
            showToast('Tên truyện không được vượt quá 50 ký tự', 'error');
            return false;
        }
        if (formData.categories.length === 0) {
            showToast('Vui lòng chọn ít nhất 1 thể loại', 'error');
            return false;
        }
        if (!formData.cover) {
            showToast('Vui lòng tải ảnh bìa lên', 'error');
            return false;
        }
        if (!formData.note || !String(formData.note).trim()) {
            showToast('Vui lòng nhập mô tả truyện', 'error');
            return false;
        }
        const summaryWordCount = countWords(formData.note);
        if (summaryWordCount < MIN_STORY_SUMMARY_WORDS) {
            showToast(`Mô tả truyện cần tối thiểu ${MIN_STORY_SUMMARY_WORDS} từ (hiện có ${summaryWordCount} từ).`, 'error');
            return false;
        }
        if (!story && formData.status !== 'Đang ra') {
            showToast('Truyện mới bắt buộc ở trạng thái Đang ra.', 'error');
            return false;
        }
        return true;
    };

    const validateStep2 = () => {
        if (chapters.length < minChapters) {
            showToast(`Cần ít nhất ${minChapters} chương để tiếp tục`, 'error');
            return false;
        }
        const chapterMissingTitle = chapters.find((ch) => !String(ch?.title ?? '').trim());
        if (chapterMissingTitle) {
            const chapterNo = Number(chapterMissingTitle?.number ?? 0) > 0 ? chapterMissingTitle.number : null;
            showToast(chapterNo ? `Chương ${chapterNo}: Chưa nhập tên chương` : 'Chưa nhập tên chương', 'error');
            return false;
        }

        const chapterMissingContent = chapters.find((ch) => !stripHtmlToText(ch?.content ?? ''));
        if (chapterMissingContent) {
            const chapterNo = Number(chapterMissingContent?.number ?? 0) > 0 ? chapterMissingContent.number : null;
            showToast(chapterNo ? `Chương ${chapterNo}: Chưa nhập nội dung` : 'Chưa nhập nội dung', 'error');
            return false;
        }
        const chaptersWithLongTitle = chapters.filter((ch) => (ch.title ?? '').trim().length > 50);
        if (chaptersWithLongTitle.length > 0) {
            showToast(`Tên chương không được vượt quá 50 ký tự (có ${chaptersWithLongTitle.length} chương vi phạm).`, 'error');
            return false;
        }
        // Validate minimum 500 words per chapter
        const chaptersWithInsufficientWords = chapters
            .map((ch) => ({
                ...ch,
                _wordCount: countWords(ch?.content ?? ''),
            }))
            .filter((ch) => ch._wordCount < 500);
        if (chaptersWithInsufficientWords.length > 0) {
            if (chaptersWithInsufficientWords.length === 1) {
                const ch = chaptersWithInsufficientWords[0];
                const chapterNo = Number(ch?.number ?? 0) > 0 ? ch.number : null;
                const msgPrefix = chapterNo ? `Chương ${chapterNo}` : 'Chương hiện tại';
                showToast(`${msgPrefix}: Chưa đủ 500 từ (hiện có ${ch._wordCount} từ)`, 'error');
                return false;
            }
            showToast(`Có ${chaptersWithInsufficientWords.length} chương chưa đủ 500 từ`, 'error');
            return false;
        }

        // Validate chế độ sáng tác (Public / Paid) giống màn tạo/chỉnh sửa chương
        const isNewStory = !story;
        for (const ch of chapters) {
            if (!ch) continue;
            if (ch.accessType === 'paid') {
                // Create story: backend chỉ cho phép PAID khi story.total_views >= 500.
                // Edit story: nếu chương đã là PAID sẵn thì BE không chặn theo rule “chuyển từ FREE sang PAID”.
                if (isNewStory && !canEnablePaidMode) {
                    showToast('Truyện cần tối thiểu 500 lượt xem mới được bật chế độ trả phí cho chương.', 'error');
                    return false;
                }
                if (!ch.price || Number(ch.price) <= 0) {
                    showToast('Vui lòng nhập giá cho chương trả phí', 'error');
                    return false;
                }
            }
        }

        return true;
    };

    const findIssuePosition = (contentRaw, needleRaw) => {
        const needle = (needleRaw ?? '').toString().trim();
        const content = (contentRaw ?? '').toString();
        if (!needle || !content) return null;

        const lowerNeedle = needle.toLowerCase();
        const lines = content.split(/\r?\n/);

        const lineIndex = lines.findIndex((ln) => ln.toLowerCase().includes(lowerNeedle));
        const lineNo = lineIndex >= 0 ? lineIndex + 1 : null;

        const paragraphs = content.split(/\r?\n\s*\r?\n/);
        const paraIndex = paragraphs.findIndex((p) => p.toLowerCase().includes(lowerNeedle));
        const paraNo = paraIndex >= 0 ? paraIndex + 1 : null;

        const idx = content.toLowerCase().indexOf(lowerNeedle);
        const charOffset = idx >= 0 ? idx + 1 : null;

        if (lineNo == null && paraNo == null && charOffset == null) return null;
        return { lineNo, paraNo, charOffset };
    };

    /** Tránh báo nhầm khi tóm tắt «Không phát hiện lỗi chính tả» (vẫn chứa «lỗi chính tả»). Khớp ChapterEditorPage / BE. */
    const summaryImpliesSpellingIssue = (summaryText) => {
        const s = String(summaryText ?? '').trim().toLowerCase();
        if (!s) return false;
        if (s.includes('không phát hiện')) return false;
        if (/không\s+có\s+lỗi\s+chính\s+tả/i.test(s)) return false;
        if (/không\s+còn\s+lỗi\s+chính\s+tả/i.test(s)) return false;
        return s.includes('lỗi chính tả') || s.includes('dấu câu');
    };

    const buildSpellingSummary = (rawSummary, spellingIssues) => {
        const count = Array.isArray(spellingIssues) ? spellingIssues.length : 0;
        if (count > 0) return `Phát hiện ${count} lỗi chính tả.`;
        const s = String(rawSummary ?? '').trim();
        if (!s) return 'Không phát hiện lỗi chính tả.';
        if (/t[ừu]\s*c[ấa]m|ch[ií]nh\s*s[áa]ch/i.test(s)) return 'Không phát hiện lỗi chính tả.';
        return s;
    };

    const policyTypeVi = (typeRaw) => {
        const t = String(typeRaw ?? '').trim().toUpperCase();
        if (t === 'BANNEDWORD') return 'Từ cấm';
        return typeRaw || '—';
    };

    const buildBannedWordsSummary = (rawSummary, policyViolations, hasInappropriateContent) => {
        const count = Array.isArray(policyViolations) ? policyViolations.length : 0;
        if (count > 0) return `Phát hiện ${count} vi phạm từ cấm/chính sách.`;
        if (hasInappropriateContent) return 'Nội dung có dấu hiệu không phù hợp theo chính sách nền tảng.';
        const s = String(rawSummary ?? '').trim();
        if (!s) return 'Không phát hiện vi phạm từ cấm/chính sách.';
        if (/ch[ií]nh t[ảa]/i.test(s)) return 'Không phát hiện vi phạm từ cấm/chính sách.';
        return s;
    };

    const handleManualSpellingCheck = async () => {
        const ch = chapters[currentChapterIndex];
        if (!ch) return;
        if (!stripHtmlToText(ch.content)) {
            setChapterCheckModal({
                open: true,
                loading: false,
                mode: 'spelling-support',
                data: null,
                error: 'Vui lòng nhập nội dung chương trước khi kiểm tra.',
            });
            return;
        }

        setManualSpellingCheckLoading(true);
        try {
            setChapterCheckModal({ open: true, loading: true, mode: 'spelling-support', data: null, error: null });
            const res = await checkChapterSpelling({
                content: stripHtmlToText(ch.content),
                storyId: story?.id ?? story?.Id ?? null,
                chapterTitle: ch.title ?? null,
            });

            const spellingIssues = res?.spellingIssues ?? res?.SpellingIssues ?? [];
            const normalizedSummary = buildSpellingSummary(res?.summary ?? res?.Summary ?? null, spellingIssues);

            setChapterCheckModal({
                open: true,
                loading: false,
                mode: 'spelling-support',
                error: null,
                data: {
                    passed: spellingIssues.length === 0 && !summaryImpliesSpellingIssue(normalizedSummary),
                    summary: normalizedSummary,
                    spellingIssues,
                    contentForPosition: ch.content,
                },
            });
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.response?.data?.detail ?? err?.message ?? 'Không thể kiểm tra chính tả.';
            setChapterCheckModal({ open: true, loading: false, mode: 'spelling-support', data: null, error: msg });
        } finally {
            setManualSpellingCheckLoading(false);
        }
    };

    const checkBannedWordsBeforeStep3 = async () => {
        // Kiểm tra toàn bộ chapters để đảm bảo “không vi phạm từ cấm” mới cho qua bước 3.
        for (let i = 0; i < chapters.length; i++) {
            const ch = chapters[i];
            if (!ch) continue;

            const content = stripHtmlToText(ch.content);
            if (!content) continue; // validateStep2 sẽ chặn nên nhánh này chủ yếu để guard.

            try {
                const res = await checkBannedWords({
                    content,
                    storyId: story?.id ?? story?.Id ?? null,
                    chapterTitle: ch.title ?? null,
                });

                const policyViolations = res?.policyViolations ?? res?.PolicyViolations ?? [];
                const passed = Boolean(res?.passed ?? res?.Passed)
                    && Array.isArray(policyViolations) && policyViolations.length === 0
                    && !(res?.hasInappropriateContent ?? res?.HasInappropriateContent);

                if (!passed) {
                    const hasInappropriateContent = Boolean(res?.hasInappropriateContent ?? res?.HasInappropriateContent);
                    const summary = buildBannedWordsSummary(res?.summary ?? res?.Summary ?? null, policyViolations, hasInappropriateContent);

                    setChapterCheckModal({
                        open: true,
                        loading: false,
                        mode: 'banned',
                        error: null,
                        data: {
                            passed,
                            summary,
                            hasInappropriateContent,
                            policyViolations,
                            contentForPosition: content,
                        },
                    });
                    return false;
                }
            } catch (err) {
                const msg = err?.response?.data?.message ?? err?.response?.data?.detail ?? err?.message ?? 'Không thể kiểm tra từ cấm/chính sách.';
                setChapterCheckModal({ open: true, loading: false, mode: 'banned', data: null, error: msg });
                return false;
            }
        }
        return true;
    };

    const handleNextStep = async () => {
        if (currentStep === 1 && !validateStep1()) return;
        if (currentStep === 2 && !validateStep2()) return;

        // Bước 2 -> Bước 3: kiểm tra từ cấm
        if (currentStep === 2) {
            const ok = await checkBannedWordsBeforeStep3();
            if (!ok) return;
        }

        if (currentStep < 4) {
            const fromStep = currentStep;
            setCurrentStep(currentStep + 1);
            window.scrollTo({ top: 0, behavior: 'smooth' });
            if (fromStep === 1) {
                showToast('Đã điền thông tin truyện thành công.', 'success');
            } else {
                showToast('Đã chuyển sang bước tiếp theo', 'success');
            }
        }
    };

    const handlePrevStep = () => {
        if (currentStep > 1) {
            setCurrentStep(currentStep - 1);
            window.scrollTo({ top: 0, behavior: 'smooth' });
        }
    };

    const handleSubmit = async (isDraft) => {
        if (!validateStep1() || !validateStep2()) {
            showToast('Vui lòng hoàn thành tất cả thông tin bắt buộc', 'error');
            return;
        }
        // Khi bấm "Xuất bản" thì các chương sẽ được tạo ở trạng thái PENDING_REVIEW (đang chờ duyệt),
        // do đó không được phép đặt trạng thái tiến độ là "Tạm dừng" hoặc "Hoàn thành".
        if (!isDraft && (formData.status === 'Tạm dừng' || formData.status === 'Hoàn thành')) {
            showToast('Truyện đang có chương chờ duyệt, vui lòng thử lại sau.', 'error');
            return;
        }

        const getCategoryId = (c) => (typeof c === 'object' && c?.id ? c.id : c);
        const categoryIds = (formData.categories || []).map(getCategoryId).filter(Boolean);

        const storyData = {
            ...formData,
            categoryIds,
            isDraft,
            status: isDraft ? 'DRAFT' : 'PENDING_REVIEW',
            storyProgressStatus: !story ? 'Đang ra' : formData.status,
            chaptersData: chapters.map((ch, i) => ({
                title: ch.title,
                content: ch.content || '',
                orderIndex: i,
                status: isDraft ? 'DRAFT' : 'PENDING_REVIEW',
                accessType: (ch.accessType === 'paid' ? 'PAID' : 'FREE'),
                coinPrice: ch.accessType === 'paid' ? Number(ch.price) || 0 : 0,
            })),
            chaptersCount: chapters.length,
            lastUpdate: 'Vừa xong',
            publishStatus: isDraft ? 'Lưu tạm' : 'Chờ duyệt',
        };

        setSaving(true);
        try {
            await onSave(storyData);
            showToast(isDraft ? 'Đã lưu bản nháp' : 'Đăng truyện thành công! Đang chờ duyệt.', 'success');
            setCompletionType(isDraft ? 'draft' : 'publish');
            setCurrentStep(4);
        } catch (err) {
            showToast(err?.message || 'Có lỗi xảy ra', 'error');
        } finally {
            setSaving(false);
        }
    };

    return (
        <div>
            <Header />
            <div style={{ minHeight: '100vh', backgroundColor: '#f5f5f5' }}>
                <ToastContainer />

                {/* Header with Stepper */}
                <div style={{ backgroundColor: '#ffffff', borderBottom: '1px solid #e0e0e0', padding: '2rem 0' }}>
                    <div style={{ maxWidth: '1000px', margin: '0 auto', padding: '0 2rem' }}>
                        <StepIndicator currentStep={currentStep} steps={steps} />
                    </div>
                </div>

                {/* Content */}
                <div style={{ maxWidth: '1000px', margin: '0 auto', padding: '2rem' }}>
                    {/* Step 1: Thông tin truyện */}
                    {currentStep === 1 && (
                        <StoryInfoForm
                            formData={formData}
                            onChange={handleFormDataChange}
                            onImageUpload={handleImageUpload}
                            lockStoryProgressStatus={!story}
                        />
                    )}

                    {/* Step 2: Nội dung */}
                    {currentStep === 2 && (
                        <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
                            <ChapterList
                                chapters={chapters}
                                currentChapterIndex={currentChapterIndex}
                                onChapterSelect={setCurrentChapterIndex}
                                onAddChapter={handleAddChapter}
                                minChapters={minChapters}
                            />

                            <ChapterEditor
                                chapter={chapters[currentChapterIndex]}
                                onChange={handleChapterChange}
                                story={story}
                                onSpellcheckSupport={handleManualSpellingCheck}
                                spellcheckLoading={manualSpellingCheckLoading}
                            />
                        </div>
                    )}

                    {/* Step 3: Review */}
                    {currentStep === 3 && (
                        <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '2rem', border: '1px solid #e0e0e0' }}>
                            <h3 style={{ fontSize: '1.125rem', fontWeight: 'bold', color: '#333333', marginBottom: '1.5rem' }}>
                                Xác nhận thông tin trước khi đăng
                            </h3>

                            <div style={{ display: 'flex', gap: '2rem', marginBottom: '2rem' }}>
                                {formData.cover && (
                                    <img
                                        src={formData.cover}
                                        alt="Cover"
                                        style={{
                                            width: '150px',
                                            height: '200px',
                                            objectFit: 'cover',
                                            borderRadius: '8px',
                                            border: '1px solid #e0e0e0'
                                        }}
                                    />
                                )}
                                <div style={{ flex: 1, display: 'grid', gap: '1rem' }}>
                                    <div style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: '1rem' }}>
                                        <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>Tên truyện:</div>
                                        <div style={{ fontSize: '0.875rem', color: '#333333', fontWeight: 500 }}>{formData.title}</div>
                                    </div>
                                    <div style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: '1rem' }}>
                                        <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>Thể loại:</div>
                                        <div style={{ fontSize: '0.875rem', color: '#333333' }}>
                                            {(formData.categories || []).map((c) => (typeof c === 'object' && c?.name ? c.name : String(c))).join(', ')}
                                        </div>
                                    </div>
                                    <div style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: '1rem' }}>
                                        <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>Số chương:</div>
                                        <div style={{ fontSize: '0.875rem', color: '#333333' }}>{chapters.length} chương</div>
                                    </div>
                                    <div style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: '1rem' }}>
                                        <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>Trạng thái:</div>
                                        <div style={{ fontSize: '0.875rem', color: '#333333' }}>{formData.status}</div>
                                    </div>
                                    <div style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: '1rem' }}>
                                        <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>Giới hạn độ tuổi:</div>
                                        <div style={{ fontSize: '0.875rem', color: '#333333' }}>{formData.ageRating}</div>
                                    </div>
                                    <div style={{ display: 'grid', gridTemplateColumns: '150px 1fr', gap: '1rem' }}>
                                        <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>Mô tả truyện:</div>
                                        <div
                                            style={{
                                                fontSize: '0.875rem',
                                                color: '#333333',
                                                whiteSpace: 'pre-wrap',
                                                lineHeight: 1.5,
                                            }}
                                        >
                                            {formData.note ? formData.note : '—'}
                                        </div>
                                    </div>
                                </div>
                            </div>

                            {/* Chapter Preview */}
                            <div style={{
                                padding: '1rem',
                                backgroundColor: '#f9fafb',
                                borderRadius: '4px',
                                border: '1px solid #e5e7eb'
                            }}>
                                <h4 style={{ fontSize: '0.875rem', fontWeight: 600, color: '#333333', marginBottom: '0.75rem' }}>
                                    Danh sách chương ({chapters.length})
                                </h4>
                                <div style={{
                                    display: 'grid',
                                    gap: '0.5rem',
                                    maxHeight: '300px',
                                    overflowY: 'auto'
                                }}>
                                    {chapters.map((ch) => (
                                        <div
                                            key={ch.id}
                                            style={{
                                                padding: '0.5rem 0.75rem',
                                                backgroundColor: '#ffffff',
                                                borderRadius: '4px',
                                                display: 'flex',
                                                justifyContent: 'space-between',
                                                alignItems: 'center',
                                                fontSize: '0.875rem'
                                            }}
                                        >
                                            <div>
                                                <span style={{ fontWeight: 600, color: '#333333' }}>Chương {ch.number}:</span>
                                                {' '}
                                                <span style={{ color: '#6b7280' }}>{ch.title || '(Chưa có tiêu đề)'}</span>
                                            </div>
                                            <span style={{ fontSize: '0.75rem', color: '#9ca3af' }}>
                                                {countWords(ch.content)} từ
                                            </span>
                                        </div>
                                    ))}
                                </div>
                            </div>
                        </div>
                    )}

                    {/* Step 4: Complete */}
                    {currentStep === 4 && (
                        <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '3rem', border: '1px solid #e0e0e0', textAlign: 'center' }}>
                            <div style={{ fontSize: '4rem', marginBottom: '1rem' }}>🎉</div>
                            <h3 style={{ fontSize: '1.5rem', fontWeight: 'bold', color: '#333333', marginBottom: '0.5rem' }}>
                                {completionType === 'draft' ? 'Đã lưu bản nháp!' : 'Đăng truyện thành công!'}
                            </h3>
                            <p style={{ fontSize: '0.875rem', color: '#6b7280', marginBottom: '2rem' }}>
                                Truyện "{formData.title}" đã được đăng tải với {chapters.length} chương
                            </p>
                            <button
                                onClick={onCancel}
                                style={{
                                    padding: '0.75rem 2rem',
                                    backgroundColor: '#13ec5b',
                                    border: 'none',
                                    borderRadius: '9999px',
                                    fontSize: '0.875rem',
                                    fontWeight: 700,
                                    color: '#ffffff',
                                    cursor: 'pointer',
                                    transition: 'all 0.2s'
                                }}
                                onMouseEnter={(e) => {
                                    e.currentTarget.style.backgroundColor = '#10d452';
                                }}
                                onMouseLeave={(e) => {
                                    e.currentTarget.style.backgroundColor = '#13ec5b';
                                }}
                            >
                                Về trang quản lý
                            </button>
                        </div>
                    )}

                    {/* Navigation Buttons */}
                    {currentStep < 4 && (
                        <div style={{
                            display: 'flex',
                            justifyContent: 'space-between',
                            marginTop: '2rem',
                            paddingTop: '2rem',
                            borderTop: '1px solid #e0e0e0'
                        }}>
                            <button
                                onClick={currentStep === 1 ? onCancel : handlePrevStep}
                                style={{
                                    padding: '0.75rem 2rem',
                                    backgroundColor: '#ffffff',
                                    border: '2px solid #13ec5b',
                                    borderRadius: '9999px',
                                    fontSize: '0.875rem',
                                    fontWeight: 700,
                                    color: '#13ec5b',
                                    cursor: 'pointer',
                                    transition: 'all 0.2s'
                                }}
                                onMouseEnter={(e) => {
                                    e.currentTarget.style.backgroundColor = '#f0fdf4';
                                    e.currentTarget.style.borderColor = '#10d452';
                                }}
                                onMouseLeave={(e) => {
                                    e.currentTarget.style.backgroundColor = '#ffffff';
                                    e.currentTarget.style.borderColor = '#13ec5b';
                                }}
                            >
                                {currentStep === 1 ? 'Hủy' : 'Quay lại'}
                            </button>

                            <div style={{ display: 'flex', gap: '1rem' }}>
                                {currentStep > 1 && currentStep !== 2 && (
                                    <button
                                        disabled={saving}
                                        onClick={() => handleSubmit(true)}
                                        style={{
                                            padding: '0.75rem 2rem',
                                            backgroundColor: '#ffffff',
                                            border: '2px solid #13ec5b',
                                            borderRadius: '9999px',
                                            fontSize: '0.875rem',
                                            fontWeight: 700,
                                            color: '#13ec5b',
                                            cursor: saving ? 'not-allowed' : 'pointer',
                                            transition: 'all 0.2s',
                                            opacity: saving ? 0.6 : 1
                                        }}
                                        onMouseEnter={(e) => {
                                            if (!saving) {
                                                e.currentTarget.style.backgroundColor = '#f0fdf4';
                                                e.currentTarget.style.borderColor = '#10d452';
                                            }
                                        }}
                                        onMouseLeave={(e) => {
                                            if (!saving) {
                                                e.currentTarget.style.backgroundColor = '#ffffff';
                                                e.currentTarget.style.borderColor = '#13ec5b';
                                            }
                                        }}
                                    >
                                        {saving ? 'Đang lưu...' : 'Lưu nháp'}
                                    </button>
                                )}

                                {currentStep === 3 ? (
                                    <button
                                        disabled={saving}
                                        onClick={() => handleSubmit(false)}
                                        style={{
                                            padding: '0.75rem 2rem',
                                            backgroundColor: '#13ec5b',
                                            border: 'none',
                                            borderRadius: '9999px',
                                            fontSize: '0.875rem',
                                            fontWeight: 700,
                                            color: '#ffffff',
                                            cursor: saving ? 'not-allowed' : 'pointer',
                                            transition: 'all 0.2s',
                                            opacity: saving ? 0.6 : 1
                                        }}
                                        onMouseEnter={(e) => {
                                            if (!saving) {
                                                e.currentTarget.style.backgroundColor = '#10d452';
                                            }
                                        }}
                                        onMouseLeave={(e) => {
                                            if (!saving) {
                                                e.currentTarget.style.backgroundColor = '#13ec5b';
                                            }
                                        }}
                                    >
                                        {saving ? 'Đang xuất bản...' : 'Xuất bản'}
                                    </button>
                                ) : (
                                    <button
                                        onClick={handleNextStep}
                                        style={{
                                            padding: '0.75rem 2rem',
                                            backgroundColor: '#13ec5b',
                                            border: 'none',
                                            borderRadius: '9999px',
                                            fontSize: '0.875rem',
                                            fontWeight: 700,
                                            color: '#ffffff',
                                            cursor: 'pointer',
                                            transition: 'all 0.2s'
                                        }}
                                        onMouseEnter={(e) => {
                                            e.currentTarget.style.backgroundColor = '#10d452';
                                        }}
                                        onMouseLeave={(e) => {
                                            e.currentTarget.style.backgroundColor = '#13ec5b';
                                        }}
                                    >
                                        Tiếp theo
                                    </button>
                                )}
                            </div>
                        </div>
                    )}
                </div>
            </div>
            {chapterCheckModal.open && (
                <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-slate-900/55 backdrop-blur-sm">
                    <div className="bg-white dark:bg-slate-900 rounded-2xl shadow-2xl max-w-2xl w-full border border-slate-200 dark:border-slate-700 overflow-hidden">
                        <div className="flex items-center justify-between px-6 py-4 border-b border-slate-200 dark:border-slate-700 shrink-0">
                            <h3 className="text-2xl font-bold text-slate-900 dark:text-white">
                                {chapterCheckModal.mode === 'spelling-support'
                                    ? 'Kết quả hỗ trợ kiểm tra chính tả'
                                    : 'Kết quả kiểm tra từ cấm/chính sách'}
                            </h3>
                            <button
                                type="button"
                                onClick={() => setChapterCheckModal((p) => ({ ...p, open: false }))}
                                className="p-1.5 hover:bg-slate-100 dark:hover:bg-slate-800 rounded-lg transition-colors"
                            >
                                <X className="w-6 h-6 text-slate-500 dark:text-slate-300" />
                            </button>
                        </div>

                        <div className="px-6 py-5 overflow-y-auto">
                            {chapterCheckModal.loading ? (
                                <div className="p-6 text-center text-slate-500">Đang kiểm tra...</div>
                            ) : chapterCheckModal.error ? (
                                <div className="mb-4 rounded-lg border border-red-200 bg-red-50 px-3 py-2 text-sm text-red-700 dark:border-red-900/50 dark:bg-red-900/20 dark:text-red-300">
                                    {chapterCheckModal.error}
                                </div>
                            ) : (
                                <>
                                    {chapterCheckModal.data?.summary && (
                                        <div className="mb-4 rounded-lg border border-slate-200 bg-slate-50 px-3 py-2 text-sm text-slate-900 dark:border-slate-700 dark:bg-slate-800 dark:text-slate-100">
                                            <div style={{ fontWeight: 800, marginBottom: '4px' }}>Tóm tắt</div>
                                            <div style={{ whiteSpace: 'pre-wrap' }}>{chapterCheckModal.data.summary}</div>
                                        </div>
                                    )}

                                    {chapterCheckModal.mode === 'banned' ? (
                                        <div className="mb-4 rounded-lg border border-amber-200 bg-amber-50 px-3 py-2 text-sm text-amber-900">
                                            Bạn cần sửa toàn bộ vi phạm từ cấm/chính sách trước khi được chuyển sang bước 3.
                                        </div>
                                    ) : null}

                                    {chapterCheckModal.mode === 'spelling-support' ? (
                                        <div className="space-y-3">
                                            {(chapterCheckModal.data?.spellingIssues ?? []).length === 0 ? (
                                                <div className="text-sm text-slate-500">Không phát hiện lỗi chính tả.</div>
                                            ) : (
                                                (chapterCheckModal.data?.spellingIssues ?? []).map((it, idx) => {
                                                    const word = it.wordOrPhrase ?? it.WordOrPhrase ?? '';
                                                    const sug = it.suggestion ?? it.Suggestion ?? '';
                                                    const ctx = it.context ?? it.Context ?? '';
                                                    return (
                                                        <div key={idx} className="rounded-xl border border-slate-200 bg-white p-3 dark:border-slate-700 dark:bg-slate-800">
                                                            <div className="text-sm text-slate-900 dark:text-slate-100">
                                                                <span style={{ fontWeight: 900 }}>Từ/Cụm</span>: <span style={{ fontWeight: 900, color: '#b91c1c' }}>{word || '—'}</span>
                                                            </div>
                                                            <div className="text-sm text-slate-900 dark:text-slate-100 mt-2">
                                                                <span style={{ fontWeight: 900 }}>Gợi ý</span>: <span style={{ fontWeight: 900, color: '#15803d' }}>{sug || '—'}</span>
                                                            </div>
                                                            {String(ctx).trim() ? (
                                                                <div className="mt-2 rounded-lg border border-slate-200 bg-slate-50 px-2 py-2 text-xs text-slate-800 dark:border-slate-600 dark:bg-slate-900 dark:text-slate-100">
                                                                    <span style={{ fontWeight: 800 }}>Câu/dòng chứa lỗi</span>
                                                                    <div className="mt-1 whitespace-pre-wrap">{ctx}</div>
                                                                </div>
                                                            ) : (
                                                                <div className="text-xs text-slate-400 dark:text-slate-400 mt-2">
                                                                    Không có đoạn trích chứa từ sai cho mục này.
                                                                </div>
                                                            )}
                                                        </div>
                                                    );
                                                })
                                            )}
                                        </div>
                                    ) : (
                                        <div className="space-y-3">
                                            {(chapterCheckModal.data?.policyViolations ?? []).length === 0 ? (
                                                <div className="text-sm text-slate-500">Không phát hiện vi phạm.</div>
                                            ) : (
                                                (chapterCheckModal.data?.policyViolations ?? []).map((it, idx) => {
                                                    const type = it.type ?? it.Type ?? '';
                                                    const desc = it.description ?? it.Description ?? '';
                                                    const quote = it.quote ?? it.Quote ?? '';
                                                    return (
                                                        <div
                                                            key={idx}
                                                            className="rounded-xl border border-red-200 bg-amber-50 p-3"
                                                        >
                                                            <div className="text-sm text-amber-900">
                                                                <span style={{ fontWeight: 900 }}>Loại</span>: <span style={{ fontWeight: 900 }}>{policyTypeVi(type)}</span>
                                                            </div>
                                                            <div className="text-sm text-amber-900 mt-2" style={{ whiteSpace: 'pre-wrap' }}>
                                                                {desc || '—'}
                                                            </div>
                                                            {quote ? (
                                                                <div className="mt-2 text-xs text-amber-800 border border-amber-200 bg-amber-50 rounded-lg p-2" style={{ whiteSpace: 'pre-wrap' }}>
                                                                    {quote}
                                                                </div>
                                                            ) : (
                                                                <div className="text-xs text-amber-800 mt-2">
                                                                    Không có đoạn trích chứa từ cấm cho mục này.
                                                                </div>
                                                            )}
                                                        </div>
                                                    );
                                                })
                                            )}
                                        </div>
                                    )}
                                </>
                            )}
                        </div>

                        <div className="border-t border-slate-200 dark:border-slate-700 px-6 py-4 flex justify-end">
                            <button
                                type="button"
                                onClick={() => setChapterCheckModal((p) => ({ ...p, open: false }))}
                                className="min-w-28 px-4 py-2.5 bg-slate-100 dark:bg-slate-700 text-slate-800 dark:text-slate-100 text-sm font-semibold rounded-full hover:bg-slate-200 dark:hover:bg-slate-600 transition-colors"
                            >
                                Đóng
                            </button>
                        </div>
                    </div>
                </div>
            )}
            <Footer />
        </div>
    );
}
