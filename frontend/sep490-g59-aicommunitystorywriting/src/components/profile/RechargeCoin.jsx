import { useCallback, useEffect, useRef, useState } from 'react';
import { Coins, CreditCard, Wallet, AlertCircle } from 'lucide-react';
import * as coinApi from '../../api/coins/coinApi';

export default function RechargeCoin() {
    const [selectedPackageId, setSelectedPackageId] = useState(null);
    const [paymentMethod, setPaymentMethod] = useState('card');
    const [packages, setPackages] = useState([]);
    const [wallet, setWallet] = useState(null);
    const [orders, setOrders] = useState([]);
    const [loading, setLoading] = useState(true);
    const [creating, setCreating] = useState(false);
    const [syncingOrderId, setSyncingOrderId] = useState(null);
    const [error, setError] = useState('');
    const autoSyncRef = useRef(false);

    const loadData = useCallback(async () => {
        setError('');
        setLoading(true);
        try {
            const [pkgRes, walletRes, ordersRes] = await Promise.all([
                coinApi.getCoinPackages(),
                coinApi.getMyWallet(),
                coinApi.getMyCoinOrders({ take: 20 }),
            ]);

            if (!pkgRes.success) throw new Error(pkgRes.message);
            if (!walletRes.success) throw new Error(walletRes.message);
            if (!ordersRes.success) throw new Error(ordersRes.message);

            setPackages(Array.isArray(pkgRes.data) ? pkgRes.data : []);
            setWallet(walletRes.data ?? null);
            setOrders(Array.isArray(ordersRes.data) ? ordersRes.data : []);
        } catch (e) {
            setError(e?.message || 'Không thể tải dữ liệu nạp coin');
        } finally {
            setLoading(false);
        }
    }, []);

    useEffect(() => {
        loadData();
    }, [loadData]);

    // Auto-sync order status when PayOS redirects back (cancel/return).
    useEffect(() => {
        if (autoSyncRef.current) return;

        const sp = new URLSearchParams(window.location.search);
        const payos = sp.get('payos');
        const orderId = sp.get('orderId');

        if (!orderId || (payos !== 'cancel' && payos !== 'return')) return;

        autoSyncRef.current = true;

        (async () => {
            await syncOrder(orderId);

            // Clean up URL params so refresh doesn't re-trigger sync.
            sp.delete('orderId');
            sp.delete('payos');
            const next = `${window.location.pathname}${sp.toString() ? `?${sp.toString()}` : ''}`;
            window.history.replaceState({}, '', next);
        })();
        // eslint-disable-next-line react-hooks/exhaustive-deps
    }, []);

    const handleRecharge = async () => {
        setError('');
        if (!selectedPackageId) {
            setError('Vui lòng chọn một gói coin.');
            return;
        }

        setCreating(true);
        try {
            const origin = window.location.origin;
            const returnUrl = `${origin}/profile?tab=recharge&payos=return`;
            const cancelUrl = `${origin}/profile?tab=recharge&payos=cancel`;

            const res = await coinApi.createPayOSPayment({
                packageId: selectedPackageId,
                returnUrl,
                cancelUrl,
            });

            if (!res.success) {
                throw new Error(res.message);
            }

            const checkoutUrl = res?.data?.checkoutUrl;
            if (!checkoutUrl) {
                throw new Error('Không nhận được checkoutUrl từ server.');
            }

            window.location.href = checkoutUrl;
        } catch (e) {
            setError(e?.message || 'Không thể tạo link thanh toán');
        } finally {
            setCreating(false);
        }
    };

    const syncOrder = async (orderId) => {
        setError('');
        setSyncingOrderId(orderId);
        try {
            const res = await coinApi.syncMyPayOSOrder(orderId);
            if (!res.success) throw new Error(res.message);
            await loadData();
        } catch (e) {
            setError(e?.message || 'Không thể đồng bộ trạng thái');
        } finally {
            setSyncingOrderId(null);
        }
    };

    const formatApiDateTime = (value) => {
        if (!value) return '';
        const s = String(value);
        // If backend returns ISO without timezone, assume it's UTC.
        const hasTimezone = /([zZ]|[+-]\d{2}:\d{2})$/.test(s);
        const iso = hasTimezone ? s : `${s}Z`;
        const d = new Date(iso);
        if (Number.isNaN(d.getTime())) return s;
        return d.toLocaleString();
    };

    return (
        <div className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-8 border border-slate-200 dark:border-slate-700">
            <h3 className="text-xl font-bold text-slate-900 dark:text-white mb-6">
                Nạp Coin
            </h3>

            {error && (
                <div className="mb-6 p-4 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 rounded-lg text-sm text-red-700 dark:text-red-300">
                    {error}
                </div>
            )}

            <div className="mb-6 flex items-center justify-between gap-4">
                <div className="flex items-center gap-2">
                    <Coins className="w-5 h-5 text-amber-500" />
                    <div className="text-sm text-slate-700 dark:text-slate-300">
                        Coin hiện tại:{' '}
                        <span className="font-bold text-slate-900 dark:text-white">
                            {(wallet?.balanceCoin ?? 0).toLocaleString()}
                        </span>
                    </div>
                </div>
                <button
                    onClick={loadData}
                    disabled={loading || creating}
                    className="px-4 py-2 text-sm font-semibold border border-slate-200 dark:border-slate-600 rounded-lg hover:bg-slate-50 dark:hover:bg-slate-700 transition disabled:opacity-50"
                >
                    Làm mới
                </button>
            </div>

            <div className="space-y-6">
                {/* Coin Packages */}
                <div>
                    <label className="block text-sm font-semibold text-slate-700 dark:text-slate-300 mb-4">
                        Chọn gói coin
                    </label>
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                        {loading ? (
                            <div className="col-span-full text-sm text-slate-500 dark:text-slate-400">
                                Đang tải gói coin...
                            </div>
                        ) : packages.length === 0 ? (
                            <div className="col-span-full text-sm text-slate-500 dark:text-slate-400">
                                Chưa có gói coin nào.
                            </div>
                        ) : (
                            packages.map((pkg) => {
                                const isActive = selectedPackageId === pkg.id;
                                const bonus = pkg.bonusCoin ?? 0;
                                const totalCoins = (pkg.coinAmount ?? 0) + bonus;
                                return (
                                    <button
                                        key={pkg.id}
                                        onClick={() => setSelectedPackageId(pkg.id)}
                                        className={`p-4 border-2 rounded-lg text-left transition-all ${
                                            isActive
                                                ? 'border-primary bg-primary/10'
                                                : 'border-slate-200 dark:border-slate-600 hover:border-primary/50'
                                        }`}
                                    >
                                        <div className="flex items-center justify-between mb-2">
                                            <div className="flex items-center gap-2">
                                                <Coins className="w-5 h-5 text-amber-500" />
                                                <span className="font-bold text-slate-900 dark:text-white">
                                                    {totalCoins.toLocaleString()} Coins
                                                </span>
                                            </div>
                                            {bonus > 0 && (
                                                <span className="text-xs bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400 px-2 py-1 rounded">
                                                    +{bonus}
                                                </span>
                                            )}
                                        </div>
                                        <div className="text-sm text-slate-600 dark:text-slate-400">
                                            {Number(pkg.priceAmount ?? 0).toLocaleString()} {pkg.currency || 'VND'}
                                        </div>
                                        <div className="mt-1 text-xs text-slate-500 dark:text-slate-500">
                                            {pkg.name}
                                        </div>
                                    </button>
                                );
                            })
                        )}
                    </div>
                </div>

                {/* Custom Amount (Not supported yet) */}
                <div>
                    <label className="block text-sm font-semibold text-slate-700 dark:text-slate-300 mb-2">
                        Nhập số coin tùy chỉnh (chưa hỗ trợ)
                    </label>
                    <div className="relative">
                        <div className="absolute inset-y-0 left-0 pl-3 flex items-center pointer-events-none">
                            <Coins className="w-5 h-5 text-slate-400" />
                        </div>
                        <input
                            type="number"
                            disabled
                            value=""
                            placeholder="Vui lòng chọn gói coin ở trên"
                            className="block w-full pl-10 pr-4 py-3 bg-slate-50 dark:bg-slate-700/50 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-900 dark:text-white opacity-70 cursor-not-allowed outline-none"
                        />
                    </div>
                </div>

                {/* Payment Method */}
                <div>
                    <label className="block text-sm font-semibold text-slate-700 dark:text-slate-300 mb-4">
                        Phương thức thanh toán
                    </label>
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
                        <button
                            onClick={() => setPaymentMethod('card')}
                            className={`p-4 border-2 rounded-lg text-left transition-all ${
                                paymentMethod === 'card'
                                    ? 'border-primary bg-primary/10'
                                    : 'border-slate-200 dark:border-slate-600 hover:border-primary/50'
                            }`}
                        >
                            <div className="flex items-center gap-3">
                                <CreditCard className="w-6 h-6 text-slate-600 dark:text-slate-400" />
                                <div>
                                    <div className="font-semibold text-slate-900 dark:text-white">
                                        Thẻ tín dụng/Ghi nợ
                                    </div>
                                    <div className="text-sm text-slate-500 dark:text-slate-400">
                                        Visa, Mastercard
                                    </div>
                                </div>
                            </div>
                        </button>

                        <button
                            onClick={() => setPaymentMethod('wallet')}
                            className={`p-4 border-2 rounded-lg text-left transition-all ${
                                paymentMethod === 'wallet'
                                    ? 'border-primary bg-primary/10'
                                    : 'border-slate-200 dark:border-slate-600 hover:border-primary/50'
                            }`}
                        >
                            <div className="flex items-center gap-3">
                                <Wallet className="w-6 h-6 text-slate-600 dark:text-slate-400" />
                                <div>
                                    <div className="font-semibold text-slate-900 dark:text-white">
                                        Ví điện tử
                                    </div>
                                    <div className="text-sm text-slate-500 dark:text-slate-400">
                                        Momo, ZaloPay, VNPay
                                    </div>
                                </div>
                            </div>
                        </button>
                    </div>
                </div>

                {/* Info */}
                <div className="p-4 bg-blue-50 dark:bg-blue-950/30 border border-blue-200 dark:border-blue-800 rounded-lg flex items-start gap-3">
                    <AlertCircle className="w-5 h-5 text-blue-600 dark:text-blue-400 flex-shrink-0 mt-0.5" />
                    <div className="text-sm text-blue-600 dark:text-blue-400">
                        <p className="font-semibold mb-1">Lưu ý:</p>
                        <p>Giao dịch sẽ được xử lý an toàn và bảo mật. Coin sẽ được cộng vào tài khoản ngay sau khi thanh toán thành công.</p>
                    </div>
                </div>

                {/* Submit Button */}
                <div className="flex justify-end">
                    <button
                        onClick={handleRecharge}
                        disabled={!selectedPackageId || creating || loading}
                        className="px-8 py-3 bg-primary text-white font-bold rounded-lg hover:bg-primary/90 transition-all disabled:opacity-50 disabled:cursor-not-allowed shadow-lg shadow-primary/25"
                    >
                        {creating ? 'Đang tạo link...' : 'Nạp Coin'}
                    </button>
                </div>

                {/* Recent Orders */}
                <div className="pt-6 border-t border-slate-200 dark:border-slate-700">
                    <div className="flex items-center justify-between mb-3">
                        <div className="text-sm font-semibold text-slate-700 dark:text-slate-300">
                            Lịch sử nạp gần đây
                        </div>
                    </div>
                    {orders.length === 0 ? (
                        <div className="text-sm text-slate-500 dark:text-slate-400">Chưa có giao dịch nào.</div>
                    ) : (
                        <div className="overflow-x-auto">
                            <table className="min-w-full text-sm">
                                <thead>
                                    <tr className="text-left text-slate-500 dark:text-slate-400">
                                        <th className="py-2 pr-4">Thời gian</th>
                                        <th className="py-2 pr-4">Gói</th>
                                        <th className="py-2 pr-4">Coins</th>
                                        <th className="py-2 pr-4">Trạng thái</th>
                                        <th className="py-2 pr-0"></th>
                                    </tr>
                                </thead>
                                <tbody className="text-slate-700 dark:text-slate-200">
                                    {orders.slice(0, 10).map((o) => (
                                        <tr key={o.id} className="border-t border-slate-100 dark:border-slate-700">
                                            <td className="py-2 pr-4 whitespace-nowrap">
                                                {formatApiDateTime(o.createdAt)}
                                            </td>
                                            <td className="py-2 pr-4 whitespace-nowrap">
                                                {o.amountPaid?.toLocaleString?.() ?? o.amountPaid} VND
                                            </td>
                                            <td className="py-2 pr-4 whitespace-nowrap">
                                                {(o.coinsGranted ?? 0).toLocaleString()}
                                            </td>
                                            <td className="py-2 pr-4 whitespace-nowrap">
                                                <span
                                                    className={`px-2 py-1 rounded text-xs font-bold ${
                                                        o.status === 'PAID'
                                                            ? 'bg-green-100 dark:bg-green-900/30 text-green-700 dark:text-green-400'
                                                            : o.status === 'FAILED'
                                                              ? 'bg-red-100 dark:bg-red-900/30 text-red-700 dark:text-red-400'
                                                              : 'bg-slate-100 dark:bg-slate-700 text-slate-700 dark:text-slate-200'
                                                    }`}
                                                >
                                                    {o.status}
                                                </span>
                                            </td>
                                            <td className="py-2 pr-0 whitespace-nowrap text-right">
                                                {o.status === 'PENDING' && (
                                                    <button
                                                        onClick={() => syncOrder(o.id)}
                                                        disabled={syncingOrderId === o.id || loading || creating}
                                                        className="px-3 py-1 text-xs font-bold border border-slate-200 dark:border-slate-600 rounded hover:bg-slate-50 dark:hover:bg-slate-700 disabled:opacity-50"
                                                    >
                                                        {syncingOrderId === o.id ? 'Đang sync...' : 'Cập nhật'}
                                                    </button>
                                                )}
                                            </td>
                                        </tr>
                                    ))}
                                </tbody>
                            </table>
                        </div>
                    )}
                </div>
            </div>
        </div>
    );
}
