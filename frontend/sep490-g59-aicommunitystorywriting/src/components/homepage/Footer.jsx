import { Link } from 'react-router-dom';
import { BookOpen, Share2, Bot, Users, Award, Shield } from 'lucide-react';

export function Footer() {
    return (
        <footer className="mt-16 bg-slate-900 border-t border-slate-700/50 py-12">
            <div className="max-w-[1280px] mx-auto px-4 grid grid-cols-1 md:grid-cols-4 gap-8">
                <div className="col-span-1 md:col-span-1">
                    <div className="flex items-center gap-2 mb-6">
                        <div className="size-8 bg-primary rounded-lg flex items-center justify-center text-white">
                            <BookOpen className="w-5 h-5" />
                        </div>
                        <span className="text-xl font-bold tracking-tight text-white">CSW_AI</span>
                    </div>
                    <p className="text-sm text-slate-400 mb-6 leading-relaxed">
                        CSW-AI là nền tảng viết truyện cộng đồng đầu tiên tại Việt Nam tích hợp công nghệ trí tuệ nhân tạo, giúp
                        tác giả và độc giả kết nối, sáng tạo và chia sẻ những câu chuyện đầy cảm hứng.
                    </p>
                    <div className="flex gap-4">
                        <a className="size-8 rounded-full bg-slate-800 flex items-center justify-center text-slate-400 hover:bg-primary hover:text-white transition-colors" href="#">
                            <Share2 className="w-4 h-4" />
                        </a>
                    </div>
                </div>

                <div>
                    <h4 className="font-bold mb-6 text-white text-sm uppercase tracking-wider">Khám phá</h4>
                    <ul className="flex flex-col gap-3 text-sm text-slate-400">
                        <li><Link to="/home" className="hover:text-primary transition-colors">Trang chủ</Link></li>
                        <li><Link to="/home" className="hover:text-primary transition-colors">Khám phá truyện</Link></li>
                        <li><a className="hover:text-primary transition-colors" href="#">Bảng xếp hạng</a></li>
                        <li><Link to="/about-us" className="hover:text-primary transition-colors">Về chúng tôi</Link></li>
                    </ul>
                </div>

                <div>
                    <h4 className="font-bold mb-6 text-white text-sm uppercase tracking-wider">Tính năng nổi bật</h4>
                    <ul className="flex flex-col gap-3 text-sm text-slate-400">
                        <li className="flex items-center gap-2">
                            <Bot className="w-4 h-4 text-primary shrink-0" />
                            <span>AI Sáng tạo thông minh</span>
                        </li>
                        <li className="flex items-center gap-2">
                            <Users className="w-4 h-4 text-primary shrink-0" />
                            <span>Cộng đồng sôi động</span>
                        </li>
                        <li className="flex items-center gap-2">
                            <Award className="w-4 h-4 text-primary shrink-0" />
                            <span>Cơ hội thu nhập</span>
                        </li>
                        <li className="flex items-center gap-2">
                            <Shield className="w-4 h-4 text-primary shrink-0" />
                            <span>Bảo vệ tác quyền</span>
                        </li>
                    </ul>
                </div>

                <div>
                    <h4 className="font-bold mb-6 text-white text-sm uppercase tracking-wider">Hỗ trợ</h4>
                    <ul className="flex flex-col gap-3 text-sm text-slate-400">
                        <li><a className="hover:text-primary transition-colors" href="#">Điều khoản dịch vụ</a></li>
                        <li><a className="hover:text-primary transition-colors" href="#">Chính sách bảo mật</a></li>
                        <li><a className="hover:text-primary transition-colors" href="#">Câu hỏi thường gặp</a></li>
                        <li><Link to="/about-us" className="hover:text-primary transition-colors">Về chúng tôi</Link></li>
                        <li><a className="hover:text-primary transition-colors" href="#">Liên hệ bản quyền</a></li>
                    </ul>
                </div>
            </div>

            <div className="max-w-[1280px] mx-auto px-4 mt-12 pt-8 border-t border-slate-700/50 flex flex-col md:flex-row justify-between items-center gap-4">
                <p className="text-xs text-slate-500">© 2026 CSW_AI. All rights reserved.</p>
                <div className="flex gap-6 text-xs text-slate-500">
                    <a className="hover:text-primary transition-colors" href="#">Facebook</a>
                    <a className="hover:text-primary transition-colors" href="#">Youtube</a>
                    <a className="hover:text-primary transition-colors" href="#">Zalo</a>
                </div>
            </div>
        </footer>
    );
}
