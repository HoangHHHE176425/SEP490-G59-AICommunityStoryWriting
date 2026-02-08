import React from 'react';
import { X, Shield, FileText, AlertCircle } from 'lucide-react';

/**
 * @param {{ isOpen: boolean; onClose?: () => void; onAccept: () => void; onDecline: () => void; }} props
 */
export function PolicyModal({ isOpen, onClose, onAccept, onDecline }) {
  if (!isOpen) return null;

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center p-4 bg-black/50 backdrop-blur-sm">
      <div className="bg-white rounded-2xl shadow-2xl max-w-4xl w-full max-h-[90vh] flex flex-col">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b border-gray-200">
          <div className="flex items-center gap-3">
            <div className="w-12 h-12 bg-gradient-to-br from-[#13EC5B] to-[#11D350] rounded-xl flex items-center justify-center">
              <Shield className="w-6 h-6 text-white" />
            </div>
            <div>
              <h2 className="text-2xl font-bold text-[#1A2332]">
                Điều Khoản & Chính Sách
              </h2>
              <p className="text-sm text-[#90A1B9]">
                Vui lòng đọc và đồng ý trước khi tiếp tục
              </p>
            </div>
          </div>
          {onClose && (
            <button
              onClick={onClose}
              className="p-2 hover:bg-gray-100 rounded-lg transition-colors"
              type="button"
              aria-label="Đóng"
            >
              <X className="w-6 h-6 text-[#90A1B9]" />
            </button>
          )}
        </div>

        {/* Content */}
        <div className="flex-1 overflow-y-auto p-6 space-y-6">
          {/* Important Notice */}
          <div className="bg-gradient-to-r from-[#FFF4E6] to-[#FFF9F0] border-l-4 border-[#FFA500] p-4 rounded-lg">
            <div className="flex gap-3">
              <AlertCircle className="w-5 h-5 text-[#FFA500] flex-shrink-0 mt-0.5" />
              <div>
                <p className="font-semibold text-[#1A2332] mb-1">Thông Báo Quan Trọng</p>
                <p className="text-sm text-[#90A1B9] leading-relaxed">
                  Bằng việc tạo tài khoản và sử dụng dịch vụ CSW-AI, bạn xác nhận rằng bạn đã đọc kỹ, hiểu rõ
                  và đồng ý tuân thủ tất cả các điều khoản, chính sách và quy định được nêu trong tài liệu này.
                  Nếu bạn không đồng ý với bất kỳ điều khoản nào, vui lòng không sử dụng dịch vụ của chúng tôi.
                </p>
              </div>
            </div>
          </div>

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
                  <li><strong className="text-[#1A2332]">Cung cấp dịch vụ:</strong> Tạo tài khoản, lưu trữ nội dung, xử lý giao dịch, hỗ trợ khách hàng</li>
                  <li><strong className="text-[#1A2332]">Cá nhân hóa trải nghiệm:</strong> Gợi ý truyện phù hợp, hiển thị nội dung liên quan, tùy chỉnh giao diện</li>
                  <li><strong className="text-[#1A2332]">Giao tiếp:</strong> Gửi thông báo quan trọng, cập nhật dịch vụ, tin tức và khuyến mãi (bạn có thể hủy đăng ký)</li>
                  <li><strong className="text-[#1A2332]">Phân tích và cải thiện:</strong> Nghiên cứu hành vi người dùng, tối ưu hiệu suất, phát triển tính năng mới</li>
                  <li><strong className="text-[#1A2332]">Bảo mật:</strong> Phát hiện gian lận, ngăn chặn lạm dụng, bảo vệ quyền lợi người dùng</li>
                  <li><strong className="text-[#1A2332]">Tuân thủ pháp luật:</strong> Đáp ứng yêu cầu pháp lý, giải quyết tranh chấp</li>
                </ul>
              </div>
              <div>
                <p className="font-semibold text-[#1A2332] mb-2">3.3. Bảo Mật Thông Tin</p>
                <p className="mb-2">Chúng tôi áp dụng các biện pháp bảo mật tiên tiến: Mã hóa SSL/TLS và AES-256, xác thực 2FA, hashing mật khẩu bcrypt, kiểm soát truy cập, giám sát 24/7, sao lưu định kỳ.</p>
              </div>
              <div>
                <p className="font-semibold text-[#1A2332] mb-2">3.4. Chia Sẻ Thông Tin</p>
                <p className="mb-2">Chúng tôi <strong className="text-[#1A2332]">không bán hoặc cho thuê</strong> thông tin cá nhân. Chia sẻ chỉ khi có đồng ý của bạn, với nhà cung cấp dịch vụ, theo yêu cầu pháp lý, bảo vệ quyền lợi, hoặc chuyển giao doanh nghiệp.</p>
              </div>
              <div>
                <p className="font-semibold text-[#1A2332] mb-2">3.5. Quyền Của Bạn</p>
                <p className="mb-2">Truy cập, chỉnh sửa, xóa, hạn chế xử lý, tải xuống dữ liệu, phản đối marketing, rút đồng ý. Liên hệ: <strong className="text-[#1A2332]">privacy@csw-ai.com</strong></p>
              </div>
              <div>
                <p className="font-semibold text-[#1A2332] mb-2">3.6. Cookie &amp; 3.7. Lưu Trữ Dữ Liệu</p>
                <p className="mb-2">Cookie dùng để ghi nhớ đăng nhập, phân tích traffic. Dữ liệu lưu tại máy chủ Việt Nam và trung tâm dữ liệu quốc tế. Lưu trữ đến khi xóa tài khoản hoặc theo pháp luật (tối thiểu 90 ngày sau khi xóa).</p>
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
                  <li><strong className="text-[#1A2332]">AI là công cụ hỗ trợ:</strong> Hỗ trợ sáng tạo nội dung, gợi ý ý tưởng, phát triển cốt truyện</li>
                  <li><strong className="text-[#1A2332]">Không thay thế con người:</strong> AI chỉ là trợ lý thông minh</li>
                  <li><strong className="text-[#1A2332]">Tính minh bạch:</strong> Nội dung có sự hỗ trợ AI có thể được đánh dấu</li>
                </ul>
              </div>
              <div>
                <p className="font-semibold text-[#1A2332] mb-2">4.2. Trách Nhiệm Của Tác Giả</p>
                <p className="mb-2">Bạn chịu trách nhiệm toàn bộ nội dung cuối cùng, kiểm tra chất lượng, tuân thủ pháp luật, không lạm dụng AI để spam hoặc vi phạm.</p>
              </div>
              <div>
                <p className="font-semibold text-[#1A2332] mb-2">4.3. Dữ Liệu Huấn Luyện &amp; 4.4. Giới Hạn</p>
                <p className="mb-2">Nội dung tương tác với AI có thể dùng cải thiện mô hình (ẩn danh). Bạn có quyền từ chối. CSW-AI không đảm bảo chất lượng nội dung do AI tạo ra.</p>
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
                <ul className="list-disc list-inside space-y-1 ml-4">
                  <li>Coin là đơn vị tiền tệ ảo; 1 Coin = 1,000 VNĐ</li>
                  <li>Nạp qua thẻ ATM, Visa/Master, Momo, ZaloPay, chuyển khoản</li>
                  <li>Coin không đổi lại tiền mặt, không có thời hạn (trừ khi tài khoản khóa/xóa)</li>
                </ul>
              </div>
              <div>
                <p className="font-semibold text-[#1A2332] mb-2">5.2. Hoàn Tiền &amp; 5.3. Thu Nhập Tác Giả</p>
                <p className="mb-2">Hoàn tiền trong 24h nếu nạp nhầm/lỗi. Tác giả nhận 70% doanh thu chương trả phí, 85% từ đề cử. Rút tối thiểu 100,000 VNĐ; xử lý 7-14 ngày. Chi tiết: billing@csw-ai.com</p>
              </div>
              <div>
                <p className="font-semibold text-[#1A2332] mb-2">5.4. Bảo Mật Thanh Toán &amp; 5.5. Gian Lận</p>
                <p className="mb-2">Giao dịch mã hóa SSL/TLS; thẻ xử lý bởi đối tác PCI-DSS. Gian lận (bot, fake) sẽ bị khóa vĩnh viễn và thu hồi thu nhập.</p>
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
              <p>Dịch vụ &quot;nguyên trạng&quot; &quot;như có&quot;. Không đảm bảo hoạt động liên tục, không lỗi. Trách nhiệm tối đa: số tiền bạn thanh toán trong 12 tháng gần nhất hoặc 1,000,000 VNĐ (tùy số nào thấp hơn). Không chịu trách nhiệm thiệt hại gián tiếp, mất doanh thu, mất dữ liệu. Liên kết bên ngoài không thuộc kiểm soát của chúng tôi.</p>
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
              <p><strong className="text-[#1A2332]">Chấm dứt do người dùng:</strong> Xóa tài khoản bất cứ lúc nào qua Cài đặt; xác nhận mật khẩu + email; vô hiệu hóa ngay, xóa vĩnh viễn sau 30 ngày. <strong className="text-[#1A2332]">Đình chỉ do CSW-AI:</strong> Khi vi phạm điều khoản, nội dung bất hợp pháp, gian lận, spam. Xử lý: cảnh báo → khóa 7 ngày → khóa 30 ngày → khóa vĩnh viễn. Coin chưa dùng bị tịch thu; thu nhập chưa rút xử lý theo quy định. Khiếu nại: appeal@csw-ai.com trong 7 ngày.</p>
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
            <div className="space-y-2 text-[#90A1B9] leading-relaxed">
              <p>CSW-AI có quyền cập nhật hoặc thay đổi điều khoản bất cứ lúc nào. Thay đổi quan trọng sẽ thông báo qua email hoặc trên nền tảng. Tiếp tục sử dụng sau thay đổi đồng nghĩa chấp nhận điều khoản mới.</p>
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
              <p>Nếu có câu hỏi về Điều khoản &amp; Chính sách, vui lòng liên hệ:</p>
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

        {/* Footer Actions */}
        <div className="p-6 border-t border-gray-200 bg-gray-50">
          <div className="flex flex-col sm:flex-row gap-3">
            <button
              type="button"
              onClick={onDecline}
              className="flex-1 px-6 py-3 border-2 border-gray-300 text-[#1A2332] rounded-xl hover:bg-gray-100 transition-all font-semibold"
            >
              Hủy Bỏ
            </button>
            <button
              type="button"
              onClick={onAccept}
              className="flex-1 px-6 py-3 bg-gradient-to-r from-[#13EC5B] to-[#11D350] text-white rounded-xl hover:shadow-[0_0_20px_rgba(19,236,91,0.5)] transition-all font-bold"
            >
              Tôi Đồng Ý Các Điều Khoản
            </button>
          </div>
          <p className="text-xs text-[#90A1B9] text-center mt-4">
            Bằng việc nhấn &quot;Tôi Đồng Ý&quot;, bạn xác nhận đã đọc và chấp nhận tất cả các điều khoản trên
          </p>
        </div>
      </div>
    </div>
  );
}
