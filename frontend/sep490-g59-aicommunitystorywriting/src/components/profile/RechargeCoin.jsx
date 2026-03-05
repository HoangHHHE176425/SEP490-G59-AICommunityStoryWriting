import { useState } from 'react';
import { Coins, CreditCard, Wallet, AlertCircle } from 'lucide-react';

export default function RechargeCoin() {
    const [selectedAmount, setSelectedAmount] = useState(null);
    const [customAmount, setCustomAmount] = useState('');
    const [paymentMethod, setPaymentMethod] = useState('card');
    const [status, setStatus] = useState(null); // 'success' | 'error' | null
    const [statusMessage, setStatusMessage] = useState('');

    const coinPackages = [
        { coins: 100, price: 100000, bonus: 0 },
        { coins: 500, price: 450000, bonus: 50 },
        { coins: 1000, price: 850000, bonus: 150, recommended: true },
        { coins: 2000, price: 1600000, bonus: 400 },
        { coins: 5000, price: 3750000, bonus: 1250 },
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

    const customPrice = customAmount ? getPriceForCustomCoins(customAmount) : null;
    const EXCHANGE_RATE = 1000; // tỉ giá tham khảo mặc định (1 Coin ≈ 1.000 VNĐ)

    const handleRecharge = () => {
        const amount = selectedAmount || Number(customAmount) || 0;
        if (!amount || amount <= 0) {
            setStatus('error');
            setStatusMessage('Vui lòng chọn hoặc nhập số coin hợp lệ.');
            return;
        }

        // Mock recharge - thay bằng API thật sau
        setStatus('success');
        setStatusMessage(
            `Demo: Yêu cầu nạp ${amount.toLocaleString()} Coins đã được tạo. Khi tích hợp cổng thanh toán, coin sẽ được cộng sau khi thanh toán thành công.`
        );
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

            {/* Status message */}
            {status && (
                <div
                    className={`mt-4 mb-2 rounded-lg px-4 py-3 text-sm flex items-start gap-2 ${
                        status === 'success'
                            ? 'bg-emerald-50 text-emerald-700 border border-emerald-200 dark:bg-emerald-900/20 dark:text-emerald-300 dark:border-emerald-900/60'
                            : 'bg-red-50 text-red-700 border border-red-200 dark:bg-red-900/20 dark:text-red-300 dark:border-red-900/60'
                    }`}
                >
                    <AlertCircle className="w-4 h-4 mt-0.5 flex-shrink-0" />
                    <p>{statusMessage}</p>
                </div>
            )}

            <div className="space-y-6">
                {/* Coin Packages */}
                <div>
                    <label className="block text-sm font-semibold text-slate-700 dark:text-slate-300 mb-4">
                        Chọn gói coin
                    </label>
                    <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                        {coinPackages.map((pkg, index) => (
                            <button
                                key={index}
                                onClick={() => {
                                    setSelectedAmount(pkg.coins);
                                    setCustomAmount('');
                                }}
                                className={`relative p-4 border-2 rounded-lg text-left transition-all ${
                                    selectedAmount === pkg.coins
                                        ? 'border-primary bg-primary/10'
                                        : 'border-slate-200 dark:border-slate-600 hover:border-primary/50'
                                }`}
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
                        ))}
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
                                setCustomAmount(e.target.value);
                                setSelectedAmount(null);
                            }}
                            placeholder="Nhập số coin"
                            className="block w-full pl-10 pr-4 py-3 bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-900 dark:text-white focus:ring-2 focus:ring-primary/50 focus:border-primary transition-all outline-none"
                            min="1"
                        />
                    </div>
                    {customPrice && (
                        <div className="mt-3 p-4 rounded-lg border border-emerald-200 dark:border-emerald-800 bg-emerald-50/80 dark:bg-emerald-950/30">
                            <p className="text-sm font-semibold text-slate-900 dark:text-white mb-1">
                                Số tiền cần thanh toán
                            </p>
                            <p className="text-xl font-bold text-primary">
                                {customPrice.totalVnd.toLocaleString('vi-VN')} VNĐ
                            </p>
                            <p className="text-xs text-slate-600 dark:text-slate-400 mt-2">
                                Tỷ giá áp dụng: <span className="font-semibold">{customPrice.ratePerCoin.toLocaleString('vi-VN')} VNĐ</span>/coin
                                <span className="ml-1 px-1.5 py-0.5 rounded bg-emerald-200/80 dark:bg-emerald-800/50 text-emerald-800 dark:text-emerald-200 text-[10px] font-medium">
                                    Bậc {customPrice.label}
                                </span>
                            </p>
                            <p className="text-xs text-slate-500 dark:text-slate-500 mt-1">
                                Nạp càng nhiều coin, tỷ giá càng ưu đãi (từ 1.000 VNĐ/coin xuống 700 VNĐ/coin).
                            </p>
                        </div>
                    )}
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
                        <p className="font-semibold mb-1">Lưu ý:</p>
                        <p>Giao dịch sẽ được xử lý an toàn và bảo mật. Coin sẽ được cộng vào tài khoản ngay sau khi thanh toán thành công.</p>
                    </div>
                </div>

                {/* Submit Button */}
                <div className="flex justify-end">
                    <button
                        onClick={handleRecharge}
                        disabled={!selectedAmount && !customAmount}
                        className="px-8 py-3 bg-primary text-white font-bold rounded-lg hover:bg-primary/90 transition-all disabled:opacity-50 disabled:cursor-not-allowed shadow-lg shadow-primary/25"
                    >
                        Nạp Coin
                    </button>
                </div>
            </div>
        </div>
    );
}
