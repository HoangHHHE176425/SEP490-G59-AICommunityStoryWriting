using System;

namespace Services.DTOs.Payments
{
    /// <summary>
    /// Dòng trong lịch sử ví của USER cho hành động donate (sender_id = user).
    /// </summary>
    public class WalletDonateHistoryItemDto
    {
        public Guid DonationId { get; set; }
        public Guid StoryId { get; set; }
        public string StoryTitle { get; set; } = null!;

        /// <summary>Coin user đã chi cho donate.</summary>
        public int CoinsPaid { get; set; }

        public DateTime DonatedAt { get; set; }
    }
}

