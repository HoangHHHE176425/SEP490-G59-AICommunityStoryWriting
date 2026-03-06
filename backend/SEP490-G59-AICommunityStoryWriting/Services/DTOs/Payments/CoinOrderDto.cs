using System;

namespace Services.DTOs.Payments
{
    public class CoinOrderDto
    {
        public Guid Id { get; set; }
        public Guid PackageId { get; set; }
        public decimal AmountPaid { get; set; }
        public int CoinsGranted { get; set; }
        public string Status { get; set; } = "PENDING";
        public string PaymentGateway { get; set; } = "PAYOS";
        public string? GatewayTransactionId { get; set; }
        public string? GatewayResponseCode { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
    }
}

