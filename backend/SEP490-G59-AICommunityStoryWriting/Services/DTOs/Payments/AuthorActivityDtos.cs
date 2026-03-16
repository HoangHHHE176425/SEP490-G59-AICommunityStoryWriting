using System;

namespace Services.DTOs.Payments
{
    /// <summary>Một dòng trong lịch sử donate + rút tiền của tác giả (bảng gộp).</summary>
    public class AuthorActivityItemDto
    {
        public string Type { get; set; } = ""; // "DONATE" | "WITHDRAW"
        public Guid Id { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int Amount { get; set; }
        public string? Note { get; set; }
        /// <summary>Với DONATE: tên người gửi. Với WITHDRAW: null.</summary>
        public string? SenderDisplayName { get; set; }
        /// <summary>Với WITHDRAW: PENDING | APPROVED | REJECTED. Với DONATE: null.</summary>
        public string? WithdrawStatus { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }

    public class AuthorActivityResponseDto
    {
        public System.Collections.Generic.List<AuthorActivityItemDto> Items { get; set; } = new();
        public int TotalCount { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
    }

    public class CreateWithdrawRequestDto
    {
        public int AmountCoins { get; set; }
        public string? BankInfo { get; set; }
    }

    public class WithdrawRequestItemDto
    {
        public Guid Id { get; set; }
        public decimal AmountRequested { get; set; }
        public decimal? FeeAmount { get; set; }
        public string? Status { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
        public string? AdminNote { get; set; }
    }
}
