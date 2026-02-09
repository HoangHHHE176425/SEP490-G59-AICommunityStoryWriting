using System;

namespace Services.DTOs.Policies
{
    public class PolicyResponseDto
    {
        public Guid Id { get; set; }
        public string? Type { get; set; }
        public string Version { get; set; } = null!;
        public string Content { get; set; } = null!;
        public bool IsActive { get; set; }
        public bool RequireResign { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? ActivatedAt { get; set; }
    }

    public class AuthorPolicyStatusDto
    {
        public PolicyResponseDto Policy { get; set; } = null!;
        public bool HasAccepted { get; set; }
        public DateTime? AcceptedAt { get; set; }
    }
}

