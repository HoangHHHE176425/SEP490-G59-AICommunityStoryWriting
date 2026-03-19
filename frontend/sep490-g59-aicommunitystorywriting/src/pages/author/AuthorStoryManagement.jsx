import { useState, useEffect, useCallback, useRef } from 'react';
import { Plus, Edit, Eye, Heart, MessageSquare, Star, ChevronRight, Book, User, LogOut, Trash2, List, Wallet, History, Coins, ArrowDownToLine, Landmark, ShieldCheck, ShieldX } from 'lucide-react';
import { StoryEditor } from './StoryEditor';
import { StoryInfoEditor } from './StoryInfoEditor';
import { ChapterListManager } from '../author/ChapterListManager';
import { StoryCommentsViewer } from './StoryCommentsViewer';
import { ChapterEditorPage } from '../author/ChapterEditorPage';
import { Footer } from '../../components/homepage/Footer';
import { Header } from '../../components/homepage/Header';
import { createStory, updateStory, getStories, getStoryById, deleteStory } from '../../api/story/storyApi';
import { createChapter, updateChapter, getChapterById, getChapters, createChapterVersion, updateChapterVersion, getChapterVersionById, submitChapterVersion } from '../../api/chapter/chapterApi';
import * as coinApi from '../../api/coins/coinApi';
import { resolveBackendUrl } from '../../utils/resolveBackendUrl';
import { useAuth } from '../../contexts/AuthContext';
import { useToast } from '../../components/author/story-editor/Toast';
import { Pagination } from '../../components/pagination/Pagination';

function mapStoryFromApi(item) {
    const status = item.status || item.Status || '';
    const storyProgressStatus = item.storyProgressStatus ?? item.StoryProgressStatus ?? '';
    const publishStatusMap = {
        DRAFT: 'Bản nháp',
        PENDING_REVIEW: 'Chờ duyệt',
        REJECTED: 'Bị từ chối',
        PUBLISHED: 'Đã xuất bản',
        HIDDEN: 'Đã ẩn',
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
        follows: Number(item.totalFavorites ?? item.TotalFavorites ?? 0),
        rating: item.avgRating ?? item.AvgRating ?? 0,
        lastUpdate: lastUpdate || 'Chưa cập nhật',
        publishStatus,
        storyProgressStatus: storyProgressStatus || 'ONGOING',
        progressStatusDisplay,
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

    // Lịch sử donate + rút tiền (author)
    const [authorActivityItems, setAuthorActivityItems] = useState([]);
    const [authorActivityLoading, setAuthorActivityLoading] = useState(false);
    const [authorActivityError, setAuthorActivityError] = useState(null);
    // Rút tiền: số dư ví, số coin nhập, trạng thái gửi
    const [withdrawBalance, setWithdrawBalance] = useState(null);
    const [withdrawAmount, setWithdrawAmount] = useState('');
    const [withdrawSubmitting, setWithdrawSubmitting] = useState(false);
    const [withdrawError, setWithdrawError] = useState(null);
    // Rút tiền: chọn TK ngân hàng từ danh sách của author
    const [selectedBankAccountIdx, setSelectedBankAccountIdx] = useState(-1);

    // Danh sách ngân hàng (dùng cho form thêm tài khoản ngân hàng)
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

    // Form "Thêm tài khoản ngân hàng" (tách khỏi phần rút tiền)
    const [bankName, setBankName] = useState('');
    const [accountNumber, setAccountNumber] = useState('');
    const [accountHolderName, setAccountHolderName] = useState('');
    const [branchName, setBranchName] = useState('');
    const [isBankVerified, setIsBankVerified] = useState(true); // demo UI

    // Danh sách tài khoản ngân hàng (demo)
    const [bankAccounts, setBankAccounts] = useState([
        {
            user_id: authorId || 'me',
            bank_name: 'Vietcombank',
            account_number: '0123456789',
            account_holder_name: userDisplayName?.toUpperCase?.() ? userDisplayName.toUpperCase() : userDisplayName,
            branch_name: 'CN TP.HCM',
            is_verified: true,
            updated_at: new Date().toISOString(),
        },
    ]);

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

    const verifiedBankAccounts = bankAccounts.filter((a) => a.is_verified);
    const selectedBankAccount = verifiedBankAccounts[selectedBankAccountIdx] ?? null;

    const buildBankInfoStringFromAccount = (acc) => {
        if (!acc) return null;
        const bn = String(acc.bank_name || '').trim();
        const an = String(acc.account_number || '').trim();
        const ah = String(acc.account_holder_name || '').trim();
        const br = String(acc.branch_name || '').trim();
        const verified = !!acc.is_verified;
        if (!bn || !an || !ah) return null;
        return [
            `bank_name=${bn}`,
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
        getStories({ authorId, page, pageSize: STORIES_PAGE_SIZE })
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
                            getChapters({ storyId, status: 'PUBLISHED', pageSize: 1 }),
                            getChapters({ storyId, status: 'PENDING_REVIEW', pageSize: 1 })
                        ])
                            .then(([pubRes, pendRes]) => {
                                const pubList = Array.isArray(pubRes) ? pubRes : (pubRes?.items ?? pubRes?.Items ?? []);
                                const pendList = Array.isArray(pendRes) ? pendRes : (pendRes?.items ?? pendRes?.Items ?? []);
                                return {
                                    ...s,
                                    _hasPublishedChapter: pubList.length > 0,
                                    _hasPendingReviewChapter: pendList.length > 0
                                };
                            })
                            .catch(() => ({ ...s, _hasPublishedChapter: false, _hasPendingReviewChapter: false }));
                    })
                ).then((itemsWithFlag) => {
                    setStories(
                        itemsWithFlag.map((item) => {
                            const mapped = mapStoryFromApi(item);
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
        if (activeView !== 'history' || !authorId) return;
        setAuthorActivityLoading(true);
        setAuthorActivityError(null);
        coinApi.getAuthorActivity({ page: 1, pageSize: 100 })
            .then((res) => {
                if (res?.success && res?.data?.items) {
                    setAuthorActivityItems(res.data.items);
                } else {
                    setAuthorActivityItems([]);
                    if (!res?.success) setAuthorActivityError(res?.message ?? 'Không tải được lịch sử.');
                }
            })
            .catch(() => {
                setAuthorActivityItems([]);
                setAuthorActivityError('Không tải được lịch sử donate và rút tiền.');
            })
            .finally(() => setAuthorActivityLoading(false));
    }, [activeView, authorId]);

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
        published: stories.filter(s => s.status === 'published').length,
        totalChapters: stories.reduce((acc, s) => acc + s.chapters, 0),
        followers: 0,
        recommendations: 0,
    };

    const handleCreateStory = () => {
        setCurrentStory(null);
        setActiveView('createStory');
    };

    const handleEditStory = async (story) => {
        if (!story?.id) return;
        try {
            const fullStory = await getStoryById(story.id);
            const mapped = mapStoryFromApi(fullStory);
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

    const handleViewComments = (story) => {
        setCurrentStory(story);
        setActiveView('comments');
    };

    const handleAddChapter = (story) => {
        setCurrentStory(story);
        setCurrentChapter(null);
        setActiveView('addChapter');
    };

    const handleEditChapter = async (chapter) => {
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
        const chapterId = chapterFromList?.id ?? chapterFromList?.Id;
        if (!chapterId) {
            showToast('Không tìm thấy ID chương', 'error');
            return;
        }
        const number = chapterFromList?.number ?? (chapterFromList?.orderIndex ?? chapterFromList?.OrderIndex ?? chapterFromList?.order_index ?? 0) + 1;
        const title = chapterFromList?.title ?? chapterFromList?.name ?? `Chương ${number}`;
        setCurrentStory(story);
        setCurrentChapter(null);
        setSourceChapterForVersion({
            id: chapterId,
            number: Number(number) || 1,
            title,
            content: '',
            status: 'draft',
            accessType: 'public',
            price: 0,
        });
        setEditingVersion(null);
        setActiveView('addChapterVersion');
    };

    /** Mở editor chỉnh sửa version đã có: load chi tiết version rồi mở ChapterEditorPage ở chế độ edit version. */
    const handleEditVersion = async (chapter, versionFromList) => {
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

    const handleSaveChapter = async (chapterData) => {
        const storyId = currentStory?.id ?? currentStory?.Id;
        if (!storyId) {
            showToast('Không tìm thấy truyện', 'error');
            return;
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

                setActiveView('chapterList');
                setCurrentChapter(null);
                setSourceChapterForVersion(null);
                setEditingVersion(null);
                return;
            }

            // Map status: 'draft' -> 'DRAFT', 'published' -> 'PENDING_REVIEW'
            const apiStatus = chapterData.status === 'published' ? 'PENDING_REVIEW' : 'DRAFT';

            // Map accessType: 'public' -> 'FREE', 'paid' -> 'PAID'
            const apiAccessType = chapterData.accessType === 'paid' ? 'PAID' : 'FREE';

            // Xác định là chỉnh sửa hay thêm mới dựa vào currentChapter hoặc chapterData.id
            const isEditMode = currentChapter && (currentChapter.id || currentChapter.Id);

            if (!isEditMode) {
                // Thêm chương mới
                const orderIndex = (chapterData.number || 1) - 1; // number bắt đầu từ 1, orderIndex từ 0

                await createChapter({
                    storyId,
                    title: chapterData.title,
                    content: chapterData.content || '',
                    orderIndex,
                    status: apiStatus,
                    accessType: apiAccessType,
                    coinPrice: apiAccessType === 'PAID' ? (chapterData.price || 0) : 0,
                });

                showToast(
                    apiStatus === 'DRAFT' ? 'Đã lưu nháp chương mới' : 'Đã xuất bản chương mới',
                    'success'
                );
            } else {
                // Cập nhật chương hiện có
                const chapterId = currentChapter.id ?? currentChapter.Id;
                if (!chapterId) {
                    showToast('Không tìm thấy ID chương', 'error');
                    return;
                }

                await updateChapter(chapterId, {
                    title: chapterData.title,
                    content: chapterData.content || '',
                    orderIndex: (chapterData.number || 1) - 1,
                    status: apiStatus,
                    accessType: apiAccessType,
                    coinPrice: apiAccessType === 'PAID' ? (chapterData.price || 0) : 0,
                    changeSummary: chapterData.changeSummary ? String(chapterData.changeSummary).trim() : undefined,
                });

                showToast(
                    apiStatus === 'DRAFT' ? 'Đã cập nhật chương (lưu nháp)' : 'Đã cập nhật chương (xuất bản)',
                    'success'
                );
            }

            // Quay về danh sách chương
            setActiveView('chapterList');
            setCurrentChapter(null);
            setSourceChapterForVersion(null);
            setEditingVersion(null);
        } catch (error) {
            const errorMessage = error?.response?.data?.message || error?.message || 'Không thể lưu chương';
            showToast(errorMessage, 'error');
            console.error('Error saving chapter:', error);
        }
    };

    const [deleteStoryConfirm, setDeleteStoryConfirm] = useState({ open: false, storyId: null });

    const handleDeleteStory = (storyId) => {
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
            storyProgressStatus: storyData.storyProgressStatus || storyData.status,
            coverImage: storyData.cover,
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
                coverImage: storyData.cover,
            });
        }

        loadStories(storiesCurrentPage);
        if (storyData.isDraft) {
            setActiveView('stories');
            setCurrentStory(null);
        }
    };

    const { showToast, ToastContainer, clearToasts } = useToast();

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
                title: infoData.title,
                summary: infoData.note ?? '',
                categoryIds,
                status: storyPublishStatus,
                storyProgressStatus: infoData.status || infoData.publishStatus,
                ageRating: infoData.ageRating,
                coverImage: infoData.cover,
            });
            setStories(stories.map(s => s.id === currentStory.id ? { ...s, ...infoData, summary: infoData.note } : s));
            setCurrentStory((prev) => prev ? { ...prev, ...infoData, summary: infoData.note } : null);
            showToast('Đã lưu thay đổi thông tin truyện', 'success');
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
                />
                <ToastContainer />
            </>
        );
    }

    if (activeView === 'addChapter' || activeView === 'editChapter' || activeView === 'addChapterVersion') {
        return (
            <>
                <ChapterEditorPage
                    story={currentStory}
                    chapter={activeView === 'editChapter' ? currentChapter : null}
                    sourceChapterForVersion={activeView === 'addChapterVersion' ? sourceChapterForVersion : null}
                    editingVersion={activeView === 'addChapterVersion' ? editingVersion : null}
                    readOnly={viewChapterOnly}
                    onSave={handleSaveChapter}
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

                        <button
                            onClick={() => {
                                setActiveMenu('bank-accounts');
                                setActiveView('bank-accounts');
                            }}
                            style={{
                                width: '100%',
                                padding: '0.875rem 1.5rem',
                                backgroundColor: activeMenu === 'bank-accounts' ? '#f0fdf4' : 'transparent',
                                border: 'none',
                                borderLeft: activeMenu === 'bank-accounts' ? '3px solid #13ec5b' : '3px solid transparent',
                                borderRadius: '9999px',
                                marginLeft: '0.5rem',
                                marginRight: '0.5rem',
                                textAlign: 'left',
                                fontSize: '0.875rem',
                                fontWeight: activeMenu === 'bank-accounts' ? 600 : 500,
                                color: activeMenu === 'bank-accounts' ? '#13ec5b' : '#333333',
                                cursor: 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.75rem',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                if (activeMenu !== 'bank-accounts') e.currentTarget.style.backgroundColor = '#f9fafb';
                            }}
                            onMouseLeave={(e) => {
                                if (activeMenu !== 'bank-accounts') e.currentTarget.style.backgroundColor = 'transparent';
                            }}
                        >
                            <Landmark style={{ width: '20px', height: '20px' }} />
                            Tài khoản ngân hàng
                        </button>

                        <button
                            onClick={() => {
                                setActiveMenu('history');
                                setActiveView('history');
                            }}
                            style={{
                                width: '100%',
                                padding: '0.875rem 1.5rem',
                                backgroundColor: activeMenu === 'history' ? '#f0fdf4' : 'transparent',
                                border: 'none',
                                borderLeft: activeMenu === 'history' ? '3px solid #13ec5b' : '3px solid transparent',
                                borderRadius: '9999px',
                                marginLeft: '0.5rem',
                                marginRight: '0.5rem',
                                textAlign: 'left',
                                fontSize: '0.875rem',
                                fontWeight: activeMenu === 'history' ? 600 : 500,
                                color: activeMenu === 'history' ? '#13ec5b' : '#333333',
                                cursor: 'pointer',
                                display: 'flex',
                                alignItems: 'center',
                                gap: '0.75rem',
                                transition: 'all 0.2s'
                            }}
                            onMouseEnter={(e) => {
                                if (activeMenu !== 'history') e.currentTarget.style.backgroundColor = '#f9fafb';
                            }}
                            onMouseLeave={(e) => {
                                if (activeMenu !== 'history') e.currentTarget.style.backgroundColor = 'transparent';
                            }}
                        >
                            <History style={{ width: '20px', height: '20px' }} />
                            Lịch sử donate và rút tiền
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
                                    onBack();
                                } catch (error) {
                                    console.error('Logout error:', error);
                                    onBack();
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
                            </div>

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
                                        Tài khoản ngân hàng (đã xác thực)
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
                                        {verifiedBankAccounts.map((acc, idx) => (
                                            <option key={`${acc.bank_name}-${acc.account_number}-${idx}`} value={String(idx)}>
                                                {acc.bank_name} • {maskAccountNumber(acc.account_number)} • {acc.account_holder_name}
                                            </option>
                                        ))}
                                    </select>

                                    {verifiedBankAccounts.length === 0 ? (
                                        <p style={{ fontSize: '0.8125rem', color: '#b45309', margin: '0.5rem 0 0 0' }}>
                                            Bạn chưa có tài khoản ngân hàng đã xác thực. Vào tab <b>Tài khoản ngân hàng</b> để thêm và xác thực trước khi rút.
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
                                        min={1}
                                        max={withdrawBalance != null ? withdrawBalance : undefined}
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
                                        !withdrawAmount ||
                                        !selectedBankAccount ||
                                        (withdrawBalance != null && Number(withdrawAmount) > withdrawBalance) ||
                                        Number(withdrawAmount) < 1
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
                                        opacity: (withdrawSubmitting || !withdrawAmount || (withdrawBalance != null && Number(withdrawAmount) > withdrawBalance)) ? 0.6 : 1,
                                        boxShadow: '0 2px 8px rgba(19, 236, 91, 0.35)',
                                        transition: 'all 0.2s ease'
                                    }}
                                    onMouseEnter={(e) => { if (!e.currentTarget.disabled) { e.currentTarget.style.backgroundColor = '#10d452'; e.currentTarget.style.transform = 'translateY(-1px)'; e.currentTarget.style.boxShadow = '0 4px 12px rgba(19, 236, 91, 0.4)'; } }}
                                    onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = '#13ec5b'; e.currentTarget.style.transform = 'translateY(0)'; e.currentTarget.style.boxShadow = '0 2px 8px rgba(19, 236, 91, 0.35)'; }}
                                    onClick={async () => {
                                        const amount = Number(withdrawAmount);
                                        if (!amount || amount < 1) return;
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
                            </div>
                        </div>
                    ) : activeView === 'bank-accounts' ? (
                        <div style={{ maxWidth: '960px' }}>
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
                                    <Landmark style={{ width: '28px', height: '28px', color: '#ffffff' }} />
                                </div>
                                <div>
                                    <h2 style={{ fontFamily: "'Plus Jakarta Sans', sans-serif", fontSize: '1.5rem', fontWeight: 700, color: '#1A2332', margin: 0, letterSpacing: '-0.02em' }}>
                                        Danh sách tài khoản ngân hàng
                                    </h2>
                                    <p style={{ fontSize: '0.875rem', color: '#90A1B9', margin: '6px 0 0 0' }}>
                                        Quản lý tài khoản nhận tiền và trạng thái xác thực (demo UI)
                                    </p>
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
                                <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '0.75rem' }}>
                                    <div style={{ gridColumn: 'span 2' }}>
                                        <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 500, color: '#4b5563', marginBottom: '0.5rem' }}>
                                            Ngân hàng
                                        </label>
                                        <select
                                            value={bankName}
                                            onChange={(e) => setBankName(e.target.value)}
                                            style={{ width: '100%', padding: '0.75rem 1rem', border: '1px solid #e5e7eb', borderRadius: '10px', fontSize: '0.9375rem', outline: 'none', backgroundColor: '#ffffff' }}
                                            onFocus={(e) => { e.currentTarget.style.borderColor = '#13ec5b'; e.currentTarget.style.boxShadow = '0 0 0 3px rgba(19, 236, 91, 0.2)'; }}
                                            onBlur={(e) => { e.currentTarget.style.borderColor = '#e5e7eb'; e.currentTarget.style.boxShadow = 'none'; }}
                                        >
                                            <option value="">Chọn ngân hàng</option>
                                            {BANK_OPTIONS.map((b) => (
                                                <option key={b} value={b}>{b}</option>
                                            ))}
                                        </select>
                                    </div>
                                    <div>
                                        <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 500, color: '#4b5563', marginBottom: '0.5rem' }}>
                                            Số tài khoản
                                        </label>
                                        <input
                                            type="text"
                                            value={accountNumber}
                                            onChange={(e) => setAccountNumber(e.target.value.replace(/[^\d\s]/g, ''))}
                                            placeholder="Nhập số tài khoản"
                                            style={{ width: '100%', padding: '0.75rem 1rem', border: '1px solid #e5e7eb', borderRadius: '10px', fontSize: '0.9375rem', outline: 'none' }}
                                            onFocus={(e) => { e.currentTarget.style.borderColor = '#13ec5b'; e.currentTarget.style.boxShadow = '0 0 0 3px rgba(19, 236, 91, 0.2)'; }}
                                            onBlur={(e) => { e.currentTarget.style.borderColor = '#e5e7eb'; e.currentTarget.style.boxShadow = 'none'; }}
                                        />
                                    </div>
                                    <div>
                                        <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 500, color: '#4b5563', marginBottom: '0.5rem' }}>
                                            Chủ tài khoản
                                        </label>
                                        <input
                                            type="text"
                                            value={accountHolderName}
                                            onChange={(e) => setAccountHolderName(e.target.value)}
                                            placeholder="Ví dụ: NGUYỄN VĂN A"
                                            style={{ width: '100%', padding: '0.75rem 1rem', border: '1px solid #e5e7eb', borderRadius: '10px', fontSize: '0.9375rem', outline: 'none' }}
                                            onFocus={(e) => { e.currentTarget.style.borderColor = '#13ec5b'; e.currentTarget.style.boxShadow = '0 0 0 3px rgba(19, 236, 91, 0.2)'; }}
                                            onBlur={(e) => { e.currentTarget.style.borderColor = '#e5e7eb'; e.currentTarget.style.boxShadow = 'none'; }}
                                        />
                                    </div>
                                    <div style={{ gridColumn: 'span 2' }}>
                                        <label style={{ display: 'block', fontSize: '0.8125rem', fontWeight: 500, color: '#4b5563', marginBottom: '0.5rem' }}>
                                            Chi nhánh (tuỳ chọn)
                                        </label>
                                        <input
                                            type="text"
                                            value={branchName}
                                            onChange={(e) => setBranchName(e.target.value)}
                                            placeholder="Ví dụ: CN TP.HCM"
                                            style={{ width: '100%', padding: '0.75rem 1rem', border: '1px solid #e5e7eb', borderRadius: '10px', fontSize: '0.9375rem', outline: 'none' }}
                                            onFocus={(e) => { e.currentTarget.style.borderColor = '#13ec5b'; e.currentTarget.style.boxShadow = '0 0 0 3px rgba(19, 236, 91, 0.2)'; }}
                                            onBlur={(e) => { e.currentTarget.style.borderColor = '#e5e7eb'; e.currentTarget.style.boxShadow = 'none'; }}
                                        />
                                    </div>
                                    <div style={{ gridColumn: 'span 2', display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                        <input id="bank-verified-list" type="checkbox" checked={isBankVerified} onChange={(e) => setIsBankVerified(e.target.checked)} />
                                        <label htmlFor="bank-verified-list" style={{ fontSize: '0.8125rem', color: '#64748b' }}>
                                            Đã xác thực (demo)
                                        </label>
                                    </div>
                                </div>

                                <div style={{ display: 'flex', justifyContent: 'flex-end', marginTop: '1rem' }}>
                                    <button
                                        type="button"
                                        onClick={() => {
                                            const bn = bankName.trim();
                                            const an = accountNumber.trim();
                                            const ah = accountHolderName.trim();
                                            if (!bn || !an || !ah) {
                                                showToast('Vui lòng nhập đủ: Ngân hàng, Số tài khoản, Chủ tài khoản.', 'error');
                                                return;
                                            }
                                            setBankAccounts((list) => [
                                                {
                                                    user_id: authorId || 'me',
                                                    bank_name: bn,
                                                    account_number: an,
                                                    account_holder_name: ah,
                                                    branch_name: branchName.trim(),
                                                    is_verified: isBankVerified,
                                                    updated_at: new Date().toISOString(),
                                                },
                                                ...list,
                                            ]);
                                            setBankName('');
                                            setAccountNumber('');
                                            setAccountHolderName('');
                                            setBranchName('');
                                            setIsBankVerified(true);
                                            showToast('Đã thêm tài khoản ngân hàng (demo).', 'success');
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
                                            boxShadow: '0 2px 8px rgba(19, 236, 91, 0.35)',
                                            transition: 'all 0.2s ease'
                                        }}
                                        onMouseEnter={(e) => { e.currentTarget.style.backgroundColor = '#10d452'; }}
                                        onMouseLeave={(e) => { e.currentTarget.style.backgroundColor = '#13ec5b'; }}
                                    >
                                        Thêm tài khoản
                                    </button>
                                </div>
                            </div>

                            <div style={{
                                backgroundColor: '#ffffff',
                                borderRadius: '16px',
                                border: '1px solid #e5e7eb',
                                boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
                                overflow: 'hidden'
                            }}>
                                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
                                    <thead>
                                        <tr style={{ backgroundColor: '#f8fafc' }}>
                                            <th style={{ padding: '1rem 1.25rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.8125rem', letterSpacing: '0.02em' }}>NGÂN HÀNG</th>
                                            <th style={{ padding: '1rem 1.25rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.8125rem', letterSpacing: '0.02em' }}>SỐ TK</th>
                                            <th style={{ padding: '1rem 1.25rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.8125rem', letterSpacing: '0.02em' }}>CHỦ TK</th>
                                            <th style={{ padding: '1rem 1.25rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.8125rem', letterSpacing: '0.02em' }}>CHI NHÁNH</th>
                                            <th style={{ padding: '1rem 1.25rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.8125rem', letterSpacing: '0.02em' }}>XÁC THỰC</th>
                                            <th style={{ padding: '1rem 1.25rem', textAlign: 'right', fontWeight: 600, color: '#475569', fontSize: '0.8125rem', letterSpacing: '0.02em' }}>CẬP NHẬT</th>
                                            <th style={{ padding: '1rem 1.25rem', textAlign: 'right', fontWeight: 600, color: '#475569', fontSize: '0.8125rem', letterSpacing: '0.02em' }}>HÀNH ĐỘNG</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {bankAccounts.length === 0 ? (
                                            <tr>
                                                <td colSpan={7} style={{ padding: '2.5rem', textAlign: 'center', color: '#64748b' }}>
                                                    Chưa có tài khoản ngân hàng nào.
                                                </td>
                                            </tr>
                                        ) : (
                                            bankAccounts.map((acc, idx) => (
                                                <tr key={`${acc.bank_name}-${acc.account_number}-${idx}`} style={{ borderBottom: '1px solid #e5e7eb' }}>
                                                    <td style={{ padding: '1rem 1.25rem', color: '#374151', fontWeight: 600 }}>{acc.bank_name}</td>
                                                    <td style={{ padding: '1rem 1.25rem', color: '#374151' }}>{maskAccountNumber(acc.account_number)}</td>
                                                    <td style={{ padding: '1rem 1.25rem', color: '#374151' }}>{acc.account_holder_name}</td>
                                                    <td style={{ padding: '1rem 1.25rem', color: '#64748b' }}>{acc.branch_name || '—'}</td>
                                                    <td style={{ padding: '1rem 1.25rem' }}>
                                                        {acc.is_verified ? (
                                                            <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.4rem', padding: '0.25rem 0.5rem', borderRadius: '9999px', backgroundColor: '#ecfdf5', color: '#047857', fontSize: '0.75rem', fontWeight: 700, border: '1px solid #a7f3d0' }}>
                                                                <ShieldCheck style={{ width: '14px', height: '14px' }} /> Verified
                                                            </span>
                                                        ) : (
                                                            <span style={{ display: 'inline-flex', alignItems: 'center', gap: '0.4rem', padding: '0.25rem 0.5rem', borderRadius: '9999px', backgroundColor: '#fffbeb', color: '#b45309', fontSize: '0.75rem', fontWeight: 700, border: '1px solid #fde68a' }}>
                                                                <ShieldX style={{ width: '14px', height: '14px' }} /> Unverified
                                                            </span>
                                                        )}
                                                    </td>
                                                    <td style={{ padding: '1rem 1.25rem', textAlign: 'right', color: '#64748b' }}>{formatTime(acc.updated_at)}</td>
                                                    <td style={{ padding: '1rem 1.25rem', textAlign: 'right' }}>
                                                        <div style={{ display: 'inline-flex', gap: '0.5rem' }}>
                                                            <button
                                                                type="button"
                                                                onClick={() => {
                                                                    setBankAccounts((list) =>
                                                                        list.map((x, i) =>
                                                                            i === idx
                                                                                ? { ...x, is_verified: !x.is_verified, updated_at: new Date().toISOString() }
                                                                                : x
                                                                        )
                                                                    );
                                                                    showToast(acc.is_verified ? 'Đã huỷ xác thực (demo).' : 'Đã xác thực (demo).', 'success');
                                                                }}
                                                                style={{
                                                                    padding: '0.45rem 0.75rem',
                                                                    borderRadius: '10px',
                                                                    border: '1px solid #e5e7eb',
                                                                    backgroundColor: '#ffffff',
                                                                    cursor: 'pointer',
                                                                    fontSize: '0.8125rem',
                                                                    fontWeight: 600,
                                                                    color: '#111827'
                                                                }}
                                                            >
                                                                {acc.is_verified ? 'Huỷ xác thực' : 'Xác thực'}
                                                            </button>
                                                            <button
                                                                type="button"
                                                                onClick={() => {
                                                                    if (!window.confirm('Xoá tài khoản ngân hàng này?')) return;
                                                                    setBankAccounts((list) => list.filter((_, i) => i !== idx));
                                                                    showToast('Đã xoá tài khoản (demo).', 'success');
                                                                }}
                                                                style={{
                                                                    padding: '0.45rem 0.75rem',
                                                                    borderRadius: '10px',
                                                                    border: '1px solid #fecaca',
                                                                    backgroundColor: '#fef2f2',
                                                                    cursor: 'pointer',
                                                                    fontSize: '0.8125rem',
                                                                    fontWeight: 700,
                                                                    color: '#b91c1c'
                                                                }}
                                                            >
                                                                Xoá
                                                            </button>
                                                        </div>
                                                    </td>
                                                </tr>
                                            ))
                                        )}
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    ) : activeView === 'history' ? (
                        <div style={{ maxWidth: '960px' }}>
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
                                    <History style={{ width: '28px', height: '28px', color: '#ffffff' }} />
                                </div>
                                <div>
                                    <h2 style={{ fontFamily: "'Plus Jakarta Sans', sans-serif", fontSize: '1.5rem', fontWeight: 700, color: '#1A2332', margin: 0, letterSpacing: '-0.02em' }}>Lịch sử donate và rút tiền</h2>
                                    <p style={{ fontSize: '0.875rem', color: '#90A1B9', margin: '6px 0 0 0' }}>Xem lịch sử nhận donate và các lần rút tiền</p>
                                </div>
                            </div>
                            <div style={{
                                backgroundColor: '#ffffff',
                                borderRadius: '16px',
                                border: '1px solid #e5e7eb',
                                boxShadow: '0 1px 3px rgba(0,0,0,0.05)',
                                overflow: 'hidden'
                            }}>
                                <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.875rem' }}>
                                    <thead>
                                        <tr style={{ backgroundColor: '#f8fafc' }}>
                                            <th style={{ padding: '1rem 1.25rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.8125rem', letterSpacing: '0.02em' }}>THỜI GIAN</th>
                                            <th style={{ padding: '1rem 1.25rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.8125rem', letterSpacing: '0.02em' }}>LOẠI</th>
                                            <th style={{ padding: '1rem 1.25rem', textAlign: 'right', fontWeight: 600, color: '#475569', fontSize: '0.8125rem', letterSpacing: '0.02em' }}>SỐ COIN</th>
                                            <th style={{ padding: '1rem 1.25rem', textAlign: 'left', fontWeight: 600, color: '#475569', fontSize: '0.8125rem', letterSpacing: '0.02em' }}>GHI CHÚ</th>
                                        </tr>
                                    </thead>
                                    <tbody>
                                        {authorActivityLoading ? (
                                            <tr>
                                                <td colSpan={4} style={{ padding: '2rem', textAlign: 'center', color: '#64748b' }}>Đang tải...</td>
                                            </tr>
                                        ) : authorActivityError ? (
                                            <tr>
                                                <td colSpan={4} style={{ padding: '2rem', textAlign: 'center', color: '#dc2626' }}>{authorActivityError}</td>
                                            </tr>
                                        ) : authorActivityItems.length === 0 ? (
                                            <tr>
                                                <td colSpan={4} style={{ padding: '3rem 1.5rem', textAlign: 'center' }}>
                                                    <div style={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: '0.75rem' }}>
                                                        <div style={{
                                                            width: '56px', height: '56px', borderRadius: '50%',
                                                            backgroundColor: '#f1f5f9',
                                                            display: 'flex', alignItems: 'center', justifyContent: 'center'
                                                        }}>
                                                            <History style={{ width: '28px', height: '28px', color: '#94a3b8' }} />
                                                        </div>
                                                        <p style={{ fontSize: '0.9375rem', fontWeight: 500, color: '#64748b', margin: 0 }}>Chưa có giao dịch nào</p>
                                                        <p style={{ fontSize: '0.8125rem', color: '#94a3b8', margin: 0 }}>Donate và rút tiền sẽ hiển thị tại đây</p>
                                                    </div>
                                                </td>
                                            </tr>
                                        ) : (
                                            authorActivityItems.map((item) => {
                                                const createdAt = item.createdAt ?? item.CreatedAt;
                                                const timeStr = createdAt ? new Date(createdAt).toLocaleString('vi-VN', { day: '2-digit', month: '2-digit', year: 'numeric', hour: '2-digit', minute: '2-digit' }) : '—';
                                                const typeLabel = (item.type || item.Type) === 'WITHDRAW' ? 'Rút tiền' : 'Donate';
                                                const amount = item.amount ?? item.Amount ?? 0;
                                                const note = (item.type || item.Type) === 'DONATE'
                                                    ? (item.senderDisplayName ?? item.SenderDisplayName ? `${item.senderDisplayName ?? item.SenderDisplayName}${item.note ?? item.Note ? ` — ${item.note || item.Note}` : ''}` : (item.note ?? item.Note) || '—')
                                                    : (item.withdrawStatus ?? item.WithdrawStatus) === 'PENDING' ? 'Chờ xử lý' : (item.note ?? item.Note) || (item.withdrawStatus ?? item.WithdrawStatus) || '—';
                                                return (
                                                    <tr key={item.id ?? item.Id} style={{ borderBottom: '1px solid #e5e7eb' }}>
                                                        <td style={{ padding: '1rem 1.25rem', color: '#374151' }}>{timeStr}</td>
                                                        <td style={{ padding: '1rem 1.25rem', color: '#374151' }}>{typeLabel}</td>
                                                        <td style={{ padding: '1rem 1.25rem', textAlign: 'right', fontWeight: 600, color: (item.type || item.Type) === 'WITHDRAW' ? '#dc2626' : '#15803d' }}>
                                                            {(item.type || item.Type) === 'WITHDRAW' ? '-' : '+'}{Number(amount).toLocaleString()} coin
                                                        </td>
                                                        <td style={{ padding: '1rem 1.25rem', color: '#64748b', maxWidth: '280px', overflow: 'hidden', textOverflow: 'ellipsis', whiteSpace: 'nowrap' }}>{note}</td>
                                                    </tr>
                                                );
                                            })
                                        )}
                                    </tbody>
                                </table>
                            </div>
                        </div>
                    ) : activeView === 'profile' ? (
                        <div style={{ maxWidth: '900px' }}>
                            {/* Thành tích */}
                            <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '1.5rem', marginBottom: '1.5rem', border: '1px solid #e0e0e0' }}>
                                <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem', marginBottom: '1.5rem' }}>
                                    <div style={{ width: '20px', height: '20px', color: '#6b7280' }}>🌱</div>
                                    <h3 style={{ fontSize: '1rem', fontWeight: 'bold', color: '#333333', margin: 0 }}>Thành tích</h3>
                                </div>

                                <div style={{ display: 'grid', gridTemplateColumns: 'repeat(4, 1fr)', gap: '1rem' }}>
                                    <div style={{ textAlign: 'center' }}>
                                        <div style={{ width: '48px', height: '48px', borderRadius: '50%', backgroundColor: '#d4fce3', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 0.75rem' }}>
                                            <Book style={{ width: '24px', height: '24px', color: '#13ec5b' }} />
                                        </div>
                                        <div style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#333333', marginBottom: '0.25rem' }}>
                                            {userStats.published}
                                        </div>
                                        <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                                            Truyện đã đăng
                                        </div>
                                    </div>

                                    <div style={{ textAlign: 'center' }}>
                                        <div style={{ width: '48px', height: '48px', borderRadius: '50%', backgroundColor: '#d4fce3', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 0.75rem' }}>
                                            <div style={{ fontSize: '1.25rem' }}>📄</div>
                                        </div>
                                        <div style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#333333', marginBottom: '0.25rem' }}>
                                            {userStats.totalChapters}
                                        </div>
                                        <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                                            Chương đã đăng
                                        </div>
                                    </div>

                                    <div style={{ textAlign: 'center' }}>
                                        <div style={{ width: '48px', height: '48px', borderRadius: '50%', backgroundColor: '#d4fce3', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 0.75rem' }}>
                                            <Heart style={{ width: '24px', height: '24px', color: '#13ec5b' }} />
                                        </div>
                                        <div style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#333333', marginBottom: '0.25rem' }}>
                                            {userStats.followers}
                                        </div>
                                        <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                                            Người theo dõi
                                        </div>
                                    </div>

                                    <div style={{ textAlign: 'center' }}>
                                        <div style={{ width: '48px', height: '48px', borderRadius: '50%', backgroundColor: '#d4fce3', display: 'flex', alignItems: 'center', justifyContent: 'center', margin: '0 auto 0.75rem' }}>
                                            <Star style={{ width: '24px', height: '24px', color: '#13ec5b' }} />
                                        </div>
                                        <div style={{ fontSize: '1.25rem', fontWeight: 'bold', color: '#333333', marginBottom: '0.25rem' }}>
                                            {userStats.recommendations}
                                        </div>
                                        <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                                            Đề cử
                                        </div>
                                    </div>
                                </div>
                            </div>

                            {/* Thông tin cá nhân */}
                            <div style={{ backgroundColor: '#ffffff', borderRadius: '8px', padding: '1.5rem', border: '1px solid #e0e0e0' }}>
                                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
                                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.5rem' }}>
                                        <div style={{ width: '20px', height: '20px', color: '#6b7280' }}>👤</div>
                                        <h3 style={{ fontSize: '1rem', fontWeight: 'bold', color: '#333333', margin: 0 }}>Thông tin cá nhân</h3>
                                    </div>
                                    <button
                                        style={{
                                            padding: '0.5rem 1.25rem',
                                            backgroundColor: '#13ec5b',
                                            border: 'none',
                                            borderRadius: '9999px',
                                            fontSize: '0.875rem',
                                            fontWeight: 600,
                                            color: '#ffffff',
                                            cursor: 'pointer'
                                        }}
                                    >
                                        CẬP NHẬT
                                    </button>
                                </div>

                                <div style={{ display: 'grid', gap: '1rem' }}>
                                    <div style={{ display: 'grid', gridTemplateColumns: '120px 1fr', gap: '1rem', alignItems: 'center' }}>
                                        <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>Tên hiển thị</div>
                                        <div style={{ fontSize: '0.875rem', color: '#333333', fontWeight: 500 }}>{userDisplayName}</div>
                                    </div>
                                    <div style={{ display: 'grid', gridTemplateColumns: '120px 1fr', gap: '1rem', alignItems: 'center' }}>
                                        <div style={{ fontSize: '0.875rem', color: '#6b7280' }}>Giới thiệu</div>
                                        <div style={{ fontSize: '0.875rem', color: '#333333' }}>Đang cập nhật</div>
                                    </div>
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
                                    </div>
                                </div>
                                <button
                                    onClick={handleCreateStory}
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
                                        cursor: 'pointer',
                                        transition: 'all 0.2s',
                                        boxShadow: '0 2px 8px rgba(19, 236, 91, 0.3)',
                                        fontFamily: "'Plus Jakarta Sans', sans-serif"
                                    }}
                                    onMouseEnter={(e) => {
                                        e.currentTarget.style.backgroundColor = '#10d452';
                                        e.currentTarget.style.transform = 'translateY(-1px)';
                                        e.currentTarget.style.boxShadow = '0 4px 12px rgba(19, 236, 91, 0.35)';
                                    }}
                                    onMouseLeave={(e) => {
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
                                    <p style={{ fontSize: '0.875rem', color: '#9ca3af', marginBottom: '1.5rem' }}>
                                        Bắt đầu sáng tác truyện đầu tiên của bạn
                                    </p>
                                    <button
                                        onClick={handleCreateStory}
                                        style={{
                                            padding: '0.75rem 1.5rem',
                                            backgroundColor: '#13ec5b',
                                            border: 'none',
                                            borderRadius: '9999px',
                                            fontSize: '0.875rem',
                                            fontWeight: 700,
                                            color: '#ffffff',
                                            cursor: 'pointer'
                                        }}
                                    >
                                        Tạo truyện mới
                                    </button>
                                </div>
                            ) : (
                                <>
                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                                        {stories.map((story) => (
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
                                                        </div>
                                                        <div style={{
                                                            padding: '0.25rem 0.75rem',
                                                            backgroundColor: ['published', 'completed'].includes(story.status) ? '#d1fae5' : '#fef3c7',
                                                            borderRadius: '4px',
                                                            fontSize: '0.75rem',
                                                            color: ['published', 'completed'].includes(story.status) ? '#065f46' : '#92400e',
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
                                                                0
                                                            </div>
                                                        </div>
                                                    </div>

                                                    {/* Status */}
                                                    <div style={{ display: 'flex', alignItems: 'center', gap: '0.75rem' }}>
                                                        <div style={{ fontSize: '0.75rem', color: '#6b7280' }}>
                                                            Trạng thái xuất bản
                                                        </div>
                                                        <div style={{
                                                            padding: '0.25rem 0.75rem',
                                                            backgroundColor: (story.status === 'published' || story.status === 'completed') ? '#d1fae5' : '#fef3c7',
                                                            borderRadius: '4px',
                                                            fontSize: '0.75rem',
                                                            color: (story.status === 'published' || story.status === 'completed') ? '#065f46' : '#92400e'
                                                        }}>
                                                            {story.publishStatus}
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
                                                        <Edit style={{ width: '14px', height: '14px' }} />
                                                        Chỉnh sửa
                                                    </button>
                                                    <button
                                                        onClick={() => handleViewComments(story)}
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
                                                        <MessageSquare style={{ width: '14px', height: '14px' }} />
                                                        Bình luận
                                                    </button>
                                                    <button
                                                        onClick={() => story.status === 'draft' && handleDeleteStory(story.id)}
                                                        disabled={story.status !== 'draft'}
                                                        title={story.status === 'draft' ? 'Xóa truyện' : 'Chỉ được xóa truyện khi ở trạng thái Bản nháp'}
                                                        style={{
                                                            display: 'flex',
                                                            alignItems: 'center',
                                                            justifyContent: 'center',
                                                            gap: '0.375rem',
                                                            padding: '0.5rem 1rem',
                                                            backgroundColor: story.status === 'draft' ? '#fff' : '#f1f5f9',
                                                            border: `1px solid ${story.status === 'draft' ? '#fecaca' : '#e2e8f0'}`,
                                                            borderRadius: '9999px',
                                                            fontSize: '0.8125rem',
                                                            fontWeight: 500,
                                                            color: story.status === 'draft' ? '#dc2626' : '#94a3b8',
                                                            cursor: story.status === 'draft' ? 'pointer' : 'not-allowed',
                                                            whiteSpace: 'nowrap',
                                                            transition: 'all 0.2s',
                                                            opacity: story.status === 'draft' ? 1 : 0.8
                                                        }}
                                                        onMouseEnter={(e) => {
                                                            if (story.status === 'draft') {
                                                                e.currentTarget.style.backgroundColor = '#fef2f2';
                                                                e.currentTarget.style.borderColor = '#ef4444';
                                                            }
                                                        }}
                                                        onMouseLeave={(e) => {
                                                            e.currentTarget.style.backgroundColor = story.status === 'draft' ? '#fff' : '#f1f5f9';
                                                            e.currentTarget.style.borderColor = story.status === 'draft' ? '#fecaca' : '#e2e8f0';
                                                        }}
                                                    >
                                                        <Trash2 style={{ width: '14px', height: '14px' }} />
                                                        Xóa
                                                    </button>
                                                </div>
                                            </div>
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
        </div>
    );
}

export default AuthorStoryManagement;