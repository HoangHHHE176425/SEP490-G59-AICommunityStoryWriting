using System;

namespace Services.DTOs.Payments
{
    public class CoinPackageDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal PriceAmount { get; set; }
        public string Currency { get; set; } = "VND";
        public int CoinAmount { get; set; }
        public int BonusCoin { get; set; }
        public bool IsActive { get; set; }
    }
}

