import { useCallback, useEffect, useRef, useState } from 'react';
import { Coins, AlertCircle } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import * as coinApi from '../../api/coins/coinApi';

export default function RechargeCoin() {
    const { user, role } = useAuth();
    const normalizedRole = (role ?? '').toString().trim().toUpperCase();
    const isAuthor = normalizedRole === 'AUTHOR' || user?.isAuthor === true;

    const [packages, setPackages] = useState([]);
    const [packagesLoading, setPackagesLoading] = useState(true);
    const [selectedPackageId, setSelectedPackageId] = useState(null);
    const [customAmount, setCustomAmount] = useState('');
    const [status, setStatus] = useState(null); // 'success' | 'error' | null
    const [statusMessage, setStatusMessage] = useState('');
    const [submitting, setSubmitting] = useState(false);
    const [wallet, setWallet] = useState(null);
    const [loading, setLoading] = useState(true);
    const [error, setError] = useState('');
    const autoSyncRef = useRef(false);
    const autoHideTimerRef = useRef(null);

    const loadWallet = useCallback(async () => {
        setLoading(true);
        setError('');
        try {
            const walletRes = await coinApi.getMyWallet();
            if (!walletRes.success) throw new Error(walletRes.message);

            setWallet(walletRes.data ?? null);
        } catch (e) {
            setWallet(null);
            setError(e?.message || 'Không thể tải ví coin');
        } finally {
            setLoading(false);
        }
    }, []);

    const loadPackages = () => {
        setPackagesLoading(true);
        setStatusMessage('');
        coinApi.getCoinPackages()
            .then((r) => {
                if (!r.success) {
                    setPackages([]);
                    if (r.message) setStatusMessage(r.message);
                    return;
                }
                const raw = r.data;
                const list = Array.isArray(raw)
                    ? raw
                    : Array.isArray(raw?.items)
                        ? raw.items
                        : Array.isArray(raw?.data)
                            ? raw.data
                            : [];
                setPackages(list);
            })
            .catch((err) => {
                setPackages([]);
                setStatusMessage(err?.message || 'Không thể kết nối API.');
            })
            .finally(() => setPackagesLoading(false));
    };

    useEffect(() => {
        loadPackages();
    }, []);

    useEffect(() => {
        loadWallet();
    }, [loadWallet]);

    // Khi donate/withdraw hoàn tất, các component khác có thể dispatch `wallet:changed`.
    // RechargeCoin cũng cần refresh lại để hiển thị đúng số dư/tổng coin.
    useEffect(() => {
        const handler = () => loadWallet().catch(() => { });
        window.addEventListener('wallet:changed', handler);
        return () => window.removeEventListener('wallet:changed', handler);
    }, [loadWallet]);

    const displayedCoins = isAuthor
        ? Number(wallet?.balanceCoin ?? wallet?.balance_coin ?? 0) + Number(wallet?.incomeBalance ?? wallet?.income_balance ?? 0)
        : Number(wallet?.balanceCoin ?? wallet?.balance_coin ?? 0);

    useEffect(() => {
        if (!status) return;
        if (autoHideTimerRef.current) clearTimeout(autoHideTimerRef.current);
        autoHideTimerRef.current = setTimeout(() => {
            setStatus(null);
            setStatusMessage('');
        }, 4000);

        return () => {
            if (autoHideTimerRef.current) clearTimeout(autoHideTimerRef.current);
        };
    }, [status]);

    const coinPackages = packages.length > 0
        ? packages.map((p) => {
            const id = p.id ?? p.Id;
            return {
                id: id != null ? String(id) : null,
                coins: Number(p.coinAmount ?? p.CoinAmount ?? 0) || 0,
                price: Number(p.priceAmount ?? p.PriceAmount ?? 0) || 0,
                bonus: Number(p.bonusCoin ?? p.BonusCoin ?? 0) || 0,
                recommended: false,
            };
        })
        : [
            { id: null, coins: 100, price: 100000, bonus: 0, recommended: false },
            { id: null, coins: 500, price: 450000, bonus: 50, recommended: false },
            { id: null, coins: 1000, price: 850000, bonus: 150, recommended: true },
            { id: null, coins: 2000, price: 1600000, bonus: 400, recommended: false },
            { id: null, coins: 5000, price: 3750000, bonus: 1250, recommended: false },
        ];

    // Tỉ giá theo bậc: nạp càng nhiều, giá mỗi coin càng rẻ (VNĐ/coin)
    const RATE_TIERS = [
        { minCoins: 1, maxCoins: 99, ratePerCoin: 1000, label: 'Cơ bản' },
        { minCoins: 100, maxCoins: 499, ratePerCoin: 900, label: 'Tiết kiệm' },
        { minCoins: 500, maxCoins: 999, ratePerCoin: 850, label: 'Ưu đãi' },
        { minCoins: 1000, maxCoins: 1999, ratePerCoin: 800, label: 'Vàng' },
        { minCoins: 2000, maxCoins: 4999, ratePerCoin: 750, label: 'Bạch kim' },
        { minCoins: 5000, maxCoins: Infinity, ratePerCoin: 700, label: 'Kim cương' },
    ];

    const getPriceForCustomCoins = (coins) => {
        const n = Math.floor(Number(coins)) || 0;
        if (n <= 0) return null;
        const tier = RATE_TIERS.find((t) => n >= t.minCoins && n <= t.maxCoins);
        if (!tier) return null;
        return {
            totalVnd: n * tier.ratePerCoin,
            ratePerCoin: tier.ratePerCoin,
            label: tier.label,
            coins: n,
        };
    };

    const EXCHANGE_RATE = 1000; // tỉ giá tham khảo mặc định (1 Coin ≈ 1.000 VNĐ)

    const formatApiDateTime = (value) => {
        if (!value) return '';
        const s = String(value);
        const hasTimezone = /([zZ]|[+-]\d{2}:\d{2})$/.test(s);
        const iso = hasTimezone ? s : `${s}Z`;
        const d = new Date(iso);
        if (Number.isNaN(d.getTime())) return s;
        return d.toLocaleString();
    };

    const resolvePackageId = () => {
        const id = selectedPackageId != null && selectedPackageId !== '' ? String(selectedPackageId) : null;
        if (id) return id;
        const amount = Math.floor(Number(customAmount)) || 0;
        if (amount <= 0) return null;
        const pkg = coinPackages.find((p) => p.id && p.coins === amount);
        return pkg?.id ?? null;
    };

    const isPackageSelected = (pkg) =>
        selectedPackageId != null && pkg.id != null && String(selectedPackageId) === String(pkg.id);

    const syncOrder = useCallback(async (orderId) => {
        setError('');
        try {
            const res = await coinApi.syncMyPayOSOrder(orderId);
            if (!res.success) throw new Error(res.message);
            await loadWallet();
            window.dispatchEvent(new Event('wallet:changed'));
            // No success toast; just refresh silently
            setStatus(null);
            setStatusMessage('');
        } catch (e) {
            setStatus('error');
            setStatusMessage(e?.message || 'Không thể đồng bộ trạng thái giao dịch.');
        }
    }, [loadWallet]);

    // Auto-sync when PayOS redirects back with ?orderId=...&payos=return|cancel
    useEffect(() => {
        if (autoSyncRef.current) return;
        const sp = new URLSearchParams(window.location.search);
        const payos = sp.get('payos');
        const orderId = sp.get('orderId');

        if (!orderId) return;
        if (!['return', 'cancel', 'success'].includes(String(payos || '').toLowerCase())) return;

        autoSyncRef.current = true;
        (async () => {
            await syncOrder(orderId);
            sp.delete('orderId');
            sp.delete('payos');
            const next = `${window.location.pathname}${sp.toString() ? `?${sp.toString()}` : ''}`;
            window.history.replaceState({}, '', next);
        })();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const handleRecharge = async () => {
        setStatus(null);
        const packageId = resolvePackageId();
        if (!packageId) {
            setStatus('error');
            setStatusMessage('Vui lòng chọn một gói coin bên trên hoặc nhập đúng số coin của gói.');
            return;
        }

        setSubmitting(true);
        const origin = window.location.origin;
        const returnUrl = `${origin}/wallet?payos=return`;
        const cancelUrl = `${origin}/wallet?payos=cancel`;

        const result = await coinApi.createPayOSPayment({ packageId, returnUrl, cancelUrl });
        setSubmitting(false);

        if (result.success && result.data?.checkoutUrl) {
            window.location.href = result.data.checkoutUrl;
            return;
        }
        setStatus('error');
        setStatusMessage(result.message || 'Không tạo được link thanh toán. Vui lòng thử lại.');
    };

    return (
        <div className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-8 border border-slate-200 dark:border-slate-700">
            <div className="flex items-center justify-between mb-2">
                <div>
                    <h3 className="text-xl font-bold text-slate-900 dark:text-white">
                        Nạp Coin
                    </h3>
                    <p className="text-sm text-slate-500 dark:text-slate-400 mt-1">
                        Chọn gói coin hoặc nhập số lượng tùy chỉnh để nạp vào ví.
                    </p>
                </div>
                <div className="hidden sm:flex flex-col items-end text-right text-xs text-slate-500 dark:text-slate-400">
                    <span className="font-semibold">Tỷ giá tham khảo</span>
                    <span>1 Coin ≈ {EXCHANGE_RATE.toLocaleString('vi-VN')} VNĐ</span>
                </div>
            </div>

            {/* Status message (errors only) */}
            {status === 'error' && (
                <div
                    className="mt-4 mb-2 rounded-lg px-4 py-3 text-sm flex items-start gap-2 bg-red-50 text-red-700 border border-red-200 dark:bg-red-900/20 dark:text-red-300 dark:border-red-900/60"
                >
                    <AlertCircle className="w-4 h-4 mt-0.5 flex-shrink-0" />
                    <p className="flex-1">{statusMessage}</p>
                    <button
                        type="button"
                        onClick={() => {
                            setStatus(null);
                            setStatusMessage('');
                        }}
                        className="ml-2 text-slate-500 hover:text-slate-700 dark:text-slate-300 dark:hover:text-white font-bold"
                        aria-label="Đóng thông báo"
                        title="Đóng"
                    >
                        ×
                    </button>
                </div>
            )}

            <div className="space-y-6">
                {error && (
                    <div className="p-4 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 rounded-lg text-sm text-red-700 dark:text-red-300">
                        {error}
                    </div>
                )}

                <div className="p-4 bg-amber-50 dark:bg-amber-950/20 border border-amber-200 dark:border-amber-800 rounded-lg flex items-center justify-between">
                    <div className="text-sm font-semibold text-amber-800 dark:text-amber-200">
                        {isAuthor ? 'Tổng coin' : 'Số dư coin'}
                    </div>
                    <div className="text-lg font-bold text-amber-800 dark:text-amber-200">
                        {Number(displayedCoins || 0).toLocaleString()}
                    </div>
                </div>

                {/* Coin Packages */}
                <div>
                    <label className="block text-sm font-semibold text-slate-700 dark:text-slate-300 mb-4">
                        Chọn gói coin
                    </label>
                    {packagesLoading && (
                        <p className="text-sm text-slate-500 dark:text-slate-400 mb-2">Đang tải gói coin...</p>
                    )}
                    {!packagesLoading && packages.length === 0 && (
                        <div className="mb-3 p-3 rounded-lg bg-amber-50 dark:bg-amber-950/20 border border-amber-200 dark:border-amber-800 flex flex-wrap items-start gap-2">
                            <div className="flex-1 min-w-0">
                                <span className="text-sm text-amber-800 dark:text-amber-200">Chưa tải được gói coin từ hệ thống.</span>
                                {statusMessage && <p className="text-xs text-amber-700 dark:text-amber-300 mt-1 break-words">{statusMessage}</p>}
                            </div>
                            <button type="button" onClick={loadPackages} className="text-sm font-semibold text-primary hover:underline shrink-0">
                                Thử lại
                            </button>
                        </div>
                    )}
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                        {coinPackages.map((pkg, index) => {
                            const hasValidId = pkg.id != null && pkg.id !== '';
                            const selected = isPackageSelected(pkg);
                            return (
                            <button
                                key={pkg.id ?? `fallback-${index}`}
                                type="button"
                                onClick={() => {
                                    if (hasValidId) {
                                        setSelectedPackageId(pkg.id);
                                        setCustomAmount('');
                                    }
                                }}
                                disabled={packages.length > 0 && !hasValidId}
                                className={`relative p-4 border-2 rounded-lg text-left transition-all ${
                                    selected
                                        ? 'border-primary bg-primary/10 ring-2 ring-primary/30'
                                        : 'border-slate-200 dark:border-slate-600 hover:border-primary/50'
                                } ${packages.length > 0 && !hasValidId ? 'opacity-60 cursor-not-allowed' : 'cursor-pointer'}`}
                            >
                                {pkg.recommended && (
                                    <span className="absolute -top-3 right-3 inline-flex items-center rounded-full bg-primary text-white text-[11px] font-semibold px-2 py-0.5 shadow-sm">
                                        Giá tốt nhất
                                    </span>
                                )}
                                <div className="flex items-center justify-between mb-2">
                                    <div className="flex items-center gap-2">
                                        <Coins className="w-5 h-5 text-amber-500" />
                                        <span className="font-bold text-slate-900 dark:text-white">
                                            {pkg.coins.toLocaleString()} Coins
                                        </span>
                                    </div>
                                    {pkg.bonus > 0 && (
                                        <span className="text-xs bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400 px-2 py-1 rounded">
                                            +{pkg.bonus}
                                        </span>
                                    )}
                                </div>
                                <div className="text-sm text-slate-600 dark:text-slate-400">
                                    {pkg.price.toLocaleString('vi-VN')} VNĐ
                                </div>
                            </button>
                            );
                        })}
                    </div>
                </div>

                {/* Custom Amount */}
                <div>
                    <label className="block text-sm font-semibold text-slate-700 dark:text-slate-300 mb-2">
                        Hoặc nhập số coin tùy chỉnh
                    </label>
                    <div className="relative">
                        <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                            <Coins className="w-5 h-5 text-slate-400" />
                        </div>
                        <input
                            type="number"
                            value={customAmount}
                            onChange={(e) => {
                                const v = e.target.value;
                                setCustomAmount(v);
                                const amount = Math.floor(Number(v)) || 0;
                                const pkg = coinPackages.find((p) => p.id && p.coins === amount);
                                setSelectedPackageId(pkg?.id ?? null);
                            }}
                            placeholder="Nhập số coin"
                            className="block w-full pl-10 pr-4 py-3 bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-900 dark:text-white focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all outline-none"
                            min="1"
                        />
                    </div>
                    {(() => {
                        const amount = Math.floor(Number(customAmount)) || 0;
                        const matchedPkg = amount > 0 ? coinPackages.find((p) => p.id && p.coins === amount) : null;
                        const tierPrice = customAmount ? getPriceForCustomCoins(customAmount) : null;
                        // Hiển thị khi có nhập số coin: nếu trùng gói thì show giá gói (số tiền thật thanh toán), không trùng thì show tỷ giá tham khảo theo bậc
                        if (!customAmount || (!matchedPkg && !tierPrice)) return null;
                        const displayVnd = matchedPkg ? matchedPkg.price : tierPrice.totalVnd;
                        const displayRate = matchedPkg ? Math.round(matchedPkg.price / matchedPkg.coins) : tierPrice.ratePerCoin;
                        const isActualPrice = !!matchedPkg;
                        return (
                            <div className="mt-3 p-4 rounded-lg border border-emerald-200 dark:border-emerald-800 bg-emerald-50/80 dark:bg-emerald-950/30">
                                <p className="text-sm font-semibold text-slate-900 dark:text-white mb-1">
                                    {isActualPrice ? 'Số tiền cần thanh toán' : 'Số tiền tham khảo (theo bậc)'}
                                </p>
                                <p className="text-xl font-bold text-primary">
                                    {displayVnd.toLocaleString('vi-VN')} VNĐ
                                </p>
                                <p className="text-xs text-slate-600 dark:text-slate-400 mt-2">
                                    Tỷ giá: <span className="font-semibold">{displayRate.toLocaleString('vi-VN')} VNĐ</span>/coin
                                    {tierPrice && (
                                        <span className="ml-1 px-1.5 py-0.5 rounded bg-emerald-200/80 dark:bg-emerald-800/50 text-emerald-800 dark:text-emerald-200 text-[10px] font-medium">
                                            Bậc {tierPrice.label}
                                        </span>
                                    )}
                                    {isActualPrice && matchedPkg?.bonus > 0 && (
                                        <span className="ml-1 text-slate-500">(+{matchedPkg.bonus} bonus)</span>
                                    )}
                                </p>
                                <p className="text-xs text-slate-500 dark:text-slate-500 mt-1">
                                    Nạp càng nhiều coin, tỷ giá càng ưu đãi (từ 1.000 VNĐ/coin xuống 700 VNĐ/coin).
                                </p>
                                {!isActualPrice && (
                                    <p className="text-xs text-amber-600 dark:text-amber-400 mt-1">
                                        Để thanh toán, chọn đúng gói có số coin tương ứng bên trên.
                                    </p>
                                )}
                            </div>
                        );
                    })()}
                </div>

                {/* Promotions */}
                <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                    <div className="p-4 rounded-lg border border-purple-200 dark:border-purple-900/60 bg-purple-50/60 dark:bg-purple-900/20">
                        <p className="text-sm font-semibold text-purple-800 dark:text-purple-200 mb-1">
                            Ưu đãi nạp lần đầu
                        </p>
                        <p className="text-xs text-purple-700 dark:text-purple-300">
                            Nạp từ 1.000 Coins trở lên lần đầu tiên sẽ được tặng thêm 10% coin (demo UI).
                        </p>
                    </div>
                    <div className="p-4 rounded-lg border border-amber-200 dark:border-amber-900/60 bg-amber-50/60 dark:bg-amber-900/20">
                        <p className="text-sm font-semibold text-amber-800 dark:text-amber-200 mb-1">
                            Khung giờ vàng
                        </p>
                        <p className="text-xs text-amber-700 dark:text-amber-300">
                            Từ 20h - 22h mỗi tối, nạp gói từ 2.000 Coins trở lên được tặng thêm 5% coin (demo UI).
                        </p>
                    </div>
                </div>

                {/* Info */}
                <div className="p-4 bg-blue-50 dark:bg-blue-950/30 border border-blue-200 dark:border-blue-800 rounded-lg flex items-start gap-3">
                    <AlertCircle className="w-5 h-5 text-blue-600 dark:text-blue-400 flex-shrink-0 mt-0.5" />
                    <div className="text-sm text-blue-600 dark:text-blue-400">
                        <p className="font-semibold mb-1">Thanh toán qua PayOS</p>
                        <p>Bạn sẽ được chuyển sang trang thanh toán PayOS để hoàn tất. Coin sẽ được cộng vào ví ngay sau khi thanh toán thành công.</p>
                    </div>
                </div>

                {/* Submit Button - chuyển sang PayOS */}
                <div className="flex justify-end">
                    <button
                        type="button"
                        onClick={handleRecharge}
                        disabled={!resolvePackageId() || submitting}
                        className="px-8 py-3 bg-primary text-white font-bold rounded-lg hover:bg-primary/90 transition-all disabled:opacity-50 disabled:cursor-not-allowed shadow-lg shadow-primary/25"
                    >
                        {submitting ? 'Đang chuyển...' : 'Thanh toán qua PayOS'}
                    </button>
                </div>

                {/* Lịch sử nạp coin đã được tách sang tab Lịch sử */}
            </div>
        </div>
    );
}
