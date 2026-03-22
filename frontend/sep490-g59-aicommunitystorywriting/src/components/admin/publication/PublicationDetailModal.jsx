import { useState, useEffect, useCallback, useRef, useMemo } from 'react';
import { X, CheckCircle, XCircle, BookOpen, FileText, Clock, User, Calendar, AlertTriangle } from 'lucide-react';
import { getChapters, getChapterById, getChapterRejectionReason } from '../../../api/chapter/chapterApi';
import { approveStory, approveChapter, rejectStory, rejectChapter, getChapterReviewContent, getPendingChapters, getModeratorChapterVersion, getReviewAssignmentSelf, submitReviewEscalation } from '../../../api/moderator/moderatorApi';
import { getSlaBadgeStyle, formatPolicySlaCountdown, normalizeTimeStatus, localDateTimeInputToIsoUtc, validateModeratorExtendProposedDeadline } from '../../../utils/moderatorReviewSla';
import { createModeratorHubConnection } from '../../../api/moderator/moderatorHub';
import { useToast } from '../../author/story-editor/Toast';

/** Map API chapter list item sang format modal cần. Khi API trả về pendingVersionTitle/pendingVersionWordCount (list chờ duyệt) thì dùng cho sidebar ngay. */
function mapChapterItem(item) {
    const orderIndex = item.orderIndex ?? item.OrderIndex ?? 0;
    return {
        id: item.id ?? item.Id,
        orderIndex,
        chapterNumber: orderIndex + 1,
        title: item.title ?? item.Title ?? '',
        content: null,
        wordCount: item.wordCount ?? item.WordCount ?? 0,
        status: (item.status ?? item.Status ?? '').toLowerCase(),
        publishedAt: item.publishedAt ?? item.PublishedAt ?? null,
        pendingVersionTitle: item.pendingVersionTitle ?? item.PendingVersionTitle ?? null,
        pendingVersionWordCount: item.pendingVersionWordCount ?? item.PendingVersionWordCount ?? null,
    };
}

/** Map chương từ story_group (tab Từ chối: publication.chapters) sang format modal */
function mapStoryGroupChapterToModal(ch) {
    const isVersion = !!(ch.isVersionHistory || ch.versionId);
    const orderIndex = ch.orderIndex ?? 0;
    return {
        // Với version history: id = versionId (để load detail version); vẫn giữ chapterId riêng.
        id: isVersion ? (ch.versionId ?? ch.id) : (ch.id ?? ch.chapterId),
        chapterId: ch.chapterId ?? ch.id ?? null,
        versionId: ch.versionId ?? null,
        versionNumber: ch.versionNumber ?? null,
        isVersionHistory: isVersion,
        orderIndex,
        chapterNumber: isVersion ? (orderIndex ? orderIndex + 1 : null) : (orderIndex + 1),
        title: ch.chapterTitle ?? '',
        titleSnapshot: ch.titleSnapshot ?? null,
        content: null,
        wordCount: ch.wordCount ?? 0,
        status: ch.status ?? 'rejected',
        publishedAt: null,
        rejectionReason: ch.rejectionReason ?? null,
        rejectedAt: ch.rejectedAt ?? null,
    };
}

export function PublicationDetailModal({ publication, onClose, onApprove, onReject, onRefresh }) {
    const { showToast, ToastContainer } = useToast();
    const [chapters, setChapters] = useState([]);
    const [chaptersLoading, setChaptersLoading] = useState(true);
    const [chapterContents, setChapterContents] = useState({});
    /** Khi có: moderator xem 2 phiên bản (bản gốc + version chờ duyệt) cho chapter đã PUBLISHED có version gửi chỉnh sửa. */
    const [chapterReviewContent, setChapterReviewContent] = useState({});
    const [selectedChapter, setSelectedChapter] = useState(null);
    const [showRejectForm, setShowRejectForm] = useState(false);
    const [showRejectConfirm, setShowRejectConfirm] = useState(false);
    const [showApproveConfirm, setShowApproveConfirm] = useState(false);
    const [rejectionReason, setRejectionReason] = useState('');
    const [isSubmitting, setIsSubmitting] = useState(false);
    /** Sau khi duyệt chương 1 đã gọi approveStory rồi thì không gọi lại khi duyệt chương 2, 3... (publication.status không đổi trong modal) */
    const storyApprovedInSessionRef = useRef(false);
    /** Vừa từ chối trong phiên này → không hiển thị khối "Đã từ chối xuất bản" / "Lý do từ chối" để moderator duyệt liên tiếp thoải mái */
    const justRejectedInSessionRef = useRef(false);
    /** Lý do từ chối lấy từ API GET /chapters/:id/rejection-reason khi chưa có trong danh sách (tab Từ chối). */
    const [fetchedRejectionByChapter, setFetchedRejectionByChapter] = useState({});
    /** Set orderIndex (0-based) các chương đã PUBLISHED — để chỉ cho phép duyệt/từ chối theo thứ tự 1,2,3... */
    const [publishedOrderIndices, setPublishedOrderIndices] = useState(new Set());
    /** Tab nội dung khi xem 2 phiên bản (gốc / version): 'original' | 'version' */
    const [contentTab, setContentTab] = useState('original');
    /** Chiều cao vùng đọc nội dung (px), có thể kéo để thay đổi. */
    const [contentAreaHeight, setContentAreaHeight] = useState(420);
    const [isResizingContent, setIsResizingContent] = useState(false);
    const resizeStartRef = useRef({ y: 0, height: 0 });
    /** Chỉ xóa chapterReviewContent khi mở publication khác; tránh effect re-run (vd publication.chapters ref mới) xóa mất dữ liệu prefetch. */
    const loadKeyRef = useRef({ storyId: null, pubId: null });
    /** Request id để bỏ qua kết quả prefetch cũ nếu đã mở publication khác. */
    const pendingPrefetchIdRef = useRef(0);

    /** Assignment / SLA / escalation (moderator đang nhận duyệt). */
    const [reviewAssignment, setReviewAssignment] = useState(null);
    const [slaTick, setSlaTick] = useState(0);
    const [escalateOpen, setEscalateOpen] = useState(false);
    const [escalateKind, setEscalateKind] = useState('EXTEND_DEADLINE');
    const [escalateReason, setEscalateReason] = useState('');
    const [escalateProposedDeadline, setEscalateProposedDeadline] = useState('');
    const [escalateSubmitting, setEscalateSubmitting] = useState(false);
    /** Đơn escalation gắn STORY (vd. trả cả truyện về hàng đợi) — tách khỏi assignment theo từng chương. */
    const [storyLevelReviewAssignment, setStoryLevelReviewAssignment] = useState(null);

    const storyId = publication?.storyId ?? publication?.story_id ?? publication?.id;

    const fetchChaptersForStory = useCallback((sid, options = {}) => {
        if (!sid) return;
        if (options.showLoading !== false) setChaptersLoading(true);
        const pubStatus = options.publicationStatus ?? 'pending';
        if (pubStatus === 'pending') {
            const prefetchId = ++pendingPrefetchIdRef.current;
            // Dùng API moderator để lấy đủ: chương PENDING_REVIEW + chương đã PUBLISHED có version chờ duyệt (hiển thị 2 tab Chương gốc / Phiên bản gửi duyệt).
            const pendingPromise = getPendingChapters({ storyId: sid, pageSize: 100 });
            const publishedPromise = getChapters({ storyId: sid, status: 'PUBLISHED', page: 1, pageSize: 500 });
            Promise.allSettled([pendingPromise, publishedPromise])
                .then(([pendingResult, publishedResult]) => {
                    if (pendingResult.status === 'fulfilled') {
                        const res = pendingResult.value;
                        const items = res?.items ?? res?.Items ?? res?.data ?? [];
                        const mapped = (Array.isArray(items) ? items : []).map(mapChapterItem);
                        if (mapped.length === 0) {
                            setChapters(mapped);
                            setSelectedChapter(null);
                        } else {
                            // "Click một loạt" tất cả chương (gọi review-content cho từng chương) trước khi hiển thị view → sidebar đúng version ngay (kể cả Chương 5).
                            return Promise.allSettled(mapped.map((c) => getChapterReviewContent(c.id)))
                                .then((results) => {
                                    if (prefetchId !== pendingPrefetchIdRef.current) return;
                                    const next = {};
                                    results.forEach((r, i) => {
                                        if (r.status === 'fulfilled' && mapped[i]) next[mapped[i].id] = r.value;
                                    });
                                    setChapterReviewContent((prev) => ({ ...prev, ...next }));
                                    setChapters(mapped);
                                    setSelectedChapter(mapped[0] ?? null);
                                });
                        }
                    }
                    if (publishedResult.status === 'fulfilled') {
                        const pubRes = publishedResult.value;
                        const pubList = pubRes?.items ?? pubRes?.Items ?? pubRes?.data ?? [];
                        const arr = Array.isArray(pubList) ? pubList : [];
                        setPublishedOrderIndices(new Set(arr.map((c) => Number(c.orderIndex ?? c.OrderIndex ?? 0))));
                    }
                })
                .catch(() => setChapters([]))
                .finally(() => setChaptersLoading(false));
        } else {
            const params = { storyId: sid, pageSize: 100 };
            if (pubStatus === 'approved') params.status = 'PUBLISHED';
            const promise = getChapters(params);
            promise
                .then((res) => {
                    const items = res?.items ?? res?.Items ?? [];
                    const mapped = items.map(mapChapterItem);
                    setChapters(mapped);
                    setSelectedChapter((prev) => (prev && mapped.some((c) => c.id === prev.id)) ? prev : (mapped[0] ?? null));
                })
                .catch(() => setChapters([]))
                .finally(() => setChaptersLoading(false));
        }
    }, []);

    useEffect(() => {
        const pubId = publication?.id ?? publication?.storyId ?? storyId ?? '';
        const newKey = { storyId: storyId ?? null, pubId: pubId ?? null };
        const keyChanged = loadKeyRef.current.storyId !== newKey.storyId || loadKeyRef.current.pubId !== newKey.pubId;

        const id = setTimeout(() => {
            if (!storyId) {
                loadKeyRef.current = { storyId: null, pubId: null };
                setChapters([]);
                setChaptersLoading(false);
                setSelectedChapter(null);
                setChapterReviewContent({});
                return;
            }
            // Chỉ clear và refetch khi mở publication khác; tránh effect re-run (vd publication.chapters ref mới) xóa chapters + chapterReviewContent → sidebar hiển thị sai.
            if (!keyChanged) return;

            // Cập nhật ref ngay khi bắt đầu load (trong setTimeout), tránh effect chạy lần 2 trước khi fetch kịp gọi → keyChanged false → bỏ qua fetch → không có dữ liệu.
            loadKeyRef.current = newKey;
            storyApprovedInSessionRef.current = false;
            justRejectedInSessionRef.current = false;
            setFetchedRejectionByChapter({});
            setPublishedOrderIndices(new Set());
            setChapters([]);
            setSelectedChapter(null);
            setChapterContents({});
            setChapterReviewContent({});
            // Tab Đã duyệt / Từ chối: item là story_group có sẵn danh sách chương (đã duyệt hoặc bị từ chối) — chỉ hiển thị các chương đó, không gọi API lấy hết chương.
            if (publication?.type === 'story_group' && Array.isArray(publication?.chapters) && publication.chapters.length > 0) {
                const mapped = publication.chapters
                    .map(mapStoryGroupChapterToModal)
                    // Sidebar lịch sử từ chối: mới nhất → cũ nhất theo thời điểm bị từ chối
                    .sort((a, b) => {
                        const ta = a?.rejectedAt ? new Date(a.rejectedAt).getTime() : 0;
                        const tb = b?.rejectedAt ? new Date(b.rejectedAt).getTime() : 0;
                        return tb - ta;
                    });
                setChapters(mapped);
                setSelectedChapter(mapped[0] ?? null);
                setChaptersLoading(false);
                return;
            }
            fetchChaptersForStory(storyId, { publicationStatus: publication?.status });
        }, 0);
        return () => clearTimeout(id);
    }, [storyId, publication?.status, publication?.type, publication?.chapters, publication?.id, publication?.storyId, fetchChaptersForStory]);

    /** Real-time: khi có claim/approve/reject, refetch danh sách chương trong modal (bỏ qua khi đang xem story_group từ tab Từ chối). */
    const refetchChaptersRef = useRef(() => { });
    refetchChaptersRef.current = () => {
        if (!storyId) return;
        if (publication?.type === 'story_group' && publication?.chapters?.length > 0) return;
        fetchChaptersForStory(storyId, { showLoading: false, publicationStatus: publication?.status });
    };
    useEffect(() => {
        if (!storyId) return;
        const { stop } = createModeratorHubConnection(() => refetchChaptersRef.current());
        return () => { stop(); };
    }, [storyId]);

    const loadChapterContent = useCallback(async (item) => {
        const key = item?.id;
        if (!key) return;
        // Lịch sử version bị từ chối: load content snapshot từ API moderator/chapters/{chapterId}/versions/{versionId}
        if (item?.isVersionHistory && item?.chapterId && item?.versionId) {
            try {
                const data = await getModeratorChapterVersion(item.chapterId, item.versionId);
                const content = data?.contentSnapshot ?? data?.ContentSnapshot ?? '';
                setChapterReviewContent((prev) => ({ ...prev, [key]: null }));
                setChapterContents((prev) => ({ ...prev, [key]: content || '(Không có nội dung phiên bản)' }));
            } catch {
                setChapterReviewContent((prev) => ({ ...prev, [key]: null }));
                setChapterContents((prev) => ({ ...prev, [key]: '(Không tải được nội dung phiên bản)' }));
            }
            return;
        }

        const chapterId = item?.id;
        try {
            const data = await getChapterReviewContent(chapterId);
            setChapterReviewContent((prev) => ({ ...prev, [chapterId]: data }));
            setChapterContents((prev) => ({
                ...prev,
                [chapterId]: data?.originalContent ?? data?.OriginalContent ?? '',
            }));
        } catch {
            try {
                const data = await getChapterById(chapterId);
                setChapterReviewContent((prev) => ({ ...prev, [chapterId]: null }));
                setChapterContents((prev) => ({
                    ...prev,
                    [chapterId]: data?.content ?? data?.Content ?? '',
                }));
            } catch {
                setChapterReviewContent((prev) => ({ ...prev, [chapterId]: null }));
                setChapterContents((prev) => ({ ...prev, [chapterId]: '(Không tải được nội dung)' }));
            }
        }
    }, []);

    useEffect(() => {
        const id = setTimeout(() => {
            if (selectedChapter?.id) loadChapterContent(selectedChapter);
        }, 0);
        return () => clearTimeout(id);
    }, [selectedChapter, loadChapterContent]);

    useEffect(() => {
        setContentTab('original');
    }, [selectedChapter?.id]);

    useEffect(() => {
        if (publication?.status !== 'pending') return undefined;
        const id = setInterval(() => setSlaTick((t) => t + 1), 30000);
        return () => clearInterval(id);
    }, [publication?.status]);

    useEffect(() => {
        if (publication?.status !== 'pending') {
            setReviewAssignment(null);
            return;
        }
        if (chaptersLoading) return;

        let cancelled = false;
        (async () => {
            try {
                if (selectedChapter && !selectedChapter.isVersionHistory) {
                    const dto = await getReviewAssignmentSelf('CHAPTER', selectedChapter.id);
                    if (!cancelled) setReviewAssignment(dto);
                    return;
                }
                if (chapters.length === 0 && storyId) {
                    const dto = await getReviewAssignmentSelf('STORY', storyId);
                    if (!cancelled) setReviewAssignment(dto);
                    return;
                }
                if (!cancelled) setReviewAssignment(null);
            } catch {
                if (!cancelled) setReviewAssignment(null);
            }
        })();
        return () => { cancelled = true; };
    }, [publication?.status, chaptersLoading, selectedChapter, chapters.length, storyId]);

    /** Khối nhiều chương: đơn PENDING cấp STORY (RELEASE / gia hạn truyện) vẫn phải chặn duyệt/từ chối chương. */
    useEffect(() => {
        if (publication?.status !== 'pending' || !storyId) {
            setStoryLevelReviewAssignment(null);
            return undefined;
        }
        const isMultiChapterContext =
            publication?.type === 'story_group'
            || (Array.isArray(publication?.chapters) && publication.chapters.length > 0)
            || chapters.length > 0;
        if (!isMultiChapterContext) {
            setStoryLevelReviewAssignment(null);
            return undefined;
        }
        let cancelled = false;
        (async () => {
            try {
                const dto = await getReviewAssignmentSelf('STORY', storyId);
                if (!cancelled) setStoryLevelReviewAssignment(dto);
            } catch {
                if (!cancelled) setStoryLevelReviewAssignment(null);
            }
        })();
        return () => { cancelled = true; };
    }, [publication?.status, publication?.type, publication?.chapters?.length, storyId, chapters.length]); // eslint-disable-line react-hooks/exhaustive-deps -- tránh dependency publication.chapters (ref)

    const escalationTarget = () => {
        if (selectedChapter && !selectedChapter.isVersionHistory) {
            return { targetType: 'CHAPTER', targetId: selectedChapter.id };
        }
        if (storyId) return { targetType: 'STORY', targetId: storyId };
        return null;
    };

    /** Lỗi hạn đề xuất (gia hạn): hiển thị đỏ trong dialog khi vi phạm 24h / muộn hơn hạn hiện tại / quá 366 ngày. */
    const extendProposedDeadlineError = useMemo(() => {
        if (!escalateOpen || escalateKind !== 'EXTEND_DEADLINE') return null;
        const raw = escalateProposedDeadline;
        if (raw == null || String(raw).trim() === '') return null;
        const iso = localDateTimeInputToIsoUtc(raw);
        if (!iso) return 'Ngày giờ đề xuất không hợp lệ.';
        const currentDl = reviewAssignment?.reviewDeadlineAt ?? reviewAssignment?.ReviewDeadlineAt ?? null;
        const check = validateModeratorExtendProposedDeadline(iso, currentDl);
        return check.ok ? null : check.message;
    }, [escalateOpen, escalateKind, escalateProposedDeadline, reviewAssignment]);

    const handleSubmitEscalation = async () => {
        const t = escalationTarget();
        if (!t || !escalateReason.trim()) {
            showToast('Vui lòng nhập lý do.', 'error');
            return;
        }
        if (escalateReason.trim().length < 10) {
            showToast('Lý do báo cáo cần ít nhất 10 ký tự (theo quy định hệ thống).', 'error');
            return;
        }
        if (escalateKind === 'EXTEND_DEADLINE') {
            const iso = localDateTimeInputToIsoUtc(escalateProposedDeadline);
            if (!iso) {
                showToast('Vui lòng chọn hạn đề xuất (gia hạn).', 'error');
                return;
            }
            const currentDl = reviewAssignment?.reviewDeadlineAt ?? reviewAssignment?.ReviewDeadlineAt ?? null;
            const check = validateModeratorExtendProposedDeadline(iso, currentDl);
            if (!check.ok) {
                showToast(check.message, 'error');
                return;
            }
        }
        setEscalateSubmitting(true);
        try {
            await submitReviewEscalation({
                targetType: t.targetType,
                targetId: t.targetId,
                requestKind: escalateKind,
                reason: escalateReason.trim(),
                proposedDeadlineAt: escalateKind === 'EXTEND_DEADLINE' ? localDateTimeInputToIsoUtc(escalateProposedDeadline) : null,
            });
            showToast('Đã gửi đơn lên quản trị.', 'success');
            setEscalateOpen(false);
            setEscalateReason('');
            setEscalateProposedDeadline('');
            const dto = await getReviewAssignmentSelf(t.targetType, t.targetId);
            setReviewAssignment(dto);
            onRefresh?.();
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? 'Gửi đơn thất bại.';
            showToast(msg, 'error');
        } finally {
            setEscalateSubmitting(false);
        }
    };

    const startResize = useCallback((e) => {
        e.preventDefault();
        e.stopPropagation();
        resizeStartRef.current = { y: e.clientY, height: contentAreaHeight };
        setIsResizingContent(true);
        document.body.style.cursor = 'ns-resize';
        document.body.style.userSelect = 'none';

        const minH = 300;
        const maxH = Math.round(window.innerHeight * 0.85);
        const onMove = (ev) => {
            const { y, height } = resizeStartRef.current;
            const delta = ev.clientY - y;
            const next = Math.min(maxH, Math.max(minH, height + delta));
            setContentAreaHeight(next);
            resizeStartRef.current = { y: ev.clientY, height: next };
        };
        const onUp = () => {
            setIsResizingContent(false);
            document.body.style.cursor = '';
            document.body.style.userSelect = '';
            document.removeEventListener('mousemove', onMove);
            document.removeEventListener('mouseup', onUp);
        };
        document.addEventListener('mousemove', onMove);
        document.addEventListener('mouseup', onUp);
    }, [contentAreaHeight]);

    /** Tab Từ chối: nếu chương chưa có rejectionReason (từ list API) thì gọi GET /chapters/:id/rejection-reason để hiển thị lý do. */
    useEffect(() => {
        if (publication?.status !== 'rejected' || !selectedChapter?.id) return;
        if (selectedChapter?.isVersionHistory) return;
        if (selectedChapter.rejectionReason) return;
        getChapterRejectionReason(selectedChapter.id)
            .then((data) => {
                const reason = data?.reason ?? data?.Reason ?? null;
                const rejectedAt = data?.rejectedAt ?? data?.RejectedAt ?? null;
                if (reason != null || rejectedAt != null) {
                    setFetchedRejectionByChapter((prev) => ({
                        ...prev,
                        [selectedChapter.id]: { reason: reason ?? '', rejectedAt }
                    }));
                }
            })
            .catch(() => { });
    }, [publication?.status, selectedChapter?.id, selectedChapter?.rejectionReason, selectedChapter?.isVersionHistory]);

    const openApproveConfirm = () => {
        if (selectedChapter) setShowApproveConfirm(true);
    };

    const handleApproveConfirm = async () => {
        if (!selectedChapter) return;
        setShowApproveConfirm(false);
        setIsSubmitting(true);
        try {
            // Gọi approveStory khi chưa duyệt truyện trong phiên (để set story PUBLISHED). Bắt 404 để không chặn duyệt chương khi truyện đã PUBLISHED.
            const needApproveStory = publication.status !== 'approved' && !storyApprovedInSessionRef.current;
            if (needApproveStory) {
                try {
                    await approveStory(storyId);
                } catch (err) {
                    if (err?.response?.status === 404) {
                        storyApprovedInSessionRef.current = true;
                    } else {
                        throw err;
                    }
                }
                storyApprovedInSessionRef.current = true;
            }
            await approveChapter(selectedChapter.id);
            showToast('Duyệt chương thành công!', 'success');
            setPublishedOrderIndices((prev) => new Set([...prev, selectedChapter.orderIndex ?? (selectedChapter.chapterNumber - 1)]));
            const remaining = chapters.filter(c => c.id !== selectedChapter.id);
            setChapters(remaining);
            setSelectedChapter(remaining[0] ?? null);
            setChapterContents((prev) => {
                const next = { ...prev };
                delete next[selectedChapter.id];
                return next;
            });
            setChapterReviewContent((prev) => {
                const next = { ...prev };
                delete next[selectedChapter.id];
                return next;
            });
            onRefresh?.();
            if (remaining.length === 0) {
                onApprove(publication.id);
            }
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? 'Không thể duyệt xuất bản. Vui lòng thử lại.';
            console.error('[PublicationDetailModal] Duyệt xuất bản thất bại:', msg, err?.response?.data ?? err);
            showToast(msg, 'error');
        } finally {
            setIsSubmitting(false);
        }
    };

    const openRejectConfirm = () => {
        if (!rejectionReason.trim()) {
            showToast('Vui lòng nhập lý do từ chối', 'error');
            return;
        }
        setShowRejectConfirm(true);
    };

    const handleRejectSubmit = async () => {
        setShowRejectConfirm(false);
        if (!rejectionReason.trim()) return;
        setIsSubmitting(true);
        try {
            if (selectedChapter) {
                await rejectChapter(selectedChapter.id, rejectionReason.trim());
                showToast('Đã từ chối chương.', 'success');
                const remaining = chapters.filter(c => c.id !== selectedChapter.id);
                setChapters(remaining);
                setSelectedChapter(remaining[0] ?? null);
                setChapterContents((prev) => {
                    const next = { ...prev };
                    delete next[selectedChapter.id];
                    return next;
                });
                setChapterReviewContent((prev) => {
                    const next = { ...prev };
                    delete next[selectedChapter.id];
                    return next;
                });
                onRefresh?.();
                // Gọi rejectStory khi không còn chương chờ duyệt. Bắt 404 (truyện đã PUBLISHED sau khi duyệt chương trước) để vẫn đóng form và không hiện toast lỗi.
                const isStoryRow = publication.type === 'story' || publication.type === 'new_story';
                if (remaining.length === 0 && isStoryRow && publication.status !== 'approved') {
                    try {
                        await rejectStory(storyId, rejectionReason.trim());
                        onReject(publication.id);
                    } catch (rejectErr) {
                        if (rejectErr?.response?.status === 404) {
                            onRefresh?.();
                        } else {
                            throw rejectErr;
                        }
                    }
                }
                if (remaining.length === 0) {
                    onClose?.();
                }
            } else {
                if (publication.type === 'story' || publication.type === 'new_story') {
                    try {
                        await rejectStory(storyId, rejectionReason.trim());
                        showToast('Đã từ chối truyện.', 'success');
                        onReject(publication.id);
                        onRefresh?.();
                    } catch (rejectErr) {
                        if (rejectErr?.response?.status === 404) {
                            showToast('Truyện không còn ở trạng thái chờ duyệt.', 'info');
                        } else {
                            throw rejectErr;
                        }
                    }
                } else {
                    showToast('Truyện không còn ở trạng thái chờ duyệt.', 'info');
                }
                onClose?.();
            }
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? 'Không thể từ chối. Vui lòng thử lại.';
            console.error('[PublicationDetailModal] Từ chối thất bại:', msg, err?.response?.data ?? err);
            showToast(msg, 'error');
        } finally {
            setShowRejectForm(false);
            setRejectionReason('');
            justRejectedInSessionRef.current = true;
            setIsSubmitting(false);
        }
    };

    const formatDate = (dateString) => {
        if (!dateString) return '-';
        const date = new Date(dateString);
        return date.toLocaleString('vi-VN', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit'
        });
    };

    const selectedOrderIndex = Number(selectedChapter?.orderIndex ?? (selectedChapter?.chapterNumber != null ? selectedChapter.chapterNumber - 1 : -1));
    const minOrderInList = chapters.length > 0 ? Math.min(...chapters.map((c) => Number(c.orderIndex ?? (c.chapterNumber != null ? c.chapterNumber - 1 : 0)))) : -1;
    const isFirstInPendingQueue = selectedOrderIndex >= 0 && minOrderInList >= 0 && selectedOrderIndex === minOrderInList;
    const ra = reviewAssignment ?? {};
    const sra = storyLevelReviewAssignment ?? {};
    const hasPendingEscalationBlock = Boolean(
        (ra.hasPendingEscalation ?? ra.HasPendingEscalation)
        || (sra.hasPendingEscalation ?? sra.HasPendingEscalation),
    );
    const baseCanApproveReject = selectedChapter && !selectedChapter?.isVersionHistory && (
        selectedOrderIndex === 0
        || publishedOrderIndices.has(Number(selectedOrderIndex - 1))
        || (minOrderInList >= 1 && isFirstInPendingQueue)
    );
    const canApproveReject = baseCanApproveReject && !hasPendingEscalationBlock;
    const orderHint = !baseCanApproveReject && selectedChapter
        ? `Phải duyệt hoặc từ chối chương ${selectedOrderIndex} trước khi xử lý chương ${selectedOrderIndex + 1}.`
        : hasPendingEscalationBlock
            ? 'Đã gửi đơn lên quản trị viên (theo truyện hoặc theo chương) — chờ xử lý xong mới được duyệt / từ chối.'
            : '';

    const authorSubmittedForSla = ra.authorSubmittedAtUtc ?? ra.AuthorSubmittedAtUtc;
    void slaTick;
    const policySlaLine = authorSubmittedForSla ? formatPolicySlaCountdown(authorSubmittedForSla).line : null;

    return (
        <>
            <ToastContainer />
            <div
                style={{
                    position: 'fixed',
                    top: 0,
                    left: 0,
                    right: 0,
                    bottom: 0,
                    backgroundColor: 'rgba(0, 0, 0, 0.5)',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center',
                    zIndex: 9999,
                    padding: '1rem'
                }}
                onClick={onClose}
            >
                <div
                    style={{
                        backgroundColor: '#ffffff',
                        borderRadius: '16px',
                        maxWidth: 'min(1400px, 96vw)',
                        width: '100%',
                        maxHeight: '94vh',
                        minHeight: '80vh',
                        display: 'flex',
                        flexDirection: 'column',
                        overflow: 'hidden',
                        boxShadow: '0 20px 60px rgba(0, 0, 0, 0.3)'
                    }}
                    onClick={(e) => e.stopPropagation()}
                >
                    {/* Header */}
                    <div style={{
                        padding: '1.5rem',
                        borderBottom: '1px solid #e2e8f0',
                        display: 'flex',
                        justifyContent: 'space-between',
                        alignItems: 'start',
                        gap: '1rem'
                    }}>
                        <div style={{ flex: 1, minWidth: 0 }}>
                            <h2 style={{
                                fontSize: '1.5rem',
                                fontWeight: 700,
                                color: '#1e293b',
                                margin: 0,
                                marginBottom: '0.5rem'
                            }}>
                                {publication.storyTitle}
                            </h2>

                            <div style={{ display: 'flex', alignItems: 'center', gap: '1.5rem', fontSize: '0.875rem', color: '#64748b', flexWrap: 'wrap' }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                                    <User style={{ width: '14px', height: '14px' }} />
                                    <span>{publication.author ?? '—'}</span>
                                </div>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                                    <span>Độ tuổi phù hợp: {publication.ageRating ? (({ ALL: 'Phù hợp mọi lứa tuổi', '13+': 'Từ 13 tuổi', '16+': 'Từ 16 tuổi', '18+': 'Từ 18 tuổi' })[String(publication.ageRating).toUpperCase()] ?? publication.ageRating) : '—'}</span>
                                </div>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem' }}>
                                    <Calendar style={{ width: '14px', height: '14px' }} />
                                    <span>{formatDate(publication.submittedAt)}</span>
                                </div>
                                <div>
                                    {publication.totalChapters != null ? `${publication.totalChapters} chương` : null}
                                </div>
                                {publication.claimedByDisplayName && (
                                    <span style={{
                                        padding: '0.25rem 0.5rem',
                                        backgroundColor: publication.isClaimedByMe ? '#d1fae5' : '#f1f5f9',
                                        color: publication.isClaimedByMe ? '#065f46' : '#64748b',
                                        borderRadius: '9999px',
                                        fontSize: '0.75rem',
                                        fontWeight: 600
                                    }}>
                                        {publication.isClaimedByMe ? 'Đã nhận bởi bạn' : `Đã nhận: ${publication.claimedByDisplayName}`}
                                    </span>
                                )}
                            </div>
                        </div>

                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', flexShrink: 0 }}>
                            <button
                                onClick={onClose}
                                style={{
                                    padding: '0.5rem',
                                    backgroundColor: 'transparent',
                                    border: 'none',
                                    cursor: 'pointer',
                                    borderRadius: '0.5rem',
                                    transition: 'background-color 0.2s',
                                    flexShrink: 0
                                }}
                                onMouseEnter={(e) => e.currentTarget.style.backgroundColor = '#f8fafc'}
                                onMouseLeave={(e) => e.currentTarget.style.backgroundColor = 'transparent'}
                            >
                                <X style={{ width: '24px', height: '24px', color: '#64748b' }} />
                            </button>
                        </div>
                    </div>

                    {publication?.status === 'pending' && hasPendingEscalationBlock && (
                        <div style={{
                            padding: '0.75rem 1.5rem',
                            backgroundColor: '#fef2f2',
                            borderBottom: '1px solid #fecaca',
                            fontSize: '0.875rem',
                            color: '#991b1b',
                            fontWeight: 600,
                        }}>
                            Bạn đã gửi đơn lên quản trị viên (gia hạn, trả truyện về hàng đợi, hoặc báo cáo theo chương) — đơn đang chờ xử lý.
                            {' '}Thao tác <strong>duyệt</strong> và <strong>từ chối</strong> chương bị khóa cho đến khi quản trị viên xử lý xong.
                        </div>
                    )}

                    {publication?.status === 'pending' && reviewAssignment && (reviewAssignment.isAssignedToMe ?? reviewAssignment.IsAssignedToMe) && (() => {
                        const rd = reviewAssignment.reviewDeadlineAt ?? reviewAssignment.ReviewDeadlineAt;
                        const ts = normalizeTimeStatus(reviewAssignment.timeStatus ?? reviewAssignment.TimeStatus);
                        const badge = ts ? getSlaBadgeStyle(ts) : null;
                        return (
                            <div style={{
                                padding: '0.75rem 1.5rem',
                                backgroundColor: '#f0f9ff',
                                borderBottom: '1px solid #bae6fd',
                                display: 'flex',
                                flexWrap: 'wrap',
                                alignItems: 'center',
                                gap: '0.75rem',
                                justifyContent: 'space-between',
                            }}
                            >
                                <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '0.5rem', flex: 1, minWidth: 0 }}>
                                    {badge && (
                                        <span style={{
                                            fontSize: '0.75rem',
                                            fontWeight: 700,
                                            padding: '0.25rem 0.5rem',
                                            borderRadius: '9999px',
                                            backgroundColor: badge.bg,
                                            color: badge.color,
                                        }}>
                                            {badge.label}
                                        </span>
                                    )}
                                    <span style={{ fontSize: '0.8125rem', color: '#0c4a6e' }}>
                                        {policySlaLine ? <>{policySlaLine}</> : null}
                                        {rd ? (
                                            <> {' '}• Hạn duyệt (bạn đã chọn): {formatDate(rd)}</>
                                        ) : null}
                                    </span>
                                </div>
                                {!hasPendingEscalationBlock && escalationTarget() && (
                                    <button
                                        type="button"
                                        onClick={() => {
                                            setEscalateKind('EXTEND_DEADLINE');
                                            setEscalateOpen(true);
                                        }}
                                        style={{
                                            display: 'inline-flex',
                                            alignItems: 'center',
                                            gap: '0.35rem',
                                            padding: '0.45rem 0.85rem',
                                            fontSize: '0.8125rem',
                                            fontWeight: 600,
                                            backgroundColor: '#fff',
                                            color: '#0369a1',
                                            border: '1px solid #7dd3fc',
                                            borderRadius: '8px',
                                            cursor: 'pointer',
                                        }}
                                    >
                                        <AlertTriangle style={{ width: '14px', height: '14px' }} />
                                        Báo cáo quản trị viên
                                    </button>
                                )}
                            </div>
                        );
                    })()}

                    {/* Body — overflow: auto để khi kéo cao vùng đọc, có thể cuộn xem hết */}
                    <div style={{
                        display: 'flex',
                        flex: 1,
                        minHeight: 0,
                        overflow: 'auto'
                    }}>
                        {chaptersLoading ? (
                            <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '3rem' }}>
                                <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>
                                    {publication?.status === 'approved' ? 'Đang tải danh sách chương đã xuất bản...' : publication?.status === 'rejected' ? 'Đang tải danh sách chương...' : 'Đang tải danh sách chương chờ duyệt...'}
                                </p>
                            </div>
                        ) : chapters.length === 0 ? (
                            <div style={{ flex: 1, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: '3rem' }}>
                                <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>
                                    {publication?.status === 'approved' ? 'Không có chương nào đã xuất bản' : publication?.status === 'rejected' ? 'Không có chương nào' : 'Không có chương nào đang chờ kiểm duyệt'}
                                </p>
                            </div>
                        ) : (
                            <>
                                {/* Sidebar - Chapter List */}
                                {chapters.length >= 1 && (
                                    <div style={{
                                        width: '280px',
                                        borderRight: '1px solid #e2e8f0',
                                        display: 'flex',
                                        flexDirection: 'column',
                                        backgroundColor: '#f8fafc'
                                    }}>
                                        <div style={{ padding: '1rem', borderBottom: '1px solid #e2e8f0' }}>
                                            <h3 style={{ fontSize: '0.875rem', fontWeight: 600, color: '#64748b', margin: 0, textTransform: 'uppercase' }}>
                                                {publication?.status === 'approved' ? 'Chương đã xuất bản' : publication?.status === 'rejected' ? 'Chương' : 'Chương chờ duyệt'}
                                            </h3>
                                        </div>
                                        <div style={{ flex: 1, overflowY: 'auto', padding: '0.5rem' }}>
                                            {chapters.map(chapter => (
                                                <button
                                                    key={chapter.id}
                                                    onClick={() => setSelectedChapter(chapter)}
                                                    style={{
                                                        width: '100%',
                                                        padding: '0.75rem',
                                                        marginBottom: '0.5rem',
                                                        textAlign: 'left',
                                                        backgroundColor: selectedChapter?.id === chapter.id ? '#ffffff' : 'transparent',
                                                        border: selectedChapter?.id === chapter.id ? '2px solid #13ec5b' : '1px solid #e2e8f0',
                                                        borderRadius: '8px',
                                                        cursor: 'pointer',
                                                        transition: 'all 0.2s'
                                                    }}
                                                    onMouseEnter={(e) => {
                                                        if (selectedChapter?.id !== chapter.id) {
                                                            e.currentTarget.style.backgroundColor = '#ffffff';
                                                        }
                                                    }}
                                                    onMouseLeave={(e) => {
                                                        if (selectedChapter?.id !== chapter.id) {
                                                            e.currentTarget.style.backgroundColor = 'transparent';
                                                        }
                                                    }}
                                                >
                                                    <div style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.25rem' }}>
                                                        Chương {chapter.chapterNumber}
                                                    </div>
                                                    <div style={{
                                                        fontSize: '0.875rem',
                                                        fontWeight: 600,
                                                        color: '#1e293b',
                                                        overflow: 'hidden',
                                                        textOverflow: 'ellipsis',
                                                        whiteSpace: 'nowrap'
                                                    }}>
                                                        {(() => {
                                                            if (chapter.displayTitle != null && String(chapter.displayTitle).trim()) return String(chapter.displayTitle).trim();
                                                            const fromList = chapter.pendingVersionTitle ?? '';
                                                            if (fromList && String(fromList).trim()) return String(fromList).trim();
                                                            const review = chapterReviewContent[chapter.id];
                                                            const hasPending = review?.hasPendingVersion ?? review?.HasPendingVersion;
                                                            const pendingVersions = review?.pendingVersions ?? review?.PendingVersions ?? [];
                                                            if (hasPending && pendingVersions?.length > 0) {
                                                                const t = pendingVersions[0]?.titleSnapshot ?? pendingVersions[0]?.TitleSnapshot ?? '';
                                                                return (t && t.trim()) ? t.trim() : (chapter.title ?? '');
                                                            }
                                                            return chapter.title ?? '';
                                                        })()}
                                                    </div>
                                                    <div style={{ fontSize: '0.75rem', color: '#64748b', marginTop: '0.25rem' }}>
                                                        {(() => {
                                                            if (chapter.displayWordCount != null && typeof chapter.displayWordCount === 'number' && chapter.displayWordCount >= 0) return `${chapter.displayWordCount} từ`;
                                                            const countFromList = chapter.pendingVersionWordCount;
                                                            if (countFromList != null && typeof countFromList === 'number' && countFromList >= 0) return `${countFromList} từ`;
                                                            const review = chapterReviewContent[chapter.id];
                                                            const hasPending = review?.hasPendingVersion ?? review?.HasPendingVersion;
                                                            const pendingVersions = review?.pendingVersions ?? review?.PendingVersions ?? [];
                                                            if (hasPending && pendingVersions?.length > 0) {
                                                                const content = pendingVersions[0]?.contentSnapshot ?? pendingVersions[0]?.ContentSnapshot ?? '';
                                                                const count = (typeof content === 'string' && content.trim()) ? content.trim().split(/\s+/).length : 0;
                                                                return `${count} từ`;
                                                            }
                                                            return `${chapter.wordCount ?? 0} từ`;
                                                        })()}
                                                    </div>
                                                    {publication?.status === 'approved' && chapter.publishedAt && (
                                                        <div style={{ fontSize: '0.6875rem', color: '#10b981', marginTop: '0.25rem' }}>
                                                            Duyệt: {formatDate(chapter.publishedAt)}
                                                        </div>
                                                    )}
                                                </button>
                                            ))}
                                        </div>
                                    </div>
                                )}

                                {/* Main Content — minWidth 0 để flex shrink; nội dung bên trong có thể cao hơn, body sẽ cuộn */}
                                <div style={{ flex: 1, display: 'flex', flexDirection: 'column', minWidth: 0, minHeight: 0 }}>
                                    {selectedChapter ? (
                                        <>
                                            <div style={{
                                                padding: '1.5rem',
                                                borderBottom: '1px solid #e2e8f0',
                                                backgroundColor: '#f8fafc'
                                            }}>
                                                <div style={{ fontSize: '0.75rem', color: '#64748b', marginBottom: '0.25rem' }}>
                                                    CHƯƠNG {selectedChapter.chapterNumber}
                                                </div>
                                                <h3 style={{ fontSize: '1.25rem', fontWeight: 700, color: '#1e293b', margin: 0, marginBottom: '0.5rem' }}>
                                                    {(() => {
                                                        const review = chapterReviewContent[selectedChapter.id];
                                                        const hasPending = review?.hasPendingVersion ?? review?.HasPendingVersion;
                                                        const pendingVersions = review?.pendingVersions ?? review?.PendingVersions ?? [];
                                                        const chapterIsPublished = (selectedChapter?.status ?? '').toLowerCase() === 'published';
                                                        if (!chapterIsPublished && hasPending && pendingVersions?.length > 0) {
                                                            const t = pendingVersions[0]?.titleSnapshot ?? pendingVersions[0]?.TitleSnapshot ?? '';
                                                            return t.trim() || selectedChapter.title;
                                                        }
                                                        return selectedChapter.title;
                                                    })()}
                                                </h3>
                                                <div style={{ fontSize: '0.875rem', color: '#64748b' }}>
                                                    {(() => {
                                                        const review = chapterReviewContent[selectedChapter.id];
                                                        const hasPending = review?.hasPendingVersion ?? review?.HasPendingVersion;
                                                        const pendingVersions = review?.pendingVersions ?? review?.PendingVersions ?? [];
                                                        const chapterIsPublished = (selectedChapter?.status ?? '').toLowerCase() === 'published';
                                                        if (!chapterIsPublished && hasPending && pendingVersions?.length > 0) {
                                                            const content = pendingVersions[0]?.contentSnapshot ?? pendingVersions[0]?.ContentSnapshot ?? '';
                                                            const count = (typeof content === 'string' && content.trim()) ? content.trim().split(/\s+/).length : 0;
                                                            return `${count} từ`;
                                                        }
                                                        return `${selectedChapter.wordCount ?? 0} từ`;
                                                    })()}
                                                </div>
                                                {publication?.status === 'approved' && selectedChapter.publishedAt && (
                                                    <div style={{ fontSize: '0.8125rem', color: '#10b981', marginTop: '0.375rem' }}>
                                                        Duyệt lúc: {formatDate(selectedChapter.publishedAt)}
                                                    </div>
                                                )}
                                                {publication?.status === 'rejected' && (() => {
                                                    const displayReason = selectedChapter?.rejectionReason ?? fetchedRejectionByChapter[selectedChapter?.id]?.reason;
                                                    const displayRejectedAt = selectedChapter?.rejectedAt ?? fetchedRejectionByChapter[selectedChapter?.id]?.rejectedAt;
                                                    if (!displayReason && !displayRejectedAt) return null;
                                                    return (
                                                        <div style={{
                                                            marginTop: '1rem',
                                                            padding: '0.75rem 1rem',
                                                            backgroundColor: '#fef2f2',
                                                            borderLeft: '4px solid #ef4444',
                                                            borderRadius: '0.5rem'
                                                        }}>
                                                            <div style={{ fontSize: '0.75rem', fontWeight: 600, color: '#991b1b', marginBottom: '0.25rem' }}>
                                                                Lý do từ chối (đã nhập trước đó):
                                                            </div>
                                                            {displayReason && (
                                                                <div style={{ fontSize: '0.875rem', color: '#991b1b', whiteSpace: 'pre-wrap' }}>
                                                                    {displayReason}
                                                                </div>
                                                            )}
                                                            {displayRejectedAt && (
                                                                <div style={{ fontSize: '0.75rem', color: '#b91c1c', marginTop: displayReason ? '0.5rem' : 0 }}>
                                                                    Từ chối lúc: {formatDate(displayRejectedAt)}
                                                                </div>
                                                            )}
                                                        </div>
                                                    );
                                                })()}
                                                {(() => {
                                                    const hasPending = publication?.isEditRequest || chapterReviewContent[selectedChapter.id]?.hasPendingVersion || chapterReviewContent[selectedChapter.id]?.HasPendingVersion;
                                                    const chapterIsPublished = (selectedChapter?.status ?? '').toLowerCase() === 'published';
                                                    if (!hasPending) return null;
                                                    if (!chapterIsPublished && !publication?.isEditRequest) return null;
                                                    return (
                                                        <div style={{
                                                            marginTop: '1rem',
                                                            padding: '0.75rem 1rem',
                                                            backgroundColor: '#fef3c7',
                                                            borderLeft: '4px solid #f59e0b',
                                                            borderRadius: '0.5rem'
                                                        }}>
                                                            <div style={{ fontSize: '0.75rem', fontWeight: 700, color: '#92400e', marginBottom: '0.25rem' }}>
                                                                Yêu cầu chỉnh sửa (chương đã xuất bản)
                                                            </div>
                                                            <div style={{ fontSize: '0.8125rem', color: '#92400e', lineHeight: 1.5 }}>
                                                                Đây là bản chỉnh sửa nội dung của chương đã xuất bản (thường do yêu cầu sau báo cáo vi phạm). Bạn sẽ xem 2 phiên bản bên dưới: <strong>bản gốc đã xuất bản</strong> và <strong>bản chỉnh sửa gửi duyệt</strong>.
                                                            </div>
                                                        </div>
                                                    );
                                                })()}
                                            </div>

                                            {/* Thanh kéo chiều cao vùng đọc — đặt ngay dưới header chương, z-index cao để không bị khối khác chặn */}
                                            <div
                                                role="separator"
                                                aria-label="Kéo để thay đổi chiều cao vùng đọc"
                                                onMouseDown={startResize}
                                                style={{
                                                    height: '20px',
                                                    minHeight: '20px',
                                                    cursor: 'ns-resize',
                                                    flexShrink: 0,
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    justifyContent: 'center',
                                                    backgroundColor: isResizingContent ? '#cbd5e1' : '#e2e8f0',
                                                    borderTop: '1px solid #cbd5e1',
                                                    borderBottom: '1px solid #cbd5e1',
                                                    position: 'relative',
                                                    zIndex: 10
                                                }}
                                                title="Kéo lên/xuống để thay đổi chiều cao vùng đọc"
                                            >
                                                <span style={{ fontSize: '12px', color: '#64748b', pointerEvents: 'none' }}>⋮⋮</span>
                                            </div>

                                            <div style={{
                                                height: contentAreaHeight,
                                                minHeight: 300,
                                                maxHeight: '85vh',
                                                display: 'flex',
                                                flexDirection: 'column',
                                                flexShrink: 0,
                                                backgroundColor: '#ffffff'
                                            }}>
                                                {(() => {
                                                    const review = chapterReviewContent[selectedChapter.id];
                                                    const hasPendingVersion = review?.hasPendingVersion ?? review?.HasPendingVersion;
                                                    const pendingVersions = review?.pendingVersions ?? review?.PendingVersions ?? [];
                                                    const contentStyle = {
                                                        maxWidth: '900px',
                                                        margin: '0 auto',
                                                        width: '100%',
                                                        fontSize: '1.125rem',
                                                        lineHeight: 2,
                                                        color: '#1e293b',
                                                        whiteSpace: 'pre-wrap',
                                                        letterSpacing: '0.01em',
                                                        padding: '0 0.5rem'
                                                    };
                                                    if (hasPendingVersion && pendingVersions.length > 0) {
                                                        const v = pendingVersions[0];
                                                        const versionTitle = v?.titleSnapshot ?? v?.TitleSnapshot ?? '';
                                                        const versionContent = v?.contentSnapshot ?? v?.ContentSnapshot ?? '';
                                                        const originalContent = review?.originalContent ?? review?.OriginalContent ?? chapterContents[selectedChapter.id] ?? '—';
                                                        const hasOriginalToShow = Boolean(originalContent && String(originalContent).trim() && originalContent !== '—');
                                                        // Ưu tiên ChapterStatus từ API getChapterReviewContent (chuẩn từ backend); fallback sang selectedChapter từ list.
                                                        const chapterStatusRaw = review?.chapterStatus ?? review?.ChapterStatus ?? selectedChapter?.status ?? selectedChapter?.Status ?? '';
                                                        const chapterStatus = String(chapterStatusRaw).toLowerCase();
                                                        const chapterIsPublished = chapterStatus === 'published';
                                                        const chapterIsDraftOrRejected = chapterStatus === 'draft' || chapterStatus === 'rejected';
                                                        // 2 tab khi chương gốc đã PUBLISHED (hoặc isEditRequest). Fallback: API trả về cả OriginalContent + version thì coi là so sánh bản gốc/version. Bản nháp/Từ chối → chỉ 1 view (version).
                                                        const showTwoTabs = !chapterIsDraftOrRejected && (chapterIsPublished || publication?.isEditRequest || (hasOriginalToShow && hasPendingVersion));
                                                        if (!showTwoTabs) {
                                                            // Chương gốc chưa xuất bản / bản nháp / từ chối: chỉ hiển thị thông tin version author gửi đi duyệt, không tab.
                                                            return (
                                                                <div style={{
                                                                    flex: 1,
                                                                    minHeight: 0,
                                                                    overflowY: 'auto',
                                                                    padding: '2.5rem 3rem'
                                                                }}>
                                                                    <div style={contentStyle}>
                                                                        {versionContent || '—'}
                                                                    </div>
                                                                </div>
                                                            );
                                                        }
                                                        const tabs = [
                                                            { id: 'original', label: 'Chương gốc' },
                                                            { id: 'version', label: `Phiên bản của tôi${versionTitle ? ` — ${versionTitle}` : ''}` }
                                                        ];
                                                        return (
                                                            <>
                                                                <div style={{
                                                                    display: 'flex',
                                                                    gap: '0.25rem',
                                                                    padding: '0 1.5rem',
                                                                    borderBottom: '2px solid #e2e8f0',
                                                                    backgroundColor: '#f8fafc',
                                                                    flexShrink: 0
                                                                }}>
                                                                    {tabs.map((tab) => (
                                                                        <button
                                                                            key={tab.id}
                                                                            type="button"
                                                                            onClick={() => setContentTab(tab.id)}
                                                                            style={{
                                                                                padding: '0.75rem 1.25rem',
                                                                                fontSize: '0.875rem',
                                                                                fontWeight: 600,
                                                                                color: contentTab === tab.id ? '#0ea5e9' : '#64748b',
                                                                                backgroundColor: 'transparent',
                                                                                border: 'none',
                                                                                borderBottom: contentTab === tab.id ? '2px solid #0ea5e9' : '2px solid transparent',
                                                                                marginBottom: '-2px',
                                                                                cursor: 'pointer',
                                                                                transition: 'color 0.2s, border-color 0.2s'
                                                                            }}
                                                                        >
                                                                            {tab.label}
                                                                        </button>
                                                                    ))}
                                                                </div>
                                                                <div style={{
                                                                    flex: 1,
                                                                    minHeight: 0,
                                                                    overflowY: 'auto',
                                                                    padding: '2.5rem 3rem'
                                                                }}>
                                                                    {contentTab === 'original' && (
                                                                        <div style={contentStyle}>
                                                                            {originalContent}
                                                                        </div>
                                                                    )}
                                                                    {contentTab === 'version' && (
                                                                        <div style={contentStyle}>
                                                                            {versionContent || '—'}
                                                                        </div>
                                                                    )}
                                                                </div>
                                                            </>
                                                        );
                                                    }
                                                    return (
                                                        <div style={{
                                                            flex: 1,
                                                            minHeight: 0,
                                                            overflowY: 'auto',
                                                            padding: '2.5rem 3rem'
                                                        }}>
                                                            <div style={contentStyle}>
                                                                {chapterContents[selectedChapter.id] ?? 'Đang tải nội dung...'}
                                                            </div>
                                                        </div>
                                                    );
                                                })()}
                                            </div>
                                        </>
                                    ) : null}
                                </div>
                            </>
                        )}
                    </div>

                    {/* Footer - Actions. Chỉ khi chương gốc đã PUBLISHED (2 tab): ẩn nút ở tab Chương gốc. Chương bản nháp/từ chối (chỉ 1 nội dung version) thì luôn hiện nút. */}
                    {chapters.length > 0 && !showRejectForm && publication?.status === 'pending' && (() => {
                        const review = selectedChapter?.id ? chapterReviewContent[selectedChapter.id] : null;
                        const hasPendingVersionForChapter = Boolean(review?.hasPendingVersion ?? review?.HasPendingVersion);
                        const chapterStatusRaw = review?.chapterStatus ?? review?.ChapterStatus ?? selectedChapter?.status ?? selectedChapter?.Status ?? '';
                        const chapterStatus = String(chapterStatusRaw).toLowerCase();
                        const chapterIsPublished = chapterStatus === 'published';
                        const chapterIsDraftOrRejected = chapterStatus === 'draft' || chapterStatus === 'rejected';
                        const originalContent = review?.originalContent ?? review?.OriginalContent ?? (selectedChapter?.id ? chapterContents[selectedChapter.id] : null);
                        const hasOriginalToShow = Boolean(originalContent && String(originalContent).trim() && originalContent !== '—');
                        const isTwoTabCase = hasPendingVersionForChapter && !chapterIsDraftOrRejected && (chapterIsPublished || publication?.isEditRequest || hasOriginalToShow);
                        const isOnOriginalTab = contentTab === 'original';
                        const hideButtonsForOriginalTab = isTwoTabCase && isOnOriginalTab;
                        if (hideButtonsForOriginalTab) {
                            return (
                                <div style={{ padding: '0.75rem 1.5rem', borderTop: '1px solid #e2e8f0', backgroundColor: '#f1f5f9', fontSize: '0.875rem', color: '#64748b' }}>
                                    Chuyển sang tab « Phiên bản của tôi » để duyệt hoặc từ chối.
                                </div>
                            );
                        }
                        return (
                            <div style={{
                                padding: '1.5rem',
                                borderTop: '1px solid #e2e8f0',
                                display: 'flex',
                                justifyContent: 'flex-end',
                                gap: '1rem',
                                backgroundColor: '#f8fafc'
                            }}>
                                <button
                                    onClick={() => canApproveReject && setShowRejectForm(true)}
                                    disabled={isSubmitting || !canApproveReject}
                                    title={orderHint || 'Từ chối chương (kèm lý do)'}
                                    style={{
                                        padding: '0.75rem 1.5rem',
                                        backgroundColor: canApproveReject ? '#ffffff' : '#f1f5f9',
                                        color: canApproveReject ? '#ef4444' : '#94a3b8',
                                        fontSize: '0.875rem',
                                        fontWeight: 700,
                                        borderRadius: '8px',
                                        border: `2px solid ${canApproveReject ? '#ef4444' : '#e2e8f0'}`,
                                        cursor: (isSubmitting || !canApproveReject) ? 'not-allowed' : 'pointer',
                                        transition: 'all 0.2s',
                                        opacity: (isSubmitting || !canApproveReject) ? 0.5 : 1,
                                        display: 'flex',
                                        alignItems: 'center',
                                        gap: '0.5rem'
                                    }}
                                    onMouseEnter={(e) => {
                                        if (!isSubmitting && canApproveReject) {
                                            e.currentTarget.style.backgroundColor = '#fef2f2';
                                        }
                                    }}
                                    onMouseLeave={(e) => {
                                        if (canApproveReject) {
                                            e.currentTarget.style.backgroundColor = '#ffffff';
                                        }
                                    }}
                                >
                                    <XCircle style={{ width: '18px', height: '18px' }} />
                                    Từ chối
                                </button>

                                <button
                                    onClick={() => canApproveReject && openApproveConfirm()}
                                    disabled={isSubmitting || !selectedChapter || !canApproveReject}
                                    title={orderHint || 'Duyệt chương xuất bản'}
                                    style={{
                                        padding: '0.75rem 1.5rem',
                                        backgroundColor: canApproveReject ? '#13ec5b' : '#e2e8f0',
                                        color: canApproveReject ? '#ffffff' : '#94a3b8',
                                        fontSize: '0.875rem',
                                        fontWeight: 700,
                                        borderRadius: '8px',
                                        border: 'none',
                                        cursor: (isSubmitting || !selectedChapter || !canApproveReject) ? 'not-allowed' : 'pointer',
                                        transition: 'all 0.2s',
                                        opacity: (isSubmitting || !selectedChapter || !canApproveReject) ? 0.5 : 1,
                                        display: 'flex',
                                        alignItems: 'center',
                                        gap: '0.5rem'
                                    }}
                                    onMouseEnter={(e) => {
                                        if (!isSubmitting && selectedChapter && canApproveReject) {
                                            e.currentTarget.style.backgroundColor = '#10d954';
                                        }
                                    }}
                                    onMouseLeave={(e) => {
                                        if (canApproveReject) {
                                            e.currentTarget.style.backgroundColor = '#13ec5b';
                                        }
                                    }}
                                >
                                    <CheckCircle style={{ width: '18px', height: '18px' }} />
                                    {isSubmitting ? 'Đang xử lý...' : 'Duyệt chương'}
                                </button>
                            </div>
                        );
                    })()}

                    {/* Rejection Form */}
                    {showRejectForm && (
                        <div style={{
                            padding: '1.5rem',
                            borderTop: '1px solid #e2e8f0',
                            backgroundColor: '#fef2f2'
                        }}>
                            <label style={{
                                display: 'block',
                                fontSize: '0.875rem',
                                fontWeight: 600,
                                color: '#991b1b',
                                marginBottom: '0.5rem'
                            }}>
                                Lý do từ chối <span style={{ color: '#ef4444' }}>*</span>
                            </label>
                            <textarea
                                value={rejectionReason}
                                onChange={(e) => setRejectionReason(e.target.value)}
                                placeholder="Nhập lý do từ chối xuất bản (bắt buộc)..."
                                rows={4}
                                style={{
                                    width: '100%',
                                    padding: '0.75rem',
                                    fontSize: '0.875rem',
                                    border: '2px solid #fca5a5',
                                    borderRadius: '8px',
                                    resize: 'vertical',
                                    outline: 'none',
                                    fontFamily: 'inherit',
                                    marginBottom: '1rem'
                                }}
                                onFocus={(e) => e.target.style.borderColor = '#ef4444'}
                                onBlur={(e) => e.target.style.borderColor = '#fca5a5'}
                            />

                            <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '1rem' }}>
                                <button
                                    onClick={() => {
                                        setShowRejectForm(false);
                                        setRejectionReason('');
                                    }}
                                    disabled={isSubmitting}
                                    style={{
                                        padding: '0.75rem 1.5rem',
                                        backgroundColor: '#ffffff',
                                        color: '#64748b',
                                        fontSize: '0.875rem',
                                        fontWeight: 600,
                                        borderRadius: '8px',
                                        border: '1px solid #e2e8f0',
                                        cursor: isSubmitting ? 'not-allowed' : 'pointer',
                                        transition: 'all 0.2s',
                                        opacity: isSubmitting ? 0.5 : 1
                                    }}
                                    onMouseEnter={(e) => {
                                        if (!isSubmitting) {
                                            e.currentTarget.style.backgroundColor = '#f8fafc';
                                        }
                                    }}
                                    onMouseLeave={(e) => {
                                        if (!isSubmitting) {
                                            e.currentTarget.style.backgroundColor = '#ffffff';
                                        }
                                    }}
                                >
                                    Hủy
                                </button>

                                <button
                                    onClick={openRejectConfirm}
                                    disabled={isSubmitting || !rejectionReason.trim() || !canApproveReject}
                                    title={!canApproveReject ? orderHint : undefined}
                                    style={{
                                        padding: '0.75rem 1.5rem',
                                        backgroundColor: (isSubmitting || !rejectionReason.trim() || !canApproveReject) ? '#e2e8f0' : '#ef4444',
                                        color: (isSubmitting || !rejectionReason.trim() || !canApproveReject) ? '#94a3b8' : '#ffffff',
                                        fontSize: '0.875rem',
                                        fontWeight: 700,
                                        borderRadius: '8px',
                                        border: 'none',
                                        cursor: (isSubmitting || !rejectionReason.trim() || !canApproveReject) ? 'not-allowed' : 'pointer',
                                        transition: 'all 0.2s',
                                        opacity: (isSubmitting || !rejectionReason.trim() || !canApproveReject) ? 0.5 : 1
                                    }}
                                    onMouseEnter={(e) => {
                                        if (!isSubmitting && rejectionReason.trim() && canApproveReject) {
                                            e.currentTarget.style.backgroundColor = '#dc2626';
                                        }
                                    }}
                                    onMouseLeave={(e) => {
                                        if (rejectionReason.trim() && canApproveReject) {
                                            e.currentTarget.style.backgroundColor = '#ef4444';
                                        }
                                    }}
                                >
                                    {isSubmitting ? 'Đang xử lý...' : 'Xác nhận từ chối'}
                                </button>
                            </div>
                        </div>
                    )}

                    {/* Already Reviewed Info - Chỉ hiển thị khi không còn chương chờ duyệt. Ẩn nếu vừa từ chối trong phiên để moderator duyệt liên tiếp không bị hiện lại lý do từ chối */}
                    {chapters.length === 0 && publication.status !== 'pending' && !justRejectedInSessionRef.current && (
                        <div style={{
                            padding: '1.5rem',
                            borderTop: '1px solid #e2e8f0',
                            backgroundColor: publication.status === 'approved' ? '#f0fdf4' : '#fef2f2'
                        }}>
                            <div style={{
                                fontSize: '0.875rem',
                                color: publication.status === 'approved' ? '#065f46' : '#991b1b',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.5rem'
                            }}>
                                {publication.status === 'approved' ? <CheckCircle style={{ width: '16px', height: '16px' }} /> : <XCircle style={{ width: '16px', height: '16px' }} />}
                                <span style={{ fontWeight: 600 }}>
                                    {publication.status === 'approved' ? 'Đã duyệt xuất bản' : 'Đã từ chối xuất bản'}
                                </span>
                                <span>•</span>
                                <span>{formatDate(publication.reviewedAt)}</span>
                                {publication.reviewedBy && (
                                    <>
                                        <span>•</span>
                                        <span>Bởi: {publication.reviewedBy}</span>
                                    </>
                                )}
                            </div>

                            {publication.status === 'rejected' && publication.rejectionReason && (
                                <div style={{
                                    marginTop: '0.75rem',
                                    padding: '0.75rem',
                                    backgroundColor: '#ffffff',
                                    borderLeft: '3px solid #ef4444',
                                    borderRadius: '0.375rem'
                                }}>
                                    <div style={{ fontSize: '0.75rem', fontWeight: 600, color: '#991b1b', marginBottom: '0.25rem' }}>
                                        Lý do từ chối:
                                    </div>
                                    <div style={{ fontSize: '0.875rem', color: '#991b1b' }}>
                                        {publication.rejectionReason}
                                    </div>
                                </div>
                            )}
                        </div>
                    )}
                </div>
            </div>

            {/* Dialog xác nhận duyệt xuất bản */}
            {showApproveConfirm && (
                <div
                    style={{
                        position: 'fixed',
                        top: 0,
                        left: 0,
                        right: 0,
                        bottom: 0,
                        backgroundColor: 'rgba(0, 0, 0, 0.5)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 10000
                    }}
                    onClick={() => setShowApproveConfirm(false)}
                >
                    <div
                        style={{
                            backgroundColor: '#ffffff',
                            borderRadius: '12px',
                            padding: '1.5rem',
                            maxWidth: '400px',
                            width: '90%',
                            boxShadow: '0 20px 60px rgba(0, 0, 0, 0.3)'
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <h3 style={{ fontSize: '1.125rem', fontWeight: 600, color: '#1e293b', margin: '0 0 1rem 0' }}>
                            Xác nhận duyệt chương
                        </h3>
                        <p style={{ fontSize: '0.875rem', color: '#64748b', margin: '0 0 1.5rem 0', lineHeight: 1.5 }}>
                            Bạn có chắc chắn muốn duyệt xuất bản chương "{selectedChapter?.title}"?
                        </p>
                        <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                            <button
                                onClick={() => setShowApproveConfirm(false)}
                                style={{
                                    padding: '0.625rem 1.25rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    color: '#64748b',
                                    backgroundColor: '#f1f5f9',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: 'pointer'
                                }}
                            >
                                Hủy
                            </button>
                            <button
                                onClick={handleApproveConfirm}
                                style={{
                                    padding: '0.625rem 1.25rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    color: '#ffffff',
                                    backgroundColor: '#13ec5b',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: 'pointer'
                                }}
                            >
                                Xác nhận
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Popup xác nhận từ chối duyệt */}
            {showRejectConfirm && (
                <div
                    style={{
                        position: 'fixed',
                        top: 0,
                        left: 0,
                        right: 0,
                        bottom: 0,
                        backgroundColor: 'rgba(0, 0, 0, 0.5)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 10000
                    }}
                    onClick={() => setShowRejectConfirm(false)}
                >
                    <div
                        style={{
                            backgroundColor: '#ffffff',
                            borderRadius: '12px',
                            padding: '1.5rem',
                            maxWidth: '400px',
                            width: '90%',
                            boxShadow: '0 20px 60px rgba(0, 0, 0, 0.3)'
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <h3 style={{ fontSize: '1.125rem', fontWeight: 600, color: '#1e293b', margin: '0 0 1rem 0' }}>
                            Xác nhận từ chối duyệt
                        </h3>
                        <p style={{ fontSize: '0.875rem', color: '#64748b', margin: '0 0 1.5rem 0', lineHeight: 1.5 }}>
                            Bạn có chắc muốn từ chối xuất bản này? Hành động này không thể hoàn tác.
                        </p>
                        <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                            <button
                                onClick={() => setShowRejectConfirm(false)}
                                style={{
                                    padding: '0.625rem 1.25rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    color: '#64748b',
                                    backgroundColor: '#f1f5f9',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: 'pointer'
                                }}
                            >
                                Hủy
                            </button>
                            <button
                                onClick={handleRejectSubmit}
                                style={{
                                    padding: '0.625rem 1.25rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    color: '#ffffff',
                                    backgroundColor: '#ef4444',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: 'pointer'
                                }}
                            >
                                Xác nhận
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {escalateOpen && (
                <div
                    style={{
                        position: 'fixed',
                        inset: 0,
                        backgroundColor: 'rgba(0,0,0,0.5)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 10001,
                        padding: '1rem',
                    }}
                    onClick={() => !escalateSubmitting && setEscalateOpen(false)}
                >
                    <div
                        style={{
                            backgroundColor: '#fff',
                            borderRadius: '12px',
                            padding: '1.5rem',
                            maxWidth: '480px',
                            width: '100%',
                            boxShadow: '0 20px 60px rgba(0,0,0,0.3)',
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <h3 style={{ margin: '0 0 0.75rem', fontSize: '1.125rem', fontWeight: 700, color: '#0f172a' }}>
                            Gửi báo cáo lên quản trị — xin gia hạn hạn duyệt
                        </h3>
                        <p style={{ margin: '0 0 1rem', fontSize: '0.8125rem', color: '#64748b' }}>
                            Đơn gia hạn theo từng chương (hoặc truyện nếu bạn đang xem cấp truyện). Sau khi gửi, bạn không thể duyệt/từ chối mục đó cho đến khi quản trị viên xử lý.
                            {' '}Để hủy nhận duyệt cả truyện, dùng nút <strong>Hủy nhận duyệt</strong> cạnh &quot;Xem chi tiết&quot; trên danh sách chờ duyệt.
                        </p>
                        <div style={{ marginBottom: '0.75rem' }}>
                            <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 600, color: '#334155' }}>
                                Hạn đề xuất sau gia hạn (bắt buộc)
                                <input
                                    type="datetime-local"
                                    value={escalateProposedDeadline}
                                    onChange={(e) => setEscalateProposedDeadline(e.target.value)}
                                    style={{
                                        display: 'block',
                                        width: '100%',
                                        marginTop: '0.35rem',
                                        padding: '0.5rem',
                                        borderRadius: '8px',
                                        border: extendProposedDeadlineError ? '2px solid #ef4444' : '1px solid #cbd5e1',
                                        outline: extendProposedDeadlineError ? 'none' : undefined,
                                    }}
                                />
                            </label>
                            <p style={{ fontSize: '0.75rem', color: '#64748b', margin: '0.5rem 0 0', lineHeight: 1.45 }}>
                                {(reviewAssignment?.reviewDeadlineAt ?? reviewAssignment?.ReviewDeadlineAt) ? (
                                    <>
                                        <strong>Hạn duyệt hiện tại của bạn:</strong>{' '}
                                        {formatDate(reviewAssignment?.reviewDeadlineAt ?? reviewAssignment?.ReviewDeadlineAt)}.
                                        {' '}Hạn đề xuất phải <strong>muộn hơn</strong> mốc này (gia hạn = kéo dài thêm, không được chọn ngày sớm hơn).
                                        {' '}Ngoài ra phải cách <strong>thời điểm hiện tại ít nhất 24 giờ</strong>.
                                    </>
                                ) : (
                                    <>
                                        Hạn đề xuất phải <strong>muộn hơn hạn duyệt</strong> bạn đã chọn khi nhận đơn, và cách <strong>hiện tại ít nhất 24 giờ</strong>.
                                    </>
                                )}
                            </p>
                            {extendProposedDeadlineError ? (
                                <div
                                    role="alert"
                                    style={{
                                        marginTop: '0.5rem',
                                        padding: '0.5rem 0.75rem',
                                        backgroundColor: '#fef2f2',
                                        border: '1px solid #fecaca',
                                        borderRadius: '8px',
                                        fontSize: '0.8125rem',
                                        fontWeight: 600,
                                        color: '#b91c1c',
                                        lineHeight: 1.5,
                                    }}
                                >
                                    {extendProposedDeadlineError}
                                </div>
                            ) : null}
                        </div>
                        <label style={{ display: 'block', marginBottom: '1rem', fontSize: '0.8125rem', fontWeight: 600, color: '#334155' }}>
                            Lý do <span style={{ color: '#ef4444' }}>*</span>
                            <textarea
                                value={escalateReason}
                                onChange={(e) => setEscalateReason(e.target.value)}
                                rows={4}
                                placeholder="Mô tả lý do (tối thiểu 10 ký tự)..."
                                style={{
                                    display: 'block',
                                    width: '100%',
                                    marginTop: '0.35rem',
                                    padding: '0.5rem',
                                    borderRadius: '8px',
                                    border: '1px solid #cbd5e1',
                                    fontFamily: 'inherit',
                                    resize: 'vertical',
                                }}
                            />
                        </label>
                        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
                            <button
                                type="button"
                                disabled={escalateSubmitting}
                                onClick={() => setEscalateOpen(false)}
                                style={{ padding: '0.5rem 1rem', borderRadius: '8px', border: '1px solid #e2e8f0', background: '#f8fafc', cursor: 'pointer' }}
                            >
                                Đóng
                            </button>
                            <button
                                type="button"
                                disabled={escalateSubmitting || (escalateKind === 'EXTEND_DEADLINE' && !!extendProposedDeadlineError)}
                                onClick={handleSubmitEscalation}
                                style={{
                                    padding: '0.5rem 1rem',
                                    borderRadius: '8px',
                                    border: 'none',
                                    background: escalateSubmitting || (escalateKind === 'EXTEND_DEADLINE' && extendProposedDeadlineError) ? '#94a3b8' : '#0ea5e9',
                                    color: '#fff',
                                    fontWeight: 600,
                                    cursor: escalateSubmitting || (escalateKind === 'EXTEND_DEADLINE' && extendProposedDeadlineError) ? 'not-allowed' : 'pointer',
                                }}
                            >
                                {escalateSubmitting ? 'Đang gửi...' : 'Gửi đơn'}
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </>
    );
}
