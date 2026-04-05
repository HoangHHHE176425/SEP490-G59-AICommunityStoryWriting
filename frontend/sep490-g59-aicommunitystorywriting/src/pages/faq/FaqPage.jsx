import { Link } from 'react-router-dom';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { ChevronDown, HelpCircle } from 'lucide-react';

const FAQ_ITEMS = [
    {
        q: 'CSW-AI là gì?',
        a: 'CSW-AI là nền tảng đọc và viết truyện cộng đồng, hỗ trợ tác giả sáng tác cùng công cụ AI (gợi ý chương, đồng sáng tác) và kết nối với độc giả qua bình luận, donate, thư viện cá nhân.',
    },
    {
        q: 'Làm sao để trở thành tác giả và đăng truyện?',
        a: 'Đăng ký tài khoản, đọc và chấp nhận điều khoản dành cho tác giả trong mục chính sách, sau đó mở trang quản lý tác giả để tạo truyện và gửi chương chờ kiểm duyệt theo quy trình nền tảng.',
    },
    {
        q: 'Đồng sáng tác với AI có nghĩa là gì? Nội dung có còn là của tôi không?',
        a: 'AI hỗ trợ gợi ý hướng đi, dàn ý hoặc đoạn văn để bạn chỉnh sửa và quyết định xuất bản. Bạn chịu trách nhiệm về bản cuối cùng được đăng; mức đóng góp AI có thể được ghi nhận trên chương theo chính sách nền tảng.',
    },
    {
        q: 'Xu (coin) trong ví dùng để làm gì? Rút tiền thế nào?',
        a: 'Xu dùng cho các tính năng trên nền tảng (ví dụ: đọc chương trả phí, ủng hộ tác giả). Tác giả có thể yêu cầu rút theo quy định trong phần ví và rút tiền, kèm thông tin tài khoản ngân hàng hợp lệ.',
    },
    {
        q: 'Truyện bị báo cáo / vi phạm thì xử lý ra sao?',
        a: 'Đội kiểm duyệt và xử lý vi phạm xem xét báo cáo theo chính sách cộng đồng. Truyện hoặc bình luận có thể bị ẩn, gắn cờ hoặc tài khoản bị hạn chế tùy mức độ.',
    },
    {
        q: 'Tôi quên mật khẩu hoặc không đăng nhập được?',
        a: 'Dùng chức năng quên mật khẩu trên trang đăng nhập (OTP qua email). Nếu vẫn lỗi, kiểm tra email spam hoặc thử trình duyệt khác.',
    },
    {
        q: 'Tôi cần hỗ trợ thêm ở đâu?',
        a: 'Xem thêm Điều khoản dịch vụ và Chính sách bảo mật trong chân trang. Các vấn đề kỹ thuật nên mô tả kèm ảnh màn hình và thời gian xảy ra để bộ phận vận hành xử lý nhanh hơn.',
    },
];

function FaqAccordionItem({ item, index }) {
    return (
        <details
            className="group border border-slate-200 rounded-xl bg-white shadow-sm open:shadow-md transition-shadow"
        >
            <summary className="flex cursor-pointer list-none items-center justify-between gap-3 px-5 py-4 text-sm font-semibold text-slate-900 marker:content-none">
                <span className="flex items-start gap-3 min-w-0">
                    <span className="flex h-7 w-7 shrink-0 items-center justify-center rounded-lg bg-primary/10 text-xs font-bold text-primary">
                        {index + 1}
                    </span>
                    <span className="pt-0.5 leading-snug">{item.q}</span>
                </span>
                <ChevronDown className="h-5 w-5 shrink-0 text-slate-400 transition-transform group-open:rotate-180" />
            </summary>
            <div className="border-t border-slate-100 px-5 pb-4 pt-0">
                <p className="pl-10 text-sm leading-relaxed text-slate-600">{item.a}</p>
            </div>
        </details>
    );
}

export default function FaqPage() {
    return (
        <div className="min-h-screen bg-slate-50 flex flex-col">
            <Header />
            <main className="flex-1">
                <div className="bg-gradient-to-br from-slate-900 via-slate-800 to-slate-900 text-white">
                    <div className="max-w-[960px] mx-auto px-4 py-14 md:py-20">
                        <div className="inline-flex items-center gap-2 rounded-full border border-white/15 bg-white/5 px-3 py-1 text-xs font-semibold text-primary mb-4">
                            <HelpCircle className="h-3.5 w-3.5" />
                            Hỗ trợ
                        </div>
                        <h1 className="text-3xl md:text-4xl font-bold tracking-tight">Câu hỏi thường gặp</h1>
                        <p className="mt-3 max-w-2xl text-sm md:text-base text-slate-300 leading-relaxed">
                            Giải đáp nhanh về tài khoản, đồng sáng tác AI, xu &amp; rút tiền, và quy tắc cộng đồng. Chi tiết pháp lý
                            xem tại{' '}
                            <Link to="/policy" className="text-primary font-medium hover:underline">
                                Điều khoản &amp; chính sách
                            </Link>
                            .
                        </p>
                    </div>
                </div>

                <div className="max-w-[720px] mx-auto px-4 -mt-6 pb-16">
                    <div className="rounded-2xl border border-slate-200 bg-white p-5 md:p-8 shadow-sm space-y-3">
                        {FAQ_ITEMS.map((item, i) => (
                            <FaqAccordionItem key={item.q} item={item} index={i} />
                        ))}
                    </div>
                </div>
            </main>
            <Footer />
        </div>
    );
}
