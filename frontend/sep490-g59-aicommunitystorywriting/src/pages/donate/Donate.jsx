import { useMemo, useState } from 'react';
import { useParams, useSearchParams, Link, useNavigate } from 'react-router-dom';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import {
    Gift,
    Coins,
    ArrowLeft,
    MessageCircle,
    Coffee,
    IceCream,
    Pizza,
    Cake,
    Gem,
    Car,
    Rocket,
    Crown,
} from 'lucide-react';

export default function Donate() {
    const { authorId } = useParams();
    const [searchParams] = useSearchParams();
    const navigate = useNavigate();

    const authorName = useMemo(
        () => searchParams.get('name') || 'Tác giả',
        [searchParams]
    );

    const [selectedAmount, setSelectedAmount] = useState(100);
    const [message, setMessage] = useState('');

    const effectiveAmount = selectedAmount;

    const handleConfirm = (e) => {
        e.preventDefault();
        if (!effectiveAmount || effectiveAmount <= 0) return;
        // Chỉ demo giao diện, chưa gọi API thật
        alert(`Cảm ơn bạn đã ủng hộ ${effectiveAmount.toLocaleString()} coin cho ${authorName}! (Demo giao diện)`);
        navigate(-1);
    };

    return (
        <div className="min-h-screen bg-white text-slate-900">
            <Header />
            <main className="pt-10 pb-16">
                <div className="max-w-[1280px] mx-auto px-4 mb-4">
                    <button
                        type="button"
                        onClick={() => navigate(-1)}
                        className="inline-flex items-center gap-2 text-sm text-slate-600 hover:text-primary transition-colors"
                    >
                        <ArrowLeft className="w-4 h-4" />
                        Quay lại
                    </button>
                </div>

                {/* Banner full chiều ngang màn */}
                <section className="w-full bg-gradient-to-br from-emerald-500 via-primary to-emerald-400 shadow-xl relative overflow-hidden">
                    <div className="absolute inset-0 opacity-30 bg-[radial-gradient(circle_at_top,_#ffffff33,_transparent_60%)]" />
                    <div className="relative max-w-[1280px] mx-auto px-4 py-6 md:py-8 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
                        <div className="flex flex-col gap-3">
                            <div className="inline-flex items-center gap-2 px-3 py-1 rounded-full bg-white/20 text-xs font-semibold text-white w-fit">
                                <Gift className="w-4 h-4" />
                                Ủng hộ tác giả
                            </div>
                            <div>
                                <p className="text-xs uppercase tracking-[0.2em] text-white/95 mb-1 font-medium">
                                    Gửi lời cảm ơn chân thành
                                </p>
                                <h1 className="text-xl md:text-2xl font-extrabold leading-snug text-white">
                                    Dành tặng cho{' '}
                                    <span className="text-white underline decoration-white/80 underline-offset-4">
                                        {authorName}
                                    </span>
                                </h1>
                            </div>
                            <p className="text-sm text-white/95 leading-relaxed max-w-xl">
                                Mỗi gói coin nhỏ là một lời động viên lớn, giúp tác giả có thêm động lực để tiếp tục
                                viết nên những câu chuyện mà bạn yêu thích.
                            </p>
                            {authorId && (
                                <p className="text-[10px] text-white/80 break-all font-medium">
                                    ID tác giả: {authorId}
                                </p>
                            )}
                        </div>
                    </div>
                </section>

                {/* Nội dung: gói quà nằm ngang + form */}
                <div className="max-w-[1280px] mx-auto px-4 pt-6">
                    <form
                        onSubmit={handleConfirm}
                        className="bg-white border border-slate-200 rounded-2xl p-6 shadow-sm space-y-6"
                    >
                        {/* Chọn gói quà — một hàng ngang */}
                        <div>
                            <div className="flex items-center gap-2 mb-3">
                                <Coins className="w-4 h-4 text-amber-500" />
                                <h2 className="text-sm font-semibold text-slate-800">
                                    Chọn gói quà tặng
                                </h2>
                            </div>
                            <div className="flex flex-nowrap gap-3 overflow-x-auto pb-2 -mx-1 px-1">
                                    {[
                                        { amount: 10, label: 'Cốc cà phê', Icon: Coffee },
                                        { amount: 20, label: 'Kem ngọt', Icon: IceCream },
                                        { amount: 50, label: 'Miếng pizza', Icon: Pizza },
                                        { amount: 100, label: 'Miếng bánh ngọt', Icon: Cake },
                                        { amount: 500, label: 'Chiếc nhẫn', Icon: Gem },
                                        { amount: 1000, label: 'Xe máy mơ ước', Icon: Car },
                                        { amount: 2000, label: 'Tên lửa động lực', Icon: Rocket },
                                        { amount: 5000, label: 'Vương miện fan cứng', Icon: Crown },
                                    ].map(({ amount, label, Icon }) => {
                                        const isActive = selectedAmount === amount;
                                        return (
                                            <button
                                                key={amount}
                                                type="button"
                                                onClick={() => setSelectedAmount(amount)}
                                                className={`flex flex-col items-center justify-center rounded-xl border px-4 py-3 text-xs font-semibold transition-all shrink-0 min-w-[80px] ${
                                                    isActive
                                                        ? 'border-amber-500 bg-amber-50 text-amber-700 shadow-[0_0_0_1px_rgba(245,158,11,0.5)]'
                                                        : 'border-slate-300 bg-slate-50 text-slate-700 hover:border-slate-400'
                                                }`}
                                            >
                                                <Icon className={`mb-1 w-5 h-5 ${isActive ? 'text-amber-500' : 'text-slate-500'}`} />
                                                <span>{amount.toLocaleString()} coin</span>
                                                <span className="mt-0.5 text-[10px] font-normal text-slate-500 line-clamp-1 text-center">
                                                    {label}
                                                </span>
                                            </button>
                                        );
                                    })}
                            </div>
                            <p className="text-[11px] text-slate-500 mt-1">
                                Bạn có thể chọn một trong các gói dễ thương ở trên để gửi quà cho tác giả.
                            </p>
                            <div className="flex items-center justify-end text-xs text-slate-600 mt-2">
                                Sẽ gửi:{' '}
                                <span className="ml-1 font-semibold text-amber-600">
                                    {effectiveAmount.toLocaleString()} coin
                                </span>
                            </div>
                        </div>

                        {/* Lời nhắn tới tác giả */}
                            <div>
                                <div className="flex items-center gap-2 mb-1">
                                    <MessageCircle className="w-4 h-4 text-sky-500" />
                                    <label className="text-xs font-semibold text-slate-700">
                                        Lời nhắn tới tác giả (tuỳ chọn)
                                    </label>
                                </div>
                                <textarea
                                    rows={3}
                                    maxLength={200}
                                    value={message}
                                    onChange={(e) => setMessage(e.target.value)}
                                    placeholder="Ví dụ: Cảm ơn vì câu chuyện rất hay! Mong chờ những chương tiếp theo ✨"
                                    className="w-full rounded-lg bg-slate-50 border border-slate-300 px-3 py-2 text-sm text-slate-900 placeholder:text-slate-400 outline-none focus:ring-2 focus:ring-primary/50 focus:border-primary resize-none"
                                />
                                <div className="mt-1 text-[10px] text-slate-500 text-right">
                                    {message.length}/200 ký tự
                                </div>
                            </div>

                            {/* Ghi chú & nút xác nhận */}
                            <div className="flex flex-col md:flex-row md:items-center md:justify-between gap-3 pt-2 border-t border-slate-200">
                                <p className="text-[11px] text-slate-500 md:max-w-sm">
                                    Đây là bản demo giao diện ủng hộ. Bước thanh toán và trừ coin thực tế sẽ được tích
                                    hợp sau.
                                </p>
                                <div className="flex items-center gap-3">
                                    <Link
                                        to="/wallet"
                                        className="text-xs font-semibold text-slate-600 hover:text-primary underline-offset-2 hover:underline"
                                    >
                                        Xem số coin trong ví
                                    </Link>
                                    <button
                                        type="submit"
                                        disabled={!effectiveAmount || effectiveAmount <= 0}
                                        className="inline-flex items-center gap-2 rounded-full bg-primary px-5 py-2.5 text-xs font-semibold text-white shadow-lg shadow-primary/30 hover:bg-primary/90 disabled:opacity-40 disabled:cursor-not-allowed"
                                    >
                                        <Gift className="w-4 h-4" />
                                        Xác nhận ủng hộ
                                    </button>
                                </div>
                            </div>
                    </form>
                </div>
            </main>
            <Footer />
        </div>
    );
}

