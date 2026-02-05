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

    public virtual DbSet<ai_model_registry> ai_model_registry { get; set; }

    public virtual DbSet<ai_plagiarism_reports> ai_plagiarism_reports { get; set; }

    public virtual DbSet<ai_sensitive_words> ai_sensitive_words { get; set; }

    public virtual DbSet<ai_usage_logs> ai_usage_logs { get; set; }

    public virtual DbSet<appeals> appeals { get; set; }

    public virtual DbSet<auth_tokens> auth_tokens { get; set; }

    public virtual DbSet<author_bank_accounts> author_bank_accounts { get; set; }

    public virtual DbSet<author_income_logs> author_income_logs { get; set; }

    public virtual DbSet<categories> categories { get; set; }

    public virtual DbSet<chapter_versions> chapter_versions { get; set; }

    public virtual DbSet<chapters> chapters { get; set; }

    public virtual DbSet<coin_orders> coin_orders { get; set; }

    public virtual DbSet<coin_packages> coin_packages { get; set; }

    public virtual DbSet<comments> comments { get; set; }

    public virtual DbSet<daily_statistics> daily_statistics { get; set; }

    public virtual DbSet<donations> donations { get; set; }

    public virtual DbSet<follows> follows { get; set; }

    public virtual DbSet<idea_posts> idea_posts { get; set; }

    public virtual DbSet<idea_proposals> idea_proposals { get; set; }

    public virtual DbSet<marketing_banners> marketing_banners { get; set; }

    public virtual DbSet<moderation_logs> moderation_logs { get; set; }

    public virtual DbSet<notifications> notifications { get; set; }

    public virtual DbSet<otp_verifications> otp_verifications { get; set; }

    public virtual DbSet<purchases> purchases { get; set; }

    public virtual DbSet<ratings> ratings { get; set; }

    public virtual DbSet<report_evidences> report_evidences { get; set; }

    public virtual DbSet<reports> reports { get; set; }

    public virtual DbSet<stories> stories { get; set; }

    public virtual DbSet<story_commitments> story_commitments { get; set; }

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
                "Server=.;Database=story_platform_v6;Trusted_Connection=True;TrustServerCertificate=True");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<admin_audit_logs>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__admin_au__3213E83F1AC5D3D4");

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
            entity.HasKey(e => e.key).HasName("PK__ai_confi__DFD83CAECA916319");

            entity.Property(e => e.key).HasMaxLength(50);
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<ai_generated_content>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__ai_gener__3213E83F961871BE");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.similarity_score).HasColumnType("decimal(5, 2)");

            entity.HasOne(d => d.chapter).WithMany(p => p.ai_generated_content)
                .HasForeignKey(d => d.chapter_id)
                .HasConstraintName("fk_aigen_chapter");
        });

        modelBuilder.Entity<ai_model_registry>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__ai_model__3213E83F6AC58A3C");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.is_default_for_gen).HasDefaultValue(false);
            entity.Property(e => e.is_default_for_mod).HasDefaultValue(false);
            entity.Property(e => e.model_name).HasMaxLength(50);
            entity.Property(e => e.provider).HasMaxLength(50);
            entity.Property(e => e.temperature).HasColumnType("decimal(3, 2)");
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<ai_plagiarism_reports>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__ai_plagi__3213E83F72577CCD");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.checked_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.similarity_ratio).HasColumnType("decimal(5, 2)");
            entity.Property(e => e.status).HasMaxLength(20);

            entity.HasOne(d => d.chapter).WithMany(p => p.ai_plagiarism_reports)
                .HasForeignKey(d => d.chapter_id)
                .HasConstraintName("fk_plag_chapter");
        });

        modelBuilder.Entity<ai_sensitive_words>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__ai_sensi__3213E83F5C3C2178");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.category).HasMaxLength(50);
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.word).HasMaxLength(100);
        });

        modelBuilder.Entity<ai_usage_logs>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__ai_usage__3213E83F6D964185");

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
            entity.HasKey(e => e.id).HasName("PK__appeals__3213E83FA1C34B96");

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
            entity.HasKey(e => e.id).HasName("PK__auth_tok__3213E83FFC053B92");

            entity.HasIndex(e => e.refresh_token, "UQ__auth_tok__7FB69BADCA111301").IsUnique();

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.refresh_token).HasMaxLength(500);

            entity.HasOne(d => d.user).WithMany(p => p.auth_tokens)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_auth_user");
        });

        modelBuilder.Entity<author_bank_accounts>(entity =>
        {
            entity.HasKey(e => e.user_id).HasName("PK__author_b__B9BE370F689F6393");

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
            entity.HasKey(e => e.id).HasName("PK__author_i__3213E83FFED67314");

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

        modelBuilder.Entity<categories>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__categori__3213E83F9591669A");

            entity.HasIndex(e => e.slug, "UQ__categori__32DD1E4C6CFF2CEF").IsUnique();

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.name).HasMaxLength(100);
            entity.Property(e => e.slug).HasMaxLength(100);

            entity.HasOne(d => d.parent_category).WithMany(p => p.Inverseparent_category)
                .HasForeignKey(d => d.parent_id)
                .HasConstraintName("fk_category_parent");
        });

        modelBuilder.Entity<chapter_versions>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__chapter___3213E83F351819DD");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.author).WithMany(p => p.chapter_versions)
                .HasForeignKey(d => d.author_id)
                .HasConstraintName("fk_versions_author");

            entity.HasOne(d => d.chapter).WithMany(p => p.chapter_versions)
                .HasForeignKey(d => d.chapter_id)
                .HasConstraintName("fk_versions_chapter");
        });

        modelBuilder.Entity<chapters>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__chapters__3213E83FC9E5BEA2");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.access_type)
                .HasMaxLength(20)
                .HasDefaultValue("FREE");
            entity.Property(e => e.ai_contribution_ratio)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.coin_price).HasDefaultValue(0);
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_ai_clean).HasDefaultValue(false);
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.title).HasMaxLength(255);
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.word_count).HasDefaultValue(0);

            entity.HasOne(d => d.story).WithMany(p => p.chapters)
                .HasForeignKey(d => d.story_id)
                .HasConstraintName("fk_chapters_story");
        });

        modelBuilder.Entity<coin_orders>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__coin_ord__3213E83FF25CC481");

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
            entity.HasKey(e => e.id).HasName("PK__coin_pac__3213E83FD27D473B");

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

        modelBuilder.Entity<comments>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__comments__3213E83F52DCAB20");

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
            entity.HasKey(e => e.stat_date).HasName("PK__daily_st__38B70DF950648CFA");

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
            entity.HasKey(e => e.id).HasName("PK__donation__3213E83FF7E6C71F");

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
            entity.HasKey(e => new { e.user_id, e.author_id }).HasName("PK__follows__51DB21B33DE17101");

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
            entity.HasKey(e => e.id).HasName("PK__idea_pos__3213E83F1F1491AE");

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
            entity.HasKey(e => e.id).HasName("PK__idea_pro__3213E83FC8DEAD81");

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
            entity.HasKey(e => e.id).HasName("PK__marketin__3213E83F45587987");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.position).HasMaxLength(50);
            entity.Property(e => e.priority).HasDefaultValue(0);
            entity.Property(e => e.title).HasMaxLength(255);
        });

        modelBuilder.Entity<moderation_logs>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__moderati__3213E83FF3FE8436");

            entity.Property(e => e.action).HasMaxLength(20);
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.target_type).HasMaxLength(20);

            entity.HasOne(d => d.moderator).WithMany(p => p.moderation_logs)
                .HasForeignKey(d => d.moderator_id)
                .HasConstraintName("fk_mod_user");
        });

        modelBuilder.Entity<notifications>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__notifica__3213E83FE90EC405");

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
            entity.HasKey(e => e.id).HasName("PK__otp_veri__3213E83FD07CAB7E");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_used).HasDefaultValue(false);
            entity.Property(e => e.otp_code).HasMaxLength(6);
            entity.Property(e => e.type).HasMaxLength(20);

            entity.HasOne(d => d.user).WithMany(p => p.otp_verifications)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_otp_user");
        });

        modelBuilder.Entity<purchases>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__purchase__3213E83FC86F570D");

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
            entity.HasKey(e => e.id).HasName("PK__ratings__3213E83FD1FE086F");

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
            entity.HasKey(e => e.id).HasName("PK__report_e__3213E83FD6F34C0C");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");

            entity.HasOne(d => d.report).WithMany(p => p.report_evidences)
                .HasForeignKey(d => d.report_id)
                .HasConstraintName("fk_evid_rep");
        });

        modelBuilder.Entity<reports>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__reports__3213E83FDA1BA60C");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.reason_category).HasMaxLength(50);
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .HasDefaultValue("NEW");
            entity.Property(e => e.target_type).HasMaxLength(20);

            entity.HasOne(d => d.assigned_toNavigation).WithMany(p => p.reportsassigned_toNavigation)
                .HasForeignKey(d => d.assigned_to)
                .HasConstraintName("fk_rep_assignee");

            entity.HasOne(d => d.reporter).WithMany(p => p.reportsreporter)
                .HasForeignKey(d => d.reporter_id)
                .HasConstraintName("fk_rep_reporter");
        });

        modelBuilder.Entity<stories>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__stories__3213E83FC03FD3BD");

            entity.HasIndex(e => e.slug, "UQ__stories__32DD1E4CB6F2F9BA").IsUnique();

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
            entity.Property(e => e.word_count).HasDefaultValue(0);

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
                        j.HasKey("story_id", "category_id").HasName("PK__story_ca__3B6772CD61F08E66");
                    });
        });

        modelBuilder.Entity<story_commitments>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__story_co__3213E83F8823F4B1");

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

        modelBuilder.Entity<system_policies>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__system_p__3213E83F9DB9CDC5");

            entity.Property(e => e.id).HasDefaultValueSql("(newid())");
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_active).HasDefaultValue(false);
            entity.Property(e => e.require_resign).HasDefaultValue(false);
            entity.Property(e => e.type).HasMaxLength(20);
            entity.Property(e => e.version).HasMaxLength(20);
        });

        modelBuilder.Entity<system_settings>(entity =>
        {
            entity.HasKey(e => e.key).HasName("PK__system_s__DFD83CAEA819FDF2");

            entity.Property(e => e.key).HasMaxLength(100);
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.value_type).HasMaxLength(20);

            entity.HasOne(d => d.updated_byNavigation).WithMany(p => p.system_settings)
                .HasForeignKey(d => d.updated_by)
                .HasConstraintName("fk_sys_user");
        });

        modelBuilder.Entity<user_activity_logs>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__user_act__3213E83F616A7B22");

            entity.Property(e => e.action_type).HasMaxLength(50);
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.ip_address).HasMaxLength(45);

            entity.HasOne(d => d.user).WithMany(p => p.user_activity_logs)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_activity_user");
        });

        modelBuilder.Entity<user_library>(entity =>
        {
            entity.HasKey(e => new { e.user_id, e.story_id, e.relation_type }).HasName("PK__user_lib__33B829F6CB79BBE5");

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
            entity.HasKey(e => e.user_id).HasName("PK__user_pro__B9BE370F23866C3D");

            entity.HasIndex(e => e.nickname, "UQ__user_pro__5CF1C59B1FECD00B").IsUnique();

            entity.Property(e => e.user_id).ValueGeneratedNever();
            entity.Property(e => e.nickname)
                .HasMaxLength(100)
                .UseCollation("Latin1_General_CI_AS");
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
            entity.HasKey(e => new { e.user_id, e.voucher_id }).HasName("PK__user_vou__21B558F5258B4F04");

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
            entity.HasKey(e => e.id).HasName("PK__users__3213E83F8B24A17B");

            entity.HasIndex(e => e.email, "UQ__users__AB6E6164B8ADEF76").IsUnique();

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
                        j.HasKey("user_id", "comment_id").HasName("PK__comment___D7C76067174594E4");
                    });
        });

        modelBuilder.Entity<violation_logs>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__violatio__3213E83F983E11C7");

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
            entity.HasKey(e => e.id).HasName("PK__vouchers__3213E83F12031197");

            entity.HasIndex(e => e.code, "UQ__vouchers__357D4CF99A1B38A1").IsUnique();

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
            entity.HasKey(e => e.user_id).HasName("PK__wallets__B9BE370FE677FE3D");

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
            entity.HasKey(e => e.id).HasName("PK__withdraw__3213E83F396A016F");

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
