import { useState } from 'react';
import { Coins, BookOpen, CreditCard, Calendar, ArrowDown, ArrowUp } from 'lucide-react';

export default function ActivityHistory() {
    const [filter, setFilter] = useState('all');

    // Mock data - replace with actual API data
    const activities = [
        {
            id: 1,
            type: 'recharge',
            title: 'Nạp coin',
            amount: 1000,
            date: '2026-01-20',
            time: '14:30',
            status: 'success',
        },
        {
            id: 2,
            type: 'unlock',
            title: 'Mở khóa truyện "Kiếm Thánh"',
            amount: -50,
            date: '2026-01-19',
            time: '10:15',
            status: 'success',
        },
        {
            id: 3,
            type: 'payment',
            title: 'Thanh toán đăng truyện',
            amount: -100,
            date: '2026-01-18',
            time: '16:45',
            status: 'success',
        },
        {
            id: 4,
            type: 'recharge',
            title: 'Nạp coin',
            amount: 500,
            date: '2026-01-15',
            time: '09:20',
            status: 'success',
        },
    ];

    const filteredActivities = filter === 'all' 
        ? activities 
        : activities.filter(a => a.type === filter);

    const getIcon = (type) => {
        switch (type) {
            case 'recharge':
                return <ArrowDown className="w-5 h-5 text-green-500" />;
            case 'unlock':
            case 'payment':
                return <ArrowUp className="w-5 h-5 text-red-500" />;
            default:
                return <Coins className="w-5 h-5 text-slate-400" />;
        }
    };

    return (
        <div className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-8 border border-slate-200 dark:border-slate-700">
            <div className="flex items-center justify-between mb-6">
                <h3 className="text-xl font-bold text-slate-900 dark:text-white">
                    Lịch sử hoạt động
                </h3>
                <div className="flex gap-2">
                    <button
                        onClick={() => setFilter('all')}
                        className={`px-4 py-2 text-sm font-semibold rounded-lg transition-colors ${
                            filter === 'all'
                                ? 'bg-primary text-white'
                                : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-400'
                        }`}
                    >
                        Tất cả
                    </button>
                    <button
                        onClick={() => setFilter('recharge')}
                        className={`px-4 py-2 text-sm font-semibold rounded-lg transition-colors ${
                            filter === 'recharge'
                                ? 'bg-primary text-white'
                                : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-400'
                        }`}
                    >
                        Nạp coin
                    </button>
                    <button
                        onClick={() => setFilter('unlock')}
                        className={`px-4 py-2 text-sm font-semibold rounded-lg transition-colors ${
                            filter === 'unlock'
                                ? 'bg-primary text-white'
                                : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-400'
                        }`}
                    >
                        Mở khóa
                    </button>
                    <button
                        onClick={() => setFilter('payment')}
                        className={`px-4 py-2 text-sm font-semibold rounded-lg transition-colors ${
                            filter === 'payment'
                                ? 'bg-primary text-white'
                                : 'bg-slate-100 dark:bg-slate-700 text-slate-600 dark:text-slate-400'
                        }`}
                    >
                        Thanh toán
                    </button>
                </div>
            </div>

            <div className="space-y-4">
                {filteredActivities.length === 0 ? (
                    <div className="text-center py-12 text-slate-500 dark:text-slate-400">
                        Không có hoạt động nào
                    </div>
                ) : (
                    filteredActivities.map((activity) => (
                        <div
                            key={activity.id}
                            className="flex items-center gap-4 p-4 border border-slate-200 dark:border-slate-700 rounded-lg hover:bg-slate-50 dark:hover:bg-slate-700/50 transition-colors"
                        >
                            <div className="flex-shrink-0">
                                {getIcon(activity.type)}
                            </div>
                            <div className="flex-1">
                                <div className="font-semibold text-slate-900 dark:text-white">
                                    {activity.title}
                                </div>
                                <div className="flex items-center gap-2 text-sm text-slate-500 dark:text-slate-400">
                                    <Calendar className="w-4 h-4" />
                                    {activity.date} lúc {activity.time}
                                </div>
                            </div>
                            <div
                                className={`font-bold ${
                                    activity.amount > 0
                                        ? 'text-green-600 dark:text-green-400'
                                        : 'text-red-600 dark:text-red-400'
                                }`}
                            >
                                {activity.amount > 0 ? '+' : ''}
                                {activity.amount.toLocaleString()} Coins
                            </div>
                        </div>
                    ))
                )}
            </div>
        </div>
    );
}
