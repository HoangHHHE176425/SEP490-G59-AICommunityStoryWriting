using System;

namespace Services.DTOs.Payments
{
    public class DonateRequestDto
    {
        public Guid AuthorId { get; set; }
        public int Amount { get; set; }
        public string? Message { get; set; }
    }

    public class DonateResponseDto
    {
        public Guid DonationId { get; set; }
        public Guid SenderId { get; set; }
        public Guid ReceiverId { get; set; }
        public int Amount { get; set; }
        public string? Message { get; set; }
        public DateTime? CreatedAt { get; set; }
        public int SenderBalanceAfter { get; set; }
        public int ReceiverBalanceAfter { get; set; }
    }
}

