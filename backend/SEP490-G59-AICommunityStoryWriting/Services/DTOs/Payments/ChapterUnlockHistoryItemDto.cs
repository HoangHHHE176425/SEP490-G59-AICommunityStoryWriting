using System;

namespace Services.DTOs.Payments
{
    /// Lịch sử mở khóa chương trả phí (trừ coin) của user.
    public class ChapterUnlockHistoryItemDto
    {
        public Guid PurchaseId { get; set; }
        public Guid StoryId { get; set; }
        public string StoryTitle { get; set; } = null!;
        public Guid ChapterId { get; set; }
        public string ChapterTitle { get; set; } = null!;

        public int CoinsPaid { get; set; }
        public DateTime UnlockedAt { get; set; }
    }
}

