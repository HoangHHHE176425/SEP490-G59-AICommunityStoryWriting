/**
 * Dịch nhanh message từ backend (thường là EN) sang tiếng Việt cho FE.
 * Chiến lược:
 * - Nếu message đã có ký tự tiếng Việt (có dấu) => giữ nguyên.
 * - Nếu message có vẻ là tiếng Anh => thử map theo một số mẫu/phân đoạn phổ biến.
 * - Nếu chưa map được => trả thông báo chung tiếng Việt để tránh hiển thị EN cho user.
 */
export function translateBackendMessage(message) {
  if (message == null) return message;
  const raw = String(message).trim();
  if (!raw) return raw;

  // Nếu đã có tiếng Việt (có dấu), coi như đúng ngôn ngữ.
  // À-ỹ là dải ký tự tiếng Việt có dấu phổ biến.
  if (/[À-ỹà-ỹ]/.test(raw)) return raw;

  const lower = raw.toLowerCase();

  const MAP = [
    // Policy delete constraints
    {
      test: /cannot delete policy that has acceptance records\. deactivate it instead\./i,
      out: 'Không thể xóa policy đã có bản ghi chấp nhận. Hãy tắt (deactivate) policy thay vì xóa.',
    },

    // Auth
    { test: /missing refresh token/i, out: 'Thiếu refresh token. Vui lòng đăng nhập lại.' },
    { test: /unauthorized/i, out: 'Bạn chưa đăng nhập hoặc phiên đăng nhập đã hết hạn.' },
    { test: /forbidden/i, out: 'Bạn không có quyền thực hiện thao tác này.' },
    { test: /account has been banned|account is banned|banned/i, out: 'Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên để được hỗ trợ.' },
    { test: /logged out/i, out: 'Đã đăng xuất.' },
    { test: /the account has been banned/i, out: 'Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên để được hỗ trợ.' },

    // OAuth / Google
    { test: /registration successful/i, out: 'Đăng ký thành công. Vui lòng kiểm tra email để xác thực OTP.' },
    { test: /account verified successfully/i, out: 'Xác thực tài khoản thành công. Bạn có thể đăng nhập.' },
    { test: /missing oauth code/i, out: 'Thiếu mã OAuth.' },
    { test: /missing oauth state/i, out: 'Thiếu OAuth state.' },
    { test: /invalid oauth state/i, out: 'OAuth state không hợp lệ.' },
    { test: /failed to exchange code with google/i, out: 'Không thể trao đổi mã với Google.' },
    { test: /google did not return id_token/i, out: 'Google không trả về id_token.' },
    { test: /google id_token missing email claim/i, out: 'id_token của Google thiếu thông tin email.' },

    // Not found
    { test: /user not found/i, out: 'Không tìm thấy người dùng.' },
    { test: /policy not found/i, out: 'Không tìm thấy chính sách.' },
    { test: /chapter not found/i, out: 'Không tìm thấy chương.' },
    { test: /comment not found/i, out: 'Không tìm thấy bình luận.' },

    // Required fields / validation
    { test: /body is required/i, out: 'Dữ liệu yêu cầu không hợp lệ.' },
    { test: /decision is required/i, out: 'Vui lòng cung cấp quyết định.' },
    { test: /status is required/i, out: 'Vui lòng cung cấp trạng thái.' },
    { test: /requestkind is required/i, out: 'Vui lòng cung cấp loại yêu cầu.' },
    { test: /reasoncode is required/i, out: 'Vui lòng cung cấp mã lý do.' },

    // Generic
    { test: /unexpected error/i, out: 'Đã xảy ra lỗi không xác định. Vui lòng thử lại sau.' },
    { test: /failed to load/i, out: 'Không thể tải dữ liệu.' },
    { test: /an error occurred while/i, out: 'Đã xảy ra lỗi khi xử lý yêu cầu.' },
    { test: /try again/i, out: 'Vui lòng thử lại sau.' },

    // Withdraw / payout
    { test: /withdraw request not found/i, out: 'Không tìm thấy yêu cầu rút tiền.' },
    { test: /only pending\/pending_review withdraw requests can be approved/i, out: 'Chỉ có thể duyệt yêu cầu ở trạng thái PENDING/PENDING_REVIEW.' },
    { test: /only pending\/pending_review withdraw requests can be rejected/i, out: 'Chỉ có thể từ chối yêu cầu ở trạng thái PENDING/PENDING_REVIEW.' },
    { test: /minimum withdrawal amount/i, out: 'Số tiền rút tối thiểu chưa đúng. Vui lòng kiểm tra lại.' },
  ];

  for (const { test, out } of MAP) {
    if (test.test(lower)) return out;
    // một số backend trả hoa thường khác
    if (test.test(raw)) return out;
  }

  // Nếu message còn nguyên tiếng Anh (khả năng cao) => thông báo chung.
  if (/[A-Za-z]/.test(raw)) return 'Đã xảy ra lỗi. Vui lòng thử lại sau.';

  return raw;
}

