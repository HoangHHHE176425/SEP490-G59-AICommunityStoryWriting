using System;
using System.Collections.Generic;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessObjects;

public partial class StoryPlatformDbContext : DbContext
{
    public StoryPlatformDbContext()
    {
    }

    public StoryPlatformDbContext(DbContextOptions<StoryPlatformDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<admin_audit_logs> admin_audit_logs { get; set; }

    public virtual DbSet<ai_configs> ai_configs { get; set; }

    public virtual DbSet<ai_generated_content> ai_generated_content { get; set; }

    public virtual DbSet<ai_sensitive_words> ai_sensitive_words { get; set; }

    public virtual DbSet<ai_usage_logs> ai_usage_logs { get; set; }

    public virtual DbSet<appeals> appeals { get; set; }

    public virtual DbSet<auth_tokens> auth_tokens { get; set; }

    public virtual DbSet<author_bank_accounts> author_bank_accounts { get; set; }

    public virtual DbSet<author_income_logs> author_income_logs { get; set; }

    public virtual DbSet<author_policy_acceptances> author_policy_acceptances { get; set; }

    public virtual DbSet<categories> categories { get; set; }

    public virtual DbSet<chapter_versions> chapter_versions { get; set; }

    public virtual DbSet<chapters> chapters { get; set; }

    public virtual DbSet<coin_orders> coin_orders { get; set; }

    public virtual DbSet<coin_packages> coin_packages { get; set; }

    public virtual DbSet<comment_reactions> comment_reactions { get; set; }

    public virtual DbSet<comments> comments { get; set; }

    public virtual DbSet<daily_statistics> daily_statistics { get; set; }

    public virtual DbSet<donations> donations { get; set; }

    public virtual DbSet<follows> follows { get; set; }

    public virtual DbSet<idea_posts> idea_posts { get; set; }

    public virtual DbSet<idea_proposals> idea_proposals { get; set; }

    public virtual DbSet<marketing_banners> marketing_banners { get; set; }

    public virtual DbSet<moderation_logs> moderation_logs { get; set; }

    public virtual DbSet<moderator_category_assignments> moderator_category_assignments { get; set; }

    public virtual DbSet<notifications> notifications { get; set; }

    public virtual DbSet<otp_verifications> otp_verifications { get; set; }

    public virtual DbSet<platform_wallet> platform_wallet { get; set; }

    public virtual DbSet<purchases> purchases { get; set; }

    public virtual DbSet<ratings> ratings { get; set; }

    public virtual DbSet<report_evidences> report_evidences { get; set; }

    public virtual DbSet<story_report_contributors> story_report_contributors { get; set; }

    public virtual DbSet<compliance_story_report_lock_requests> compliance_story_report_lock_requests { get; set; }

    public virtual DbSet<compliance_admin_action_requests> compliance_admin_action_requests { get; set; }

    public virtual DbSet<reports> reports { get; set; }

    public virtual DbSet<review_assignments> review_assignments { get; set; }

    public virtual DbSet<review_escalation_requests> review_escalation_requests { get; set; }

    public virtual DbSet<stories> stories { get; set; }

    public virtual DbSet<story_character_memory> story_character_memory { get; set; }

    public virtual DbSet<story_commitments> story_commitments { get; set; }

    public virtual DbSet<story_event_memory> story_event_memory { get; set; }

    public virtual DbSet<story_story_state> story_story_state { get; set; }

    public virtual DbSet<story_versions> story_versions { get; set; }

    public virtual DbSet<system_policies> system_policies { get; set; }

    public virtual DbSet<system_settings> system_settings { get; set; }

    public virtual DbSet<user_activity_logs> user_activity_logs { get; set; }

    public virtual DbSet<user_library> user_library { get; set; }

    public virtual DbSet<user_profiles> user_profiles { get; set; }

    public virtual DbSet<user_vouchers> user_vouchers { get; set; }

    public virtual DbSet<users> users { get; set; }

    public virtual DbSet<violation_logs> violation_logs { get; set; }

    public virtual DbSet<vouchers> vouchers { get; set; }

    public virtual DbSet<wallets> wallets { get; set; }

    public virtual DbSet<withdraw_requests> withdraw_requests { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer(
                "Server= localhost;uid=sa;password=a123;database=story_platform_v13;Encrypt=True;TrustServerCertificate=True;",
                sqlServerOptions => sqlServerOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(30),
                    errorNumbersToAdd: null));
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<admin_audit_logs>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__admin_au__3213E83F18E6D20F");

            entity.Property(e => e.action_type).HasMaxLength(50);
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ip_address).HasMaxLength(45);
            entity.Property(e => e.target_type).HasMaxLength(50);

            entity.HasOne(d => d.admin).WithMany(p => p.admin_audit_logs)
                .HasForeignKey(d => d.admin_id)
                .HasConstraintName("fk_audit_admin");
        });

        modelBuilder.Entity<ai_configs>(entity =>
        {
            entity.HasKey(e => e.key).HasName("PK__ai_confi__DFD83CAEA0138069");

            entity.Property(e => e.key).HasMaxLength(50);
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<ai_generated_content>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__ai_gener__3213E83FB120536F");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.chapter_index);

            entity.HasOne(d => d.chapter).WithMany(p => p.ai_generated_content)
                .HasForeignKey(d => d.chapter_id)
                .HasConstraintName("fk_aigen_chapter");
        });
        modelBuilder.Entity<ai_sensitive_words>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__ai_sensi__3213E83F668D9B1C");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.category).HasMaxLength(50);
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.word).HasMaxLength(100);
        });

        modelBuilder.Entity<ai_usage_logs>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__ai_usage__3213E83F63EFF73F");

            entity.Property(e => e.action_type).HasMaxLength(50);
            entity.Property(e => e.completion_tokens).HasDefaultValue(0);
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.model_name).HasMaxLength(50);
            entity.Property(e => e.prompt_tokens).HasDefaultValue(0);
            entity.Property(e => e.status).HasMaxLength(20);
            entity.Property(e => e.total_tokens).HasDefaultValue(0);

            entity.HasOne(d => d.user).WithMany(p => p.ai_usage_logs)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_ai_user");
        });

        modelBuilder.Entity<appeals>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__appeals__3213E83FD201903C");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .HasDefaultValue("PENDING");

            entity.HasOne(d => d.user).WithMany(p => p.appeals)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_appeal_user");

            entity.HasOne(d => d.violation).WithMany(p => p.appeals)
                .HasForeignKey(d => d.violation_id)
                .HasConstraintName("fk_appeal_viol");
        });

        modelBuilder.Entity<auth_tokens>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__auth_tok__3213E83F274B7121");

            entity.HasIndex(e => e.refresh_token, "UQ__auth_tok__7FB69BAD4F694F22").IsUnique();

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.refresh_token).HasMaxLength(500);

            entity.HasOne(d => d.user).WithMany(p => p.auth_tokens)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_auth_user");
        });

        modelBuilder.Entity<author_bank_accounts>(entity =>
        {
            entity.HasKey(e => e.user_id).HasName("PK__author_b__B9BE370FEC5823B3");

            entity.Property(e => e.user_id).ValueGeneratedNever();
            entity.Property(e => e.account_holder_name).HasMaxLength(100);
            entity.Property(e => e.account_number).HasMaxLength(50);
            entity.Property(e => e.bank_name).HasMaxLength(100);
            entity.Property(e => e.branch_name).HasMaxLength(255);
            entity.Property(e => e.is_verified).HasDefaultValue(false);
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.user).WithOne(p => p.author_bank_accounts)
                .HasForeignKey<author_bank_accounts>(d => d.user_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_bank_user");
        });

        modelBuilder.Entity<author_income_logs>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__author_i__3213E83F406131D5");

            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.gross_amount).HasColumnType("decimal(15, 2)");
            entity.Property(e => e.net_amount).HasColumnType("decimal(15, 2)");
            entity.Property(e => e.platform_fee).HasColumnType("decimal(15, 2)");
            entity.Property(e => e.source_type).HasMaxLength(20);
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .HasDefaultValue("AVAILABLE");

            entity.HasOne(d => d.author).WithMany(p => p.author_income_logs)
                .HasForeignKey(d => d.author_id)
                .HasConstraintName("fk_income_auth");
        });

        modelBuilder.Entity<author_policy_acceptances>(entity =>
        {
            entity.HasIndex(e => new { e.policy_id, e.accepted_at }, "IX_author_policy_acceptances_policy").IsDescending(false, true);

            entity.HasIndex(e => new { e.user_id, e.accepted_at }, "IX_author_policy_acceptances_user").IsDescending(false, true);

            entity.HasIndex(e => new { e.user_id, e.policy_id }, "UQ_author_policy_acceptances_user_policy").IsUnique();

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.accepted_at)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.accepted_for)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("AUTHOR");
            entity.Property(e => e.ip_address)
                .HasMaxLength(45)
                .IsUnicode(false);
            entity.Property(e => e.user_agent).HasMaxLength(256);

            entity.HasOne(d => d.policy).WithMany(p => p.author_policy_acceptances)
                .HasForeignKey(d => d.policy_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_author_policy_acceptances_policy");

            entity.HasOne(d => d.user).WithMany(p => p.author_policy_acceptances)
                .HasForeignKey(d => d.user_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_author_policy_acceptances_user");
        });

        modelBuilder.Entity<categories>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__categori__3213E83F073BAF97");

            entity.HasIndex(e => e.slug, "UQ__categori__32DD1E4C2D81D8D2").IsUnique();

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.name).HasMaxLength(100);
            entity.Property(e => e.slug).HasMaxLength(100);
        });

        modelBuilder.Entity<chapter_versions>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__chapter___3213E83F704C1A60");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.title_snapshot).HasMaxLength(255);
            entity.Property(e => e.ai_similarity_percent);

            entity.HasOne(d => d.author).WithMany(p => p.chapter_versions)
                .HasForeignKey(d => d.author_id)
                .HasConstraintName("fk_versions_author");

            entity.HasOne(d => d.chapter).WithMany(p => p.chapter_versions)
                .HasForeignKey(d => d.chapter_id)
                .HasConstraintName("fk_versions_chapter");
        });

        modelBuilder.Entity<chapters>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__chapters__3213E83F338EC550");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.access_type)
                .HasMaxLength(20)
                .HasDefaultValue("FREE");
            entity.Property(e => e.ai_contribution_ratio)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.ai_similarity_percent).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.coin_price).HasDefaultValue(0);
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_ai_clean).HasDefaultValue(false);
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.title).HasMaxLength(255);
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.submitted_for_review_at).HasColumnType("datetime2");
            entity.Property(e => e.word_count).HasDefaultValue(0);

            entity.HasOne(d => d.story).WithMany(p => p.chapters)
                .HasForeignKey(d => d.story_id)
                .HasConstraintName("fk_chapters_story");
        });

        modelBuilder.Entity<coin_orders>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__coin_ord__3213E83FA9BF69E8");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.amount_paid).HasColumnType("decimal(15, 2)");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.gateway_transaction_id).HasMaxLength(255);
            entity.Property(e => e.payment_gateway).HasMaxLength(50);
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .HasDefaultValue("PENDING");

            entity.HasOne(d => d.package).WithMany(p => p.coin_orders)
                .HasForeignKey(d => d.package_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_order_pkg");

            entity.HasOne(d => d.user).WithMany(p => p.coin_orders)
                .HasForeignKey(d => d.user_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_order_user");
        });

        modelBuilder.Entity<coin_packages>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__coin_pac__3213E83F1B21ACD7");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.bonus_coin).HasDefaultValue(0);
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.currency)
                .HasMaxLength(10)
                .HasDefaultValue("VND");
            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.name).HasMaxLength(100);
            entity.Property(e => e.price_amount).HasColumnType("decimal(15, 2)");
        });

        modelBuilder.Entity<comment_reactions>(entity =>
        {
            entity.HasKey(e => new { e.user_id, e.comment_id }).HasName("PK__comment___D7C76067E9C71316");

            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.reaction_type).HasMaxLength(20);

            entity.HasOne(d => d.comment).WithMany(p => p.comment_reactions)
                .HasForeignKey(d => d.comment_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_creact_comment");

            entity.HasOne(d => d.user).WithMany(p => p.comment_reactions)
                .HasForeignKey(d => d.user_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_creact_user");
        });

        modelBuilder.Entity<comments>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__comments__3213E83F34EAD765");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.likes_count).HasDefaultValue(0);
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .HasDefaultValue("APPROVED");
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.parent).WithMany(p => p.Inverseparent)
                .HasForeignKey(d => d.parent_id)
                .HasConstraintName("fk_comm_parent");

            entity.HasOne(d => d.story).WithMany(p => p.comments)
                .HasForeignKey(d => d.story_id)
                .HasConstraintName("fk_comm_story");

            entity.HasOne(d => d.userNavigation).WithMany(p => p.comments)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_comm_user");
        });

        modelBuilder.Entity<daily_statistics>(entity =>
        {
            entity.HasKey(e => e.stat_date).HasName("PK__daily_st__38B70DF9D4077D5B");

            entity.Property(e => e.active_users_count).HasDefaultValue(0);
            entity.Property(e => e.new_chapters_count).HasDefaultValue(0);
            entity.Property(e => e.new_stories_count).HasDefaultValue(0);
            entity.Property(e => e.new_users_count).HasDefaultValue(0);
            entity.Property(e => e.pending_reports_count).HasDefaultValue(0);
            entity.Property(e => e.pending_withdrawals_count).HasDefaultValue(0);
            entity.Property(e => e.total_coins_spent).HasDefaultValue(0);
            entity.Property(e => e.total_revenue_naira)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(15, 2)");
            entity.Property(e => e.total_views_day).HasDefaultValue(0L);
            entity.Property(e => e.total_withdrawals_paid)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(15, 2)");
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<donations>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__donation__3213E83FA16407BA");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.receiver).WithMany(p => p.donationsreceiver)
                .HasForeignKey(d => d.receiver_id)
                .HasConstraintName("fk_don_receiver");

            entity.HasOne(d => d.sender).WithMany(p => p.donationssender)
                .HasForeignKey(d => d.sender_id)
                .HasConstraintName("fk_don_sender");
        });

        modelBuilder.Entity<follows>(entity =>
        {
            entity.HasKey(e => new { e.user_id, e.author_id }).HasName("PK__follows__51DB21B3DAD7E967");

            entity.Property(e => e.followed_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.author).WithMany(p => p.followsauthor)
                .HasForeignKey(d => d.author_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_follows_author");

            entity.HasOne(d => d.user).WithMany(p => p.followsuser)
                .HasForeignKey(d => d.user_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_follows_user");
        });

        modelBuilder.Entity<idea_posts>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__idea_pos__3213E83F90D57DEB");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.reward_coin).HasDefaultValue(0);
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .HasDefaultValue("OPEN");

            entity.HasOne(d => d.story).WithMany(p => p.idea_posts)
                .HasForeignKey(d => d.story_id)
                .HasConstraintName("fk_idea_story");
        });

        modelBuilder.Entity<idea_proposals>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__idea_pro__3213E83F582BB3CE");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_selected).HasDefaultValue(false);
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .HasDefaultValue("ACTIVE");
            entity.Property(e => e.vote_count).HasDefaultValue(0);

            entity.HasOne(d => d.post).WithMany(p => p.idea_proposals)
                .HasForeignKey(d => d.post_id)
                .HasConstraintName("fk_prop_post");
        });

        modelBuilder.Entity<marketing_banners>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__marketin__3213E83FA64C1014");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.position).HasMaxLength(50);
            entity.Property(e => e.priority).HasDefaultValue(0);
            entity.Property(e => e.title).HasMaxLength(255);
        });

        modelBuilder.Entity<moderation_logs>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__moderati__3213E83FB70CE74B");

            entity.Property(e => e.action).HasMaxLength(20);
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.target_type).HasMaxLength(20);

            entity.HasOne(d => d.moderator).WithMany(p => p.moderation_logs)
                .HasForeignKey(d => d.moderator_id)
                .HasConstraintName("fk_mod_user");
        });

        modelBuilder.Entity<moderator_category_assignments>(entity =>
        {
            entity.HasKey(e => new { e.moderator_id, e.category_id });

            entity.Property(e => e.assigned_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.category).WithMany(p => p.moderator_category_assignments)
                .HasForeignKey(d => d.category_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_modcat_category");

            entity.HasOne(d => d.moderator).WithMany(p => p.moderator_category_assignments)
                .HasForeignKey(d => d.moderator_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_modcat_user");
        });

        modelBuilder.Entity<notifications>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__notifica__3213E83F1D4E0D6D");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_read).HasDefaultValue(false);
            entity.Property(e => e.type).HasMaxLength(50);

            entity.HasOne(d => d.user).WithMany(p => p.notifications)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_notif_user");
        });

        modelBuilder.Entity<otp_verifications>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__otp_veri__3213E83F11F0B536");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_used).HasDefaultValue(false);
            entity.Property(e => e.otp_code).HasMaxLength(6);
            entity.Property(e => e.type).HasMaxLength(20);

            entity.HasOne(d => d.user).WithMany(p => p.otp_verifications)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_otp_user");
        });

        modelBuilder.Entity<platform_wallet>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK_platform_wallet");

            entity.Property(e => e.id).ValueGeneratedNever();
            entity.Property(e => e.balance_coin).HasDefaultValue(0);
            entity.Property(e => e.updated_at).HasDefaultValueSql("(sysutcdatetime())");
        });

        modelBuilder.Entity<purchases>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__purchase__3213E83F61537654");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.escrow_status)
                .HasMaxLength(20)
                .HasDefaultValue("NA");
            entity.Property(e => e.platform_fee_ratio)
                .HasDefaultValue(3000m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.purchase_type).HasMaxLength(20);

            entity.HasOne(d => d.story).WithMany(p => p.purchases)
                .HasForeignKey(d => d.story_id)
                .HasConstraintName("fk_purch_story");

            entity.HasOne(d => d.user).WithMany(p => p.purchases)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_purch_user");
        });

        modelBuilder.Entity<ratings>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__ratings__3213E83F11D3A18E");

            entity.HasIndex(e => new { e.user_id, e.story_id }, "uk_ratings").IsUnique();

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .HasDefaultValue("VISIBLE");

            entity.HasOne(d => d.story).WithMany(p => p.ratings)
                .HasForeignKey(d => d.story_id)
                .HasConstraintName("fk_ratings_story");

            entity.HasOne(d => d.user).WithMany(p => p.ratings)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_ratings_user");
        });

        modelBuilder.Entity<report_evidences>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__report_e__3213E83F960D5BAA");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.report).WithMany(p => p.report_evidences)
                .HasForeignKey(d => d.report_id)
                .HasConstraintName("fk_evid_rep");
        });

        modelBuilder.Entity<story_report_contributors>(entity =>
        {
            entity.HasKey(e => new { e.story_id, e.user_id }).HasName("PK_story_report_contributors");

            entity.Property(e => e.reason_category).HasMaxLength(50);
            entity.Property(e => e.description).HasMaxLength(4000);
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");

            entity.HasOne<stories>().WithMany()
                .HasForeignKey(e => e.story_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_story_report_contributors_story");

            entity.HasOne<users>().WithMany()
                .HasForeignKey(e => e.user_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_story_report_contributors_user");
        });

        modelBuilder.Entity<compliance_story_report_lock_requests>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK_compliance_story_report_lock_requests");

            entity.Property(e => e.message).HasMaxLength(2000);
            entity.Property(e => e.status).HasMaxLength(20);
            entity.Property(e => e.resolution_note).HasMaxLength(2000);
            entity.Property(e => e.resolution_action).HasMaxLength(30);
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.urgency_tier).HasMaxLength(20).HasDefaultValue("STANDARD");

            entity.HasOne(d => d.story).WithMany()
                .HasForeignKey(d => d.story_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_csr_lock_req_story");

            entity.HasOne(d => d.requester).WithMany()
                .HasForeignKey(d => d.requester_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_csr_lock_req_requester");

            entity.HasOne(d => d.resolved_byNavigation).WithMany()
                .HasForeignKey(d => d.resolved_by_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_csr_lock_req_resolver");
        });

        modelBuilder.Entity<compliance_admin_action_requests>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK_compliance_admin_action_requests");

            entity.Property(e => e.request_kind).HasMaxLength(40);
            entity.Property(e => e.message).HasMaxLength(2000);
            entity.Property(e => e.status).HasMaxLength(20);
            entity.Property(e => e.resolution_note).HasMaxLength(2000);
            entity.Property(e => e.resolution_action).HasMaxLength(40);
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.urgency_tier).HasMaxLength(20).HasDefaultValue("STANDARD");

            entity.HasOne(d => d.story).WithMany()
                .HasForeignKey(d => d.story_id)
                .OnDelete(DeleteBehavior.Cascade)
                .HasConstraintName("FK_caa_req_story");

            entity.HasOne(d => d.target_user).WithMany()
                .HasForeignKey(d => d.target_user_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_caa_req_target_user");

            entity.HasOne(d => d.requester).WithMany()
                .HasForeignKey(d => d.requester_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_caa_req_requester");

            entity.HasOne(d => d.resolved_byNavigation).WithMany()
                .HasForeignKey(d => d.resolved_by_id)
                .OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_caa_req_resolver");
        });

        modelBuilder.Entity<reports>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__reports__3213E83F8FEA556C");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.reason_category).HasMaxLength(50);
            entity.Property(e => e.contributor_count).HasDefaultValue(1);
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .HasDefaultValue("NEW");
            entity.Property(e => e.target_type).HasMaxLength(20);

            entity.Property(e => e.compliance_resolved_by);

            entity.HasOne(d => d.assigned_toNavigation).WithMany(p => p.reportsassigned_toNavigation)
                .HasForeignKey(d => d.assigned_to)
                .HasConstraintName("fk_rep_assignee");

            entity.HasOne(d => d.reporter).WithMany(p => p.reportsreporter)
                .HasForeignKey(d => d.reporter_id)
                .HasConstraintName("fk_rep_reporter");
        });

        modelBuilder.Entity<review_assignments>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__review_a__3213E83F79677D76");

            entity.HasIndex(e => new { e.assignee_id, e.status }, "IX_review_assignee_status");

            entity.HasIndex(e => new { e.target_type, e.target_id }, "UX_review_active_target")
                .IsUnique()
                .HasFilter("([status]='IN_PROGRESS')");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.assigned_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.assignee_role).HasMaxLength(30);
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .HasDefaultValue("IN_PROGRESS");
            entity.Property(e => e.target_type).HasMaxLength(30);

            entity.HasOne(d => d.assignee).WithMany(p => p.review_assignments)
                .HasForeignKey(d => d.assignee_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_reviewassign_user");
        });

        modelBuilder.Entity<review_escalation_requests>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK_review_escalation_requests");

            entity.HasIndex(e => new { e.target_type, e.target_id, e.status }, "IX_review_escalation_target_status");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.target_type).HasMaxLength(30);
            entity.Property(e => e.request_kind).HasMaxLength(40);
            entity.Property(e => e.reason).HasMaxLength(4000);
            entity.Property(e => e.status).HasMaxLength(20);
            entity.Property(e => e.resolver_note).HasMaxLength(2000);
            entity.Property(e => e.created_at).HasDefaultValueSql("(sysutcdatetime())");
            entity.Property(e => e.sender_urgency_tier).HasMaxLength(20);

            entity.HasOne<users>().WithMany()
                .HasForeignKey(d => d.sender_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_review_sender");

            entity.HasOne<users>().WithMany()
                .HasForeignKey(d => d.resolver_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("FK_review_resolver");
        });

        modelBuilder.Entity<stories>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__stories__3213E83F02458C4B");

            entity.HasIndex(e => e.slug, "UQ__stories__32DD1E4C902D0D0B").IsUnique();

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.age_rating)
                .HasMaxLength(10)
                .HasDefaultValue("ALL");
            entity.Property(e => e.avg_rating)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(3, 2)");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.slug).HasMaxLength(255);
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.story_progress_status)
                .HasMaxLength(20)
                .HasDefaultValue("ONGOING");
            entity.Property(e => e.title).HasMaxLength(255);
            entity.Property(e => e.total_chapters).HasDefaultValue(0);
            entity.Property(e => e.total_favorites).HasDefaultValue(0);
            entity.Property(e => e.total_views).HasDefaultValue(0L);
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.submitted_for_review_at).HasColumnType("datetime2");
            entity.Property(e => e.word_count).HasDefaultValue(0);
            entity.Property(e => e.comments_disabled).HasDefaultValue(false);
            entity.Property(e => e.compliance_hidden).HasDefaultValue(false);
            entity.Property(e => e.compliance_flagged).HasDefaultValue(false);
            entity.Property(e => e.compliance_flag_note).HasMaxLength(1000);
            entity.Property(e => e.compliance_flagged_at).HasColumnType("datetime2");

            entity.HasOne(d => d.author).WithMany(p => p.stories)
                .HasForeignKey(d => d.author_id)
                .HasConstraintName("fk_stories_author");

            entity.HasMany(d => d.category).WithMany(p => p.story)
                .UsingEntity<Dictionary<string, object>>(
                    "story_categories",
                    r => r.HasOne<categories>().WithMany()
                        .HasForeignKey("category_id")
                        .HasConstraintName("fk_sc_category"),
                    l => l.HasOne<stories>().WithMany()
                        .HasForeignKey("story_id")
                        .HasConstraintName("fk_sc_story"),
                    j =>
                    {
                        j.HasKey("story_id", "category_id").HasName("PK__story_ca__3B6772CD221C46DF");
                    });
        });

        modelBuilder.Entity<story_character_memory>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__story_ch__3213E83FC0837ADE");

            entity.HasIndex(e => e.story_id, "IX_story_character_memory_story_id");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.character_name).HasMaxLength(255);
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.story).WithMany(p => p.story_character_memory)
                .HasForeignKey(d => d.story_id)
                .HasConstraintName("fk_character_memory_story");
        });

        modelBuilder.Entity<story_commitments>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__story_co__3213E83FE7805C45");

            entity.HasIndex(e => new { e.story_id, e.user_id }, "uk_story_commitments").IsUnique();

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.ip_address).HasMaxLength(45);
            entity.Property(e => e.policy_version).HasMaxLength(20);
            entity.Property(e => e.signed_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.story).WithMany(p => p.story_commitments)
                .HasForeignKey(d => d.story_id)
                .HasConstraintName("fk_commit_story");

            entity.HasOne(d => d.user).WithMany(p => p.story_commitments)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_commit_user");
        });

        modelBuilder.Entity<story_event_memory>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__story_ev__3213E83F2A834483");

            entity.HasIndex(e => e.story_id, "IX_story_event_memory_story_id");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.chapter).WithMany(p => p.story_event_memory)
                .HasForeignKey(d => d.chapter_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_event_memory_chapter");

            entity.HasOne(d => d.story).WithMany(p => p.story_event_memory)
                .HasForeignKey(d => d.story_id)
                .HasConstraintName("fk_event_memory_story");
        });

        modelBuilder.Entity<story_story_state>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__story_st__3213E83FE42D7272");

            entity.HasIndex(e => e.story_id, "IX_story_story_state_story_id");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.story).WithMany(p => p.story_story_state)
                .HasForeignKey(d => d.story_id)
                .HasConstraintName("fk_story_state_story");
        });

        modelBuilder.Entity<story_versions>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__story_ve__3213E83FFBD7D78B");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.status_snapshot).HasMaxLength(20);
            entity.Property(e => e.title_snapshot).HasMaxLength(255);

            entity.HasOne(d => d.author).WithMany(p => p.story_versions)
                .HasForeignKey(d => d.author_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_storyver_author");

            entity.HasOne(d => d.story).WithMany(p => p.story_versions)
                .HasForeignKey(d => d.story_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_storyver_story");
        });

        modelBuilder.Entity<system_policies>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__system_p__3213E83F26986CC4");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_active).HasDefaultValue(false);
            entity.Property(e => e.require_resign).HasDefaultValue(false);
            entity.Property(e => e.type).HasMaxLength(20);
            entity.Property(e => e.version).HasMaxLength(20);
        });

        modelBuilder.Entity<system_settings>(entity =>
        {
            entity.HasKey(e => e.key).HasName("PK__system_s__DFD83CAEEF9D57AF");

            entity.Property(e => e.key).HasMaxLength(100);
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.value_type).HasMaxLength(20);

            entity.HasOne(d => d.updated_byNavigation).WithMany(p => p.system_settings)
                .HasForeignKey(d => d.updated_by)
                .HasConstraintName("fk_sys_user");
        });

        modelBuilder.Entity<user_activity_logs>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__user_act__3213E83F3131A3CC");

            entity.Property(e => e.action_type).HasMaxLength(50);
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ip_address).HasMaxLength(45);

            entity.HasOne(d => d.user).WithMany(p => p.user_activity_logs)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_activity_user");
        });

        modelBuilder.Entity<user_library>(entity =>
        {
            entity.HasKey(e => new { e.user_id, e.story_id, e.relation_type }).HasName("PK__user_lib__33B829F6A3C07ACB");

            entity.Property(e => e.relation_type).HasMaxLength(20);
            entity.Property(e => e.last_read_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.story).WithMany(p => p.user_library)
                .HasForeignKey(d => d.story_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_lib_story");

            entity.HasOne(d => d.user).WithMany(p => p.user_library)
                .HasForeignKey(d => d.user_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_lib_user");
        });

        modelBuilder.Entity<user_profiles>(entity =>
        {
            entity.HasKey(e => e.user_id).HasName("PK__user_pro__B9BE370F827737D3");

            entity.HasIndex(e => e.nickname, "UQ__user_pro__5CF1C59B5388C815").IsUnique();

            entity.HasIndex(e => e.id_number, "UX_user_profiles_id_number_notnull")
                .IsUnique()
                .HasFilter("([id_number] IS NOT NULL)");

            entity.HasIndex(e => e.phone, "UX_user_profiles_phone_notnull")
                .IsUnique()
                .HasFilter("([phone] IS NOT NULL)");

            entity.Property(e => e.user_id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.id_number).HasMaxLength(50);
            entity.Property(e => e.nickname)
                .HasMaxLength(100)
                .UseCollation("Latin1_General_CI_AS");
            entity.Property(e => e.phone).HasMaxLength(20);
            entity.Property(e => e.settings).HasDefaultValue("{\"allow_notif\": true, \"dark_mode\": false}");
            entity.Property(e => e.social_links).HasDefaultValue("{}");
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.user).WithOne(p => p.user_profiles)
                .HasForeignKey<user_profiles>(d => d.user_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_profiles_user");
        });

        modelBuilder.Entity<user_vouchers>(entity =>
        {
            entity.HasKey(e => new { e.user_id, e.voucher_id }).HasName("PK__user_vou__21B558F5975433EA");

            entity.Property(e => e.applied_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.user).WithMany(p => p.user_vouchers)
                .HasForeignKey(d => d.user_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_uv_user");

            entity.HasOne(d => d.voucher).WithMany(p => p.user_vouchers)
                .HasForeignKey(d => d.voucher_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_uv_vouch");
        });

        modelBuilder.Entity<users>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__users__3213E83F03733555");

            entity.HasIndex(e => e.email, "UQ__users__AB6E6164DDD2A795").IsUnique();

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.email).HasMaxLength(255);
            entity.Property(e => e.must_resign_policy).HasDefaultValue(false);
            entity.Property(e => e.role)
                .HasMaxLength(20)
                .HasDefaultValue("USER");
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .HasDefaultValue("PENDING");
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.author_writing_suspended_until).HasColumnType("datetime2");

            entity.HasMany(d => d.comment).WithMany(p => p.user)
                .UsingEntity<Dictionary<string, object>>(
                    "comment_likes",
                    r => r.HasOne<comments>().WithMany()
                        .HasForeignKey("comment_id")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_clikes_comm"),
                    l => l.HasOne<users>().WithMany()
                        .HasForeignKey("user_id")
                        .OnDelete(DeleteBehavior.ClientSetNull)
                        .HasConstraintName("fk_clikes_user"),
                    j =>
                    {
                        j.HasKey("user_id", "comment_id").HasName("PK__comment___D7C76067480249F4");
                    });
        });

        modelBuilder.Entity<violation_logs>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__violatio__3213E83FE19ADD18");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_appealed).HasDefaultValue(false);
            entity.Property(e => e.is_refunded).HasDefaultValue(false);
            entity.Property(e => e.penalty_type).HasMaxLength(50);
            entity.Property(e => e.policy_reference).HasMaxLength(100);
            entity.Property(e => e.target_type).HasMaxLength(20);
            entity.Property(e => e.total_refunded_amount).HasDefaultValue(0);

            entity.HasOne(d => d.compliance_officer).WithMany(p => p.violation_logscompliance_officer)
                .HasForeignKey(d => d.compliance_officer_id)
                .HasConstraintName("fk_viol_officer");

            entity.HasOne(d => d.violator).WithMany(p => p.violation_logsviolator)
                .HasForeignKey(d => d.violator_id)
                .HasConstraintName("fk_viol_user");
        });

        modelBuilder.Entity<vouchers>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__vouchers__3213E83F94C31AE6");

            entity.HasIndex(e => e.code, "UQ__vouchers__357D4CF905F6CEA2").IsUnique();

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.code).HasMaxLength(50);
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.max_discount_amount).HasColumnType("decimal(15, 2)");
            entity.Property(e => e.min_order_value).HasDefaultValue(0);
            entity.Property(e => e.type).HasMaxLength(20);
            entity.Property(e => e.usage_limit).HasDefaultValue(1);
            entity.Property(e => e.used_count).HasDefaultValue(0);
            entity.Property(e => e.value).HasColumnType("decimal(15, 2)");
        });

        modelBuilder.Entity<wallets>(entity =>
        {
            entity.HasKey(e => e.user_id).HasName("PK__wallets__B9BE370F9D95AA58");

            entity.Property(e => e.user_id).ValueGeneratedNever();
            entity.Property(e => e.balance_coin).HasDefaultValue(0);
            entity.Property(e => e.currency)
                .HasMaxLength(10)
                .HasDefaultValue("VND");
            entity.Property(e => e.frozen_balance)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(15, 2)");
            entity.Property(e => e.income_balance)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(15, 2)");
            entity.Property(e => e.pending_escrow_balance)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(15, 2)");
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.user).WithOne(p => p.wallets)
                .HasForeignKey<wallets>(d => d.user_id)
                .OnDelete(DeleteBehavior.ClientSetNull)
                .HasConstraintName("fk_wallets_user");
        });

        modelBuilder.Entity<withdraw_requests>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__withdraw__3213E83F2F69A0FC");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.amount_requested).HasColumnType("decimal(15, 2)");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.fee_amount)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(15, 2)");
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .HasDefaultValue("PENDING");

            entity.HasOne(d => d.author).WithMany(p => p.withdraw_requestsauthor)
                .HasForeignKey(d => d.author_id)
                .HasConstraintName("fk_with_auth");

            entity.HasOne(d => d.processed_byNavigation).WithMany(p => p.withdraw_requestsprocessed_byNavigation)
                .HasForeignKey(d => d.processed_by)
                .HasConstraintName("fk_with_admin");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
