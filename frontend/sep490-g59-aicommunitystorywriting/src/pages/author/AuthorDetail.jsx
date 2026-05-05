import { useEffect, useState, useCallback } from 'react';
import { useParams, Link, useNavigate } from 'react-router-dom';
import { UserPlus, UserMinus, Gift, BookOpen, Quote, Eye, Star, CheckCircle } from 'lucide-react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { getProfileByUserId } from '../../api/account/accountApi';
import { getStoriesByAuthor } from '../../api/story/storyApi';
import { getAuthorFollowing, getAuthorFollowersCount, followAuthor, unfollowAuthor } from '../../api/author/authorApi';
import { useAuth } from '../../contexts/AuthContext';
import { resolveBackendUrl } from '../../utils/resolveBackendUrl';

const exploreCtaLogoUrl = `${import.meta.env.BASE_URL}logo.png`;

function formatJoinDate(dateStr) {
    if (!dateStr) return '';
    const d = new Date(dateStr);
    if (Number.isNaN(d.getTime())) return '';
    return d.toLocaleDateString('vi-VN', {
        year: 'numeric',
        month: 'long',
        day: 'numeric',
    });
}

export function AuthorDetail() {
    const { authorId } = useParams();
    const navigate = useNavigate();
    const { user, isAuthenticated } = useAuth();
    const [profile, setProfile] = useState(null);
    const [stories, setStories] = useState([]);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState(null);
    const [isFollowing, setIsFollowing] = useState(false);
    const [followLoading, setFollowLoading] = useState(false);
    const [followError, setFollowError] = useState(null);
    const [followerCount, setFollowerCount] = useState(0);
    const [recommendationCount, setRecommendationCount] = useState(0);

    const loadData = useCallback(async () => {
        if (!authorId) {
            setError('Thiếu thông tin tác giả.');
            setLoading(false);
            return;
        }
        setLoading(true);
        setError(null);
        try {
            const [p, s, followers] = await Promise.all([
                getProfileByUserId(authorId),
                getStoriesByAuthor(authorId, { pageSize: 20, sortBy: 'createdAt', sortOrder: 'DESC' }),
                getAuthorFollowersCount(authorId).catch(() => null),
            ]);
            const items = Array.isArray(s)
                ? s
                : (s?.items ?? s?.Items ?? []);
            setProfile(p);
            setStories(items);
            if (typeof followers === 'number') setFollowerCount(Math.max(0, followers));
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? 'Không tải được thông tin tác giả.';
            setError(msg);
        } finally {
            setLoading(false);
        }
    }, [authorId]);

    useEffect(() => {
        loadData();
    }, [loadData]);

    useEffect(() => {
        if (!authorId || !user?.id) {
            if (!user?.id) setIsFollowing(false);
            return;
        }
        getAuthorFollowing(authorId)
            .then((data) => setIsFollowing(!!(data?.following ?? data?.Following)))
            .catch(() => setIsFollowing(false));
    }, [authorId, user?.id]);

    const goToLogin = useCallback(() => {
        const from = authorId ? `/authors/${authorId}` : '/home';
        navigate('/login', { state: { from } });
    }, [authorId, navigate]);

    const handleFollowToggle = useCallback(async () => {
        if (!authorId || followLoading) return;
        if (!isAuthenticated) {
            goToLogin();
            return;
        }
        setFollowLoading(true);
        setFollowError(null);
        try {
            if (isFollowing) {
                await unfollowAuthor(authorId);
                setIsFollowing(false);
                setRecommendationCount((c) => Math.max(0, Number(c) - 1));
            } else {
                await followAuthor(authorId);
                setIsFollowing(true);
                setRecommendationCount((c) => Math.max(0, Number(c) + 1));
            }
            try {
                const fc = await getAuthorFollowersCount(authorId);
                setFollowerCount(Math.max(0, fc));
            } catch {
                /* giữ số cũ nếu API lỗi */
            }
            const p = await getProfileByUserId(authorId);
            setProfile(p);
        } catch (err) {
            const msg = err?.response?.data?.message ?? err?.message ?? (isFollowing ? 'Không bỏ theo dõi được.' : 'Không theo dõi được.');
            setFollowError(msg);
        } finally {
            setFollowLoading(false);
        }
    }, [authorId, isFollowing, followLoading, isAuthenticated, goToLogin]);

    useEffect(() => {
        if (!profile?.stats) return;
        // Số người theo dõi lấy từ GET /api/authors/{id}/followers-count (loadData + sau khi follow).
        if (typeof profile.stats.recommendations === 'number') setRecommendationCount(profile.stats.recommendations);
    }, [profile]);

    useEffect(() => {
        if (!profile?.stats) return;
        if (typeof profile.stats.recommendations !== 'number') setRecommendationCount(isFollowing ? 1 : 0);
    }, [isFollowing, profile]);

    const displayName = profile?.displayName ?? 'Tác giả';
    const handleDonateClick = useCallback(() => {
        if (!authorId) return;
        if (!isAuthenticated) {
            goToLogin();
            return;
        }
        navigate(`/donate/${authorId}?name=${encodeURIComponent(displayName)}`);
    }, [authorId, isAuthenticated, goToLogin, navigate, displayName]);
    const avatarUrl = profile?.avatarUrl ? resolveBackendUrl(profile.avatarUrl) : '';
    const joinDate = formatJoinDate(profile?.joinDate);
    const totalReads = profile?.stats?.totalReads ?? 0;
    const storiesWritten = profile?.stats?.storiesWritten ?? stories.length ?? 0;
    const likes = profile?.stats?.likes ?? 0;
    // Recommendations: từ profile.stats hoặc fallback theo trạng thái follow; followers: từ API followers-count.

    return (
        <div className="min-h-screen bg-slate-50">
            <Header />

            <main className="max-w-[1280px] mx-auto px-4 pt-10 pb-12">
                {loading && (
                    <div className="py-12 text-center text-slate-500 text-sm">
                        Đang tải trang tác giả...
                    </div>
                )}
                {!loading && error && (
                    <div className="py-12 text-center text-red-500 text-sm">
                        {error}
                    </div>
                )}
                {!loading && !error && (
                    <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
                        {/* Hồ sơ tác giả (giống trang /author) */}
                        <section className="lg:col-span-4 space-y-6">
                            {/* Header thông tin + Thành tích */}
                            <div className="bg-white rounded-2xl shadow-sm border border-slate-200 mb-2 overflow-hidden">
                                <div className="h-2 bg-gradient-to-r from-emerald-500 via-teal-500 to-primary" />

                                <div className="p-6">
                                    <div className="flex items-start gap-4 mb-6">
                                        <div className="relative w-16 h-16 rounded-full bg-slate-100 flex items-center justify-center overflow-hidden">
                                            <div className="absolute -inset-1 rounded-full bg-gradient-to-r from-emerald-400 to-primary opacity-20 blur-sm" />
                                            <div className="relative w-full h-full rounded-full flex items-center justify-center text-2xl font-bold text-primary bg-white/40">
                                                {avatarUrl ? (
                                                    <img
                                                        src={avatarUrl}
                                                        alt={displayName}
                                                        className="w-full h-full object-cover"
                                                    />
                                                ) : (
                                                    displayName.charAt(0).toUpperCase()
                                                )}
                                            </div>
                                        </div>

                                        <div className="flex-1">
                                            <h1 className="text-xl font-extrabold text-slate-900 leading-tight">
                                                {displayName}
                                            </h1>
                                            {joinDate && (
                                                <p className="text-xs text-slate-500 mt-1">
                                                    Tham gia từ {joinDate}
                                                </p>
                                            )}

                                            <div className="flex flex-wrap gap-2 mt-3">
                                                <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-emerald-500/10 text-emerald-700 border border-emerald-500/20">
                                                    <span aria-hidden>🤝</span>
                                                    <span>
                                                        {followerCount.toLocaleString()} người theo dõi
                                                    </span>
                                                </span>
                                                {profile?.isVerified && (
                                                    <span className="inline-flex items-center gap-2 px-3 py-1 rounded-full text-xs font-semibold bg-sky-500/10 text-sky-700 border border-sky-500/20">
                                                        <CheckCircle className="w-4 h-4" />
                                                        Đã xác thực
                                                    </span>
                                                )}
                                            </div>
                                        </div>
                                    </div>

                                    {/* Nút Follow / Unfollow & Donate */}
                                    <div className="flex flex-wrap gap-3 items-center">
                                        <button
                                            type="button"
                                            onClick={handleFollowToggle}
                                            disabled={followLoading}
                                            className={`flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-semibold transition-all disabled:opacity-70 border ${
                                                isFollowing
                                                    ? 'bg-white text-slate-700 border-slate-200 hover:border-slate-300 hover:bg-slate-50'
                                                    : 'bg-primary/95 text-white border-primary hover:bg-primary shadow-md'
                                            }`}
                                        >
                                            {followLoading ? (
                                                'Đang xử lý...'
                                            ) : isFollowing ? (
                                                <>
                                                    <UserMinus className="w-4 h-4" />
                                                    Bỏ theo dõi
                                                </>
                                            ) : (
                                                <>
                                                    <UserPlus className="w-4 h-4" />
                                                    Theo dõi
                                                </>
                                            )}
                                        </button>

                                        <button
                                            type="button"
                                            onClick={handleDonateClick}
                                            className="flex items-center gap-2 px-4 py-2.5 rounded-xl text-sm font-semibold bg-amber-500 text-white hover:bg-amber-600 shadow-md transition-all border border-amber-500/10"
                                        >
                                            <Gift className="w-4 h-4" />
                                            Ủng hộ
                                        </button>
                                    </div>

                                    {followError && (
                                        <p className="mt-3 text-sm text-red-600">{followError}</p>
                                    )}

                                    {/* Thành tích */}
                                    <div className="mt-6">
                                        <div className="flex items-center gap-2 mb-4">
                                            <span className="text-lg">🌱</span>
                                            <h2 className="text-lg font-bold text-slate-900">Thành tích</h2>
                                        </div>

                                        <div className="grid grid-cols-2 gap-3">
                                            <div className="p-3 rounded-xl border border-slate-200 bg-gradient-to-b from-emerald-50/60 to-white hover:border-emerald-200 transition-colors">
                                                <div className="w-11 h-11 rounded-xl bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center mx-auto mb-2">
                                                    <span className="text-2xl">📚</span>
                                                </div>
                                                <div className="text-xl font-extrabold text-slate-900 text-center">
                                                    {storiesWritten.toLocaleString()}
                                                </div>
                                                <div className="text-xs text-slate-500 mt-1 text-center">
                                                    Truyện đã đăng
                                                </div>
                                            </div>

                                            <div className="p-3 rounded-xl border border-slate-200 bg-gradient-to-b from-emerald-50/60 to-white hover:border-emerald-200 transition-colors">
                                                <div className="w-11 h-11 rounded-xl bg-emerald-500/10 border border-emerald-500/20 flex items-center justify-center mx-auto mb-2">
                                                    <span className="text-2xl">👀</span>
                                                </div>
                                                <div className="text-xl font-extrabold text-slate-900 text-center">
                                                    {totalReads.toLocaleString()}
                                                </div>
                                                <div className="text-xs text-slate-500 mt-1 text-center">
                                                    Lượt đọc
                                                </div>
                                            </div>

                                            <div className="p-3 rounded-xl border border-slate-200 bg-gradient-to-b from-teal-50/60 to-white hover:border-teal-200 transition-colors">
                                                <div className="w-11 h-11 rounded-xl bg-teal-500/10 border border-teal-500/20 flex items-center justify-center mx-auto mb-2">
                                                    <span className="text-2xl">🤝</span>
                                                </div>
                                                <div className="text-xl font-extrabold text-slate-900 text-center">
                                                    {followerCount.toLocaleString()}
                                                </div>
                                                <div className="text-xs text-slate-500 mt-1 text-center">
                                                    Người theo dõi
                                                </div>
                                            </div>

                                            <div className="p-3 rounded-xl border border-slate-200 bg-gradient-to-b from-amber-50/70 to-white hover:border-amber-200 transition-colors">
                                                <div className="w-11 h-11 rounded-xl bg-amber-500/10 border border-amber-500/20 flex items-center justify-center mx-auto mb-2">
                                                    <span className="text-2xl">⭐</span>
                                                </div>
                                                <div className="text-xl font-extrabold text-slate-900 text-center">
                                                    {recommendationCount.toLocaleString()}
                                                </div>
                                                <div className="text-xs text-slate-500 mt-1 text-center">
                                                    Đề cử
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                </div>
                            </div>

                            {/* Thông tin cá nhân */}
                            <div className="bg-white rounded-xl shadow-sm border border-slate-200 p-6">
                                <div className="flex items-center justify-between mb-4">
                                    <div className="flex items-center gap-2">
                                        <span className="text-lg">👤</span>
                                        <h2 className="text-lg font-bold text-slate-900">Thông tin cá nhân</h2>
                                    </div>
                                </div>
                                <div className="space-y-4 text-sm">
                                    <div className="grid grid-cols-[110px,1fr] gap-3 items-center">
                                        <div className="text-slate-500">Tên hiển thị</div>
                                        <div className="font-medium text-slate-900">
                                            {displayName}
                                        </div>
                                    </div>
                                    <div className="grid grid-cols-[110px,1fr] gap-3">
                                        <div className="text-slate-500">Giới thiệu</div>
                                        <div className="text-slate-700">
                                            {profile?.bio || profile?.description || 'Đang cập nhật'}
                                        </div>
                                    </div>
                                </div>
                            </div>
                        </section>

                        {/* Stories list / right column */}
                        <section className="lg:col-span-8 space-y-6">
                            {/* Banner / Hero cho khu vực tác giả */}
                            <div className="relative rounded-xl overflow-hidden bg-gradient-to-br from-slate-900 via-emerald-700 to-primary border border-slate-800/60 p-6 md:p-8 text-white shadow-lg">
                                <div className="absolute inset-0 bg-[url('data:image/svg+xml,%3Csvg width=\'60\' height=\'60\' viewBox=\'0 0 60 60\' xmlns=\'http://www.w3.org/2000/svg\'%3E%3Cg fill=\'none\' fill-rule=\'evenodd\'%3E%3Cg fill=\'%2310b981\' fill-opacity=\'0.06\'%3E%3Cpath d=\'M36 34v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zm0-30V0h-2v4h-4v2h4v4h2V6h4V4h-4zM6 34v-4H4v4H0v2h4v4h2v-4h4v-2H6zM6 4V0H4v4H0v2h4v4h2V6h4V4H6z\'/%3E%3C/g%3E%3C/g%3E%3C/svg%3E')] opacity-80" aria-hidden="true" />
                                <div className="relative flex flex-col md:flex-row md:items-center gap-5">
                                    <div className="flex items-center gap-3">
                                        <div className="w-12 h-12 rounded-full bg-white/10 flex items-center justify-center backdrop-blur-sm">
                                            <BookOpen className="w-7 h-7 text-emerald-300" />
                                        </div>
                                        <div>
                                            <p className="text-xs uppercase tracking-[0.2em] text-emerald-200/80 mb-1">
                                                Hồ sơ tác giả nổi bật
                                            </p>
                                            <h3 className="text-xl md:text-2xl font-extrabold">
                                                Thế giới truyện của <span className="text-emerald-200">{displayName}</span>
                                            </h3>
                                        </div>
                                    </div>
                                    <div className="flex-1" />
                                    <div className="flex flex-wrap gap-3 text-xs">
                                        <div className="px-3 py-1.5 rounded-full bg-white/10 border border-white/15 flex items-center gap-2">
                                            <span className="text-emerald-200 font-semibold">
                                                {stories.length.toLocaleString()}
                                            </span>
                                            <span className="text-emerald-50/80">truyện đã xuất bản</span>
                                        </div>
                                        <div className="px-3 py-1.5 rounded-full bg-white/10 border border-white/15 flex items-center gap-2">
                                            <span className="text-emerald-200 font-semibold">
                                                {totalReads.toLocaleString()}
                                            </span>
                                            <span className="text-emerald-50/80">lượt đọc tích lũy</span>
                                        </div>
                                        <div className="px-3 py-1.5 rounded-full bg-white/10 border border-white/15 flex items-center gap-2">
                                            <span className="text-emerald-200 font-semibold">
                                                {followerCount.toLocaleString()}
                                            </span>
                                            <span className="text-emerald-50/80">người theo dõi</span>
                                        </div>
                                    </div>
                                </div>
                                <p className="relative mt-4 text-xs md:text-sm text-emerald-50/80 max-w-2xl">
                                    {stories.length > 0
                                        ? 'Mỗi câu chuyện là một mảnh ghép trong vũ trụ sáng tạo riêng của tác giả. Lướt xuống để khám phá những tác phẩm được độc giả yêu thích nhất.'
                                        : 'Tác giả đang chuẩn bị những chương truyện đầu tiên. Hãy theo dõi để không bỏ lỡ khi tác phẩm chính thức ra mắt.'}
                                </p>
                            </div>

                            {/* Thẻ giới thiệu / quote */}
                            {(profile?.bio || profile?.description) && (profile.bio || profile.description) !== 'Đang cập nhật' && (
                                <div className="bg-white rounded-xl border border-slate-200 p-5 shadow-sm">
                                    <div className="flex gap-3">
                                        <Quote className="w-8 h-8 text-primary/50 shrink-0 mt-0.5" />
                                        <div>
                                            <p className="text-sm font-medium text-slate-600 italic">
                                                &ldquo;{profile?.bio || profile?.description}&rdquo;
                                            </p>
                                            <p className="text-xs text-slate-500 mt-2">— {displayName}</p>
                                        </div>
                                    </div>
                                </div>
                            )}

                            <div className="flex items-center justify-between mb-4">
                                <h2 className="text-lg md:text-xl font-bold text-slate-900">
                                    Truyện của {displayName}
                                </h2>
                                <span className="text-xs md:text-sm text-slate-500">
                                    {stories.length.toLocaleString()} truyện
                                </span>
                            </div>

                            {stories.length === 0 ? (
                                <div className="bg-white rounded-xl border border-dashed border-slate-300 py-10 px-4 text-center text-sm text-slate-500">
                                    Tác giả chưa có truyện nào được xuất bản.
                                </div>
                            ) : (
                                <div className="bg-white rounded-xl border border-slate-200 shadow-sm divide-y divide-slate-200">
                                    {stories.map((s, index) => {
                                        const id = s.id ?? s.Id;
                                        const title = s.title ?? s.Title ?? 'Không có tiêu đề';
                                        const coverPath = s.coverImage ?? s.CoverImage;
                                        const cover = coverPath ? resolveBackendUrl(coverPath) : '';
                                        const summary = s.summary ?? s.Summary ?? '';
                                        const categories = (s.categoryNames ?? s.CategoryNames ?? '')
                                            .split(',')
                                            .map((x) => x.trim())
                                            .filter(Boolean);
                                        const views = Number(s.totalViews ?? s.TotalViews ?? 0) || 0;
                                        const rating = Number(s.avgRating ?? s.AvgRating ?? 0) || 0;
                                        const totalRatings = Number(s.totalRatings ?? s.TotalRatings ?? 0) || 0;
                                        const mainCategory = categories[0] || 'bảng xếp hạng';
                                        return (
                                            <Link
                                                key={id}
                                                to={id ? `/story/${id}` : '#'}
                                                className="flex gap-4 p-4 md:p-5 hover:bg-slate-50 transition-colors"
                                            >
                                                {cover ? (
                                                    <img
                                                        src={cover}
                                                        alt={title}
                                                        className="w-20 h-28 md:w-24 md:h-32 object-cover rounded-md flex-shrink-0"
                                                    />
                                                ) : (
                                                    <div className="w-20 h-28 md:w-24 md:h-32 bg-slate-200 flex items-center justify-center text-slate-500 text-[11px] rounded-md flex-shrink-0">
                                                        Không có ảnh
                                                    </div>
                                                )}
                                                <div className="flex-1 flex flex-col gap-2">
                                                    <div>
                                                        <h3 className="text-sm md:text-base font-semibold text-slate-900 line-clamp-2">
                                                            {title}
                                                        </h3>
                                                        {categories.length > 0 && (
                                                            <div className="flex flex-wrap items-center gap-2 mt-1 text-[11px] md:text-xs text-slate-500">
                                                                {categories.map((c) => (
                                                                    <span
                                                                        key={c}
                                                                        className="px-2 py-0.5 rounded-full bg-slate-100 text-slate-600"
                                                                    >
                                                                        {c}
                                                                    </span>
                                                                ))}
                                                            </div>
                                                        )}
                                                    </div>
                                                    {summary && (
                                                        <p className="text-xs md:text-sm text-slate-600 line-clamp-2">
                                                            {summary}
                                                        </p>
                                                    )}
                                                    <div className="flex flex-wrap items-center gap-4 text-[11px] md:text-xs text-slate-500">
                                                        <span className="inline-flex items-center gap-1">
                                                            <Eye className="w-3.5 h-3.5" />
                                                            {views.toLocaleString()} lượt đọc
                                                        </span>
                                                        {rating > 0 && (
                                                            <span className="inline-flex items-center gap-1">
                                                                <Star className="w-3.5 h-3.5 text-amber-400" />
                                                                {rating.toFixed(1)} / 5 ({totalRatings.toLocaleString()} đánh giá)
                                                            </span>
                                                        )}
                                                    </div>
                                                    <div className="pt-2 text-[11px] md:text-xs text-slate-500">
                                                        <span>
                                                            #{index + 1} trong {mainCategory}
                                                        </span>
                                                    </div>
                                                </div>
                                            </Link>
                                        );
                                    })}
                                </div>
                            )}

                            {/* Thẻ gợi ý / làm đầy trang */}
                            <div className="bg-white rounded-xl border border-slate-200 overflow-hidden shadow-sm">
                                <div className="flex flex-col sm:flex-row">
                                    <div className="w-full sm:w-52 shrink-0 overflow-hidden bg-white flex items-center justify-center p-4">
                                        <img
                                            src={exploreCtaLogoUrl}
                                            alt=""
                                            className="w-full max-h-32 sm:max-h-none sm:h-full sm:min-h-[9rem] object-contain"
                                        />
                                    </div>
                                    <div className="p-5 flex-1">
                                        <h3 className="font-semibold text-slate-800 mb-1">Khám phá thêm truyện</h3>
                                        <p className="text-sm text-slate-600 mb-4">
                                            Xem nhiều thể loại và tác giả khác trên nền tảng.
                                        </p>
                                        <Link
                                            to="/story-list"
                                            className="inline-flex items-center gap-2 text-sm font-medium text-primary hover:underline"
                                        >
                                            Đi tới Khám phá
                                            <span aria-hidden>→</span>
                                        </Link>
                                    </div>
                                </div>
                            </div>
                        </section>
                    </div>
                )}
            </main>

            <Footer />
        </div>
    );
}

