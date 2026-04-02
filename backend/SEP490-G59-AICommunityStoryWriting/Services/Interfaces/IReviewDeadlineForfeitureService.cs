namespace Services.Interfaces;

/// <summary>Tự động trả đơn nhận duyệt về hàng đợi khi quá hạn; ghi moderation_logs và chặn moderator nhận lại cùng truyện.</summary>
public interface IReviewDeadlineForfeitureService
{
    /// <returns>Số assignment đã xử lý trong lần gọi.</returns>
    int ProcessOverdueClaims();
}
