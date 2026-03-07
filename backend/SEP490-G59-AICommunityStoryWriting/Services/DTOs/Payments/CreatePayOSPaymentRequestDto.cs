using System;
using System.ComponentModel.DataAnnotations;

namespace Services.DTOs.Payments
{
    public class CreatePayOSPaymentRequestDto
    {
        [Required]
        public Guid PackageId { get; set; }

        [Required]
        [Url]
        public string ReturnUrl { get; set; } = string.Empty;

        [Required]
        [Url]
        public string CancelUrl { get; set; } = string.Empty;
    }
}

