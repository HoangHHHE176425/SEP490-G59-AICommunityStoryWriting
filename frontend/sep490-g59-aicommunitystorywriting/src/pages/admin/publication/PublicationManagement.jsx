import { useState, useEffect, useCallback, useRef } from 'react';
import { PublicationList } from '../../../components/admin/publication/PublicationList';
import { PublicationDetailModal } from '../../../components/admin/publication/PublicationDetailModal';
import { Pagination } from '../../../components/pagination/Pagination';
import { getStories, getStoryById } from '../../../api/story/storyApi';
import { getPendingStories, getPendingChapters, getModeratorReviewedStories, getModeratorReviewedChapters, getRejectedChapterVersionsHistory, claimStory, claimChapter, submitReviewEscalation } from '../../../api/moderator/moderatorApi';
import { getProfileByUserId } from '../../../api/account/accountApi';
import { reviewDeadlineAfterDaysUtc, localDateTimeInputToIsoUtc, worstTimeStatus } from '../../../utils/moderatorReviewSla';
import { createModeratorHubConnection } from '../../../api/moderator/moderatorHub';
import { resolveBackendUrl } from '../../../utils/resolveBackendUrl';
import { getActivePolicy } from '../../../api/policy/policyApi';
import { PolicyBody } from '../../../components/policy/PolicyBody';

/** Bổ sung ageRating từ GET /stories/:id cho publication có storyId nhưng chưa có ageRating (tránh lỗi hiển thị "—" khi API danh sách không trả hoặc trả null). */
async function enrichAgeRatingFromStory(list) {
    const needEnrich = (p) => (p.storyId ?? p.id) && !(p.ageRating ?? p.age_rating);
    const ids = [...new Set(list.filter(needEnrich).map((p) => p.storyId ?? p.id).filter(Boolean))];
    if (ids.length === 0) return list;
    const storyMap = {};
    await Promise.all(ids.map((id) => getStoryById(id).then((s) => { storyMap[id] = s; }).catch(() => { })));
    return list.map((p) => {
        const id = p.storyId ?? p.id;
        const story = id ? storyMap[id] : null;
        const rating = story ? (story.ageRating ?? story.AgeRating ?? null) : null;
        if (!rating) return p;
        return { ...p, ageRating: rating };
    });
}

/** Gọi API profile theo authorId để lấy tên tác giả khi author đang N/A. */
async function enrichPublicationsWithAuthorProfile(list) {
    const needEnrich = (pub) => {
        const id = pub.authorId ?? pub.author_id;
        const name = pub.author;
        return id && (!name || name === 'N/A' || String(name).trim() === '');
    };
    const ids = [...new Set(list.filter(needEnrich).map((p) => p.authorId ?? p.author_id).filter(Boolean))];
    if (ids.length === 0) return list;
    const profileMap = {};
    await Promise.all(ids.map((id) => getProfileByUserId(id).then((profile) => { profileMap[id] = profile; }).catch(() => { })));
    return list.map((p) => {
        const id = p.authorId ?? p.author_id;
        const profile = id ? profileMap[id] : null;
        if (!profile) return p;
        return { ...p, author: profile.displayName ?? p.author };
    });
}

/** Map API story item sang format publication cho PublicationList / PublicationDetailModal */
function mapStoryToPublication(item) {
    const statusApi = (item.status ?? item.Status ?? '').toUpperCase();
    const statusMap = {
        PENDING_REVIEW: 'pending',
        PUBLISHED: 'approved',
        REJECTED: 'rejected',
    };
    const status = statusMap[statusApi] ?? 'pending';
    const categoryNamesStr = item.categoryNames ?? item.CategoryNames ?? '';
    const categoryNamesArr = categoryNamesStr
        ? String(categoryNamesStr).split(',').map((s) => s.trim()).filter(Boolean)
        : [];
    const coverPath = item.coverImage ?? item.CoverImage;
    const storyId = item.id ?? item.Id;
    return {
        id: storyId,
        storyId,
        storyTitle: item.title ?? item.Title ?? '',
        storyCover: coverPath ? resolveBackendUrl(coverPath) : '',
        author: item.authorName ?? item.AuthorName ?? 'N/A',
        authorId: item.authorId ?? item.AuthorId ?? null,
        type: 'new_story',
        status,
        submittedAt: item.createdAt ?? item.CreatedAt ?? item.updatedAt ?? item.UpdatedAt ?? null,
        reviewedAt: null,
        reviewedBy: null,
        rejectionReason: item.rejectionReason ?? item.RejectionReason ?? null,
        chapters: [],
        totalChapters: item.totalChapters ?? item.TotalChapters ?? 0,
        totalWords: 0,
        categories: categoryNamesArr,
        description: item.summary ?? item.Summary ?? '',
        ageRating: item.ageRating ?? item.AgeRating ?? null,
    };
}

/** Lấy cover, author, authorId, ageRating, categories, description từ GET /stories/:id. */
function storyResponseToMeta(story) {
    if (!story) return { storyCover: '', author: 'N/A', authorId: null, ageRating: null, categories: [], description: '' };
    const categoryNamesStr = story.categoryNames ?? story.CategoryNames ?? '';
    const categoryNamesArr = categoryNamesStr ? String(categoryNamesStr).split(',').map((s) => s.trim()).filter(Boolean) : [];
    const coverPath = story.coverImage ?? story.CoverImage;
    return {
        storyCover: coverPath ? resolveBackendUrl(coverPath) : '',
        author: story.authorName ?? story.AuthorName ?? 'N/A',
        authorId: story.authorId ?? story.AuthorId ?? null,
        ageRating: story.ageRating ?? story.AgeRating ?? 'ALL',
        categories: categoryNamesArr,
        description: story.summary ?? story.Summary ?? '',
    };
}

const STATUS_PARAM_MAP = {
    pending: 'PENDING_REVIEW',
    approved: 'PUBLISHED',
    rejected: 'REJECTED',
    all: null,
};

const PAGE_SIZE = 10;
/** Backend: MinHoursUntilDeadline = 24, MaxDeadlineDaysAhead = 366 */
const MIN_CLAIM_DEADLINE_HOURS = 25;
const MAX_CLAIM_DEADLINE_DAYS = 366;

/** Giá trị cho input datetime-local (giờ local). */
function toDatetimeLocalValue(d) {
    const pad = (n) => String(n).padStart(2, '0');
    return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function getDefaultDatetimeLocalForClaim() {
    const d = new Date();
    d.setTime(d.getTime() + 48 * 60 * 60 * 1000);
    return toDatetimeLocalValue(d);
}

function getMinDatetimeLocalForClaim() {
    const d = new Date();
    d.setTime(d.getTime() + MIN_CLAIM_DEADLINE_HOURS * 60 * 60 * 1000);
    return toDatetimeLocalValue(d);
}

function getMaxDatetimeLocalForClaim() {
    const d = new Date();
    d.setTime(d.getTime() + MAX_CLAIM_DEADLINE_DAYS * 24 * 60 * 60 * 1000);
    return toDatetimeLocalValue(d);
}

/** @returns {string|null} lỗi tiếng Việt hoặc null */
function validateClaimDeadlineLocal(datetimeLocalValue) {
    if (!datetimeLocalValue) return 'Vui lòng chọn hạn hoàn thành duyệt.';
    const picked = new Date(datetimeLocalValue);
    if (Number.isNaN(picked.getTime())) return 'Thời gian không hợp lệ.';
    const min = new Date();
    min.setTime(min.getTime() + 24 * 60 * 60 * 1000);
    const max = new Date();
    max.setTime(max.getTime() + MAX_CLAIM_DEADLINE_DAYS * 24 * 60 * 60 * 1000);
    if (picked <= min) return 'Hạn duyệt phải sau ít nhất 24 giờ kể từ bây giờ.';
    if (picked > max) return `Hạn duyệt không được vượt quá ${MAX_CLAIM_DEADLINE_DAYS} ngày.`;
    return null;
}

/** Backend khuyến nghị: load lại danh sách duyệt/từ chối mỗi 30 giây để cập nhật khi có thay đổi từ nơi khác */
const REFRESH_INTERVAL_MS = 30 * 1000;

/** Gộp thông báo admin từ chối đơn hủy nhận duyệt (mới nhất theo thời gian). */
function pickGroupAdminRejectedRelease(chapters, storyItem) {
    const items = [];
    if (storyItem) items.push(storyItem);
    if (Array.isArray(chapters)) items.push(...chapters);
    let bestTime = -Infinity;
    let bestNote = null;
    let bestCurrentFlag = null;
    for (const it of items) {
        const note = it.adminRejectedReleaseNote ?? it.AdminRejectedReleaseNote ?? null;
        const atStr = it.adminRejectedReleaseAt ?? it.AdminRejectedReleaseAt;
        const currentFlagRaw = it.isCurrentClaimRejection ?? it.IsCurrentClaimRejection;
        const hasCurrentFlag = typeof currentFlagRaw === 'boolean';
        const isCurrentClaimRejection = hasCurrentFlag ? Boolean(currentFlagRaw) : null;
        const claimedAtStr = it.claimedAt ?? it.ClaimedAt ?? null;
        const t = atStr ? new Date(atStr).getTime() : NaN;
        const claimedAt = claimedAtStr ? new Date(claimedAtStr).getTime() : NaN;
        // Ưu tiên cờ BE trả về. Nếu BE chưa có cờ thì fallback theo mốc claim hiện tại.
        if (hasCurrentFlag) {
            if (!isCurrentClaimRejection) continue;
        } else if (Number.isFinite(t) && Number.isFinite(claimedAt) && t < claimedAt) {
            continue;
        }
        const hasSignal = Number.isFinite(t) || (note != null && String(note).trim() !== '');
        if (!hasSignal) continue;
        const sortKey = Number.isFinite(t) ? t : 0;
        if (sortKey >= bestTime) {
            bestTime = sortKey;
            bestNote = note;
            bestCurrentFlag = hasCurrentFlag ? Boolean(currentFlagRaw) : null;
        }
    }
    if (bestTime === -Infinity && (bestNote == null || String(bestNote).trim() === '')) return {};
    return {
        adminRejectedReleaseNote: bestNote != null && String(bestNote).trim() !== '' ? String(bestNote).trim() : null,
        adminRejectedReleaseAt: bestTime > -Infinity ? new Date(bestTime).toISOString() : null,
        isCurrentClaimRejection: bestCurrentFlag,
    };
}

/** Gộp thông báo admin từ chối đơn xin gia hạn (EXTEND_DEADLINE). */
function pickGroupAdminRejectedExtend(chapters, storyItem) {
    const items = [];
    if (storyItem) items.push(storyItem);
    if (Array.isArray(chapters)) items.push(...chapters);
    let bestTime = -Infinity;
    let bestNote = null;
    for (const it of items) {
        const note = it.adminRejectedExtendNote ?? it.AdminRejectedExtendNote ?? null;
        const atStr = it.adminRejectedExtendAt ?? it.AdminRejectedExtendAt;
        const t = atStr ? new Date(atStr).getTime() : NaN;
        const hasSignal = Number.isFinite(t) || (note != null && String(note).trim() !== '');
        if (!hasSignal) continue;
        const sortKey = Number.isFinite(t) ? t : 0;
        if (sortKey >= bestTime) {
            bestTime = sortKey;
            bestNote = note;
        }
    }
    if (bestTime === -Infinity && (bestNote == null || String(bestNote).trim() === '')) return {};
    return {
        adminRejectedExtendNote: bestNote != null && String(bestNote).trim() !== '' ? String(bestNote).trim() : null,
        adminRejectedExtendAt: bestTime > -Infinity ? new Date(bestTime).toISOString() : null,
    };
}

/** Map item từ moderator/stories/pending sang format dùng chung (type story). Dùng trạng thái thật từ API để khi duyệt chương không gọi approveStory nếu truyện đã PUBLISHED. */
function mapPendingStoryToItem(s) {
    const id = s.id ?? s.Id;
    const coverPath = s.coverImage ?? s.CoverImage;
    const categoryNamesStr = s.categoryNames ?? s.CategoryNames ?? '';
    const categoryNamesArr = categoryNamesStr ? String(categoryNamesStr).split(',').map((x) => x.trim()).filter(Boolean) : [];
    const statusApi = (s.status ?? s.Status ?? '').toUpperCase();
    const statusMap = { PENDING_REVIEW: 'pending', PUBLISHED: 'approved', REJECTED: 'rejected' };
    const status = statusMap[statusApi] ?? 'pending';
    return {
        id,
        storyId: id,
        type: 'story',
        storyTitle: s.title ?? s.Title ?? '',
        storyCover: coverPath ? resolveBackendUrl(coverPath) : '',
        author: s.authorName ?? s.AuthorName ?? 'N/A',
        authorId: s.authorId ?? s.AuthorId ?? null,
        status,
        submittedAt: s.createdAt ?? s.CreatedAt ?? s.updatedAt ?? s.UpdatedAt ?? null,
        totalChapters: s.totalChapters ?? s.TotalChapters ?? 0,
        categories: categoryNamesArr,
        description: s.summary ?? s.Summary ?? '',
        isClaimedByMe: s.isClaimedByMe ?? s.IsClaimedByMe ?? false,
        claimedByDisplayName: s.claimedByDisplayName ?? s.ClaimedByDisplayName ?? null,
        claimedAt: s.claimedAt ?? s.ClaimedAt ?? null,
        pendingSince: s.pendingSince ?? s.PendingSince ?? null,
        timeStatus: s.timeStatus ?? s.TimeStatus ?? null,
        hasPendingEscalation: s.hasPendingEscalation ?? s.HasPendingEscalation ?? false,
        adminRejectedReleaseNote: s.adminRejectedReleaseNote ?? s.AdminRejectedReleaseNote ?? null,
        adminRejectedReleaseAt: s.adminRejectedReleaseAt ?? s.AdminRejectedReleaseAt ?? null,
        isCurrentClaimRejection: s.isCurrentClaimRejection ?? s.IsCurrentClaimRejection ?? null,
        adminRejectedExtendNote: s.adminRejectedExtendNote ?? s.AdminRejectedExtendNote ?? null,
        adminRejectedExtendAt: s.adminRejectedExtendAt ?? s.AdminRejectedExtendAt ?? null,
    };
}

/** Map chapter từ API reviewed (REJECTED/PUBLISHED) sang format dùng chung. */
function mapReviewedChapterToItem(c) {
    const id = c.id ?? c.Id;
    const storyId = c.storyId ?? c.StoryId;
    const statusApi = (c.status ?? c.Status ?? '').toUpperCase();
    const statusMap = { PENDING_REVIEW: 'pending', PUBLISHED: 'approved', REJECTED: 'rejected' };
    return {
        id,
        chapterId: id,
        storyId,
        type: 'chapter',
        storyTitle: c.storyTitle ?? c.StoryTitle ?? '',
        storyCover: '',
        chapterTitle: c.title ?? c.Title ?? '',
        orderIndex: c.orderIndex ?? c.OrderIndex ?? 0,
        status: statusMap[statusApi] ?? 'rejected',
        submittedAt: c.createdAt ?? c.CreatedAt ?? null,
        wordCount: c.wordCount ?? c.WordCount ?? 0,
        rejectionReason: c.rejectionReason ?? c.RejectionReason ?? null,
        rejectedAt: c.rejectedAt ?? c.RejectedAt ?? null,
    };
}

/** Map version bị từ chối (history) sang format dùng chung (type chapter). */
function mapRejectedVersionToItem(v) {
    const id = v.id ?? v.Id;
    const chapterId = v.chapterId ?? v.ChapterId;
    const storyId = v.storyId ?? v.StoryId;
    const versionNumber = Number(v.versionNumber ?? v.VersionNumber ?? 0) || 0;
    const chapterTitle = v.chapterTitle ?? v.ChapterTitle ?? '';
    const titleSnapshot = v.titleSnapshot ?? v.TitleSnapshot ?? '';
    const label = versionNumber > 0 ? `Phiên bản #${versionNumber}` : 'Phiên bản';
    return {
        id,
        chapterId: chapterId ?? id,
        versionId: id,
        versionNumber,
        storyId,
        type: 'chapter',
        storyTitle: v.storyTitle ?? v.StoryTitle ?? '',
        storyCover: '',
        chapterTitle: `${chapterTitle}${chapterTitle ? ' — ' : ''}${label}${titleSnapshot ? `: ${titleSnapshot}` : ''}`,
        orderIndex: v.chapterOrderIndex ?? v.ChapterOrderIndex ?? 0,
        status: 'rejected',
        submittedAt: null,
        wordCount: v.wordCount ?? v.WordCount ?? 0,
        rejectionReason: v.rejectionReason ?? v.RejectionReason ?? null,
        rejectedAt: v.rejectedAt ?? v.RejectedAt ?? null,
        isVersionHistory: true,
    };
}

/** Map item từ moderator/chapters/pending sang format dùng chung (type chapter). */
function mapPendingChapterToItem(c) {
    const id = c.id ?? c.Id;
    const storyId = c.storyId ?? c.StoryId;
    const statusApi = (c.status ?? c.Status ?? '').toUpperCase();
    const isEditRequest = statusApi === 'PUBLISHED'; // Chương đã xuất bản, có version gửi chỉnh sửa (vd: sau báo cáo vi phạm)
    return {
        id,
        chapterId: id,
        storyId,
        type: 'chapter',
        storyTitle: c.storyTitle ?? c.StoryTitle ?? '',
        storyCover: '',
        chapterTitle: c.title ?? c.Title ?? '',
        orderIndex: c.orderIndex ?? c.OrderIndex ?? 0,
        author: null,
        authorId: null,
        status: 'pending',
        submittedAt: c.createdAt ?? c.CreatedAt ?? null,
        totalChapters: null,
        categories: [],
        wordCount: c.wordCount ?? c.WordCount ?? 0,
        isClaimedByMe: c.isClaimedByMe ?? c.IsClaimedByMe ?? false,
        claimedByDisplayName: c.claimedByDisplayName ?? c.ClaimedByDisplayName ?? null,
        claimedAt: c.claimedAt ?? c.ClaimedAt ?? null,
        isEditRequest,
        pendingSince: c.pendingSince ?? c.PendingSince ?? null,
        timeStatus: c.timeStatus ?? c.TimeStatus ?? null,
        hasPendingEscalation: c.hasPendingEscalation ?? c.HasPendingEscalation ?? false,
        adminRejectedReleaseNote: c.adminRejectedReleaseNote ?? c.AdminRejectedReleaseNote ?? null,
        adminRejectedReleaseAt: c.adminRejectedReleaseAt ?? c.AdminRejectedReleaseAt ?? null,
        isCurrentClaimRejection: c.isCurrentClaimRejection ?? c.IsCurrentClaimRejection ?? null,
        adminRejectedExtendNote: c.adminRejectedExtendNote ?? c.AdminRejectedExtendNote ?? null,
        adminRejectedExtendAt: c.adminRejectedExtendAt ?? c.AdminRejectedExtendAt ?? null,
    };
}

export function PublicationManagement({ initialFilterStatus = 'pending' }) {
    const [selectedPublication, setSelectedPublication] = useState(null);
    const [filterStatus, setFilterStatus] = useState(initialFilterStatus); // 'pending' | 'approved' | 'rejected'
    const [publications, setPublications] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [statsData, setStatsData] = useState({ pending: 0, approved: 0, rejected: 0, total: 0 });
    const [currentPage, setCurrentPage] = useState(1);
    const [totalPages, setTotalPages] = useState(1);
    const [totalCount, setTotalCount] = useState(0);
    const [claimingId, setClaimingId] = useState(null); // id đang gọi claim
    const [releasingAllClaimsStoryId, setReleasingAllClaimsStoryId] = useState(null);
    /** Popup xác nhận hủy nhận duyệt (thay window.confirm). */
    const [releaseConfirmTarget, setReleaseConfirmTarget] = useState(null); // { storyId, storyTitle, chapterCount }
    /** Thông báo sau khi gửi đơn / lỗi. */
    const [releaseResultMessage, setReleaseResultMessage] = useState(null);
    /** Lý do gửi đơn RELEASE_ASSIGNMENT cấp truyện (tối thiểu 10 ký tự). */
    const [releaseReason, setReleaseReason] = useState('');
    /** Lỗi trong popup gửi đơn (validation / API). */
    const [releaseFormError, setReleaseFormError] = useState('');
    const [showClaimModal, setShowClaimModal] = useState(false); // modal "Chọn truyện để nhận duyệt"
    const [claimConfirmTarget, setClaimConfirmTarget] = useState(null); // { type: 'story', id, title } khi cần popup xác nhận
    /** Hạn hoàn thành duyệt (datetime-local) khi xác nhận trong popup — gửi API reviewDeadlineAt (ISO UTC). */
    const [claimReviewDeadlineLocal, setClaimReviewDeadlineLocal] = useState('');
    /** Modal "Nhận duyệt đơn": danh sách truyện + chương chưa nhận (type 'story' | 'chapter') */
    const [claimModalItems, setClaimModalItems] = useState([]);
    const [claimModalLoading, setClaimModalLoading] = useState(false);
    /** Số đơn chưa nhận (unclaimed) — hiển thị bên cạnh "Nhận duyệt đơn" để moderator biết. */
    const [unclaimedCount, setUnclaimedCount] = useState(0);
    /** Cache Đã duyệt / Từ chối để vừa vào màn đã load sẵn, chuyển tab thấy ngay. */
    const [approvedCache, setApprovedCache] = useState({ items: [], total: 0, totalPages: 1 });
    const [rejectedCache, setRejectedCache] = useState({ items: [], total: 0, totalPages: 1 });
    const [approvedCacheLoading, setApprovedCacheLoading] = useState(false);
    const [rejectedCacheLoading, setRejectedCacheLoading] = useState(false);
    /** Popup nhận duyệt: chọn hạn (7/14 ngày hoặc tùy chỉnh) + cam kết */
    const [claimDeadlineChoice, setClaimDeadlineChoice] = useState('7'); // '7' | '14' | 'custom'
    const [claimCustomDeadline, setClaimCustomDeadline] = useState('');
    const [claimCommitted, setClaimCommitted] = useState(false);
    const [modalClaimBusy, setModalClaimBusy] = useState(false);
    const [policyModalOpen, setPolicyModalOpen] = useState(false);
    const [policyLoading, setPolicyLoading] = useState(false);
    const [policyError, setPolicyError] = useState('');
    const [authorPolicy, setAuthorPolicy] = useState(null);
    const [processModalOpen, setProcessModalOpen] = useState(false);

    // Khi dashboard MODERATOR bấm nút, thay đổi tab ở màn publication ngay lập tức.
    useEffect(() => {
        if (initialFilterStatus) setFilterStatus(initialFilterStatus);
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, [initialFilterStatus]);

    /** Modal "Nhận duyệt đơn": gộp theo truyện — mỗi truyện 1 dòng; nhận 1 lần = claim truyện (nếu chưa) + tất cả chương chờ duyệt của truyện đó. */
    const loadClaimModalItems = useCallback(() => {
        setClaimModalLoading(true);
        Promise.all([
            getPendingStories({ claimFilter: 'UNCLAIMED', pageSize: 100 }),
            getPendingChapters({ claimFilter: 'UNCLAIMED', pageSize: 100 }),
            getPendingStories({ claimFilter: 'all', pageSize: 200 })
        ])
            .then(([storiesRes, chaptersRes, allStoriesRes]) => {
                const storyItems = storiesRes?.items ?? storiesRes?.Items ?? [];
                const chapterItems = chaptersRes?.items ?? chaptersRes?.Items ?? [];
                const allStoryItems = allStoriesRes?.items ?? allStoriesRes?.Items ?? [];
                const stories = storyItems.map(mapPendingStoryToItem).filter((s) => s.status === 'pending');
                const allStoriesMap = new Map(allStoryItems.map((s) => [s.id ?? s.Id, mapPendingStoryToItem(s)]));
                const chapters = chapterItems.map((c) => {
                    const ch = mapPendingChapterToItem(c);
                    const story = allStoriesMap.get(ch.storyId);
                    return {
                        ...ch,
                        storyCover: story?.storyCover ?? '',
                        totalChapters: story?.totalChapters ?? null,
                    };
                });
                const unclaimedStoryIds = new Set(stories.map((s) => s.storyId ?? s.id));
                const chaptersByStory = new Map();
                for (const ch of chapters) {
                    const sid = ch.storyId;
                    if (!sid) continue;
                    if (!chaptersByStory.has(sid)) chaptersByStory.set(sid, []);
                    chaptersByStory.get(sid).push(ch);
                }
                const storyIdsWithUnclaimed = new Set([...unclaimedStoryIds, ...chaptersByStory.keys()]);
                const grouped = [];
                for (const storyId of storyIdsWithUnclaimed) {
                    const storyMeta = allStoriesMap.get(storyId);
                    const storyChapters = chaptersByStory.get(storyId) ?? [];
                    const isStoryUnclaimed = unclaimedStoryIds.has(storyId);
                    const storyTitle = storyMeta?.storyTitle ?? storyChapters[0]?.storyTitle ?? 'Truyện';
                    const storyCover = storyMeta?.storyCover ?? storyChapters[0]?.storyCover ?? '';
                    const chapterIds = storyChapters.map((c) => c.chapterId ?? c.id);
                    grouped.push({
                        _claimType: 'story_group',
                        _claimId: storyId,
                        storyId,
                        storyTitle,
                        storyCover: storyCover || '',
                        author: storyMeta?.author ?? 'N/A',
                        totalChapters: storyMeta?.totalChapters ?? storyChapters.length,
                        categories: storyMeta?.categories ?? [],
                        _chapterIds: chapterIds,
                        _isStoryUnclaimed: isStoryUnclaimed,
                        _chapterCount: chapterIds.length,
                    });
                }
                setClaimModalItems(grouped);
                // Lấy ảnh bìa từ GET /stories/:id để luôn đúng (tránh lỗi ảnh khi mở modal lần 2)
                Promise.all(grouped.map((g) => getStoryById(g.storyId).then((s) => s).catch(() => null)))
                    .then((storyDetails) => {
                        const enriched = grouped.map((g, i) => {
                            const story = storyDetails[i];
                            const coverPath = story?.coverImage ?? story?.CoverImage;
                            const cover = coverPath ? resolveBackendUrl(coverPath) : (g.storyCover || '');
                            return { ...g, storyCover: cover || g.storyCover };
                        });
                        setClaimModalItems(enriched);
                    })
                    .catch(() => { });
            })
            .catch(() => setClaimModalItems([]))
            .finally(() => setClaimModalLoading(false));
    }, []);

    /** Số đơn chưa nhận = số truyện có đơn chờ nhận (mỗi truyện tính 1 đơn, trùng với số dòng trong modal "Chọn truyện hoặc chương để nhận duyệt"). */
    const loadUnclaimedCount = useCallback(() => {
        Promise.all([
            getPendingStories({ claimFilter: 'UNCLAIMED', pageSize: 100 }),
            getPendingChapters({ claimFilter: 'UNCLAIMED', pageSize: 100 }),
        ])
            .then(([storiesRes, chaptersRes]) => {
                const storyItems = storiesRes?.items ?? storiesRes?.Items ?? [];
                const chapterItems = chaptersRes?.items ?? chaptersRes?.Items ?? [];
                const stories = storyItems.map(mapPendingStoryToItem).filter((s) => s.status === 'pending');
                const chapters = chapterItems.map(mapPendingChapterToItem);
                const norm = (id) => (id != null ? String(id).toLowerCase() : '');
                const unclaimedStoryIds = new Set(stories.map((s) => norm(s.storyId ?? s.id)).filter(Boolean));
                const chaptersByStory = new Map();
                for (const ch of chapters) {
                    const sid = norm(ch.storyId);
                    if (!sid) continue;
                    if (!chaptersByStory.has(sid)) chaptersByStory.set(sid, []);
                    chaptersByStory.get(sid).push(ch);
                }
                const storyIdsWithUnclaimed = new Set([...unclaimedStoryIds, ...chaptersByStory.keys()]);
                setUnclaimedCount(storyIdsWithUnclaimed.size);
            })
            .catch(() => setUnclaimedCount(0));
    }, []);

    /** Preload Đã duyệt: truyện PUBLISHED + chương PUBLISHED (gộp theo truyện) để hiển thị cả chương đã duyệt (ví dụ Chương 1 Conan). */
    const loadApprovedCache = useCallback(() => {
        setApprovedCacheLoading(true);
        Promise.all([
            getModeratorReviewedStories({ status: 'PUBLISHED', page: 1, pageSize: 500, sortBy: 'updated_at', sortOrder: 'desc' }),
            getModeratorReviewedChapters({ status: 'PUBLISHED', pageSize: 500, sortBy: 'updated_at', sortOrder: 'desc' }),
        ])
            .then(([storiesRes, chaptersRes]) => {
                const storyItems = storiesRes?.items ?? storiesRes?.Items ?? [];
                const chapterItems = chaptersRes?.items ?? chaptersRes?.Items ?? [];
                const storyPubs = Array.isArray(storyItems) ? storyItems.map(mapStoryToPublication) : [];
                const chapterList = Array.isArray(chapterItems) ? chapterItems.map(mapReviewedChapterToItem) : [];
                const byStory = new Map();
                for (const ch of chapterList) {
                    const sid = ch.storyId;
                    if (!sid) continue;
                    if (!byStory.has(sid)) byStory.set(sid, { storyId: sid, storyTitle: ch.storyTitle, storyCover: '', author: null, categories: [], chapters: [] });
                    byStory.get(sid).chapters.push(ch);
                }
                const storyIdsInStoryPubs = new Set(storyPubs.map((s) => s.storyId ?? s.id));
                const chapterGroups = [];
                for (const g of byStory.values()) {
                    if (storyIdsInStoryPubs.has(g.storyId)) continue;
                    const rep = g.chapters[0];
                    chapterGroups.push({
                        type: 'story_group',
                        id: g.storyId,
                        storyId: g.storyId,
                        storyTitle: g.storyTitle,
                        storyCover: g.storyCover || '',
                        author: g.author,
                        categories: g.categories || [],
                        description: '',
                        status: 'approved',
                        chapters: g.chapters,
                        representativePublication: rep,
                        chapterCount: g.chapters.length,
                    });
                }
                const total = storyPubs.length + chapterGroups.length;
                const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

                if (chapterGroups.length === 0) {
                    const combined = [...storyPubs, ...chapterGroups];
                    enrichAgeRatingFromStory(combined).then((withAge) => enrichPublicationsWithAuthorProfile(withAge).then((items) => {
                        setApprovedCache({ items, total, totalPages });
                        setStatsData((prev) => ({ ...prev, approved: total }));
                    }));
                    return;
                }
                Promise.all(chapterGroups.map((g) => getStoryById(g.storyId).catch(() => null)))
                    .then((storyDetails) => {
                        storyDetails.forEach((story, i) => {
                            const meta = storyResponseToMeta(story);
                            chapterGroups[i].storyCover = meta.storyCover;
                            chapterGroups[i].author = meta.author;
                            chapterGroups[i].authorId = meta.authorId ?? chapterGroups[i].authorId;
                            chapterGroups[i].ageRating = meta.ageRating ?? chapterGroups[i].ageRating;
                            chapterGroups[i].categories = meta.categories;
                            chapterGroups[i].description = meta.description;
                        });
                        const combined = [...storyPubs, ...chapterGroups];
                        return enrichAgeRatingFromStory(combined).then((withAge) => enrichPublicationsWithAuthorProfile(withAge).then((items) => {
                            setApprovedCache({ items, total, totalPages });
                            setStatsData((prev) => ({ ...prev, approved: total }));
                        }));
                    })
                    .catch(() => {
                        setApprovedCache({ items: [...storyPubs, ...chapterGroups], total, totalPages });
                        setStatsData((prev) => ({ ...prev, approved: total }));
                    });
            })
            .catch((err) => {
                console.warn('[PublicationManagement] loadApprovedCache failed:', err?.response?.status, err?.response?.data ?? err?.message);
            })
            .finally(() => setApprovedCacheLoading(false));
    }, []);

    /** Preload Từ chối: truyện REJECTED + chương REJECTED + version REJECTED (gộp theo truyện), cập nhật rejectedCache và stats. */
    const loadRejectedCache = useCallback(() => {
        setRejectedCacheLoading(true);
        Promise.all([
            getModeratorReviewedStories({ status: 'REJECTED', pageSize: 500, sortBy: 'updated_at', sortOrder: 'desc' }),
            getModeratorReviewedChapters({ status: 'REJECTED', pageSize: 500, sortBy: 'updated_at', sortOrder: 'desc' }),
            getRejectedChapterVersionsHistory(),
        ])
            .then(([storiesRes, chaptersRes, versionsRes]) => {
                const storyItems = storiesRes?.items ?? storiesRes?.Items ?? [];
                const chapterItems = chaptersRes?.items ?? chaptersRes?.Items ?? [];
                const storyPubs = Array.isArray(storyItems) ? storyItems.map(mapStoryToPublication) : [];
                const chapterList = Array.isArray(chapterItems) ? chapterItems.map(mapReviewedChapterToItem) : [];
                const versionList = Array.isArray(versionsRes) ? versionsRes.map(mapRejectedVersionToItem) : [];
                const byStory = new Map();
                for (const ch of [...chapterList, ...versionList]) {
                    const sid = ch.storyId;
                    if (!sid) continue;
                    if (!byStory.has(sid)) byStory.set(sid, { storyId: sid, storyTitle: ch.storyTitle, storyCover: '', author: null, categories: [], chapters: [] });
                    byStory.get(sid).chapters.push(ch);
                }
                const chapterGroups = [];
                for (const g of byStory.values()) {
                    const rep = g.chapters[0];
                    chapterGroups.push({
                        type: 'story_group',
                        id: g.storyId,
                        storyId: g.storyId,
                        storyTitle: g.storyTitle,
                        storyCover: g.storyCover || '',
                        author: g.author,
                        categories: g.categories || [],
                        description: '',
                        status: 'rejected',
                        chapters: g.chapters,
                        representativePublication: rep,
                        chapterCount: g.chapters.length,
                    });
                }
                const total = storyPubs.length + chapterGroups.length;
                const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

                if (chapterGroups.length === 0) {
                    const combined = [...storyPubs, ...chapterGroups];
                    enrichAgeRatingFromStory(combined).then((withAge) => enrichPublicationsWithAuthorProfile(withAge).then((items) => {
                        setRejectedCache({ items, total, totalPages });
                        setStatsData((prev) => ({ ...prev, rejected: total }));
                    }));
                    return;
                }
                Promise.all(chapterGroups.map((g) => getStoryById(g.storyId).catch(() => null)))
                    .then((storyDetails) => {
                        storyDetails.forEach((story, i) => {
                            const meta = storyResponseToMeta(story);
                            chapterGroups[i].storyCover = meta.storyCover;
                            chapterGroups[i].author = meta.author;
                            chapterGroups[i].authorId = meta.authorId ?? chapterGroups[i].authorId;
                            chapterGroups[i].ageRating = meta.ageRating ?? chapterGroups[i].ageRating;
                            chapterGroups[i].categories = meta.categories;
                            chapterGroups[i].description = meta.description;
                        });
                        const combined = [...storyPubs, ...chapterGroups];
                        return enrichAgeRatingFromStory(combined).then((withAge) => enrichPublicationsWithAuthorProfile(withAge).then((items) => {
                            setRejectedCache({ items, total, totalPages });
                            setStatsData((prev) => ({ ...prev, rejected: total }));
                        }));
                    })
                    .catch(() => {
                        setRejectedCache({ items: [...storyPubs, ...chapterGroups], total, totalPages });
                        setStatsData((prev) => ({ ...prev, rejected: total }));
                    });
            })
            .catch((err) => {
                console.warn('[PublicationManagement] loadRejectedCache failed:', err?.response?.status, err?.response?.data ?? err?.message);
            })
            .finally(() => setRejectedCacheLoading(false));
    }, []);

    const loadPublications = useCallback((page = 1, options = {}) => {
        const silent = options.silent === true;
        if (!silent) {
            setLoading(true);
            setError(null);
        }

        if (filterStatus === 'pending') {
            // Tab Chờ duyệt: chỉ lấy đơn đã được moderator hiện tại nhận (CLAIMED) để luôn thấy đúng danh sách sau khi nhận duyệt.
            const claimFilterParam = 'CLAIMED';
            Promise.all([
                getPendingStories({ pageSize: 500, claimFilter: claimFilterParam, sortBy: 'deadline_at', sortOrder: 'asc' }),
                getPendingChapters({ pageSize: 500, claimFilter: claimFilterParam, sortBy: 'deadline_at', sortOrder: 'asc' })
            ])
                .then(([storiesRes, chaptersRes]) => {
                    const storyItems = storiesRes?.items ?? storiesRes?.Items ?? [];
                    const chapterItems = chaptersRes?.items ?? chaptersRes?.Items ?? [];
                    const storyList = storyItems.map(mapPendingStoryToItem);
                    let chapterList = chapterItems.map(mapPendingChapterToItem);
                    // Gắn ảnh bìa truyện + mô tả + tác giả + thể loại cho chương (từ truyện cùng storyId)
                    const storyById = new Map(storyList.map((s) => [s.storyId ?? s.id, s]));
                    chapterList = chapterList.map((ch) => {
                        const story = storyById.get(ch.storyId);
                        return {
                            ...ch,
                            storyCover: story?.storyCover ?? '',
                            description: story?.description ?? '',
                            author: story?.author ?? ch.author,
                            categories: (story?.categories ?? ch.categories) || [],
                        };
                    });
                    // Tab Chờ duyệt chỉ hiển thị item đang thực sự chờ duyệt (pending). API CLAIMED đã trả về chỉ đơn đã nhận.
                    const combined = [...storyList, ...chapterList].filter((p) => p.status === 'pending');
                    const norm = (id) => (id != null ? String(id).toLowerCase() : '');
                    // Nếu đã có chương của một truyện trong list thì không hiện dòng truyện (tránh 2 phần cùng truyện).
                    const storyIdsWithChapters = new Set(
                        combined.filter((p) => p.type === 'chapter').map((p) => norm(p.storyId)).filter(Boolean)
                    );
                    const combinedDeduped = combined.filter(
                        (p) => p.type !== 'story' || !storyIdsWithChapters.has(norm(p.storyId ?? p.id))
                    );
                    // Gộp theo truyện: một khối mỗi truyện (chứa 1 truyện hoặc nhiều chương). Chuẩn hóa storyId để tránh đếm trùng.
                    const byStory = new Map();
                    for (const p of combinedDeduped) {
                        const sid = norm(p.storyId ?? p.id) || null;
                        if (!sid) continue;
                        if (!byStory.has(sid)) byStory.set(sid, { storyId: p.storyId ?? p.id, storyTitle: p.storyTitle, storyCover: p.storyCover ?? '', author: p.author, categories: p.categories ?? [], chapters: [], storyItem: null });
                        const g = byStory.get(sid);
                        if (p.type === 'chapter') {
                            const chId = p.id ?? p.chapterId;
                            if (!g.chapters.some((c) => (c.id ?? c.chapterId) === chId)) g.chapters.push(p);
                        } else g.storyItem = p;
                    }
                    const groupedList = [];
                    for (const g of byStory.values()) {
                        const rep = g.chapters[0] ?? g.storyItem;
                        if (!rep) continue;
                        // Chỉ hiển thị nhóm có ít nhất 1 chương chờ duyệt. Truyện đã hủy hết chương (0 chương) thì không hiện trong Chờ duyệt.
                        if (g.chapters.length === 0) continue;
                        const times = g.chapters
                            .map((c) => c.pendingSince ?? c.submittedAt)
                            .filter(Boolean)
                            .map((d) => new Date(d).getTime())
                            .filter((t) => Number.isFinite(t));
                        const slaPendingSince = times.length ? new Date(Math.min(...times)).toISOString() : null;
                        const slaTimeStatus = worstTimeStatus(g.chapters.map((c) => c.timeStatus).filter(Boolean));
                        const hasPendingEscalation = g.chapters.some((c) => c.hasPendingEscalation);
                        const adminRej = pickGroupAdminRejectedRelease(g.chapters, g.storyItem);
                        const adminExt = pickGroupAdminRejectedExtend(g.chapters, g.storyItem);
                        groupedList.push({
                            type: 'story_group',
                            id: g.storyId,
                            storyId: g.storyId,
                            storyTitle: g.storyTitle,
                            storyCover: g.storyCover,
                            author: g.author,
                            categories: g.categories,
                            description: rep.description ?? '',
                            status: 'pending',
                            chapters: g.chapters,
                            representativePublication: rep,
                            chapterCount: g.chapters.length,
                            slaPendingSince,
                            slaTimeStatus,
                            hasPendingEscalation,
                            ...adminRej,
                            ...adminExt,
                        });
                    }
                    groupedList.sort((a, b) => {
                        const ta = a.slaPendingSince ? new Date(a.slaPendingSince).getTime() : Infinity;
                        const tb = b.slaPendingSince ? new Date(b.slaPendingSince).getTime() : Infinity;
                        return ta - tb;
                    });
                    if (groupedList.length === 0) {
                        setPublications([]);
                        setTotalCount(0);
                        setTotalPages(1);
                        setCurrentPage(1);
                        setStatsData(prev => ({ ...prev, pending: 0 }));
                        return;
                    }
                    // Bổ sung ảnh bìa, tác giả, độ tuổi phù hợp, thể loại, mô tả từ GET /stories/:id.
                    return Promise.all(groupedList.map((g) => getStoryById(g.storyId).then((story) => storyResponseToMeta(story)).catch(() => null)))
                        .then((metas) => {
                            const enriched = groupedList.map((g, i) => {
                                const meta = metas[i];
                                if (!meta) return g;
                                return {
                                    ...g,
                                    storyCover: meta.storyCover || g.storyCover,
                                    author: meta.author ?? g.author,
                                    authorId: meta.authorId ?? g.authorId,
                                    ageRating: meta.ageRating ?? g.ageRating,
                                    categories: (meta.categories?.length ? meta.categories : g.categories) ?? [],
                                    description: meta.description || g.description || '',
                                };
                            });
                            return enrichPublicationsWithAuthorProfile(enriched).then((finalList) => {
                                setPublications(finalList);
                                const total = finalList.length;
                                setTotalCount(total);
                                setTotalPages(Math.max(1, Math.ceil(total / PAGE_SIZE)));
                                setCurrentPage(Math.min(page, Math.max(1, Math.ceil(total / PAGE_SIZE))));
                                setStatsData(prev => ({ ...prev, pending: total }));
                            });
                        });
                })
                .catch((err) => {
                    if (!silent) setError(err?.response?.data?.message ?? err?.message ?? 'Không tải được danh sách. Bạn cần đăng nhập với vai trò MODERATOR hoặc Admin.');
                    setPublications([]);
                    setTotalCount(0);
                    setTotalPages(1);
                })
                .finally(() => { if (!silent) setLoading(false); });
            return;
        }

        // Lịch sử đã duyệt (PUBLISHED) / từ chối (REJECTED). Tab Từ chối = truyện REJECTED + chương REJECTED (gộp theo truyện).
        const statusParam = STATUS_PARAM_MAP[filterStatus];
        if (!statusParam) {
            if (!silent) setLoading(false);
            return;
        }
        if (filterStatus === 'rejected') {
            Promise.all([
                getModeratorReviewedStories({ status: statusParam, pageSize: 500, sortBy: 'updated_at', sortOrder: 'desc' }),
                getModeratorReviewedChapters({ status: statusParam, pageSize: 500, sortBy: 'updated_at', sortOrder: 'desc' }),
                getRejectedChapterVersionsHistory(),
            ])
                .then(([storiesRes, chaptersRes, versionsRes]) => {
                    const storyItems = storiesRes?.items ?? storiesRes?.Items ?? [];
                    const chapterItems = chaptersRes?.items ?? chaptersRes?.Items ?? [];
                    const storyPubs = Array.isArray(storyItems) ? storyItems.map(mapStoryToPublication) : [];
                    const chapterList = Array.isArray(chapterItems) ? chapterItems.map(mapReviewedChapterToItem) : [];
                    const versionList = Array.isArray(versionsRes) ? versionsRes.map(mapRejectedVersionToItem) : [];
                    const byStory = new Map();
                    for (const ch of [...chapterList, ...versionList]) {
                        const sid = ch.storyId;
                        if (!sid) continue;
                        if (!byStory.has(sid)) byStory.set(sid, { storyId: sid, storyTitle: ch.storyTitle, storyCover: '', author: null, categories: [], chapters: [] });
                        byStory.get(sid).chapters.push(ch);
                    }
                    const chapterGroups = [];
                    for (const g of byStory.values()) {
                        const rep = g.chapters[0];
                        chapterGroups.push({
                            type: 'story_group',
                            id: g.storyId,
                            storyId: g.storyId,
                            storyTitle: g.storyTitle,
                            storyCover: g.storyCover || '',
                            author: g.author,
                            categories: g.categories || [],
                            status: 'rejected',
                            chapters: g.chapters,
                            representativePublication: rep,
                            chapterCount: g.chapters.length,
                        });
                    }
                    const combined = [...storyPubs, ...chapterGroups];
                    const total = combined.length;
                    const pages = Math.max(1, Math.ceil(total / PAGE_SIZE));
                    enrichAgeRatingFromStory(combined).then((withAge) => enrichPublicationsWithAuthorProfile(withAge).then((items) => {
                        setPublications(items);
                        setTotalCount(total);
                        setTotalPages(pages);
                        setCurrentPage(Math.min(page, pages));
                    }));
                })
                .catch((err) => {
                    if (!silent) setError(err?.response?.data?.message ?? err?.message ?? 'Không tải được lịch sử từ chối.');
                    if (!silent) setTotalCount(0);
                    if (!silent) setTotalPages(1);
                })
                .finally(() => { if (!silent) setLoading(false); });
            return;
        }
        getModeratorReviewedStories({
            status: statusParam,
            page,
            pageSize: PAGE_SIZE,
            sortBy: 'updated_at',
            sortOrder: 'desc',
        })
            .then((res) => {
                const items = res?.items ?? res?.Items ?? [];
                const total = res?.totalCount ?? res?.TotalCount ?? res?.total ?? items.length;
                const pages = res?.totalPages ?? res?.TotalPages ?? Math.max(1, Math.ceil(total / PAGE_SIZE));
                const pageNum = res?.page ?? res?.Page ?? page;
                const list = Array.isArray(items) ? items.map(mapStoryToPublication) : [];
                return enrichAgeRatingFromStory(list).then((withAge) => enrichPublicationsWithAuthorProfile(withAge).then((enrichedList) => {
                    setPublications(enrichedList);
                    setTotalCount(total);
                    setTotalPages(pages);
                    setCurrentPage(pageNum);
                }));
            })
            .catch((err) => {
                if (!silent) setError(err?.response?.data?.message ?? err?.message ?? 'Không tải được lịch sử đã duyệt.');
                if (!silent) setTotalCount(0);
                if (!silent) setTotalPages(1);
            })
            .finally(() => { if (!silent) setLoading(false); });
    }, [filterStatus]);

    const handlePageChange = (page) => {
        setCurrentPage(page);
        if (filterStatus === 'pending') loadPublications(page);
    };

    const loadStats = useCallback(() => {
        Promise.all([
            getStories({ pageSize: 500 }),
            getPendingStories({ pageSize: 500, claimFilter: 'CLAIMED' }),
            getPendingChapters({ pageSize: 500, claimFilter: 'CLAIMED' })
        ])
            .then(([storiesRes, pendingStoriesRes, pendingChaptersRes]) => {
                const storyItems = storiesRes?.items ?? storiesRes?.Items ?? [];
                const mapped = storyItems.map(mapStoryToPublication);
                const pendingStoryItems = pendingStoriesRes?.items ?? pendingStoriesRes?.Items ?? [];
                const pendingChapterItems = pendingChaptersRes?.items ?? pendingChaptersRes?.Items ?? [];
                const pendingStoryList = pendingStoryItems.map(mapPendingStoryToItem);
                const pendingChapterList = pendingChapterItems.map(mapPendingChapterToItem);
                const combined = [...pendingStoryList, ...pendingChapterList].filter((p) => p.status === 'pending');
                const norm = (id) => (id != null ? String(id).toLowerCase() : '');
                const storyIdsWithChapters = new Set(
                    combined.filter((p) => p.type === 'chapter').map((p) => norm(p.storyId)).filter(Boolean)
                );
                const combinedDeduped = combined.filter(
                    (p) => p.type !== 'story' || !storyIdsWithChapters.has(norm(p.storyId ?? p.id))
                );
                const byStory = new Map();
                for (const p of combinedDeduped) {
                    const sid = norm(p.storyId ?? p.id) || 'none';
                    if (!sid || sid === 'none') continue;
                    if (!byStory.has(sid)) byStory.set(sid, { chapters: [] });
                    const g = byStory.get(sid);
                    if (p.type === 'chapter') {
                        const chId = p.id ?? p.chapterId;
                        if (!g.chapters.some((c) => (c.id ?? c.chapterId) === chId)) g.chapters.push(p);
                    }
                }
                const pendingCount = [...byStory.values()].filter((g) => g.chapters.length > 0).length;
                setStatsData({
                    pending: pendingCount,
                    approved: mapped.filter(p => p.status === 'approved').length,
                    rejected: mapped.filter(p => p.status === 'rejected').length,
                    total: mapped.length
                });
            })
            .catch(() => setStatsData({ pending: 0, approved: 0, rejected: 0, total: 0 }));
    }, []);

    useEffect(() => {
        setError(null);
        if (filterStatus !== 'pending') return;
        const id = setTimeout(() => {
            setCurrentPage(1);
            loadPublications(1);
        }, 0);
        return () => clearTimeout(id);
    }, [loadPublications, filterStatus]);

    /** Ngay khi vào màn: preload Đã duyệt và Từ chối để chuyển tab thấy ngay. */
    useEffect(() => {
        loadApprovedCache();
        loadRejectedCache();
    }, [loadApprovedCache, loadRejectedCache]);

    /** Chuyển sang tab Đã duyệt / Từ chối: hiển thị từ cache; nếu cache trống thì gọi load ngay. */
    const approvedCacheRequestedRef = useRef(false);
    const rejectedCacheRequestedRef = useRef(false);
    useEffect(() => {
        if (filterStatus === 'approved') {
            setPublications(approvedCache.items);
            setTotalCount(approvedCache.total);
            setTotalPages(approvedCache.totalPages);
            setCurrentPage(1);
            if (approvedCache.items.length === 0 && !approvedCacheLoading && !approvedCacheRequestedRef.current) {
                approvedCacheRequestedRef.current = true;
                loadApprovedCache();
            }
        } else if (filterStatus === 'rejected') {
            setPublications(rejectedCache.items);
            setTotalCount(rejectedCache.total);
            setTotalPages(rejectedCache.totalPages);
            setCurrentPage(1);
            if (rejectedCache.items.length === 0 && !rejectedCacheLoading && !rejectedCacheRequestedRef.current) {
                rejectedCacheRequestedRef.current = true;
                loadRejectedCache();
            }
        } else {
            approvedCacheRequestedRef.current = false;
            rejectedCacheRequestedRef.current = false;
        }
    }, [filterStatus, approvedCache, rejectedCache, approvedCacheLoading, rejectedCacheLoading, loadApprovedCache, loadRejectedCache]);

    useEffect(() => {
        const id = setTimeout(() => {
            loadStats();
            loadUnclaimedCount();
        }, 0);
        return () => clearTimeout(id);
    }, [loadStats, loadUnclaimedCount]);

    /** Chỉ refresh định kỳ khi đang ở tab Chờ duyệt. Tab Đã duyệt/Từ chối dùng cache, không gọi loadPublications (tránh ghi đè list và làm mất item như Conan). */
    useEffect(() => {
        if (filterStatus !== 'pending') return;
        const intervalId = setInterval(() => {
            loadPublications(currentPage, { silent: true });
            loadStats();
            loadUnclaimedCount();
        }, REFRESH_INTERVAL_MS);
        return () => clearInterval(intervalId);
    }, [filterStatus, loadPublications, loadStats, loadUnclaimedCount, currentPage]);

    /** Refetch khi SignalR báo PendingListChanged. Chỉ refetch khi đang ở tab Chờ duyệt; tab Đã duyệt/Từ chối không ghi đè list. */
    const onRefetchPendingRef = useRef(() => { });
    onRefetchPendingRef.current = () => {
        if (filterStatus !== 'pending') return;
        loadPublications(currentPage, { silent: true });
        loadStats();
        loadUnclaimedCount();
    };

    useEffect(() => {
        const { stop } = createModeratorHubConnection(() => onRefetchPendingRef.current());
        return () => { stop(); };
    }, []);

    useEffect(() => {
        if (showClaimModal) loadClaimModalItems();
    }, [showClaimModal, loadClaimModalItems]);

    useEffect(() => {
        if (claimConfirmTarget) {
            setClaimDeadlineChoice('7');
            setClaimCustomDeadline('');
            setClaimCommitted(false);
        }
    }, [claimConfirmTarget]);

    const getClaimReviewDeadlineIso = () => {
        if (claimDeadlineChoice === 'custom') {
            const iso = localDateTimeInputToIsoUtc(claimCustomDeadline);
            return iso || reviewDeadlineAfterDaysUtc(7);
        }
        return reviewDeadlineAfterDaysUtc(claimDeadlineChoice === '14' ? 14 : 7);
    };

    const filteredPublications = (filterStatus === 'pending' || filterStatus === 'rejected' || filterStatus === 'approved')
        ? publications.slice((currentPage - 1) * PAGE_SIZE, currentPage * PAGE_SIZE)
        : publications;

    const handleViewDetail = (publication) => {
        setSelectedPublication(publication);
    };

    const handleCloseDetail = () => {
        setSelectedPublication(null);
    };

    /** Lấy thông báo lỗi khi claim (404 = đã được moderator khác nhận). */
    const getClaimErrorMessage = (err) => {
        if (err?.response?.status === 404) {
            return err?.response?.data?.message ?? 'Đã được moderator khác nhận duyệt.';
        }
        return err?.response?.data?.message ?? err?.message ?? 'Không thể nhận duyệt đơn.';
    };

    const handleClaimStory = async (storyId) => {
        setClaimingId(storyId);
        try {
            await claimStory(storyId, reviewDeadlineAfterDaysUtc(7));
            loadPublications(currentPage);
            loadStats();
        } catch (err) {
            alert(getClaimErrorMessage(err));
        } finally {
            setClaimingId(null);
        }
    };

    const handleClaimChapter = async (chapterId) => {
        setClaimingId(chapterId);
        try {
            await claimChapter(chapterId, reviewDeadlineAfterDaysUtc(7));
            loadPublications(currentPage);
            loadStats();
        } catch (err) {
            alert(getClaimErrorMessage(err));
        } finally {
            setClaimingId(null);
        }
    };

    /** Mở popup xác nhận hủy nhận duyệt. */
    const handleReleaseAllClaimsForStory = (pub) => {
        const storyId = pub?.storyId ?? pub?.id;
        if (!storyId) return;
        const chapterCount = pub?.chapterCount ?? pub?.chapters?.length ?? 0;
        setReleaseReason('');
        setReleaseFormError('');
        setReleaseConfirmTarget({
            storyId,
            storyTitle: pub?.storyTitle ?? '',
            chapterCount,
        });
    };

    /** Xác nhận trong popup — gửi đơn RELEASE_ASSIGNMENT (STORY) lên admin. */
    const handleConfirmReleaseAllClaims = async () => {
        const target = releaseConfirmTarget;
        if (!target?.storyId) return;
        const reason = releaseReason.trim();
        if (reason.length < 10) {
            setReleaseFormError('Lý do cần ít nhất 10 ký tự (theo quy định hệ thống).');
            return;
        }
        setReleaseFormError('');
        const { storyId } = target;
        setReleasingAllClaimsStoryId(storyId);
        try {
            await submitReviewEscalation({
                targetType: 'STORY',
                targetId: String(storyId),
                requestKind: 'RELEASE_ASSIGNMENT',
                reason,
                proposedDeadlineAt: null,
            });
            setReleaseConfirmTarget(null);
            setReleaseReason('');
            const openSid = selectedPublication?.storyId ?? selectedPublication?.id;
            if (openSid != null && String(openSid) === String(storyId)) {
                setSelectedPublication(null);
            }
            loadPublications(currentPage);
            loadStats();
            setReleaseResultMessage('Đã gửi đơn lên quản trị viên. Sau khi đơn được duyệt, các chương và phần nhận duyệt truyện (nếu có) sẽ trở lại hàng đợi. Trong lúc chờ, bạn không thể duyệt/từ chối chương của truyện này.');
        } catch (err) {
            setReleaseFormError(err?.response?.data?.message ?? err?.message ?? 'Không thể gửi đơn.');
        } finally {
            setReleasingAllClaimsStoryId(null);
        }
    };

    /** Xác nhận nhận duyệt từ popup. story_group = nhận cả truyện (nếu chưa) + tất cả chương của truyện đó trong một lần. */
    const handleConfirmClaimFromModal = async () => {
        if (!claimConfirmTarget) return;
        if (!claimCommitted) {
            alert('Vui lòng xác nhận cam kết hoàn thành duyệt trong hạn đã chọn.');
            return;
        }
        if (claimDeadlineChoice === 'custom' && !localDateTimeInputToIsoUtc(claimCustomDeadline)) {
            alert('Vui lòng chọn ngày giờ hạn duyệt hợp lệ.');
            return;
        }
        const reviewDeadlineAt = getClaimReviewDeadlineIso();
        const { type, id, storyId, chapterIds, isStoryUnclaimed } = claimConfirmTarget;
        setModalClaimBusy(true);
        setClaimingId(id ?? storyId);
        try {
            if (type === 'story_group') {
                if (isStoryUnclaimed && (storyId || id)) await claimStory(storyId || id, reviewDeadlineAt);
                for (const chapterId of chapterIds ?? []) {
                    await claimChapter(chapterId, reviewDeadlineAt);
                }
            } else if (type === 'story') {
                await claimStory(id, reviewDeadlineAt);
            } else {
                await claimChapter(id, reviewDeadlineAt);
            }
            setClaimConfirmTarget(null);
            setShowClaimModal(false);
            loadPublications(1);
            loadStats();
            loadUnclaimedCount();
            loadClaimModalItems();
        } catch (err) {
            alert(getClaimErrorMessage(err));
        } finally {
            setClaimingId(null);
            setModalClaimBusy(false);
        }
    };

    const handleApprove = () => {
        setSelectedPublication(null);
        loadPublications(currentPage);
        loadStats();
        loadApprovedCache();
    };

    const handleReject = () => {
        setSelectedPublication(null);
        loadPublications(currentPage);
        loadStats();
        loadRejectedCache();
    };

    const openAuthorPolicyModal = async () => {
        setPolicyModalOpen(true);
        setPolicyLoading(true);
        setPolicyError('');
        try {
            const policy = await getActivePolicy('AUTHOR');
            setAuthorPolicy(policy);
        } catch (e) {
            const msg =
                e?.response?.data?.message
                ?? e?.response?.data?.Message
                ?? e?.message
                ?? 'Không thể tải chính sách tác giả.';
            setPolicyError(msg);
            setAuthorPolicy(null);
        } finally {
            setPolicyLoading(false);
        }
    };

    const stats = statsData;

    return (
        <div style={{ padding: '2rem' }}>
            {/* Header */}
            <div style={{ marginBottom: '2rem' }}>
                <h1 style={{
                    fontSize: '1.875rem',
                    fontWeight: 700,
                    color: '#1e293b',
                    margin: 0,
                    marginBottom: '0.5rem'
                }}>
                    Quản lý xuất bản
                </h1>
                <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginTop: '0.75rem' }}>
                    <button
                        type="button"
                        onClick={openAuthorPolicyModal}
                        style={{
                            padding: '0.5rem 1rem',
                            fontSize: '0.875rem',
                            fontWeight: 600,
                            backgroundColor: '#ffffff',
                            color: '#1f2937',
                            border: '1px solid #e2e8f0',
                            borderRadius: '8px',
                            cursor: 'pointer',
                        }}
                    >
                        Xem chính sách hệ thống
                    </button>
                    <button
                        type="button"
                        onClick={() => setProcessModalOpen(true)}
                        style={{
                            padding: '0.5rem 1rem',
                            fontSize: '0.875rem',
                            fontWeight: 600,
                            backgroundColor: '#ffffff',
                            color: '#1f2937',
                            border: '1px solid #e2e8f0',
                            borderRadius: '8px',
                            cursor: 'pointer',
                        }}
                    >
                        Xem quy trình duyệt xuất bản
                    </button>
                </div>
            </div>

            {/* Nút "Nhận duyệt đơn" — số bên cạnh = đơn chưa ai nhận; tab Chờ duyệt = đơn bạn đã nhận, đang chờ bạn duyệt */}
            <div style={{
                backgroundColor: '#ffffff',
                borderRadius: '12px',
                padding: '0.75rem 1rem',
                marginBottom: '1rem',
                border: '1px solid #e2e8f0'
            }}>
                <button
                    onClick={() => setShowClaimModal(true)}
                    style={{
                        padding: '0.5rem 1rem',
                        fontSize: '0.875rem',
                        fontWeight: 600,
                        backgroundColor: '#0ea5e9',
                        color: '#ffffff',
                        border: 'none',
                        borderRadius: '8px',
                        cursor: 'pointer',
                        display: 'inline-flex',
                        alignItems: 'center',
                        gap: '0.5rem'
                    }}
                >
                    Nhận duyệt đơn
                    {unclaimedCount > 0 && (
                        <span
                            style={{
                                minWidth: '20px',
                                height: '20px',
                                padding: '0 6px',
                                display: 'inline-flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                fontSize: '0.75rem',
                                fontWeight: 700,
                                backgroundColor: '#ffffff',
                                color: '#0ea5e9',
                                borderRadius: '9999px',
                            }}
                        >
                            {unclaimedCount > 99 ? '99+' : unclaimedCount}
                        </span>
                    )}
                </button>
            </div>

            {/* Filter Tabs */}
            <div style={{
                backgroundColor: '#ffffff',
                borderRadius: '12px',
                padding: '1rem',
                marginBottom: '1.5rem',
                border: '1px solid #e2e8f0',
                display: 'flex',
                gap: '0.5rem',
                flexWrap: 'wrap'
            }}>
                {[
                    { value: 'pending', label: 'Chờ duyệt', color: '#ffc107' },
                    { value: 'approved', label: 'Đã duyệt', color: '#10b981' },
                    { value: 'rejected', label: 'Từ chối', color: '#ef4444' }
                ].map(tab => (
                    <button
                        key={tab.value}
                        onClick={() => setFilterStatus(tab.value)}
                        style={{
                            padding: '0.625rem 1.25rem',
                            fontSize: '0.875rem',
                            fontWeight: 600,
                            backgroundColor: filterStatus === tab.value ? tab.color : 'transparent',
                            color: filterStatus === tab.value ? '#ffffff' : '#64748b',
                            border: filterStatus === tab.value ? 'none' : '1px solid #e2e8f0',
                            borderRadius: '9999px',
                            cursor: 'pointer',
                            transition: 'all 0.2s'
                        }}
                        onMouseEnter={(e) => {
                            if (filterStatus !== tab.value) {
                                e.currentTarget.style.backgroundColor = '#f8fafc';
                            }
                        }}
                        onMouseLeave={(e) => {
                            if (filterStatus !== tab.value) {
                                e.currentTarget.style.backgroundColor = 'transparent';
                            }
                        }}
                    >
                        {tab.label} ({
                            tab.value === 'pending' ? stats.pending :
                                tab.value === 'approved' ? approvedCache.total :
                                    rejectedCache.total
                        })
                    </button>
                ))}
            </div>

            {/* Publications List */}
            {(loading || (filterStatus === 'approved' && approvedCacheLoading) || (filterStatus === 'rejected' && rejectedCacheLoading)) ? (
                <div style={{
                    backgroundColor: '#ffffff',
                    borderRadius: '12px',
                    padding: '4rem 2rem',
                    textAlign: 'center',
                    border: '1px solid #e2e8f0'
                }}>
                    <div style={{ fontSize: '2rem', marginBottom: '1rem' }}>⏳</div>
                    <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>Đang tải danh sách truyện...</p>
                </div>
            ) : error ? (
                <div style={{
                    backgroundColor: '#fee2e2',
                    borderRadius: '12px',
                    padding: '1.5rem',
                    border: '1px solid #ef4444'
                }}>
                    <p style={{ fontSize: '0.875rem', color: '#991b1b', margin: 0 }}>{error}</p>
                </div>
            ) : (
                <>
                    <div style={{ backgroundColor: '#ffffff', borderRadius: '12px', border: '1px solid #e2e8f0', overflow: 'hidden' }}>
                        <PublicationList
                            publications={filteredPublications}
                            onViewDetail={handleViewDetail}
                            onClaimStory={handleClaimStory}
                            onClaimChapter={handleClaimChapter}
                            claimingId={claimingId}
                            showClaimButton={false}
                            showModeratorSla={filterStatus === 'pending'}
                            onReleaseAllClaimsForStory={handleReleaseAllClaimsForStory}
                            releasingAllClaimsStoryId={releasingAllClaimsStoryId}
                        />
                        {totalPages > 1 && (
                            <Pagination
                                currentPage={currentPage}
                                totalPages={totalPages}
                                totalItems={totalCount}
                                itemsPerPage={PAGE_SIZE}
                                onPageChange={handlePageChange}
                                itemLabel="truyện"
                            />
                        )}
                    </div>
                </>
            )}

            {/* Modal "Chọn truyện để nhận duyệt" — danh sách truyện trùng category với moderator */}
            {showClaimModal && (
                <div
                    style={{
                        position: 'fixed',
                        inset: 0,
                        backgroundColor: 'rgba(0,0,0,0.5)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 1000,
                        padding: '1rem'
                    }}
                    onClick={() => setShowClaimModal(false)}
                >
                    <div
                        style={{
                            backgroundColor: '#fff',
                            borderRadius: '12px',
                            maxWidth: '560px',
                            width: '100%',
                            maxHeight: '85vh',
                            overflow: 'hidden',
                            display: 'flex',
                            flexDirection: 'column',
                            boxShadow: '0 20px 40px rgba(0,0,0,0.15)'
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div style={{ padding: '1rem 1.25rem', borderBottom: '1px solid #e2e8f0', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <h2 style={{ margin: 0, fontSize: '1.125rem', fontWeight: 600, color: '#1e293b' }}>
                                Chọn truyện hoặc chương để nhận duyệt
                            </h2>
                            <button
                                type="button"
                                onClick={() => setShowClaimModal(false)}
                                style={{ background: 'none', border: 'none', fontSize: '1.5rem', cursor: 'pointer', color: '#64748b', lineHeight: 1 }}
                                aria-label="Đóng"
                            >
                                ×
                            </button>
                        </div>
                        <div style={{ padding: '1rem', overflow: 'auto', flex: 1 }}>
                            {claimModalLoading ? (
                                <p style={{ textAlign: 'center', color: '#64748b', margin: 0 }}>Đang tải danh sách...</p>
                            ) : claimModalItems.length === 0 ? (
                                <p style={{ textAlign: 'center', color: '#64748b', margin: 0 }}>
                                    Không có truyện hoặc chương nào chưa nhận (trùng thể loại với bạn).
                                </p>
                            ) : (
                                <div style={{ display: 'flex', flexDirection: 'column', gap: '0.75rem' }}>
                                    {claimModalItems.map((item) => {
                                        const isGroup = item._claimType === 'story_group';
                                        const subtitle = isGroup
                                            ? (item._chapterCount ? `${item._chapterCount} chương chờ duyệt` : 'Truyện chờ duyệt')
                                            : item._claimType === 'story'
                                                ? `${item.author ?? ''}${item.totalChapters != null ? ` • ${item.totalChapters} chương` : ''}`
                                                : `Chương ${(item.orderIndex ?? 0) + 1} • ${item.wordCount ?? 0} từ`;
                                        const confirmTitle = isGroup ? `${item.storyTitle} (${item._chapterCount || 0} chương)` : (item._claimType === 'story' ? item.storyTitle : `${item.storyTitle} — ${item.chapterTitle || 'Chương'}`);
                                        const confirmPayload = isGroup
                                            ? { type: 'story_group', id: item._claimId, title: confirmTitle, storyId: item.storyId, chapterIds: item._chapterIds, isStoryUnclaimed: item._isStoryUnclaimed }
                                            : { type: item._claimType, id: item._claimId, title: confirmTitle };
                                        return (
                                            <div
                                                key={item._claimType + '-' + item._claimId}
                                                style={{
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    gap: '1rem',
                                                    padding: '0.75rem',
                                                    border: '1px solid #e2e8f0',
                                                    borderRadius: '8px',
                                                    backgroundColor: '#fafafa'
                                                }}
                                            >
                                                <div style={{ position: 'relative', width: '48px', height: '64px', borderRadius: '6px', flexShrink: 0, overflow: 'hidden' }}>
                                                    <div style={{ position: 'absolute', inset: 0, backgroundColor: '#e2e8f0', display: 'flex', alignItems: 'center', justifyContent: 'center', fontSize: '1.5rem' }}>📄</div>
                                                    {item.storyCover ? (
                                                        <img
                                                            src={item.storyCover}
                                                            alt=""
                                                            style={{ position: 'relative', zIndex: 1, width: '100%', height: '100%', objectFit: 'cover', borderRadius: '6px' }}
                                                            onError={(e) => { e.target.style.opacity = '0'; e.target.style.position = 'absolute'; }}
                                                            referrerPolicy="no-referrer"
                                                        />
                                                    ) : null}
                                                </div>
                                                <div style={{ flex: 1, minWidth: 0 }}>
                                                    <div style={{ fontWeight: 600, color: '#1e293b', marginBottom: '0.25rem' }}>
                                                        {item.storyTitle}
                                                        {!isGroup && item._claimType === 'chapter' && item.chapterTitle && (
                                                            <span style={{ fontWeight: 500, color: '#64748b', fontSize: '0.875rem' }}> — {item.chapterTitle}</span>
                                                        )}
                                                    </div>
                                                    <div style={{ fontSize: '0.8125rem', color: '#64748b' }}>{subtitle}</div>
                                                    {isGroup && Array.isArray(item.categories) && item.categories.length > 0 && (
                                                        <div style={{ display: 'flex', gap: '0.25rem', flexWrap: 'wrap', marginTop: '0.25rem' }}>
                                                            {item.categories.slice(0, 3).map((c) => (
                                                                <span key={c} style={{ fontSize: '0.7rem', padding: '0.125rem 0.375rem', backgroundColor: '#e2e8f0', borderRadius: '4px', color: '#475569' }}>{c}</span>
                                                            ))}
                                                        </div>
                                                    )}
                                                </div>
                                                <button
                                                    type="button"
                                                    onClick={() => {
                                                        setClaimReviewDeadlineLocal(getDefaultDatetimeLocalForClaim());
                                                        setClaimConfirmTarget(confirmPayload);
                                                    }}
                                                    disabled={claimingId === (item._claimId ?? item.storyId)}
                                                    style={{
                                                        padding: '0.5rem 0.875rem',
                                                        fontSize: '0.8125rem',
                                                        fontWeight: 600,
                                                        backgroundColor: '#0ea5e9',
                                                        color: '#fff',
                                                        border: 'none',
                                                        borderRadius: '8px',
                                                        cursor: claimingId === (item._claimId ?? item.storyId) ? 'wait' : 'pointer',
                                                        opacity: claimingId === (item._claimId ?? item.storyId) ? 0.7 : 1
                                                    }}
                                                >
                                                    {claimingId === (item._claimId ?? item.storyId) ? '...' : 'Nhận duyệt đơn'}
                                                </button>
                                            </div>
                                        );
                                    })}
                                </div>
                            )}
                        </div>
                    </div>
                </div>
            )}

            {/* Popup xác nhận nhận duyệt */}
            {claimConfirmTarget && (
                <div
                    style={{
                        position: 'fixed',
                        inset: 0,
                        backgroundColor: 'rgba(0,0,0,0.5)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 1001,
                        padding: '1rem'
                    }}
                    onClick={() => setClaimConfirmTarget(null)}
                >
                    <div
                        style={{
                            backgroundColor: '#fff',
                            borderRadius: '12px',
                            padding: '1.5rem',
                            maxWidth: '440px',
                            width: '100%',
                            boxShadow: '0 20px 40px rgba(0,0,0,0.15)'
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <p style={{ margin: 0, marginBottom: '0.75rem', fontSize: '0.9375rem', color: '#1e293b' }}>
                            Nhận duyệt {claimConfirmTarget.type === 'story_group' ? 'truyện và tất cả chương' : claimConfirmTarget.type === 'chapter' ? 'chương' : 'truyện'}{' '}
                            <strong>&quot;{claimConfirmTarget.title}&quot;</strong>
                        </p>
                        <p style={{ margin: '0 0 0.75rem', fontSize: '0.8125rem', color: '#64748b' }}>
                            Chọn <strong>hạn hoàn thành duyệt</strong> (UTC theo máy chủ). Hạn phải cách hiện tại ít nhất ~24 giờ theo quy định hệ thống.
                        </p>
                        <div style={{ display: 'flex', gap: '0.5rem', flexWrap: 'wrap', marginBottom: '0.75rem' }}>
                            {[
                                { v: '7', label: '7 ngày' },
                                { v: '14', label: '14 ngày' },
                                { v: 'custom', label: 'Tùy chỉnh' },
                            ].map((opt) => (
                                <button
                                    key={opt.v}
                                    type="button"
                                    onClick={() => setClaimDeadlineChoice(opt.v)}
                                    style={{
                                        padding: '0.4rem 0.75rem',
                                        fontSize: '0.8125rem',
                                        fontWeight: 600,
                                        borderRadius: '8px',
                                        border: claimDeadlineChoice === opt.v ? '2px solid #0ea5e9' : '1px solid #e2e8f0',
                                        backgroundColor: claimDeadlineChoice === opt.v ? '#e0f2fe' : '#fff',
                                        color: '#0f172a',
                                        cursor: 'pointer',
                                    }}
                                >
                                    {opt.label}
                                </button>
                            ))}
                        </div>
                        {claimDeadlineChoice === 'custom' && (
                            <input
                                type="datetime-local"
                                value={claimCustomDeadline}
                                onChange={(e) => setClaimCustomDeadline(e.target.value)}
                                style={{
                                    width: '100%',
                                    marginBottom: '0.75rem',
                                    padding: '0.5rem',
                                    borderRadius: '8px',
                                    border: '1px solid #cbd5e1',
                                    fontSize: '0.875rem',
                                }}
                            />
                        )}
                        <label style={{ display: 'flex', alignItems: 'flex-start', gap: '0.5rem', fontSize: '0.8125rem', color: '#334155', marginBottom: '1rem', cursor: 'pointer' }}>
                            <input
                                type="checkbox"
                                checked={claimCommitted}
                                onChange={(e) => setClaimCommitted(e.target.checked)}
                                style={{ marginTop: '0.2rem' }}
                            />
                            <span>
                                Tôi cam kết sẽ xử lý kiểm duyệt trong hạn đã chọn; nếu không kịp tôi sẽ gửi báo cáo lên quản trị để gia hạn hoặc chuyển đơn.
                            </span>
                        </label>
                        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem' }}>
                            <button
                                type="button"
                                onClick={() => setClaimConfirmTarget(null)}
                                style={{ padding: '0.5rem 1rem', fontSize: '0.875rem', fontWeight: 600, backgroundColor: '#f1f5f9', color: '#475569', border: 'none', borderRadius: '8px', cursor: 'pointer' }}
                            >
                                Hủy
                            </button>
                            <button
                                type="button"
                                onClick={handleConfirmClaimFromModal}
                                disabled={!claimCommitted || modalClaimBusy}
                                style={{
                                    padding: '0.5rem 1rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    backgroundColor: claimCommitted && !modalClaimBusy ? '#0ea5e9' : '#94a3b8',
                                    color: '#fff',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: claimCommitted && !modalClaimBusy ? 'pointer' : 'not-allowed',
                                }}
                            >
                                Xác nhận nhận duyệt
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Popup xác nhận hủy nhận duyệt (toàn bộ chương + lock truyện nếu có) */}
            {releaseConfirmTarget && (
                <div
                    role="dialog"
                    aria-modal="true"
                    aria-labelledby="release-confirm-title"
                    style={{
                        position: 'fixed',
                        inset: 0,
                        backgroundColor: 'rgba(0,0,0,0.5)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 10050,
                        padding: '1rem',
                    }}
                    onClick={() => {
                        if (releasingAllClaimsStoryId) return;
                        setReleaseConfirmTarget(null);
                        setReleaseFormError('');
                    }}
                >
                    <div
                        style={{
                            backgroundColor: '#fff',
                            borderRadius: '12px',
                            padding: '1.5rem',
                            maxWidth: '440px',
                            width: '100%',
                            boxShadow: '0 20px 40px rgba(0,0,0,0.15)',
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <h2 id="release-confirm-title" style={{ margin: '0 0 0.35rem', fontSize: '1.125rem', fontWeight: 700, color: '#0f172a', lineHeight: 1.35 }}>
                            Trả truyện về hàng đợi kiểm duyệt chung?
                        </h2>
                        <p style={{ margin: '0 0 0.65rem', fontSize: '0.8125rem', fontWeight: 600, color: '#64748b' }}>
                            Gửi đơn lên quản trị viên — sau khi đơn được <strong>chấp nhận</strong>, hệ thống mới trả các mục về hàng đợi (giống luồng hủy nhận duyệt thông thường).
                        </p>
                        <p style={{ margin: '0 0 0.75rem', fontSize: '0.9375rem', color: '#334155', lineHeight: 1.55 }}>
                            Yêu cầu này áp dụng cho <strong>tất cả chương</strong> bạn đang giữ trong truyện
                            {releaseConfirmTarget.chapterCount > 0 ? (
                                <> (hiện <strong>{releaseConfirmTarget.chapterCount}</strong> chương)</>
                            ) : null}
                            {' '}và phần nhận duyệt <strong>truyện</strong> (nếu bạn đã nhận). Trong lúc đơn chờ xử lý, bạn không thể duyệt/từ chối chương của truyện này.
                        </p>
                        {releaseConfirmTarget.storyTitle ? (
                            <p style={{ margin: '0 0 0.75rem', fontSize: '0.875rem', color: '#475569', padding: '0.65rem 0.75rem', backgroundColor: '#f8fafc', borderRadius: '8px', border: '1px solid #e2e8f0' }}>
                                <span style={{ color: '#64748b' }}>Truyện áp dụng:</span>{' '}
                                <strong style={{ color: '#0f172a' }}>&quot;{releaseConfirmTarget.storyTitle}&quot;</strong>
                            </p>
                        ) : null}
                        <label style={{ display: 'block', marginBottom: '0.75rem', fontSize: '0.8125rem', fontWeight: 600, color: '#334155' }}>
                            Lý do gửi đơn <span style={{ color: '#ef4444' }}>*</span> (tối thiểu 10 ký tự)
                            <textarea
                                value={releaseReason}
                                onChange={(e) => { setReleaseReason(e.target.value); if (releaseFormError) setReleaseFormError(''); }}
                                rows={4}
                                placeholder="Ví dụ: Không đủ thời gian xử lý hết khối chương, đề nghị trả về hàng đợi..."
                                disabled={!!releasingAllClaimsStoryId}
                                style={{
                                    display: 'block',
                                    width: '100%',
                                    marginTop: '0.35rem',
                                    padding: '0.5rem',
                                    borderRadius: '8px',
                                    border: '1px solid #cbd5e1',
                                    fontFamily: 'inherit',
                                    resize: 'vertical',
                                    boxSizing: 'border-box',
                                }}
                            />
                        </label>
                        {releaseFormError ? (
                            <p style={{ margin: '0 0 0.75rem', fontSize: '0.8125rem', color: '#b91c1c' }}>{releaseFormError}</p>
                        ) : null}
                        <div style={{ display: 'flex', justifyContent: 'flex-end', gap: '0.5rem', flexWrap: 'wrap' }}>
                            <button
                                type="button"
                                disabled={!!releasingAllClaimsStoryId}
                                onClick={() => { setReleaseConfirmTarget(null); setReleaseFormError(''); }}
                                style={{
                                    padding: '0.5rem 1rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    backgroundColor: '#f1f5f9',
                                    color: '#475569',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: releasingAllClaimsStoryId ? 'wait' : 'pointer',
                                }}
                            >
                                Đóng
                            </button>
                            <button
                                type="button"
                                disabled={!!releasingAllClaimsStoryId}
                                onClick={handleConfirmReleaseAllClaims}
                                style={{
                                    padding: '0.5rem 1rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    backgroundColor: releasingAllClaimsStoryId ? '#94a3b8' : '#dc2626',
                                    color: '#fff',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: releasingAllClaimsStoryId ? 'wait' : 'pointer',
                                }}
                            >
                                {releasingAllClaimsStoryId ? 'Đang gửi...' : 'Gửi đơn lên quản trị'}
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Thông báo đã gửi đơn thành công */}
            {releaseResultMessage && (
                <div
                    role="dialog"
                    aria-modal="true"
                    style={{
                        position: 'fixed',
                        inset: 0,
                        backgroundColor: 'rgba(0,0,0,0.5)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 10051,
                        padding: '1rem',
                    }}
                    onClick={() => setReleaseResultMessage(null)}
                >
                    <div
                        style={{
                            backgroundColor: '#fff',
                            borderRadius: '12px',
                            padding: '1.5rem',
                            maxWidth: '400px',
                            width: '100%',
                            boxShadow: '0 20px 40px rgba(0,0,0,0.15)',
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <p style={{ margin: '0 0 1rem', fontSize: '0.9375rem', color: '#334155', lineHeight: 1.5 }}>
                            {releaseResultMessage}
                        </p>
                        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
                            <button
                                type="button"
                                onClick={() => setReleaseResultMessage(null)}
                                style={{
                                    padding: '0.5rem 1rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    backgroundColor: '#0ea5e9',
                                    color: '#fff',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: 'pointer',
                                }}
                            >
                                Đã hiểu
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {policyModalOpen && (
                <div
                    style={{
                        position: 'fixed',
                        inset: 0,
                        backgroundColor: 'rgba(0,0,0,0.5)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 10060,
                        padding: '1rem',
                    }}
                    onClick={() => setPolicyModalOpen(false)}
                >
                    <div
                        style={{
                            backgroundColor: '#fff',
                            borderRadius: '12px',
                            maxWidth: '900px',
                            width: '100%',
                            maxHeight: '85vh',
                            overflow: 'auto',
                            boxShadow: '0 20px 40px rgba(0,0,0,0.15)',
                            border: '1px solid #e2e8f0',
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div style={{ padding: '1rem 1.25rem', borderBottom: '1px solid #e2e8f0', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <h2 style={{ margin: 0, fontSize: '1.125rem', fontWeight: 700, color: '#1e293b' }}>Chính sách hệ thống cho kiểm duyệt viên</h2>
                            <button
                                type="button"
                                onClick={() => setPolicyModalOpen(false)}
                                style={{ background: 'none', border: 'none', fontSize: '1.5rem', cursor: 'pointer', color: '#64748b', lineHeight: 1 }}
                                aria-label="Đóng"
                            >
                                ×
                            </button>
                        </div>
                        <div style={{ padding: '1rem 1.25rem' }}>
                            {policyLoading ? (
                                <p style={{ margin: 0, color: '#64748b', fontSize: '0.875rem' }}>Đang tải chính sách tác giả...</p>
                            ) : policyError ? (
                                <div style={{ padding: '0.75rem 1rem', borderRadius: '8px', border: '1px solid #fecaca', background: '#fef2f2', color: '#991b1b', fontSize: '0.875rem' }}>
                                    {policyError}
                                </div>
                            ) : authorPolicy ? (
                                <>
                                    <div style={{ marginBottom: '0.75rem', fontSize: '0.8125rem', color: '#64748b' }}>
                                        Chính sách tác giả đang áp dụng{authorPolicy.version ? ` · phiên bản v${authorPolicy.version}` : ''}.
                                    </div>
                                    <PolicyBody content={authorPolicy.content} />
                                </>
                            ) : (
                                <p style={{ margin: 0, color: '#64748b', fontSize: '0.875rem' }}>Chưa có chính sách tác giả đang áp dụng.</p>
                            )}
                        </div>
                    </div>
                </div>
            )}

            {processModalOpen && (
                <div
                    style={{
                        position: 'fixed',
                        inset: 0,
                        backgroundColor: 'rgba(0,0,0,0.5)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 10060,
                        padding: '1rem',
                    }}
                    onClick={() => setProcessModalOpen(false)}
                >
                    <div
                        style={{
                            backgroundColor: '#fff',
                            borderRadius: '12px',
                            maxWidth: '760px',
                            width: '100%',
                            maxHeight: '85vh',
                            overflow: 'auto',
                            boxShadow: '0 20px 40px rgba(0,0,0,0.15)',
                            border: '1px solid #e2e8f0',
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div style={{ padding: '1rem 1.25rem', borderBottom: '1px solid #e2e8f0', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                            <h2 style={{ margin: 0, fontSize: '1.125rem', fontWeight: 700, color: '#1e293b' }}>Quy trình duyệt xuất bản dành cho kiểm duyệt viên</h2>
                            <button
                                type="button"
                                onClick={() => setProcessModalOpen(false)}
                                style={{ background: 'none', border: 'none', fontSize: '1.5rem', cursor: 'pointer', color: '#64748b', lineHeight: 1 }}
                                aria-label="Đóng"
                            >
                                ×
                            </button>
                        </div>
                        <div style={{ padding: '1rem 1.25rem', color: '#334155', fontSize: '0.875rem', lineHeight: 1.6 }}>
                            <p style={{ margin: '0 0 0.5rem', fontWeight: 700, color: '#1e293b' }}>Quy trình đề xuất (ngắn gọn):</p>
                            <ol style={{ margin: 0, paddingLeft: '1.25rem', display: 'grid', gap: '0.45rem' }}>
                                <li>Nhận đơn duyệt từ hàng đợi và kiểm tra thông tin truyện/chương cần xử lý.</li>
                                <li>Đọc nội dung, đối chiếu chính sách tác giả và tiêu chí xuất bản của hệ thống.</li>
                                <li>Ra quyết định chấp nhận hoặc từ chối, ghi chú rõ ràng và đúng trọng tâm.</li>
                                <li>Nếu có vướng mắc thẩm quyền, gửi đơn lên quản trị viên để xin hỗ trợ xử lý.</li>
                                <li>Hoàn tất đơn và kiểm tra trạng thái đã cập nhật đúng trong danh sách/nhật ký.</li>
                            </ol>
                        </div>
                    </div>
                </div>
            )}

            {/* Detail Modal */}
            {selectedPublication && (
                <PublicationDetailModal
                    publication={selectedPublication}
                    onClose={handleCloseDetail}
                    onApprove={handleApprove}
                    onReject={handleReject}
                    onRefresh={() => {
                        loadPublications(currentPage);
                        loadStats();
                        loadRejectedCache();
                    }}
                    onClaimStory={handleClaimStory}
                    claimingId={claimingId}
                />
            )}
        </div>
    );
}
