using System;
using System.Collections.Generic;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessObjects.DataAccessObjects.Context;

public partial class StoryPlatformDbContext : DbContext
{
    public StoryPlatformDbContext()
    {
    }

    public StoryPlatformDbContext(DbContextOptions<StoryPlatformDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<auth_token> auth_tokens { get; set; }

    public virtual DbSet<category> categories { get; set; }

    public virtual DbSet<chapter> chapters { get; set; }

    public virtual DbSet<story> stories { get; set; }

    public virtual DbSet<user> users { get; set; }

    public virtual DbSet<user_profile> user_profiles { get; set; }

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
        modelBuilder.Entity<auth_token>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__auth_tok__3213E83FE7E5A23D");

            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.user).WithMany(p => p.auth_tokens).HasConstraintName("fk_token_user");
        });

        modelBuilder.Entity<category>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__categori__3213E83F339C9A1E");

            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_active).HasDefaultValue(true);
        });

        modelBuilder.Entity<chapter>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__chapters__3213E83FC7C788E2");

            entity.Property(e => e.access_type).HasDefaultValue("FREE");
            entity.Property(e => e.ai_contribution_ratio).HasDefaultValue(0m);
            entity.Property(e => e.coin_price).HasDefaultValue(0);
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_ai_clean).HasDefaultValue(false);
            entity.Property(e => e.status).HasDefaultValue("DRAFT");
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.word_count).HasDefaultValue(0);

            entity.HasOne(d => d.story).WithMany(p => p.chapters).HasConstraintName("fk_chapter_story");
        });

        modelBuilder.Entity<story>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__stories__3213E83F4F3A94B4");

            entity.Property(e => e.access_type).HasDefaultValue("FREE");
            entity.Property(e => e.age_rating).HasDefaultValue("ALL");
            entity.Property(e => e.avg_rating).HasDefaultValue(0m);
            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.expected_chapters).HasDefaultValue(0);
            entity.Property(e => e.full_story_price).HasDefaultValue(0);
            entity.Property(e => e.release_frequency_days).HasDefaultValue(7);
            entity.Property(e => e.sale_type).HasDefaultValue("CHAPTER");
            entity.Property(e => e.status).HasDefaultValue("DRAFT");
            entity.Property(e => e.total_chapters).HasDefaultValue(0);
            entity.Property(e => e.total_favorites).HasDefaultValue(0);
            entity.Property(e => e.total_views).HasDefaultValue(0L);
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.word_count).HasDefaultValue(0);

            entity.HasOne(d => d.author).WithMany(p => p.stories)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_story_author");

            entity.HasOne(d => d.category).WithMany(p => p.stories)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_story_category");
        });

        modelBuilder.Entity<user>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__users__3213E83F36BD1CEF");

            entity.Property(e => e.created_at).HasDefaultValueSql("(getdate())");
            entity.Property(e => e.is_author).HasDefaultValue(false);
            entity.Property(e => e.must_resign_policy).HasDefaultValue(false);
            entity.Property(e => e.role).HasDefaultValue("USER");
            entity.Property(e => e.status).HasDefaultValue("PENDING");
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");
        });

        modelBuilder.Entity<user_profile>(entity =>
        {
            entity.HasKey(e => e.id).HasName("PK__user_pro__3213E83F74CD80A6");

            entity.Property(e => e.settings).HasDefaultValue("{\"allow_notif\":true,\"dark_mode\":false}");
            entity.Property(e => e.social_links).HasDefaultValue("{}");
            entity.Property(e => e.updated_at).HasDefaultValueSql("(getdate())");

            entity.HasOne(d => d.user).WithOne(p => p.user_profile).HasConstraintName("fk_profile_user");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
