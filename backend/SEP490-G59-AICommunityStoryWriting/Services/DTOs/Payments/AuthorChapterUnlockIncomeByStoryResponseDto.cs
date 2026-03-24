using System;
using System.Collections.Generic;

namespace Services.DTOs.Payments
{
    public class AuthorChapterUnlockIncomeByStoryResponseDto
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        public List<AuthorChapterUnlockIncomeByStoryItemDto> Items { get; set; } = new();
    }
}

