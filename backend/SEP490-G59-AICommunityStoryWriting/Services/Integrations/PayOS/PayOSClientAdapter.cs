using System.Text.Json;
using Services.Interfaces;

namespace Services.Integrations.PayOS
{
    public class PayOSClientAdapter : IPayOSClient
    {
        private readonly PayOSClient _client;

        public PayOSClientAdapter(PayOSClient client)
        {
            _client = client;
        }

        public Task<PayOSClient.CreatePaymentLinkResult> CreatePaymentLinkAsync(
            long orderCode,
            decimal amountVnd,
            string description,
            string cancelUrl,
            string returnUrl,
            int? expiredAt = null,
            CancellationToken cancellationToken = default)
        {
            return _client.CreatePaymentLinkAsync(
                orderCode,
                amountVnd,
                description,
                cancelUrl,
                returnUrl,
                expiredAt,
                cancellationToken);
        }

        public Task<PayOSClient.GetPaymentRequestResult> GetPaymentRequestAsync(
            string id,
            CancellationToken cancellationToken = default)
        {
            return _client.GetPaymentRequestAsync(id, cancellationToken);
        }

        public string ComputeWebhookSignature(JsonElement dataObject)
        {
            return _client.ComputeWebhookSignature(dataObject);
        }
    }
}
