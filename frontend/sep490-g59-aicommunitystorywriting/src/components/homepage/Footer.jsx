import { BookOpen, Share2 } from 'lucide-react';

export function Footer() {
    return (
        <footer className="mt-16 bg-white dark:bg-slate-900 border-t border-slate-200 dark:border-slate-800 py-12">
            <div className="max-w-[1280px] mx-auto px-4 grid grid-cols-1 md:grid-cols-4 gap-8">
                <div className="col-span-1 md:col-span-1">
                    <div className="flex items-center gap-2 mb-6">
                        <div className="size-8 bg-primary rounded-lg flex items-center justify-center text-white">
                            <BookOpen className="w-5 h-5" />
                        </div>
                        <span className="text-xl font-bold tracking-tight text-slate-900 dark:text-white">Waka</span>
                    </div>
                    <p className="text-sm text-slate-500 dark:text-slate-400 mb-6">
                        Nền tảng e-book và sáng tác cộng đồng lớn nhất Việt Nam. Nơi lan tỏa tri thức và đam mê viết lách.
                    </p>
                    <div className="flex gap-4">
                        <a className="size-8 rounded-full bg-slate-100 dark:bg-slate-800 flex items-center justify-center text-slate-600 dark:text-slate-400 hover:bg-primary hover:text-white transition-colors" href="#">
                            <Share2 className="w-4 h-4" />
                        </a>
                    </div>
                </div>

                <div>
                    <h4 className="font-bold mb-6 text-white text-sm uppercase tracking-wider">Khám phá</h4>
                    <ul className="flex flex-col gap-3 text-sm text-slate-400">
                        <li><Link to="/home" className="hover:text-primary transition-colors">Trang chủ</Link></li>
                        <li><Link to="/story-list" className="hover:text-primary transition-colors">Khám phá truyện</Link></li>
                        <li><a className="hover:text-primary transition-colors" href="#">Bảng xếp hạng</a></li>
                        <li><a className="hover:text-primary transition-colors" href="#">Review truyện</a></li>
                    </ul>
                </div>

                <div>
                    <h4 className="font-bold mb-6 dark:text-white text-sm uppercase tracking-wider">Hỗ trợ</h4>
                    <ul className="flex flex-col gap-3 text-sm text-slate-500 dark:text-slate-400">
                        <li><a className="hover:text-primary transition-colors" href="#">Điều khoản dịch vụ</a></li>
                        <li><a className="hover:text-primary transition-colors" href="#">Chính sách bảo mật</a></li>
                        <li><a className="hover:text-primary transition-colors" href="#">Câu hỏi thường gặp</a></li>
                        <li><a className="hover:text-primary transition-colors" href="#">Liên hệ bản quyền</a></li>
                    </ul>
                </div>

                <div>
                    <h4 className="font-bold mb-6 dark:text-white text-sm uppercase tracking-wider">Tải ứng dụng</h4>
                    <div className="flex flex-col gap-3">
                        <button className="flex items-center gap-3 px-4 py-2 bg-slate-100 dark:bg-slate-800 rounded-lg hover:bg-slate-200 dark:hover:bg-slate-700 transition-colors w-full sm:w-auto">
                            <div className="w-6 h-6 bg-slate-300 dark:bg-slate-700 rounded"></div>
                            <div className="text-left">
                                <p className="text-[10px] uppercase font-bold text-slate-500">Available on</p>
                                <p className="text-xs font-bold dark:text-white">Google Play</p>
                            </div>
                        </button>
                        <button className="flex items-center gap-3 px-4 py-2 bg-slate-100 dark:bg-slate-800 rounded-lg hover:bg-slate-200 dark:hover:bg-slate-700 transition-colors w-full sm:w-auto">
                            <div className="w-6 h-6 bg-slate-300 dark:bg-slate-700 rounded"></div>
                            <div className="text-left">
                                <p className="text-[10px] uppercase font-bold text-slate-500">Download on</p>
                                <p className="text-xs font-bold dark:text-white">App Store</p>
                            </div>
                        </button>
                    </div>
                </div>
            </div>

            <div className="max-w-[1280px] mx-auto px-4 mt-12 pt-8 border-t border-slate-200 dark:border-slate-800 flex flex-col md:flex-row justify-between items-center gap-4">
                <p className="text-xs text-slate-400">© 2026 Waka Corporation. All rights reserved.</p>
                <div className="flex gap-6 text-xs text-slate-400">
                    <a className="hover:text-primary transition-colors" href="#">Facebook</a>
                    <a className="hover:text-primary transition-colors" href="#">Youtube</a>
                    <a className="hover:text-primary transition-colors" href="#">Zalo</a>
                </div>
            </div>
        </footer>
    );
}