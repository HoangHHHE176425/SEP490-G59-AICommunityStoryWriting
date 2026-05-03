import { ChevronRight, Star } from 'lucide-react';
import { useState, useEffect, useCallback, useMemo } from 'react';
import { useParams, useNavigate, useLocation } from 'react-router-dom';
import StoryHeader from '../../components/story-detail/StoryHeader';
import { ChapterList } from '../../components/story-detail/ChapterList';
import { CommentSection } from '../../components/story-detail/CommentSection';
import { AuthorCard } from '../../components/story-detail/AuthorCard';
import { RelatedStories } from '../../components/story-detail/RelatedStories';
import { RatingModal } from '../../components/story-detail/RatingModal';
import { ReportModal } from '../../components/story-detail/ReportModal';
import { Footer } from '../../components/homepage/Footer';
import { Header } from '../../components/homepage/Header';
import {
    getStoryById,
    getStories,
    recordStoryView,
    getViewerKeyForViewCache,
    hasViewedStoryInCooldown,
    setStoryViewCache,
    rateStory,
    getStoryRatings,
    getStoryComments,
    addStoryComment,
    toggleCommentLike,
    followStory,
    unfollowStory,
} from '../../api/story/storyApi';
import { getChapters, getChapterById, getChaptersByStoryId } from '../../api/chapter/chapterApi';
import { getProfileByUserId } from '../../api/account/accountApi';
import { getAuthorFollowersCount } from '../../api/author/authorApi';
import { resolveBackendUrl } from '../../utils/resolveBackendUrl';
import { useAuth } from '../../contexts/AuthContext';
import { useToast } from '../../components/author/story-editor/Toast';
import {
    getCommentReportReasons,
    getStoryReportReasons,
    reportStory,
    reportStoryComment,
} from '../../api/report/reportApi';

function formatTimeAgo(dateStr) {
    if (!dateStr) return '';
    const date = new Date(dateStr);
    const now = new Date();
    const diffMs = now - date;
    const diffMins = Math.floor(diffMs / 60000);
    const diffHours = Math.floor(diffMs / 3600000);
    const diffDays = Math.floor(diffMs / 86400000);
    if (diffMins < 60) return `${diffMins} phút trước`;
    if (diffHours < 24) return `${diffHours} giờ trước`;
    if (diffDays < 7) return `${diffDays} ngày trước`;
    return date.toLocaleDateString('vi-VN');
}

function parseAiPercent(raw) {
    if (raw == null) return 0;
    if (typeof raw === 'number') return Number.isFinite(raw) ? raw : 0;
    if (typeof raw === 'string') {
        const normalized = raw.replace(',', '.').trim();
        const direct = Number(normalized.replace('%', '').trim());
        if (Number.isFinite(direct)) return direct;
        const match = normalized.match(/-?\d+(?:\.\d+)?/);
        const n = match ? Number(match[0]) : NaN;
        return Number.isFinite(n) ? n : 0;
    }
    return 0;
}

function extractChapterAiPercent(chapter) {
    const candidates = [
        chapter?.aiContributionRatio,
        chapter?.AiContributionRatio,
        chapter?.aiSimilarityPercent,
        chapter?.AiSimilarityPercent,
        chapter?.aiContribution,
        chapter?.AiContribution,
        chapter?.aiSimilarity,
        chapter?.AiSimilarity,
        chapter?.aiContributionLabel,
        chapter?.AiContributionLabel,
        chapter?.ai_contribution_ratio,
        chapter?.ai_similarity_percent,
    ];
    return candidates
        .map((x) => parseAiPercent(x))
        .filter((x) => Number.isFinite(x) && x > 0)
        .reduce((max, x) => (x > max ? x : max), 0);
}

export function StoryDetail() {
    const { storyId } = useParams();
    const navigate = useNavigate();
    const location = useLocation();
    const { user } = useAuth();
    const viewerKey = getViewerKeyForViewCache(user?.id ?? null);
    const [story, setStory] = useState(null);
    const [chapters, setChapters] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [isFollowing, setIsFollowing] = useState(false);
    const [activeTab, setActiveTab] = useState('chapters');
    const [isRatingModalOpen, setIsRatingModalOpen] = useState(false);
    const [isReportCommentModalOpen, setIsReportCommentModalOpen] = useState(false);
    const [isReportStoryModalOpen, setIsReportStoryModalOpen] = useState(false);
    const [reportingCommentId, setReportingCommentId] = useState(null);
    const [ratingError, setRatingError] = useState(null);
    const [ratingSubmitting, setRatingSubmitting] = useState(false);
    const { showToast, ToastContainer } = useToast();
    const [comments, setComments] = useState([]);
    const [commentsLoading, setCommentsLoading] = useState(false);
    const [commentError, setCommentError] = useState(null);
    const [reviews, setReviews] = useState([]);
    const [reviewsLoading, setReviewsLoading] = useState(false);
    const [visibleReviewsCount, setVisibleReviewsCount] = useState(3);
    const [reportSubmitting, setReportSubmitting] = useState(false);
    const [reportError, setReportError] = useState(null);
    const [reportReasonOptions, setReportReasonOptions] = useState({ story: [], comment: [] });
    const [relatedStoriesData, setRelatedStoriesData] = useState([]);

    const normalizeReasonOptions = useCallback((list) => {
        const rows = Array.isArray(list) ? list : [];
        return rows
            .map((x) => {
                const value = String(x?.code ?? x?.Code ?? '').trim();
                if (!value) return null;
                const label = String(x?.labelVi ?? x?.LabelVi ?? x?.label ?? x?.Label ?? value).trim();
                return { value, label };
            })
            .filter(Boolean);
    }, []);

    const loadReportReasons = useCallback(async (targetType) => {
        try {
            if (targetType === 'story') {
                if ((reportReasonOptions.story?.length ?? 0) > 0) return;
                const list = await getStoryReportReasons();
                setReportReasonOptions((prev) => ({ ...prev, story: normalizeReasonOptions(list) }));
            } else {
                if ((reportReasonOptions.comment?.length ?? 0) > 0) return;
                const list = await getCommentReportReasons();
                setReportReasonOptions((prev) => ({ ...prev, comment: normalizeReasonOptions(list) }));
            }
        } catch (e) {
            const msg = e?.response?.data?.message ?? e?.message ?? 'Không tải được lý do báo cáo.';
            showToast(msg, 'error');
        }
    }, [normalizeReasonOptions, reportReasonOptions.comment?.length, reportReasonOptions.story?.length, showToast]);

    const svgAvatarDataUrl = (name) => {
        const initial = (String(name || 'T').trim()[0] || 'T').toUpperCase();
        const svg = `
          <svg xmlns="http://www.w3.org/2000/svg" width="256" height="256">
            <defs>
              <linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
                <stop offset="0" stop-color="#13EC5B"/>
                <stop offset="1" stop-color="#2B7FFF"/>
              </linearGradient>
            </defs>
            <rect width="256" height="256" rx="40" fill="url(#g)"/>
            <text x="50%" y="54%" dominant-baseline="middle" text-anchor="middle"
                  font-family="Arial, Helvetica, sans-serif" font-size="120" font-weight="800" fill="white">${initial}</text>
          </svg>
        `.trim();
        return `data:image/svg+xml;utf8,${encodeURIComponent(svg)}`;
    };

    const getStoryAuthorName = (storyRes) => {
        const name =
            storyRes?.authorName ??
            storyRes?.AuthorName ??
            storyRes?.author?.name ??
            storyRes?.author?.displayName ??
            storyRes?.authorDisplayName ??
            storyRes?.AuthorDisplayName ??
            storyRes?.createdByName ??
            storyRes?.CreatedByName ??
            null;

        return typeof name === 'string' && name.trim() ? name.trim() : 'Tác giả';
    };

    const getStoryAuthorAvatar = (storyRes, authorName) => {
        const avatar =
            storyRes?.authorAvatarUrl ??
            storyRes?.AuthorAvatarUrl ??
            storyRes?.authorAvatar ??
            storyRes?.AuthorAvatar ??
            storyRes?.author?.avatar ??
            storyRes?.author?.avatarUrl ??
            storyRes?.author?.AvatarUrl ??
            storyRes?.avatarUrl ??
            storyRes?.AvatarUrl ??
            null;

        if (avatar && typeof avatar === 'string' && avatar.trim()) return resolveBackendUrl(avatar.trim());
        return svgAvatarDataUrl(authorName);
    };

    useEffect(() => {
        let cancelled = false;
        const id = setTimeout(() => {
            if (!storyId) {
                setLoading(false);
                setError('Thiếu ID truyện');
                return;
            }
            setLoading(true);
            setError(null);
            const inCooldown = hasViewedStoryInCooldown(storyId, viewerKey);
            const loadData = () =>
                Promise.all([
                    getStoryById(storyId, { recordView: false }),
                    getChapters({ storyId, status: 'PUBLISHED', pageSize: 500 }),
                ]);
            (inCooldown ? loadData() : recordStoryView(storyId).then(() => { setStoryViewCache(storyId, viewerKey); return loadData(); }))
                .then(async ([storyRes, chaptersRes]) => {
                    if (cancelled) return;
                    const rawItems = Array.isArray(chaptersRes) ? chaptersRes : (chaptersRes?.items ?? chaptersRes?.Items ?? []);
                    let chapterItemsWithAi = await Promise.all(
                        rawItems.map(async (ch) => {
                            const chapterId = ch?.id ?? ch?.Id;
                            const accessType = (ch.accessType ?? ch.AccessType ?? 'FREE').toUpperCase();
                            const coinPrice = Number(ch.coinPrice ?? ch.CoinPrice ?? 0) || 0;
                            const isPaid = accessType === 'PAID' && coinPrice > 0;

                            const hasAiField =
                                ch?.aiContributionRatio != null ||
                                ch?.AiContributionRatio != null ||
                                ch?.aiSimilarityPercent != null ||
                                ch?.AiSimilarityPercent != null;

                            if (!chapterId) return ch;

                            if (!hasAiField) {
                                try {
                                    const details = await getChapterById(chapterId);
                                    return { ...ch, ...details };
                                } catch {
                                    return ch;
                                }
                            }

                            if (isPaid && user?.id) {
                                try {
                                    const details = await getChapterById(chapterId);
                                    const rawUn =
                                        details?.isUnlocked ??
                                        details?.IsUnlocked ??
                                        details?.unlocked ??
                                        details?.Unlocked;
                                    return {
                                        ...ch,
                                        isUnlocked: Boolean(rawUn),
                                        IsUnlocked: Boolean(rawUn),
                                    };
                                } catch {
                                    return ch;
                                }
                            }

                            return ch;
                        })
                    );
                    const hasAnyAiField = chapterItemsWithAi.some((ch) => extractChapterAiPercent(ch) > 0);
                    if (!hasAnyAiField && storyId) {
                        try {
                            const fullChapterList = await getChaptersByStoryId(storyId);
                            const fullItems = Array.isArray(fullChapterList) ? fullChapterList : [];
                            const aiByChapterId = new Map(
                                fullItems.map((ch) => [String(ch?.id ?? ch?.Id ?? ''), extractChapterAiPercent(ch)])
                            );
                            chapterItemsWithAi = chapterItemsWithAi.map((ch) => {
                                const chapterId = String(ch?.id ?? ch?.Id ?? '');
                                const aiPercent = aiByChapterId.get(chapterId) ?? 0;
                                if (aiPercent <= 0) return ch;
                                return { ...ch, aiContributionRatio: aiPercent };
                            });
                        } catch {
                            // keep original chapter list if fallback endpoint fails
                        }
                    }
                    const hasAnyAiAfterByStoryId = chapterItemsWithAi.some((ch) => extractChapterAiPercent(ch) > 0);
                    if (!hasAnyAiAfterByStoryId && storyId) {
                        try {
                            // Fallback cuối: một số môi trường không trả AI% khi lọc status=PUBLISHED.
                            // Lấy thêm danh sách chapter không lọc status rồi map AI% theo id/order.
                            const allChaptersRes = await getChapters({ storyId, pageSize: 500 });
                            const allItems = Array.isArray(allChaptersRes)
                                ? allChaptersRes
                                : (allChaptersRes?.items ?? allChaptersRes?.Items ?? []);

                            const aiById = new Map();
                            const aiByOrder = new Map();
                            allItems.forEach((ch) => {
                                const aiPercent = extractChapterAiPercent(ch);
                                if (!(aiPercent > 0)) return;
                                const id = String(ch?.id ?? ch?.Id ?? '');
                                const order = Number(ch?.orderIndex ?? ch?.OrderIndex ?? -1);
                                if (id) aiById.set(id, aiPercent);
                                if (Number.isFinite(order) && order >= 0) aiByOrder.set(order, aiPercent);
                            });

                            chapterItemsWithAi = chapterItemsWithAi.map((ch) => {
                                const id = String(ch?.id ?? ch?.Id ?? '');
                                const order = Number(ch?.orderIndex ?? ch?.OrderIndex ?? -1);
                                const aiPercent = (id && aiById.get(id)) ?? (Number.isFinite(order) ? aiByOrder.get(order) : 0) ?? 0;
                                if (!(aiPercent > 0)) return ch;
                                return {
                                    ...ch,
                                    aiContributionRatio: aiPercent,
                                };
                            });
                        } catch {
                            // keep original chapter list if this fallback fails
                        }
                    }
                    const categoryNamesStr = storyRes?.categoryNames ?? storyRes?.CategoryNames ?? '';
                    const genreArr = categoryNamesStr
                        ? String(categoryNamesStr).split(',').map((s) => s.trim()).filter(Boolean)
                        : [];
                    const coverPath = storyRes?.coverImage ?? storyRes?.CoverImage;
                    const totalViews = Number(storyRes?.totalViews ?? storyRes?.TotalViews ?? 0);
                    const totalComments = Number(storyRes?.totalComments ?? storyRes?.TotalComments ?? 0);
                    const totalChapters = chapterItemsWithAi.length;
                    const storyUsesAiRaw = storyRes?.usesAi ?? storyRes?.UsesAi;
                    const chapterAiRatios = chapterItemsWithAi
                        .map((ch) => extractChapterAiPercent(ch))
                        .filter((x) => Number.isFinite(x) && x > 0);
                    const hasAiAssistedChapter = chapterAiRatios.length > 0;
                    const storyUsesAi = storyUsesAiRaw === true || hasAiAssistedChapter;
                    const authorId = storyRes?.authorId ?? storyRes?.AuthorId;
                    const categoryIdsRaw =
                        storyRes?.categoryIds ??
                        storyRes?.CategoryIds ??
                        storyRes?.categories?.map?.((x) => x?.id ?? x?.Id) ??
                        [];
                    const categoryIds = Array.isArray(categoryIdsRaw)
                        ? categoryIdsRaw.map((x) => String(x || '').trim()).filter(Boolean)
                        : [];
                    setIsFollowing(!!(storyRes?.userIsFollowing ?? storyRes?.UserIsFollowing));
                    const progressStatusRaw = (storyRes?.storyProgressStatus ?? storyRes?.StoryProgressStatus ?? 'ONGOING')?.toString?.() ?? 'ONGOING';
                    const progressUpper = String(progressStatusRaw).toUpperCase();
                    const progressLabel = progressUpper === 'COMPLETED' ? 'Hoàn thành' : progressUpper === 'HIATUS' ? 'Tạm dừng' : 'Đang ra';
                    const authorName = getStoryAuthorName(storyRes);
                    const storyPayload = {
                        id: storyRes?.id ?? storyRes?.Id,
                        title: storyRes?.title ?? storyRes?.Title ?? 'Không có tiêu đề',
                        author: {
                            id: authorId,
                            userId: authorId,
                            name: authorName,
                            avatar: getStoryAuthorAvatar(storyRes, authorName),
                            // null = đang chờ GET /authors/{id}/followers-count (tránh hiển thị 0 giả)
                            followers: authorId ? null : 0,
                        },
                        cover: coverPath ? resolveBackendUrl(coverPath) : '',
                        genre: genreArr.length ? genreArr : ['Chưa phân loại'],
                        // Trạng thái tiến độ truyện: Đang ra / Tạm dừng / Hoàn thành
                        storyProgressStatus: progressUpper,
                        storyProgressLabel: progressLabel,
                        // Nhãn cập nhật (UI): tách riêng khỏi trạng thái tiến độ
                        updateLabel: 'Đang cập nhật',
                        rating: Number(storyRes?.avgRating ?? storyRes?.AvgRating ?? 0) || 0,
                        totalRatings: Number(storyRes?.totalRatings ?? storyRes?.TotalRatings ?? 0) || 0,
                        views: totalViews,
                        totalViews,
                        comments: totalComments,
                        chapters: totalChapters,
                        words: 0,
                        lastUpdate: storyRes?.updatedAt ? formatTimeAgo(storyRes.updatedAt) : 'Chưa cập nhật',
                        description: storyRes?.summary ?? storyRes?.Summary ?? 'Chưa có giới thiệu.',
                        lastReadChapterId: storyRes?.lastReadChapterId ?? storyRes?.LastReadChapterId ?? null,
                        lastReadChapterTitle: storyRes?.lastReadChapterTitle ?? storyRes?.LastReadChapterTitle ?? null,
                        lastReadAt: (storyRes?.lastReadAt ?? storyRes?.LastReadAt) ? formatTimeAgo(storyRes?.lastReadAt ?? storyRes?.LastReadAt) : null,
                        categoryIds,
                        commentsDisabled: !!(storyRes?.commentsDisabled ?? storyRes?.CommentsDisabled),
                        usesAi: storyUsesAi,
                    };
                    const newCount = 3; // số chương mới nhất được gắn nhãn MỚI
                    setChapters(chapterItemsWithAi.map((ch, idx) => {
                        const orderIndex = ch.orderIndex ?? ch.OrderIndex ?? idx;
                        const num = orderIndex + 1;
                        const updatedAt = ch.updatedAt ?? ch.UpdatedAt ?? ch.publishedAt ?? ch.PublishedAt;
                        const accessType = (ch.accessType ?? ch.AccessType ?? 'FREE').toUpperCase();
                        const coinPrice = Number(ch.coinPrice ?? ch.CoinPrice ?? 0) || 0;
                        const unlockKnown =
                            ch?.isUnlocked !== undefined ||
                            ch?.IsUnlocked !== undefined ||
                            ch?.unlocked !== undefined ||
                            ch?.Unlocked !== undefined;
                        const isUnlocked = Boolean(
                            ch?.isUnlocked ??
                            ch?.IsUnlocked ??
                            ch?.unlocked ??
                            ch?.Unlocked ??
                            false
                        );
                        const isPaidLocked = accessType === 'PAID' && coinPrice > 0 && (unlockKnown ? !isUnlocked : true);
                        const aiContributionRatio = extractChapterAiPercent(ch);
                        return {
                            id: num,
                            chapterId: ch.id ?? ch.Id,
                            title: ch.title ?? ch.Title ?? `Chương ${num}`,
                            time: updatedAt ? formatTimeAgo(updatedAt) : '',
                            views: Number(ch.viewCount ?? ch.ViewCount ?? ch.views ?? 0) || 0,
                            commentCount: Number(ch.commentCount ?? ch.CommentCount ?? 0) || 0,
                            isNew: idx >= chapterItemsWithAi.length - newCount,
                            isLocked: isPaidLocked,
                            isUnlocked,
                            unlockKnown,
                            accessType,
                            coinPrice,
                            aiContributionRatio,
                            isAiAssisted: aiContributionRatio > 0,
                        };
                    }));
                    if (!authorId) {
                        setStory(storyPayload);
                        return;
                    }
                    return Promise.all([
                        getProfileByUserId(authorId),
                        getAuthorFollowersCount(authorId).catch(() => 0),
                    ])
                        .then(([profile, followerCount]) => {
                            if (cancelled) return;
                            storyPayload.author = {
                                id: profile.id ?? authorId,
                                userId: profile.id ?? authorId,
                                name: profile.displayName ?? storyPayload.author.name,
                                avatar: profile.avatarUrl ? resolveBackendUrl(profile.avatarUrl) : storyPayload.author.avatar,
                                followers: typeof followerCount === 'number' ? followerCount : 0,
                            };
                            setStory(storyPayload);
                        })
                        .catch(() => {
                            if (cancelled) return;
                            // Profile lỗi vẫn cố lấy số follower (public API)
                            getAuthorFollowersCount(authorId)
                                .then((n) => {
                                    if (cancelled) return;
                                    storyPayload.author.followers = typeof n === 'number' ? n : 0;
                                    setStory({ ...storyPayload });
                                })
                                .catch(() => {
                                    if (!cancelled) {
                                        storyPayload.author.followers = 0;
                                        setStory({ ...storyPayload });
                                    }
                                });
                        });
                })
                .catch((err) => {
                    if (!cancelled) {
                        setError(err?.message ?? 'Không tải được truyện');
                        setStory(null);
                        setChapters([]);
                    }
                })
                .finally(() => {
                    if (!cancelled) setLoading(false);
                });
        }, 0);
        return () => {
            cancelled = true;
            clearTimeout(id);
        };
    }, [storyId, viewerKey, user?.id]);

    useEffect(() => {
        let cancelled = false;
        const categoryIds = Array.isArray(story?.categoryIds) ? story.categoryIds : [];
        if (!storyId || categoryIds.length === 0) {
            setRelatedStoriesData([]);
            return;
        }

        getStories({
            page: 1,
            pageSize: 24,
            status: 'PUBLISHED',
            categoryIds,
        })
            .then((res) => {
                if (cancelled) return;
                const items = Array.isArray(res?.items)
                    ? res.items
                    : Array.isArray(res?.Items)
                        ? res.Items
                        : Array.isArray(res)
                            ? res
                            : [];

                const normalized = items
                    .map((it) => {
                        const id = it?.id ?? it?.Id;
                        if (!id) return null;
                        return {
                            id: String(id),
                            title: it?.title ?? it?.Title ?? 'Không có tiêu đề',
                            cover: resolveBackendUrl(it?.coverImage ?? it?.CoverImage ?? ''),
                            author: it?.authorName ?? it?.AuthorName ?? 'Tác giả',
                            rating: Number(it?.avgRating ?? it?.AvgRating ?? 0) || 0,
                            chapters: Number(it?.totalChapters ?? it?.TotalChapters ?? 0) || 0,
                        };
                    })
                    .filter(Boolean)
                    .filter((it) => it.id !== String(storyId))
                    .slice(0, 12);

                setRelatedStoriesData(normalized);
            })
            .catch(() => {
                if (!cancelled) setRelatedStoriesData([]);
            });

        return () => {
            cancelled = true;
        };
    }, [storyId, story?.categoryIds]);

    const loadComments = useCallback((options = {}) => {
        if (!storyId) return;
        const silent = options.silent === true;
        if (!silent) {
            setCommentsLoading(true);
            setCommentError(null);
        }
        getStoryComments(storyId)
            .then((list) => setComments(Array.isArray(list) ? list : []))
            .catch((err) => { if (!silent) setCommentError(err?.response?.data?.message ?? 'Không tải được bình luận.'); })
            .finally(() => { if (!silent) setCommentsLoading(false); });
    }, [storyId]);

    useEffect(() => {
        if (storyId && activeTab === 'comments') loadComments();
    }, [storyId, activeTab, loadComments]);

    // Mở tab bình luận khi vào từ thông báo (hash #comment-{guid})
    useEffect(() => {
        const h = location.hash || '';
        if (h && /^#comment-/i.test(h)) {
            setActiveTab('comments');
        }
    }, [location.hash]);

    useEffect(() => {
        const h = location.hash || '';
        if (!h || !/^#comment-/i.test(h)) return;
        if (activeTab !== 'comments' || commentsLoading) return;
        const elId = h.slice(1);
        const frame = requestAnimationFrame(() => {
            document.getElementById(elId)?.scrollIntoView({ behavior: 'smooth', block: 'center' });
        });
        return () => cancelAnimationFrame(frame);
    }, [location.hash, activeTab, commentsLoading, comments.length]);

    // Load comment count sớm (silent) để tab hiển thị đúng số trước khi user click vào.
    useEffect(() => {
        if (storyId) loadComments({ silent: true });
    }, [storyId, loadComments]);

    const loadReviews = useCallback(() => {
        if (!storyId) return;
        setReviewsLoading(true);
        getStoryRatings(storyId)
            .then((list) => {
                const arr = Array.isArray(list) ? list : [];
                setReviews(arr);
                // Đồng bộ số đánh giá ở header (API story đôi khi trả TotalRatings không khớp)
                setStory((prev) => (prev ? { ...prev, totalRatings: arr.length } : prev));
            })
            .catch(() => {
                setReviews([]);
                setStory((prev) => (prev ? { ...prev, totalRatings: 0 } : prev));
            })
            .finally(() => setReviewsLoading(false));
    }, [storyId]);

    // Mỗi user chỉ được đánh giá 1 lần; BE cũng chặn. Dùng để ẩn nút "Đánh giá" và chặn mở modal.
    const { hasUserRated, userRatingStars } = useMemo(() => {
        const uid = user?.id;
        if (!uid) return { hasUserRated: false, userRatingStars: null };
        const mine = reviews.find((r) => String(r.userId ?? r.UserId ?? '') === String(uid));
        return {
            hasUserRated: !!mine,
            userRatingStars: mine != null ? Number(mine.starValue ?? mine.StarValue ?? 0) : null,
        };
    }, [user?.id, reviews]);

    const isStoryOwner = useMemo(() => {
        if (!user?.id || !story?.author) return false;
        const uid = String(user.id).toLowerCase();
        const aid = String(story.author?.id ?? story.author?.userId ?? '').toLowerCase();
        return !!aid && uid === aid;
    }, [user?.id, story?.author]);

    /** Đồng bộ với BE (READ_CHAPTER): có tiến độ đọc chương từ getStoryById. */
    const hasReadAnyChapter = Boolean(story?.lastReadChapterId);

    // Load đánh giá ngay khi có storyId để tab hiển thị đúng số (0) trước khi user click tab
    useEffect(() => {
        if (storyId) loadReviews();
    }, [storyId, loadReviews]);

    useEffect(() => {
        if (activeTab === 'reviews') setVisibleReviewsCount(3);
    }, [activeTab]);

    const handleAddComment = async (content, parentId) => {
        if (!storyId) return;
        if (story?.commentsDisabled) {
            setCommentError('Truyện này đang trong quá trình xử lý vi phạm nên hiện không thể bình luận.');
            return;
        }
        setCommentError(null);
        try {
            await addStoryComment(storyId, { content: content.trim(), parentId: parentId || undefined });
            loadComments();
            showToast('Đã gửi bình luận.', 'success');
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? 'Không thể gửi bình luận.';
            setCommentError(msg);
        }
    };

    const handleLikeComment = async (commentId) => {
        if (!storyId || !user?.id) return;
        try {
            const res = await toggleCommentLike(storyId, commentId);
            setComments((prev) =>
                prev.map((c) =>
                    (c.id === commentId || c.Id === commentId)
                        ? { ...c, userHasLiked: res.liked, likesCount: res.likesCount ?? res.likes_count ?? c.likesCount ?? c.likes_count ?? 0 }
                        : c
                )
            );
        } catch {
            // ignore
        }
    };


    const handleReportComment = (commentId) => {
        setReportingCommentId(commentId);
        setReportError(null);
        loadReportReasons('comment');
        setIsReportCommentModalOpen(true);
    };

    const handleSubmitRating = async (starValue, reviewText) => {
        if (!storyId) return;
        if (isStoryOwner) {
            setRatingError('Không thể tự đánh giá.');
            return;
        }
        if (!user?.id) {
            setRatingError('Vui lòng đăng nhập để đánh giá.');
            return;
        }
        setRatingError(null);
        setRatingSubmitting(true);
        try {
            const data = await rateStory(storyId, { starValue, reviewText });
            setStory((prev) => (prev ? { ...prev, rating: data.avgRating ?? data.avg, totalRatings: data.ratingCount ?? data.count ?? 0 } : prev));
            setIsRatingModalOpen(false);
            loadReviews();
            showToast('Đánh giá thành công!', 'success');
        } catch (err) {
            const status = err?.response?.status;
            const msg = err?.response?.data?.message ?? err?.message ?? 'Không thể gửi đánh giá.';
            if (status === 401) {
                setRatingError('Vui lòng đăng nhập để đánh giá.');
            } else {
                setRatingError(msg);
                // BE trả lỗi "đã đánh giá" khi user đánh giá lần 2 → đồng bộ lại danh sách để UI hiển thị "Bạn đã đánh giá"
                if (msg && (msg.includes('đã đánh giá') || msg.includes('đánh giá lại'))) {
                    setIsRatingModalOpen(false);
                    loadReviews();
                }
            }
        } finally {
            setRatingSubmitting(false);
        }
    };

    const handleOpenRating = () => {
        if (hasUserRated) return; // Mỗi user chỉ được đánh giá 1 lần
        if (isStoryOwner) {
            showToast('Không thể tự đánh giá.', 'warning');
            return;
        }
        if (!user?.id) {
            showToast('Vui lòng đăng nhập để đánh giá.', 'warning');
            return;
        }
        if (!hasReadAnyChapter) {
            showToast('Bạn cần đọc ít nhất 1 chương trước khi đánh giá.', 'warning');
            return;
        }
        setRatingError(null);
        setIsRatingModalOpen(true);
    };

    const handleOpenStoryReport = () => {
        if (!user?.id) {
            showToast('Vui lòng đăng nhập để báo cáo vi phạm.', 'warning');
            return;
        }
        setReportError(null);
        loadReportReasons('story');
        setIsReportStoryModalOpen(true);
    };

    const handleCloseRatingModal = () => {
        setIsRatingModalOpen(false);
        setRatingError(null);
    };

    const handleToggleFollow = async () => {
        if (!storyId) return;
        if (isStoryOwner) {
            showToast('Bạn không thể theo dõi truyện của chính mình.', 'warning');
            return;
        }
        if (!user?.id) {
            showToast('Vui lòng đăng nhập để theo dõi truyện.', 'warning');
            return;
        }
        try {
            if (isFollowing) {
                await unfollowStory(storyId);
                setIsFollowing(false);
                showToast('Đã bỏ theo dõi.', 'success');
            } else {
                await followStory(storyId);
                setIsFollowing(true);
                showToast('Đã theo dõi truyện. Bạn sẽ nhận thông báo khi có chương mới.', 'success');
            }
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? (isFollowing ? 'Không thể bỏ theo dõi.' : 'Không thể theo dõi.');
            showToast(msg, 'error');
        }
    };

    const handleSubmitCommentReport = async (payload) => {
        if (!storyId || !reportingCommentId) return false;
        setReportSubmitting(true);
        setReportError(null);
        try {
            const details = [
                payload?.description,
                payload?.evidenceLinks?.length ? `Link bằng chứng: ${payload.evidenceLinks.join(', ')}` : '',
                payload?.evidenceImages?.length ? `Ảnh bằng chứng: ${payload.evidenceImages.join(', ')}` : '',
            ].filter(Boolean).join('\n');

            await reportStoryComment(storyId, reportingCommentId, {
                reasonCode: payload?.reasonCode,
                description: details || undefined,
            });
            showToast('Đã gửi báo cáo. Chúng tôi sẽ xem xét trong thời gian sớm nhất.', 'success');
            return true;
        } catch (e) {
            const msg = e?.response?.data?.message ?? e?.message ?? 'Không gửi được báo cáo bình luận.';
            setReportError(msg);
            showToast(msg, 'error');
            return false;
        } finally {
            setReportSubmitting(false);
        }
    };

    const handleSubmitStoryReport = async (payload) => {
        if (!storyId) return false;
        if (!user?.id) {
            setReportError('Vui lòng đăng nhập để gửi báo cáo vi phạm.');
            return false;
        }
        if (!hasReadAnyChapter) {
            setReportError('Bạn cần đọc ít nhất 1 chương trước khi gửi báo cáo.');
            return false;
        }
        setReportSubmitting(true);
        setReportError(null);
        try {
            const details = [
                payload?.description,
                payload?.evidenceLinks?.length ? `Link bằng chứng: ${payload.evidenceLinks.join(', ')}` : '',
                payload?.evidenceImages?.length ? `Ảnh bằng chứng: ${payload.evidenceImages.join(', ')}` : '',
            ].filter(Boolean).join('\n');

            await reportStory(storyId, {
                reasonCode: payload?.reasonCode,
                description: details || undefined,
            });
            showToast('Đã gửi báo cáo. Chúng tôi sẽ xem xét trong thời gian sớm nhất.', 'success');
            return true;
        } catch (e) {
            const msg = e?.response?.data?.message ?? e?.message ?? 'Không gửi được báo cáo truyện.';
            setReportError(msg);
            showToast(msg, 'error');
            return false;
        } finally {
            setReportSubmitting(false);
        }
    };

    if (loading) {
        return (
            <div className="min-h-screen bg-slate-50 dark:bg-background-dark">
                <Header />
                <div className="max-w-[1280px] mx-auto px-4 py-12 text-center text-slate-500">Đang tải...</div>
                <Footer />
            </div>
        );
    }
    if (error || !story) {
        return (
            <div className="min-h-screen bg-slate-50 dark:bg-background-dark">
                <Header />
                <div className="max-w-[1280px] mx-auto px-4 py-12 text-center text-red-500">{error || 'Không tìm thấy truyện'}</div>
                <Footer />
            </div>
        );
    }

    return (
        <div className="min-h-screen bg-slate-50 dark:bg-background-dark">
            <Header />
            {/* Breadcrumb */}
            <div className="bg-white dark:bg-slate-900 border-b border-slate-200 dark:border-slate-800">
                <div className="max-w-[1280px] mx-auto px-4 py-3">
                    <div className="flex items-center gap-2 text-sm text-slate-600 dark:text-slate-400">
                        <a href="/home" className="hover:text-primary transition-colors">Trang chủ</a>
                        <ChevronRight className="w-4 h-4" />
                        <span className="text-slate-900 dark:text-white font-medium line-clamp-1">{story.title}</span>
                    </div>
                </div>
            </div>

            <div className="max-w-[1280px] mx-auto px-4 py-6">
                <div className="grid grid-cols-1 lg:grid-cols-12 gap-6">
                    {/* Main Content */}
                    <div className="lg:col-span-8">
                        {/* Story Header */}
                        <StoryHeader
                            story={story}
                            isFollowing={isFollowing}
                            onToggleFollow={handleToggleFollow}
                            onOpenRating={handleOpenRating}
                            hasUserRated={hasUserRated}
                            userRatingStars={userRatingStars}
                            isLoggedIn={!!user?.id}
                            hasReadAnyChapter={hasReadAnyChapter}
                            isStoryOwner={isStoryOwner}
                            onOpenReport={handleOpenStoryReport}
                            onReadStory={() => {
                                const first = chapters[0];
                                if (first?.chapterId && storyId) {
                                    navigate(`/chapter?storyId=${storyId}&chapterId=${first.chapterId}`);
                                } else if (storyId) {
                                    navigate(`/chapter?storyId=${storyId}`);
                                }
                            }}
                        />

                        {/* Tabs */}
                        <div className="bg-white dark:bg-slate-900 rounded-xl shadow-sm border border-slate-200 dark:border-slate-800 mt-6">
                            <div className="border-b border-slate-200 dark:border-slate-800">
                                <div className="flex gap-6 px-6">
                                    <button
                                        onClick={() => setActiveTab('chapters')}
                                        className={`py-4 border-b-2 font-semibold text-sm transition-colors ${activeTab === 'chapters'
                                            ? 'border-primary text-primary'
                                            : 'border-transparent text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                                            }`}
                                    >
                                        Danh sách chương ({story.chapters})
                                    </button>
                                    <button
                                        onClick={() => setActiveTab('comments')}
                                        className={`py-4 border-b-2 font-semibold text-sm transition-colors ${activeTab === 'comments'
                                            ? 'border-primary text-primary'
                                            : 'border-transparent text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                                            }`}
                                    >
                                        Bình luận ({comments.length.toLocaleString()})
                                    </button>
                                    <button
                                        onClick={() => setActiveTab('reviews')}
                                        className={`py-4 border-b-2 font-semibold text-sm transition-colors ${activeTab === 'reviews'
                                            ? 'border-primary text-primary'
                                            : 'border-transparent text-slate-600 dark:text-slate-400 hover:text-slate-900 dark:hover:text-white'
                                            }`}
                                    >
                                        Đánh giá ({(reviews.length > 0 ? reviews.length : (story.totalRatings ?? 0)).toLocaleString()})
                                    </button>
                                </div>
                            </div>

                            <div className="p-6">
                                {activeTab === 'chapters' && <ChapterList chapters={chapters} storyId={storyId} lastReadChapterId={story.lastReadChapterId} />}

                                {activeTab === 'comments' && (
                                    <CommentSection
                                        storyId={storyId}
                                        comments={comments}
                                        isLoggedIn={!!user?.id}
                                        commentsDisabled={!!story?.commentsDisabled}
                                        commentError={commentError}
                                        commentsLoading={commentsLoading}
                                        onSubmitComment={handleAddComment}
                                        onLikeComment={handleLikeComment}
                                        onReportComment={handleReportComment}
                                        formatTimeAgo={formatTimeAgo}
                                    />
                                )}

                                {activeTab === 'reviews' && (
                                    <>
                                        {reviewsLoading ? (
                                            <p className="text-slate-500 dark:text-slate-400 text-sm py-4">Đang tải đánh giá...</p>
                                        ) : reviews.length === 0 ? (
                                            <div className="text-center py-12">
                                                <Star className="w-12 h-12 text-slate-300 dark:text-slate-700 mx-auto mb-4" />
                                                <p className="text-slate-500 dark:text-slate-400">Chưa có đánh giá nào</p>
                                            </div>
                                        ) : (
                                            <>
                                                <div className="space-y-4">
                                                    {reviews.slice(0, visibleReviewsCount).map((r) => {
                                                        const name = r.userDisplayName ?? r.UserDisplayName ?? 'Ẩn danh';
                                                        const stars = Number(r.starValue ?? r.StarValue ?? 0);
                                                        const text = r.reviewText ?? r.ReviewText ?? '';
                                                        const createdAt = r.createdAt ?? r.CreatedAt;
                                                        return (
                                                            <div key={r.id ?? r.Id} className="flex gap-3 p-4 bg-slate-50 dark:bg-slate-800 rounded-lg">
                                                                <div className="w-10 h-10 rounded-full bg-primary/20 shrink-0 flex items-center justify-center text-primary font-bold text-sm">
                                                                    {(name || '?').charAt(0).toUpperCase()}
                                                                </div>
                                                                <div className="flex-1 min-w-0">
                                                                    <div className="flex items-center gap-2 flex-wrap">
                                                                        <span className="font-semibold text-slate-900 dark:text-white text-sm">{name}</span>
                                                                        <span className="flex items-center gap-0.5">
                                                                            {[1, 2, 3, 4, 5].map((i) => (
                                                                                <Star key={i} className={`w-4 h-4 ${i <= stars ? 'fill-amber-400 text-amber-400' : 'text-slate-300 dark:text-slate-600'}`} />
                                                                            ))}
                                                                        </span>
                                                                        {createdAt && (
                                                                            <span className="text-xs text-slate-500 dark:text-slate-400">{formatTimeAgo(createdAt)}</span>
                                                                        )}
                                                                    </div>
                                                                    {text && <p className="text-slate-600 dark:text-slate-400 text-sm mt-1 whitespace-pre-wrap">{text}</p>}
                                                                </div>
                                                            </div>
                                                        );
                                                    })}
                                                </div>
                                                <div className="flex flex-wrap gap-3 mt-4">
                                                    {reviews.length > visibleReviewsCount && (
                                                        <button
                                                            type="button"
                                                            onClick={() => setVisibleReviewsCount((n) => n + 3)}
                                                            className="text-sm text-primary hover:underline"
                                                        >
                                                            Xem thêm đánh giá ({reviews.length - visibleReviewsCount})
                                                        </button>
                                                    )}
                                                    {visibleReviewsCount > 3 && (
                                                        <button
                                                            type="button"
                                                            onClick={() => setVisibleReviewsCount((n) => Math.max(3, n - 3))}
                                                            className="text-sm text-slate-500 dark:text-slate-400 hover:underline"
                                                        >
                                                            Ẩn bớt đánh giá
                                                        </button>
                                                    )}
                                                </div>
                                            </>
                                        )}
                                    </>
                                )}
                            </div>
                        </div>
                    </div>

                    {/* Sidebar */}
                    <div className="lg:col-span-4">
                        <div className="sticky top-20 space-y-6">
                            <AuthorCard author={story.author} />
                        </div>
                    </div>
                </div>

                {/* Related Stories Section - Full Width */}
                {relatedStoriesData.length > 0 && (
                    <div className="mt-10">
                        <RelatedStories stories={relatedStoriesData} />
                    </div>
                )}
            </div>

            {/* Modals */}
            <RatingModal
                isOpen={isRatingModalOpen}
                onClose={handleCloseRatingModal}
                onSubmit={handleSubmitRating}
                errorMessage={ratingError}
                submitting={ratingSubmitting}
            />

            <ReportModal
                isOpen={isReportCommentModalOpen}
                onClose={() => { setIsReportCommentModalOpen(false); setReportingCommentId(null); setReportError(null); }}
                onSubmit={handleSubmitCommentReport}
                title="Báo cáo bình luận"
                type="comment"
                targetId={reportingCommentId}
                reasonOptions={reportReasonOptions.comment}
                submitting={reportSubmitting}
                errorMessage={reportError}
                onClearError={() => setReportError(null)}
            />

            <ReportModal
                isOpen={isReportStoryModalOpen}
                onClose={() => { setIsReportStoryModalOpen(false); setReportError(null); }}
                onSubmit={handleSubmitStoryReport}
                title="Báo cáo truyện"
                type="story"
                storyId={storyId}
                storyTitle={story?.title}
                reasonOptions={reportReasonOptions.story}
                submitting={reportSubmitting}
                errorMessage={reportError}
                onClearError={() => setReportError(null)}
            />
            <ToastContainer />
            <Footer />
        </div>
    );
}
