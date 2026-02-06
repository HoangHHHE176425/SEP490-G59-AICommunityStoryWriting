using System;
using System.Collections.Generic;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessObjects.Models;

public partial class StoryPlatformDbContext : DbContext
{
    public StoryPlatformDbContext()
    {
    }

    public StoryPlatformDbContext(DbContextOptions<StoryPlatformDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AdminAuditLog> AdminAuditLogs { get; set; }

    public virtual DbSet<AiConfig> AiConfigs { get; set; }

    public virtual DbSet<AiGeneratedContent> AiGeneratedContents { get; set; }

    public virtual DbSet<AiModelRegistry> AiModelRegistries { get; set; }

    public virtual DbSet<AiPlagiarismReport> AiPlagiarismReports { get; set; }

    public virtual DbSet<AiSensitiveWord> AiSensitiveWords { get; set; }

    public virtual DbSet<AiUsageLog> AiUsageLogs { get; set; }

    public virtual DbSet<Appeal> Appeals { get; set; }

    public virtual DbSet<AuthToken> AuthTokens { get; set; }

    public virtual DbSet<AuthorBankAccount> AuthorBankAccounts { get; set; }

    public virtual DbSet<AuthorIncomeLog> AuthorIncomeLogs { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Chapter> Chapters { get; set; }

    public virtual DbSet<ChapterVersion> ChapterVersions { get; set; }

    public virtual DbSet<CoinOrder> CoinOrders { get; set; }

    public virtual DbSet<CoinPackage> CoinPackages { get; set; }

    public virtual DbSet<Comment> Comments { get; set; }

    public virtual DbSet<DailyStatistic> DailyStatistics { get; set; }

    public virtual DbSet<Donation> Donations { get; set; }

    public virtual DbSet<Follow> Follows { get; set; }

    public virtual DbSet<IdeaPost> IdeaPosts { get; set; }

    public virtual DbSet<IdeaProposal> IdeaProposals { get; set; }

    public virtual DbSet<MarketingBanner> MarketingBanners { get; set; }

    public virtual DbSet<ModerationLog> ModerationLogs { get; set; }

    public virtual DbSet<Notification> Notifications { get; set; }

    public virtual DbSet<OtpVerification> OtpVerifications { get; set; }

    public virtual DbSet<Purchase> Purchases { get; set; }

    public virtual DbSet<Rating> Ratings { get; set; }

    public virtual DbSet<Report> Reports { get; set; }

    public virtual DbSet<ReportEvidence> ReportEvidences { get; set; }

    public virtual DbSet<Story> Stories { get; set; }

    public virtual DbSet<StoryCommitment> StoryCommitments { get; set; }

    public virtual DbSet<SystemPolicy> SystemPolicies { get; set; }

    public virtual DbSet<SystemSetting> SystemSettings { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserActivityLog> UserActivityLogs { get; set; }

    public virtual DbSet<UserLibrary> UserLibraries { get; set; }

    public virtual DbSet<UserProfile> UserProfiles { get; set; }

    public virtual DbSet<UserVoucher> UserVouchers { get; set; }

    public virtual DbSet<ViolationLog> ViolationLogs { get; set; }

    public virtual DbSet<Voucher> Vouchers { get; set; }

    public virtual DbSet<Wallet> Wallets { get; set; }

    public virtual DbSet<WithdrawRequest> WithdrawRequests { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server= TRUONG\\HIHITRUONGNE;uid=sa;password=123;database=story_platform_v6;Encrypt=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AdminAuditLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__admin_au__3213E83F22D13D20");

            entity.ToTable("admin_audit_logs");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ActionType)
                .HasMaxLength(50)
                .HasColumnName("action_type");
            entity.Property(e => e.AdminId).HasColumnName("admin_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .HasColumnName("ip_address");
            entity.Property(e => e.NewValue).HasColumnName("new_value");
            entity.Property(e => e.OldValue).HasColumnName("old_value");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.TargetType)
                .HasMaxLength(50)
                .HasColumnName("target_type");

            entity.HasOne(d => d.Admin).WithMany(p => p.AdminAuditLogs)
                .HasForeignKey(d => d.AdminId)
                .HasConstraintName("fk_audit_admin");
        });

        modelBuilder.Entity<AiConfig>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK__ai_confi__DFD83CAED05A657C");

            entity.ToTable("ai_configs");

            entity.Property(e => e.Key)
                .HasMaxLength(50)
                .HasColumnName("key");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("updated_at");
            entity.Property(e => e.Value).HasColumnName("value");
        });

        modelBuilder.Entity<AiGeneratedContent>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ai_gener__3213E83FF8A7A90D");

            entity.ToTable("ai_generated_content");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.AiOutput).HasColumnName("ai_output");
            entity.Property(e => e.ChapterId).HasColumnName("chapter_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.InputPrompt).HasColumnName("input_prompt");
            entity.Property(e => e.SimilarityScore)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("similarity_score");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Chapter).WithMany(p => p.AiGeneratedContents)
                .HasForeignKey(d => d.ChapterId)
                .HasConstraintName("fk_aigen_chapter");
        });

        modelBuilder.Entity<AiModelRegistry>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ai_model__3213E83FFA64022A");

            entity.ToTable("ai_model_registry");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ApiKeyVaultRef).HasColumnName("api_key_vault_ref");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.IsDefaultForGen)
                .HasDefaultValue(false)
                .HasColumnName("is_default_for_gen");
            entity.Property(e => e.IsDefaultForMod)
                .HasDefaultValue(false)
                .HasColumnName("is_default_for_mod");
            entity.Property(e => e.MaxTokens).HasColumnName("max_tokens");
            entity.Property(e => e.ModelName)
                .HasMaxLength(50)
                .HasColumnName("model_name");
            entity.Property(e => e.Provider)
                .HasMaxLength(50)
                .HasColumnName("provider");
            entity.Property(e => e.Temperature)
                .HasColumnType("decimal(3, 2)")
                .HasColumnName("temperature");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<AiPlagiarismReport>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ai_plagi__3213E83F16639DEE");

            entity.ToTable("ai_plagiarism_reports");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ChapterId).HasColumnName("chapter_id");
            entity.Property(e => e.CheckedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("checked_at");
            entity.Property(e => e.SimilarityRatio)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("similarity_ratio");
            entity.Property(e => e.SourceDetails).HasColumnName("source_details");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasColumnName("status");

            entity.HasOne(d => d.Chapter).WithMany(p => p.AiPlagiarismReports)
                .HasForeignKey(d => d.ChapterId)
                .HasConstraintName("fk_plag_chapter");
        });

        modelBuilder.Entity<AiSensitiveWord>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ai_sensi__3213E83FF1C3FF4A");

            entity.ToTable("ai_sensitive_words");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Category)
                .HasMaxLength(50)
                .HasColumnName("category");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.Word)
                .HasMaxLength(100)
                .HasColumnName("word");
        });

        modelBuilder.Entity<AiUsageLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ai_usage__3213E83F549AA16D");

            entity.ToTable("ai_usage_logs");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ActionType)
                .HasMaxLength(50)
                .HasColumnName("action_type");
            entity.Property(e => e.ChapterId).HasColumnName("chapter_id");
            entity.Property(e => e.CompletionTokens)
                .HasDefaultValue(0)
                .HasColumnName("completion_tokens");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.ModelName)
                .HasMaxLength(50)
                .HasColumnName("model_name");
            entity.Property(e => e.PromptTokens)
                .HasDefaultValue(0)
                .HasColumnName("prompt_tokens");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasColumnName("status");
            entity.Property(e => e.StoryId).HasColumnName("story_id");
            entity.Property(e => e.TotalTokens)
                .HasDefaultValue(0)
                .HasColumnName("total_tokens");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.AiUsageLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_ai_user");
        });

        modelBuilder.Entity<Appeal>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__appeals__3213E83F9A57C790");

            entity.ToTable("appeals");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.EvidenceUrl).HasColumnName("evidence_url");
            entity.Property(e => e.ReviewedBy).HasColumnName("reviewed_by");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("PENDING")
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.ViolationId).HasColumnName("violation_id");

            entity.HasOne(d => d.User).WithMany(p => p.Appeals)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_appeal_user");

            entity.HasOne(d => d.Violation).WithMany(p => p.Appeals)
                .HasForeignKey(d => d.ViolationId)
                .HasConstraintName("fk_appeal_viol");
        });

        modelBuilder.Entity<AuthToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__auth_tok__3213E83FD7857375");

            entity.ToTable("auth_tokens");

            entity.HasIndex(e => e.RefreshToken, "UQ__auth_tok__7FB69BADBBDCD6DB").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.DeviceInfo).HasColumnName("device_info");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.RefreshToken)
                .HasMaxLength(500)
                .HasColumnName("refresh_token");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.AuthTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_auth_user");
        });

        modelBuilder.Entity<AuthorBankAccount>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__author_b__B9BE370FE4F66118");

            entity.ToTable("author_bank_accounts");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.AccountHolderName)
                .HasMaxLength(100)
                .HasColumnName("account_holder_name");
            entity.Property(e => e.AccountNumber)
                .HasMaxLength(50)
                .HasColumnName("account_number");
            entity.Property(e => e.BankName)
                .HasMaxLength(100)
                .HasColumnName("bank_name");
            entity.Property(e => e.BranchName)
                .HasMaxLength(255)
                .HasColumnName("branch_name");
            entity.Property(e => e.IsVerified)
                .HasDefaultValue(false)
                .HasColumnName("is_verified");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.User).WithOne(p => p.AuthorBankAccount)
                .HasForeignKey<AuthorBankAccount>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_bank_user");
        });

        modelBuilder.Entity<AuthorIncomeLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__author_i__3213E83F292FF02A");

            entity.ToTable("author_income_logs");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.GrossAmount)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("gross_amount");
            entity.Property(e => e.NetAmount)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("net_amount");
            entity.Property(e => e.PlatformFee)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("platform_fee");
            entity.Property(e => e.SourceId).HasColumnName("source_id");
            entity.Property(e => e.SourceType)
                .HasMaxLength(20)
                .HasColumnName("source_type");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("AVAILABLE")
                .HasColumnName("status");

            entity.HasOne(d => d.Author).WithMany(p => p.AuthorIncomeLogs)
                .HasForeignKey(d => d.AuthorId)
                .HasConstraintName("fk_income_auth");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__categori__3213E83F0686D272");

            entity.ToTable("categories");

            entity.HasIndex(e => e.Slug, "UQ__categori__32DD1E4C26ABBA62").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IconUrl).HasColumnName("icon_url");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.ParentCategoryId).HasColumnName("parent_category_id");
            entity.Property(e => e.Slug)
                .HasMaxLength(100)
                .HasColumnName("slug");

            entity.HasOne(d => d.ParentCategory).WithMany(p => p.InverseParentCategory)
                .HasForeignKey(d => d.ParentCategoryId)
                .HasConstraintName("fk_category_parent");
        });

        modelBuilder.Entity<Chapter>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__chapters__3213E83F33BFFC3D");

            entity.ToTable("chapters");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.AccessType)
                .HasMaxLength(20)
                .HasDefaultValue("FREE")
                .HasColumnName("access_type");
            entity.Property(e => e.AiContributionRatio)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("ai_contribution_ratio");
            entity.Property(e => e.CoinPrice)
                .HasDefaultValue(0)
                .HasColumnName("coin_price");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.IsAiClean)
                .HasDefaultValue(false)
                .HasColumnName("is_ai_clean");
            entity.Property(e => e.OrderIndex).HasColumnName("order_index");
            entity.Property(e => e.PublishedAt).HasColumnName("published_at");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("DRAFT")
                .HasColumnName("status");
            entity.Property(e => e.StoryId).HasColumnName("story_id");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("updated_at");
            entity.Property(e => e.WordCount)
                .HasDefaultValue(0)
                .HasColumnName("word_count");

            entity.HasOne(d => d.Story).WithMany(p => p.Chapters)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("fk_chapters_story");
        });

        modelBuilder.Entity<ChapterVersion>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__chapter___3213E83FF8ED68B2");

            entity.ToTable("chapter_versions");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.ChangeSummary).HasColumnName("change_summary");
            entity.Property(e => e.ChapterId).HasColumnName("chapter_id");
            entity.Property(e => e.ContentSnapshot).HasColumnName("content_snapshot");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.VersionNumber).HasColumnName("version_number");

            entity.HasOne(d => d.Author).WithMany(p => p.ChapterVersions)
                .HasForeignKey(d => d.AuthorId)
                .HasConstraintName("fk_versions_author");

            entity.HasOne(d => d.Chapter).WithMany(p => p.ChapterVersions)
                .HasForeignKey(d => d.ChapterId)
                .HasConstraintName("fk_versions_chapter");
        });

        modelBuilder.Entity<CoinOrder>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__coin_ord__3213E83F1ED34C84");

            entity.ToTable("coin_orders");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.AmountPaid)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("amount_paid");
            entity.Property(e => e.CoinsGranted).HasColumnName("coins_granted");
            entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.GatewayResponseCode).HasColumnName("gateway_response_code");
            entity.Property(e => e.GatewayTransactionId)
                .HasMaxLength(255)
                .HasColumnName("gateway_transaction_id");
            entity.Property(e => e.PackageId).HasColumnName("package_id");
            entity.Property(e => e.PaymentGateway)
                .HasMaxLength(50)
                .HasColumnName("payment_gateway");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("PENDING")
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Package).WithMany(p => p.CoinOrders)
                .HasForeignKey(d => d.PackageId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_order_pkg");

            entity.HasOne(d => d.User).WithMany(p => p.CoinOrders)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_order_user");
        });

        modelBuilder.Entity<CoinPackage>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__coin_pac__3213E83F2F4880EE");

            entity.ToTable("coin_packages");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.BonusCoin)
                .HasDefaultValue(0)
                .HasColumnName("bonus_coin");
            entity.Property(e => e.CoinAmount).HasColumnName("coin_amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.Currency)
                .HasMaxLength(10)
                .HasDefaultValue("VND")
                .HasColumnName("currency");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .HasColumnName("name");
            entity.Property(e => e.PriceAmount)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("price_amount");
        });

        modelBuilder.Entity<Comment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__comments__3213E83FFDD37B0E");

            entity.ToTable("comments");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ChapterId).HasColumnName("chapter_id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.LikesCount)
                .HasDefaultValue(0)
                .HasColumnName("likes_count");
            entity.Property(e => e.ParentId).HasColumnName("parent_id");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("APPROVED")
                .HasColumnName("status");
            entity.Property(e => e.StoryId).HasColumnName("story_id");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Parent).WithMany(p => p.InverseParent)
                .HasForeignKey(d => d.ParentId)
                .HasConstraintName("fk_comm_parent");

            entity.HasOne(d => d.Story).WithMany(p => p.Comments)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("fk_comm_story");

            entity.HasOne(d => d.User).WithMany(p => p.Comments)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_comm_user");
        });

        modelBuilder.Entity<DailyStatistic>(entity =>
        {
            entity.HasKey(e => e.StatDate).HasName("PK__daily_st__38B70DF9AC372577");

            entity.ToTable("daily_statistics");

            entity.Property(e => e.StatDate).HasColumnName("stat_date");
            entity.Property(e => e.ActiveUsersCount)
                .HasDefaultValue(0)
                .HasColumnName("active_users_count");
            entity.Property(e => e.NewChaptersCount)
                .HasDefaultValue(0)
                .HasColumnName("new_chapters_count");
            entity.Property(e => e.NewStoriesCount)
                .HasDefaultValue(0)
                .HasColumnName("new_stories_count");
            entity.Property(e => e.NewUsersCount)
                .HasDefaultValue(0)
                .HasColumnName("new_users_count");
            entity.Property(e => e.PendingReportsCount)
                .HasDefaultValue(0)
                .HasColumnName("pending_reports_count");
            entity.Property(e => e.PendingWithdrawalsCount)
                .HasDefaultValue(0)
                .HasColumnName("pending_withdrawals_count");
            entity.Property(e => e.TotalCoinsSpent)
                .HasDefaultValue(0)
                .HasColumnName("total_coins_spent");
            entity.Property(e => e.TotalRevenueNaira)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("total_revenue_naira");
            entity.Property(e => e.TotalViewsDay)
                .HasDefaultValue(0L)
                .HasColumnName("total_views_day");
            entity.Property(e => e.TotalWithdrawalsPaid)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("total_withdrawals_paid");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<Donation>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__donation__3213E83F1E90BEDB");

            entity.ToTable("donations");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Amount).HasColumnName("amount");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.Message).HasColumnName("message");
            entity.Property(e => e.ReceiverId).HasColumnName("receiver_id");
            entity.Property(e => e.SenderId).HasColumnName("sender_id");
            entity.Property(e => e.StoryId).HasColumnName("story_id");

            entity.HasOne(d => d.Receiver).WithMany(p => p.DonationReceivers)
                .HasForeignKey(d => d.ReceiverId)
                .HasConstraintName("fk_don_receiver");

            entity.HasOne(d => d.Sender).WithMany(p => p.DonationSenders)
                .HasForeignKey(d => d.SenderId)
                .HasConstraintName("fk_don_sender");
        });

        modelBuilder.Entity<Follow>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.AuthorId }).HasName("PK__follows__51DB21B363A8944F");

            entity.ToTable("follows");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.FollowedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("followed_at");

            entity.HasOne(d => d.Author).WithMany(p => p.FollowAuthors)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_follows_author");

            entity.HasOne(d => d.User).WithMany(p => p.FollowUsers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_follows_user");
        });

        modelBuilder.Entity<IdeaPost>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__idea_pos__3213E83F9850CAED");

            entity.ToTable("idea_posts");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
            entity.Property(e => e.RewardCoin)
                .HasDefaultValue(0)
                .HasColumnName("reward_coin");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("OPEN")
                .HasColumnName("status");
            entity.Property(e => e.StoryId).HasColumnName("story_id");
            entity.Property(e => e.Topic).HasColumnName("topic");

            entity.HasOne(d => d.Story).WithMany(p => p.IdeaPosts)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("fk_idea_story");
        });

        modelBuilder.Entity<IdeaProposal>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__idea_pro__3213E83FC8D1B7B1");

            entity.ToTable("idea_proposals");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.IsSelected)
                .HasDefaultValue(false)
                .HasColumnName("is_selected");
            entity.Property(e => e.PostId).HasColumnName("post_id");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("ACTIVE")
                .HasColumnName("status");
            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.VoteCount)
                .HasDefaultValue(0)
                .HasColumnName("vote_count");

            entity.HasOne(d => d.Post).WithMany(p => p.IdeaProposals)
                .HasForeignKey(d => d.PostId)
                .HasConstraintName("fk_prop_post");
        });

        modelBuilder.Entity<MarketingBanner>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__marketin__3213E83F4B89ABE4");

            entity.ToTable("marketing_banners");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.EndDate).HasColumnName("end_date");
            entity.Property(e => e.ImageUrl).HasColumnName("image_url");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.LinkUrl).HasColumnName("link_url");
            entity.Property(e => e.Position)
                .HasMaxLength(50)
                .HasColumnName("position");
            entity.Property(e => e.Priority)
                .HasDefaultValue(0)
                .HasColumnName("priority");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
        });

        modelBuilder.Entity<ModerationLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__moderati__3213E83FF2D9096F");

            entity.ToTable("moderation_logs");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.Action)
                .HasMaxLength(20)
                .HasColumnName("action");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.ModeratorId).HasColumnName("moderator_id");
            entity.Property(e => e.ProcessingTimeMs).HasColumnName("processing_time_ms");
            entity.Property(e => e.RejectionReason).HasColumnName("rejection_reason");
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.TargetType)
                .HasMaxLength(20)
                .HasColumnName("target_type");

            entity.HasOne(d => d.Moderator).WithMany(p => p.ModerationLogs)
                .HasForeignKey(d => d.ModeratorId)
                .HasConstraintName("fk_mod_user");
        });

        modelBuilder.Entity<Notification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__notifica__3213E83F372A4DF2");

            entity.ToTable("notifications");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.IsRead)
                .HasDefaultValue(false)
                .HasColumnName("is_read");
            entity.Property(e => e.LinkUrl).HasColumnName("link_url");
            entity.Property(e => e.Title).HasColumnName("title");
            entity.Property(e => e.Type)
                .HasMaxLength(50)
                .HasColumnName("type");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.Notifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_notif_user");
        });

        modelBuilder.Entity<OtpVerification>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__otp_veri__3213E83F7195A841");

            entity.ToTable("otp_verifications");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.ExpiredAt).HasColumnName("expired_at");
            entity.Property(e => e.IsUsed)
                .HasDefaultValue(false)
                .HasColumnName("is_used");
            entity.Property(e => e.OtpCode)
                .HasMaxLength(6)
                .HasColumnName("otp_code");
            entity.Property(e => e.Type)
                .HasMaxLength(20)
                .HasColumnName("type");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.OtpVerifications)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_otp_user");
        });

        modelBuilder.Entity<Purchase>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__purchase__3213E83F5F7D69E9");

            entity.ToTable("purchases");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ChapterId).HasColumnName("chapter_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.EscrowStatus)
                .HasMaxLength(20)
                .HasDefaultValue("NA")
                .HasColumnName("escrow_status");
            entity.Property(e => e.PlatformFeeRatio)
                .HasDefaultValue(30.00m)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("platform_fee_ratio");
            entity.Property(e => e.PricePaid).HasColumnName("price_paid");
            entity.Property(e => e.PurchaseType)
                .HasMaxLength(20)
                .HasColumnName("purchase_type");
            entity.Property(e => e.ReleasedAt).HasColumnName("released_at");
            entity.Property(e => e.StoryId).HasColumnName("story_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Story).WithMany(p => p.Purchases)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("fk_purch_story");

            entity.HasOne(d => d.User).WithMany(p => p.Purchases)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_purch_user");
        });

        modelBuilder.Entity<Rating>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__ratings__3213E83F42244565");

            entity.ToTable("ratings");

            entity.HasIndex(e => new { e.UserId, e.StoryId }, "uk_ratings").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.ReviewText).HasColumnName("review_text");
            entity.Property(e => e.StarValue).HasColumnName("star_value");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("VISIBLE")
                .HasColumnName("status");
            entity.Property(e => e.StoryId).HasColumnName("story_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Story).WithMany(p => p.Ratings)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("fk_ratings_story");

            entity.HasOne(d => d.User).WithMany(p => p.Ratings)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_ratings_user");
        });

        modelBuilder.Entity<Report>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__reports__3213E83F7F3093D8");

            entity.ToTable("reports");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.AssignedTo).HasColumnName("assigned_to");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.ReasonCategory)
                .HasMaxLength(50)
                .HasColumnName("reason_category");
            entity.Property(e => e.ReporterId).HasColumnName("reporter_id");
            entity.Property(e => e.ResolvedAt).HasColumnName("resolved_at");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("NEW")
                .HasColumnName("status");
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.TargetType)
                .HasMaxLength(20)
                .HasColumnName("target_type");

            entity.HasOne(d => d.AssignedToNavigation).WithMany(p => p.ReportAssignedToNavigations)
                .HasForeignKey(d => d.AssignedTo)
                .HasConstraintName("fk_rep_assignee");

            entity.HasOne(d => d.Reporter).WithMany(p => p.ReportReporters)
                .HasForeignKey(d => d.ReporterId)
                .HasConstraintName("fk_rep_reporter");
        });

        modelBuilder.Entity<ReportEvidence>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__report_e__3213E83FF4F679D3");

            entity.ToTable("report_evidences");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.EvidenceText).HasColumnName("evidence_text");
            entity.Property(e => e.EvidenceUrl).HasColumnName("evidence_url");
            entity.Property(e => e.ReportId).HasColumnName("report_id");

            entity.HasOne(d => d.Report).WithMany(p => p.ReportEvidences)
                .HasForeignKey(d => d.ReportId)
                .HasConstraintName("fk_evid_rep");
        });

        modelBuilder.Entity<Story>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__stories__3213E83FF6C87BDE");

            entity.ToTable("stories");

            entity.HasIndex(e => e.Slug, "UQ__stories__32DD1E4CFCED3C5C").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.AgeRating)
                .HasMaxLength(10)
                .HasDefaultValue("ALL")
                .HasColumnName("age_rating");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.AvgRating)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(3, 2)")
                .HasColumnName("avg_rating");
            entity.Property(e => e.CoverImage).HasColumnName("cover_image");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.LastPublishedAt).HasColumnName("last_published_at");
            entity.Property(e => e.PublishedAt).HasColumnName("published_at");
            entity.Property(e => e.Slug)
                .HasMaxLength(255)
                .HasColumnName("slug");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("DRAFT")
                .HasColumnName("status");
            entity.Property(e => e.StoryProgressStatus)
                .HasMaxLength(20)
                .HasDefaultValue("ONGOING")
                .HasColumnName("story_progress_status");
            entity.Property(e => e.Summary).HasColumnName("summary");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .HasColumnName("title");
            entity.Property(e => e.TotalChapters)
                .HasDefaultValue(0)
                .HasColumnName("total_chapters");
            entity.Property(e => e.TotalFavorites)
                .HasDefaultValue(0)
                .HasColumnName("total_favorites");
            entity.Property(e => e.TotalViews)
                .HasDefaultValue(0L)
                .HasColumnName("total_views");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("updated_at");
            entity.Property(e => e.WordCount)
                .HasDefaultValue(0)
                .HasColumnName("word_count");

            entity.HasOne(d => d.Author).WithMany(p => p.Stories)
                .HasForeignKey(d => d.AuthorId)
                .HasConstraintName("fk_stories_author");

            entity.HasMany(d => d.Categories).WithMany(p => p.Stories)
                .UsingEntity<Dictionary<string, object>>(
                    "StoryCategory",
                    r => r.HasOne<Category>().WithMany()
                        .HasForeignKey("CategoryId")
                        .HasConstraintName("fk_sc_category"),
                    l => l.HasOne<Story>().WithMany()
                        .HasForeignKey("StoryId")
                        .HasConstraintName("fk_sc_story"),
                    j =>
                    {
                        j.HasKey("StoryId", "CategoryId").HasName("PK__story_ca__3B6772CD07458A65");
                        j.ToTable("story_categories");
                        j.IndexerProperty<Guid>("StoryId").HasColumnName("story_id");
                        j.IndexerProperty<Guid>("CategoryId").HasColumnName("category_id");
                    });
        });

        modelBuilder.Entity<StoryCommitment>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__story_co__3213E83F07CBDC49");

            entity.ToTable("story_commitments");

            entity.HasIndex(e => new { e.StoryId, e.UserId }, "uk_story_commitments").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .HasColumnName("ip_address");
            entity.Property(e => e.PolicyVersion)
                .HasMaxLength(20)
                .HasColumnName("policy_version");
            entity.Property(e => e.SignedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("signed_at");
            entity.Property(e => e.StoryId).HasColumnName("story_id");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.Story).WithMany(p => p.StoryCommitments)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("fk_commit_story");

            entity.HasOne(d => d.User).WithMany(p => p.StoryCommitments)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_commit_user");
        });

        modelBuilder.Entity<SystemPolicy>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__system_p__3213E83FD9A9C65F");

            entity.ToTable("system_policies");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ActivatedAt).HasColumnName("activated_at");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(false)
                .HasColumnName("is_active");
            entity.Property(e => e.RequireResign)
                .HasDefaultValue(false)
                .HasColumnName("require_resign");
            entity.Property(e => e.Type)
                .HasMaxLength(20)
                .HasColumnName("type");
            entity.Property(e => e.Version)
                .HasMaxLength(20)
                .HasColumnName("version");
        });

        modelBuilder.Entity<SystemSetting>(entity =>
        {
            entity.HasKey(e => e.Key).HasName("PK__system_s__DFD83CAEFE36E280");

            entity.ToTable("system_settings");

            entity.Property(e => e.Key)
                .HasMaxLength(100)
                .HasColumnName("key");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("updated_at");
            entity.Property(e => e.UpdatedBy).HasColumnName("updated_by");
            entity.Property(e => e.Value).HasColumnName("value");
            entity.Property(e => e.ValueType)
                .HasMaxLength(20)
                .HasColumnName("value_type");

            entity.HasOne(d => d.UpdatedByNavigation).WithMany(p => p.SystemSettings)
                .HasForeignKey(d => d.UpdatedBy)
                .HasConstraintName("fk_sys_user");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__users__3213E83F7E00E162");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "UQ__users__AB6E616494613327").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.DeletionRequestedAt).HasColumnName("deletion_requested_at");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .HasColumnName("email");
            entity.Property(e => e.EmailVerifiedAt).HasColumnName("email_verified_at");
            entity.Property(e => e.MustResignPolicy)
                .HasDefaultValue(false)
                .HasColumnName("must_resign_policy");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .HasDefaultValue("USER")
                .HasColumnName("role");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("PENDING")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("updated_at");

            entity.HasMany(d => d.CommentsNavigation).WithMany(p => p.Users)
                .UsingEntity<Dictionary<string, object>>(
                    "CommentLike",
                    r => r.HasOne<Comment>().WithMany()
                        .HasForeignKey("CommentId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_clikes_comm"),
                    l => l.HasOne<User>().WithMany()
                        .HasForeignKey("UserId")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_clikes_user"),
                    j =>
                    {
                        j.HasKey("UserId", "CommentId").HasName("PK__comment___D7C76067F6C75CCE");
                        j.ToTable("comment_likes");
                        j.IndexerProperty<Guid>("UserId").HasColumnName("user_id");
                        j.IndexerProperty<Guid>("CommentId").HasColumnName("comment_id");
                    });
        });

        modelBuilder.Entity<UserActivityLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__user_act__3213E83F22F89632");

            entity.ToTable("user_activity_logs");

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.ActionType)
                .HasMaxLength(50)
                .HasColumnName("action_type");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.DeviceInfo).HasColumnName("device_info");
            entity.Property(e => e.IpAddress)
                .HasMaxLength(45)
                .HasColumnName("ip_address");
            entity.Property(e => e.RawData).HasColumnName("raw_data");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.UserActivityLogs)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_activity_user");
        });

        modelBuilder.Entity<UserLibrary>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.StoryId, e.RelationType }).HasName("PK__user_lib__33B829F62803C86C");

            entity.ToTable("user_library");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.StoryId).HasColumnName("story_id");
            entity.Property(e => e.RelationType)
                .HasMaxLength(20)
                .HasColumnName("relation_type");
            entity.Property(e => e.LastReadAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("last_read_at");
            entity.Property(e => e.LastReadChapterId).HasColumnName("last_read_chapter_id");

            entity.HasOne(d => d.Story).WithMany(p => p.UserLibraries)
                .HasForeignKey(d => d.StoryId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_lib_story");

            entity.HasOne(d => d.User).WithMany(p => p.UserLibraries)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_lib_user");
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__user_pro__B9BE370F096F317C");

            entity.ToTable("user_profiles");

            entity.HasIndex(e => e.Nickname, "UQ__user_pro__5CF1C59B4A1760AF").IsUnique();

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.AvatarUrl).HasColumnName("avatar_url");
            entity.Property(e => e.Bio).HasColumnName("bio");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Nickname)
                .HasMaxLength(100)
                .UseCollation("Latin1_General_CI_AS")
                .HasColumnName("nickname");
            entity.Property(e => e.Settings)
                .HasDefaultValue("{\"allow_notif\": true, \"dark_mode\": false}")
                .HasColumnName("settings");
            entity.Property(e => e.SocialLinks)
                .HasDefaultValue("{}")
                .HasColumnName("social_links");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("updated_at");
            entity.Property(e => e.Phone)
              .HasColumnName("phone")
              .HasMaxLength(20); 
            entity.Property(e => e.IdNumber)
                  .HasColumnName("id_number")
                  .HasMaxLength(20);

            entity.HasOne(d => d.User).WithOne(p => p.UserProfile)
                .HasForeignKey<UserProfile>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_profiles_user");
        });

        modelBuilder.Entity<UserVoucher>(entity =>
        {
            entity.HasKey(e => new { e.UserId, e.VoucherId }).HasName("PK__user_vou__21B558F5C926933D");

            entity.ToTable("user_vouchers");

            entity.Property(e => e.UserId).HasColumnName("user_id");
            entity.Property(e => e.VoucherId).HasColumnName("voucher_id");
            entity.Property(e => e.AppliedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("applied_at");
            entity.Property(e => e.OrderId).HasColumnName("order_id");

            entity.HasOne(d => d.User).WithMany(p => p.UserVouchers)
                .HasForeignKey(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_uv_user");

            entity.HasOne(d => d.Voucher).WithMany(p => p.UserVouchers)
                .HasForeignKey(d => d.VoucherId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_uv_vouch");
        });

        modelBuilder.Entity<ViolationLog>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__violatio__3213E83FEA2E5775");

            entity.ToTable("violation_logs");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.ComplianceOfficerId).HasColumnName("compliance_officer_id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.IsAppealed)
                .HasDefaultValue(false)
                .HasColumnName("is_appealed");
            entity.Property(e => e.IsRefunded)
                .HasDefaultValue(false)
                .HasColumnName("is_refunded");
            entity.Property(e => e.PenaltyType)
                .HasMaxLength(50)
                .HasColumnName("penalty_type");
            entity.Property(e => e.PolicyReference)
                .HasMaxLength(100)
                .HasColumnName("policy_reference");
            entity.Property(e => e.Reason).HasColumnName("reason");
            entity.Property(e => e.TargetId).HasColumnName("target_id");
            entity.Property(e => e.TargetType)
                .HasMaxLength(20)
                .HasColumnName("target_type");
            entity.Property(e => e.TotalRefundedAmount)
                .HasDefaultValue(0)
                .HasColumnName("total_refunded_amount");
            entity.Property(e => e.ViolatorId).HasColumnName("violator_id");

            entity.HasOne(d => d.ComplianceOfficer).WithMany(p => p.ViolationLogComplianceOfficers)
                .HasForeignKey(d => d.ComplianceOfficerId)
                .HasConstraintName("fk_viol_officer");

            entity.HasOne(d => d.Violator).WithMany(p => p.ViolationLogViolators)
                .HasForeignKey(d => d.ViolatorId)
                .HasConstraintName("fk_viol_user");
        });

        modelBuilder.Entity<Voucher>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__vouchers__3213E83F3D650B2E");

            entity.ToTable("vouchers");

            entity.HasIndex(e => e.Code, "UQ__vouchers__357D4CF961B75F30").IsUnique();

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.Code)
                .HasMaxLength(50)
                .HasColumnName("code");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.ExpiryDate).HasColumnName("expiry_date");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.MaxDiscountAmount)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("max_discount_amount");
            entity.Property(e => e.MinOrderValue)
                .HasDefaultValue(0)
                .HasColumnName("min_order_value");
            entity.Property(e => e.StartDate).HasColumnName("start_date");
            entity.Property(e => e.Type)
                .HasMaxLength(20)
                .HasColumnName("type");
            entity.Property(e => e.UsageLimit)
                .HasDefaultValue(1)
                .HasColumnName("usage_limit");
            entity.Property(e => e.UsedCount)
                .HasDefaultValue(0)
                .HasColumnName("used_count");
            entity.Property(e => e.Value)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("value");
        });

        modelBuilder.Entity<Wallet>(entity =>
        {
            entity.HasKey(e => e.UserId).HasName("PK__wallets__B9BE370F595A2BB1");

            entity.ToTable("wallets");

            entity.Property(e => e.UserId)
                .ValueGeneratedNever()
                .HasColumnName("user_id");
            entity.Property(e => e.BalanceCoin)
                .HasDefaultValue(0)
                .HasColumnName("balance_coin");
            entity.Property(e => e.Currency)
                .HasMaxLength(10)
                .HasDefaultValue("VND")
                .HasColumnName("currency");
            entity.Property(e => e.FrozenBalance)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("frozen_balance");
            entity.Property(e => e.IncomeBalance)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("income_balance");
            entity.Property(e => e.PendingEscrowBalance)
                .HasDefaultValue(0.00m)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("pending_escrow_balance");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("updated_at");

            entity.HasOne(d => d.User).WithOne(p => p.Wallet)
                .HasForeignKey<Wallet>(d => d.UserId)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_wallets_user");
        });

        modelBuilder.Entity<WithdrawRequest>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__withdraw__3213E83F8D007D01");

            entity.ToTable("withdraw_requests");

            entity.Property(e => e.Id)
                .HasDefaultValueSql("(newid())")
                .HasColumnName("id");
            entity.Property(e => e.AdminNote).HasColumnName("admin_note");
            entity.Property(e => e.AmountRequested)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("amount_requested");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.BankInfoSnapshot).HasColumnName("bank_info_snapshot");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnName("created_at");
            entity.Property(e => e.FeeAmount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(15, 2)")
                .HasColumnName("fee_amount");
            entity.Property(e => e.ProcessedAt).HasColumnName("processed_at");
            entity.Property(e => e.ProcessedBy).HasColumnName("processed_by");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .HasDefaultValue("PENDING")
                .HasColumnName("status");
            entity.Property(e => e.TransactionProofUrl).HasColumnName("transaction_proof_url");

            entity.HasOne(d => d.Author).WithMany(p => p.WithdrawRequestAuthors)
                .HasForeignKey(d => d.AuthorId)
                .HasConstraintName("fk_with_auth");

            entity.HasOne(d => d.ProcessedByNavigation).WithMany(p => p.WithdrawRequestProcessedByNavigations)
                .HasForeignKey(d => d.ProcessedBy)
                .HasConstraintName("fk_with_admin");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
