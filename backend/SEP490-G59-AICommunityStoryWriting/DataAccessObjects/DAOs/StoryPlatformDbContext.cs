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

    public virtual DbSet<auth_tokens> auth_tokens { get; set; }

    public virtual DbSet<categories> categories { get; set; }

    public virtual DbSet<chapters> chapters { get; set; }

    public virtual DbSet<stories> stories { get; set; }

    public virtual DbSet<user_profiles> user_profiles { get; set; }

    public virtual DbSet<users> users { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            optionsBuilder.UseSqlServer(
                "Server=.;Database=story_platform;Trusted_Connection=True;TrustServerCertificate=True");
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<auth_tokens>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__auth_tok__3213E83F732E5C4E");

            entity.HasIndex(e => e.refresh_token, "uq_refresh_token").IsUnique();

            entity.Property(e => e.id)
                .HasMaxLength(36)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.created_at)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.expires_at).HasColumnType("datetime");
            entity.Property(e => e.refresh_token)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.user_id)
                .HasMaxLength(36)
                .IsUnicode(false)
                .IsFixedLength();

            entity.HasOne(d => d.user).WithMany(p => p.auth_tokens)
                .HasForeignKey(d => d.user_id)
                .HasConstraintName("fk_token_user");
        });

        modelBuilder.Entity<categories>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__categori__3213E83F429B8052");

            entity.HasIndex(e => e.slug, "UQ__categori__32DD1E4C8D2DA8E1").IsUnique();

            entity.Property(e => e.created_at)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.is_active).HasDefaultValue(true);
            entity.Property(e => e.name)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.slug)
                .HasMaxLength(100)
                .IsUnicode(false);
        });

        modelBuilder.Entity<chapters>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__chapters__3213E83F3640A924");

            entity.HasIndex(e => new { e.story_id, e.order_index }, "uq_story_chapter").IsUnique();

            entity.Property(e => e.id)
                .HasMaxLength(36)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.access_type)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("FREE");
            entity.Property(e => e.ai_contribution_ratio)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)");
            entity.Property(e => e.coin_price).HasDefaultValue(0);
            entity.Property(e => e.created_at)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.is_ai_clean).HasDefaultValue(false);
            entity.Property(e => e.published_at).HasColumnType("datetime");
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.story_id)
                .HasMaxLength(36)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.title)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.updated_at)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.word_count).HasDefaultValue(0);

            entity.HasOne(d => d.story).WithMany(p => p.chapters)
                .HasForeignKey(d => d.story_id)
                .HasConstraintName("fk_chapter_story");
        });

        modelBuilder.Entity<stories>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__stories__3213E83FD5087FFA");

            entity.HasIndex(e => e.slug, "UQ__stories__32DD1E4C1C8D6999").IsUnique();

            entity.Property(e => e.id)
                .HasMaxLength(36)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.access_type)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("FREE");
            entity.Property(e => e.age_rating)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("ALL");
            entity.Property(e => e.author_id)
                .HasMaxLength(36)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.avg_rating)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(3, 2)");
            entity.Property(e => e.created_at)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.expected_chapters).HasDefaultValue(0);
            entity.Property(e => e.full_story_price).HasDefaultValue(0);
            entity.Property(e => e.last_published_at).HasColumnType("datetime");
            entity.Property(e => e.published_at).HasColumnType("datetime");
            entity.Property(e => e.release_frequency_days).HasDefaultValue(7);
            entity.Property(e => e.sale_type)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("CHAPTER");
            entity.Property(e => e.slug)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT");
            entity.Property(e => e.title)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.total_chapters).HasDefaultValue(0);
            entity.Property(e => e.total_favorites).HasDefaultValue(0);
            entity.Property(e => e.total_views).HasDefaultValue(0L);
            entity.Property(e => e.updated_at)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.word_count).HasDefaultValue(0);

            entity.HasOne(d => d.author).WithMany(p => p.stories)
                .HasForeignKey(d => d.author_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_story_author");

            entity.HasOne(d => d.category).WithMany(p => p.stories)
                .HasForeignKey(d => d.category_id)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_story_category");
        });

        modelBuilder.Entity<user_profiles>(entity =>
        {
            entity.HasKey(e => e.user_id).HasName("PK__user_pro__B9BE370F8BF78B4C");

            entity.HasIndex(e => e.nickname, "UQ__user_pro__5CF1C59B26A6DDD8").IsUnique();

            entity.Property(e => e.user_id)
                .HasMaxLength(36)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.nickname)
                .HasMaxLength(100)
                .IsUnicode(false);
            entity.Property(e => e.settings).HasDefaultValue("{\"allow_notif\":true,\"dark_mode\":false}");
            entity.Property(e => e.social_links).HasDefaultValue("{}");
            entity.Property(e => e.updated_at)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");

            entity.HasOne(d => d.user).WithOne(p => p.user_profiles)
                .HasForeignKey<user_profiles>(d => d.user_id)
                .HasConstraintName("fk_profile_user");
        });

        modelBuilder.Entity<users>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__users__3213E83F83911EE7");

            entity.HasIndex(e => e.email, "UQ__users__AB6E6164AB54604B").IsUnique();

            entity.Property(e => e.id)
                .HasMaxLength(36)
                .IsUnicode(false)
                .IsFixedLength();
            entity.Property(e => e.created_at)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
            entity.Property(e => e.deletion_requested_at).HasColumnType("datetime");
            entity.Property(e => e.email)
                .HasMaxLength(255)
                .IsUnicode(false);
            entity.Property(e => e.email_verified_at).HasColumnType("datetime");
            entity.Property(e => e.is_author).HasDefaultValue(false);
            entity.Property(e => e.must_resign_policy).HasDefaultValue(false);
            entity.Property(e => e.role)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("USER");
            entity.Property(e => e.status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING");
            entity.Property(e => e.updated_at)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
