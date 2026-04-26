import { useState, useEffect, useCallback, useRef } from 'react';
import { Plus, Edit, Eye, Heart, Star, ChevronRight, Book, User, LogOut, Trash2, List, Wallet, History, Coins, ArrowDownToLine, Landmark, Percent, X } from 'lucide-react';
import { StoryEditor } from './StoryEditor';
import { StoryInfoEditor } from './StoryInfoEditor';
import { ChapterListManager } from '../author/ChapterListManager';
import { StoryCommentsViewer } from './StoryCommentsViewer';
import { ChapterEditorPage } from '../author/ChapterEditorPage';
import { Footer } from '../../components/homepage/Footer';
import { Header } from '../../components/homepage/Header';
import { createStory, updateStory, getStoriesByAuthor, getStoryById, deleteStory } from '../../api/story/storyApi';
import { createChapter, updateChapter, getChapterById, getChapters, createChapterVersion, updateChapterVersion, getChapterVersionById, submitChapterVersion } from '../../api/chapter/chapterApi';
import * as coinApi from '../../api/coins/coinApi';
import { getAuthorFollowersCount, getAuthorFollowers } from '../../api/author/authorApi';
import { resolveBackendUrl } from '../../utils/resolveBackendUrl';
import { createInitialAvatarDataUrl } from '../../utils/avatarFallback';
import { useAuth } from '../../contexts/AuthContext';
import { useToast } from '../../components/author/story-editor/Toast';
import { Pagination } from '../../components/pagination/Pagination';
import { setAuthorChapterListActive } from '../../utils/authorUiFlags';
import { useSearchParams, useNavigate } from 'react-router-dom';
import { getAiUsageLimit } from '../../api/ai/aiApi';
import { getNotifications } from '../../api/notification/notificationApi';

function mapStoryFromApi(item) {
    const status = item.status || item.Status || '';
    const storyProgressStatus = item.storyProgressStatus ?? item.StoryProgressStatus ?? '';
    const publishStatusMap = {
        DRAFT: 'Bản nháp',
        PENDING_REVIEW: 'Chờ duyệt',
        REJECTED: 'Bị từ chối',
        PUBLISHED: 'Đã xuất bản',
        HIDDEN: 'Đã ẩn vĩnh viễn',
        COMPLETED: 'Hoàn thành',
        CANCELLED: 'Đã hủy',
    };
    const progressStatusMap = {
        ONGOING: 'Đang ra',
        COMPLETED: 'Hoàn thành',
        HIATUS: 'Tạm dừng',
    };
    const publishStatus = publishStatusMap[status.toUpperCase()] ?? status;
    const progressStatusDisplay = progressStatusMap[storyProgressStatus.toUpperCase()] ?? progressStatusMap.ONGOING;
    // Lấy thể loại từ story_categories (CategoryIds + CategoryNames)
    const categoryIds = item.categoryIds ?? item.CategoryIds ?? [];
    const categoryNamesStr = item.categoryNames ?? item.CategoryNames ?? '';
    const categoryNamesArr = categoryNamesStr
        ? String(categoryNamesStr).split(',').map((s) => s.trim()).filter(Boolean)
        : [];
    const categories = Array.isArray(categoryIds) && categoryIds.length > 0
        ? categoryIds.map((id, i) => ({ id, name: categoryNamesArr[i] ?? '' })).filter((c) => c.id)
        : categoryNamesArr.map((name) => ({ id: name, name })); // fallback: chỉ có tên
    const updatedAt = item.updatedAt || item.UpdatedAt;
    const lastUpdate = updatedAt
        ? new Date(updatedAt).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
        : '';
    const coverPath = item.coverImage ?? item.CoverImage;
    const summary = item.summary ?? item.Summary ?? '';
    const ageRatingMap = { ALL: 'Phù hợp mọi lứa tuổi', '13+': 'Từ 13 tuổi', '16+': 'Từ 16 tuổi', '18+': 'Từ 18 tuổi' };
    const rawAge = item.ageRating ?? item.AgeRating ?? 'ALL';
    const ageRating = ageRatingMap[rawAge] ?? ageRatingMap.ALL;
    const follows = Number(
        item.totalFavorites
        ?? item.TotalFavorites
        ?? item.total_favorites
        ?? item.favoritesCount
        ?? item.FavoritesCount
        ?? item.followCount
        ?? item.FollowCount
        ?? 0
    );
    const ratingRaw =
        item.avgRating
        ?? item.AvgRating
        ?? item.avg_rating
        ?? item.rating
        ?? item.Rating
        ?? 0;
    const rating = Number(ratingRaw);
    const isComplianceHiddenFlag = Boolean(
        item.complianceHidden
        ?? item.ComplianceHidden
        ?? item.compliance_hidden
        ?? false
    );
    const isHiddenByStatus = String(status || '').toUpperCase() === 'HIDDEN';
    const isComplianceHidden = isComplianceHiddenFlag || isHiddenByStatus;
    return {
        id: item.id ?? item.Id,
        title: item.title ?? item.Title,
        cover: coverPath ? resolveBackendUrl(coverPath) : '',
        summary,
        ageRating,
        categories,
        status: status.toLowerCase(),
        chapters: item.publishedChaptersCount ?? item.PublishedChaptersCount ?? item.totalChapters ?? item.TotalChapters ?? 0,
        totalViews: Number(item.totalViews ?? item.TotalViews ?? 0),
        follows: Number.isFinite(follows) ? follows : 0,
        rating: Number.isFinite(rating) ? rating : 0,
        lastUpdate: lastUpdate || 'Chưa cập nhật',
        publishStatus,
        storyProgressStatus: storyProgressStatus || 'ONGOING',
        progressStatusDisplay,
        isComplianceHidden,
    };
}

const AUTHOR_WRITING_SUSPENDED_BANNER =
    'Tài khoản đang bị tạm đình chỉ quyền viết để điều tra vi phạm.';
const AUTHOR_WRITING_SUSPENDED_TOOLTIP =
    'Bạn đang bị tạm đình chỉ quyền viết để điều tra vi phạm.';

const BANK_ACCOUNT_NUMBER_MIN = 6;
const BANK_ACCOUNT_NUMBER_MAX = 19;
const BANK_ACCOUNT_HOLDER_MIN = 3;
const BANK_ACCOUNT_HOLDER_MAX = 100;
const BANK_BRANCH_MAX = 120;

/**
 * Kiểm tra form thêm TK ngân hàng (đồng bộ quy tắc hiển thị với BE: đủ trường + BIN + độ dài STK).
 * @returns {{ errors: Record<string, string>, normalized: { bankTrim: string, digits: string, holderForApi: string, branchTrim: string, bin: string } | null }}
 */
function validateAuthorBankAccountInput({ bankName, accountNumber, accountHolderName, branchName, bankOptions, bankBinMap }) {
    const errors = {};
    const bankTrim = String(bankName || '').trim();
    if (!bankTrim) {
        errors.bankName = 'Vui lòng chọn ngân hàng.';
    } else if (!bankOptions.includes(bankTrim)) {
        errors.bankName = 'Ngân hàng không hợp lệ.';
    } else if (!(String(bankBinMap[bankTrim] || '').trim())) {
        errors.bankName = 'Ngân hàng này chưa được cấu hình mã BIN. Vui lòng chọn ngân hàng khác.';
    }

    const digits = String(accountNumber || '').replace(/\D/g, '');
    if (!digits) {
        errors.accountNumber = 'Vui lòng nhập số tài khoản (chỉ chữ số).';
    } else if (digits.length < BANK_ACCOUNT_NUMBER_MIN || digits.length > BANK_ACCOUNT_NUMBER_MAX) {
        errors.accountNumber = `Số tài khoản cần từ ${BANK_ACCOUNT_NUMBER_MIN} đến ${BANK_ACCOUNT_NUMBER_MAX} chữ số.`;
    }

    const holderNorm = String(accountHolderName || '').trim().replace(/\s+/g, ' ');
    if (!holderNorm) {
        errors.accountHolderName = 'Vui lòng nhập tên chủ tài khoản.';
    } else if (holderNorm.length < BANK_ACCOUNT_HOLDER_MIN) {
        errors.accountHolderName = `Tên chủ tài khoản tối thiểu ${BANK_ACCOUNT_HOLDER_MIN} ký tự.`;
    } else if (holderNorm.length > BANK_ACCOUNT_HOLDER_MAX) {
        errors.accountHolderName = `Tên chủ tài khoản tối đa ${BANK_ACCOUNT_HOLDER_MAX} ký tự.`;
    } else if (!/^[\p{L}\s'.-]+$/u.test(holderNorm)) {
        errors.accountHolderName = 'Chỉ dùng chữ cái (có dấu), khoảng trắng và các ký tự . \' -';
    }

    const branchTrim = String(branchName || '').trim();
    if (branchTrim.length > BANK_BRANCH_MAX) {
        errors.branchName = `Chi nhánh tối đa ${BANK_BRANCH_MAX} ký tự.`;
    }

    if (Object.keys(errors).length > 0) {
        return { errors, normalized: null };
    }

    const holderForApi = holderNorm.toLocaleUpperCase('vi-VN');
    const bin = String(bankBinMap[bankTrim] || '').trim();
    return {
        errors,
        normalized: { bankTrim, digits, holderForApi, branchTrim, bin },
    };
}

export function AuthorStoryManagement({ onBack }) {
    const { user, logout } = useAuth();

    // Get user display name
    const getUserDisplayName = () => {
        if (!user) return 'Người dùng';
        return user.displayName ?? user.DisplayName ?? user.fullName ?? user.FullName ?? user.nickname ?? user.Nickname ?? user.userName ?? user.UserName ?? user.name ?? user.Name ?? 'Người dùng';
    };

    const userDisplayName = getUserDisplayName();
    const [activeView, setActiveView] = useState('stories');
    const [activeMenu, setActiveMenu] = useState('stories');
    const [currentStory, setCurrentStory] = useState(null);
    const [currentChapter, setCurrentChapter] = useState(null);
    /** Chương gốc khi đang tạo version (mở editor giống tạo chương mới nhưng pre-fill từ chương này). */
    const [sourceChapterForVersion, setSourceChapterForVersion] = useState(null);
    const [editingVersion, setEditingVersion] = useState(null);
    /** True khi mở màn editor chỉ để xem chi tiết chương (read-only), không lưu/xuất bản. */
    const [viewChapterOnly, setViewChapterOnly] = useState(false);
    const [stories, setStories] = useState([]);
    const [storiesLoading, setStoriesLoading] = useState(true);
    const [storiesError, setStoriesError] = useState(null);
    const [storiesCurrentPage, setStoriesCurrentPage] = useState(1);
    const [storiesTotalPages, setStoriesTotalPages] = useState(1);
    const [storiesTotalCount, setStoriesTotalCount] = useState(0);

    const STORIES_PAGE_SIZE = 10;
    const authorId = user?.id ?? user?.Id;
    const authorWritingSuspendedUntilRaw =
        user?.authorWritingSuspendedUntilUtc
        ?? user?.AuthorWritingSuspendedUntilUtc
        ?? user?.author_writing_suspended_until
        ?? user?.authorWritingSuspendedUntil
        ?? null;
    const authorWritingSuspendedUntilDate = authorWritingSuspendedUntilRaw
        ? new Date(/(Z|[+-]\d{2}:\d{2}|[+-]\d{4})$/i.test(String(authorWritingSuspendedUntilRaw).trim())
            ? String(authorWritingSuspendedUntilRaw).trim()
            : `${String(authorWritingSuspendedUntilRaw).trim()}Z`)
        : null;
    const isAuthorWritingSuspended = !!(authorWritingSuspendedUntilDate && !Number.isNaN(authorWritingSuspendedUntilDate.getTime()) && authorWritingSuspendedUntilDate.getTime() > Date.now());
    const [searchParams] = useSearchParams();
    const navigate = useNavigate();

    useEffect(() => {
        setAuthorChapterListActive(activeView === 'chapterList');
        return () => setAuthorChapterListActive(false);
    }, [activeView]);

    // Lịch sử donate + rút tiền (author)
    const [authorActivityItems, setAuthorActivityItems] = useState([]);
    const [authorActivityLoading, setAuthorActivityLoading] = useState(false);
    const [authorActivityError, setAuthorActivityError] = useState(null);
    const [cancelWithdrawConfirm, setCancelWithdrawConfirm] = useState({ open: false, withdrawId: null });
    // Rút tiền: số dư ví, số coin nhập, trạng thái gửi
    const [withdrawBalance, setWithdrawBalance] = useState(null);
    const [withdrawAmount, setWithdrawAmount] = useState('');
    const [withdrawSubmitting, setWithdrawSubmitting] = useState(false);
    const [withdrawError, setWithdrawError] = useState(null);
    // Rút tiền: chọn TK ngân hàng từ danh sách của author
    const [selectedBankAccountIdx, setSelectedBankAccountIdx] = useState(-1);

    // Danh sách ngân hàng (dùng cho form thêm tài khoản ngân hàng)
    // PayOS payout batch yêu cầu `toBin` (Bank BIN). FE tự ánh xạ từ ngân hàng đã chọn.
    const BANK_BIN_MAP = {
        Vietcombank: '970436',
        VietinBank: '970415',
        BIDV: '970418',
        Agribank: '970405',
        'Techcombank': '970407',
        'MB Bank': '970422',
        ACB: '970416',
        Sacombank: '970403',
        VPBank: '970432',
        TPBank: '970423',
        SHB: '970443',
        HDBank: '970437',
        OCB: '970448',
        VIB: '970441',
        Eximbank: '970431',
        // MSB: chưa map mặc định (cần nhập thủ công nếu PayOS yêu cầu).
    };

    const BANK_OPTIONS = [
        'Vietcombank',
        'VietinBank',
        'BIDV',
        'Agribank',
        'Techcombank',
        'MB Bank',
        'ACB',
        'Sacombank',
        'VPBank',
        'TPBank',
        'SHB',
        'HDBank',
        'OCB',
        'VIB',
        'Eximbank',
        'MSB',
    ];

    // UC-60: 100 coin = 10,000 VND => 1 coin = 100 VND
    const COIN_RATE_VND = 100;
    const MIN_WITHDRAW_VND = 50_000;
    const MIN_WITHDRAW_COINS = Math.floor(MIN_WITHDRAW_VND / COIN_RATE_VND); // 500

    const withdrawBalanceNum =
        withdrawBalance != null && withdrawBalance !== '' ? Number(withdrawBalance) : null;
    /** Tránh min > max trên input[type=number] (gây tooltip tiếng Anh sai nghĩa khi số dư < mức tối thiểu). */
    const withdrawInputUseNativeMinMax =
        withdrawBalanceNum != null &&
        Number.isFinite(withdrawBalanceNum) &&
        withdrawBalanceNum >= MIN_WITHDRAW_COINS;

    const formatVnd = (vnd) => {
        try {
            return new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND', maximumFractionDigits: 0 }).format(vnd || 0);
        } catch {
            return `${vnd || 0} VND`;
        }
    };

    // Form "Thêm tài khoản ngân hàng" (tách khỏi phần rút tiền)
    const [bankName, setBankName] = useState('');
    const [accountNumber, setAccountNumber] = useState('');
    const [accountHolderName, setAccountHolderName] = useState('');
    const [branchName, setBranchName] = useState('');
    const [bankFieldErrors, setBankFieldErrors] = useState({});
    const [profileFollowersCount, setProfileFollowersCount] = useState(0);
    const [showBankModal, setShowBankModal] = useState(false);
    const [showHistoryModal, setShowHistoryModal] = useState(false);
    const [historyModalTab, setHistoryModalTab] = useState('donate');
    const [authorUnlockItems, setAuthorUnlockItems] = useState([]);
    const [authorUnlockLoading, setAuthorUnlockLoading] = useState(false);
    const [authorUnlockError, setAuthorUnlockError] = useState(null);
    const [authorUnlockPage, setAuthorUnlockPage] = useState(1);
    const [authorUnlockTotalCount, setAuthorUnlockTotalCount] = useState(0);
    const AUTHOR_UNLOCK_PAGE_SIZE = 20;
    const [showFollowersModal, setShowFollowersModal] = useState(false);
    const [followersItems, setFollowersItems] = useState([]);
    const [followersLoading, setFollowersLoading] = useState(false);
    const [followersError, setFollowersError] = useState(null);
    const [followersPage, setFollowersPage] = useState(1);
    const [followersPageSize] = useState(10);
    const [followersTotalCount, setFollowersTotalCount] = useState(0);
    const [followersSearchInput, setFollowersSearchInput] = useState('');
    const [followersSearchKeyword, setFollowersSearchKeyword] = useState('');
    const [authorAiBudget, setAuthorAiBudget] = useState(null);
    const [authorAiBudgetLoading, setAuthorAiBudgetLoading] = useState(false);
    const [authorAiBudgetError, setAuthorAiBudgetError] = useState(null);
    const [authorReportNotifications, setAuthorReportNotifications] = useState([]);
    const [authorReportLoading, setAuthorReportLoading] = useState(false);
    const [authorReportError, setAuthorReportError] = useState(null);
    const [reportStoryFilterId, setReportStoryFilterId] = useState('');

    // Danh sách tài khoản ngân hàng (load từ backend)
    const [bankAccounts, setBankAccounts] = useState([]);

    const maskAccountNumber = (value) => {
        const s = String(value || '').replace(/\s+/g, '');
        if (!s) return '—';
        if (s.length <= 4) return s;
        return `${'•'.repeat(Math.max(0, s.length - 4))}${s.slice(-4)}`;
    };

    const formatTime = (iso) => {
        const d = new Date(iso);
        if (Number.isNaN(d.getTime())) return iso || '—';
        return d.toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' });
    };

    const parseStoryIdFromNotificationLink = useCallback((linkUrl) => {
        const value = String(linkUrl || '');
        const match = value.match(/\/story\/([0-9a-fA-F-]{36})/i);
        return match?.[1] ?? '';
    }, []);

    const loadAuthorReportNotifications = useCallback(async () => {
        setAuthorReportLoading(true);
        setAuthorReportError(null);
        try {
            const list = await getNotifications({ limit: 100, onlyUnread: false });
            const reportTypes = new Set(['STORY_REPORTED_TO_AUTHOR', 'COMMENT_REPORTED_TO_OWNER']);
            const mapped = (Array.isArray(list) ? list : [])
                .filter((n) => reportTypes.has(String(n?.type ?? '').toUpperCase()))
                .map((n) => ({
                    ...n,
                    storyIdFromLink: parseStoryIdFromNotificationLink(n?.linkUrl),
                }))
                .sort((a, b) => {
                    const ta = Date.parse(a?.createdAt ?? '') || 0;
                    const tb = Date.parse(b?.createdAt ?? '') || 0;
                    return tb - ta;
                });
            setAuthorReportNotifications(mapped);
        } catch (err) {
            const message = err?.response?.data?.message ?? err?.message ?? 'Không tải được danh sách báo cáo.';
            setAuthorReportError(message);
            setAuthorReportNotifications([]);
        } finally {
            setAuthorReportLoading(false);
        }
    }, [parseStoryIdFromNotificationLink]);

    const withdrawBankAccounts = bankAccounts;
    const selectedBankAccount = withdrawBankAccounts[selectedBankAccountIdx] ?? null;

    const buildBankInfoStringFromAccount = (acc) => {
        if (!acc) return null;
        const bn = String(acc.bank_name || '').trim();
        const bb = String(acc.bank_bin || BANK_BIN_MAP[acc.bank_name] || '').trim();
        // PayOS toAccountNumber should be digits only; remove whitespace safely.
        const an = String(acc.account_number || '').replace(/[^\d]/g, '').trim();
        const ah = String(acc.account_holder_name || '').trim();
        const br = String(acc.branch_name || '').trim();
        const verified = !!acc.is_verified;
        if (!bn || !an || !ah) return null;
        return [
            `bank_name=${bn}`,
            `bank_bin=${bb || ''}`,
            `account_number=${an}`,
            `account_holder_name=${ah}`,
            `branch_name=${br || '-'}`,
            `is_verified=${verified ? '1' : '0'}`,
        ].join(' | ');
    };

    const loadStories = useCallback((page = 1, options = {}) => {
        if (!authorId) {
            setStories([]);
            setStoriesLoading(false);
            return;
        }
        const silent = options.silent === true;
        if (!silent) {
            setStoriesLoading(true);
            setStoriesError(null);
        }
        getStoriesByAuthor(authorId, { page, pageSize: STORIES_PAGE_SIZE })
            .then((res) => {
                const items = res?.items ?? res?.Items ?? [];
                const total = res?.totalCount ?? res?.totalItems ?? res?.total ?? items.length;
                const pages = res?.totalPages ?? Math.max(1, Math.ceil(total / STORIES_PAGE_SIZE));
                if (items.length === 0) {
                    setStories([]);
                    setStoriesTotalCount(0);
                    setStoriesTotalPages(1);
                    setStoriesCurrentPage(res?.page ?? page);
                    return;
                }
                // Trạng thái truyện: PUBLISHED nếu có ≥1 chương PUBLISHED; nếu không thì PENDING_REVIEW nếu có ≥1 chương PENDING_REVIEW; còn lại Bản nháp / Bị từ chối
                return Promise.all(
                    items.map((s) => {
                        const storyId = s.id ?? s.Id;
                        return Promise.all([
                            getStoryById(storyId, { recordView: false }).catch(() => null),
                            getChapters({ storyId, status: 'PUBLISHED', pageSize: 1 }),
                            getChapters({ storyId, status: 'PENDING_REVIEW', pageSize: 1 })
                        ])
                            .then(([storyDetail, pubRes, pendRes]) => {
                                const pubList = Array.isArray(pubRes) ? pubRes : (pubRes?.items ?? pubRes?.Items ?? []);
                                const pendList = Array.isArray(pendRes) ? pendRes : (pendRes?.items ?? pendRes?.Items ?? []);
                                const complianceHidden =
                                    storyDetail?.complianceHidden
                                    ?? storyDetail?.ComplianceHidden
                                    ?? storyDetail?.compliance_hidden
                                    ?? s?.complianceHidden
                                    ?? s?.ComplianceHidden
                                    ?? s?.compliance_hidden
                                    ?? false;
                                return {
                                    ...s,
                                    _hasPublishedChapter: pubList.length > 0,
                                    _hasPendingReviewChapter: pendList.length > 0,
                                    _complianceHidden: Boolean(complianceHidden),
                                };
                            })
                            .catch(() => ({ ...s, _hasPublishedChapter: false, _hasPendingReviewChapter: false, _complianceHidden: false }));
                    })
                ).then((itemsWithFlag) => {
                    setStories(
                        itemsWithFlag.map((item) => {
                            const mapped = mapStoryFromApi({
                                ...item,
                                complianceHidden: item._complianceHidden,
                            });
                            // Giữ lại flag FE để chặn cập nhật trạng thái tiến độ khi có chương đang chờ duyệt.
                            mapped._hasPendingReviewChapter = item._hasPendingReviewChapter === true;
                            if (mapped.isComplianceHidden) {
                                const isPermanentHidden = String(item.status ?? item.Status ?? '').toUpperCase() === 'HIDDEN';
                                mapped.status = 'hidden';
                                mapped.publishStatus = isPermanentHidden ? 'Đã ẩn vĩnh viễn' : 'Đã ẩn tạm thời';
                                mapped.isPermanentlyHidden = isPermanentHidden;
                                return mapped;
                            }
                            const hasPublished = item._hasPublishedChapter === true;
                            const hasPendingReview = item._hasPendingReviewChapter === true;
                            if (hasPublished) {
                                mapped.status = 'published';
                                mapped.publishStatus = 'Đã xuất bản';
                            } else if (hasPendingReview) {
                                mapped.status = 'pending_review';
                                mapped.publishStatus = 'Chờ duyệt';
                                if (!silent) {
                                    const currentStatus = (item.status ?? item.Status ?? '').toUpperCase();
                                    if (currentStatus !== 'PENDING_REVIEW') {
                                        const id = item.id ?? item.Id;
                                        const categoryIds = item.categoryIds ?? item.CategoryIds ?? [];
                                        const ids = Array.isArray(categoryIds) ? categoryIds : [];
                                        updateStory(id, {
                                            title: item.title ?? item.Title ?? 'Untitled',
                                            summary: item.summary ?? item.Summary ?? '',
                                            categoryIds: ids,
                                            status: 'PENDING_REVIEW',
                                            ageRating: item.ageRating ?? item.AgeRating ?? 'ALL',
                                            storyProgressStatus: item.storyProgressStatus ?? item.StoryProgressStatus ?? 'ONGOING'
                                        }).catch(() => { });
                                    }
                                }
                            } else {
                                mapped.status = 'draft';
                                mapped.publishStatus = 'Bản nháp';
                            }
                            return mapped;
                        })
                    );
                    setStoriesTotalCount(total);
                    setStoriesTotalPages(pages);
                    setStoriesCurrentPage(res?.page ?? page);
                });
            })
            .catch((err) => {
                if (!silent) {
                    setStoriesError(err?.message ?? 'Không tải được danh sách truyện');
                    setStories([]);
                    setStoriesTotalCount(0);
                    setStoriesTotalPages(1);
                }
            })
            .finally(() => { if (!silent) setStoriesLoading(false); });
    }, [authorId]);

    const handleStoriesPageChange = (page) => {
        setStoriesCurrentPage(page);
        loadStories(page);
    };

    useEffect(() => {
        queueMicrotask(() => loadStories(1));
    }, [loadStories]);

    useEffect(() => {
        if (!authorId) {
            setProfileFollowersCount(0);
            return;
        }
        let cancelled = false;
        getAuthorFollowersCount(authorId)
            .then((n) => {
                if (!cancelled) setProfileFollowersCount(Number.isFinite(n) ? n : 0);
            })
            .catch(() => {
                if (!cancelled) setProfileFollowersCount(0);
            });
        return () => { cancelled = true; };
    }, [authorId]);

    const loadFollowers = useCallback(async (page = 1, keyword = '') => {
        if (!authorId) {
            setFollowersItems([]);
            setFollowersTotalCount(0);
            setFollowersError(null);
            setFollowersLoading(false);
            return;
        }
        setFollowersLoading(true);
        setFollowersError(null);
        try {
            const res = await getAuthorFollowers(authorId, { page, pageSize: followersPageSize, search: keyword });
            const items = res?.items ?? res?.Items ?? [];
            const total = Number(res?.totalCount ?? res?.TotalCount ?? items.length);
            const currentPage = Number(res?.page ?? res?.Page ?? page) || 1;
            setFollowersItems(Array.isArray(items) ? items : []);
            setFollowersTotalCount(Number.isFinite(total) ? total : 0);
            setFollowersPage(currentPage > 0 ? currentPage : 1);
        } catch (err) {
            setFollowersItems([]);
            setFollowersTotalCount(0);
            setFollowersError(err?.response?.data?.message ?? err?.message ?? 'Không tải được danh sách người theo dõi.');
        } finally {
            setFollowersLoading(false);
        }
    }, [authorId, followersPageSize]);

    useEffect(() => {
        if (!showFollowersModal) return;
        loadFollowers(followersPage, followersSearchKeyword);
    }, [showFollowersModal, followersPage, followersSearchKeyword, loadFollowers]);

    useEffect(() => {
        if (!authorId) {
            setAuthorAiBudget(null);
            setAuthorAiBudgetError(null);
            setAuthorAiBudgetLoading(false);
            return;
        }
        if (activeView !== 'profile') return;
        let cancelled = false;
        const loadBudget = async () => {
            setAuthorAiBudgetLoading(true);
            setAuthorAiBudgetError(null);
            try {
                const data = await getAiUsageLimit();
                if (cancelled) return;
                setAuthorAiBudget(data?.authorTokenBudget ?? null);
            } catch (e) {
                if (cancelled) return;
                setAuthorAiBudget(null);
                setAuthorAiBudgetError(e?.response?.data?.message || e?.message || 'Không tải được token AI.');
            } finally {
                if (!cancelled) setAuthorAiBudgetLoading(false);
            }
        };
        loadBudget();
        return () => {
            cancelled = true;
        };
    }, [authorId, activeView]);

    useEffect(() => {
        if (activeView !== 'bank-accounts' && activeView !== 'history') return;
        setActiveView('profile');
        setActiveMenu('profile');
    }, [activeView]);

    useEffect(() => {
        const shouldPoll = showHistoryModal || activeView === 'withdraw';
        if (!shouldPoll || !authorId) return;

        let cancelled = false;
        let inFlight = false;
        let didInitialLoad = false;

        const fetchActivity = async ({ silent } = { silent: false }) => {
            if (inFlight) return;
            inFlight = true;
            try {
                if (!silent) {
                    setAuthorActivityLoading(true);
                    setAuthorActivityError(null);
                }

                const res = await coinApi.getAuthorActivity({ page: 1, pageSize: 100 });
                if (cancelled) return;

                if (res?.success && res?.data?.items) {
                    setAuthorActivityItems(res.data.items);
                    // Keep withdrawable balance in sync after admin approves / PayOS completes (poll only refreshed activity before).
                    if (activeView === 'withdraw') {
                        try {
                            const w = await coinApi.getMyWallet();
                            if (!cancelled && w?.success && w?.data != null) {
                                setWithdrawBalance(w.data.incomeBalance ?? w.data.income_balance ?? 0);
                            }
                        } catch {
                            /* ignore */
                        }
                    }
                } else {
                    if (!silent) {
                        setAuthorActivityItems([]);
                        if (!res?.success) setAuthorActivityError(res?.message ?? 'Không tải được lịch sử.');
                    }
                }
            } catch {
                if (!cancelled && !silent) {
                    setAuthorActivityItems([]);
                    setAuthorActivityError('Không tải được lịch sử donate và rút tiền.');
                }
            } finally {
                if (!cancelled) {
                    inFlight = false;
                    if (!silent) setAuthorActivityLoading(false);
                    didInitialLoad = true;
                }
            }
        };

        // Initial load
        fetchActivity({ silent: false });

        // Poll while history modal or withdraw tab is open, so PROCESSING -> COMPLETED/FAILED updates automatically.
        const intervalMs = 10000; // 10s
        const id = setInterval(() => {
            if (cancelled) return;
            if (typeof document !== 'undefined' && document.visibilityState !== 'visible') return;
            // After first load, poll silently to avoid loading spinner flicker.
            fetchActivity({ silent: didInitialLoad });
        }, intervalMs);

        return () => {
            cancelled = true;
            clearInterval(id);
        };
    }, [activeView, authorId, showHistoryModal]);

    const loadAuthorUnlockHistory = useCallback(async (page = 1) => {
        if (!authorId) {
            setAuthorUnlockItems([]);
            setAuthorUnlockTotalCount(0);
            setAuthorUnlockError(null);
            return;
        }
        setAuthorUnlockLoading(true);
        setAuthorUnlockError(null);
        try {
            const res = await coinApi.getAuthorUnlockChapterIncomeHistory({
                page,
                pageSize: AUTHOR_UNLOCK_PAGE_SIZE,
            });
            if (res?.success && res?.data) {
                setAuthorUnlockItems(res.data.items ?? res.data.Items ?? []);
                setAuthorUnlockTotalCount(Number(res.data.totalCount ?? res.data.TotalCount ?? 0));
                setAuthorUnlockPage(Number(res.data.page ?? res.data.Page ?? page) || 1);
            } else {
                setAuthorUnlockItems([]);
                setAuthorUnlockTotalCount(0);
                if (!res?.success) {
                    setAuthorUnlockError(res?.message ?? 'Không tải được lịch sử mở khóa chương.');
                }
            }
        } catch {
            setAuthorUnlockItems([]);
            setAuthorUnlockTotalCount(0);
            setAuthorUnlockError('Không tải được lịch sử mở khóa chương.');
        } finally {
            setAuthorUnlockLoading(false);
        }
    }, [authorId]);

    useEffect(() => {
        if (!showHistoryModal || !authorId) return;
        setHistoryModalTab('donate');
        loadAuthorUnlockHistory(1);
    }, [showHistoryModal, authorId, loadAuthorUnlockHistory]);

    const handleCancelWithdraw = (withdrawId) => {
        if (!withdrawId) return;
        setCancelWithdrawConfirm({ open: true, withdrawId });
    };

    const handleConfirmCancelWithdraw = async () => {
        const withdrawId = cancelWithdrawConfirm.withdrawId;
        setCancelWithdrawConfirm({ open: false, withdrawId: null });
        if (!withdrawId) return;

        setAuthorActivityLoading(true);
        setAuthorActivityError(null);
        try {
            const res = await coinApi.cancelWithdrawRequest(withdrawId);
            if (!res?.success) {
                setAuthorActivityError(res?.message ?? 'Không thể hủy yêu cầu rút tiền.');
                return;
            }

            showToast('Đã hủy yêu cầu rút tiền.', 'success');

            // Refresh wallet + history for current screen.
            const w = await coinApi.getMyWallet();
            if (w?.success && w?.data) {
                setWithdrawBalance(w.data.incomeBalance ?? w.data.income_balance ?? 0);
            }
            const ar = await coinApi.getAuthorActivity({ page: 1, pageSize: 100 });
            if (ar?.success && ar?.data?.items) setAuthorActivityItems(ar.data.items);
        } finally {
            setAuthorActivityLoading(false);
        }
    };

    useEffect(() => {
        if (activeView !== 'withdraw' || !authorId) return;
        setWithdrawError(null);
        coinApi.getMyWallet()
            .then((res) => {
                if (res?.success && res?.data != null) {
                    // Tác giả rút tiền từ `income_balance` (khả dụng để withdraw),
                    // còn `balance_coin` là phần spendable.
                    setWithdrawBalance(res.data.incomeBalance ?? res.data.income_balance ?? 0);
                } else {
                    setWithdrawBalance(0);
                }
            })
            .catch(() => setWithdrawBalance(0));
    }, [activeView, authorId]);

    // Load author bank accounts when rút tiền hoặc popup quản lý TK mở
    useEffect(() => {
        if ((!showBankModal && activeView !== 'withdraw') || !authorId) return;

        coinApi.getAuthorBankAccounts()
            .then((res) => {
                if (!res?.success) throw new Error(res?.message ?? 'Không tải được tài khoản ngân hàng.');

                const items = res?.data ?? [];
                const normalized = Array.isArray(items)
                    ? items.map((acc) => ({
                        ...acc,
                        bank_bin: acc.bank_bin || BANK_BIN_MAP[acc.bank_name] || '',
                        account_number: String(acc.account_number || '').replace(/[^\d]/g, ''),
                        account_holder_name: acc.account_holder_name ?? '',
                        branch_name: acc.branch_name ?? '',
                        is_verified: !!acc.is_verified,
                    }))
                    : [];

                setBankAccounts(normalized);
                setSelectedBankAccountIdx(normalized.length > 0 ? 0 : -1);
            })
            .catch(() => {
                setBankAccounts([]);
                setSelectedBankAccountIdx(-1);
            });
    }, [showBankModal, activeView, authorId]);

    /** Real-time: refetch danh sách truyện khi tab đang hiển thị (moderator duyệt/từ chối → trạng thái truyện thay đổi). */
    const STORIES_POLL_INTERVAL_MS = 1000;
    const storiesCurrentPageRef = useRef(storiesCurrentPage);
    const loadStoriesRef = useRef(loadStories);
    useEffect(() => {
        storiesCurrentPageRef.current = storiesCurrentPage;
        loadStoriesRef.current = loadStories;
    }, [storiesCurrentPage, loadStories]);
    useEffect(() => {
        if (activeView !== 'stories' || !authorId) return;
        const tick = () => {
            if (typeof document !== 'undefined' && document.visibilityState === 'visible') {
                loadStoriesRef.current?.(storiesCurrentPageRef.current, { silent: true });
            }
        };
        const id = setInterval(tick, STORIES_POLL_INTERVAL_MS);
        return () => clearInterval(id);
    }, [activeView, authorId]);

    // Mock comments data
    const mockComments = [
        {
            id: 1,
            userName: 'Nguyễn Văn A',
            userAvatar: 'https://api.dicebear.com/7.x/avataaars/svg?seed=user1',
            time: '2 giờ trước',
            content: 'Truyện hay quá! Mong tác giả cập nhật thêm nhiều chương.',
            likes: 15
        },
        {
            id: 2,
            userName: 'Trần Thị B',
            userAvatar: 'https://api.dicebear.com/7.x/avataaars/svg?seed=user2',
            time: '5 giờ trước',
            content: 'Nhân vật chính rất hay, tính cách rõ ràng.',
            likes: 8
        },
    ];

    const userStats = {
        published: stories.filter((s) => s.status === 'published').length,
        totalChapters: stories.reduce((acc, s) => acc + s.chapters, 0),
        totalViews: stories.reduce((acc, s) => acc + (Number(s.totalViews) || 0), 0),
        followers: profileFollowersCount,
    };
    const authorAiUnlimited = !!(authorAiBudget?.unlimitedLifetime);
    const authorAiLimitText = authorAiBudgetLoading
        ? '...'
        : (authorAiUnlimited
            ? 'Không giới hạn'
            : Number(authorAiBudget?.tokenLimit ?? 0).toLocaleString('vi-VN'));

    const handleCreateStory = () => {
        if (isAuthorWritingSuspended) {
            showToast(AUTHOR_WRITING_SUSPENDED_TOOLTIP, 'error');
            return;
        }
        setCurrentStory(null);
        setActiveView('createStory');
    };

    const handleEditStory = async (story) => {
        if (isAuthorWritingSuspended) {
            showToast(AUTHOR_WRITING_SUSPENDED_TOOLTIP, 'error');
            return;
        }
        const isComplianceHidden = Boolean(
            story?.isComplianceHidden
            ?? story?.complianceHidden
            ?? story?.ComplianceHidden
            ?? story?.compliance_hidden
            ?? false
        ) || String(story?.status ?? '').toLowerCase() === 'hidden';
        if (isComplianceHidden) {
            showToast('Truyện đã bị ẩn vĩnh viễn do vi phạm, không thể chỉnh sửa.', 'error');
            return;
        }
        const statusLower = String(story?.status ?? '').toLowerCase();
        if (statusLower === 'pending_review') {
            showToast('Truyện đang ở trạng thái chờ duyệt, bạn không thể chỉnh sửa lúc này.', 'error');
            return;
        }
        if (!story?.id) return;
        try {
            const fullStory = await getStoryById(story.id);
            const mapped = mapStoryFromApi(fullStory);
            // Tính flag chapter đang chờ duyệt để StoryInfoEditor chặn cập nhật Tạm dừng/Hoàn thành.
            // Chỉ cần biết có tồn tại >= 1 chapter pending_review.
            try {
                const pendRes = await getChapters({ storyId: story.id, status: 'PENDING_REVIEW', pageSize: 1 });
                const pendList = Array.isArray(pendRes) ? pendRes : (pendRes?.items ?? pendRes?.Items ?? []);
                mapped._hasPendingReviewChapter = pendList.length > 0;
            } catch {
                mapped._hasPendingReviewChapter = false;
            }
            setCurrentStory(mapped);
            setActiveView('editInfo');
        } catch (err) {
            showToast(err?.response?.data?.message ?? err?.message ?? 'Không tải được thông tin truyện', 'error');
        }
    };

    const handleViewChapters = (story) => {
        setCurrentStory(story);
        setActiveView('chapterList');
    };

    const handleAddChapter = async (story) => {
        if (isAuthorWritingSuspended) {
            showToast(AUTHOR_WRITING_SUSPENDED_TOOLTIP, 'error');
            return;
        }
        const isComplianceHidden = Boolean(
            story?.isComplianceHidden
            ?? story?.complianceHidden
            ?? story?.ComplianceHidden
            ?? story?.compliance_hidden
            ?? false
        ) || String(story?.status ?? '').toLowerCase() === 'hidden';
        if (isComplianceHidden) {
            showToast('Truyện đã bị ẩn vĩnh viễn do vi phạm, không thể thêm chương mới.', 'error');
            return;
        }
        const storyId = story?.id ?? story?.Id;
        if (!storyId) {
            showToast('Không tìm thấy truyện', 'error');
            return;
        }
        try {
            const res = await getChapters({ storyId, page: 1, pageSize: 500 });
            const items = res?.items ?? res?.Items ?? [];
            const arr = Array.isArray(items) ? items : [];
            const nextOrderIndex = arr.length > 0
                ? Math.max(...arr.map((c) => Number(c.orderIndex ?? c.OrderIndex ?? 0))) + 1
                : 0;
            const draftChapterId = crypto.randomUUID();
            const chapterNumber = nextOrderIndex + 1;
            setCurrentStory(story);
            setCurrentChapter({
                id: draftChapterId,
                number: chapterNumber,
                title: '',
                content: '',
                status: 'draft',
                accessType: 'public',
                price: 0,
            });
            setActiveView('addChapter');
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? 'Không thể mở tạo chương mới';
            showToast(msg, 'error');
        }
    };

    const handleEditChapter = async (chapter) => {
        if (isAuthorWritingSuspended) {
            showToast(AUTHOR_WRITING_SUSPENDED_TOOLTIP, 'error');
            return;
        }
        setViewChapterOnly(false);
        const chapterId = chapter?.id ?? chapter?.Id;
        if (!chapterId) {
            showToast('Không tìm thấy ID chương', 'error');
            return;
        }

        try {
            const fullChapter = await getChapterById(chapterId);
            const status = (fullChapter.status ?? fullChapter.Status ?? 'DRAFT').toUpperCase();
            const accessTypeApi = (fullChapter.accessType ?? fullChapter.AccessType ?? 'FREE').toUpperCase();
            const mappedChapter = {
                id: fullChapter.id ?? fullChapter.Id,
                number: (fullChapter.orderIndex ?? fullChapter.OrderIndex ?? 0) + 1,
                title: fullChapter.title ?? fullChapter.Title ?? '',
                content: fullChapter.content ?? fullChapter.Content ?? '',
                status: status.toLowerCase(),
                accessType: accessTypeApi === 'PAID' ? 'paid' : 'public',
                price: fullChapter.coinPrice ?? fullChapter.CoinPrice ?? 0,
            };
            setCurrentChapter(mappedChapter);
            setActiveView('editChapter');
        } catch (error) {
            const errorMessage = error?.response?.data?.message || error?.message || 'Không thể tải thông tin chương';
            showToast(errorMessage, 'error');
            console.error('Error loading chapter:', error);
        }
    };

    /** Mở màn xem chi tiết chương (read-only, cùng giao diện editor nhưng không chỉnh sửa/lưu). */
    const handleViewChapter = async (chapter) => {
        setViewChapterOnly(true);
        const chapterId = chapter?.id ?? chapter?.Id;
        if (!chapterId) {
            showToast('Không tìm thấy ID chương', 'error');
            return;
        }
        try {
            const fullChapter = await getChapterById(chapterId);
            const status = (fullChapter.status ?? fullChapter.Status ?? 'DRAFT').toUpperCase();
            const accessTypeApi = (fullChapter.accessType ?? fullChapter.AccessType ?? 'FREE').toUpperCase();
            const mappedChapter = {
                id: fullChapter.id ?? fullChapter.Id,
                number: (fullChapter.orderIndex ?? fullChapter.OrderIndex ?? 0) + 1,
                title: fullChapter.title ?? fullChapter.Title ?? '',
                content: fullChapter.content ?? fullChapter.Content ?? '',
                status: status.toLowerCase(),
                accessType: accessTypeApi === 'PAID' ? 'paid' : 'public',
                price: fullChapter.coinPrice ?? fullChapter.CoinPrice ?? 0,
            };
            setCurrentChapter(mappedChapter);
            setActiveView('editChapter');
        } catch (error) {
            const errorMessage = error?.response?.data?.message || error?.message || 'Không thể tải thông tin chương';
            showToast(errorMessage, 'error');
            setViewChapterOnly(false);
        }
    };

    /** Mở màn tạo version mới: form để trống, chỉ cần chapter id + số chương để hiển thị. */
    const handleAddVersion = (story, chapterFromList) => {
        if (isAuthorWritingSuspended) {
            showToast(AUTHOR_WRITING_SUSPENDED_TOOLTIP, 'error');
            return;
        }
        const chapterId = chapterFromList?.id ?? chapterFromList?.Id;
        if (!chapterId) {
            showToast('Không tìm thấy ID chương', 'error');
            return;
        }
        const number = chapterFromList?.number ?? (chapterFromList?.orderIndex ?? chapterFromList?.OrderIndex ?? chapterFromList?.order_index ?? 0) + 1;
        const title = chapterFromList?.title ?? chapterFromList?.name ?? `Chương ${number}`;
        setCurrentStory(story);
        setCurrentChapter(null);
        const chStatus = (chapterFromList?.status ?? 'draft').toString().toLowerCase();
        setSourceChapterForVersion({
            id: chapterId,
            number: Number(number) || 1,
            title,
            content: '',
            status: chStatus,
            accessType: 'public',
            price: 0,
        });
        setEditingVersion(null);
        setActiveView('addChapterVersion');
    };

    /** Mở editor chỉnh sửa version đã có: load chi tiết version rồi mở ChapterEditorPage ở chế độ edit version. */
    const handleEditVersion = async (chapter, versionFromList) => {
        if (isAuthorWritingSuspended) {
            showToast(AUTHOR_WRITING_SUSPENDED_TOOLTIP, 'error');
            return;
        }
        const chapterId = chapter?.id ?? chapter?.Id;
        const versionId = versionFromList?.id ?? versionFromList?.Id;
        if (!chapterId || !versionId) {
            showToast('Không tìm thấy chương hoặc phiên bản', 'error');
            return;
        }
        try {
            const detail = await getChapterVersionById(chapterId, versionId);
            const id = detail.id ?? detail.Id;
            const titleSnapshot = detail.titleSnapshot ?? detail.TitleSnapshot ?? detail.title_snapshot ?? '';
            const contentSnapshot = detail.contentSnapshot ?? detail.ContentSnapshot ?? detail.content_snapshot ?? '';
            const versionNumber = detail.versionNumber ?? detail.VersionNumber ?? detail.version_number ?? 1;
            const chapterNumber = chapter.number ?? (chapter.orderIndex ?? chapter.order_index ?? 0) + 1;
            const sourceMapped = {
                id: chapterId,
                number: Number(chapterNumber) || 1,
                title: chapter.title ?? chapter.name ?? `Chương ${chapterNumber}`,
                status: (chapter.status ?? chapter.Status ?? 'draft').toString().toLowerCase(),
            };
            setSourceChapterForVersion(sourceMapped);
            setEditingVersion({
                id,
                chapterId,
                titleSnapshot,
                contentSnapshot,
                versionNumber: Number(versionNumber) || 1,
                status: detail.status ?? detail.Status,
            });
            setActiveView('addChapterVersion');
        } catch (error) {
            const msg = error?.response?.data?.message || error?.message || 'Không thể tải phiên bản';
            showToast(msg, 'error');
        }
    };

    /** Mở xem chi tiết phiên bản (read-only). */
    const handleViewVersion = async (chapter, versionFromList) => {
        const chapterId = chapter?.id ?? chapter?.Id;
        const versionId = versionFromList?.id ?? versionFromList?.Id;
        if (!chapterId || !versionId) {
            showToast('Không tìm thấy chương hoặc phiên bản', 'error');
            return;
        }
        try {
            const detail = await getChapterVersionById(chapterId, versionId);
            const id = detail.id ?? detail.Id;
            const titleSnapshot = detail.titleSnapshot ?? detail.TitleSnapshot ?? detail.title_snapshot ?? '';
            const contentSnapshot = detail.contentSnapshot ?? detail.ContentSnapshot ?? detail.content_snapshot ?? '';
            const versionNumber = detail.versionNumber ?? detail.VersionNumber ?? detail.version_number ?? 1;
            const chapterNumber = chapter.number ?? (chapter.orderIndex ?? chapter.order_index ?? 0) + 1;
            const sourceMapped = {
                id: chapterId,
                number: Number(chapterNumber) || 1,
                title: chapter.title ?? chapter.name ?? `Chương ${chapterNumber}`,
                status: (chapter.status ?? chapter.Status ?? 'draft').toString().toLowerCase(),
            };
            setViewChapterOnly(true);
            setSourceChapterForVersion(sourceMapped);
            setEditingVersion({
                id,
                chapterId,
                titleSnapshot,
                contentSnapshot,
                versionNumber: Number(versionNumber) || 1,
                status: detail.status ?? detail.Status,
            });
            setActiveView('addChapterVersion');
        } catch (error) {
            const msg = error?.response?.data?.message || error?.message || 'Không thể tải phiên bản';
            showToast(msg, 'error');
        }
    };

    /** Sau khi lưu chương + (tuỳ chọn) popup so sánh AI — ChapterEditorPage gọi khi hoàn tất. */
    const navigateAwayFromChapterEditor = () => {
        setActiveView('chapterList');
        setCurrentChapter(null);
        setSourceChapterForVersion(null);
        setEditingVersion(null);
        setViewChapterOnly(false);
    };

    const handleSaveChapter = async (chapterData) => {
        if (isAuthorWritingSuspended && !viewChapterOnly) {
            showToast(AUTHOR_WRITING_SUSPENDED_TOOLTIP, 'error');
            throw new Error('AUTHOR_WRITING_SUSPENDED');
        }
        const storyId = currentStory?.id ?? currentStory?.Id;
        if (!storyId) {
            showToast('Không tìm thấy truyện', 'error');
            throw new Error('NO_STORY');
        }

        try {
            // Chế độ version: tạo mới hoặc cập nhật version; nếu status === 'published' thì gửi duyệt (giống nút Xuất bản ở danh sách phiên bản)
            if (chapterData.sourceChapterId) {
                const chapterId = chapterData.sourceChapterId;
                const titleSnapshot = chapterData.title ?? '';
                const contentSnapshot = chapterData.content ?? '';
                const shouldSubmitForReview = chapterData.status === 'published';
                let versionId = chapterData.editingVersionId;

                if (chapterData.editingVersionId) {
                    await updateChapterVersion(chapterId, chapterData.editingVersionId, { titleSnapshot, contentSnapshot });
                    showToast(shouldSubmitForReview ? 'Đã cập nhật và gửi phiên bản đi duyệt' : 'Đã cập nhật phiên bản', 'success');
                } else {
                    const created = await createChapterVersion(chapterId, { titleSnapshot, contentSnapshot });
                    versionId = created?.id ?? created?.Id ?? created?.id;
                    showToast(shouldSubmitForReview && versionId ? 'Đã tạo và gửi phiên bản đi duyệt' : 'Đã tạo phiên bản', 'success');
                }

                if (shouldSubmitForReview && versionId) {
                    await submitChapterVersion(chapterId, versionId);
                }

                return { chapterId: null, mode: 'version' };
            }

            // Map status: 'draft' -> 'DRAFT', 'published' -> 'PENDING_REVIEW'
            const apiStatus = chapterData.status === 'published' ? 'PENDING_REVIEW' : 'DRAFT';

            // Map accessType: 'public' -> 'FREE', 'paid' -> 'PAID'
            const apiAccessType = chapterData.accessType === 'paid' ? 'PAID' : 'FREE';

            // Chỉ coi là chỉnh sửa khi đang ở view editChapter; addChapter luôn là tạo mới.
            const isEditMode = activeView === 'editChapter' && currentChapter && (currentChapter.id || currentChapter.Id);

            if (!isEditMode) {
                // Thêm chương mới
                const orderIndex = (chapterData.number || 1) - 1; // number bắt đầu từ 1, orderIndex từ 0

                const created = await createChapter({
                    id: chapterData.id ?? currentChapter?.id ?? currentChapter?.Id,
                    storyId,
                    title: chapterData.title,
                    content: chapterData.content || '',
                    orderIndex,
                    status: apiStatus,
                    accessType: apiAccessType,
                    coinPrice: apiAccessType === 'PAID' ? (chapterData.price || 0) : 0,
                    aiSimilarityPercent: chapterData.aiSimilarityPercent,
                });

                const newChapterId = created?.id ?? created?.Id;
                showToast(
                    apiStatus === 'DRAFT' ? 'Đã lưu nháp chương mới' : 'Đã xuất bản chương mới',
                    'success'
                );
                return { chapterId: newChapterId };
            }
            // Cập nhật chương hiện có
            const chapterId = currentChapter.id ?? currentChapter.Id;
            if (!chapterId) {
                showToast('Không tìm thấy ID chương', 'error');
                throw new Error('NO_CHAPTER_ID');
            }

            await updateChapter(chapterId, {
                title: chapterData.title,
                content: chapterData.content || '',
                orderIndex: (chapterData.number || 1) - 1,
                status: apiStatus,
                accessType: apiAccessType,
                coinPrice: apiAccessType === 'PAID' ? (chapterData.price || 0) : 0,
                changeSummary: chapterData.changeSummary ? String(chapterData.changeSummary).trim() : undefined,
                aiSimilarityPercent: chapterData.aiSimilarityPercent,
            });

            showToast(
                apiStatus === 'DRAFT' ? 'Đã cập nhật chương (lưu nháp)' : 'Đã cập nhật chương (xuất bản)',
                'success'
            );
            return { chapterId };
        } catch (error) {
            const data = error?.response?.data;
            const errorMessage =
                (typeof data === 'string' && data.trim() ? data.trim() : null) ??
                data?.message ??
                data?.Message ??
                data?.detail ??
                data?.title ??
                error?.message ??
                'Không thể lưu chương';
            if (error?.message !== 'NO_STORY' && error?.message !== 'NO_CHAPTER_ID' && error?.message !== 'AUTHOR_WRITING_SUSPENDED') {
                showToast(errorMessage, 'error');
            }
            console.error('Error saving chapter:', error);
            throw error;
        }
    };

    const [deleteStoryConfirm, setDeleteStoryConfirm] = useState({ open: false, storyId: null });

    const handleDeleteStory = (storyId) => {
        if (isAuthorWritingSuspended) {
            showToast(AUTHOR_WRITING_SUSPENDED_TOOLTIP, 'error');
            return;
        }
        if (!storyId) return;
        setDeleteStoryConfirm({ open: true, storyId });
    };

    const handleConfirmDeleteStory = () => {
        const { storyId } = deleteStoryConfirm;
        if (!storyId) return;
        setDeleteStoryConfirm({ open: false, storyId: null });
        deleteStory(storyId)
            .then(() => loadStories(storiesCurrentPage))
            .catch((err) => {
                const msg = err?.response?.data?.message ?? err?.message ?? 'Xóa truyện thất bại';
                alert(msg);
            });
    };

    const handleSaveStory = async (storyData) => {
        if (currentStory) {
            setStories(stories.map(s => s.id === currentStory.id ? { ...s, ...storyData } : s));
            setActiveView('stories');
            setCurrentStory(null);
            return;
        }

        const payload = {
            title: storyData.title,
            summary: storyData.note || '',
            categoryIds: storyData.categoryIds || [],
            ageRating: storyData.ageRating,
            storyProgressStatus: 'Đang ra',
            coverImage: storyData.coverFile || storyData.cover,
        };
        const created = await createStory(payload);
        const storyId = created?.id ?? created?.Id;

        const chaptersData = storyData.chaptersData || [];
        for (let i = 0; i < chaptersData.length; i++) {
            const ch = chaptersData[i];
            await createChapter({
                storyId,
                title: ch.title,
                content: ch.content || '',
                orderIndex: i,
                status: ch.status || 'DRAFT',
                accessType: ch.accessType || 'FREE',
                coinPrice: ch.coinPrice || 0,
            });
        }

        if (!storyData.isDraft) {
            await updateStory(storyId, {
                title: storyData.title,
                summary: storyData.note || '',
                categoryIds: storyData.categoryIds || [],
                status: 'PENDING_REVIEW',
                ageRating: storyData.ageRating,
                storyProgressStatus: storyData.storyProgressStatus,
                coverImage: storyData.coverFile || storyData.cover,
            });
        }

        loadStories(storiesCurrentPage);
    };

    const { showToast, ToastContainer, clearToasts } = useToast();

    useEffect(() => {
        const view = searchParams.get('view');
        if (!view) return;

        if (view === 'profile') {
            setActiveView('profile');
            setActiveMenu('profile');
            navigate('/author', { replace: true });
            return;
        }
        if (view === 'stories') {
            setActiveView('stories');
            setActiveMenu('stories');
            navigate('/author', { replace: true });
            return;
        }
        if (view === 'reports') {
            const storyId = String(searchParams.get('storyId') || '').trim();
            setReportStoryFilterId(storyId);
            setActiveView('reports');
            setActiveMenu('reports');
            navigate('/author', { replace: true });
            return;
        }

        navigate('/author', { replace: true });
    }, [searchParams, navigate]);

    useEffect(() => {
        if (activeView !== 'reports') return;
        loadAuthorReportNotifications();
    }, [activeView, loadAuthorReportNotifications]);

    /** Chỉ xóa toasts khi vừa chuyển SANG màn danh sách chương (từ màn khác), tránh xóa mỗi lần re-render gây nhấp nháy. */
    const prevActiveViewRef = useRef(activeView);
    useEffect(() => {
        const prev = prevActiveViewRef.current;
        prevActiveViewRef.current = activeView;
        if (prev !== 'chapterList' && activeView === 'chapterList') clearToasts();
    }, [activeView, clearToasts]);

    const getCategoryId = (c) => (typeof c === 'object' && c?.id ? c.id : c);

    const handleSaveInfo = async (infoData) => {
        if (!currentStory?.id) return;
        try {
            const hasPendingReviewChapter = Boolean(currentStory?._hasPendingReviewChapter);
            const selectedProgressStatus = String(infoData?.status ?? infoData?.publishStatus ?? '').trim();
            if (hasPendingReviewChapter && (selectedProgressStatus === 'Tạm dừng' || selectedProgressStatus === 'Hoàn thành')) {
                showToast('Truyện đang có chương chờ duyệt, vui lòng thử lại sau.', 'error');
                return;
            }

            const storyTitle = String(infoData?.title ?? '').trim();
            if (!storyTitle) {
                showToast('Vui lòng nhập tên truyện', 'error');
                return;
            }
            if (storyTitle.length > 50) {
                showToast('Tên truyện không được vượt quá 50 ký tự', 'error');
                return;
            }

            const rawIds = (infoData.categories || []).map(getCategoryId).filter(Boolean);
            const categoryIds = rawIds.filter((id) =>
                /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/i.test(String(id))
            );
            if (categoryIds.length === 0) {
                showToast('Vui lòng chọn ít nhất một thể loại', 'error');
                return;
            }
            const storyPublishStatus = (currentStory.status || 'draft').toUpperCase();
            await updateStory(currentStory.id, {
                title: storyTitle,
                summary: infoData.note ?? '',
                categoryIds,
                status: storyPublishStatus,
                storyProgressStatus: infoData.status || infoData.publishStatus,
                ageRating: infoData.ageRating,
                coverImage: infoData.coverFile || infoData.cover,
            });

            // Update FE state đúng field (không ghi đè `status` publication bằng progress status UI).
            const uiProgress = infoData.status || infoData.publishStatus || currentStory?.progressStatusDisplay || 'Đang ra';
            const nextProgressApi =
                uiProgress === 'Tạm dừng' ? 'HIATUS' :
                    uiProgress === 'Hoàn thành' ? 'COMPLETED' :
                        'ONGOING';

            setStories(stories.map((s) => {
                if (s.id !== currentStory.id) return s;
                return {
                    ...s,
                    title: infoData.title,
                    summary: infoData.note ?? s.summary,
                    ageRating: infoData.ageRating ?? s.ageRating,
                    cover: infoData.cover ?? s.cover,
                    storyProgressStatus: nextProgressApi,
                    progressStatusDisplay: uiProgress,
                };
            }));

            setCurrentStory((prev) => {
                if (!prev) return null;
                return {
                    ...prev,
                    title: infoData.title,
                    summary: infoData.note ?? prev.summary,
                    ageRating: infoData.ageRating ?? prev.ageRating,
                    cover: infoData.cover ?? prev.cover,
                    storyProgressStatus: nextProgressApi,
                    progressStatusDisplay: uiProgress,
                };
            });

            showToast('Đã lưu thông tin truyện thành công.', 'success');
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.response?.data?.error ?? err?.message ?? 'Không thể lưu thay đổi';
            showToast(msg, 'error');
            throw err;
        }
    };

    // Render different views
    if (activeView === 'createStory') {
        return (
            <StoryEditor
                story={null}
                onSave={handleSaveStory}
                onCancel={() => {
                    setActiveView('stories');
                    setCurrentStory(null);
                }}
            />
        );
    }

    if (activeView === 'editInfo') {
        return (
            <>
                <StoryInfoEditor
                    story={currentStory}
                    onSave={handleSaveInfo}
                    onCancel={() => {
                        setActiveView('stories');
                        setCurrentStory(null);
                    }}
                />
                <ToastContainer />
            </>
        );
    }

    if (activeView === 'chapterList') {
        return (
            <>
                <ChapterListManager
                    story={currentStory}
                    isAuthorWritingSuspended={isAuthorWritingSuspended}
                    onBack={() => {
                        setActiveView('stories');
                        setCurrentStory(null);
                        loadStories(storiesCurrentPage);
                    }}
                    onAddChapter={() => handleAddChapter(currentStory)}
                    onEditChapter={(chapter) => handleEditChapter(chapter)}
                    onViewChapter={(chapter) => handleViewChapter(chapter)}
                    onAddVersion={(chapter) => handleAddVersion(currentStory, chapter)}
                    onEditVersion={(chapter, version) => handleEditVersion(chapter, version)}
                    onViewVersion={(chapter, version) => handleViewVersion(chapter, version)}
                />
                <ToastContainer />
            </>
        );
    }

    if (activeView === 'addChapter' || activeView === 'editChapter' || activeView === 'addChapterVersion') {
        return (
            <>
                {/* Cùng hook useToast với handleSaveChapter — bắt buộc render để toast lỗi (vd. chương trước chưa xuất bản) hiển thị khi đang sửa chương */}
                <ToastContainer />
                <ChapterEditorPage
                    story={currentStory}
                    chapter={(activeView === 'editChapter' || activeView === 'addChapter') ? currentChapter : null}
                    isCreateMode={activeView === 'addChapter'}
                    sourceChapterForVersion={activeView === 'addChapterVersion' ? sourceChapterForVersion : null}
                    editingVersion={activeView === 'addChapterVersion' ? editingVersion : null}
                    readOnly={viewChapterOnly}
                    onSave={handleSaveChapter}
                    onNavigateAfterSave={navigateAwayFromChapterEditor}
                    onCancel={() => {
                        setActiveView('chapterList');
                        setCurrentChapter(null);
                        setSourceChapterForVersion(null);
                        setEditingVersion(null);
                        setViewChapterOnly(false);
                    }}
                />
            </>
        );
    }

    if (activeView === 'comments') {
        return (
            <StoryCommentsViewer
                story={currentStory}
                comments={mockComments}
                onBack={() => {
                    setActiveView('stories');
                    setCurrentStory(null);
                }}
            />
        );
    }

    const filteredAuthorReports = (Array.isArray(authorReportNotifications) ? authorReportNotifications : [])
        .filter((item) => !reportStoryFilterId || String(item.storyIdFromLink || '').toLowerCase() === reportStoryFilterId.toLowerCase());

    return (
        <div>
            <Header />
            <div style={{ minHeight: '100vh', backgroundColor: '#f5f5f5', display: 'flex' }}>
                {/* Sidebar */}
                <div style={{
                    width: '280px',
                    backgroundColor: '#ffffff',
                    borderRight: '1px solid #e0e0e0',
                    display: 'flex',
                    flexDirection: 'column',
                    height: '100vh',
                    position: 'sticky',
                    top: 0
                }}>
                    {/* User Profile Section */}
                    <div style={{
                        padding: '2rem 1.5rem',
                        borderBottom: '1px solid #e0e0e0',
                        backgroundColor: '#f9fafb'
                    }}>
                        <div style={{
                            display: 'flex',
                            alignItems: 'center',
                            gap: '1rem',
                            marginBottom: '1rem'
                        }}>
                            <div style={{
                                width: '56px',
                                height: '56px',
                                borderRadius: '50%',
                                backgroundColor: '#13ec5b',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                color: '#ffffff',
                                fontSize: '1.5rem',
                                fontWeight: 'bold',
                                flexShrink: 0
                            }}>
                                {userDisplayName.charAt(0).toUpperCase()}
                            </div>
                            <div style={{ flex: 1, minWidth: 0 }}>
                                <h2 style={{
                                    fontSize: '1rem',
                                    fontWeight: 'bold',
                                    color: '#333333',
                                    margin: '0 0 0.25rem 0',
                                    overflow: 'hidden',
                                    textOverflow: 'ellipsis',
                                    whiteSpace: 'nowrap'
                                }}>
                                    {userDisplayName}
                                </h2>
                                <p style={{
                                    fontSize: '0.75rem',
                                    color: '#6b7280',
                                    margin: 0,
                                    overflow: 'hidden',
                                    textOverflow: 'ellipsis',
                                    whiteSpace: 'nowrap'
                                }}>
                                    Tác giả
                                </p>
                            </div>
                        </div>
                    </div>

                    {/* Navigation Menu */}
                    <nav style={{
                        flex: 1,
                        padding: '1rem 0',
                        display: 'flex',
                        flexDirection: 'column',
                        gap: '0.25rem'
                    }}>
                        <button
                            onClick={() => {
                                setActiveMenu('profile');
                                setActiveView('profile');
                            }}
                            style={{
                                width: '100%',
                                padding: '0.875rem 1.5rem',
                                backgroundColor: activeMenu === 'profile' ? '#f0fdf4' : 'transparent',
                                border: 'none',
                                borderLeft: activeMenu === 'profile' ? '3px solid #13ec5b' : '3px solid transparent',
                                borderRadius: '9999px',
                                marginLeft: '0.5rem',
                                marginRight: '0.5rem',
                                textAlign: 'left',
                                fontSize: '0.875rem',
                                fontWeight: activeMenu === 'profile' ? 600 : 500,
                                color: activeMenu === 'profile' ? '#13ec5b' : '#333333',
                                cursor: 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.75rem',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                if (activeMenu !== 'profile') {
                                    e.currentTarget.style.backgroundColor = '#f9fafb';
                                }
                            }}
                            onMouseLeave={(e) => {
                                if (activeMenu !== 'profile') {
                                    e.currentTarget.style.backgroundColor = 'transparent';
                                }
                            }}
                        >
                            <User style={{ width: '20px', height: '20px' }} />
                            Hồ sơ tác giả
                        </button>

                        <button
                            onClick={() => {
                                setActiveMenu('stories');
                                setActiveView('stories');
                            }}
                            style={{
                                width: '100%',
                                padding: '0.875rem 1.5rem',
                                backgroundColor: activeMenu === 'stories' ? '#f0fdf4' : 'transparent',
                                border: 'none',
                                borderLeft: activeMenu === 'stories' ? '3px solid #13ec5b' : '3px solid transparent',
                                borderRadius: '9999px',
                                marginLeft: '0.5rem',
                                marginRight: '0.5rem',
                                textAlign: 'left',
                                fontSize: '0.875rem',
                                fontWeight: activeMenu === 'stories' ? 600 : 500,
                                color: activeMenu === 'stories' ? '#13ec5b' : '#333333',
                                cursor: 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.75rem',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                if (activeMenu !== 'stories') {
                                    e.currentTarget.style.backgroundColor = '#f9fafb';
                                }
                            }}
                            onMouseLeave={(e) => {
                                if (activeMenu !== 'stories') {
                                    e.currentTarget.style.backgroundColor = 'transparent';
                                }
                            }}
                        >
                            <Book style={{ width: '20px', height: '20px' }} />
                            Truyện của tôi
                        </button>

                        <button
                            onClick={() => {
                                setActiveMenu('reports');
                                setActiveView('reports');
                            }}
                            style={{
                                width: '100%',
                                padding: '0.875rem 1.5rem',
                                backgroundColor: activeMenu === 'reports' ? '#f0fdf4' : 'transparent',
                                border: 'none',
                                borderLeft: activeMenu === 'reports' ? '3px solid #13ec5b' : '3px solid transparent',
                                borderRadius: '9999px',
                                marginLeft: '0.5rem',
                                marginRight: '0.5rem',
                                textAlign: 'left',
                                fontSize: '0.875rem',
                                fontWeight: activeMenu === 'reports' ? 600 : 500,
                                color: activeMenu === 'reports' ? '#13ec5b' : '#333333',
                                cursor: 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.75rem',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                if (activeMenu !== 'reports') e.currentTarget.style.backgroundColor = '#f9fafb';
                            }}
                            onMouseLeave={(e) => {
                                if (activeMenu !== 'reports') e.currentTarget.style.backgroundColor = 'transparent';
                            }}
                        >
                            <List style={{ width: '20px', height: '20px' }} />
                            Chi tiết báo cáo
                        </button>

                        <button
                            onClick={() => {
                                setActiveMenu('withdraw');
                                setActiveView('withdraw');
                            }}
                            style={{
                                width: '100%',
                                padding: '0.875rem 1.5rem',
                                backgroundColor: activeMenu === 'withdraw' ? '#f0fdf4' : 'transparent',
                                border: 'none',
                                borderLeft: activeMenu === 'withdraw' ? '3px solid #13ec5b' : '3px solid transparent',
                                borderRadius: '9999px',
                                marginLeft: '0.5rem',
                                marginRight: '0.5rem',
                                textAlign: 'left',
                                fontSize: '0.875rem',
                                fontWeight: activeMenu === 'withdraw' ? 600 : 500,
                                color: activeMenu === 'withdraw' ? '#13ec5b' : '#333333',
                                cursor: 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.75rem',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                if (activeMenu !== 'withdraw') e.currentTarget.style.backgroundColor = '#f9fafb';
                            }}
                            onMouseLeave={(e) => {
                                if (activeMenu !== 'withdraw') e.currentTarget.style.backgroundColor = 'transparent';
                            }}
                        >
                            <Wallet style={{ width: '20px', height: '20px' }} />
                            Rút tiền
                        </button>
                    </nav>

                    {/* Logout Section */}
                    <div style={{
                        padding: '1rem 1.5rem',
                        borderTop: '1px solid #e0e0e0',
                        backgroundColor: '#f9fafb'
                    }}>
                        <button
                            onClick={async () => {
                                try {
                                    await logout();
                                    onBack?.();
                                    navigate('/', { replace: true });
                                } catch (error) {
                                    console.error('Logout error:', error);
                                    onBack?.();
                                    navigate('/', { replace: true });
                                }
                            }}
                            style={{
                                width: '100%',
                                padding: '0.875rem 1.5rem',
                                backgroundColor: 'transparent',
                                border: '2px solid #ef4444',
                                borderRadius: '9999px',
                                textAlign: 'center',
                                fontSize: '0.875rem',
                                fontWeight: 600,
                                color: '#ef4444',
                                cursor: 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'center',
                                gap: '0.75rem',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                e.currentTarget.style.backgroundColor = '#fee2e2';
                                e.currentTarget.style.borderColor = '#dc2626';
                            }}
                            onMouseLeave={(e) => {
                                e.currentTarget.style.backgroundColor = 'transparent';
                                e.currentTarget.style.borderColor = '#ef4444';
                            }}
                        >
                            <LogOut style={{ width: '18px', height: '18px' }} />
                            Đăng xuất
                        </button>
                    </div>
                </div>

                {/* Main Content */}
                <div style={{ flex: 1, padding: '2rem' }}>
                    {activeView === 'withdraw' ? (
                        <div style={{ maxWidth: '720px' }}>
                            <div style={{
                                display: 'flex',
                                alignItems: 'center',
                                gap: '1rem',
                                marginBottom: '1.75rem',
                                padding: '1.5rem 1.75rem',
                                backgroundColor: '#ffffff',
                                borderRadius: '16px',
                                border: '1px solid #e5e7eb',
                                boxShadow: '0 1px 3px rgba(0,0,0,0.06)'
                            }}>
                                <div style={{
                                    width: '52px', height: '52px', borderRadius: '14px',
                                    background: 'linear-gradient(135deg, #13ec5b 0%, #10d452 100%)',
                                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                                    boxShadow: '0 4px 14px rgba(19, 236, 91, 0.3)'
                                }}>
                                    <Wallet style={{ width: '28px', height: '28px', color: '#ffffff' }} />
                                </div>
                                <div>
                                    <h2 style={{ fontFamily: "'Plus Jakarta Sans', sans-serif", fontSize: '1.5rem', fontWeight: 700, color: '#1A2332', margin: 0, letterSpacing: '-0.02em' }}>Rút tiền</h2>
                                    <p style={{ fontSize: '0.875rem', color: '#90A1B9', margin: '6px 0 0 0' }}>Rút số dư từ donate về tài khoản của bạn</p>
                                </div>
                            </div>

                            <div style={{
                                backgroundColor: '#ffffff',
                                borderRadius: '16px',
                                padding: '1.75rem',
                                border: '1px solid #e5e7eb',
                                boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
                                marginBottom: '1.5rem'
                            }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1.25rem' }}>
                                    <Coins style={{ width: '20px', height: '20px', color: '#13ec5b' }} />
                                    <span style={{ fontSize: '0.875rem', fontWeight: 600, color: '#374151' }}>Số dư khả dụng</span>
                                </div>
                                <div style={{
                                    display: 'inline-flex',
                                    alignItems: 'baseline',
                                    gap: '0.5rem',
                                    padding: '1rem 1.25rem',
                                    backgroundColor: '#f0fdf4',
                                    borderRadius: '12px',
                                    border: '1px solid #bbf7d0'
                                }}>
                                    <span style={{ fontSize: '1.75rem', fontWeight: 700, color: '#15803d', letterSpacing: '-0.02em' }}>{withdrawBalance != null ? Number(withdrawBalance).toLocaleString() : '—'}</span>
                                    <span style={{ fontSize: '0.875rem', color: '#166534', fontWeight: 500 }}>coin</span>
                                </div>
                                <div style={{ fontSize: '0.8125rem', color: '#166534', marginTop: '0.5rem' }}>
                                    ≈ {withdrawBalance != null ? formatVnd(Number(withdrawBalance) * COIN_RATE_VND) : '—'}
                                </div>
                            </div>

                            <p style={{ margin: '0 0 1rem 0', fontSize: '0.8125rem', color: '#92400e' }}>
                                Lưu ý: Rút tiền không tính thêm phí nền tảng, bạn nhận 100% từ số dư thu nhập khả dụng.
                            </p>

                            <div style={{
                                backgroundColor: '#ffffff',
                                borderRadius: '16px',
                                padding: '1.75rem',
                                border: '1px solid #e5e7eb',
                                boxShadow: '0 1px 3px rgba(0,0,0,0.05)'
                            }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1.25rem' }}>
                                    <ArrowDownToLine style={{ width: '20px', height: '20px', color: '#6b7280' }} />
                                    <span style={{ fontSize: '0.875rem', fontWeight: 600, color: '#374151' }}>Yêu cầu rút tiền</span>
                                </div>
                                {withdrawError && (
                                    <p style={{ fontSize: '0.875rem', color: '#dc2626', marginBottom: '0.75rem' }}>{withdrawError}</p>
                                )}
                                <div style={{ marginBottom: '1.25rem' }}>
                                    <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 500, color: '#4b5563', marginBottom: '0.5rem' }}>
                                        Tài khoản ngân hàng
                                    </label>
                                    <select
                                        value={selectedBankAccountIdx >= 0 ? String(selectedBankAccountIdx) : ''}
                                        onChange={(e) => setSelectedBankAccountIdx(e.target.value === '' ? -1 : Number(e.target.value))}
                                        style={{
                                            width: '100%',
                                            maxWidth: '520px',
                                            padding: '0.75rem 1rem',
                                            border: '1px solid #e5e7eb',
                                            borderRadius: '10px',
                                            fontSize: '0.9375rem',
                                            outline: 'none',
                                            backgroundColor: '#ffffff'
                                        }}
                                        onFocus={(e) => { e.currentTarget.style.borderColor = '#13ec5b'; e.currentTarget.style.boxShadow = '0 0 0 3px rgba(19, 236, 91, 0.2)'; }}
                                        onBlur={(e) => { e.currentTarget.style.borderColor = '#e5e7eb'; e.currentTarget.style.boxShadow = 'none'; }}
                                    >
                                        <option value="">Chọn tài khoản ngân hàng</option>
                                        {withdrawBankAccounts.map((acc, idx) => (
                                            <option key={`${acc.bank_name}-${acc.account_number}-${idx}`} value={String(idx)}>
                                                {acc.bank_name} • {maskAccountNumber(acc.account_number)} • {acc.account_holder_name}
                                            </option>
                                        ))}
                                    </select>

                                    {withdrawBankAccounts.length === 0 ? (
                                        <p style={{ fontSize: '0.8125rem', color: '#b45309', margin: '0.5rem 0 0 0' }}>
                                            Bạn chưa có tài khoản ngân hàng. Mở <b>Hồ sơ tác giả</b> → <b>Tài khoản ngân hàng</b> để thêm thông tin trước khi rút.
                                        </p>
                                    ) : !selectedBankAccount ? (
                                        <p style={{ fontSize: '0.8125rem', color: '#64748b', margin: '0.5rem 0 0 0' }}>
                                            Chọn 1 tài khoản để gửi kèm theo yêu cầu rút tiền.
                                        </p>
                                    ) : (
                                        <div style={{
                                            marginTop: '0.75rem',
                                            padding: '0.75rem 1rem',
                                            backgroundColor: '#f8fafc',
                                            border: '1px solid #e5e7eb',
                                            borderRadius: '12px',
                                            maxWidth: '640px'
                                        }}>
                                            <div style={{ fontSize: '0.875rem', fontWeight: 600, color: '#111827' }}>
                                                {selectedBankAccount.bank_name} — {selectedBankAccount.branch_name || '—'}
                                            </div>
                                            <div style={{ fontSize: '0.8125rem', color: '#64748b', marginTop: '0.25rem' }}>
                                                {selectedBankAccount.account_holder_name} • {selectedBankAccount.account_number}
                                            </div>
                                        </div>
                                    )}
                                </div>
                                <div style={{ marginBottom: '1.25rem' }}>
                                    <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 500, color: '#4b5563', marginBottom: '0.5rem' }}>Số coin muốn rút</label>
                                    <input
                                        type="number"
                                        placeholder="0"
                                        min={withdrawInputUseNativeMinMax ? MIN_WITHDRAW_COINS : undefined}
                                        max={withdrawInputUseNativeMinMax ? withdrawBalanceNum : undefined}
                                        step={1}
                                        value={withdrawAmount}
                                        onChange={(e) => setWithdrawAmount(e.target.value.replace(/[^0-9]/g, '') || '')}
                                        style={{
                                            width: '100%',
                                            maxWidth: '320px',
                                            padding: '0.75rem 1rem',
                                            border: '1px solid #e5e7eb',
                                            borderRadius: '10px',
                                            fontSize: '0.9375rem',
                                            outline: 'none'
                                        }}
                                        onFocus={(e) => { e.currentTarget.style.borderColor = '#13ec5b'; e.currentTarget.style.boxShadow = '0 0 0 3px rgba(19, 236, 91, 0.2)'; }}
                                        onBlur={(e) => { e.currentTarget.style.borderColor = '#e5e7eb'; e.currentTarget.style.boxShadow = 'none'; }}
                                    />
                                    <p style={{ fontSize: '0.8125rem', color: '#64748b', margin: '0.5rem 0 0 0', maxWidth: '520px', lineHeight: 1.45 }}>
                                        Rút tối thiểu <strong>{MIN_WITHDRAW_COINS.toLocaleString('vi-VN')} coin</strong> (khoảng{' '}
                                        {formatVnd(MIN_WITHDRAW_VND)}).
                                        {withdrawBalanceNum != null &&
                                            Number.isFinite(withdrawBalanceNum) &&
                                            withdrawBalanceNum > 0 &&
                                            withdrawBalanceNum < MIN_WITHDRAW_COINS ? (
                                            <span style={{ display: 'block', color: '#b45309', marginTop: '0.35rem', fontWeight: 500 }}>
                                                Số dư hiện tại ({withdrawBalanceNum.toLocaleString('vi-VN')} coin) chưa đủ mức rút tối
                                                thiểu.
                                            </span>
                                        ) : null}
                                    </p>
                                    {!selectedBankAccount && (
                                        <p style={{ fontSize: '0.8125rem', color: '#b45309', margin: '0.5rem 0 0 0' }}>
                                            Vui lòng chọn tài khoản ngân hàng để rút tiền.
                                        </p>
                                    )}
                                </div>
                                <button
                                    type="button"
                                    disabled={
                                        withdrawSubmitting ||
                                        withdrawBalance == null ||
                                        !withdrawAmount ||
                                        !selectedBankAccount ||
                                        !selectedBankAccount?.bank_bin ||
                                        (withdrawBalance != null && Number(withdrawAmount) > withdrawBalance) ||
                                        Number(withdrawAmount) < MIN_WITHDRAW_COINS
                                    }
                                    style={{
                                        padding: '0.625rem 1.5rem',
                                        backgroundColor: '#13ec5b',
                                        border: 'none',
                                        borderRadius: '10px',
                                        fontSize: '0.875rem',
                                        fontWeight: 600,
                                        color: '#ffffff',
                                        cursor: withdrawSubmitting ? 'not-allowed' : 'pointer',
                                        opacity: (withdrawSubmitting || !withdrawAmount || withdrawBalance == null || (withdrawBalance != null && Number(withdrawAmount) > withdrawBalance) || Number(withdrawAmount) < MIN_WITHDRAW_COINS) ? 0.6 : 1,
                                        boxShadow: '0 2px 8px rgba(19, 236, 91, 0.35)',
                                        transition: 'all 0.2s ease'
                                    }}
                                    onMouseEnter={(e) => { if (!e.currentTarget.disabled) { e.currentTarget.style.backgroundColor = '#10d452'; e.currentTarget.style.transform = 'translateY(-1px)'; e.currentTarget.style.boxShadow = '0 4px 12px rgba(19, 236, 91, 0.4)'; } }}
                                    onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = '#13ec5b'; e.currentTarget.style.transform = 'translateY(0)'; e.currentTarget.style.boxShadow = '0 2px 8px rgba(19, 236, 91, 0.35)'; }}
                                    onClick={async () => {
                                        const amount = Number(withdrawAmount);
                                        if (!amount || amount < MIN_WITHDRAW_COINS) {
                                            setWithdrawError(
                                                `Số coin rút tối thiểu là ${MIN_WITHDRAW_COINS.toLocaleString('vi-VN')} coin (tương đương khoảng ${formatVnd(MIN_WITHDRAW_VND)}).`
                                            );
                                            return;
                                        }
                                        if (!selectedBankAccount?.bank_bin && !BANK_BIN_MAP[selectedBankAccount?.bank_name || '']) {
                                            setWithdrawError('Ngân hàng đã chọn chưa có BIN mapping. Vui lòng chọn ngân hàng khác.');
                                            return;
                                        }
                                        const bankInfo = buildBankInfoStringFromAccount(selectedBankAccount);
                                        if (!bankInfo) {
                                            setWithdrawError('Vui lòng chọn tài khoản ngân hàng hợp lệ.');
                                            return;
                                        }
                                        setWithdrawSubmitting(true);
                                        setWithdrawError(null);
                                        const res = await coinApi.createWithdrawRequest({ amountCoins: amount, bankInfo });
                                        setWithdrawSubmitting(false);
                                        if (res?.success) {
                                            setWithdrawAmount('');
                                            showToast('Đã gửi yêu cầu rút tiền. Quản trị viên sẽ xử lý.', 'success');
                                            coinApi.getMyWallet().then((r) => {
                                                if (r?.success && r?.data) {
                                                    setWithdrawBalance(r.data.incomeBalance ?? r.data.income_balance ?? 0);
                                                }
                                            });
                                            coinApi.getAuthorActivity({ page: 1, pageSize: 100 }).then((ar) => { if (ar?.success && ar?.data?.items) setAuthorActivityItems(ar.data.items); });
                                            // Để Header/Wallet trang cập nhật tổng coin ngay khi income_balance chuyển sang frozen_balance
                                            window.dispatchEvent(new Event('wallet:changed'));
                                        } else {
                                            setWithdrawError(res?.message ?? 'Không gửi được yêu cầu rút tiền.');
                                        }
                                    }}
                                >
                                    {withdrawSubmitting ? 'Đang gửi...' : 'Gửi yêu cầu rút tiền'}
                                </button>
                                <p style={{ marginTop: '0.75rem', fontSize: '0.8125rem', color: '#64748b' }}>
                                    Lưu ý: Sau khi yêu cầu được duyệt, tiền sẽ được chuyển về tài khoản ngân hàng trong khoảng 3-5 ngày làm việc.
                                </p>
                            </div>
                        </div>
                    ) : activeView === 'reports' ? (
                        <div style={{ maxWidth: '980px' }}>
                            <div style={{
                                display: 'flex',
                                alignItems: 'center',
                                justifyContent: 'space-between',
                                gap: '1rem',
                                marginBottom: '1.25rem',
                                flexWrap: 'wrap'
                            }}>
                                <div>
                                    <h2 style={{ margin: 0, fontSize: '1.4rem', color: '#1f2937' }}>Chi tiết báo cáo</h2>
                                    <p style={{ margin: '0.35rem 0 0 0', color: '#6b7280', fontSize: '0.9rem' }}>
                                        Danh sách thông báo báo cáo liên quan truyện của bạn
                                    </p>
                                </div>
                                <div style={{ display: 'flex', gap: '0.5rem' }}>
                                    {reportStoryFilterId && (
                                        <button
                                            type="button"
                                            onClick={() => setReportStoryFilterId('')}
                                            style={{ border: '1px solid #e5e7eb', background: '#fff', borderRadius: '9999px', padding: '0.45rem 0.9rem', cursor: 'pointer', fontSize: '0.82rem' }}
                                        >
                                            Bỏ lọc truyện
                                        </button>
                                    )}
                                    <button
                                        type="button"
                                        onClick={loadAuthorReportNotifications}
                                        style={{ border: 'none', background: '#13ec5b', color: '#fff', borderRadius: '9999px', padding: '0.45rem 1rem', cursor: 'pointer', fontSize: '0.82rem', fontWeight: 600 }}
                                    >
                                        Làm mới
                                    </button>
                                </div>
                            </div>

                            {authorReportError && (
                                <div style={{ marginBottom: '1rem', padding: '0.75rem 1rem', borderRadius: '10px', border: '1px solid #fecaca', background: '#fef2f2', color: '#b91c1c', fontSize: '0.9rem' }}>
                                    {authorReportError}
                                </div>
                            )}

                            <div style={{ background: '#fff', border: '1px solid #e5e7eb', borderRadius: '14px', overflow: 'hidden' }}>
                                {authorReportLoading ? (
                                    <div style={{ padding: '1rem', color: '#64748b' }}>Đang tải danh sách báo cáo...</div>
                                ) : filteredAuthorReports.length === 0 ? (
                                    <div style={{ padding: '1rem', color: '#64748b' }}>
                                        {reportStoryFilterId ? 'Không có báo cáo cho truyện này.' : 'Chưa có thông báo báo cáo nào.'}
                                    </div>
                                ) : (
                                    filteredAuthorReports.map((item) => (
                                        <div key={item.id} style={{ padding: '0.95rem 1rem', borderBottom: '1px solid #f1f5f9' }}>
                                            <div style={{ display: 'flex', justifyContent: 'space-between', gap: '0.75rem', alignItems: 'flex-start' }}>
                                                <div>
                                                    <div style={{ fontWeight: 700, color: '#111827', marginBottom: '0.3rem' }}>{item.title || 'Thông báo báo cáo'}</div>
                                                    <div style={{ color: '#475569', fontSize: '0.9rem', whiteSpace: 'pre-wrap', lineHeight: 1.45 }}>
                                                        {item.content || 'Không có nội dung chi tiết.'}
                                                    </div>
                                                </div>
                                                <div style={{ fontSize: '0.78rem', color: '#94a3b8', whiteSpace: 'nowrap' }}>
                                                    {formatTime(item.createdAt)}
                                                </div>
                                            </div>
                                            <div style={{ marginTop: '0.6rem', display: 'flex', gap: '0.5rem', flexWrap: 'wrap' }}>
                                                {item.storyIdFromLink && (
                                                    <span style={{ fontSize: '0.75rem', color: '#0f766e', background: '#f0fdfa', border: '1px solid #99f6e4', borderRadius: '9999px', padding: '0.15rem 0.6rem' }}>
                                                        StoryId: {item.storyIdFromLink}
                                                    </span>
                                                )}
                                                <button
                                                    type="button"
                                                    onClick={() => navigate(item.linkUrl || '/home')}
                                                    style={{ fontSize: '0.78rem', border: '1px solid #d1d5db', background: '#fff', borderRadius: '9999px', padding: '0.2rem 0.7rem', cursor: 'pointer' }}
                                                >
                                                    Mở truyện liên quan
                                                </button>
                                            </div>
                                        </div>
                                    ))
                                )}
                            </div>
                        </div>
                    ) : activeView === 'profile' ? (
                        <div style={{ maxWidth: '900px' }}>
                            {/* Thành tích + liên kết nhanh */}
                            <div style={{
                                backgroundColor: '#ffffff',
                                borderRadius: '16px',
                                padding: '1.5rem',
                                marginBottom: '1.5rem',
                                border: '1px solid #e5e7eb',
                                boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
                            }}
                            >
                                <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: '1rem', flexWrap: 'wrap', marginBottom: '1.25rem' }}>
                                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                        <div style={{ width: '20px', height: '20px', color: '#6b7280' }}>🌱</div>
                                        <h3 style={{ fontSize: '1.0625rem', fontWeight: 700, color: '#1e293b', margin: 0 }}>Thành tích</h3>
                                    </div>
                                </div>

                                <div style={{
                                    display: 'grid',
                                    gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))',
                                    gap: '0.75rem',
                                    marginBottom: '1.25rem',
                                }}
                                >
                                    {[
                                        { icon: Book, color: '#059669', bg: '#ecfdf5', label: 'Truyện đã đăng', value: userStats.published },
                                        { icon: List, color: '#7c3aed', bg: '#f5f3ff', label: 'Chương đã đăng', value: userStats.totalChapters },
                                        { icon: Eye, color: '#0ea5e9', bg: '#f0f9ff', label: 'Lượt xem (tổng)', value: userStats.totalViews.toLocaleString('vi-VN') },
                                        { icon: Heart, color: '#e11d48', bg: '#fff1f2', label: 'Người theo dõi', value: userStats.followers },
                                        { icon: Coins, color: '#b45309', bg: '#fffbeb', label: 'Hạn mức token AI', value: authorAiLimitText },
                                    ].map((item) => {
                                        const IconComp = item.icon;
                                        return (
                                            <div
                                                key={item.label}
                                                style={{
                                                    borderRadius: '14px',
                                                    border: '1px solid #e2e8f0',
                                                    padding: '1rem',
                                                    background: 'linear-gradient(180deg, #ffffff 0%, #f8fafc 100%)',
                                                }}
                                            >
                                                <div style={{
                                                    width: '40px',
                                                    height: '40px',
                                                    borderRadius: '12px',
                                                    backgroundColor: item.bg,
                                                    display: 'flex',
                                                    alignItems: 'center',
                                                    justifyContent: 'center',
                                                    marginBottom: '0.65rem',
                                                }}
                                                >
                                                    <IconComp style={{ width: '20px', height: '20px', color: item.color }} />
                                                </div>
                                                <div style={{ fontSize: '1.35rem', fontWeight: 800, color: '#0f172a', letterSpacing: '-0.02em', lineHeight: 1.1 }}>
                                                    {item.value}
                                                </div>
                                                <div style={{ fontSize: '0.75rem', color: '#64748b', marginTop: '0.35rem', fontWeight: 500 }}>
                                                    {item.label}
                                                </div>
                                                {item.hint && (
                                                    <div style={{ fontSize: '0.6875rem', color: '#94a3b8', marginTop: '0.25rem' }}>
                                                        {item.hint}
                                                    </div>
                                                )}
                                            </div>
                                    )})}
                                </div>
                                {authorAiBudgetError && (
                                    <p style={{ margin: 0, marginTop: '-0.5rem', marginBottom: '1rem', fontSize: '0.75rem', color: '#dc2626' }}>
                                        {authorAiBudgetError}
                                    </p>
                                )}

                                <div style={{
                                    display: 'flex',
                                    flexWrap: 'wrap',
                                    gap: '0.75rem',
                                    paddingTop: '1rem',
                                    borderTop: '1px solid #f1f5f9',
                                }}
                                >
                                    <button
                                        type="button"
                                        onClick={() => {
                                            setBankFieldErrors({});
                                            setShowBankModal(true);
                                        }}
                                        style={{
                                            display: 'inline-flex',
                                            alignItems: 'center',
                                            gap: '0.5rem',
                                            padding: '0.65rem 1.1rem',
                                            borderRadius: '9999px',
                                            border: '1px solid #bbf7d0',
                                            backgroundColor: '#f0fdf4',
                                            color: '#166534',
                                            fontSize: '0.875rem',
                                            fontWeight: 600,
                                            cursor: 'pointer',
                                        }}
                                    >
                                        <Landmark style={{ width: '18px', height: '18px' }} />
                                        Tài khoản ngân hàng
                                    </button>
                                    <button
                                        type="button"
                                        onClick={() => {
                                            setFollowersPage(1);
                                            setFollowersSearchInput('');
                                            setFollowersSearchKeyword('');
                                            setShowFollowersModal(true);
                                        }}
                                        style={{
                                            display: 'inline-flex',
                                            alignItems: 'center',
                                            gap: '0.5rem',
                                            padding: '0.65rem 1.1rem',
                                            borderRadius: '9999px',
                                            border: '1px solid #fecdd3',
                                            backgroundColor: '#fff1f2',
                                            color: '#be123c',
                                            fontSize: '0.875rem',
                                            fontWeight: 600,
                                            cursor: 'pointer',
                                        }}
                                    >
                                        <Heart style={{ width: '18px', height: '18px' }} />
                                        Người theo dõi
                                    </button>
                                    <button
                                        type="button"
                                        onClick={() => setShowHistoryModal(true)}
                                        style={{
                                            display: 'inline-flex',
                                            alignItems: 'center',
                                            gap: '0.5rem',
                                            padding: '0.65rem 1.1rem',
                                            borderRadius: '9999px',
                                            border: '1px solid #e2e8f0',
                                            backgroundColor: '#f8fafc',
                                            color: '#334155',
                                            fontSize: '0.875rem',
                                            fontWeight: 600,
                                            cursor: 'pointer',
                                        }}
                                    >
                                        <History style={{ width: '18px', height: '18px' }} />
                                        Lịch sử donate &amp; rút tiền
                                    </button>
                                </div>
                            </div>
                        </div>
                    ) : (
                        <div style={{ maxWidth: '1200px' }}>
                            {/* Header - format đồng bộ với hệ thống */}
                            <div style={{
                                display: 'flex',
                                justifyContent: 'space-between',
                                alignItems: 'center',
                                flexWrap: 'wrap',
                                gap: '1rem',
                                marginBottom: '1.75rem',
                                padding: '1.25rem 1.5rem',
                                backgroundColor: '#ffffff',
                                borderRadius: '16px',
                                border: '1px solid #e5e7eb',
                                boxShadow: '0 1px 3px rgba(0,0,0,0.06)'
                            }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '1rem' }}>
                                    <div style={{
                                        width: '48px',
                                        height: '48px',
                                        borderRadius: '12px',
                                        background: 'linear-gradient(135deg, #13ec5b 0%, #10d452 100%)',
                                        display: 'flex',
                                        alignItems: 'center',
                                        justifyContent: 'center',
                                        boxShadow: '0 4px 12px rgba(19, 236, 91, 0.25)',
                                        flexShrink: 0
                                    }}>
                                        <Book style={{ width: '26px', height: '26px', color: '#ffffff' }} />
                                    </div>
                                    <div>
                                        <h2 style={{
                                            fontFamily: "'Plus Jakarta Sans', sans-serif",
                                            fontSize: '1.5rem',
                                            fontWeight: 700,
                                            color: '#1A2332',
                                            margin: 0,
                                            letterSpacing: '-0.02em',
                                            lineHeight: 1.3
                                        }}>
                                            Truyện của tôi
                                        </h2>
                                        <p style={{
                                            fontFamily: "'Plus Jakarta Sans', sans-serif",
                                            fontSize: '0.875rem',
                                            color: '#90A1B9',
                                            margin: '4px 0 0 0',
                                            fontWeight: 400
                                        }}>
                                            Quản lý và sáng tác truyện của bạn
                                        </p>
                                        {isAuthorWritingSuspended && (
                                            <div
                                                style={{
                                                    marginTop: '8px',
                                                    padding: '7px 10px',
                                                    borderRadius: '8px',
                                                    border: '1px solid #fecaca',
                                                    backgroundColor: '#fff1f2',
                                                    color: '#991b1b',
                                                    fontSize: '0.75rem',
                                                    fontWeight: 600,
                                                }}
                                                title={AUTHOR_WRITING_SUSPENDED_TOOLTIP}
                                            >
                                                {AUTHOR_WRITING_SUSPENDED_BANNER}
                                            </div>
                                        )}
                                    </div>
                                </div>
                                <button
                                    onClick={handleCreateStory}
                                    disabled={isAuthorWritingSuspended}
                                    title={isAuthorWritingSuspended ? AUTHOR_WRITING_SUSPENDED_TOOLTIP : 'Thêm truyện mới'}
                                    style={{
                                        display: 'flex',
                                        alignItems: 'center',
                                        gap: '0.5rem',
                                        padding: '0.75rem 1.5rem',
                                        backgroundColor: '#13ec5b',
                                        border: 'none',
                                        borderRadius: '9999px',
                                        fontSize: '0.875rem',
                                        fontWeight: 700,
                                        color: '#ffffff',
                                        cursor: isAuthorWritingSuspended ? 'not-allowed' : 'pointer',
                                        transition: 'all 0.2s',
                                        boxShadow: '0 2px 8px rgba(19, 236, 91, 0.3)',
                                        fontFamily: "'Plus Jakarta Sans', sans-serif",
                                        opacity: isAuthorWritingSuspended ? 0.65 : 1
                                    }}
                                    onMouseEnter={(e) => {
                                        if (isAuthorWritingSuspended) return;
                                        e.currentTarget.style.backgroundColor = '#10d452';
                                        e.currentTarget.style.transform = 'translateY(-1px)';
                                        e.currentTarget.style.boxShadow = '0 4px 12px rgba(19, 236, 91, 0.35)';
                                    }}
                                    onMouseLeave={(e) => {
                                        if (isAuthorWritingSuspended) return;
                                        e.currentTarget.style.backgroundColor = '#13ec5b';
                                        e.currentTarget.style.transform = 'translateY(0)';
                                        e.currentTarget.style.boxShadow = '0 2px 8px rgba(19, 236, 91, 0.3)';
                                    }}
                                >
                                    <Plus style={{ width: '18px', height: '18px' }} />
                                    Thêm truyện mới
                                </button>
                            </div>

                            {/* Stories List */}
                            {storiesLoading ? (
                                <div style={{
                                    backgroundColor: '#ffffff',
                                    borderRadius: '8px',
                                    padding: '3rem',
                                    textAlign: 'center',
                                    border: '1px solid #e0e0e0'
                                }}>
                                    <p style={{ fontSize: '0.875rem', color: '#6b7280' }}>Đang tải danh sách truyện...</p>
                                </div>
                            ) : storiesError ? (
                                <div style={{
                                    backgroundColor: '#ffffff',
                                    borderRadius: '8px',
                                    padding: '3rem',
                                    textAlign: 'center',
                                    border: '1px solid #e0e0e0'
                                }}>
                                    <p style={{ fontSize: '0.875rem', color: '#dc2626', marginBottom: '1rem' }}>{storiesError}</p>
                                    <button
                                        onClick={() => loadStories(storiesCurrentPage)}
                                        style={{
                                            padding: '0.625rem 1.25rem',
                                            fontSize: '0.875rem',
                                            fontWeight: 600,
                                            borderRadius: '9999px',
                                            border: '1px solid #e0e0e0',
                                            backgroundColor: '#ffffff',
                                            color: '#333333',
                                            cursor: 'pointer'
                                        }}
                                    >
                                        Thử lại
                                    </button>
                                </div>
                            ) : stories.length === 0 ? (
                                <div style={{
                                    backgroundColor: '#ffffff',
                                    borderRadius: '8px',
                                    padding: '3rem',
                                    textAlign: 'center',
                                    border: '1px solid #e0e0e0'
                                }}>
                                    <div style={{ fontSize: '3rem', marginBottom: '1rem' }}>📖</div>
                                    <h3 style={{ fontSize: '1.125rem', color: '#6b7280', marginBottom: '0.5rem' }}>
                                        Chưa có truyện nào
                                    </h3>
                                    <p style={{ fontSize: '0.875rem', color: '#9ca3af', margin: 0 }}>
                                        Bắt đầu sáng tác truyện đầu tiên của bạn — dùng nút &quot;+ Thêm truyện mới&quot; ở góc trên.
                                    </p>
                                </div>
                            ) : (
                                <>
                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                                        {stories.map((story) => (
                                            (() => {
                                                const storyComplianceLocked = Boolean(story?.isComplianceHidden) || String(story?.status ?? '').toLowerCase() === 'hidden';
                                                return (
                                            <div
                                                key={story.id}
                                                style={{
                                                    backgroundColor: '#ffffff',
                                                    borderRadius: '8px',
                                                    padding: '1.25rem',
                                                    border: '1px solid #e0e0e0',
                                                    display: 'flex',
                                                    gap: '1.25rem'
                                                }}
                                            >
                                                {/* Cover */}
                                                <img
                                                    src={story.cover || 'https://via.placeholder.com/80x107?text=No+Cover'}
                                                    alt={story.title}
                                                    style={{
                                                        width: '80px',
                                                        height: '107px',
                                                        objectFit: 'cover',
                                                        borderRadius: '4px',
                                                        flexShrink: 0
                                                    }}
                                                />

                                                {/* Info */}
                                                <div style={{ flex: 1, minWidth: 0 }}>
                                                    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '0.75rem' }}>
                                                        <div style={{ flex: 1, minWidth: 0 }}>
                                                            <h3 style={{
                                                                fontSize: '1rem',
                                                                fontWeight: 'bold',
                                                                color: '#333333',
                                                                margin: '0 0 0.5rem 0',
                                                                overflow: 'hidden',
                                                                textOverflow: 'ellipsis',
                                                                whiteSpace: 'nowrap'
                                                            }}>
                                                                {story.title}
                                                            </h3>
                                                            <div style={{ fontSize: '0.75rem', color: '#9ca3af' }}>
                                                                {story.lastUpdate}
                                                            </div>
                                                            {story.isComplianceHidden ? (
                                                                (() => {
                                                                    const isPermanentlyHidden = String(story.status ?? '').toLowerCase() === 'hidden';
                                                                    return (
                                                                <div
                                                                    style={{
                                                                        marginTop: '0.5rem',
                                                                        padding: '0.375rem 0.625rem',
                                                                        borderRadius: '6px',
                                                                        border: '1px solid #fcd34d',
                                                                        backgroundColor: '#fffbeb',
                                                                        color: '#92400e',
                                                                        fontSize: '0.75rem',
                                                                        fontWeight: 600,
                                                                    }}
                                                                >
                                                                    {isPermanentlyHidden
                                                                        ? 'Truyện này đã bị ẩn tạm thời để phục vụ quá trình điều tra vi phạm.'
                                                                        : 'Truyện này đang bị tạm ẩn để điều tra và xử lý vi phạm.'}
                                                                </div>
                                                                    );
                                                                })()
                                                            ) : null}
                                                        </div>
                                                        <div style={{
                                                            padding: '0.25rem 0.75rem',
                                                            backgroundColor: storyComplianceLocked ? '#fef3c7' : ['published', 'completed'].includes(story.status) ? '#d1fae5' : '#fef3c7',
                                                            borderRadius: '4px',
                                                            fontSize: '0.75rem',
                                                            color: storyComplianceLocked ? '#92400e' : ['published', 'completed'].includes(story.status) ? '#065f46' : '#92400e',
                                                            marginLeft: '1rem',
                                                            flexShrink: 0
                                                        }}>
                                                            {story.publishStatus}
                                                        </div>
                                                    </div>

                                                    {/* Stats */}
                                                    <div style={{
                                                        display: 'grid',
                                                        gridTemplateColumns: 'repeat(4, 1fr)',
                                                        gap: '1rem',
                                                        padding: '0.75rem 0',
                                                        borderTop: '1px solid #f3f4f6',
                                                        borderBottom: '1px solid #f3f4f6',
                                                        marginBottom: '1rem'
                                                    }}>
                                                        <div>
                                                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', marginBottom: '0.25rem' }}>
                                                                <Book style={{ width: '14px', height: '14px', color: '#6b7280' }} />
                                                                <span style={{ fontSize: '0.75rem', color: '#6b7280' }}>Chương</span>
                                                            </div>
                                                            <div style={{ fontSize: '1rem', fontWeight: 'bold', color: '#333333' }}>
                                                                {story.chapters}
                                                            </div>
                                                        </div>

                                                        <div>
                                                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', marginBottom: '0.25rem' }}>
                                                                <Eye style={{ width: '14px', height: '14px', color: '#6b7280' }} />
                                                                <span style={{ fontSize: '0.75rem', color: '#6b7280' }}>Lượt đọc</span>
                                                            </div>
                                                            <div style={{ fontSize: '1rem', fontWeight: 'bold', color: '#333333' }}>
                                                                {story.totalViews.toLocaleString()}
                                                            </div>
                                                        </div>

                                                        <div>
                                                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', marginBottom: '0.25rem' }}>
                                                                <Heart style={{ width: '14px', height: '14px', color: '#6b7280' }} />
                                                                <span style={{ fontSize: '0.75rem', color: '#6b7280' }}>Theo dõi</span>
                                                            </div>
                                                            <div style={{ fontSize: '1rem', fontWeight: 'bold', color: '#333333' }}>
                                                                {story.follows}
                                                            </div>
                                                        </div>

                                                        <div>
                                                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.375rem', marginBottom: '0.25rem' }}>
                                                                <Star style={{ width: '14px', height: '14px', color: '#6b7280' }} />
                                                                <span style={{ fontSize: '0.75rem', color: '#6b7280' }}>Đề cử</span>
                                                            </div>
                                                            <div style={{ fontSize: '1rem', fontWeight: 'bold', color: '#333333' }}>
                                                                {Number(story.rating ?? 0) > 0 ? Number(story.rating).toFixed(1) : 0}
                                                            </div>
                                                        </div>
                                                    </div>

                                                    {/* Status */}
                                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.55rem' }}>
                                                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                                                            <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                                                                Trạng thái xuất bản
                                                            </div>
                                                            <div style={{
                                                                padding: '0.25rem 0.75rem',
                                                                backgroundColor: storyComplianceLocked ? '#fef3c7' : (story.status === 'published' || story.status === 'completed') ? '#d1fae5' : '#fef3c7',
                                                                borderRadius: '9999px',
                                                                fontSize: '0.75rem',
                                                                fontWeight: 600,
                                                                color: storyComplianceLocked ? '#92400e' : (story.status === 'published' || story.status === 'completed') ? '#065f46' : '#92400e'
                                                            }}>
                                                                {story.publishStatus}
                                                            </div>
                                                        </div>
                                                        <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                                                            <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                                                                Trạng thái tiến độ
                                                            </div>
                                                            <div style={{
                                                                padding: '0.25rem 0.75rem',
                                                                borderRadius: '9999px',
                                                                fontSize: '0.75rem',
                                                                fontWeight: 600,
                                                                backgroundColor: story.storyProgressStatus === 'COMPLETED'
                                                                    ? '#dcfce7'
                                                                    : story.storyProgressStatus === 'HIATUS'
                                                                        ? '#fee2e2'
                                                                        : '#dbeafe',
                                                                color: story.storyProgressStatus === 'COMPLETED'
                                                                    ? '#166534'
                                                                    : story.storyProgressStatus === 'HIATUS'
                                                                        ? '#991b1b'
                                                                        : '#1d4ed8'
                                                            }}>
                                                                {story.progressStatusDisplay || 'Đang ra'}
                                                            </div>
                                                        </div>
                                                    </div>
                                                </div>

                                                {/* Action Buttons */}
                                                <div style={{
                                                    display: 'flex',
                                                    flexDirection: 'column',
                                                    gap: '0.5rem',
                                                    flexShrink: 0,
                                                    minWidth: '140px'
                                                }}>
                                                    <button
                                                        onClick={() => handleViewChapters(story)}
                                                        title={storyComplianceLocked ? 'Truyện đã ẩn vĩnh viễn: chỉ xem danh sách chương, không thể thao tác chỉnh sửa/xuất bản.' : 'Danh sách chương'}
                                                        style={{
                                                            display: 'flex',
                                                            alignItems: 'center',
                                                            justifyContent: 'center',
                                                            gap: '0.375rem',
                                                            padding: '0.5rem 1rem',
                                                            backgroundColor: '#f8fafc',
                                                            border: '1px solid #e2e8f0',
                                                            borderRadius: '9999px',
                                                            fontSize: '0.8125rem',
                                                            fontWeight: 500,
                                                            color: '#475569',
                                                            cursor: 'pointer',
                                                            whiteSpace: 'nowrap',
                                                            transition: 'all 0.2s'
                                                        }}
                                                        onMouseEnter={(e) => {
                                                            e.currentTarget.style.backgroundColor = '#f1f5f9';
                                                            e.currentTarget.style.borderColor = '#13ec5b';
                                                            e.currentTarget.style.color = '#13ec5b';
                                                        }}
                                                        onMouseLeave={(e) => {
                                                            e.currentTarget.style.backgroundColor = '#f8fafc';
                                                            e.currentTarget.style.borderColor = '#e2e8f0';
                                                            e.currentTarget.style.color = '#475569';
                                                        }}
                                                    >
                                                        <List style={{ width: '14px', height: '14px' }} />
                                                        Danh sách chương
                                                    </button>
                                                    <button
                                                        onClick={() => handleEditStory(story)}
                                                        disabled={isAuthorWritingSuspended || story.status === 'pending_review' || storyComplianceLocked}
                                                        title={
                                                            isAuthorWritingSuspended
                                                                ? AUTHOR_WRITING_SUSPENDED_TOOLTIP
                                                                : storyComplianceLocked
                                                                    ? 'Truyện đã bị ẩn vĩnh viễn do vi phạm, không thể chỉnh sửa'
                                                                : story.status === 'pending_review'
                                                                    ? 'Truyện đang ở trạng thái chờ duyệt, không thể chỉnh sửa'
                                                                    : 'Chỉnh sửa truyện'
                                                        }
                                                        style={{
                                                            display: 'flex',
                                                            alignItems: 'center',
                                                            justifyContent: 'center',
                                                            gap: '0.375rem',
                                                            padding: '0.5rem 1rem',
                                                            backgroundColor: '#f8fafc',
                                                            border: '1px solid #e2e8f0',
                                                            borderRadius: '9999px',
                                                            fontSize: '0.8125rem',
                                                            fontWeight: 500,
                                                            color: '#475569',
                                                            cursor: (isAuthorWritingSuspended || story.status === 'pending_review' || storyComplianceLocked) ? 'not-allowed' : 'pointer',
                                                            whiteSpace: 'nowrap',
                                                            transition: 'all 0.2s',
                                                            opacity: (isAuthorWritingSuspended || story.status === 'pending_review' || storyComplianceLocked) ? 0.7 : 1
                                                        }}
                                                        onMouseEnter={(e) => {
                                                            if (isAuthorWritingSuspended || story.status === 'pending_review' || storyComplianceLocked) return;
                                                            e.currentTarget.style.backgroundColor = '#f1f5f9';
                                                            e.currentTarget.style.borderColor = '#13ec5b';
                                                            e.currentTarget.style.color = '#13ec5b';
                                                        }}
                                                        onMouseLeave={(e) => {
                                                            if (isAuthorWritingSuspended || story.status === 'pending_review' || storyComplianceLocked) return;
                                                            e.currentTarget.style.backgroundColor = '#f8fafc';
                                                            e.currentTarget.style.borderColor = '#e2e8f0';
                                                            e.currentTarget.style.color = '#475569';
                                                        }}
                                                    >
                                                        <Edit style={{ width: '14px', height: '14px' }} />
                                                        Chỉnh sửa
                                                    </button>
                                                    <button
                                                        onClick={() => !isAuthorWritingSuspended && !storyComplianceLocked && story.status === 'draft' && handleDeleteStory(story.id)}
                                                        disabled={isAuthorWritingSuspended || storyComplianceLocked || story.status !== 'draft'}
                                                        title={isAuthorWritingSuspended ? AUTHOR_WRITING_SUSPENDED_TOOLTIP : storyComplianceLocked ? 'Truyện đã bị ẩn vĩnh viễn do vi phạm, không thể xóa' : story.status === 'draft' ? 'Xóa truyện' : 'Chỉ được xóa truyện khi ở trạng thái Bản nháp'}
                                                        style={{
                                                            display: 'flex',
                                                            alignItems: 'center',
                                                            justifyContent: 'center',
                                                            gap: '0.375rem',
                                                            padding: '0.5rem 1rem',
                                                            backgroundColor: (!isAuthorWritingSuspended && !storyComplianceLocked && story.status === 'draft') ? '#fff' : '#f1f5f9',
                                                            border: `1px solid ${(!isAuthorWritingSuspended && !storyComplianceLocked && story.status === 'draft') ? '#fecaca' : '#e2e8f0'}`,
                                                            borderRadius: '9999px',
                                                            fontSize: '0.8125rem',
                                                            fontWeight: 500,
                                                            color: (!isAuthorWritingSuspended && !storyComplianceLocked && story.status === 'draft') ? '#dc2626' : '#94a3b8',
                                                            cursor: (!isAuthorWritingSuspended && !storyComplianceLocked && story.status === 'draft') ? 'pointer' : 'not-allowed',
                                                            whiteSpace: 'nowrap',
                                                            transition: 'all 0.2s',
                                                            opacity: (!isAuthorWritingSuspended && !storyComplianceLocked && story.status === 'draft') ? 1 : 0.8
                                                        }}
                                                        onMouseEnter={(e) => {
                                                            if (!isAuthorWritingSuspended && !storyComplianceLocked && story.status === 'draft') {
                                                                e.currentTarget.style.backgroundColor = '#fef2f2';
                                                                e.currentTarget.style.borderColor = '#ef4444';
                                                            }
                                                        }}
                                                        onMouseLeave={(e) => {
                                                            e.currentTarget.style.backgroundColor = (!isAuthorWritingSuspended && !storyComplianceLocked && story.status === 'draft') ? '#fff' : '#f1f5f9';
                                                            e.currentTarget.style.borderColor = (!isAuthorWritingSuspended && !storyComplianceLocked && story.status === 'draft') ? '#fecaca' : '#e2e8f0';
                                                        }}
                                                    >
                                                        <Trash2 style={{ width: '14px', height: '14px' }} />
                                                        Xóa
                                                    </button>
                                                </div>
                                            </div>
                                                );
                                            })()
                                        ))}
                                    </div>
                                    {!storiesLoading && !storiesError && storiesTotalPages > 1 && (
                                        <Pagination
                                            currentPage={storiesCurrentPage}
                                            totalPages={storiesTotalPages}
                                            totalItems={storiesTotalCount}
                                            itemsPerPage={STORIES_PAGE_SIZE}
                                            onPageChange={handleStoriesPageChange}
                                            itemLabel="truyện"
                                        />
                                    )}
                                </>
                            )}
                        </div>
                    )}
                </div>
            </div>
            <Footer />

            {/* Popup: tài khoản ngân hàng */}
            {showBankModal && (
                <div
                    role="presentation"
                    style={{
                        position: 'fixed',
                        inset: 0,
                        backgroundColor: 'rgba(15, 23, 42, 0.45)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 10000,
                        padding: '1rem',
                    }}
                    onClick={() => setShowBankModal(false)}
                >
                    <div
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="bank-modal-title"
                        style={{
                            backgroundColor: '#ffffff',
                            borderRadius: '16px',
                            maxWidth: '960px',
                            width: '100%',
                            maxHeight: 'min(90vh, 900px)',
                            overflow: 'auto',
                            boxShadow: '0 25px 50px rgba(0,0,0,0.18)',
                            border: '1px solid #e5e7eb',
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: '1rem', padding: '1.25rem 1.5rem', borderBottom: '1px solid #f1f5f9' }}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', minWidth: 0 }}>
                                <div style={{
                                    width: '44px', height: '44px', borderRadius: '12px',
                                    background: 'linear-gradient(135deg, #13ec5b 0%, #10d452 100%)',
                                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                                    flexShrink: 0,
                                }}
                                >
                                    <Landmark style={{ width: '22px', height: '22px', color: '#ffffff' }} />
                                </div>
                                <div style={{ minWidth: 0 }}>
                                    <h2 id="bank-modal-title" style={{ fontSize: '1.125rem', fontWeight: 700, color: '#0f172a', margin: 0 }}>Tài khoản ngân hàng</h2>
                                    <p style={{ fontSize: '0.8125rem', color: '#64748b', margin: '0.35rem 0 0 0', lineHeight: 1.45 }}>
                                        Bạn tự chịu trách nhiệm về thông tin nhập. Hệ thống không yêu cầu bước xác thực riêng trên giao diện này.
                                    </p>
                                </div>
                            </div>
                            <button
                                type="button"
                                aria-label="Đóng"
                                onClick={() => setShowBankModal(false)}
                                style={{
                                    border: 'none',
                                    background: '#f1f5f9',
                                    borderRadius: '10px',
                                    width: '36px',
                                    height: '36px',
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    cursor: 'pointer',
                                    flexShrink: 0,
                                }}
                            >
                                <X style={{ width: '18px', height: '18px', color: '#475569' }} />
                            </button>
                        </div>

                        <div style={{ padding: '1.25rem 1.5rem 1.5rem' }}>
                            <div style={{
                                backgroundColor: '#f8fafc',
                                borderRadius: '14px',
                                padding: '1.25rem',
                                border: '1px solid #e2e8f0',
                                marginBottom: '1.25rem',
                            }}
                            >
                                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                                    <div style={{ gridColumn: 'span 2' }}>
                                        <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 500, color: '#4b5563', marginBottom: '0.5rem' }}>Ngân hàng</label>
                                        <select
                                            value={bankName}
                                            aria-invalid={!!bankFieldErrors.bankName}
                                            onChange={(e) => {
                                                setBankName(e.target.value);
                                                setBankFieldErrors((p) => {
                                                    if (!p.bankName) return p;
                                                    const n = { ...p };
                                                    delete n.bankName;
                                                    return n;
                                                });
                                            }}
                                            style={{
                                                width: '100%',
                                                padding: '0.75rem 1rem',
                                                border: `1px solid ${bankFieldErrors.bankName ? '#f87171' : '#e5e7eb'}`,
                                                borderRadius: '10px',
                                                fontSize: '0.9375rem',
                                                outline: 'none',
                                                backgroundColor: '#ffffff',
                                            }}
                                        >
                                            <option value="">Chọn ngân hàng</option>
                                            {BANK_OPTIONS.map((b) => (
                                                <option key={b} value={b}>{b}</option>
                                            ))}
                                        </select>
                                        {bankFieldErrors.bankName ? (
                                            <p style={{ fontSize: '0.75rem', color: '#dc2626', margin: '0.35rem 0 0 0', lineHeight: 1.35 }}>{bankFieldErrors.bankName}</p>
                                        ) : null}
                                    </div>
                                    <div>
                                        <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 500, color: '#4b5563', marginBottom: '0.5rem' }}>Số tài khoản</label>
                                        <input
                                            type="text"
                                            inputMode="numeric"
                                            autoComplete="off"
                                            value={accountNumber}
                                            aria-invalid={!!bankFieldErrors.accountNumber}
                                            onChange={(e) => {
                                                setAccountNumber(e.target.value.replace(/[^\d]/g, ''));
                                                setBankFieldErrors((p) => {
                                                    if (!p.accountNumber) return p;
                                                    const n = { ...p };
                                                    delete n.accountNumber;
                                                    return n;
                                                });
                                            }}
                                            placeholder="Nhập số tài khoản"
                                            style={{
                                                width: '100%',
                                                padding: '0.75rem 1rem',
                                                border: `1px solid ${bankFieldErrors.accountNumber ? '#f87171' : '#e5e7eb'}`,
                                                borderRadius: '10px',
                                                fontSize: '0.9375rem',
                                                outline: 'none',
                                            }}
                                        />
                                        {bankFieldErrors.accountNumber ? (
                                            <p style={{ fontSize: '0.75rem', color: '#dc2626', margin: '0.35rem 0 0 0', lineHeight: 1.35 }}>{bankFieldErrors.accountNumber}</p>
                                        ) : (
                                            <p style={{ fontSize: '0.7rem', color: '#94a3b8', margin: '0.35rem 0 0 0' }}>
                                                {BANK_ACCOUNT_NUMBER_MIN}–{BANK_ACCOUNT_NUMBER_MAX} chữ số, không khoảng trắng.
                                            </p>
                                        )}
                                    </div>
                                    <div>
                                        <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 500, color: '#4b5563', marginBottom: '0.5rem' }}>Chủ tài khoản</label>
                                        <input
                                            type="text"
                                            value={accountHolderName}
                                            aria-invalid={!!bankFieldErrors.accountHolderName}
                                            onChange={(e) => {
                                                setAccountHolderName(e.target.value);
                                                setBankFieldErrors((p) => {
                                                    if (!p.accountHolderName) return p;
                                                    const n = { ...p };
                                                    delete n.accountHolderName;
                                                    return n;
                                                });
                                            }}
                                            placeholder="Ví dụ: NGUYỄN VĂN A"
                                            style={{
                                                width: '100%',
                                                padding: '0.75rem 1rem',
                                                border: `1px solid ${bankFieldErrors.accountHolderName ? '#f87171' : '#e5e7eb'}`,
                                                borderRadius: '10px',
                                                fontSize: '0.9375rem',
                                                outline: 'none',
                                            }}
                                        />
                                        {bankFieldErrors.accountHolderName ? (
                                            <p style={{ fontSize: '0.75rem', color: '#dc2626', margin: '0.35rem 0 0 0', lineHeight: 1.35 }}>{bankFieldErrors.accountHolderName}</p>
                                        ) : (
                                            <p style={{ fontSize: '0.7rem', color: '#94a3b8', margin: '0.35rem 0 0 0' }}>
                                                Ghi đúng họ tên trên thẻ; khi lưu sẽ chuẩn hoá IN HOA (tiếng Việt).
                                            </p>
                                        )}
                                    </div>
                                    <div style={{ gridColumn: 'span 2' }}>
                                        <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 500, color: '#4b5563', marginBottom: '0.5rem' }}>Chi nhánh (tuỳ chọn)</label>
                                        <input
                                            type="text"
                                            value={branchName}
                                            aria-invalid={!!bankFieldErrors.branchName}
                                            onChange={(e) => {
                                                setBranchName(e.target.value);
                                                setBankFieldErrors((p) => {
                                                    if (!p.branchName) return p;
                                                    const n = { ...p };
                                                    delete n.branchName;
                                                    return n;
                                                });
                                            }}
                                            placeholder="Ví dụ: CN TP.HCM"
                                            style={{
                                                width: '100%',
                                                padding: '0.75rem 1rem',
                                                border: `1px solid ${bankFieldErrors.branchName ? '#f87171' : '#e5e7eb'}`,
                                                borderRadius: '10px',
                                                fontSize: '0.9375rem',
                                                outline: 'none',
                                            }}
                                        />
                                        {bankFieldErrors.branchName ? (
                                            <p style={{ fontSize: '0.75rem', color: '#dc2626', margin: '0.35rem 0 0 0', lineHeight: 1.35 }}>{bankFieldErrors.branchName}</p>
                                        ) : null}
                                    </div>
                                </div>
                                <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '1rem' }}>
                                    <button
                                        type="button"
                                        onClick={async () => {
                                            const { errors, normalized } = validateAuthorBankAccountInput({
                                                bankName,
                                                accountNumber,
                                                accountHolderName,
                                                branchName,
                                                bankOptions: BANK_OPTIONS,
                                                bankBinMap: BANK_BIN_MAP,
                                            });
                                            if (!normalized) {
                                                setBankFieldErrors(errors);
                                                const firstMsg = Object.values(errors)[0];
                                                if (firstMsg) showToast(firstMsg, 'error');
                                                return;
                                            }
                                            setBankFieldErrors({});
                                            const res = await coinApi.upsertAuthorBankAccount({
                                                bankName: normalized.bankTrim,
                                                bankBin: normalized.bin,
                                                accountNumber: normalized.digits,
                                                accountHolderName: normalized.holderForApi,
                                                branchName: normalized.branchTrim || undefined,
                                                isVerified: true,
                                            });
                                            if (!res?.success) {
                                                showToast(res?.message ?? 'Không thể thêm tài khoản ngân hàng.', 'error');
                                                return;
                                            }
                                            setBankName('');
                                            setAccountNumber('');
                                            setAccountHolderName('');
                                            setBranchName('');
                                            showToast('Đã thêm tài khoản ngân hàng.', 'success');
                                            const r = await coinApi.getAuthorBankAccounts();
                                            if (r?.success) {
                                                const items = r?.data ?? [];
                                                const normalized = Array.isArray(items)
                                                    ? items.map((acc) => ({
                                                        ...acc,
                                                        bank_bin: acc.bank_bin || BANK_BIN_MAP[acc.bank_name] || '',
                                                        account_number: String(acc.account_number || '').replace(/[^\d]/g, ''),
                                                        account_holder_name: acc.account_holder_name ?? '',
                                                        branch_name: acc.branch_name ?? '',
                                                        is_verified: !!acc.is_verified,
                                                    }))
                                                    : [];
                                                setBankAccounts(normalized);
                                                setSelectedBankAccountIdx(normalized.length > 0 ? 0 : -1);
                                            }
                                        }}
                                        style={{
                                            padding: '0.625rem 1.25rem',
                                            backgroundColor: '#13ec5b',
                                            border: 'none',
                                            borderRadius: '10px',
                                            fontSize: '0.875rem',
                                            fontWeight: 600,
                                            color: '#ffffff',
                                            cursor: 'pointer',
                                        }}
                                    >
                                        Thêm tài khoản
                                    </button>
                                </div>
                            </div>

                            <div style={{ borderRadius: '14px', border: '1px solid #e5e7eb', overflow: 'hidden' }}>
                                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
                                    <thead>
                                        <tr style={{ backgroundColor: '#f8fafc' }}>
                                            <th style={{ padding: '0.85rem 1rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>NGÂN HÀNG</th>
                                            <th style={{ padding: '0.85rem 1rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>SỐ TK</th>
                                            <th style={{ padding: '0.85rem 1rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>CHỦ TK</th>
                                            <th style={{ padding: '0.85rem 1rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>CHI NHÁNH</th>
                                            <th style={{ padding: '0.85rem 1rem', textAlign: 'right', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>CẬP NHẬT</th>
                                            <th style={{ padding: '0.85rem 1rem', textAlign: 'right', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>HÀNH ĐỘNG</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {bankAccounts.length === 0 ? (
                                            <tr>
                                                <td colSpan={6} style={{ padding: '2rem', textAlign: 'center', color: '#64748b' }}>Chưa có tài khoản ngân hàng nào.</td>
                                            </tr>
                                        ) : (
                                            bankAccounts.map((acc, idx) => (
                                                <tr key={`${acc.bank_name}-${acc.account_number}-${idx}`} style={{ borderBottom: '1px solid #e5e7eb' }}>
                                                    <td style={{ padding: '0.85rem 1rem', color: '#374151', fontWeight: 600 }}>{acc.bank_name}</td>
                                                    <td style={{ padding: '0.85rem 1rem', color: '#374151' }}>{maskAccountNumber(acc.account_number)}</td>
                                                    <td style={{ padding: '0.85rem 1rem', color: '#374151' }}>{acc.account_holder_name}</td>
                                                    <td style={{ padding: '0.85rem 1rem', color: '#64748b' }}>{acc.branch_name || '—'}</td>
                                                    <td style={{ padding: '0.85rem 1rem', textAlign: 'right', color: '#64748b' }}>{formatTime(acc.updated_at)}</td>
                                                    <td style={{ padding: '0.85rem 1rem', textAlign: 'right' }}>
                                                        <button
                                                            type="button"
                                                            onClick={async () => {
                                                                if (!window.confirm('Xoá tài khoản ngân hàng này?')) return;
                                                                const res = await coinApi.deleteAuthorBankAccount();
                                                                if (!res?.success) {
                                                                    showToast(res?.message ?? 'Không thể xoá tài khoản ngân hàng.', 'error');
                                                                    return;
                                                                }
                                                                showToast('Đã xoá tài khoản ngân hàng.', 'success');
                                                                const r = await coinApi.getAuthorBankAccounts();
                                                                if (r?.success) {
                                                                    const items = r?.data ?? [];
                                                                    const normalized = Array.isArray(items)
                                                                        ? items.map((a) => ({
                                                                            ...a,
                                                                            bank_bin: a.bank_bin || BANK_BIN_MAP[a.bank_name] || '',
                                                                            account_number: String(a.account_number || '').replace(/[^\d]/g, ''),
                                                                            account_holder_name: a.account_holder_name ?? '',
                                                                            branch_name: a.branch_name ?? '',
                                                                            is_verified: !!a.is_verified,
                                                                        }))
                                                                        : [];
                                                                    setBankAccounts(normalized);
                                                                    setSelectedBankAccountIdx(normalized.length > 0 ? 0 : -1);
                                                                }
                                                            }}
                                                            style={{
                                                                padding: '0.45rem 0.75rem',
                                                                borderRadius: '10px',
                                                                border: '1px solid #fecaca',
                                                                backgroundColor: '#fef2f2',
                                                                cursor: 'pointer',
                                                                fontSize: '0.8125rem',
                                                                fontWeight: 700,
                                                                color: '#b91c1c',
                                                            }}
                                                        >
                                                            Xoá
                                                        </button>
                                                    </td>
                                                </tr>
                                            ))
                                        )}
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    </div>
                </div>
            )}

            {/* Popup: danh sách người theo dõi */}
            {showFollowersModal && (
                <div
                    role="presentation"
                    style={{
                        position: 'fixed',
                        inset: 0,
                        backgroundColor: 'rgba(15, 23, 42, 0.45)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 10000,
                        padding: '1rem',
                    }}
                    onClick={() => setShowFollowersModal(false)}
                >
                    <div
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="followers-modal-title"
                        style={{
                            backgroundColor: '#ffffff',
                            borderRadius: '16px',
                            maxWidth: '900px',
                            width: '100%',
                            maxHeight: 'min(90vh, 900px)',
                            overflow: 'auto',
                            boxShadow: '0 25px 50px rgba(0,0,0,0.18)',
                            border: '1px solid #e5e7eb',
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: '1rem', padding: '1.25rem 1.5rem', borderBottom: '1px solid #f1f5f9' }}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', minWidth: 0 }}>
                                <div style={{
                                    width: '44px', height: '44px', borderRadius: '12px',
                                    background: 'linear-gradient(135deg, #f43f5e 0%, #e11d48 100%)',
                                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                                    flexShrink: 0,
                                }}
                                >
                                    <Heart style={{ width: '22px', height: '22px', color: '#ffffff' }} />
                                </div>
                                <div style={{ minWidth: 0 }}>
                                    <h2 id="followers-modal-title" style={{ fontSize: '1.125rem', fontWeight: 700, color: '#0f172a', margin: 0 }}>
                                        Danh sách người theo dõi
                                    </h2>
                                    <p style={{ fontSize: '0.8125rem', color: '#64748b', margin: '0.35rem 0 0 0' }}>
                                        Tổng số người theo dõi: <b>{Number(followersTotalCount || 0).toLocaleString('vi-VN')}</b>
                                    </p>
                                </div>
                            </div>
                            <button
                                type="button"
                                aria-label="Đóng"
                                onClick={() => setShowFollowersModal(false)}
                                style={{
                                    border: 'none',
                                    background: '#f1f5f9',
                                    borderRadius: '10px',
                                    width: '36px',
                                    height: '36px',
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    cursor: 'pointer',
                                    flexShrink: 0,
                                }}
                            >
                                <X style={{ width: '18px', height: '18px', color: '#475569' }} />
                            </button>
                        </div>

                        <div style={{ padding: '1.25rem 1.5rem 1.5rem' }}>
                            <div style={{ display: 'flex', gap: '0.75rem', marginBottom: '1rem' }}>
                                <input
                                    type="text"
                                    value={followersSearchInput}
                                    onChange={(e) => setFollowersSearchInput(e.target.value)}
                                    placeholder="Tìm theo tên hiển thị hoặc email..."
                                    style={{
                                        flex: 1,
                                        padding: '0.7rem 0.95rem',
                                        border: '1px solid #e2e8f0',
                                        borderRadius: '10px',
                                        outline: 'none',
                                        fontSize: '0.875rem',
                                    }}
                                />
                                <button
                                    type="button"
                                    onClick={() => {
                                        setFollowersPage(1);
                                        setFollowersSearchKeyword(followersSearchInput.trim());
                                    }}
                                    style={{
                                        padding: '0.7rem 1rem',
                                        borderRadius: '10px',
                                        border: '1px solid #e2e8f0',
                                        backgroundColor: '#f8fafc',
                                        color: '#334155',
                                        fontWeight: 700,
                                        cursor: 'pointer',
                                    }}
                                >
                                    Tìm kiếm
                                </button>
                            </div>

                            <div style={{ borderRadius: '14px', border: '1px solid #e5e7eb', overflow: 'hidden' }}>
                                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
                                    <thead>
                                        <tr style={{ backgroundColor: '#f8fafc' }}>
                                            <th style={{ padding: '0.85rem 1rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>NGƯỜI DÙNG</th>
                                            <th style={{ padding: '0.85rem 1rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>EMAIL</th>
                                            <th style={{ padding: '0.85rem 1rem', textAlign: 'right', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>THEO DÕI TỪ</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {followersLoading ? (
                                            <tr>
                                                <td colSpan={3} style={{ padding: '2rem', textAlign: 'center', color: '#64748b' }}>Đang tải danh sách follower...</td>
                                            </tr>
                                        ) : followersError ? (
                                            <tr>
                                                <td colSpan={3} style={{ padding: '2rem', textAlign: 'center', color: '#dc2626' }}>{followersError}</td>
                                            </tr>
                                        ) : followersItems.length === 0 ? (
                                            <tr>
                                                <td colSpan={3} style={{ padding: '2rem', textAlign: 'center', color: '#64748b' }}>Chưa có người theo dõi nào.</td>
                                            </tr>
                                        ) : (
                                            followersItems.map((follower) => {
                                                const id = follower.userId ?? follower.UserId;
                                                const displayName = follower.displayName ?? follower.DisplayName ?? 'Người dùng';
                                                const email = follower.email ?? follower.Email ?? '—';
                                                const avatar = follower.avatarUrl ?? follower.AvatarUrl;
                                                const followedAt = follower.followedAt ?? follower.FollowedAt;
                                                const timeLabel = followedAt
                                                    ? new Date(followedAt).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric' })
                                                    : '—';
                                                return (
                                                    <tr key={id ?? email} style={{ borderBottom: '1px solid #e5e7eb' }}>
                                                        <td style={{ padding: '0.85rem 1rem' }}>
                                                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.7rem' }}>
                                                                <img
                                                                    src={avatar ? resolveBackendUrl(avatar) : createInitialAvatarDataUrl(displayName, 96)}
                                                                    alt={displayName}
                                                                    style={{ width: '36px', height: '36px', borderRadius: '9999px', objectFit: 'cover', border: '1px solid #e2e8f0' }}
                                                                />
                                                                <span style={{ color: '#1e293b', fontWeight: 600 }}>{displayName}</span>
                                                            </div>
                                                        </td>
                                                        <td style={{ padding: '0.85rem 1rem', color: '#334155' }}>{email}</td>
                                                        <td style={{ padding: '0.85rem 1rem', textAlign: 'right', color: '#64748b' }}>{timeLabel}</td>
                                                    </tr>
                                                );
                                            })
                                        )}
                                    </tbody>
                                </table>
                            </div>

                            {!followersLoading && !followersError && followersTotalCount > followersPageSize && (
                                <Pagination
                                    currentPage={followersPage}
                                    totalPages={Math.max(1, Math.ceil(followersTotalCount / followersPageSize))}
                                    totalItems={followersTotalCount}
                                    itemsPerPage={followersPageSize}
                                    onPageChange={setFollowersPage}
                                    itemLabel="người theo dõi"
                                />
                            )}
                        </div>
                    </div>
                </div>
            )}

            {/* Popup: lịch sử donate & rút tiền */}
            {showHistoryModal && (
                <div
                    role="presentation"
                    style={{
                        position: 'fixed',
                        inset: 0,
                        backgroundColor: 'rgba(15, 23, 42, 0.45)',
                        display: 'flex',
                        alignItems: 'center',
                        justifyContent: 'center',
                        zIndex: 10000,
                        padding: '1rem',
                    }}
                    onClick={() => setShowHistoryModal(false)}
                >
                    <div
                        role="dialog"
                        aria-modal="true"
                        aria-labelledby="history-modal-title"
                        style={{
                            backgroundColor: '#ffffff',
                            borderRadius: '16px',
                            maxWidth: '960px',
                            width: '100%',
                            maxHeight: 'min(90vh, 900px)',
                            overflow: 'auto',
                            boxShadow: '0 25px 50px rgba(0,0,0,0.18)',
                            border: '1px solid #e5e7eb',
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <div style={{ display: 'flex', alignItems: 'flex-start', justifyContent: 'space-between', gap: '1rem', padding: '1.25rem 1.5rem', borderBottom: '1px solid #f1f5f9' }}>
                            <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem', minWidth: 0 }}>
                                <div style={{
                                    width: '44px', height: '44px', borderRadius: '12px',
                                    background: 'linear-gradient(135deg, #13ec5b 0%, #10d452 100%)',
                                    display: 'flex', alignItems: 'center', justifyContent: 'center',
                                    flexShrink: 0,
                                }}
                                >
                                    <History style={{ width: '22px', height: '22px', color: '#ffffff' }} />
                                </div>
                                <div style={{ minWidth: 0 }}>
                                    <h2 id="history-modal-title" style={{ fontSize: '1.125rem', fontWeight: 700, color: '#0f172a', margin: 0 }}>Lịch sử donate, mở khóa &amp; rút tiền</h2>
                                    <p style={{ fontSize: '0.8125rem', color: '#64748b', margin: '0.35rem 0 0 0' }}>Donate nhận được, lượt mở khóa chương trả phí của độc giả và các yêu cầu rút tiền.</p>
                                </div>
                            </div>
                            <button
                                type="button"
                                aria-label="Đóng"
                                onClick={() => setShowHistoryModal(false)}
                                style={{
                                    border: 'none',
                                    background: '#f1f5f9',
                                    borderRadius: '10px',
                                    width: '36px',
                                    height: '36px',
                                    display: 'flex',
                                    alignItems: 'center',
                                    justifyContent: 'center',
                                    cursor: 'pointer',
                                    flexShrink: 0,
                                }}
                            >
                                <X style={{ width: '18px', height: '18px', color: '#475569' }} />
                            </button>
                        </div>

                        <div style={{ padding: '1.25rem 1.5rem 1.5rem' }}>
                            <div
                                role="tablist"
                                aria-label="Chọn loại lịch sử"
                                style={{
                                    display: 'flex',
                                    flexWrap: 'wrap',
                                    gap: '0.5rem',
                                    marginBottom: '1.25rem',
                                }}
                            >
                                <button
                                    type="button"
                                    role="tab"
                                    aria-selected={historyModalTab === 'donate'}
                                    onClick={() => setHistoryModalTab('donate')}
                                    style={{
                                        padding: '0.5rem 1rem',
                                        borderRadius: '10px',
                                        border: historyModalTab === 'donate' ? '1px solid #22c55e' : '1px solid #e2e8f0',
                                        backgroundColor: historyModalTab === 'donate' ? '#f0fdf4' : '#ffffff',
                                        color: historyModalTab === 'donate' ? '#166534' : '#64748b',
                                        fontSize: '0.8125rem',
                                        fontWeight: 700,
                                        cursor: 'pointer',
                                    }}
                                >
                                    Donate &amp; rút tiền
                                </button>
                                <button
                                    type="button"
                                    role="tab"
                                    aria-selected={historyModalTab === 'unlock'}
                                    onClick={() => setHistoryModalTab('unlock')}
                                    style={{
                                        padding: '0.5rem 1rem',
                                        borderRadius: '10px',
                                        border: historyModalTab === 'unlock' ? '1px solid #22c55e' : '1px solid #e2e8f0',
                                        backgroundColor: historyModalTab === 'unlock' ? '#f0fdf4' : '#ffffff',
                                        color: historyModalTab === 'unlock' ? '#166534' : '#64748b',
                                        fontSize: '0.8125rem',
                                        fontWeight: 700,
                                        cursor: 'pointer',
                                    }}
                                >
                                    Mở khóa chương
                                </button>
                            </div>

                            {historyModalTab === 'donate' && (
                                <>
                                    <div style={{
                                        backgroundColor: '#f0fdf4',
                                        borderRadius: '14px',
                                        padding: '1rem 1.15rem',
                                        border: '1px solid #bbf7d0',
                                        marginBottom: '1.25rem',
                                    }}
                                    >
                                        <div style={{ display: 'flex', alignItems: 'flex-start', gap: '0.75rem' }}>
                                            <div style={{
                                                width: '40px', height: '40px', borderRadius: '12px', backgroundColor: '#dcfce7',
                                                display: 'flex', alignItems: 'center', justifyContent: 'center', flexShrink: 0,
                                            }}
                                            >
                                                <Percent style={{ width: '20px', height: '20px', color: '#16a34a' }} />
                                            </div>
                                            <div>
                                                <div style={{ fontSize: '0.9rem', fontWeight: 800, color: '#166534', marginBottom: '0.25rem' }}>Tỷ lệ chia sẻ: 70% cho tác giả, 30% nền tảng</div>
                                                <div style={{ fontSize: '0.8125rem', color: '#166534', lineHeight: 1.5 }}>
                                                    Các khoản <b>Donate</b> trong lịch sử là phần tác giả nhận sau khi nền tảng trừ <b>30%</b> phí.
                                                </div>
                                            </div>
                                        </div>
                                    </div>

                                    <div style={{ borderRadius: '14px', border: '1px solid #e5e7eb', overflow: 'hidden' }}>
                                        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
                                            <thead>
                                                <tr style={{ backgroundColor: '#f8fafc' }}>
                                                    <th style={{ padding: '0.85rem 1rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>THỜI GIAN</th>
                                                    <th style={{ padding: '0.85rem 1rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>LOẠI</th>
                                                    <th style={{ padding: '0.85rem 1rem', textAlign: 'right', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>SỐ COIN</th>
                                                    <th style={{ padding: '0.85rem 1rem', textAlign: 'right', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>THỰC NHẬN</th>
                                                    <th style={{ padding: '0.85rem 1rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>GHI CHÚ</th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                {authorActivityLoading ? (
                                                    <tr>
                                                        <td colSpan={5} style={{ padding: '2rem', textAlign: 'center', color: '#64748b' }}>Đang tải...</td>
                                                    </tr>
                                                ) : authorActivityError ? (
                                                    <tr>
                                                        <td colSpan={5} style={{ padding: '2rem', textAlign: 'center', color: '#dc2626' }}>{authorActivityError}</td>
                                                    </tr>
                                                ) : authorActivityItems.length === 0 ? (
                                                    <tr>
                                                        <td colSpan={5} style={{ padding: '2.5rem 1.5rem', textAlign: 'center', color: '#64748b' }}>Chưa có giao dịch nào.</td>
                                                    </tr>
                                                ) : (
                                                    authorActivityItems.map((item) => {
                                                        const createdAt = item.createdAt ?? item.CreatedAt;
                                                        const timeStr = createdAt
                                                            ? new Date(createdAt).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
                                                            : '—';
                                                        const typeLabel = (item.type || item.Type) === 'WITHDRAW' ? 'Rút tiền' : 'Donate';
                                                        const amount = item.amount ?? item.Amount ?? 0;
                                                        const isWithdraw = (item.type || item.Type) === 'WITHDRAW';
                                                        const withdrawStatusRaw = item.withdrawStatus ?? item.WithdrawStatus;
                                                        const statusUpper = String(withdrawStatusRaw ?? '').toUpperCase();
                                                        const netReceived = isWithdraw
                                                            ? (statusUpper === 'COMPLETED' || statusUpper === 'SUCCESS' ? Number(amount) : 0)
                                                            : Math.max(0, Number(amount) - Math.floor(Number(amount) * 0.3));
                                                        const statusLabel =
                                                            statusUpper === 'PENDING' ? 'Chờ xử lý' :
                                                                statusUpper === 'PENDING_REVIEW' ? 'Chờ xét duyệt' :
                                                                    statusUpper === 'PROCESSING' ? 'Đang xử lý' :
                                                                        statusUpper === 'COMPLETED' || statusUpper === 'SUCCESS' ? 'Hoàn thành' :
                                                                            statusUpper === 'FAILED' ? 'Thất bại' :
                                                                                statusUpper === 'CANCELLED' ? 'Đã hủy' :
                                                                                    statusUpper || '—';
                                                        const note = isWithdraw
                                                            ? (['PENDING', 'PENDING_REVIEW', 'PROCESSING'].includes(statusUpper)
                                                                ? statusLabel
                                                                : (item.note ?? item.Note) || statusLabel)
                                                            : (item.senderDisplayName ?? item.SenderDisplayName
                                                                ? `${item.senderDisplayName ?? item.SenderDisplayName}${item.note ?? item.Note ? ` — ${item.note || item.Note}` : ''}`
                                                                : (item.note ?? item.Note) || '—');
                                                        const canCancelWithdraw = isWithdraw && (statusUpper === 'PENDING' || statusUpper === 'PENDING_REVIEW');
                                                        const vndAmount = Number(amount) * COIN_RATE_VND;
                                                        const vndNet = netReceived * COIN_RATE_VND;
                                                        return (
                                                            <tr key={item.id ?? item.Id} style={{ borderBottom: '1px solid #e5e7eb' }}>
                                                                <td style={{ padding: '0.85rem 1rem', color: '#374151' }}>{timeStr}</td>
                                                                <td style={{ padding: '0.85rem 1rem', color: '#374151' }}>{typeLabel}</td>
                                                                <td style={{ padding: '0.85rem 1rem', textAlign: 'right', fontWeight: 600, color: (item.type || item.Type) === 'WITHDRAW' ? '#dc2626' : '#15803d' }}>
                                                                    <div>{(item.type || item.Type) === 'WITHDRAW' ? '-' : '+'}{Number(amount).toLocaleString()} coin</div>
                                                                    <div style={{ fontSize: '0.75rem', color: '#64748b' }}>≈ {formatVnd(vndAmount)}</div>
                                                                </td>
                                                                <td style={{ padding: '0.85rem 1rem', textAlign: 'right', fontWeight: 700, color: (item.type || item.Type) === 'WITHDRAW' ? '#b91c1c' : '#16a34a' }}>
                                                                    <div>
                                                                        {isWithdraw ? (netReceived > 0 ? `+${Number(netReceived).toLocaleString()}` : '—') : `+${Number(netReceived).toLocaleString()}`} coin
                                                                    </div>
                                                                    <div style={{ fontSize: '0.75rem', color: '#64748b' }}>
                                                                        {isWithdraw ? (netReceived > 0 ? `≈ ${formatVnd(vndNet)}` : '—') : `≈ ${formatVnd(vndNet)}`}
                                                                    </div>
                                                                </td>
                                                                <td style={{ padding: '0.85rem 1rem', color: '#64748b', maxWidth: '280px' }}>
                                                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '0.5rem' }}>
                                                                        <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{note}</div>
                                                                        {canCancelWithdraw && (
                                                                            <button
                                                                                type="button"
                                                                                onClick={() => handleCancelWithdraw(item.id ?? item.Id)}
                                                                                style={{
                                                                                    alignSelf: 'flex-start',
                                                                                    padding: '0.35rem 0.75rem',
                                                                                    borderRadius: '10px',
                                                                                    border: '1px solid #fecaca',
                                                                                    backgroundColor: '#fef2f2',
                                                                                    color: '#b91c1c',
                                                                                    fontSize: '0.75rem',
                                                                                    fontWeight: 700,
                                                                                    cursor: 'pointer',
                                                                                }}
                                                                            >
                                                                                Hủy
                                                                            </button>
                                                                        )}
                                                                    </div>
                                                                </td>
                                                            </tr>
                                                        );
                                                    })
                                                )}
                                            </tbody>
                                        </table>
                                    </div>
                                </>
                            )}

                            {historyModalTab === 'unlock' && (
                                <>
                                    <div style={{
                                        backgroundColor: '#f0f9ff',
                                        borderRadius: '14px',
                                        padding: '1rem 1.15rem',
                                        border: '1px solid #bae6fd',
                                        marginBottom: '1.25rem',
                                    }}
                                    >
                                        <div style={{ fontSize: '0.9rem', fontWeight: 800, color: '#0369a1', marginBottom: '0.25rem' }}>Mở khóa chương trả phí</div>
                                        <div style={{ fontSize: '0.8125rem', color: '#0c4a6e', lineHeight: 1.5 }}>
                                            Mỗi dòng là một lượt độc giả mở khóa chương. <b>Coin đã trả</b> là số coin người đọc bị trừ; <b>Phí NT</b> và <b>Thực nhận</b> theo ghi nhận hệ thống (thu nhập tác giả sau phí nền tảng).
                                        </div>
                                    </div>
                                    <div style={{ borderRadius: '14px', border: '1px solid #e5e7eb', overflow: 'hidden' }}>
                                        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
                                            <thead>
                                                <tr style={{ backgroundColor: '#f8fafc' }}>
                                                    <th style={{ padding: '0.85rem 1rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>THỜI GIAN</th>
                                                    <th style={{ padding: '0.85rem 1rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>TRUYỆN</th>
                                                    <th style={{ padding: '0.85rem 1rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>CHƯƠNG</th>
                                                    <th style={{ padding: '0.85rem 1rem', textAlign: 'right', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>COIN ĐÃ TRẢ</th>
                                                    <th style={{ padding: '0.85rem 1rem', textAlign: 'right', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>PHÍ NT</th>
                                                    <th style={{ padding: '0.85rem 1rem', textAlign: 'right', fontWeight: 600, color: '#475569', fontSize: '0.75rem' }}>THỰC NHẬN</th>
                                                </tr>
                                            </thead>
                                            <tbody>
                                                {authorUnlockLoading ? (
                                                    <tr>
                                                        <td colSpan={6} style={{ padding: '2rem', textAlign: 'center', color: '#64748b' }}>Đang tải...</td>
                                                    </tr>
                                                ) : authorUnlockError ? (
                                                    <tr>
                                                        <td colSpan={6} style={{ padding: '2rem', textAlign: 'center', color: '#dc2626' }}>{authorUnlockError}</td>
                                                    </tr>
                                                ) : authorUnlockItems.length === 0 ? (
                                                    <tr>
                                                        <td colSpan={6} style={{ padding: '2.5rem 1.5rem', textAlign: 'center', color: '#64748b' }}>Chưa có lượt mở khóa nào.</td>
                                                    </tr>
                                                ) : (
                                                    authorUnlockItems.map((row, idx) => {
                                                        const unlockedAt = row.unlockedAt ?? row.UnlockedAt;
                                                        const timeStr = unlockedAt
                                                            ? new Date(unlockedAt).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' })
                                                            : '—';
                                                        const storyTitle = row.storyTitle ?? row.StoryTitle ?? '—';
                                                        const chapterTitle = row.chapterTitle ?? row.ChapterTitle ?? '—';
                                                        const coinsPaid = Number(row.coinsPaid ?? row.CoinsPaid ?? 0);
                                                        const platformFee = Math.round(Number(row.platformFee ?? row.PlatformFee ?? 0));
                                                        const netAmount = Math.round(Number(row.netAmount ?? row.NetAmount ?? 0));
                                                        const vndPaid = coinsPaid * COIN_RATE_VND;
                                                        const vndNet = netAmount * COIN_RATE_VND;
                                                        const rowKey = row.purchaseId ?? row.PurchaseId ?? idx;
                                                        return (
                                                            <tr key={String(rowKey)} style={{ borderBottom: '1px solid #e5e7eb' }}>
                                                                <td style={{ padding: '0.85rem 1rem', color: '#374151' }}>{timeStr}</td>
                                                                <td style={{ padding: '0.85rem 1rem', color: '#374151', maxWidth: '200px' }}>
                                                                    <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={storyTitle}>{storyTitle}</div>
                                                                </td>
                                                                <td style={{ padding: '0.85rem 1rem', color: '#374151', maxWidth: '180px' }}>
                                                                    <div style={{ overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }} title={chapterTitle}>{chapterTitle}</div>
                                                                </td>
                                                                <td style={{ padding: '0.85rem 1rem', textAlign: 'right', fontWeight: 600, color: '#0f766e' }}>
                                                                    <div>{coinsPaid.toLocaleString()} coin</div>
                                                                    <div style={{ fontSize: '0.75rem', color: '#64748b' }}>≈ {formatVnd(vndPaid)}</div>
                                                                </td>
                                                                <td style={{ padding: '0.85rem 1rem', textAlign: 'right', color: '#92400e' }}>
                                                                    {platformFee.toLocaleString()} coin
                                                                </td>
                                                                <td style={{ padding: '0.85rem 1rem', textAlign: 'right', fontWeight: 700, color: '#16a34a' }}>
                                                                    <div>
                                                                        +{netAmount.toLocaleString()} coin
                                                                    </div>
                                                                    <div style={{ fontSize: '0.75rem', color: '#64748b' }}>≈ {formatVnd(vndNet)}</div>
                                                                </td>
                                                            </tr>
                                                        );
                                                    })
                                                )}
                                            </tbody>
                                        </table>
                                    </div>
                                    {!authorUnlockLoading && !authorUnlockError && authorUnlockTotalCount > AUTHOR_UNLOCK_PAGE_SIZE && (
                                        <div style={{ marginTop: '1rem' }}>
                                            <Pagination
                                                currentPage={authorUnlockPage}
                                                totalPages={Math.max(1, Math.ceil(authorUnlockTotalCount / AUTHOR_UNLOCK_PAGE_SIZE))}
                                                totalItems={authorUnlockTotalCount}
                                                itemsPerPage={AUTHOR_UNLOCK_PAGE_SIZE}
                                                onPageChange={(p) => loadAuthorUnlockHistory(p)}
                                                itemLabel="lượt mở khóa"
                                            />
                                        </div>
                                    )}
                                </>
                            )}
                        </div>
                    </div>
                </div>
            )}

            {/* Popup xác nhận xóa truyện */}
            {deleteStoryConfirm.open && (
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
                    onClick={() => setDeleteStoryConfirm({ open: false, storyId: null })}
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
                            Xác nhận xóa truyện
                        </h3>
                        <p style={{ fontSize: '0.875rem', color: '#64748b', margin: '0 0 1.5rem 0', lineHeight: 1.5 }}>
                            Bạn có chắc chắn muốn xóa truyện này? Toàn bộ chương và dữ liệu liên quan sẽ bị xóa vĩnh viễn. Hành động này không thể hoàn tác.
                        </p>
                        <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                            <button
                                onClick={() => setDeleteStoryConfirm({ open: false, storyId: null })}
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
                                onClick={handleConfirmDeleteStory}
                                style={{
                                    padding: '0.625rem 1.25rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    color: '#ffffff',
                                    backgroundColor: '#dc2626',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: 'pointer'
                                }}
                            >
                                Xóa
                            </button>
                        </div>
                    </div>
                </div>
            )}

            {/* Popup xác nhận hủy yêu cầu rút tiền */}
            {cancelWithdrawConfirm.open && (
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
                    onClick={() => setCancelWithdrawConfirm({ open: false, withdrawId: null })}
                >
                    <div
                        style={{
                            backgroundColor: '#ffffff',
                            borderRadius: '12px',
                            padding: '1.5rem',
                            maxWidth: '420px',
                            width: '90%',
                            boxShadow: '0 20px 60px rgba(0, 0, 0, 0.3)'
                        }}
                        onClick={(e) => e.stopPropagation()}
                    >
                        <h3 style={{ fontSize: '1.125rem', fontWeight: 600, color: '#1e293b', margin: '0 0 1rem 0' }}>
                            Xác nhận hủy yêu cầu rút tiền
                        </h3>
                        <p style={{ fontSize: '0.875rem', color: '#64748b', margin: '0 0 1.5rem 0', lineHeight: 1.5 }}>
                            Bạn có chắc chắn muốn hủy yêu cầu rút tiền này không?
                        </p>
                        <div style={{ display: 'flex', gap: '0.75rem', justifyContent: 'flex-end' }}>
                            <button
                                onClick={() => setCancelWithdrawConfirm({ open: false, withdrawId: null })}
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
                                Không, giữ nguyên
                            </button>
                            <button
                                onClick={handleConfirmCancelWithdraw}
                                style={{
                                    padding: '0.625rem 1.25rem',
                                    fontSize: '0.875rem',
                                    fontWeight: 600,
                                    color: '#ffffff',
                                    backgroundColor: '#dc2626',
                                    border: 'none',
                                    borderRadius: '8px',
                                    cursor: 'pointer'
                                }}
                            >
                                Có, hủy yêu cầu
                            </button>
                        </div>
                    </div>
                </div>
            )}
        </div>
    );
}

export default AuthorStoryManagement;