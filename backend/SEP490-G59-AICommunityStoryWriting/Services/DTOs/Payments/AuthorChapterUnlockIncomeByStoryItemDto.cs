using System;

namespace Services.DTOs.Payments
{
    public class AuthorChapterUnlockIncomeByStoryItemDto
    {
        public Guid StoryId { get; set; }
        public string StoryTitle { get; set; } = null!;

        public decimal GrossAmount { get; set; }
        public decimal PlatformFee { get; set; }
        public decimal NetAmount { get; set; }

        public int UnlockCount { get; set; }

        public DateTime? LastUnlockedAt { get; set; }
    }
}

