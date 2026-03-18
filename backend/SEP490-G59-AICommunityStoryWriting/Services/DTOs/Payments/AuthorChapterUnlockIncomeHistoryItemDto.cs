using System;

namespace Services.DTOs.Payments
{
    public class AuthorChapterUnlockIncomeHistoryItemDto
    {
        public Guid PurchaseId { get; set; }

        public Guid StoryId { get; set; }
        public string StoryTitle { get; set; } = null!;

        public Guid ChapterId { get; set; }
        public string ChapterTitle { get; set; } = null!;

        public int CoinsPaid { get; set; }

        public decimal GrossAmount { get; set; }
        public decimal PlatformFee { get; set; }
        public decimal NetAmount { get; set; }

        public DateTime UnlockedAt { get; set; }
    }
}

