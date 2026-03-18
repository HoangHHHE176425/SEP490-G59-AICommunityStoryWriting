using System;

namespace Services.DTOs.Payments
{
    public class WalletDto
    {
        public Guid UserId { get; set; }
        public int BalanceCoin { get; set; }
        public decimal IncomeBalance { get; set; }
        public decimal FrozenBalance { get; set; }
        public decimal PendingEscrowBalance { get; set; }
        public string Currency { get; set; } = "VND";
        public DateTime? UpdatedAt { get; set; }
    }
}

