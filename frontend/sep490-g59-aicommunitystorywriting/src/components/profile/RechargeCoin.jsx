import { useState } from 'react';
import { Coins, CreditCard, Wallet, AlertCircle } from 'lucide-react';

export default function RechargeCoin() {
    const [selectedAmount, setSelectedAmount] = useState(null);
    const [customAmount, setCustomAmount] = useState('');
    const [paymentMethod, setPaymentMethod] = useState('card');

    const coinPackages = [
        { coins: 100, price: 10000, bonus: 0 },
        { coins: 500, price: 45000, bonus: 50 },
        { coins: 1000, price: 85000, bonus: 150 },
        { coins: 2000, price: 160000, bonus: 400 },
        { coins: 5000, price: 375000, bonus: 1250 },
    ];

    const handleRecharge = () => {
        // Mock recharge - replace with actual API call
        alert('Tính năng nạp coin đang được phát triển');
    };

    return (
        <div className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-8 border border-slate-200 dark:border-slate-700">
            <h3 className="text-xl font-bold text-slate-900 dark:text-white mb-6">
                Nạp Coin
            </h3>

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
                                className={`p-4 border-2 rounded-lg text-left transition-all ${
                                    selectedAmount === pkg.coins
                                        ? 'border-primary bg-primary/10'
                                        : 'border-slate-200 dark:border-slate-600 hover:border-primary/50'
                                }`}
                            >
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
                                    {pkg.price.toLocaleString()} VNĐ
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
