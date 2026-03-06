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
    }
}

