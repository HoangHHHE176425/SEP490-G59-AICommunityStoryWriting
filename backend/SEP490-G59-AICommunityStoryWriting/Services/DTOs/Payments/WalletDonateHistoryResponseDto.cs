using System.Collections.Generic;

namespace Services.DTOs.Payments
{
    public class WalletDonateHistoryResponseDto
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        public List<WalletDonateHistoryItemDto> Items { get; set; } = new();
    }
}

