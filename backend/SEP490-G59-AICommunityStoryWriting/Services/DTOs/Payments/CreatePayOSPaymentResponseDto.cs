using System;

namespace Services.DTOs.Payments
{
    public class CreatePayOSPaymentResponseDto
    {
        public Guid CoinOrderId { get; set; }
        public Guid PackageId { get; set; }
        public decimal AmountPaid { get; set; }
        public int CoinsGranted { get; set; }

        public long OrderCode { get; set; }
        public string PaymentLinkId { get; set; } = string.Empty;
        public string CheckoutUrl { get; set; } = string.Empty;
    }
}

