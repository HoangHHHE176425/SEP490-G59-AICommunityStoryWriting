using System;
using System.Collections.Generic;

namespace Services.DTOs.Account
{
    public class AuthorOnboardingStatusResponse
    {
        public string CurrentRole { get; set; } = "USER";
        public bool IsAuthor { get; set; }
        public bool HasActiveAuthorPolicy { get; set; }
        public Guid? ActiveAuthorPolicyId { get; set; }
        public string? ActiveAuthorPolicyVersion { get; set; }
        public bool HasAcceptedActivePolicy { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public bool CanBecomeAuthor { get; set; }
        public List<string> MissingRequirements { get; set; } = new();
    }

    public class BecomeAuthorResponse
    {
        public string AccessToken { get; set; } = null!;
        public string Role { get; set; } = "AUTHOR";
        public Guid PolicyId { get; set; }
        public bool AcceptedPolicyNow { get; set; }
        public DateTime AcceptedAt { get; set; }
    }
}
