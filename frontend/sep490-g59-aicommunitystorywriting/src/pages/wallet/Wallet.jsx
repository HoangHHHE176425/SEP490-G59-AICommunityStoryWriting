import { useEffect, useMemo, useState } from 'react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { Wallet as WalletIcon, History, ArrowDownCircle, ArrowUpCircle, Lock } from 'lucide-react';
import { useAuth } from '../../contexts/AuthContext';
import RechargeCoin from '../../components/profile/RechargeCoin';
import ActivityHistory from '../../components/profile/ActivityHistory';
import * as coinApi from '../../api/coins/coinApi';

export default function Wallet() {
    const { user, role } = useAuth();
    const [activeTab, setActiveTab] = useState('recharge');

    const [walletBalance, setWalletBalance] = useState(null);
    const [incomeBalance, setIncomeBalance] = useState(0);
    const [frozenBalance, setFrozenBalance] = useState(0);
    const [totalRechargeCoins, setTotalRechargeCoins] = useState(0);
    const [totalSpentCoins, setTotalSpentCoins] = useState(0); // chưa có API
    const [lockedCoins, setLockedCoins] = useState(0); // map từ frozen_balance (tạm khóa khi rút)
    const [loadingStats, setLoadingStats] = useState(true);
    const [statsError, setStatsError] = useState('');

    const normalizedRole = (role ?? '').toString().trim().toUpperCase();
    const isAuthor = normalizedRole === 'AUTHOR' || user?.isAuthor === true;
    const displayBalance = isAuthor
        ? incomeBalance
        : walletBalance ?? (user?.stats?.currentCoins ?? 0);

    const spentPercent = useMemo(() => {
        if (totalRechargeCoins <= 0) return 0;
        if (totalSpentCoins <= 0) return 0;
        return Math.min(100, Math.round((totalSpentCoins / totalRechargeCoins) * 100));
    }, [totalRechargeCoins, totalSpentCoins]);

    useEffect(() => {
        let cancelled = false;

        const load = async () => {
            setLoadingStats(true);
            setStatsError('');
            try {
                const [walletRes, ordersRes] = await Promise.all([
                    coinApi.getMyWallet(),
                    coinApi.getMyCoinOrders({ take: 200 }),
                ]);

                if (!walletRes?.success) throw new Error(walletRes?.message || 'Không thể tải ví');
                if (!ordersRes?.success) throw new Error(ordersRes?.message || 'Không thể tải lịch sử giao dịch');

                if (cancelled) return;

                setWalletBalance(walletRes?.data?.balanceCoin ?? 0);
                setIncomeBalance(Number(walletRes?.data?.incomeBalance ?? 0) || 0);
                setFrozenBalance(Number(walletRes?.data?.frozenBalance ?? 0) || 0);

                const orders = Array.isArray(ordersRes.data) ? ordersRes.data : [];
                const totalPaid = orders
                    .filter((o) => String(o.status || '').toUpperCase() === 'PAID')
                    .reduce((sum, o) => sum + (Number(o.coinsGranted ?? 0) || 0), 0);

                setTotalRechargeCoins(totalPaid);
                setTotalSpentCoins(0);
                // frozen_balance = coin đang bị khóa do yêu cầu rút (chờ admin duyệt)
                setLockedCoins(Number(walletRes?.data?.frozenBalance ?? 0) || 0);
            } catch (e) {
                if (cancelled) return;
                setStatsError(e?.message || 'Không thể tải dữ liệu ví');
            } finally {
                if (!cancelled) setLoadingStats(false);
            }
        };

        load().catch(() => {});

        const handler = () => load().catch(() => {});
        window.addEventListener('wallet:changed', handler);
        return () => {
            cancelled = true;
            window.removeEventListener('wallet:changed', handler);
        };
    }, []);

    const tabs = [
        { id: 'recharge', label: 'Nạp tiền', icon: WalletIcon },
        { id: 'history', label: 'Lịch sử', icon: History },
    ];

    const renderContent = () => {
        switch (activeTab) {
            case 'recharge':
                return <RechargeCoin />;
            case 'history':
                return <ActivityHistory mode="wallet" />;
            default:
                return <RechargeCoin />;
        }
    };

    return (
        <div className="min-h-screen bg-background-light dark:bg-background-dark flex flex-col">
            <Header />
            <div className="flex-1">
                <div className="max-w-[1280px] mx-auto px-4 py-8">
                    {/* Wallet summary */}
                    <div className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-6 mb-6 border border-slate-200 dark:border-slate-700">
                        <div className="flex flex-col gap-6 md:flex-row md:items-center md:justify-between">
                            <div className="flex items-center gap-3">
                                <div className="size-12 rounded-full bg-primary/10 flex items-center justify-center">
                                    <WalletIcon className="w-6 h-6 text-primary" />
                                </div>
                                <div>
                                    <h2 className="text-xl font-bold text-slate-900 dark:text-white">Ví của tôi</h2>
                                    <p className="text-sm text-slate-500 dark:text-slate-400">
                                        Quản lý số dư coin và giao dịch
                                    </p>
                                </div>
                            </div>
                            <div className="text-right">
                                <p className="text-xs uppercase tracking-wide text-slate-500 dark:text-slate-400">
                                    Số dư hiện tại
                                </p>
                                <p className="text-2xl font-bold text-primary">
                                    {Number(displayBalance || 0).toLocaleString()} Coins
                                </p>
                            </div>
                        </div>

                        {statsError && (
                            <div className="mt-4 p-4 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 rounded-lg text-sm text-red-700 dark:text-red-300">
                                {statsError}
                            </div>
                        )}

                        {/* Quick stats */}
                        <div className="mt-6 grid grid-cols-1 sm:grid-cols-3 gap-4">
                            <div className="flex items-center gap-3 rounded-lg border border-emerald-100 dark:border-emerald-900/60 bg-emerald-50/60 dark:bg-emerald-900/20 px-3 py-2.5">
                                <div className="size-9 rounded-full bg-emerald-500/10 flex items-center justify-center">
                                    <ArrowDownCircle className="w-5 h-5 text-emerald-500" />
                                </div>
                                <div>
                                    <p className="text-xs text-slate-500 dark:text-slate-400">Tổng đã nạp</p>
                                    <p className="text-sm font-semibold text-slate-900 dark:text-white">
                                        {loadingStats ? '...' : `${totalRechargeCoins.toLocaleString()} Coins`}
                                    </p>
                                </div>
                            </div>
                            <div className="flex items-center gap-3 rounded-lg border border-amber-100 dark:border-amber-900/60 bg-amber-50/60 dark:bg-amber-900/20 px-3 py-2.5">
                                <div className="size-9 rounded-full bg-amber-500/10 flex items-center justify-center">
                                    <ArrowUpCircle className="w-5 h-5 text-amber-500" />
                                </div>
                                <div>
                                    <p className="text-xs text-slate-500 dark:text-slate-400">Thu nhập khả dụng</p>
                                    <p className="text-sm font-semibold text-slate-900 dark:text-white">
                                        {loadingStats ? '...' : `${incomeBalance.toLocaleString()} Coins`}
                                    </p>
                                </div>
                            </div>
                            <div className="flex items-center gap-3 rounded-lg border border-slate-200 dark:border-slate-700 bg-slate-50/80 dark:bg-slate-900/40 px-3 py-2.5">
                                <div className="size-9 rounded-full bg-slate-500/10 flex items-center justify-center">
                                    <Lock className="w-5 h-5 text-slate-500" />
                                </div>
                                <div>
                                    <p className="text-xs text-slate-500 dark:text-slate-400">Số dư khóa</p>
                                    <p className="text-sm font-semibold text-slate-900 dark:text-white">
                                        {loadingStats ? '...' : `${lockedCoins.toLocaleString()} Coins`}
                                    </p>
                                </div>
                            </div>
                        </div>

                        {/* Usage progress */}
                        {!loadingStats && totalSpentCoins > 0 && totalRechargeCoins > 0 && (
                            <div className="mt-5">
                                <div className="flex items-center justify-between mb-1 text-xs text-slate-500 dark:text-slate-400">
                                    <span>Đã sử dụng {spentPercent}% số coin đã nạp</span>
                                    <span>
                                        {totalSpentCoins.toLocaleString()} /{' '}
                                        {totalRechargeCoins.toLocaleString()} Coins
                                    </span>
                                </div>
                                <div className="h-2 rounded-full bg-slate-100 dark:bg-slate-900 overflow-hidden">
                                    <div
                                        className="h-full bg-primary/80 rounded-full transition-all"
                                        style={{ width: `${spentPercent}%` }}
                                    ></div>
                                </div>
                            </div>
                        )}
                    </div>

                    {/* Tabs */}
                    <div className="mb-8 border-b border-slate-200 dark:border-slate-700">
                        <div className="flex gap-8 overflow-x-auto">
                            {tabs.map((tab) => {
                                const Icon = tab.icon;
                                const isActive = activeTab === tab.id;
                                return (
                                    <button
                                        key={tab.id}
                                        onClick={() => setActiveTab(tab.id)}
                                        className={`flex items-center gap-2 px-4 py-4 font-semibold text-sm transition-colors border-b-2 whitespace-nowrap ${isActive
                                                ? 'text-primary border-primary'
                                                : 'text-slate-500 dark:text-slate-400 border-transparent hover:text-primary'
                                            }`}
                                    >
                                        <Icon className="w-5 h-5" />
                                        {tab.label}
                                    </button>
                                );
                            })}
                        </div>
                    </div>

                    {renderContent()}
                </div>
            </div>
            <Footer />
        </div>
    );
}
