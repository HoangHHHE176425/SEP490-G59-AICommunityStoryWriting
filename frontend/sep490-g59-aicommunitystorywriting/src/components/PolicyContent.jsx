import { FileText, Shield, AlertCircle } from 'lucide-react';

export function PolicyContent() {
    return (
        <div className="space-y-6">
            {/* 1. Terms of Service */}
                    <section>
                        <div className="flex items-center gap-2 mb-4">
                            <FileText className="w-5 h-5 text-[#13EC5B]" />
                            <h3 className="text-xl font-bold text-[#1A2332]">
                                1. Điều Khoản Sử Dụng Dịch Vụ
                            </h3>
                        </div>
                        <div className="space-y-4 text-[#90A1B9] leading-relaxed">
                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">1.1. Đăng ký và Quản lý Tài khoản</p>
                                <p className="mb-2">
                                    Khi đăng ký tài khoản trên CSW-AI, bạn cam kết:
                                </p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li>Cung cấp thông tin chính xác, đầy đủ và cập nhật (họ tên, email, số điện thoại nếu có)</li>
                                    <li>Duy trì tính chính xác của thông tin tài khoản và cập nhật khi có thay đổi</li>
                                    <li>Không sử dụng thông tin của người khác hoặc tạo tài khoản giả mạo</li>
                                    <li>Bảo mật thông tin đăng nhập (mật khẩu, token xác thực) và không chia sẻ cho bất kỳ ai</li>
                                    <li>Chịu trách nhiệm hoàn toàn đối với mọi hoạt động diễn ra dưới tài khoản của bạn</li>
                                    <li>Thông báo ngay cho CSW-AI nếu phát hiện tài khoản bị sử dụng trái phép</li>
                                </ul>
                                <p className="mt-2 text-sm italic">
                                    <strong className="text-[#1A2332]">Lưu ý:</strong> CSW-AI không chịu trách nhiệm về bất kỳ tổn thất nào
                                    phát sinh do việc bạn không bảo mật thông tin đăng nhập.
                                </p>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">1.2. Yêu Cầu Về Độ Tuổi và Năng Lực Pháp Lý</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li>Bạn phải từ <strong className="text-[#1A2332]">đủ 13 tuổi trở lên</strong> để đăng ký và sử dụng dịch vụ CSW-AI</li>
                                    <li>Nếu bạn từ 13-17 tuổi, bạn cần có <strong className="text-[#1A2332]">sự đồng ý và giám sát</strong> của phụ huynh
                                        hoặc người giám hộ hợp pháp khi sử dụng dịch vụ</li>
                                    <li>Người dùng dưới 18 tuổi không được thực hiện các giao dịch thanh toán mà không có sự cho phép của phụ huynh</li>
                                    <li>Phụ huynh/người giám hộ chịu trách nhiệm giám sát hoạt động và nội dung mà trẻ vị thành niên tiếp cận</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">1.3. Cam Kết Sử Dụng Hợp Pháp</p>
                                <p className="mb-2">Bạn đồng ý rằng sẽ:</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li>Sử dụng dịch vụ cho mục đích hợp pháp, đúng đắn và tuân thủ pháp luật Việt Nam</li>
                                    <li>Không sử dụng dịch vụ để thực hiện hoặc khuyến khích các hành vi bất hợp pháp</li>
                                    <li>Không can thiệp, phá hoại hoặc gây gián đoạn hoạt động của hệ thống</li>
                                    <li>Không sử dụng bot, script tự động hoặc phương tiện không hợp lệ để truy cập dịch vụ</li>
                                    <li>Không thu thập dữ liệu người dùng khác mà không có sự đồng ý</li>
                                    <li>Không mạo danh cá nhân, tổ chức hoặc đại diện sai sự liên kết với CSW-AI</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">1.4. Giới Hạn Tài Khoản</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li>Mỗi người chỉ được tạo <strong className="text-[#1A2332]">một (01) tài khoản chính</strong></li>
                                    <li>Việc tạo nhiều tài khoản để lạm dụng hệ thống, nhận ưu đãi trùng lặp hoặc spam sẽ bị cấm vĩnh viễn</li>
                                    <li>Tài khoản không được chuyển nhượng, bán hoặc cho thuê cho người khác</li>
                                </ul>
                            </div>
                        </div>
                    </section>

                    {/* 2. Content Policy */}
                    <section>
                        <div className="flex items-center gap-2 mb-4">
                            <FileText className="w-5 h-5 text-[#2B7FFF]" />
                            <h3 className="text-xl font-bold text-[#1A2332]">
                                2. Chính Sách Nội Dung và Quyền Sở Hữu Trí Tuệ
                            </h3>
                        </div>
                        <div className="space-y-4 text-[#90A1B9] leading-relaxed">
                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">2.1. Quyền Sở Hữu Nội Dung</p>
                                <p className="mb-2">
                                    <strong className="text-[#1A2332]">Bạn giữ toàn quyền sở hữu</strong> đối với mọi nội dung (truyện, bình luận,
                                    hình ảnh, v.v.) mà bạn tạo ra và đăng tải lên CSW-AI. Tuy nhiên:
                                </p>
                                <ul className="list-disc list-inside space-y-2 ml-4">
                                    <li>Bằng việc đăng tải nội dung công khai, bạn cấp cho CSW-AI một <strong className="text-[#1A2332]">
                                        giấy phép không độc quyền, miễn phí, có thể chuyển nhượng, toàn cầu</strong> để sử dụng, hiển thị,
                                        phân phối, sao chép và tạo tác phẩm phái sinh từ nội dung đó nhằm mục đích vận hành và quảng bá dịch vụ</li>
                                    <li>Giấy phép này <strong className="text-[#1A2332]">không bao gồm quyền bán</strong> nội dung của bạn
                                        cho bên thứ ba mà không có sự đồng ý của bạn</li>
                                    <li>Nếu bạn xóa nội dung, giấy phép sẽ chấm dứt sau một khoảng thời gian hợp lý (tối đa 30 ngày) để
                                        xử lý các vấn đề kỹ thuật và cache</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">2.2. Nội Dung Nghiêm Cấm</p>
                                <p className="mb-2">
                                    CSW-AI nghiêm cấm đăng tải các loại nội dung sau:
                                </p>

                                <div className="space-y-3 ml-4">
                                    <div>
                                        <p className="font-semibold text-[#1A2332]">a) Nội dung vi phạm pháp luật:</p>
                                        <ul className="list-disc list-inside space-y-1 ml-4 mt-1">
                                            <li>Chống phá nhà nước, xuyên tạc lịch sử, kích động bạo lực</li>
                                            <li>Xúc phạm danh dự, uy tín của tổ chức, cá nhân</li>
                                            <li>Tuyên truyền chiến tranh, khủng bố, tội phạm</li>
                                            <li>Vi phạm an ninh quốc gia, trật tự an toàn xã hội</li>
                                        </ul>
                                    </div>

                                    <div>
                                        <p className="font-semibold text-[#1A2332]">b) Nội dung khiêu dâm và bạo lực:</p>
                                        <ul className="list-disc list-inside space-y-1 ml-4 mt-1">
                                            <li>Hình ảnh, văn bản khiêu dâm, kích dục (18+) không được phép</li>
                                            <li>Nội dung tình dục liên quan đến trẻ em dưới mọi hình thức</li>
                                            <li>Bạo lực đẫm máu, tra tấn, ngược đãi động vật</li>
                                            <li>Cảnh tự tử, tự gây thương tích chi tiết</li>
                                        </ul>
                                    </div>

                                    <div>
                                        <p className="font-semibold text-[#1A2332]">c) Nội dung phân biệt đối xử và thù ghét:</p>
                                        <ul className="list-disc list-inside space-y-1 ml-4 mt-1">
                                            <li>Kỳ thị chủng tộc, dân tộc, tôn giáo, giới tính, khuynh hướng tình dục</li>
                                            <li>Kích động thù ghét, phân biệt đối xử với nhóm người cụ thể</li>
                                            <li>Quấy rối, bắt nạt, đe dọa người dùng khác</li>
                                        </ul>
                                    </div>

                                    <div>
                                        <p className="font-semibold text-[#1A2332]">d) Vi phạm quyền sở hữu trí tuệ:</p>
                                        <ul className="list-disc list-inside space-y-1 ml-4 mt-1">
                                            <li>Sao chép toàn bộ hoặc phần lớn tác phẩm của người khác mà không có phép</li>
                                            <li>Đăng tải nội dung có bản quyền (sách, phim, nhạc) mà không được ủy quyền</li>
                                            <li>Sử dụng hình ảnh, logo, thương hiệu của người khác trái phép</li>
                                        </ul>
                                    </div>

                                    <div>
                                        <p className="font-semibold text-[#1A2332]">e) Spam và lừa đảo:</p>
                                        <ul className="list-disc list-inside space-y-1 ml-4 mt-1">
                                            <li>Quảng cáo thương mại, spam liên kết ngoài không liên quan</li>
                                            <li>Lừa đảo, đa cấp, kiếm tiền bất hợp pháp</li>
                                            <li>Phishing, thu thập thông tin cá nhân trái phép</li>
                                            <li>Virus, malware, mã độc hại</li>
                                        </ul>
                                    </div>

                                    <div>
                                        <p className="font-semibold text-[#1A2332]">f) Thông tin sai lệch và đáng lo ngại:</p>
                                        <ul className="list-disc list-inside space-y-1 ml-4 mt-1">
                                            <li>Tin giả, thông tin sai lệch gây hoang mang dư luận</li>
                                            <li>Hướng dẫn làm vũ khí, chế tạo chất nổ, ma túy</li>
                                            <li>Khuyến khích tự tử, tự gây thương tích, rối loạn ăn uống</li>
                                        </ul>
                                    </div>
                                </div>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">2.3. Quy Trình Kiểm Duyệt Nội Dung</p>
                                <ul className="list-disc list-inside space-y-2 ml-4">
                                    <li><strong className="text-[#1A2332]">Kiểm duyệt tự động:</strong> Hệ thống AI sẽ quét và phát hiện
                                        nội dung vi phạm ngay khi đăng tải</li>
                                    <li><strong className="text-[#1A2332]">Kiểm duyệt thủ công:</strong> Đội ngũ moderator sẽ xem xét
                                        các báo cáo từ người dùng trong vòng 24-48 giờ</li>
                                    <li><strong className="text-[#1A2332]">Hành động xử lý:</strong> Tùy mức độ vi phạm, nội dung có thể bị:
                                        <ul className="list-disc list-inside space-y-1 ml-6 mt-1">
                                            <li>Cảnh báo và yêu cầu chỉnh sửa</li>
                                            <li>Ẩn khỏi công chúng (chỉ bạn thấy)</li>
                                            <li>Xóa vĩnh viễn</li>
                                            <li>Khóa tài khoản tạm thời (7-30 ngày)</li>
                                            <li>Khóa tài khoản vĩnh viễn (vi phạm nghiêm trọng)</li>
                                        </ul>
                                    </li>
                                    <li>Bạn có quyền <strong className="text-[#1A2332]">khiếu nại</strong> quyết định kiểm duyệt trong vòng 7 ngày
                                        qua email support@csw-ai.com</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">2.4. Báo Cáo Vi Phạm</p>
                                <p className="mb-2">
                                    Nếu bạn phát hiện nội dung vi phạm, vui lòng báo cáo ngay bằng cách:
                                </p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li>Nhấn nút &quot;Báo cáo&quot; trên nội dung đó</li>
                                    <li>Chọn lý do vi phạm cụ thể</li>
                                    <li>Cung cấp thêm thông tin chi tiết nếu cần</li>
                                </ul>
                                <p className="mt-2 text-sm">
                                    Chúng tôi cam kết bảo mật danh tính người báo cáo và xử lý trong thời gian sớm nhất.
                                </p>
                            </div>
                        </div>
                    </section>

                    {/* 3. Privacy Policy */}
                    <section>
                        <div className="flex items-center gap-2 mb-4">
                            <Shield className="w-5 h-5 text-[#FFA500]" />
                            <h3 className="text-xl font-bold text-[#1A2332]">
                                3. Chính Sách Bảo Mật Thông Tin Cá Nhân
                            </h3>
                        </div>
                        <div className="space-y-4 text-[#90A1B9] leading-relaxed">
                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">3.1. Thông Tin Chúng Tôi Thu Thập</p>
                                <div className="space-y-2 ml-4">
                                    <div>
                                        <p className="font-semibold text-[#1A2332]">a) Thông tin bạn cung cấp:</p>
                                        <ul className="list-disc list-inside space-y-1 ml-4 mt-1">
                                            <li>Thông tin đăng ký: Họ tên, email, mật khẩu, ngày sinh</li>
                                            <li>Thông tin hồ sơ: Ảnh đại diện, tiểu sử, liên kết mạng xã hội</li>
                                            <li>Nội dung tạo ra: Truyện, bình luận, đánh giá, tin nhắn</li>
                                            <li>Thông tin thanh toán: Thông tin thẻ, tài khoản ngân hàng (được mã hóa bởi đối tác thanh toán)</li>
                                        </ul>
                                    </div>

                                    <div>
                                        <p className="font-semibold text-[#1A2332]">b) Thông tin tự động thu thập:</p>
                                        <ul className="list-disc list-inside space-y-1 ml-4 mt-1">
                                            <li>Dữ liệu sử dụng: Lịch sử đọc, thời gian trực tuyến, tương tác với nội dung</li>
                                            <li>Dữ liệu kỹ thuật: Địa chỉ IP, loại thiết bị, trình duyệt, hệ điều hành</li>
                                            <li>Cookie và công nghệ tracking tương tự</li>
                                            <li>Dữ liệu vị trí (nếu bạn cho phép)</li>
                                        </ul>
                                    </div>

                                    <div>
                                        <p className="font-semibold text-[#1A2332]">c) Thông tin từ bên thứ ba:</p>
                                        <ul className="list-disc list-inside space-y-1 ml-4 mt-1">
                                            <li>Thông tin từ Google, Facebook khi bạn đăng nhập qua mạng xã hội</li>
                                            <li>Thông tin từ đối tác thanh toán (đã được mã hóa)</li>
                                        </ul>
                                    </div>
                                </div>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">3.2. Mục Đích Sử Dụng Thông Tin</p>
                                <p className="mb-2">Thông tin của bạn được sử dụng để:</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li><strong className="text-[#1A2332]">Cung cấp dịch vụ:</strong> Tạo tài khoản, lưu trữ nội dung,
                                        xử lý giao dịch, hỗ trợ khách hàng</li>
                                    <li><strong className="text-[#1A2332]">Cá nhân hóa trải nghiệm:</strong> Gợi ý truyện phù hợp,
                                        hiển thị nội dung liên quan, tùy chỉnh giao diện</li>
                                    <li><strong className="text-[#1A2332]">Giao tiếp:</strong> Gửi thông báo quan trọng, cập nhật dịch vụ,
                                        tin tức và khuyến mãi (bạn có thể hủy đăng ký)</li>
                                    <li><strong className="text-[#1A2332]">Phân tích và cải thiện:</strong> Nghiên cứu hành vi người dùng,
                                        tối ưu hiệu suất, phát triển tính năng mới</li>
                                    <li><strong className="text-[#1A2332]">Bảo mật:</strong> Phát hiện gian lận, ngăn chặn lạm dụng,
                                        bảo vệ quyền lợi người dùng</li>
                                    <li><strong className="text-[#1A2332]">Tuân thủ pháp luật:</strong> Đáp ứng yêu cầu pháp lý,
                                        giải quyết tranh chấp</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">3.3. Bảo Mật Thông Tin</p>
                                <p className="mb-2">Chúng tôi áp dụng các biện pháp bảo mật tiên tiến:</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li><strong className="text-[#1A2332]">Mã hóa:</strong> SSL/TLS cho truyền tải dữ liệu,
                                        AES-256 cho lưu trữ</li>
                                    <li><strong className="text-[#1A2332]">Xác thực:</strong> Two-factor authentication (2FA),
                                        hashing mật khẩu với bcrypt</li>
                                    <li><strong className="text-[#1A2332]">Kiểm soát truy cập:</strong> Phân quyền chặt chẽ,
                                        chỉ nhân viên được ủy quyền mới truy cập dữ liệu</li>
                                    <li><strong className="text-[#1A2332]">Giám sát:</strong> Theo dõi 24/7, phát hiện và phản ứng
                                        nhanh với sự cố bảo mật</li>
                                    <li><strong className="text-[#1A2332]">Sao lưu:</strong> Backup dữ liệu định kỳ,
                                        disaster recovery plan</li>
                                </ul>
                                <p className="mt-2 text-sm italic">
                                    <strong className="text-[#1A2332]">Lưu ý:</strong> Mặc dù chúng tôi nỗ lực cao nhất,
                                    không có hệ thống nào an toàn tuyệt đối 100%. Bạn cũng cần bảo vệ thông tin cá nhân của mình.
                                </p>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">3.4. Chia Sẻ Thông Tin</p>
                                <p className="mb-2">
                                    Chúng tôi <strong className="text-[#1A2332]">không bán hoặc cho thuê</strong> thông tin cá nhân của bạn.
                                    Thông tin chỉ được chia sẻ trong các trường hợp:
                                </p>
                                <ul className="list-disc list-inside space-y-2 ml-4">
                                    <li><strong className="text-[#1A2332]">Với sự đồng ý của bạn:</strong> Khi bạn chủ động cho phép
                                        chia sẻ thông tin với bên thứ ba</li>
                                    <li><strong className="text-[#1A2332]">Nhà cung cấp dịch vụ:</strong> Đối tác thanh toán,
                                        hosting, analytics (theo NDA và cam kết bảo mật)</li>
                                    <li><strong className="text-[#1A2332]">Yêu cầu pháp lý:</strong> Khi có lệnh từ cơ quan có thẩm quyền,
                                        phòng chống tội phạm</li>
                                    <li><strong className="text-[#1A2332]">Bảo vệ quyền lợi:</strong> Trong trường hợp tranh chấp pháp lý,
                                        điều tra gian lận</li>
                                    <li><strong className="text-[#1A2332]">Chuyển giao doanh nghiệp:</strong> Trong trường hợp sáp nhập,
                                        mua bán (bạn sẽ được thông báo trước)</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">3.5. Quyền Của Bạn</p>
                                <p className="mb-2">Bạn có các quyền sau đối với dữ liệu cá nhân:</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li><strong className="text-[#1A2332]">Truy cập:</strong> Xem thông tin cá nhân chúng tôi lưu trữ</li>
                                    <li><strong className="text-[#1A2332]">Chỉnh sửa:</strong> Cập nhật, sửa đổi thông tin không chính xác</li>
                                    <li><strong className="text-[#1A2332]">Xóa:</strong> Yêu cầu xóa tài khoản và dữ liệu (trừ dữ liệu phải lưu theo pháp luật)</li>
                                    <li><strong className="text-[#1A2332]">Hạn chế xử lý:</strong> Giới hạn cách chúng tôi sử dụng dữ liệu của bạn</li>
                                    <li><strong className="text-[#1A2332]">Tải xuống:</strong> Nhận bản sao dữ liệu ở định dạng phổ thông (data portability)</li>
                                    <li><strong className="text-[#1A2332]">Phản đối:</strong> Từ chối việc xử lý dữ liệu cho mục đích marketing</li>
                                    <li><strong className="text-[#1A2332]">Rút đồng ý:</strong> Hủy sự đồng ý đã cấp bất cứ lúc nào</li>
                                </ul>
                                <p className="mt-2 text-sm">
                                    Để thực hiện các quyền này, vui lòng liên hệ: <strong className="text-[#1A2332]">privacy@csw-ai.com</strong>
                                </p>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">3.6. Cookie và Công Nghệ Tracking</p>
                                <p className="mb-2">Chúng tôi sử dụng cookie và công nghệ tương tự để:</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li>Ghi nhớ đăng nhập và tùy chọn của bạn</li>
                                    <li>Phân tích traffic và hành vi người dùng</li>
                                    <li>Hiển thị quảng cáo phù hợp (nếu có)</li>
                                </ul>
                                <p className="mt-2">
                                    Bạn có thể quản lý cookie qua cài đặt trình duyệt. Tuy nhiên, việc vô hiệu hóa cookie
                                    có thể ảnh hưởng đến một số tính năng của dịch vụ.
                                </p>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">3.7. Lưu Trữ Dữ Liệu</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li>Dữ liệu được lưu trữ tại <strong className="text-[#1A2332]">máy chủ tại Việt Nam</strong>
                                        và các trung tâm dữ liệu quốc tế tuân thủ tiêu chuẩn bảo mật</li>
                                    <li>Thời gian lưu trữ: Cho đến khi bạn xóa tài khoản hoặc theo quy định pháp luật (tối thiểu 90 ngày sau khi xóa)</li>
                                    <li>Sau khi xóa tài khoản, dữ liệu sẽ được anonymized hoặc xóa vĩnh viễn trong vòng 30 ngày</li>
                                </ul>
                            </div>
                        </div>
                    </section>

                    {/* 4. AI Usage Policy */}
                    <section>
                        <div className="flex items-center gap-2 mb-4">
                            <FileText className="w-5 h-5 text-[#FB2C36]" />
                            <h3 className="text-xl font-bold text-[#1A2332]">
                                4. Chính Sách Sử Dụng Công Nghệ AI
                            </h3>
                        </div>
                        <div className="space-y-4 text-[#90A1B9] leading-relaxed">
                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">4.1. Vai Trò của AI trong CSW-AI</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li><strong className="text-[#1A2332]">AI là công cụ hỗ trợ:</strong> Công nghệ AI (GPT-4, Gemini)
                                        được tích hợp để hỗ trợ sáng tạo nội dung, gợi ý ý tưởng, phát triển cốt truyện và cải thiện văn phong</li>
                                    <li><strong className="text-[#1A2332]">Không thay thế con người:</strong> AI không được thiết kế
                                        để thay thế hoàn toàn sự sáng tạo của con người, mà chỉ là trợ lý thông minh</li>
                                    <li><strong className="text-[#1A2332]">Tính minh bạch:</strong> Nội dung được tạo với sự hỗ trợ của AI
                                        có thể được đánh dấu để độc giả nhận biết (tùy chọn)</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">4.2. Trách Nhiệm Của Tác Giả</p>
                                <p className="mb-2">Khi sử dụng AI để hỗ trợ viết truyện, bạn cam kết:</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li><strong className="text-[#1A2332]">Chịu trách nhiệm toàn bộ:</strong> Bạn hoàn toàn chịu trách nhiệm
                                        về nội dung cuối cùng được xuất bản, kể cả khi có sử dụng hỗ trợ từ AI</li>
                                    <li><strong className="text-[#1A2332]">Kiểm tra chất lượng:</strong> Xem xét, chỉnh sửa và đảm bảo
                                        nội dung do AI tạo ra phù hợp với ý định sáng tạo của bạn</li>
                                    <li><strong className="text-[#1A2332]">Tuân thủ pháp luật:</strong> Đảm bảo nội dung không vi phạm
                                        quyền tác giả, không chứa thông tin sai lệch hoặc nội dung bất hợp pháp</li>
                                    <li><strong className="text-[#1A2332]">Tránh lạm dụng:</strong> Không sử dụng AI để tạo spam,
                                        nội dung vi phạm hoặc sao chép tác phẩm của người khác</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">4.3. Dữ Liệu Huấn Luyện và Cải Thiện AI</p>
                                <ul className="list-disc list-inside space-y-2 ml-4">
                                    <li><strong className="text-[#1A2332]">Thu thập dữ liệu:</strong> Nội dung bạn tạo và tương tác
                                        với AI có thể được thu thập để cải thiện mô hình AI trong tương lai</li>
                                    <li><strong className="text-[#1A2332]">Ẩn danh hóa:</strong> Dữ liệu được xử lý ở dạng ẩn danh và
                                        tổng hợp, không bao gồm thông tin cá nhân nhận dạng</li>
                                    <li><strong className="text-[#1A2332]">Quyền từ chối:</strong> Bạn có thể từ chối việc sử dụng
                                        dữ liệu của mình để huấn luyện AI bằng cách liên hệ với chúng tôi</li>
                                    <li><strong className="text-[#1A2332]">Bảo mật:</strong> Dữ liệu tương tác với AI được mã hóa
                                        và lưu trữ an toàn theo tiêu chuẩn bảo mật cao nhất</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">4.4. Giới Hạn và Miễn Trách</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li>AI có thể tạo ra nội dung không chính xác, không phù hợp hoặc gây hiểu lầm</li>
                                    <li>CSW-AI không đảm bảo chất lượng, tính nguyên bản hoặc phù hợp pháp lý của nội dung do AI tạo ra</li>
                                    <li>Chúng tôi không chịu trách nhiệm về bất kỳ tổn thất nào phát sinh từ việc sử dụng AI</li>
                                    <li>AI có thể tạm thời không khả dụng do bảo trì, nâng cấp hoặc sự cố kỹ thuật</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">4.5. Quyền Sở Hữu Nội Dung AI</p>
                                <p>
                                    Nội dung được tạo ra bởi AI với sự tham gia sáng tạo của bạn (prompts, chỉnh sửa, sắp xếp) được coi là
                                    <strong className="text-[#1A2332]"> tác phẩm của bạn</strong>. Tuy nhiên, nội dung hoàn toàn do AI tạo ra
                                    mà không có sự can thiệp sáng tạo có thể không được bảo hộ bản quyền theo pháp luật hiện hành.
                                </p>
                            </div>
                        </div>
                    </section>

                    {/* 5. Payment & Monetization */}
                    <section>
                        <div className="flex items-center gap-2 mb-4">
                            <FileText className="w-5 h-5 text-[#13EC5B]" />
                            <h3 className="text-xl font-bold text-[#1A2332]">
                                5. Thanh Toán, Kiếm Tiền và Giao Dịch Tài Chính
                            </h3>
                        </div>
                        <div className="space-y-4 text-[#90A1B9] leading-relaxed">
                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">5.1. Hệ Thống Coin</p>
                                <ul className="list-disc list-inside space-y-2 ml-4">
                                    <li><strong className="text-[#1A2332]">Coin là gì:</strong> Coin là đơn vị tiền tệ ảo trong CSW-AI,
                                        được sử dụng để mở khóa chương trả phí, đề cử tác giả và mua các dịch vụ trên nền tảng</li>
                                    <li><strong className="text-[#1A2332]">Tỷ giá:</strong> 1 Coin = 1,000 VNĐ (có thể thay đổi, sẽ được thông báo trước)</li>
                                    <li><strong className="text-[#1A2332]">Phương thức nạp:</strong> Thẻ ATM nội địa, Visa/Master Card,
                                        Momo, ZaloPay, chuyển khoản ngân hàng</li>
                                    <li><strong className="text-[#1A2332]">Coin không có giá trị thực:</strong> Coin không thể đổi lại thành tiền mặt
                                        và không có giá trị pháp lý bên ngoài nền tảng CSW-AI</li>
                                    <li><strong className="text-[#1A2332]">Thời hạn:</strong> Coin không có thời hạn sử dụng,
                                        trừ khi tài khoản bị khóa hoặc xóa</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">5.2. Chính Sách Hoàn Tiền</p>
                                <ul className="list-disc list-inside space-y-2 ml-4">
                                    <li><strong className="text-[#1A2332]">Hoàn tiền trong vòng 24h:</strong> Nếu bạn nạp nhầm hoặc
                                        gặp lỗi kỹ thuật, có thể yêu cầu hoàn tiền trong vòng 24 giờ kể từ khi giao dịch thành công</li>
                                    <li><strong className="text-[#1A2332]">Không hoàn tiền sau khi sử dụng:</strong> Coin đã sử dụng
                                        để mở khóa chương hoặc đề cử không được hoàn lại</li>
                                    <li><strong className="text-[#1A2332]">Quy trình:</strong> Gửi email đến billing@csw-ai.com kèm
                                        mã giao dịch, lý do và bằng chứng. Xử lý trong vòng 5-7 ngày làm việc</li>
                                    <li><strong className="text-[#1A2332]">Phí hoàn tiền:</strong> Có thể áp dụng phí xử lý 5% cho
                                        một số phương thức thanh toán</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">5.3. Thu Nhập Từ Truyện (Dành Cho Tác Giả)</p>
                                <div className="space-y-2 ml-4">
                                    <p className="font-semibold text-[#1A2332]">a) Chương trả phí:</p>
                                    <ul className="list-disc list-inside space-y-1 ml-4 mt-1">
                                        <li>Tác giả được đặt giá cho từng chương (tối thiểu 10 coin, tối đa 1000 coin)</li>
                                        <li>Tác giả nhận <strong className="text-[#1A2332]">70%</strong> doanh thu từ chương trả phí
                                            (CSW-AI giữ 30% phí dịch vụ)</li>
                                        <li>Doanh thu được cập nhật realtime sau mỗi giao dịch</li>
                                    </ul>

                                    <p className="font-semibold text-[#1A2332] mt-3">b) Hệ thống đề cử:</p>
                                    <ul className="list-disc list-inside space-y-1 ml-4 mt-1">
                                        <li>Độc giả có thể đề cử truyện bằng coin (10 - 10,000 coin)</li>
                                        <li>Tác giả nhận <strong className="text-[#1A2332]">85%</strong> số coin đề cử</li>
                                        <li>Đề cử giúp truyện được hiển thị nổi bật trên trang chủ</li>
                                    </ul>

                                    <p className="font-semibold text-[#1A2332] mt-3">c) Chương trình Premium:</p>
                                    <ul className="list-disc list-inside space-y-1 ml-4 mt-1">
                                        <li>Tác giả có thể ký hợp đồng độc quyền với CSW-AI</li>
                                        <li>Nhận thu nhập cố định + % doanh thu cao hơn (lên đến 80%)</li>
                                        <li>Được hỗ trợ marketing, biên tập chuyên nghiệp</li>
                                    </ul>

                                    <p className="font-semibold text-[#1A2332] mt-3">d) Quảng cáo (sắp ra mắt):</p>
                                    <ul className="list-disc list-inside space-y-1 ml-4 mt-1">
                                        <li>Truyện có lượt đọc cao sẽ được chạy quảng cáo</li>
                                        <li>Chia sẻ 50% doanh thu quảng cáo với tác giả</li>
                                    </ul>
                                </div>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">5.4. Rút Tiền</p>
                                <ul className="list-disc list-inside space-y-2 ml-4">
                                    <li><strong className="text-[#1A2332]">Điều kiện:</strong> Thu nhập tối thiểu 100,000 VNĐ mới được rút</li>
                                    <li><strong className="text-[#1A2332]">Phương thức:</strong> Chuyển khoản ngân hàng (trong nước),
                                        Momo, ZaloPay</li>
                                    <li><strong className="text-[#1A2332]">Thời gian xử lý:</strong> 7-14 ngày làm việc sau khi duyệt yêu cầu</li>
                                    <li><strong className="text-[#1A2332]">Phí giao dịch:</strong>
                                        <ul className="list-disc list-inside space-y-1 ml-6 mt-1">
                                            <li>Chuyển khoản ngân hàng: Miễn phí</li>
                                            <li>Ví điện tử (Momo, ZaloPay): 2% phí xử lý</li>
                                            <li>Chuyển khoản quốc tế: 5% + phí ngân hàng trung gian</li>
                                        </ul>
                                    </li>
                                    <li><strong className="text-[#1A2332]">Giới hạn:</strong> Tối đa 2 lần rút/tháng,
                                        mỗi lần tối đa 50,000,000 VNĐ</li>
                                    <li><strong className="text-[#1A2332]">Thuế thu nhập:</strong> Tác giả tự chịu trách nhiệm
                                        khai báo và nộp thuế thu nhập cá nhân theo quy định pháp luật Việt Nam</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">5.5. Bảo Mật Thông Tin Thanh Toán</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li>Mọi giao dịch được mã hóa bằng SSL/TLS 256-bit</li>
                                    <li>Thông tin thẻ tín dụng được xử lý bởi đối tác thanh toán được chứng nhận PCI-DSS</li>
                                    <li>CSW-AI không lưu trữ thông tin thẻ tín dụng đầy đủ trên hệ thống</li>
                                    <li>Xác thực 2 lớp (3D Secure) cho tất cả giao dịch thẻ quốc tế</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">5.6. Phát Hiện và Ngăn Chặn Gian Lận</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li>Hệ thống tự động phát hiện giao dịch bất thường và đình chỉ tài khoản nghi ngờ</li>
                                    <li>Tài khoản có hành vi gian lận (bot view, fake donation) sẽ bị khóa vĩnh viễn
                                        và thu hồi toàn bộ thu nhập</li>
                                    <li>Chúng tôi hợp tác với cơ quan chức năng để xử lý các hành vi lừa đảo</li>
                                </ul>
                            </div>
                        </div>
                    </section>

                    {/* 6. Limitation of Liability */}
                    <section>
                        <div className="flex items-center gap-2 mb-4">
                            <AlertCircle className="w-5 h-5 text-[#2B7FFF]" />
                            <h3 className="text-xl font-bold text-[#1A2332]">
                                6. Giới Hạn Trách Nhiệm và Miễn Trừ
                            </h3>
                        </div>
                        <div className="space-y-4 text-[#90A1B9] leading-relaxed">
                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">6.1. Tính Sẵn Có Của Dịch Vụ</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li>Chúng tôi cố gắng duy trì dịch vụ hoạt động 24/7, nhưng <strong className="text-[#1A2332]">
                                        không đảm bảo 100%</strong> không gián đoạn hoặc không có lỗi</li>
                                    <li>Dịch vụ có thể tạm ngưng do bảo trì, nâng cấp, sự cố kỹ thuật hoặc yêu cầu pháp lý</li>
                                    <li>CSW-AI không chịu trách nhiệm về bất kỳ tổn thất nào do dịch vụ gián đoạn</li>
                                    <li>Chúng tôi sẽ cố gắng thông báo trước ít nhất 24 giờ về bảo trì có kế hoạch</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">6.2. Nội Dung Người Dùng</p>
                                <ul className="list-disc list-inside space-y-2 ml-4">
                                    <li>CSW-AI là nền tảng cung cấp dịch vụ, <strong className="text-[#1A2332]">
                                        không phải là nhà xuất bản</strong> nội dung</li>
                                    <li>Chúng tôi không chịu trách nhiệm về:
                                        <ul className="list-disc list-inside space-y-1 ml-6 mt-1">
                                            <li>Chất lượng, độ chính xác, tính hợp pháp của nội dung do người dùng tạo ra</li>
                                            <li>Vi phạm bản quyền, danh dự, uy tín do nội dung người dùng gây ra</li>
                                            <li>Tranh chấp giữa người dùng với nhau</li>
                                            <li>Tổn thất do tin tưởng vào nội dung trên nền tảng</li>
                                        </ul>
                                    </li>
                                    <li>Mỗi người dùng phải <strong className="text-[#1A2332]">tự chịu trách nhiệm</strong>
                                        về nội dung mình tạo ra và đăng tải</li>
                                    <li>Chúng tôi chỉ can thiệp khi có báo cáo vi phạm và chứng cứ rõ ràng</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">6.3. Giới Hạn Bồi Thường</p>
                                <p className="mb-2">
                                    Trong mọi trường hợp, trách nhiệm tối đa của CSW-AI đối với bạn sẽ không vượt quá:
                                </p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li>Số tiền bạn đã thanh toán cho CSW-AI trong 12 tháng gần nhất, HOẶC</li>
                                    <li>1,000,000 VNĐ (một triệu đồng),</li>
                                    <li><strong className="text-[#1A2332]">Tùy theo số nào thấp hơn</strong></li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">6.4. Miễn Trừ Các Loại Thiệt Hại</p>
                                <p className="mb-2">CSW-AI và các đối tác không chịu trách nhiệm về:</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li>Thiệt hại gián tiếp, ngẫu nhiên, đặc biệt, trừng phạt hoặc do hậu quả</li>
                                    <li>Mất mát về doanh thu, lợi nhuận, danh tiếng hoặc cơ hội kinh doanh</li>
                                    <li>Mất mát hoặc hư hỏng dữ liệu</li>
                                    <li>Việc sử dụng hoặc không thể sử dụng dịch vụ</li>
                                    <li>Hành vi của người dùng khác trên nền tảng</li>
                                </ul>
                                <p className="mt-2 text-sm italic">
                                    <strong className="text-[#1A2332]">Lưu ý:</strong> Một số khu vực pháp lý không cho phép loại trừ
                                    hoặc giới hạn trách nhiệm đối với một số thiệt hại nhất định. Trong trường hợp đó, trách nhiệm
                                    của chúng tôi sẽ được giới hạn ở mức tối đa được pháp luật cho phép.
                                </p>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">6.5. Bảo Đảm</p>
                                <p className="mb-2">Dịch vụ được cung cấp trên cơ sở <strong className="text-[#1A2332]">"NGUYÊN TRẠNG"</strong> và
                                    <strong className="text-[#1A2332]"> "NHƯ CÓ"</strong>. Chúng tôi không bảo đảm rằng:</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li>Dịch vụ sẽ hoạt động liên tục, an toàn, không có lỗi</li>
                                    <li>Kết quả thu được từ việc sử dụng dịch vụ sẽ chính xác hoặc đáng tin cậy</li>
                                    <li>Chất lượng của dịch vụ sẽ đáp ứng mong đợi của bạn</li>
                                    <li>Bất kỳ lỗi nào sẽ được khắc phục</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">6.6. Liên Kết Bên Ngoài</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li>Dịch vụ có thể chứa liên kết đến các website bên thứ ba</li>
                                    <li>Chúng tôi không kiểm soát và không chịu trách nhiệm về nội dung, chính sách bảo mật
                                        hoặc hoạt động của bất kỳ website bên thứ ba nào</li>
                                    <li>Bạn tự chịu rủi ro khi truy cập các liên kết bên ngoài</li>
                                </ul>
                            </div>
                        </div>
                    </section>

                    {/* 7. Account Termination */}
                    <section>
                        <div className="flex items-center gap-2 mb-4">
                            <FileText className="w-5 h-5 text-[#FFA500]" />
                            <h3 className="text-xl font-bold text-[#1A2332]">
                                7. Chấm Dứt Tài Khoản và Hậu Quả
                            </h3>
                        </div>
                        <div className="space-y-4 text-[#90A1B9] leading-relaxed">
                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">7.1. Chấm Dứt Do Người Dùng</p>
                                <ul className="list-disc list-inside space-y-2 ml-4">
                                    <li><strong className="text-[#1A2332]">Quyền xóa tài khoản:</strong> Bạn có thể xóa tài khoản
                                        bất cứ lúc nào thông qua Cài đặt → Xóa tài khoản</li>
                                    <li><strong className="text-[#1A2332]">Xác nhận:</strong> Bạn cần nhập mật khẩu và xác nhận qua email
                                        trước khi xóa</li>
                                    <li><strong className="text-[#1A2332]">Thời gian xử lý:</strong> Tài khoản sẽ bị vô hiệu hóa ngay lập tức,
                                        và được xóa vĩnh viễn sau 30 ngày (có thể khôi phục trong thời gian này)</li>
                                    <li><strong className="text-[#1A2332]">Hậu quả:</strong> Tất cả dữ liệu cá nhân, nội dung,
                                        coin chưa sử dụng sẽ bị xóa vĩnh viễn và không thể khôi phục</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">7.2. Đình Chỉ Do CSW-AI</p>
                                <p className="mb-2">Chúng tôi có quyền đình chỉ tạm thời hoặc vĩnh viễn tài khoản của bạn nếu:</p>
                                <ul className="list-disc list-inside space-y-2 ml-4">
                                    <li>Vi phạm bất kỳ điều khoản nào trong tài liệu này</li>
                                    <li>Đăng tải nội dung vi phạm pháp luật, xâm phạm quyền của người khác</li>
                                    <li>Có hành vi gian lận, lừa đảo, lạm dụng hệ thống</li>
                                    <li>Sử dụng bot, script tự động hoặc các phương tiện không hợp lệ</li>
                                    <li>Tạo nhiều tài khoản để spam hoặc lạm dụng</li>
                                    <li>Có yêu cầu từ cơ quan chức năng</li>
                                    <li>Không thanh toán đúng hạn (đối với dịch vụ trả phí)</li>
                                </ul>
                                <p className="mt-2 text-sm">
                                    <strong className="text-[#1A2332]">Lưu ý:</strong> Chúng tôi không bắt buộc phải thông báo trước
                                    hoặc đưa ra lý do cụ thể khi đình chỉ tài khoản vi phạm nghiêm trọng.
                                </p>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">7.3. Xử Lý Vi Phạm</p>
                                <div className="space-y-2 ml-4">
                                    <p className="font-semibold text-[#1A2332]">Tùy mức độ vi phạm, chúng tôi sẽ áp dụng:</p>
                                    <ul className="list-disc list-inside space-y-1 ml-4 mt-1">
                                        <li><strong className="text-[#1A2332]">Cảnh báo lần 1:</strong> Gửi email cảnh báo,
                                            yêu cầu chỉnh sửa nội dung vi phạm</li>
                                        <li><strong className="text-[#1A2332]">Cảnh báo lần 2:</strong> Xóa nội dung vi phạm,
                                            hạn chế đăng bài trong 7 ngày</li>
                                        <li><strong className="text-[#1A2332]">Vi phạm lần 3:</strong> Khóa tài khoản tạm thời 30 ngày</li>
                                        <li><strong className="text-[#1A2332]">Vi phạm nghiêm trọng:</strong> Khóa vĩnh viễn ngay lập tức
                                            (không cảnh báo trước)</li>
                                    </ul>
                                </div>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">7.4. Hậu Quả Sau Khi Chấm Dứt</p>
                                <ul className="list-disc list-inside space-y-2 ml-4">
                                    <li><strong className="text-[#1A2332]">Mất quyền truy cập:</strong> Bạn sẽ mất quyền truy cập
                                        ngay lập tức vào tất cả nội dung và dữ liệu trên tài khoản</li>
                                    <li><strong className="text-[#1A2332]">Coin không hoàn lại:</strong> Coin chưa sử dụng sẽ bị
                                        tịch thu và không được hoàn tiền</li>
                                    <li><strong className="text-[#1A2332]">Thu nhập chưa rút:</strong> Nếu tài khoản bị khóa do vi phạm,
                                        thu nhập chưa rút sẽ bị tịch thu. Nếu tài khoản bị xóa bởi người dùng, thu nhập ≥ 100,000 VNĐ
                                        vẫn được rút trong vòng 90 ngày</li>
                                    <li><strong className="text-[#1A2332]">Nội dung công khai:</strong> Nội dung đã xuất bản công khai
                                        có thể vẫn hiển thị cho đến khi được xóa thủ công hoặc sau 30 ngày</li>
                                    <li><strong className="text-[#1A2332]">Dữ liệu pháp lý:</strong> Một số dữ liệu cần thiết có thể được lưu
                                        trong 90 ngày để tuân thủ pháp luật hoặc giải quyết tranh chấp</li>
                                </ul>
                            </div>

                            <div>
                                <p className="font-semibold text-[#1A2332] mb-2">7.5. Quyền Khiếu Nại</p>
                                <ul className="list-disc list-inside space-y-1 ml-4">
                                    <li>Nếu bạn cho rằng tài khoản bị khóa nhầm, có thể gửi khiếu nại đến appeal@csw-ai.com
                                        trong vòng 7 ngày</li>
                                    <li>Cung cấp đầy đủ thông tin: email đăng ký, mô tả vấn đề, bằng chứng (nếu có)</li>
                                    <li>Chúng tôi sẽ xem xét và phản hồi trong vòng 5-7 ngày làm việc</li>
                                    <li>Quyết định cuối cùng của CSW-AI là quyết định không thể khiếu nại thêm</li>
                                </ul>
                            </div>
                        </div>
                    </section>

                    {/* 8. Changes to Terms */}
                    <section>
                        <div className="flex items-center gap-2 mb-4">
                            <FileText className="w-5 h-5 text-[#13EC5B]" />
                            <h3 className="text-xl font-bold text-[#1A2332]">
                                8. Thay Đổi Điều Khoản
                            </h3>
                        </div>
                        <div className="space-y-4 text-[#90A1B9] leading-relaxed">
                            <p>
                                CSW-AI có quyền cập nhật hoặc thay đổi các điều khoản này bất cứ lúc nào. Các thay đổi quan trọng
                                sẽ được thông báo qua email hoặc thông báo trên nền tảng. Việc bạn tiếp tục sử dụng dịch vụ sau
                                khi có thay đổi đồng nghĩa với việc bạn chấp nhận các điều khoản mới.
                            </p>
                        </div>
                    </section>

                    {/* 9. Contact */}
                    <section>
                        <div className="flex items-center gap-2 mb-4">
                            <FileText className="w-5 h-5 text-[#2B7FFF]" />
                            <h3 className="text-xl font-bold text-[#1A2332]">
                                9. Liên Hệ
                            </h3>
                        </div>
                        <div className="space-y-2 text-[#90A1B9] leading-relaxed">
                            <p>
                                Nếu bạn có bất kỳ câu hỏi nào về Điều khoản & Chính sách này, vui lòng liên hệ:
                            </p>
                            <ul className="space-y-1 ml-4">
                                <li><strong className="text-[#1A2332]">Email:</strong> support@csw-ai.com</li>
                                <li><strong className="text-[#1A2332]">Địa chỉ:</strong> Hà Nội, Việt Nam</li>
                                <li><strong className="text-[#1A2332]">Hotline:</strong> 1900-xxxx (8:00 - 22:00)</li>
                            </ul>
                        </div>
                    </section>

                    <div className="pt-6 border-t border-gray-200">
                        <p className="text-sm text-[#90A1B9] text-center">
                            <strong className="text-[#1A2332]">Cập nhật lần cuối:</strong> 08/02/2026
                        </p>
                    </div>
        </div>
    );
}