using System;
using System.Collections.Generic;

namespace BusinessObjects.Entities;

public partial class users
{
    public Guid id { get; set; }

    public string email { get; set; } = null!;

    public string password_hash { get; set; } = null!;

    public string? role { get; set; }

    public string? status { get; set; }

    public DateTime? email_verified_at { get; set; }

    public bool? must_resign_policy { get; set; }

    public DateTime? deletion_requested_at { get; set; }

    public DateTime? created_at { get; set; }

    public DateTime? updated_at { get; set; }

    public virtual ICollection<admin_audit_logs> admin_audit_logs { get; set; } = new List<admin_audit_logs>();

    public virtual ICollection<ai_usage_logs> ai_usage_logs { get; set; } = new List<ai_usage_logs>();

    public virtual ICollection<appeals> appeals { get; set; } = new List<appeals>();

    public virtual ICollection<auth_tokens> auth_tokens { get; set; } = new List<auth_tokens>();

    public virtual author_bank_accounts? author_bank_accounts { get; set; }

    public virtual ICollection<author_income_logs> author_income_logs { get; set; } = new List<author_income_logs>();

    public virtual ICollection<chapter_versions> chapter_versions { get; set; } = new List<chapter_versions>();

    public virtual ICollection<coin_orders> coin_orders { get; set; } = new List<coin_orders>();

    public virtual ICollection<comments> comments { get; set; } = new List<comments>();

    public virtual ICollection<donations> donationsreceiver { get; set; } = new List<donations>();

    public virtual ICollection<donations> donationssender { get; set; } = new List<donations>();

    public virtual ICollection<follows> followsauthor { get; set; } = new List<follows>();

    public virtual ICollection<follows> followsuser { get; set; } = new List<follows>();

    public virtual ICollection<moderation_logs> moderation_logs { get; set; } = new List<moderation_logs>();

    public virtual ICollection<notifications> notifications { get; set; } = new List<notifications>();

    public virtual ICollection<otp_verifications> otp_verifications { get; set; } = new List<otp_verifications>();

    public virtual ICollection<purchases> purchases { get; set; } = new List<purchases>();

    public virtual ICollection<ratings> ratings { get; set; } = new List<ratings>();

    public virtual ICollection<reports> reportsassigned_toNavigation { get; set; } = new List<reports>();

    public virtual ICollection<reports> reportsreporter { get; set; } = new List<reports>();

    public virtual ICollection<stories> stories { get; set; } = new List<stories>();

    public virtual ICollection<story_commitments> story_commitments { get; set; } = new List<story_commitments>();

    public virtual ICollection<author_policy_acceptances> author_policy_acceptances { get; set; } = new List<author_policy_acceptances>();

    public virtual ICollection<system_settings> system_settings { get; set; } = new List<system_settings>();

    public virtual ICollection<user_activity_logs> user_activity_logs { get; set; } = new List<user_activity_logs>();

    public virtual ICollection<user_library> user_library { get; set; } = new List<user_library>();

    public virtual user_profiles? user_profiles { get; set; }

    public virtual ICollection<user_vouchers> user_vouchers { get; set; } = new List<user_vouchers>();

    public virtual ICollection<violation_logs> violation_logscompliance_officer { get; set; } = new List<violation_logs>();

    public virtual ICollection<violation_logs> violation_logsviolator { get; set; } = new List<violation_logs>();

    public virtual wallets? wallets { get; set; }

    public virtual ICollection<withdraw_requests> withdraw_requestsauthor { get; set; } = new List<withdraw_requests>();

    public virtual ICollection<withdraw_requests> withdraw_requestsprocessed_byNavigation { get; set; } = new List<withdraw_requests>();

    public virtual ICollection<comments> comment { get; set; } = new List<comments>();
}
