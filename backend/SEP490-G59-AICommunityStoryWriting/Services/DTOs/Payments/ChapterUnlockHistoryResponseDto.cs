using System;
using System.Collections.Generic;

namespace Services.DTOs.Payments
{
    public class ChapterUnlockHistoryResponseDto
    {
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }

        public List<ChapterUnlockHistoryItemDto> Items { get; set; } = new();
    }
}

