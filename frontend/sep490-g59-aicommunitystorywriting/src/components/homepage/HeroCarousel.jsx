export function HeroCarousel() {
    return (
        <section className="relative overflow-hidden rounded-2xl group">
            <div className="flex overflow-x-auto hide-scrollbar snap-x snap-mandatory">
                {/* Slide 1 */}
                <div className="min-w-full snap-center relative aspect-[21/9] md:aspect-[25/9] lg:aspect-[32/9]">
                    <div className="absolute inset-0 bg-gradient-to-r from-black/80 via-black/40 to-transparent z-10"></div>
                    <img
                        alt="Featured Story 1"
                        className="absolute inset-0 w-full h-full object-cover"
                        src="https://images.unsplash.com/photo-1598669266459-eef1467c15be?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxmYW50YXN5JTIwd2FycmlvciUyMGJvb2t8ZW58MXx8fHwxNzY4NDg2MzI5fDA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral"
                    />
                    <div className="relative z-20 h-full flex flex-col justify-center px-8 md:px-16 max-w-2xl gap-4">
                        <span className="inline-block px-3 py-1 bg-primary text-white text-[10px] font-bold uppercase tracking-wider rounded w-fit">
                            Editor's Choice
                        </span>
                        <h2 className="text-3xl md:text-5xl font-extrabold text-white leading-tight">
                            Hành Trình Vạn Cổ: Thần Đạo Đan Tôn
                        </h2>
                        <p className="text-slate-200 text-sm md:text-base line-clamp-2">
                            Khám phá thế giới tiên hiệp đầy kỳ ảo cùng Lăng Hàn, kẻ nắm giữ bí mật của Đan đạo tối thượng.
                        </p>
                        <div className="flex gap-4 pt-2">
                            <button className="px-8 py-3 bg-primary text-white font-bold rounded-lg hover:scale-105 transition-transform">
                                Đọc ngay
                            </button>
                            <button className="px-8 py-3 bg-white/10 hover:bg-white/20 text-white font-bold backdrop-blur-md rounded-lg transition-colors border border-white/20">
                                Chi tiết
                            </button>
                        </div>
                    </div>
                </div>

                {/* Slide 2 */}
                <div className="min-w-full snap-center relative aspect-[21/9] md:aspect-[25/9] lg:aspect-[32/9]">
                    <div className="absolute inset-0 bg-gradient-to-r from-black/80 via-black/40 to-transparent z-10"></div>
                    <img
                        alt="Featured Story 2"
                        className="absolute inset-0 w-full h-full object-cover"
                        src="https://images.unsplash.com/photo-1762554914464-1ea94ff92f49?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxtYWdpYyUyMHNwZWxsJTIwYm9va3xlbnwxfHx8fDE3Njg0ODYzMjl8MA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral"
                    />
                    <div className="relative z-20 h-full flex flex-col justify-center px-8 md:px-16 max-w-2xl gap-4">
                        <span className="inline-block px-3 py-1 bg-blue-500 text-white text-[10px] font-bold uppercase tracking-wider rounded w-fit">
                            New Release
                        </span>
                        <h2 className="text-3xl md:text-5xl font-extrabold text-white leading-tight">
                            Đô Thị Thần Y: Thiên Hạ Vô Song
                        </h2>
                        <p className="text-slate-200 text-sm md:text-base line-clamp-2">
                            Sự trở lại của một vị thần y trong thế giới hiện đại đầy rẫy âm mưu và quyền lực.
                        </p>
                        <div className="flex gap-4 pt-2">
                            <button className="px-8 py-3 bg-primary text-white font-bold rounded-lg hover:scale-105 transition-transform">
                                Đọc ngay
                            </button>
                        </div>
                    </div>
                </div>

                {/* Slide 3 */}
                <div className="min-w-full snap-center relative aspect-[21/9] md:aspect-[25/9] lg:aspect-[32/9]">
                    <div className="absolute inset-0 bg-gradient-to-r from-black/80 via-black/40 to-transparent z-10"></div>
                    <img
                        alt="Featured Story 3"
                        className="absolute inset-0 w-full h-full object-cover"
                        src="https://images.unsplash.com/photo-1610926597998-fc7f2c1b89b0?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHwxfHxteXN0aWNhbCUyMGRyYWdvbnxlbnwxfHx8fDE3Njg0ODYzMzB8MA&ixlib=rb-4.1.0&q=80&w=1080&utm_source=figma&utm_medium=referral"
                    />
                    <div className="relative z-20 h-full flex flex-col justify-center px-8 md:px-16 max-w-2xl gap-4">
                        <span className="inline-block px-3 py-1 bg-red-500 text-white text-[10px] font-bold uppercase tracking-wider rounded w-fit">
                            Hot Series
                        </span>
                        <h2 className="text-3xl md:text-5xl font-extrabold text-white leading-tight">
                            Long Tộc Truyền Kỳ: Thiên Hạ Đệ Nhất
                        </h2>
                        <p className="text-slate-200 text-sm md:text-base line-clamp-2">
                            Huyết mạch long tộc thức tỉnh, mở ra cuộc hành trình chinh phục đại lục huyền huyễn.
                        </p>
                        <div className="flex gap-4 pt-2">
                            <button className="px-8 py-3 bg-primary text-white font-bold rounded-lg hover:scale-105 transition-transform">
                                Đọc ngay
                            </button>
                        </div>
                    </div>
                </div>
            </div>

            {/* Navigation dots */}
            <div className="absolute bottom-6 left-1/2 -translate-x-1/2 z-30 flex gap-2">
                <div className="size-2 rounded-full bg-primary"></div>
                <div className="size-2 rounded-full bg-white/40"></div>
                <div className="size-2 rounded-full bg-white/40"></div>
            </div>
        </section>
    );
}
