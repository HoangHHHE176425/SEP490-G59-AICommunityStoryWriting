using System.Text.Json;
using Services.Integrations.PayOS;

namespace Services.Interfaces
{
    public interface IPayOSClient
    {
        Task<PayOSClient.CreatePaymentLinkResult> CreatePaymentLinkAsync(
            long orderCode,
            decimal amountVnd,
            string description,
            string cancelUrl,
            string returnUrl,
            int? expiredAt = null,
            CancellationToken cancellationToken = default);

        Task<PayOSClient.GetPaymentRequestResult> GetPaymentRequestAsync(
            string id,
            CancellationToken cancellationToken = default);

        string ComputeWebhookSignature(JsonElement dataObject);
    }
}
