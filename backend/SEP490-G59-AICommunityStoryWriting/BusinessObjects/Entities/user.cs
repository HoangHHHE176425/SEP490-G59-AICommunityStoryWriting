using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class User
{
    public Guid Id { get; set; }

    public string Email { get; set; } = null!;

    public string PasswordHash { get; set; } = null!;

    public string? Role { get; set; }

    public string? Status { get; set; }

    public DateTime? EmailVerifiedAt { get; set; }

    public bool? MustResignPolicy { get; set; }

    public DateTime? DeletionRequestedAt { get; set; }

    public DateTime? CreatedAt { get; set; }

    public DateTime? UpdatedAt { get; set; }

    public virtual ICollection<AdminAuditLog> AdminAuditLogs { get; set; } = new List<AdminAuditLog>();

    public virtual ICollection<AiUsageLog> AiUsageLogs { get; set; } = new List<AiUsageLog>();

    public virtual ICollection<Appeal> Appeals { get; set; } = new List<Appeal>();

    public virtual ICollection<AuthToken> AuthTokens { get; set; } = new List<AuthToken>();

    public virtual AuthorBankAccount? AuthorBankAccount { get; set; }

    public virtual ICollection<AuthorIncomeLog> AuthorIncomeLogs { get; set; } = new List<AuthorIncomeLog>();

    public virtual ICollection<ChapterVersion> ChapterVersions { get; set; } = new List<ChapterVersion>();

    public virtual ICollection<CoinOrder> CoinOrders { get; set; } = new List<CoinOrder>();

    public virtual ICollection<Comment> Comments { get; set; } = new List<Comment>();

    public virtual ICollection<Donation> DonationReceivers { get; set; } = new List<Donation>();

    public virtual ICollection<Donation> DonationSenders { get; set; } = new List<Donation>();

    public virtual ICollection<Follow> FollowAuthors { get; set; } = new List<Follow>();

    public virtual ICollection<Follow> FollowUsers { get; set; } = new List<Follow>();

    public virtual ICollection<ModerationLog> ModerationLogs { get; set; } = new List<ModerationLog>();

    public virtual ICollection<Notification> Notifications { get; set; } = new List<Notification>();

    public virtual ICollection<OtpVerification> OtpVerifications { get; set; } = new List<OtpVerification>();

    public virtual ICollection<Purchase> Purchases { get; set; } = new List<Purchase>();

    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();

    public virtual ICollection<Report> ReportAssignedToNavigations { get; set; } = new List<Report>();

    public virtual ICollection<Report> ReportReporters { get; set; } = new List<Report>();

    public virtual ICollection<Story> Stories { get; set; } = new List<Story>();

    public virtual ICollection<StoryCommitment> StoryCommitments { get; set; } = new List<StoryCommitment>();

    public virtual ICollection<SystemSetting> SystemSettings { get; set; } = new List<SystemSetting>();

    public virtual ICollection<UserActivityLog> UserActivityLogs { get; set; } = new List<UserActivityLog>();

    public virtual ICollection<UserLibrary> UserLibraries { get; set; } = new List<UserLibrary>();

    public virtual UserProfile? UserProfile { get; set; }

    public virtual ICollection<UserVoucher> UserVouchers { get; set; } = new List<UserVoucher>();

    public virtual ICollection<ViolationLog> ViolationLogComplianceOfficers { get; set; } = new List<ViolationLog>();

    public virtual ICollection<ViolationLog> ViolationLogViolators { get; set; } = new List<ViolationLog>();

    public virtual Wallet? Wallet { get; set; }

    public virtual ICollection<WithdrawRequest> WithdrawRequestAuthors { get; set; } = new List<WithdrawRequest>();

    public virtual ICollection<WithdrawRequest> WithdrawRequestProcessedByNavigations { get; set; } = new List<WithdrawRequest>();

    public virtual ICollection<Comment> CommentsNavigation { get; set; } = new List<Comment>();
}
