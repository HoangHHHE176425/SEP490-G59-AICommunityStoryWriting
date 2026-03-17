using Services.DTOs.Payments;

namespace Services.Interfaces
{
    public interface ICoinPaymentService
    {
        Task<IReadOnlyList<CoinPackageDto>> GetActivePackagesAsync(CancellationToken cancellationToken = default);
        Task<WalletDto> GetOrCreateWalletAsync(Guid userId, CancellationToken cancellationToken = default);
        Task<IReadOnlyList<CoinOrderDto>> GetMyOrdersAsync(Guid userId, int take = 50, CancellationToken cancellationToken = default);

        Task<CreatePayOSPaymentResponseDto> CreatePayOSPaymentAsync(Guid userId, CreatePayOSPaymentRequestDto request, CancellationToken cancellationToken = default);
        Task<string> ProcessPayOSWebhookAsync(string rawBody, CancellationToken cancellationToken = default);
        Task<CoinOrderDto> SyncMyPayOSOrderAsync(Guid userId, Guid coinOrderId, CancellationToken cancellationToken = default);

        Task<DonateResponseDto> DonateAsync(Guid senderUserId, Guid receiverUserId, int amount, string? message, CancellationToken cancellationToken = default);

        /// <summary>Lịch sử donate nhận + rút tiền của tác giả (gộp, sắp xếp theo ngày giảm dần).</summary>
        Task<AuthorActivityResponseDto> GetAuthorActivityAsync(Guid authorUserId, int page = 1, int pageSize = 50, CancellationToken cancellationToken = default);

        /// <summary>Tạo yêu cầu rút tiền (author). Trừ balance_coin khi tạo; admin xử lý sau.</summary>
        Task<WithdrawRequestItemDto> CreateWithdrawRequestAsync(Guid authorUserId, int amountCoins, string? bankInfo, CancellationToken cancellationToken = default);
    }
}

